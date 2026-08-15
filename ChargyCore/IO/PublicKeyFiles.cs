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

using System.Text.RegularExpressions;

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.IO
{

    /// <summary>
    /// Makes sense of the public key files that arrive alongside a charge
    /// transparency record.
    ///
    /// A charging station's public key is very often *not* part of the record it
    /// signed — an operator publishes it separately, precisely so that the record
    /// cannot vouch for itself. Chargy therefore has to pair a key file with the
    /// data file it belongs to, and it does so through their file names.
    /// </summary>
    public static partial class PublicKeyFiles
    {

        #region IdFromFileName    (FileName)

        /// <summary>
        /// The identifier a public key file carries in its name.
        ///
        /// "0024b1000002e300_2-publicKey.pem" and "0024b1000002e300_2.chargy"
        /// both reduce to "0024b1000002e300_2", which is how Chargy knows the key
        /// belongs to that record.
        /// </summary>
        /// <param name="FileName">The name of a file.</param>
        public static String IdFromFileName(String FileName)
        {

            // The first dot, not the last: "0024b1000002e300_2.tar.bz2" and
            // "0024b1000002e300_2.chargy" have to reduce to the same identifier.
            var firstDot = FileName.IndexOf('.');

            var baseName = firstDot >= 0
                               ? FileName[..firstDot]
                               : FileName;

            return PublicKeyMarkerRegex().Replace(baseName, "", 1);

        }

        #endregion

        #region TryGetPublicKeyHEX(FileName, Text)

        /// <summary>
        /// Read a public key file and return the key in the shape the charge
        /// transparency data formats expect it in.
        ///
        /// For Ed25519, Ed448 and ML-DSA that is the bare key; for the elliptic
        /// curve keys it is the whole SubjectPublicKeyInfo, because the formats
        /// that use them — OCMF and PCDF among them — were specified against the
        /// full DER structure and their signatures were computed accordingly.
        /// </summary>
        /// <param name="FileName">The name of the file.</param>
        /// <param name="Text">The contents of the file.</param>
        public static String? TryGetPublicKeyHEX(String   FileName,
                                                 String?  Text)
        {

            if (Text is null)
                return null;

            var text = Text.Trim();

            #region A PEM encoded public key

            if (text.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal) &&
                text.EndsWith  ("-----END PUBLIC KEY-----",   StringComparison.Ordinal))
            {

                try
                {

                    var base64 = String.Concat(
                                     text.Replace("-----BEGIN PUBLIC KEY-----", "").
                                          Replace("-----END PUBLIC KEY-----",   "").
                                          Split('\n').
                                          Select (line => line.Trim()).
                                          Where  (line => line.Length > 0 && !line.StartsWith('#'))
                                 );

                    return PublicKeyHEXOf(Convert.FromBase64String(base64));

                }
                catch (Exception)
                {
                    return null;
                }

            }

            #endregion

            #region ..., or a hexadecimal one

            if (PublicKeyParser.LooksLikeAPublicKeyFile(FileName, text))
            {

                try
                {
                    return PublicKeyHEXOf(Convert.FromHexString(WhitespaceRegex().Replace(text, "")));
                }
                catch (Exception)
                {
                    return null;
                }

            }

            #endregion

            return null;

        }

        #endregion

        #region TryCreateLookup   (ProcessedFiles)

        /// <summary>
        /// Build a public key lookup, but only when the files Chargy was given are
        /// public keys and nothing else.
        ///
        /// Dropping in a key file on its own is a meaningful thing to do: the
        /// application then remembers the key and uses it for the records that
        /// follow. But as soon as one of the files is an actual charge
        /// transparency record, the records are what the user came for, and the
        /// keys are merely their supporting evidence.
        /// </summary>
        /// <param name="ProcessedFiles">The files Chargy has made sense of.</param>
        public static PublicKeyLookup? TryCreateLookup(IReadOnlyList<ExtendedFileInfo> ProcessedFiles)
        {

            if (ProcessedFiles.Count == 0)
                return null;

            var publicKeys = new List<PublicKey>();

            foreach (var processedFile in ProcessedFiles)
                switch (processedFile.Result)
                {

                    case PublicKey publicKey:
                        publicKeys.Add(publicKey);
                        break;

                    case PublicKeyLookup publicKeyLookup:
                        publicKeys.AddRange(publicKeyLookup.PublicKeys);
                        break;

                    // Anything that is not a public key — a record, a live link,
                    // or a file that could not be understood at all — means this
                    // is not a "just the keys, please" situation.
                    default:
                        return null;

                }

            // A single file that already is a lookup is returned unchanged, so
            // that whatever else it carries survives.
            if (ProcessedFiles.Count == 1 &&
                ProcessedFiles[0].Result is PublicKeyLookup singleLookup)
            {
                return singleLookup;
            }

            return new PublicKeyLookup(publicKeys);

        }

        #endregion


        #region (private, static) PublicKeyHEXOf(DER)

        /// <summary>
        /// The hexadecimal form of a public key, as the data formats expect it.
        /// </summary>
        /// <param name="DER">A DER encoded SubjectPublicKeyInfo.</param>
        private static String? PublicKeyHEXOf(Byte[] DER)
        {

            var parsed = PublicKeyParser.TryParseDER(DER);

            if (parsed is null)
                return null;

            return parsed.KeyType is "EdDSA" or "ML-DSA"
                       ? parsed.ValueHEX
                       : Convert.ToHexStringLower(DER);

        }

        #endregion

        #region (private, static) Regular expressions

        /// <summary>
        /// The "publicKey" marker in a file name, however it was punctuated.
        /// </summary>
        [GeneratedRegex(@"[-_]?public[-_]?key", RegexOptions.IgnoreCase)]
        private static partial Regex PublicKeyMarkerRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();

        #endregion


    }

}
