/*
 * Copyright (c) 2018-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of ChargyCore <https://github.com/OpenChargingCloud/ChargyCore.NET>
 *
 * Licensed under the Affero GPL license, Version 3.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.gnu.org/licenses/agpl.html
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System.Text;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.Formats.OCMF
{

    /// <summary>
    /// Checks the signature of an OCMF document.
    ///
    /// OCMF leaves a great deal open. The public key arrives from outside the
    /// document — from a separate file, from the container, or from the charge
    /// point operator's backend — and the standard does not say how it is
    /// encoded, so hexadecimal, base32 and base64 all occur and have to be told
    /// apart by inspection. The signature may be hexadecimal or base64. And the
    /// algorithm name carries both the curve and the digest, including two
    /// combinations that are cryptographically wrong but were nevertheless used
    /// to sign real charging sessions.
    ///
    /// Every one of those is a place where guessing wrong means telling an EV
    /// driver their receipt is invalid when it is not. So each failure is
    /// reported as its own kind: an unknown algorithm, an undecodable key and a
    /// signature that genuinely does not match are three different statements.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    public partial class OCMFSignatureValidator(I18NDictionary I18N)
    {

        #region Data

        private readonly I18NDictionary i18n = I18N;

        #endregion


        #region Validate(Document, PublicKey, PublicKeyEncoding = null)

        /// <summary>
        /// Check the signature of an OCMF document against the given public key.
        /// </summary>
        /// <param name="Document">An OCMF document.</param>
        /// <param name="PublicKey">The public key of the energy meter.</param>
        /// <param name="PublicKeyEncoding">How the public key is encoded, when known.</param>
        public VerificationResult Validate(OCMFDocument  Document,
                                           String?       PublicKey,
                                           String?       PublicKeyEncoding = null)
        {

            if (PublicKey is null || PublicKey.Length == 0)
            {
                Document.ValidationStatus = VerificationResult.PublicKeyNotFound;
                return Document.ValidationStatus;
            }

            if (Document.SignatureAlgorithm is not OCMFSignatureAlgorithm algorithm)
            {
                AddError(Document, "Verification_UnknownSignatureFormat");
                Document.ValidationStatus = VerificationResult.UnknownSignatureFormat;
                return Document.ValidationStatus;
            }

            Document.PublicKey = PublicKey;

            return algorithm.SignsMessageDirectly
                       ? ValidateDirectlySigned(Document, algorithm, PublicKey, PublicKeyEncoding)
                       : ValidateECDSA         (Document, algorithm, PublicKey, PublicKeyEncoding);

        }

        #endregion


        #region (private) ValidateDirectlySigned(Document, Algorithm, PublicKey, PublicKeyEncoding)

        /// <summary>
        /// Check an EdDSA or ML-DSA signature.
        ///
        /// These sign the payload itself rather than a digest of it, so the raw
        /// payload bytes go to the signature suite unchanged.
        /// </summary>
        private VerificationResult ValidateDirectlySigned(OCMFDocument            Document,
                                                          OCMFSignatureAlgorithm  Algorithm,
                                                          String                  PublicKey,
                                                          String?                 PublicKeyEncoding)
        {

            try
            {

                var suite = SignatureSuites.TryGet(Algorithm.SuiteName);

                if (suite is null)
                {
                    AddError(Document, "Verification_UnknownSignatureFormat", Algorithm.Name);
                    Document.ValidationStatus = VerificationResult.UnknownSignatureFormat;
                    return Document.ValidationStatus;
                }

                var (publicKeyBytes, encoding) = DecodePublicKey(PublicKey, PublicKeyEncoding);

                if (publicKeyBytes is null)
                {
                    Document.ValidationStatus = VerificationResult.UnknownPublicKeyFormat;
                    return Document.ValidationStatus;
                }

                Document.PublicKeyEncoding = encoding;

                if (Document.SignatureBytes is not Byte[] signatureBytes)
                {
                    AddError(Document, "Verification_SignatureMalformed");
                    Document.ValidationStatus = VerificationResult.InvalidSignature;
                    return Document.ValidationStatus;
                }

                var valid = suite.Verify(
                                Encoding.UTF8.GetBytes(Document.RawPayload),
                                signatureBytes,
                                publicKeyBytes
                            );

                if (!valid)
                    AddError(Document, "Verification_SignatureMismatch");

                Document.ValidationStatus = valid
                                                ? VerificationResult.ValidSignature
                                                : VerificationResult.InvalidSignature;

                return Document.ValidationStatus;

            }
            catch (Exception exception)
            {
                AddError(Document, "Verification_SignatureMalformed", exception);
                Document.ValidationStatus = VerificationResult.InvalidSignature;
                return Document.ValidationStatus;
            }

        }

        #endregion

        #region (private) ValidateECDSA         (Document, Algorithm, PublicKey, PublicKeyEncoding)

        /// <summary>
        /// Check an ECDSA signature over the hashed payload.
        /// </summary>
        private VerificationResult ValidateECDSA(OCMFDocument            Document,
                                                 OCMFSignatureAlgorithm  Algorithm,
                                                 String                  PublicKey,
                                                 String?                 PublicKeyEncoding)
        {

            #region The curve

            // Chargy recognises more algorithm names than it can verify. Saying
            // so plainly is better than reporting a bad signature for a curve
            // this library simply does not implement.
            var curve = ECCurveVerifier.TryGet(Algorithm.CurveName);

            if (curve is null)
            {
                AddError(Document, "Verification_PublicKeyDecodingFailed", $"Unsupported ECC curve '{Algorithm.Name}'!");
                Document.ValidationStatus = VerificationResult.InvalidPublicKey;
                return Document.ValidationStatus;
            }

            #endregion

            #region The public key

            ECVerificationKey? verificationKey;

            try
            {

                var (publicKeyBytes, encoding) = DecodePublicKey(PublicKey, PublicKeyEncoding);

                if (publicKeyBytes is null)
                {
                    Document.ValidationStatus = VerificationResult.UnknownPublicKeyFormat;
                    return Document.ValidationStatus;
                }

                Document.PublicKeyEncoding = encoding;

                // An OCMF public key is a DER encoded SubjectPublicKeyInfo holding
                // an uncompressed point.
                var parsed = PublicKeyParser.TryParseDER(publicKeyBytes);

                if (parsed is null)
                {
                    AddError(Document, "Verification_PublicKeyDecodingFailed");
                    Document.ValidationStatus = VerificationResult.InvalidPublicKey;
                    return Document.ValidationStatus;
                }

                // Fails when the point does not lie on the curve the algorithm named.
                verificationKey = curve.ParsePublicKey(Convert.ToHexStringLower(parsed.Value));

                if (verificationKey is null)
                {
                    AddError(Document, "Verification_PublicKeyDecodingFailed");
                    Document.ValidationStatus = VerificationResult.InvalidPublicKey;
                    return Document.ValidationStatus;
                }

                SetPublicKeyCoordinates(Document, parsed.Value, curve.CoordinateLength);

            }
            catch (Exception exception)
            {
                AddError(Document, "Verification_PublicKeyDecodingFailed", exception);
                Document.ValidationStatus = VerificationResult.InvalidPublicKey;
                return Document.ValidationStatus;
            }

            #endregion

            #region The signature itself

            try
            {

                if (Document.SignatureRS is not SignatureRS signature)
                {
                    AddError(Document, "Verification_SignatureMalformed");
                    Document.ValidationStatus = VerificationResult.InvalidSignature;
                    return Document.ValidationStatus;
                }

                var valid = verificationKey.Verify(
                                Document.HashValue,
                                signature.R,
                                signature.S
                            );

                if (!valid)
                    AddError(Document, "Verification_SignatureMismatch");

                Document.ValidationStatus = valid
                                                ? VerificationResult.ValidSignature
                                                : VerificationResult.InvalidSignature;

                return Document.ValidationStatus;

            }
            catch (Exception exception)
            {
                AddError(Document, "Verification_SignatureMalformed", exception);
                Document.ValidationStatus = VerificationResult.InvalidSignature;
                return Document.ValidationStatus;
            }

            #endregion

        }

        #endregion


        #region (private, static) DecodePublicKey(PublicKey, PublicKeyEncoding)

        /// <summary>
        /// Decode a public key, working out how it was encoded when nobody said.
        ///
        /// OCMF does not prescribe an encoding for the public key, and all three
        /// in use are made of characters the others also use — so the order the
        /// candidates are tried in decides the answer for short keys. It is the
        /// order ChargyCore.TS uses, and it must stay that way: changing it would
        /// silently change which keys decode.
        /// </summary>
        /// <param name="PublicKey">The public key, as text.</param>
        /// <param name="PublicKeyEncoding">How it is encoded, when known.</param>
        private static (Byte[]? Bytes, String? Encoding) DecodePublicKey(String   PublicKey,
                                                                         String?  PublicKeyEncoding)
        {

            #region The encoding the caller stated

            if (PublicKeyEncoding is not null && PublicKeyEncoding.Length > 0)
            {

                var stated = PublicKeyEncoding.ToLowerInvariant();

                var bytes = stated switch {
                                "hex"     => TryDecode(() => Convert.FromHexString(PublicKey)),
                                "base32"  => TryDecode(() => PublicKey.FromBASE32()),
                                "base64"  => TryDecode(() => Convert.FromBase64String(PublicKey)),
                                _         => null
                            };

                if (bytes is not null)
                    return (bytes, stated);

            }

            #endregion

            #region ..., or the encoding the key looks like

            if (HexRegex().IsMatch(PublicKey) &&
                TryDecode(() => Convert.FromHexString(PublicKey)) is Byte[] fromHex)
                return (fromHex, "hex");

            if (Base32Regex().IsMatch(PublicKey) &&
                TryDecode(() => PublicKey.FromBASE32()) is Byte[] fromBase32)
                return (fromBase32, "base32");

            if (Base64Regex().IsMatch(PublicKey) &&
                TryDecode(() => Convert.FromBase64String(PublicKey)) is Byte[] fromBase64)
                return (fromBase64, "base64");

            #endregion

            return (null, null);

        }

        #endregion

        #region (private, static) SetPublicKeyCoordinates(Document, PublicKeyBytes, CoordinateLength)

        /// <summary>
        /// Record the two coordinates of the public key, for the verification trace.
        /// </summary>
        /// <param name="Document">An OCMF document.</param>
        /// <param name="PublicKeyBytes">The uncompressed SEC1 point.</param>
        /// <param name="CoordinateLength">The length of one coordinate in bytes.</param>
        private static void SetPublicKeyCoordinates(OCMFDocument  Document,
                                                    Byte[]        PublicKeyBytes,
                                                    Int32         CoordinateLength)
        {

            // The leading 0x04 marks an uncompressed point; the two coordinates follow.
            if (PublicKeyBytes.Length != 1 + 2 * CoordinateLength ||
                PublicKeyBytes[0]     != 0x04)
            {
                return;
            }

            Document.PublicKeyX = Convert.ToHexStringLower(PublicKeyBytes.AsSpan(1,                    CoordinateLength));
            Document.PublicKeyY = Convert.ToHexStringLower(PublicKeyBytes.AsSpan(1 + CoordinateLength, CoordinateLength));

        }

        #endregion

        #region (private, static) TryDecode      (Decode)

        /// <summary>
        /// Run a decoder, treating a failure as "not this encoding".
        /// </summary>
        /// <param name="Decode">A decoder.</param>
        private static Byte[]? TryDecode(Func<Byte[]> Decode)
        {

            try
            {
                return Decode();
            }
            catch (Exception)
            {
                return null;
            }

        }

        #endregion

        #region (private) AddError               (Document, ReasonKey, Detail = null)

        /// <summary>
        /// Record why the verification of an OCMF document failed.
        /// </summary>
        /// <param name="Document">An OCMF document.</param>
        /// <param name="ReasonKey">The i18n key of the reason.</param>
        /// <param name="Detail">An optional technical detail.</param>
        private void AddError(OCMFDocument  Document,
                              String        ReasonKey,
                              Object?       Detail = null)
        {

            Document.Errors.Add(
                new Error(
                    i18n.GetMultilanguageText(ReasonKey),
                    SeverityLevel.High,
                    ReasonKey,
                    Detail switch {
                        Exception exception  => exception.Message,
                        String    text       => text,
                        _                    => null
                    }
                )
            );

        }

        #endregion

        #region (private) Regular expressions

        [GeneratedRegex("^[0-9A-Fa-f]+$")]
        private static partial Regex HexRegex();

        [GeneratedRegex("^(?:[A-Z2-7]{8})*(?:[A-Z2-7]{2}={6}|[A-Z2-7]{4}={4}|[A-Z2-7]{5}={3}|[A-Z2-7]{7}=)?$")]
        private static partial Regex Base32Regex();

        [GeneratedRegex("^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$")]
        private static partial Regex Base64Regex();

        #endregion


    }

}
