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

#endregion

namespace cloud.charging.open.chargy.Formats.OCMF
{

    /// <summary>
    /// One OCMF document: "OCMF|{payload}|{signature}".
    /// </summary>
    public class OCMFDocument
    {

        #region Properties

        /// <summary>The whole document, exactly as it was read.</summary>
        public String                Raw                   { get; internal set; } = "";

        /// <summary>
        /// The payload, exactly as it was read.
        ///
        /// This is the string the signature was computed over, character for
        /// character. It is kept rather than re-serialised from the parsed JSON,
        /// because re-serialising would reorder keys, change the spacing or
        /// normalise a number — and any of those would make a perfectly good
        /// signature fail.
        /// </summary>
        public String                RawPayload            { get; internal set; } = "";

        /// <summary>The parsed payload.</summary>
        public JObject               Payload               { get; internal set; } = [];

        /// <summary>The parsed signature block.</summary>
        public JObject               Signature             { get; internal set; } = [];

        /// <summary>The signature itself, as the document wrote it.</summary>
        public String                SignatureData
            => Signature["SD"]?.Value<String>() ?? "";

        /// <summary>How the signature was encoded: "hex", "base64", or absent.</summary>
        public String?               SignatureEncoding
            => Signature["SE"]?.Value<String>();

        /// <summary>The signature algorithm the document names.</summary>
        public String?               SignatureAlgorithmName
            => Signature["SA"]?.Value<String>();

        /// <summary>The signature algorithm, when Chargy recognises it.</summary>
        public OCMFSignatureAlgorithm?  SignatureAlgorithm    { get; internal set; }

        /// <summary>How the hash is described in a verification trace.</summary>
        public String                HashAlgorithm         { get; internal set; } = "?";

        /// <summary>The hash of the payload, hexadecimal; empty when the payload is signed directly.</summary>
        public String                HashValue             { get; internal set; } = "?";

        /// <summary>The signature bytes, for the algorithms that sign the payload directly.</summary>
        public Byte[]?               SignatureBytes        { get; internal set; }

        /// <summary>The r and s of an ECDSA signature.</summary>
        public SignatureRS?          SignatureRS           { get; internal set; }

        /// <summary>The public key this document was checked against.</summary>
        public String?               PublicKey             { get; internal set; }

        /// <summary>How the public key was encoded.</summary>
        public String?               PublicKeyEncoding     { get; internal set; }

        /// <summary>The x coordinate of the public key, once it was decoded.</summary>
        public String?               PublicKeyX            { get; internal set; }

        /// <summary>The y coordinate of the public key, once it was decoded.</summary>
        public String?               PublicKeyY            { get; internal set; }

        /// <summary>What the verification concluded.</summary>
        public VerificationResult    ValidationStatus      { get; internal set; } = VerificationResult.Unvalidated;

        /// <summary>Everything that made the verification of this document fail.</summary>
        public List<Error>           Errors                { get; }               = [];

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this OCMF document.
        /// </summary>
        public override String ToString()

            => $"OCMF {Payload["FV"]?.Value<String>() ?? "?"}: {ValidationStatus.AsText()}";

        #endregion


    }

}
