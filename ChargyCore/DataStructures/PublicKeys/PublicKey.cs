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
    /// The public key of an energy meter, a charging station, a charging station
    /// operator or any other party in the charge transparency public key
    /// infrastructure.
    /// </summary>
    /// <param name="Value">The public key itself, usually hexadecimal.</param>
    /// <param name="Algorithm">The signature algorithm, either a name or an object identifier.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Subject">An optional subject this key belongs to.</param>
    /// <param name="Type">An optional key type, either a name or an object identifier.</param>
    /// <param name="Format">An optional key format, e.g. "DER".</param>
    /// <param name="Encoding">An optional key encoding, e.g. "hex".</param>
    /// <param name="Signatures">Optional signatures certifying this public key.</param>
    /// <param name="Certainty">
    /// How sure we are that this key belongs to the given subject, between 0.0 and 1.0.
    /// A key read from the charge transparency record itself is less trustworthy than
    /// one supplied out of band, and the GUI shows the difference to the user.
    /// </param>
    /// <param name="X">The x coordinate, for keys given as a coordinate pair.</param>
    /// <param name="Y">The y coordinate, for keys given as a coordinate pair.</param>
    public class PublicKey(String                          Value,
                           OIDInfo?                        Algorithm   = null,
                           IEnumerable<String>?            Context     = null,
                           JToken?                         Subject     = null,
                           OIDInfo?                        Type        = null,
                           String?                         Format      = null,
                           String?                         Encoding    = null,
                           IEnumerable<PublicKeySignature>? Signatures = null,
                           Double?                         Certainty   = null,
                           String?                         X           = null,
                           String?                         Y           = null)
    {

        #region Properties

        /// <summary>The public key itself, usually hexadecimal.</summary>
        public String                              Value        { get; } = Value;

        /// <summary>The signature algorithm, either a name or an object identifier.</summary>
        public OIDInfo?                            Algorithm    { get; } = Algorithm;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>               Context      { get; } = Context?.  ToArray() ?? [];

        /// <summary>An optional subject this key belongs to.</summary>
        public JToken?                             Subject      { get; } = Subject;

        /// <summary>An optional key type, either a name or an object identifier.</summary>
        public OIDInfo?                            Type         { get; } = Type;

        /// <summary>An optional key format, e.g. "DER".</summary>
        public String?                             Format       { get; } = Format;

        /// <summary>An optional key encoding, e.g. "hex".</summary>
        public String?                             Encoding     { get; } = Encoding;

        /// <summary>Optional signatures certifying this public key.</summary>
        public IReadOnlyList<PublicKeySignature>   Signatures   { get; } = Signatures?.ToArray() ?? [];

        /// <summary>How sure we are that this key belongs to the given subject.</summary>
        public Double?                             Certainty    { get; } = Certainty;

        /// <summary>The x coordinate, for keys given as a coordinate pair.</summary>
        public String?                             X            { get; } = X;

        /// <summary>The y coordinate, for keys given as a coordinate pair.</summary>
        public String?                             Y            { get; } = Y;

        /// <summary>Whether this key is given as a pair of coordinates rather than as a single value.</summary>
        public Boolean                             IsXY
            => X is not null && Y is not null;

        #endregion


        #region (static) TryParse(JSON, out PublicKey)

        /// <summary>
        /// Try to parse the given JSON as a public key.
        /// </summary>
        /// <param name="JSON">A JSON representation of a public key.</param>
        /// <param name="PublicKey">The parsed public key.</param>
        public static Boolean TryParse(JObject JSON, out PublicKey? PublicKey)
        {

            PublicKey = null;

            var value  = JSON["value"]?.Value<String>();
            var x      = JSON["x"]?.    Value<String>();
            var y      = JSON["y"]?.    Value<String>();

            // A key is either a single value or a full coordinate pair.
            if (value is null && (x is null || y is null))
                return false;

            if (!IsAPublicKeySubject(JSON["subject"]))
                return false;

            var signatures = new List<PublicKeySignature>();

            if (JSON["signatures"] is JArray signatureArray)
                foreach (var signatureJSON in signatureArray.OfType<JObject>())
                    if (PublicKeySignature.TryParse(signatureJSON, out var signature))
                        signatures.Add(signature!);
                    else
                        return false;

            PublicKey = new PublicKey(
                            value ?? "",
                            OIDInfo.TryParse(JSON["algorithm"]),
                            ParseContext(JSON["@context"]),
                            JSON["subject"],
                            OIDInfo.TryParse(JSON["type"]),
                            JSON["format"]?.  Value<String>(),
                            JSON["encoding"]?.Value<String>(),
                            signatures,
                            JSON["certainty"]?.Value<Double>(),
                            x,
                            y
                        );

            return true;

        }

        #endregion

        #region (static) IsAPublicKeySubject(Subject)

        /// <summary>
        /// Whether the given JSON is a valid public key subject: absent, a string,
        /// an array of strings, or an object whose values are strings or arrays of
        /// strings.
        /// </summary>
        /// <param name="Subject">A JSON representation of a public key subject.</param>
        public static Boolean IsAPublicKeySubject(JToken? Subject)
        {

            if (Subject is null || Subject.Type == JTokenType.Null)
                return true;

            if (Subject.Type == JTokenType.String)
                return true;

            if (Subject is JArray array)
                return array.All(element => element.Type == JTokenType.String);

            if (Subject is JObject json)
                return json.Properties().All(property => property.Value.Type == JTokenType.String ||
                                                        (property.Value is JArray values &&
                                                         values.All(value => value.Type == JTokenType.String)));

            return false;

        }

        #endregion

        #region (static) ParseContext(Context)

        /// <summary>
        /// A JSON-LD context, which may be given as a single string or as an array.
        /// </summary>
        /// <param name="Context">A JSON representation of a JSON-LD context.</param>
        internal static String[] ParseContext(JToken? Context)

            => Context switch {
                   null                                        => [],
                   JArray array                                => [.. array.Where (element => element.Type == JTokenType.String).
                                                                             Select(element => element.Value<String>()!)],
                   _ when Context.Type == JTokenType.String     => [ Context.Value<String>()! ],
                   _                                           => []
               };

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this public key.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Context.Count == 1)
                json.Add(new JProperty("@context",    Context[0]));

            else if (Context.Count > 1)
                json.Add(new JProperty("@context",    new JArray(Context)));

            if (Subject    is not null)
                json.Add(new JProperty("subject",     Subject));

            if (Algorithm  is not null)
                json.Add(new JProperty("algorithm",   Algorithm.ToJSON()));

            if (Type       is not null)
                json.Add(new JProperty("type",        Type.ToJSON()));

            if (Format     is not null)
                json.Add(new JProperty("format",      Format));

            if (Encoding   is not null)
                json.Add(new JProperty("encoding",    Encoding));

            if (X is not null && Y is not null)
            {
                json.Add(new JProperty("x",           X));
                json.Add(new JProperty("y",           Y));
            }

            json.Add(new JProperty("value",           Value));

            if (Signatures.Count > 0)
                json.Add(new JProperty("signatures",  new JArray(Signatures.Select(signature => signature.ToJSON()))));

            if (Certainty.HasValue)
                json.Add(new JProperty("certainty",   Certainty.Value));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this public key.
        /// </summary>
        public override String ToString()

            => IsXY
                   ? $"x: {X}, y: {Y}"
                   : Value;

        #endregion


    }

}
