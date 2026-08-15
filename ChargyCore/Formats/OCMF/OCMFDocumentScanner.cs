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
using System.Text;

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.chargy.Formats.OCMF
{

    /// <summary>
    /// The result of scanning text for OCMF documents.
    /// </summary>
    /// <param name="Documents">The documents that were found.</param>
    /// <param name="ErrorMessage">Why no documents were found, when none were.</param>
    public readonly record struct OCMFScanResult(IReadOnlyList<OCMFDocument>  Documents,
                                                 String?                      ErrorMessage = null)
    {

        /// <summary>Whether any documents were found.</summary>
        public Boolean Success
            => ErrorMessage is null && Documents.Count > 0;

    }


    /// <summary>
    /// Finds the OCMF documents in a piece of text.
    ///
    /// An OCMF document is "OCMF|{payload}|{signature}", and several of them may
    /// sit in one file, one per line — or, in a SAFE XML container, spread across
    /// several elements. So the text is scanned character by character rather than
    /// split on separators: a payload contains braces, quotes and pipes of its
    /// own, and only tracking the brace depth finds where it really ends.
    ///
    /// The scanner keeps the raw payload text of every document it finds. That is
    /// the whole point of scanning this way: the signature covers those exact
    /// characters, so re-serialising the parsed JSON would destroy it.
    /// </summary>
    public class OCMFDocumentScanner
    {

        #region Scan(OCMFDocuments)

        /// <summary>
        /// Find every OCMF document in the given texts.
        /// </summary>
        /// <param name="OCMFDocuments">One or more texts that may hold OCMF documents.</param>
        public OCMFScanResult Scan(IEnumerable<String> OCMFDocuments)
        {

            var combined   = String.Join("\n", OCMFDocuments);
            var documents  = new List<OCMFDocument>();

            // 0: looking for "OCMF"      1: found it, expecting '|'
            // 2: reading the payload     3: read it, expecting '|'
            // 4: reading the signature
            var structure       = 0;
            var depth           = 0;
            var documentStart   = -1;
            var blockStart      = -1;
            var rawPayload      = "";
            var payload         = new JObject();

            try
            {

                for (var i = 0; i < combined.Length; i++)
                {

                    #region The "OCMF" header

                    if (structure == 0 && i >= 3 &&
                        combined[i]     == 'F' && combined[i - 1] == 'M' &&
                        combined[i - 2] == 'C' && combined[i - 3] == 'O')
                    {
                        structure      = 1;
                        documentStart  = i - 3;
                        continue;
                    }

                    #endregion

                    #region The separators around the payload

                    if ((structure == 1 || structure == 3) && combined[i] == '|')
                    {
                        structure++;
                        continue;
                    }

                    #endregion

                    if (structure != 2 && structure != 4)
                        continue;

                    #region The payload and the signature, tracked by brace depth

                    if (combined[i] == '{')
                    {

                        depth++;

                        if (depth == 1)
                            blockStart = i;

                        continue;

                    }

                    if (combined[i] != '}')
                        continue;

                    depth--;

                    if (depth != 0)
                        continue;

                    var blockEnd = i;

                    if (blockStart != -1)
                    {

                        #region The payload

                        if (structure == 2)
                        {

                            rawPayload = combined[blockStart..(blockEnd + 1)];

                            try
                            {
                                payload = ChargyLib.ParseJSON(rawPayload);
                            }
                            catch (Exception)
                            {
                                return new OCMFScanResult(
                                           [],
                                           $"The {documents.Count + 1}. OCMF payload is not a valid JSON document!"
                                       );
                            }

                        }

                        #endregion

                        #region ..., and the signature that closes the document

                        if (structure == 4)
                        {

                            JObject signature;

                            try
                            {
                                signature = ChargyLib.ParseJSON(combined[blockStart..(blockEnd + 1)]);
                            }
                            catch (Exception)
                            {
                                return new OCMFScanResult(
                                           [],
                                           $"The {documents.Count + 1}. OCMF signature is not a valid JSON document!"
                                       );
                            }

                            documents.Add(
                                BuildDocument(
                                    combined[documentStart..(blockEnd + 1)],
                                    rawPayload,
                                    payload,
                                    signature
                                )
                            );

                        }

                        #endregion

                    }

                    #endregion

                    structure++;

                    if (structure == 5)
                        structure = 0;

                }

                return documents.Count > 0
                           ? new OCMFScanResult(documents)
                           : new OCMFScanResult([], "No valid OCMF data found!");

            }
            catch (Exception exception)
            {
                return new OCMFScanResult([], exception.Message);
            }

        }

        #endregion


        #region (private, static) BuildDocument (Raw, RawPayload, Payload, Signature)

        /// <summary>
        /// Assemble a document and hash its payload.
        /// </summary>
        /// <param name="Raw">The whole document.</param>
        /// <param name="RawPayload">The payload, exactly as it was read.</param>
        /// <param name="Payload">The parsed payload.</param>
        /// <param name="Signature">The parsed signature block.</param>
        private static OCMFDocument BuildDocument(String   Raw,
                                                  String   RawPayload,
                                                  JObject  Payload,
                                                  JObject  Signature)
        {

            var document = new OCMFDocument {
                               Raw         = Raw,
                               RawPayload  = RawPayload,
                               Payload     = Payload,
                               Signature   = Signature
                           };

            #region Which algorithm, and therefore which digest?

            var algorithm = OCMFSignatureAlgorithm.TryGet(document.SignatureAlgorithmName);

            if (algorithm is null)
            {
                document.HashAlgorithm     = "?";
                document.HashValue         = "?";
                document.ValidationStatus  = VerificationResult.UnknownSignatureFormat;
                return document;
            }

            document.SignatureAlgorithm  = algorithm;
            document.HashAlgorithm       = algorithm.HashDescription;

            document.HashValue           = algorithm.SignsMessageDirectly
                                               ? ""
                                               : HashOf(algorithm.HashName, RawPayload);

            #endregion

            #region Decode the signature

            if (!TryDecodeSignature(document, algorithm))
                return document;

            #endregion

            document.ValidationStatus = VerificationResult.Unvalidated;

            return document;

        }

        #endregion

        #region (private, static) TryDecodeSignature(Document, Algorithm)

        /// <summary>
        /// Decode the signature into whichever shape its algorithm needs.
        ///
        /// EdDSA and ML-DSA signatures are opaque byte strings. An ECDSA
        /// signature is a DER structure holding two integers, and those are what
        /// the verification actually uses.
        /// </summary>
        /// <param name="Document">An OCMF document.</param>
        /// <param name="Algorithm">Its signature algorithm.</param>
        private static Boolean TryDecodeSignature(OCMFDocument            Document,
                                                  OCMFSignatureAlgorithm  Algorithm)
        {

            try
            {

                var encoded = (Document.SignatureEncoding?.ToLowerInvariant() ?? "") switch {
                                  ""        => Convert.FromHexString(Document.SignatureData),
                                  "hex"     => Convert.FromHexString(Document.SignatureData),
                                  "base64"  => Convert.FromBase64String(Document.SignatureData),
                                  _         => null
                              };

                if (encoded is null)
                {
                    Document.ValidationStatus = VerificationResult.UnknownSignatureFormat;
                    return false;
                }

                if (Algorithm.SignsMessageDirectly)
                {
                    Document.SignatureBytes = encoded;
                    return true;
                }

                var rs = Crypto.ECCurveVerifier.TryDecodeDERSignature(encoded);

                if (rs is null)
                {
                    Document.ValidationStatus = VerificationResult.InvalidSignature;
                    return false;
                }

                Document.SignatureRS = new SignatureRS(
                                           rs.Value.R,
                                           rs.Value.S,
                                           Document.SignatureData
                                       );

                return true;

            }
            catch (Exception)
            {
                Document.ValidationStatus = VerificationResult.InvalidSignature;
                return false;
            }

        }

        #endregion

        #region (private, static) HashOf            (HashName, Text)

        /// <summary>
        /// Hash the raw payload with the digest the algorithm names.
        /// </summary>
        /// <param name="HashName">The name of a digest.</param>
        /// <param name="Text">The raw payload.</param>
        private static String HashOf(String?  HashName,
                                     String   Text)
        {

            var bytes = Encoding.UTF8.GetBytes(Text);

            return Convert.ToHexStringLower(
                       HashName switch {
                           "SHA384"  => SHA384.HashData(bytes),
                           "SHA512"  => SHA512.HashData(bytes),
                           _         => SHA256.HashData(bytes)
                       }
                   );

        }

        #endregion


    }

}
