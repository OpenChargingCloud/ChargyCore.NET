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

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.chargy.Formats.OCMF
{

    /// <summary>
    /// How an OCMF signature is to be shown to somebody comparing it by eye.
    ///
    /// Verification does not need this: a signature either matches or it does
    /// not. Showing it does, and what "showing it" means depends on the
    /// algorithm. An ECDSA signature is a DER structure around two numbers, and
    /// the two numbers are what somebody would compare against a second tool. An
    /// EdDSA signature is not DER at all — it is r and s written one after the
    /// other — and an ML-DSA signature has no components to separate.
    ///
    /// Presenting all three the same way would tell a reader that a value is
    /// something it is not, which is a poor way to answer the question "is this
    /// really the signature I was shown elsewhere?".
    /// </summary>
    /// <param name="Format">How the value is laid out: "RS, hex", "raw, hex" or "rs, hex".</param>
    /// <param name="ValueLabel">What the value is: "raw" bytes or a "der" structure.</param>
    /// <param name="Value">The signature, hexadecimal and upper case.</param>
    /// <param name="R">The first half of the signature, where there is one.</param>
    /// <param name="S">The second half of the signature, where there is one.</param>
    public partial class OCMFSignatureDisplay(String   Format,
                                              String   ValueLabel,
                                              String   Value,
                                              String?  R  = null,
                                              String?  S  = null)
    {

        #region Properties

        /// <summary>How the value is laid out.</summary>
        public String   Format        { get; } = Format;

        /// <summary>What the value is: raw bytes or a DER structure.</summary>
        public String   ValueLabel    { get; } = ValueLabel;

        /// <summary>The signature, hexadecimal and upper case.</summary>
        public String   Value         { get; } = Value;

        /// <summary>The first half of the signature, where there is one.</summary>
        public String?  R             { get; } = R;

        /// <summary>The second half of the signature, where there is one.</summary>
        public String?  S             { get; } = S;

        #endregion


        #region (static) Of(Document)

        /// <summary>
        /// How the signature of the given OCMF document is to be shown.
        /// </summary>
        /// <param name="Document">An OCMF document.</param>
        public static OCMFSignatureDisplay Of(OCMFDocument Document)

            => Of(
                   Document.Signature,
                   Document.SignatureBytes,
                   Document.SignatureRS
               );

        #endregion

        #region (static) Of(Signature, SignatureBytes = null, SignatureRS = null)

        /// <summary>
        /// How the given OCMF signature is to be shown.
        /// </summary>
        /// <param name="Signature">The signature block of an OCMF document.</param>
        /// <param name="SignatureBytes">The already decoded signature, when it was decoded.</param>
        /// <param name="SignatureRS">The two numbers of an ECDSA signature, when it is one.</param>
        public static OCMFSignatureDisplay Of(JObject       Signature,
                                              Byte[]?       SignatureBytes  = null,
                                              SignatureRS?  SignatureRS     = null)
        {

            var algorithm  = Signature["SA"]?.Value<String>();

            var rawHex     = Convert.ToHexString(
                                 SignatureBytes is not null && SignatureBytes.Length > 0
                                     ? SignatureBytes
                                     : DecodeRaw(
                                           Signature["SD"]?.Value<String>() ?? "",
                                           Signature["SE"]?.Value<String>()
                                       )
                             );

            #region EdDSA, where r and s simply follow one another

            if (algorithm is "EdDSA-Ed25519" or "EdDSA-Ed448")
            {

                // Ed25519 signs into 64 bytes, Ed448 into 114. Half of each is
                // r and half is s, so the split is a fixed number of characters
                // — unless the signature is not the length it should be, in
                // which case there is nothing to split and saying so is better
                // than cutting an arbitrary string in two.
                var componentLength = algorithm == "EdDSA-Ed25519" ? 64 : 114;

                return rawHex.Length != componentLength * 2
                           ? new OCMFSignatureDisplay("RS, hex", "raw", rawHex)
                           : new OCMFSignatureDisplay(
                                 "RS, hex",
                                 "raw",
                                 rawHex,
                                 rawHex[..componentLength],
                                 rawHex[componentLength..]
                             );

            }

            #endregion

            #region ML-DSA, which has no components at all

            if (algorithm is "ML-DSA-44" or "ML-DSA-65" or "ML-DSA-87")
                return new OCMFSignatureDisplay("raw, hex", "raw", rawHex);

            #endregion

            #region ..., and ECDSA, where the two numbers sit inside a DER structure

            return new OCMFSignatureDisplay(
                       "rs, hex",
                       "der",
                       rawHex,
                       SignatureRS?.R.ToLowerInvariant().PadLeft(56, '0'),
                       SignatureRS?.S.ToLowerInvariant().PadLeft(56, '0')
                   );

            #endregion

        }

        #endregion

        #region (private, static) DecodeRaw(Value, Encoding)

        /// <summary>
        /// Decode a signature that has not been decoded yet.
        ///
        /// OCMF states the encoding in "SE", and where it does not, the value is
        /// read as hexadecimal when it could be and as base64 otherwise.
        /// </summary>
        /// <param name="Value">The signature, as the document wrote it.</param>
        /// <param name="Encoding">How it is encoded, when the document said.</param>
        private static Byte[] DecodeRaw(String   Value,
                                        String?  Encoding)
        {

            var encoding = Encoding?.ToLowerInvariant();

            if (encoding == "hex" ||
                (encoding is null && HexRegex().IsMatch(Value)))
            {

                if (Value.Length % 2 != 0)
                    throw new FormatException("Raw hexadecimal signature data is malformed.");

                return Convert.FromHexString(Value);

            }

            if (encoding is null or "base64")
                return Convert.FromBase64String(Value);

            throw new FormatException($"Unsupported raw signature encoding '{Encoding}'.");

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this signature display.
        /// </summary>
        public override String ToString()

            => $"{ValueLabel}: {Value} ({Format})";

        #endregion


        #region (private) Regular expressions

        [GeneratedRegex("^[0-9a-fA-F]+$")]
        private static partial Regex HexRegex();

        #endregion


    }

}
