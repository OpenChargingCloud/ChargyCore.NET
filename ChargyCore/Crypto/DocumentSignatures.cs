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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy.Crypto
{

    /// <summary>
    /// How one signature over a whole document came out.
    /// </summary>
    public enum DocumentSignatureStatus
    {

        /// <summary>The key is known and the signature matches the signed data.</summary>
        ValidSignature,

        /// <summary>Everything resolved, but the signature does not match the signed data.</summary>
        InvalidSignature,

        /// <summary>No public key of the document carries the key id the signature names.</summary>
        UnknownPublicKey,

        /// <summary>The algorithm or one of the encodings is not supported here.</summary>
        UnsupportedAlgorithm,

        /// <summary>The entry is not a well-formed signature at all.</summary>
        Malformed

    }


    /// <summary>
    /// How a document as a whole came out.
    /// </summary>
    public enum DocumentSignaturesStatus
    {

        /// <summary>No "signatures" array, or an empty one: nothing to verify.</summary>
        Unsigned,

        /// <summary>Every signature verified.</summary>
        AllValid,

        /// <summary>At least one verified and at least one did not.</summary>
        SomeValid,

        /// <summary>Signatures are present, but not one of them verified.</summary>
        NoneValid

    }


    /// <summary>
    /// The outcome of one entry of a "signatures" array.
    /// </summary>
    /// <param name="Index">The position within the "signatures" array.</param>
    /// <param name="KeyId">The key id the signature names, when it names one.</param>
    /// <param name="Algorithm">The algorithm the signature names, when it names one.</param>
    /// <param name="Status">How the signature came out.</param>
    /// <param name="Details">A technical detail for developers; never localized, never shown as it is.</param>
    public sealed class DocumentSignatureResult(Int32                    Index,
                                                String?                  KeyId,
                                                String?                  Algorithm,
                                                DocumentSignatureStatus  Status,
                                                String?                  Details = null)
    {

        #region Properties

        /// <summary>The position within the "signatures" array.</summary>
        public Int32                    Index        { get; } = Index;

        /// <summary>The key id the signature names, when it names one.</summary>
        public String?                  KeyId        { get; } = KeyId;

        /// <summary>The algorithm the signature names, when it names one.</summary>
        public String?                  Algorithm    { get; } = Algorithm;

        /// <summary>How the signature came out.</summary>
        public DocumentSignatureStatus  Status       { get; } = Status;

        /// <summary>A technical detail for developers.</summary>
        public String?                  Details      { get; } = Details;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this result.
        /// </summary>
        public override String ToString()

            => $"#{Index} {Algorithm ?? "?"}: {Status}{(Details is not null ? $" ({Details})" : "")}";

        #endregion

    }


    /// <summary>
    /// The outcome of every entry of a "signatures" array.
    /// </summary>
    /// <param name="Status">How the document as a whole came out.</param>
    /// <param name="Signatures">How each signature came out.</param>
    public sealed class DocumentSignaturesResult(DocumentSignaturesStatus              Status,
                                                 IEnumerable<DocumentSignatureResult>  Signatures)
    {

        #region Properties

        /// <summary>How the document as a whole came out.</summary>
        public DocumentSignaturesStatus                Status        { get; } = Status;

        /// <summary>How each signature came out.</summary>
        public IReadOnlyList<DocumentSignatureResult>  Signatures    { get; } = [ .. Signatures ];

        /// <summary>How many of the signatures verified.</summary>
        public Int32                                   ValidCount
            => Signatures.Count(signature => signature.Status == DocumentSignatureStatus.ValidSignature);

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this result.
        /// </summary>
        public override String ToString()

            => $"{Status}: {ValidCount} of {Signatures.Count} valid";

        #endregion

    }


    /// <summary>
    /// A public key as a document lists it.
    /// </summary>
    /// <param name="Algorithm">The key type, e.g. "ECDSA-secp256r1".</param>
    /// <param name="Encodings">How the value was encoded, innermost first.</param>
    /// <param name="Value">The value.</param>
    /// <param name="KeyUsage">What the key may be used for.</param>
    public sealed class DocumentPublicKey(String               Algorithm,
                                          IEnumerable<String>  Encodings,
                                          String               Value,
                                          IEnumerable<String>  KeyUsage)
    {

        /// <summary>The key type, e.g. "ECDSA-secp256r1".</summary>
        public String                 Algorithm    { get; } = Algorithm;

        /// <summary>How the value was encoded, innermost first.</summary>
        public IReadOnlyList<String>  Encodings    { get; } = [ .. Encodings ];

        /// <summary>The value.</summary>
        public String                 Value        { get; } = Value;

        /// <summary>What the key may be used for.</summary>
        public IReadOnlyList<String>  KeyUsage     { get; } = [ .. KeyUsage ];

    }


    /// <summary>
    /// Signatures over a whole JSON document, as a charge transparency live link
    /// carries them: every entry of the "signatures" array names the key that
    /// signed by its id, which properties were covered, and how the covered
    /// properties were turned into bytes.
    ///
    /// Nothing here throws at the caller. A document that is unsigned, signed by
    /// an unknown key, or signed badly is reported as such and stays usable —
    /// whether that is worth a warning or worse is the caller's decision.
    ///
    /// The port of upstream's DocumentSignatures.ts (0.14.0, 0.16.1). It differs
    /// in one direction only: a raw key is wrapped into its SubjectPublicKeyInfo
    /// for every curve and every ML-DSA parameter set this library knows, where
    /// upstream carries fixed headers for P-256, P-384, P-521, Ed25519 and Ed448.
    /// </summary>
    public static class DocumentSignatures
    {

        #region Data

        /// <summary>
        /// How a key id is derived when a document does not say: the SHA-256 over
        /// the canonical SubjectPublicKeyInfo form, hexadecimal.
        /// </summary>
        public static IReadOnlyList<String> DefaultKeyIdGeneration { get; } = [ "SubjectPublicKeyInfo", "DER", "SHA-256", "hex" ];

        /// <summary>What the signatures cover when a document does not say.</summary>
        private static readonly String[] defaultExcludedProperties  = [ "signatures" ];

        /// <summary>
        /// The only way of turning covered properties into bytes understood here:
        /// canonicalize the remaining JSON per RFC 8785 and read it as UTF-8.
        /// </summary>
        private static readonly String[] canonicalJSONEncodings     = [ "JSON", "JCS", "UTF-8" ];

        #endregion


        #region CollectDocumentPublicKeys(Document)

        /// <summary>
        /// Every public key a document lists, in the order they appear: those of
        /// the charging station operator, the one of the energy meter of the EVSE,
        /// and those of the grid operator, which signs the power constraints it
        /// sends rather than the document carrying them.
        ///
        /// A key that does not carry the three things needed to use it — an
        /// algorithm, an encodings pipeline and a value — is skipped rather than
        /// guessed at.
        /// </summary>
        /// <param name="Document">A JSON document.</param>
        public static IReadOnlyList<DocumentPublicKey> CollectDocumentPublicKeys(JObject Document)
        {

            var publicKeys = new List<DocumentPublicKey>();

            void CollectFrom(JToken? Candidates)
            {

                if (Candidates is not JArray candidates)
                    return;

                foreach (var entry in candidates.OfType<JObject>())
                {

                    var algorithm  = AsString     (entry["algorithm"]);
                    var value      = AsString     (entry["value"]);
                    var encodings  = AsStringArray(entry["encodings"]);
                    var keyUsage   = AsStringArray(entry["keyUsage"]) ?? [];

                    if (algorithm is null || algorithm.Length == 0 ||
                        value     is null || value.    Length == 0 ||
                        encodings is null || encodings.Length == 0)
                    {
                        continue;
                    }

                    publicKeys.Add(new DocumentPublicKey(algorithm, encodings, value, keyUsage));

                }

            }

            CollectFrom(Document["chargingStationOperator"]?["publicKeys"]);
            CollectFrom(Document["chargingStation"]?["EVSE"]?["energyMeter"]?["publicKeys"]);
            CollectFrom(Document["gridOperator"]?["publicKeys"]);

            return publicKeys;

        }

        #endregion

        #region VerifyDocumentSignatures(Document)

        /// <summary>
        /// Verify every signature a document carries over itself.
        ///
        /// A document without signatures is reported as unsigned rather than as a
        /// failure: whether that is acceptable depends on the document, and saying
        /// so is the caller's job.
        ///
        /// The document must be the one as it was read. Verification covers every
        /// property except those the signatures exclude, so anything added to the
        /// object beforehand — a default timestamp, a verification result —
        /// changes the bytes that are verified and makes a good signature look bad.
        /// </summary>
        /// <param name="Document">A JSON document.</param>
        public static DocumentSignaturesResult VerifyDocumentSignatures(JObject Document)
        {

            if (Document["signatures"] is not JArray signatures || signatures.Count == 0)
                return new DocumentSignaturesResult(DocumentSignaturesStatus.Unsigned, []);

            return VerifySignaturesOf(
                       Document,
                       signatures,
                       CollectDocumentPublicKeys(Document),
                       AsStringArray(Document["keyIdGeneration"]) ?? [ .. DefaultKeyIdGeneration ]
                   );

        }

        #endregion

        #region VerifyEmbeddedSignatures(Embedded, EnclosingDocument)

        /// <summary>
        /// Verify the signatures an object inside a document carries over itself
        /// — a legally relevant log message, say, signed by the grid operator who
        /// sent it rather than by whoever assembled the document around it.
        ///
        /// The signatures sit on the object, the keys to check them and the
        /// "keyIdGeneration" belong to the enclosing document, so the two are
        /// passed separately. The object is canonicalized on its own, exactly as
        /// a document is.
        /// </summary>
        /// <param name="Embedded">The signed object.</param>
        /// <param name="EnclosingDocument">The document listing the keys.</param>
        public static DocumentSignaturesResult VerifyEmbeddedSignatures(JObject  Embedded,
                                                                        JObject  EnclosingDocument)
        {

            if (Embedded["signatures"] is not JArray signatures || signatures.Count == 0)
                return new DocumentSignaturesResult(DocumentSignaturesStatus.Unsigned, []);

            return VerifySignaturesOf(
                       Embedded,
                       signatures,
                       CollectDocumentPublicKeys(EnclosingDocument),
                       AsStringArray(EnclosingDocument["keyIdGeneration"]) ?? [ .. DefaultKeyIdGeneration ]
                   );

        }

        #endregion


        #region (private, static) VerifySignaturesOf(SignedObject, Signatures, PublicKeys, KeyIdGeneration)

        /// <summary>
        /// What both of the above do once they know which object is signed, which
        /// keys may have signed it and how key ids are computed.
        /// </summary>
        private static DocumentSignaturesResult VerifySignaturesOf(JObject                           SignedObject,
                                                                   JArray                            Signatures,
                                                                   IReadOnlyList<DocumentPublicKey>  PublicKeys,
                                                                   IReadOnlyList<String>             KeyIdGeneration)
        {

            var results     = Signatures.Select((signature, index) => VerifySignature(SignedObject, signature, index, PublicKeys, KeyIdGeneration)).
                                         ToArray();

            var validCount  = results.Count(result => result.Status == DocumentSignatureStatus.ValidSignature);

            return new DocumentSignaturesResult(
                       validCount == 0              ? DocumentSignaturesStatus.NoneValid
                     : validCount == results.Length ? DocumentSignaturesStatus.AllValid
                     :                                DocumentSignaturesStatus.SomeValid,
                       results
                   );

        }

        #endregion

        #region (private, static) VerifySignature   (SignedObject, Signature, Index, PublicKeys, KeyIdGeneration)

        private static DocumentSignatureResult VerifySignature(JObject                           SignedObject,
                                                               JToken                            Signature,
                                                               Int32                             Index,
                                                               IReadOnlyList<DocumentPublicKey>  PublicKeys,
                                                               IReadOnlyList<String>             KeyIdGeneration)
        {

            if (Signature is not JObject entry)
                return new DocumentSignatureResult(Index, null, null, DocumentSignatureStatus.Malformed, "The signature is not a JSON object.");

            var keyId       = AsString     (entry["keyId"]);
            var algorithm   = AsString     (entry["algorithm"]);
            var value       = AsString     (entry["value"]);
            var encodings   = AsStringArray(entry["encodings"]);
            var signedData  = entry["signedData"] as JObject;

            // Everything the result can say about this entry, whatever happens below.
            DocumentSignatureResult Described(DocumentSignatureStatus  Status,
                                              String?                  Details = null)

                => new (Index, keyId, algorithm, Status, Details);

            if (keyId     is null || keyId.    Length == 0 ||
                algorithm is null || algorithm.Length == 0 ||
                value     is null || value.    Length == 0 ||
                encodings is null || encodings.Length == 0)
            {
                return Described(DocumentSignatureStatus.Malformed, "A signature needs a keyId, an algorithm, encodings and a value.");
            }

            #region What was signed

            var signedEncodings = AsStringArray(signedData?["encodings"]) ?? canonicalJSONEncodings;

            if (!signedEncodings.SequenceEqual(canonicalJSONEncodings))
                return Described(DocumentSignatureStatus.UnsupportedAlgorithm,
                                 $"Only [ {String.Join(", ", canonicalJSONEncodings)} ] signed data is understood here.");

            var excludedProperties = (AsStringArray(signedData?["excludedProperties"]) ?? defaultExcludedProperties).ToList();

            // The signature can never cover itself, so a document that forgets to
            // say so is still read the only way it could have been signed.
            if (!excludedProperties.Contains("signatures"))
                excludedProperties.Add("signatures");

            Byte[] signedBytes;

            try
            {

                var covered = new JObject(
                                  SignedObject.Properties().
                                               Where (property => !excludedProperties.Contains(property.Name)).
                                               Select(property => new JProperty(property.Name, property.Value.DeepClone()))
                              );

                signedBytes = CanonicalJSON.ToUTF8Bytes(covered);

            }
            catch (Exception exception)
            {
                return Described(DocumentSignatureStatus.Malformed, $"The signed properties could not be canonicalized: {exception.Message}");
            }

            #endregion

            #region With which key

            var suiteName = SuiteName(algorithm);

            if (suiteName is null)
                return Described(DocumentSignatureStatus.UnsupportedAlgorithm, $"Unsupported signature algorithm: {algorithm}");

            // Which key signed follows from the id alone. Comparing
            // case-insensitively costs nothing and spares every writer of a
            // document the question whether hexadecimal ids are upper or lower case.
            var publicKey = PublicKeys.FirstOrDefault(candidate => String.Equals(ComputeKeyId(candidate, KeyIdGeneration), keyId, StringComparison.OrdinalIgnoreCase));

            if (publicKey is null)
                return Described(DocumentSignatureStatus.UnknownPublicKey, $"No public key of this document has the key id {keyId}.");

            #endregion

            #region Does it match?

            try
            {

                var suite           = SignatureSuites.Get(suiteName);
                var publicKeyBytes  = PublicKeyBytes(publicKey, "raw");
                var signatureBytes  = DecodeText(value, encodings[^1]);

                if (!suite.IsValidPublicKey(publicKeyBytes))
                    return Described(DocumentSignatureStatus.Malformed, $"The public key is not valid for {algorithm}.");

                // The suite hashes the canonical bytes itself, with the digest the
                // algorithm names; the DER or compact shape of the signature is
                // detected rather than assumed.
                return Described(suite.Verify(signedBytes, signatureBytes, publicKeyBytes)
                                     ? DocumentSignatureStatus.ValidSignature
                                     : DocumentSignatureStatus.InvalidSignature);

            }
            catch (Exception exception)
            {
                return Described(DocumentSignatureStatus.Malformed, exception.Message);
            }

            #endregion

        }

        #endregion

        #region (private, static) ComputeKeyId      (PublicKey, KeyIdGeneration)

        /// <summary>
        /// The id of a public key, following the document's "keyIdGeneration"
        /// pipeline, e.g. [ "SubjectPublicKeyInfo", "DER", "SHA-256", "hex" ].
        /// </summary>
        private static String? ComputeKeyId(DocumentPublicKey      PublicKey,
                                            IReadOnlyList<String>  KeyIdGeneration)
        {

            if (KeyIdGeneration.Count == 0)
                return null;

            var index      = 0;
            var structure  = KeyIdGeneration[index++];

            // A SubjectPublicKeyInfo is only ever serialized as DER; the step is
            // part of the notation rather than a choice.
            if (structure == "SubjectPublicKeyInfo" && index < KeyIdGeneration.Count && KeyIdGeneration[index] == "DER")
                index++;

            Byte[] bytes;

            try
            {
                bytes = PublicKeyBytes(PublicKey, structure);
            }
            catch
            {
                return null;
            }

            for (; index < KeyIdGeneration.Count; index++)
            {

                switch (KeyIdGeneration[index])
                {

                    case "SHA-256":  bytes = SHA256.HashData(bytes);  break;
                    case "SHA-384":  bytes = SHA384.HashData(bytes);  break;
                    case "SHA-512":  bytes = SHA512.HashData(bytes);  break;

                    // A pipeline ends with the text encoding of the id.
                    case "hex":      return Convert.ToHexString  (bytes);
                    case "base64":   return Convert.ToBase64String(bytes);

                    default:         return null;

                }

            }

            return null;

        }

        #endregion

        #region (private, static) PublicKeyBytes    (PublicKey, Structure)

        /// <summary>
        /// The key material of a public key in the requested structure, converting
        /// between the stored form and the requested one. A COSE key or a bare
        /// point arrives here already as the SubjectPublicKeyInfo or raw key it
        /// stands for, so it has a key id like any other key.
        /// </summary>
        /// <exception cref="ArgumentException">When the key cannot be read or not be represented so.</exception>
        private static Byte[] PublicKeyBytes(DocumentPublicKey  PublicKey,
                                             String             Structure)
        {

            var read = LiveLinkPublicKey.Read(PublicKey.Value, PublicKey.Encodings, PublicKey.Algorithm);

            if (read.KeyHEX is not String keyHEX)
                throw new ArgumentException(read.Details ?? "The public key cannot be read!");

            var stored = Convert.FromHexString(keyHEX);

            // The verifiers take an elliptic curve key as its SubjectPublicKeyInfo
            // and every other key raw, so the stored bytes say which they are.
            var subjectPublicKeyInfo = PublicKeyParser.TryParseDER(stored);

            return Structure switch {

                       "SubjectPublicKeyInfo" when subjectPublicKeyInfo is not null
                           => stored,

                       "SubjectPublicKeyInfo"
                           => LiveLinkPublicKey.SubjectPublicKeyInfoOf(stored, PublicKey.Algorithm)
                                  ?? throw new ArgumentException($"Cannot wrap a raw {PublicKey.Algorithm} key into a SubjectPublicKeyInfo!"),

                       "raw"
                           => subjectPublicKeyInfo?.Value ?? stored,

                       _   => throw new ArgumentException($"Cannot represent a key as {Structure}!")

                   };

        }

        #endregion

        #region (private, static) SuiteName         (Algorithm)

        /// <summary>
        /// The signature algorithms of a document, mapped onto the suites
        /// available here. The key entries and the signature entries spell these
        /// differently: a key says "ECDSA-secp256r1", the signature that used it
        /// names the digest as well, "ECDSA-secp256r1-SHA256".
        /// </summary>
        private static String? SuiteName(String Algorithm)

            => Algorithm switch {
                   "ECDSA-secp256r1-SHA256"  => "ECDSA-P256",
                   "ECDSA-secp384r1-SHA384"  => "ECDSA-P384",
                   "ECDSA-secp521r1-SHA512"  => "ECDSA-P521",
                   "EdDSA-Ed25519"           => "Ed25519",
                   "EdDSA-Ed448"             => "Ed448",
                   "ML-DSA-44"               => "ML-DSA-44",
                   "ML-DSA-65"               => "ML-DSA-65",
                   "ML-DSA-87"               => "ML-DSA-87",
                   _                         => null
               };

        #endregion

        #region (private, static) DecodeText        (Value, Encoding)

        private static Byte[] DecodeText(String  Value,
                                         String  Encoding)

            => Encoding switch {
                   "hex"     => Convert.FromHexString   (Value),
                   "base64"  => Convert.FromBase64String(Value),
                   _         => throw new ArgumentException($"Unsupported text encoding: {Encoding}")
               };

        #endregion

        #region (private, static) AsString / AsStringArray

        private static String? AsString(JToken? JSON)

            => JSON?.Type == JTokenType.String
                   ? JSON.Value<String>()
                   : null;

        private static String[]? AsStringArray(JToken? JSON)

            => JSON is JArray array && array.All(entry => entry.Type == JTokenType.String)
                   ? [ .. array.Select(entry => entry.Value<String>()!) ]
                   : null;

        #endregion

    }

}
