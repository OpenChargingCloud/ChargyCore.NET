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

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// A value that may be given either as a bare string or as an object carrying
    /// its format and encoding alongside the value.
    /// </summary>
    /// <param name="Value">The value itself.</param>
    /// <param name="Format">An optional format, e.g. "DER".</param>
    /// <param name="Encoding">An optional encoding, e.g. "hex".</param>
    public class EncodedValue(String   Value,
                              String?  Format    = null,
                              String?  Encoding  = null)
    {

        #region Properties

        /// <summary>The value itself.</summary>
        public String   Value       { get; } = Value;

        /// <summary>An optional format, e.g. "DER".</summary>
        public String?  Format      { get; } = Format;

        /// <summary>An optional encoding, e.g. "hex".</summary>
        public String?  Encoding    { get; } = Encoding;

        #endregion


        #region (static) TryParse(JSON, out EncodedValue)

        /// <summary>
        /// Try to parse the given JSON as an encoded value.
        /// </summary>
        /// <param name="JSON">A JSON representation of an encoded value.</param>
        /// <param name="EncodedValue">The parsed encoded value.</param>
        public static Boolean TryParse(JToken? JSON, out EncodedValue? EncodedValue)
        {

            EncodedValue = null;

            if (JSON is null)
                return false;

            if (JSON.Type == JTokenType.String)
            {
                EncodedValue = new EncodedValue(JSON.Value<String>()!);
                return true;
            }

            if (JSON is JObject json &&
                json["value"]?.Value<String>() is String value)
            {

                EncodedValue = new EncodedValue(
                                   value,
                                   json["format"]?.  Value<String>(),
                                   json["encoding"]?.Value<String>()
                               );

                return true;

            }

            return false;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this encoded value: a bare string when
        /// neither format nor encoding is known, an object otherwise.
        /// </summary>
        public JToken ToJSON()
        {

            if (Format is null && Encoding is null)
                return new JValue(Value);

            var json = new JObject();

            if (Format   is not null)
                json.Add(new JProperty("format",    Format));

            if (Encoding is not null)
                json.Add(new JProperty("encoding",  Encoding));

            json.Add(new JProperty("value", Value));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this encoded value.
        /// </summary>
        public override String ToString()

            => Value;

        #endregion


    }


    /// <summary>
    /// A signature certifying a public key, i.e. one link in the chain of trust
    /// from an energy meter up to a charging station operator.
    /// </summary>
    /// <param name="Id">An optional identification of this signature.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Algorithm">An optional signature algorithm.</param>
    /// <param name="Format">An optional signature format, e.g. "DER".</param>
    /// <param name="Encoding">An optional signature encoding, e.g. "hex".</param>
    /// <param name="Value">An optional signature value.</param>
    /// <param name="PublicKey">An optional public key of the signer.</param>
    /// <param name="SignatureValue">An optional signature, when it is not given as <paramref name="Value"/>.</param>
    /// <param name="Timestamp">An optional timestamp of the signature.</param>
    /// <param name="Issuer">An optional issuer of the signature.</param>
    /// <param name="Signer">An optional signer.</param>
    /// <param name="NotBefore">An optional start of the validity period.</param>
    /// <param name="NotAfter">An optional end of the validity period.</param>
    /// <param name="KeyUsage">Optional restrictions on what the certified key may be used for.</param>
    /// <param name="Operations">Optional operations this signature covers.</param>
    /// <param name="Comment">An optional comment.</param>
    public class PublicKeySignature(String?               Id              = null,
                                    IEnumerable<String>?  Context         = null,
                                    OIDInfo?              Algorithm       = null,
                                    String?               Format          = null,
                                    String?               Encoding        = null,
                                    String?               Value           = null,
                                    EncodedValue?         PublicKey       = null,
                                    EncodedValue?         SignatureValue  = null,
                                    String?               Timestamp       = null,
                                    String?               Issuer          = null,
                                    String?               Signer          = null,
                                    String?               NotBefore       = null,
                                    String?               NotAfter        = null,
                                    IEnumerable<String>?  KeyUsage        = null,
                                    JObject?              Operations      = null,
                                    JObject?              Comment         = null)
    {

        #region Properties

        /// <summary>An optional identification of this signature.</summary>
        public String?                Id                { get; } = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>  Context           { get; } = Context?. ToArray() ?? [];

        /// <summary>An optional signature algorithm.</summary>
        public OIDInfo?               Algorithm         { get; } = Algorithm;

        /// <summary>An optional signature format, e.g. "DER".</summary>
        public String?                Format            { get; } = Format;

        /// <summary>An optional signature encoding, e.g. "hex".</summary>
        public String?                Encoding          { get; } = Encoding;

        /// <summary>An optional signature value.</summary>
        public String?                Value             { get; } = Value;

        /// <summary>An optional public key of the signer.</summary>
        public EncodedValue?          PublicKey         { get; } = PublicKey;

        /// <summary>An optional signature, when it is not given as <see cref="Value"/>.</summary>
        public EncodedValue?          SignatureValue    { get; } = SignatureValue;

        /// <summary>An optional timestamp of the signature.</summary>
        public String?                Timestamp         { get; } = Timestamp;

        /// <summary>An optional issuer of the signature.</summary>
        public String?                Issuer            { get; } = Issuer;

        /// <summary>An optional signer.</summary>
        public String?                Signer            { get; } = Signer;

        /// <summary>An optional start of the validity period.</summary>
        public String?                NotBefore         { get; } = NotBefore;

        /// <summary>An optional end of the validity period.</summary>
        public String?                NotAfter          { get; } = NotAfter;

        /// <summary>Optional restrictions on what the certified key may be used for.</summary>
        public IReadOnlyList<String>  KeyUsage          { get; } = KeyUsage?.ToArray() ?? [];

        /// <summary>Optional operations this signature covers.</summary>
        public JObject?               Operations        { get; } = Operations;

        /// <summary>An optional comment.</summary>
        public JObject?               Comment           { get; } = Comment;

        #endregion


        #region (static) TryParse(JSON, out PublicKeySignature)

        /// <summary>
        /// Try to parse the given JSON as a public key signature.
        /// </summary>
        /// <param name="JSON">A JSON representation of a public key signature.</param>
        /// <param name="PublicKeySignature">The parsed public key signature.</param>
        public static Boolean TryParse(JObject JSON, out PublicKeySignature? PublicKeySignature)
        {

            PublicKeySignature = null;

            EncodedValue? publicKey      = null;
            EncodedValue? signatureValue = null;

            if (JSON["publicKey"] is JToken publicKeyJSON &&
               !EncodedValue.TryParse(publicKeyJSON, out publicKey))
            {
                return false;
            }

            if (JSON["signature"] is JToken signatureJSON &&
               !EncodedValue.TryParse(signatureJSON, out signatureValue))
            {
                return false;
            }

            var value      = JSON["value"]?.    Value<String>();
            var algorithm  = OIDInfo.TryParse(JSON["algorithm"]);
            var timestamp  = JSON["timestamp"]?.Value<String>();
            var issuer     = JSON["issuer"]?.   Value<String>();
            var signer     = JSON["signer"]?.   Value<String>();
            var keyUsage   = JSON["keyUsage"] as JArray;

            // An object that says nothing at all is not a signature. Without this
            // check every stray JSON object next to a public key would be read as
            // an empty certification.
            if (value          is null &&
                signatureValue is null &&
                algorithm      is null &&
                timestamp      is null &&
                issuer         is null &&
                signer         is null &&
                keyUsage       is null)
            {
                return false;
            }

            PublicKeySignature = new PublicKeySignature(
                                     JSON["@id"]?.      Value<String>(),
                                     chargy.PublicKey.ParseContext(JSON["@context"]),
                                     algorithm,
                                     JSON["format"]?.   Value<String>(),
                                     JSON["encoding"]?. Value<String>(),
                                     value,
                                     publicKey,
                                     signatureValue,
                                     timestamp,
                                     issuer,
                                     signer,
                                     JSON["notBefore"]?.Value<String>(),
                                     JSON["notAfter"]?. Value<String>(),
                                     keyUsage?.Where (usage => usage.Type == JTokenType.String).
                                               Select(usage => usage.Value<String>()!),
                                     JSON["operations"] as JObject,
                                     JSON["comment"]    as JObject
                                 );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this public key signature.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Id              is not null)
                json.Add(new JProperty("@id",        Id));

            if (Context.Count == 1)
                json.Add(new JProperty("@context",   Context[0]));

            else if (Context.Count > 1)
                json.Add(new JProperty("@context",   new JArray(Context)));

            if (Algorithm       is not null)
                json.Add(new JProperty("algorithm",  Algorithm.ToJSON()));

            if (Format          is not null)
                json.Add(new JProperty("format",     Format));

            if (Encoding        is not null)
                json.Add(new JProperty("encoding",   Encoding));

            if (Value           is not null)
                json.Add(new JProperty("value",      Value));

            if (PublicKey       is not null)
                json.Add(new JProperty("publicKey",  PublicKey.ToJSON()));

            if (SignatureValue  is not null)
                json.Add(new JProperty("signature",  SignatureValue.ToJSON()));

            if (Timestamp       is not null)
                json.Add(new JProperty("timestamp",  Timestamp));

            if (Issuer          is not null)
                json.Add(new JProperty("issuer",     Issuer));

            if (Signer          is not null)
                json.Add(new JProperty("signer",     Signer));

            if (NotBefore       is not null)
                json.Add(new JProperty("notBefore",  NotBefore));

            if (NotAfter        is not null)
                json.Add(new JProperty("notAfter",   NotAfter));

            if (KeyUsage.Count > 0)
                json.Add(new JProperty("keyUsage",   new JArray(KeyUsage)));

            if (Operations      is not null)
                json.Add(new JProperty("operations", Operations));

            if (Comment         is not null)
                json.Add(new JProperty("comment",    Comment));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this public key signature.
        /// </summary>
        public override String ToString()

            => $"{Signer ?? Issuer ?? Id ?? "<anonymous>"}: {Value ?? SignatureValue?.Value ?? "<no signature>"}";

        #endregion


    }

}
