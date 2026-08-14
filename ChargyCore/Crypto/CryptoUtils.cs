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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy.Crypto
{

    /// <summary>
    /// Why a signed JSON message did or did not verify.
    /// </summary>
    public enum JSONSignatureVerificationStatus
    {

        /// <summary>The signature is valid.</summary>
        True,

        /// <summary>The signature does not match the message.</summary>
        False,

        /// <summary>The message is not a JSON object.</summary>
        InvalidJSON,

        /// <summary>The message carries no signatures at all.</summary>
        MissingSignatures,

        /// <summary>The signatures property is present but not an array.</summary>
        InvalidSignaturesArray,

        /// <summary>A signature object is missing one of its mandatory fields.</summary>
        InvalidSignatureStructure,

        /// <summary>The base64 and hexadecimal spellings of a value disagree.</summary>
        InvalidSignatureEncoding,

        /// <summary>The public key is not usable for the selected algorithm.</summary>
        InvalidPublicKey,

        /// <summary>The signature could not be decoded.</summary>
        InvalidSignature,

        /// <summary>The message could not be canonicalized.</summary>
        InvalidCanonicalJSON

    }


    /// <summary>
    /// The outcome of verifying one signature.
    /// </summary>
    /// <param name="Status">Why the signature did or did not verify.</param>
    /// <param name="Description">An optional explanation.</param>
    public class JSONSignatureVerificationResult(JSONSignatureVerificationStatus  Status,
                                                 String?                          Description = null)
    {

        /// <summary>Why the signature did or did not verify.</summary>
        public JSONSignatureVerificationStatus  Status         { get; } = Status;

        /// <summary>An optional explanation.</summary>
        public String?                          Description    { get; } = Description;

        /// <summary>Whether the signature is valid.</summary>
        public Boolean                          IsValid
            => Status == JSONSignatureVerificationStatus.True;

        /// <summary>Return a text representation of this result.</summary>
        public override String ToString()
            => Description is null
                   ? Status.ToString()
                   : $"{Status}: {Description}";

    }


    /// <summary>
    /// The outcome of verifying every signature of a message.
    /// </summary>
    /// <param name="Status">Whether all signatures are valid.</param>
    /// <param name="Description">An optional explanation.</param>
    /// <param name="Signatures">The outcome per signature, by its index.</param>
    public class JSONMessageSignaturesVerificationResult(JSONSignatureVerificationStatus                            Status,
                                                         String?                                                    Description  = null,
                                                         IReadOnlyDictionary<Int32, JSONSignatureVerificationResult>? Signatures  = null)
    {

        /// <summary>Whether all signatures are valid.</summary>
        public JSONSignatureVerificationStatus                             Status         { get; } = Status;

        /// <summary>An optional explanation.</summary>
        public String?                                                     Description    { get; } = Description;

        /// <summary>The outcome per signature, by its index.</summary>
        public IReadOnlyDictionary<Int32, JSONSignatureVerificationResult>  Signatures     { get; } = Signatures ?? new Dictionary<Int32, JSONSignatureVerificationResult>();

        /// <summary>Whether all signatures are valid.</summary>
        public Boolean                                                     IsValid
            => Status == JSONSignatureVerificationStatus.True;

        /// <summary>Return a text representation of this result.</summary>
        public override String ToString()
            => $"{Status} ({Signatures.Count} signature(s))";

    }


    /// <summary>
    /// Signing and verifying JSON messages.
    ///
    /// The message is canonicalized before it is signed, so that a signature
    /// survives being re-serialized with different key order or whitespace — which
    /// is what happens to a charge transparency record on its way through a
    /// backend. The signatures themselves are excluded from what is signed, or no
    /// second signature could ever be added.
    /// </summary>
    public static class CryptoUtils
    {

        #region Data

        /// <summary>The algorithm used when a message does not name one.</summary>
        public const String DefaultAlgorithm = "ECDSA-P256";

        #endregion


        #region SignMessage         (JSONMessage, KeyPairs)

        /// <summary>
        /// Sign the given JSON message with every usable key pair and attach the
        /// signatures to it.
        ///
        /// Returns false when not a single key pair could be used, so that a
        /// caller cannot mistake an unsigned message for a signed one, and false
        /// when a signature this method just produced does not verify.
        /// </summary>
        /// <param name="JSONMessage">The message to sign; its "signatures" array is extended in place.</param>
        /// <param name="KeyPairs">The key pairs to sign with.</param>
        public static Boolean SignMessage(JObject                     JSONMessage,
                                          params SignatureKeyPair?[]  KeyPairs)

            => SignMessage(JSONMessage, null, KeyPairs);

        #endregion

        #region SignMessage         (JSONMessage, Options, KeyPairs)

        /// <summary>
        /// Sign the given JSON message with every usable key pair and attach the
        /// signatures to it.
        /// </summary>
        /// <param name="JSONMessage">The message to sign; its "signatures" array is extended in place.</param>
        /// <param name="Options">Optional signing options.</param>
        /// <param name="KeyPairs">The key pairs to sign with.</param>
        public static Boolean SignMessage(JObject                     JSONMessage,
                                          SignatureOptions?           Options,
                                          params SignatureKeyPair?[]  KeyPairs)
        {

            if (JSONMessage is null || KeyPairs is null)
                return false;

            if (JSONMessage["signatures"] is JToken existing && existing is not JArray)
                return false;

            var signaturesCreated = 0;

            foreach (var keyPair in KeyPairs)
            {

                if (keyPair is null)
                    continue;

                var suite = SignatureSuites.TryGet(keyPair.Algorithm);

                if (suite is null || !suite.IsValidPrivateKey(keyPair.PrivateKey))
                    continue;

                var plainText         = CanonicalBytesWithoutSignatures(JSONMessage);
                var signatureEncoding = Options?.Encoding ?? suite.SignatureEncoding;
                var publicKey         = keyPair.PublicKey.Length > 0
                                            ? keyPair.PublicKey
                                            : suite.GetPublicKey(keyPair.PrivateKey);

                var signOptions       = new SignatureOptions(
                                            Options?.Context,
                                            Options?.Prehashed,
                                            Options?.LowS,
                                            signatureEncoding
                                        );

                var signature         = suite.Sign(plainText, keyPair.PrivateKey, signOptions);

                // Verify before attaching, so that a signature which does not
                // validate is never left behind in the caller's message.
                if (!suite.Verify(plainText, signature, publicKey, signOptions))
                    return false;

                var signatureJSON = new JObject(
                                        new JProperty("algorithm",          keyPair.Algorithm),
                                        new JProperty("publicKeyEncoding",  IsRawKeyAlgorithm(keyPair.Algorithm) ? "raw" : "sec1"),
                                        new JProperty("signatureEncoding",  AsText(signatureEncoding)),
                                        new JProperty("publicKey",          Convert.ToBase64String   (publicKey)),
                                        new JProperty("publicKeyHEX",       Convert.ToHexStringLower (publicKey)),
                                        new JProperty("signature",          Convert.ToBase64String   (signature)),
                                        new JProperty("signatureHEX",       Convert.ToHexStringLower (signature))
                                    );

                if (Options?.Context is Byte[] context && context.Length > 0)
                {
                    signatureJSON.Add(new JProperty("context",     Convert.ToBase64String  (context)));
                    signatureJSON.Add(new JProperty("contextHEX",  Convert.ToHexStringLower(context)));
                }

                if (JSONMessage["signatures"] is not JArray signatures)
                {
                    signatures = [];
                    JSONMessage["signatures"] = signatures;
                }

                signatures.Add(signatureJSON);
                signaturesCreated++;

            }

            // Never report success when not a single key pair could be used.
            return signaturesCreated > 0;

        }

        #endregion


        #region VerifyMessage       (JSONMessage, Options = null)

        /// <summary>
        /// Whether every signature of the given JSON message is valid.
        /// </summary>
        /// <param name="JSONMessage">A signed JSON message.</param>
        /// <param name="Options">Optional verification options.</param>
        public static Boolean VerifyMessage(JObject?           JSONMessage,
                                            SignatureOptions?  Options = null)

            => VerifyMessageResults(JSONMessage, Options).IsValid;

        #endregion

        #region VerifyMessageResults(JSONMessage, Options = null)

        /// <summary>
        /// Verify every signature of the given JSON message, reporting why each
        /// one did or did not verify.
        /// </summary>
        /// <param name="JSONMessage">A signed JSON message.</param>
        /// <param name="Options">Optional verification options.</param>
        public static JSONMessageSignaturesVerificationResult VerifyMessageResults(JObject?           JSONMessage,
                                                                                   SignatureOptions?  Options = null)
        {

            if (JSONMessage is null)
                return new JSONMessageSignaturesVerificationResult(
                           JSONSignatureVerificationStatus.InvalidJSON,
                           "JSON message is missing."
                       );

            if (JSONMessage["signatures"] is JToken token && token is not JArray)
                return new JSONMessageSignaturesVerificationResult(
                           JSONSignatureVerificationStatus.InvalidSignaturesArray,
                           "The signatures property must be an array when present."
                       );

            if (JSONMessage["signatures"] is not JArray signatures || signatures.Count == 0)
                return new JSONMessageSignaturesVerificationResult(
                           JSONSignatureVerificationStatus.MissingSignatures,
                           "JSON message does not contain any signatures."
                       );

            var results = new Dictionary<Int32, JSONSignatureVerificationResult>();

            for (var i = 0; i < signatures.Count; i++)
                results[i] = VerifySignature(JSONMessage, signatures[i], Options);

            var allValid = results.Values.All(result => result.IsValid);

            return new JSONMessageSignaturesVerificationResult(
                       allValid
                           ? JSONSignatureVerificationStatus.True
                           : JSONSignatureVerificationStatus.False,
                       allValid
                           ? null
                           : "At least one signature is invalid.",
                       results
                   );

        }

        #endregion

        #region VerifySignature     (JSONMessage, Signature, Options = null)

        /// <summary>
        /// Verify a single signature against the given JSON message.
        /// </summary>
        /// <param name="JSONMessage">A signed JSON message.</param>
        /// <param name="Signature">One of its signatures.</param>
        /// <param name="Options">Optional verification options.</param>
        public static JSONSignatureVerificationResult VerifySignature(JObject?           JSONMessage,
                                                                      JToken?            Signature,
                                                                      SignatureOptions?  Options = null)
        {

            if (JSONMessage is null)
                return new JSONSignatureVerificationResult(
                           JSONSignatureVerificationStatus.InvalidJSON,
                           "JSON message is missing."
                       );

            if (Signature is not JObject signature ||
                signature["publicKey"]?.   Type != JTokenType.String ||
                signature["publicKeyHEX"]?.Type != JTokenType.String ||
                signature["signature"]?.   Type != JTokenType.String ||
                signature["signatureHEX"]?.Type != JTokenType.String)
            {
                return new JSONSignatureVerificationResult(
                           JSONSignatureVerificationStatus.InvalidSignatureStructure,
                           "Signature object must contain publicKey, publicKeyHEX, signature, and signatureHEX strings."
                       );
            }

            // The two spellings of the same bytes must agree. A record where they
            // disagree has been edited by something that understood only one of
            // them, and neither can then be trusted.
            if (!EncodingsMatch(signature))
                return new JSONSignatureVerificationResult(
                           JSONSignatureVerificationStatus.InvalidSignatureEncoding,
                           "Base64 and hexadecimal encodings of the public key or signature do not match."
                       );

            Byte[] plainText;

            try
            {
                plainText = CanonicalBytesWithoutSignatures(JSONMessage);
            }
            catch (Exception e)
            {
                return new JSONSignatureVerificationResult(
                           JSONSignatureVerificationStatus.InvalidCanonicalJSON,
                           e.Message
                       );
            }

            var algorithm = signature["algorithm"]?.Value<String>() ?? DefaultAlgorithm;
            var suite     = SignatureSuites.TryGet(algorithm);

            if (suite is null)
                return new JSONSignatureVerificationResult(
                           JSONSignatureVerificationStatus.InvalidSignature,
                           $"Unsupported signature algorithm '{algorithm}'."
                       );

            var publicKey      = ECCurveVerifier.TryParseHex(signature["publicKeyHEX"]!.Value<String>());
            var signatureBytes = ECCurveVerifier.TryParseHex(signature["signatureHEX"]!.Value<String>());

            if (publicKey is null || signatureBytes is null)
                return new JSONSignatureVerificationResult(
                           JSONSignatureVerificationStatus.InvalidSignature,
                           "Public key or signature is not valid hexadecimal data."
                       );

            if (!suite.IsValidPublicKey(publicKey))
                return new JSONSignatureVerificationResult(
                           JSONSignatureVerificationStatus.InvalidPublicKey,
                           "Public key is not valid for the selected signature algorithm."
                       );

            var encoding = ParseEncoding(signature["signatureEncoding"]?.Value<String>())
                               ?? (IsRawKeyAlgorithm(algorithm)
                                       ? SignatureEncoding.Raw
                                       : SignatureEncoding.DER);

            var context  = ECCurveVerifier.TryParseHex(signature["contextHEX"]?.Value<String>())
                               ?? Options?.Context;

            try
            {

                var isValid = suite.Verify(
                                  plainText,
                                  signatureBytes,
                                  publicKey,
                                  new SignatureOptions(
                                      context,
                                      Options?.Prehashed,
                                      Options?.LowS,
                                      encoding == SignatureEncoding.Raw ? null : encoding
                                  )
                              );

                return isValid
                           ? new JSONSignatureVerificationResult(JSONSignatureVerificationStatus.True)
                           : new JSONSignatureVerificationResult(
                                 JSONSignatureVerificationStatus.False,
                                 "Signature does not match the canonical JSON message."
                             );

            }
            catch (Exception e)
            {
                return new JSONSignatureVerificationResult(
                           JSONSignatureVerificationStatus.InvalidSignature,
                           e.Message
                       );
            }

        }

        #endregion


        #region (private) CanonicalBytesWithoutSignatures(JSONMessage)

        /// <summary>
        /// The canonical UTF-8 bytes of the message with its signatures removed.
        ///
        /// The signatures cannot be part of what is signed, or no second signature
        /// could ever be added to a message.
        /// </summary>
        private static Byte[] CanonicalBytesWithoutSignatures(JObject JSONMessage)
        {

            var clone = (JObject) JSONMessage.DeepClone();

            clone.Remove("signatures");

            return CanonicalJSON.ToUTF8Bytes(clone);

        }

        #endregion

        #region (private) EncodingsMatch(Signature)

        /// <summary>
        /// Whether the base64 and hexadecimal spellings of the public key, the
        /// signature and the context all describe the same bytes.
        /// </summary>
        private static Boolean EncodingsMatch(JObject Signature)
        {

            static Boolean Matches(String? Base64, String? Hex)
            {

                if (Base64 is null || Hex is null)
                    return false;

                try
                {
                    return String.Equals(
                               Convert.ToHexStringLower(Convert.FromBase64String(Base64)),
                               Hex.Trim().ToLowerInvariant(),
                               StringComparison.Ordinal
                           );
                }
                catch
                {
                    return false;
                }

            }

            if (!Matches(Signature["publicKey"]?.Value<String>(), Signature["publicKeyHEX"]?.Value<String>()) ||
                !Matches(Signature["signature"]?.Value<String>(), Signature["signatureHEX"]?.Value<String>()))
            {
                return false;
            }

            var context = Signature["context"]?.Value<String>();

            return context is null ||
                   Matches(context, Signature["contextHEX"]?.Value<String>());

        }

        #endregion

        #region (private) IsRawKeyAlgorithm / AsText / ParseEncoding

        /// <summary>
        /// Whether the public keys of this algorithm are raw bytes rather than a
        /// SEC1 encoded curve point.
        /// </summary>
        private static Boolean IsRawKeyAlgorithm(String Algorithm)

            => Algorithm.StartsWith("Ed",     StringComparison.Ordinal) ||
               Algorithm.StartsWith("ML-DSA", StringComparison.Ordinal);

        private static String AsText(SignatureEncoding Encoding)

            => Encoding switch {
                   SignatureEncoding.Compact  => "compact",
                   SignatureEncoding.Raw      => "raw",
                   _                          => "der"
               };

        private static SignatureEncoding? ParseEncoding(String? Text)

            => Text switch {
                   "compact"  => SignatureEncoding.Compact,
                   "der"      => SignatureEncoding.DER,
                   "raw"      => SignatureEncoding.Raw,
                   _          => null
               };

        #endregion

    }

}
