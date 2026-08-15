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

using System.Security.Cryptography;

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.Formats.EDL40
{

    /// <summary>
    /// One EDL40 or ISA document, and what checking its signature concluded.
    ///
    /// Everything an EV driver could be shown about the signature is kept here —
    /// the bytes that were signed, the hash of them, the public key, r and s —
    /// because "invalid signature" on its own is an accusation without evidence,
    /// and the person holding the receipt is entitled to the evidence.
    /// </summary>
    public class EDL40Document
    {

        #region Properties

        /// <summary>The document as it arrived.</summary>
        public required String               Raw                { get; init; }

        /// <summary>Which of the two SML layouts this is.</summary>
        public required EDL40Variant         Variant            { get; init; }

        /// <summary>The elliptic curve the signature lives on.</summary>
        public required ECCurve              Curve              { get; init; }

        /// <summary>The identification of the meter, hexadecimal.</summary>
        public required String               ServerId           { get; init; }

        /// <summary>The contract identification, hexadecimal and without its padding.</summary>
        public required String               ContractId         { get; init; }

        /// <summary>The public key of the meter, hexadecimal.</summary>
        public required String               PublicKey          { get; init; }

        /// <summary>The 320 bytes the meter signed, hexadecimal.</summary>
        public required String               SignedData         { get; init; }

        /// <summary>The hash of the signed bytes, truncated to the size of the curve.</summary>
        public required String               HashValue          { get; init; }

        /// <summary>The signature, hexadecimal.</summary>
        public required String               SignatureHEX       { get; init; }

        /// <summary>The signature, as its two integers.</summary>
        public required SignatureRS          Signature          { get; init; }

        /// <summary>The pagination counter of the meter.</summary>
        public required Int64                Pagination         { get; init; }

        /// <summary>What checking the signature concluded.</summary>
        public required VerificationResult   ValidationStatus   { get; init; }

        /// <summary>
        /// For an ISA document: whether it is the start, an update, or the end of
        /// a charging session.
        /// </summary>
        public String?                       ListNameContext    { get; init; }

        /// <summary>The hash algorithm, which is SHA-256 for both layouts.</summary>
        public String                        HashAlgorithm
            => "SHA256";

        /// <summary>The format of the public key, which is a bare pair of coordinates.</summary>
        public String                        PublicKeyFormat
            => "XY";

        #endregion


        #region (static) Verify(SignatureData, PublicKeyHEX, Raw)

        /// <summary>
        /// Check the signature of an EDL40 or ISA document.
        /// </summary>
        /// <param name="SignatureData">A parsed document, with the bytes it says were signed.</param>
        /// <param name="PublicKeyHEX">The public key of the meter, hexadecimal.</param>
        /// <param name="Raw">The document as it arrived.</param>
        public static EDL40Document Verify(AEDL40SignatureData  SignatureData,
                                           String               PublicKeyHEX,
                                           String               Raw)
        {

            var publicKey  = ChargyLib.CleanHex(PublicKeyHEX);
            var outcome    = Check(SignatureData, publicKey);

            return new EDL40Document {

                       Raw               = Raw,
                       Variant           = SignatureData.Variant,
                       Curve             = outcome.Curve,
                       ServerId          = Convert.ToHexStringLower(SignatureData.ServerId),
                       ContractId        = Convert.ToHexStringLower(TrimTrailingNuls(SignatureData.ContractId)),
                       PublicKey         = publicKey,
                       SignedData        = Convert.ToHexStringLower(SignatureData.SignedData),
                       HashValue         = outcome.HashValue,
                       SignatureHEX      = Convert.ToHexStringLower(outcome.Signature),
                       Signature         = ToSignatureRS(outcome.Signature),
                       Pagination        = SignatureData.Pagination,
                       ValidationStatus  = outcome.Status,

                       ListNameContext   = SignatureData is ISAEDL40SignatureData isa
                                               ? isa.ListNameContext
                                               : null

                   };

        }

        #endregion

        #region (private, static) Check(SignatureData, PublicKey)

        /// <summary>
        /// Work out which curve this document is signed on, and whether the
        /// signature holds.
        ///
        /// Nothing in the document names the curve. It is deduced from the length
        /// of the public key and the length of the signature — 48 bytes of key
        /// means secp192r1, 64 means secp256r1 — and the hash is then truncated to
        /// match. That deduction is safe: a key and a signature of mismatched
        /// lengths simply cannot verify, and the result says so.
        /// </summary>
        /// <param name="SignatureData">A parsed document.</param>
        /// <param name="PublicKey">The public key of the meter, hexadecimal and cleaned.</param>
        private static (VerificationResult Status, ECCurve Curve, String HashValue, Byte[] Signature) Check(AEDL40SignatureData  SignatureData,
                                                                                                            String               PublicKey)
        {

            #region An ISA meter always signs on secp256r1

            if (SignatureData is ISAEDL40SignatureData isa)
            {

                var isaSignature  = isa.DataSignature;
                var isaHash       = HashOf(isa.SignedData, 32);

                if (PublicKey.Length != 128 ||
                    isaSignature.Length != 64)
                {
                    return (VerificationResult.InvalidPublicKey, ECCurve.secp256r1, isaHash, isaSignature);
                }

                return (
                           VerifyRawSignature(ECCurve.secp256r1, PublicKey, isaSignature, isaHash),
                           ECCurve.secp256r1,
                           isaHash,
                           isaSignature
                       );

            }

            #endregion

            var edl40      = (EDL40PSignatureData) SignatureData;

            // Some meters append two bytes to the SML signature which are not part
            // of it — they are repeated inside the signed block instead.
            var cutoff     = edl40.Version == 4 || edl40.ListSignature.Length == 50
                                 ? 2
                                 : 0;

            var signature  = edl40.ListSignature[..Math.Max(0, edl40.ListSignature.Length - cutoff)];

            if (PublicKey.Length == 96 &&
                signature.Length == 48)
            {

                var hash = HashOf(edl40.SignedData, 24);

                return (
                           VerifyRawSignature(ECCurve.secp192r1, PublicKey, signature, hash),
                           ECCurve.secp192r1,
                           hash,
                           signature
                       );

            }

            if (PublicKey.Length == 128 &&
                signature.Length == 64)
            {

                var hash = HashOf(edl40.SignedData, 32);

                return (
                           VerifyRawSignature(ECCurve.secp256r1, PublicKey, signature, hash),
                           ECCurve.secp256r1,
                           hash,
                           signature
                       );

            }

            #region A key of a known length with a signature of the wrong length is a broken signature, not a broken key

            var curve = PublicKey.Length == 128
                            ? ECCurve.secp256r1
                            : ECCurve.secp192r1;

            return (
                       PublicKey.Length == 96 || PublicKey.Length == 128
                           ? VerificationResult.InvalidSignature
                           : VerificationResult.InvalidPublicKey,
                       curve,
                       HashOf(SignatureData.SignedData, PublicKey.Length == 128 ? 32 : 24),
                       signature
                   );

            #endregion

        }

        #endregion

        #region (private, static) VerifyRawSignature(Curve, PublicKey, Signature, HashValue)

        /// <summary>
        /// Check a raw r||s signature against a public key given as its two bare
        /// coordinates.
        /// </summary>
        /// <param name="Curve">The elliptic curve.</param>
        /// <param name="PublicKey">The public key, hexadecimal, without the SEC1 prefix.</param>
        /// <param name="Signature">The signature, as r followed by s.</param>
        /// <param name="HashValue">The truncated hash of the signed bytes, hexadecimal.</param>
        private static VerificationResult VerifyRawSignature(ECCurve  Curve,
                                                             String   PublicKey,
                                                             Byte[]   Signature,
                                                             String   HashValue)
        {

            try
            {

                var verificationKey = ECCurveVerifier.Get(Curve).ParsePublicKey(PublicKey);

                if (verificationKey is null)
                    return VerificationResult.InvalidSignature;

                return verificationKey.Verify(HashValue, Signature)
                           ? VerificationResult.ValidSignature
                           : VerificationResult.InvalidSignature;

            }
            catch (Exception)
            {
                return VerificationResult.InvalidSignature;
            }

        }

        #endregion

        #region (private, static) HashOf(SignedData, Length)

        /// <summary>
        /// The SHA-256 hash of the signed bytes, truncated to the size of the
        /// curve's order.
        ///
        /// Truncating rather than reducing is what the meters do, and it is what
        /// ECDSA prescribes when the hash is wider than the order.
        /// </summary>
        /// <param name="SignedData">The bytes that were signed.</param>
        /// <param name="Length">How many bytes of the hash the curve uses.</param>
        private static String HashOf(Byte[]  SignedData,
                                     Int32   Length)

            => Convert.ToHexStringLower(SHA256.HashData(SignedData).AsSpan(0, Length));

        #endregion

        #region (private, static) ToSignatureRS(Signature)

        /// <summary>
        /// Take a raw r||s signature apart into its two integers.
        /// </summary>
        /// <param name="Signature">The signature, as r followed by s.</param>
        private static SignatureRS ToSignatureRS(Byte[] Signature)
        {

            var hex   = Convert.ToHexStringLower(Signature);
            var half  = hex.Length / 2;

            return new SignatureRS(
                       hex[..half],
                       hex[half..],
                       Value:      hex,
                       Algorithm:  CryptoAlgorithm.ECC.AsText(),
                       Format:     SignatureFormat.RS.AsText()
                   );

        }

        #endregion

        #region (private, static) TrimTrailingNuls(Bytes)

        /// <summary>
        /// Drop the padding a fixed width field was filled up with.
        /// </summary>
        /// <param name="Bytes">A fixed width field.</param>
        private static Byte[] TrimTrailingNuls(Byte[] Bytes)
        {

            var end = Bytes.Length;

            while (end > 0 && Bytes[end - 1] == 0x00)
                end--;

            return Bytes[..end];

        }

        #endregion

    }

}
