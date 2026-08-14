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
    /// A cryptographic signature over an energy meter measurement.
    ///
    /// Every field is optional, because the supported charge transparency data
    /// formats disagree about which of them they carry: some name the algorithm
    /// and the format explicitly, others leave both implicit in the format
    /// identifier of the surrounding record.
    /// </summary>
    /// <param name="Value">The signature itself, usually hexadecimal.</param>
    /// <param name="Algorithm">An optional signature algorithm.</param>
    /// <param name="Format">An optional signature format.</param>
    /// <param name="PreviousValue">An optional signature of the previous measurement, for hash chained formats.</param>
    public class Signature(String?  Value          = null,
                           String?  Algorithm      = null,
                           String?  Format         = null,
                           String?  PreviousValue  = null)
    {

        #region Properties

        /// <summary>The signature itself, usually hexadecimal.</summary>
        public String?  Value            { get; } = Value;

        /// <summary>An optional signature algorithm.</summary>
        public String?  Algorithm        { get; } = Algorithm;

        /// <summary>An optional signature format.</summary>
        public String?  Format           { get; } = Format;

        /// <summary>An optional signature of the previous measurement, for hash chained formats.</summary>
        public String?  PreviousValue    { get; } = PreviousValue;

        #endregion


        #region (static) TryParse(JSON, out Signature)

        /// <summary>
        /// Try to parse the given JSON as a signature.
        /// </summary>
        /// <param name="JSON">A JSON representation of a signature.</param>
        /// <param name="Signature">The parsed signature.</param>
        public static Boolean TryParse(JObject JSON, out Signature? Signature)
        {

            // An "r"/"s" pair is a signature in its own right and must not be
            // flattened into a plain signature, or its two halves would be lost.
            if (SignatureRS.TryParse(JSON, out var signatureRS))
            {
                Signature = signatureRS;
                return true;
            }

            Signature = new Signature(
                            JSON["value"]?.        Value<String>(),
                            JSON["algorithm"]?.    Value<String>(),
                            JSON["format"]?.       Value<String>(),
                            JSON["previousValue"]?.Value<String>()
                        );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this signature.
        /// </summary>
        public virtual JObject ToJSON()
        {

            var json = new JObject();

            if (Algorithm     is not null)
                json.Add(new JProperty("algorithm",      Algorithm));

            if (Format        is not null)
                json.Add(new JProperty("format",         Format));

            if (PreviousValue is not null)
                json.Add(new JProperty("previousValue",  PreviousValue));

            if (Value         is not null)
                json.Add(new JProperty("value",          Value));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this signature.
        /// </summary>
        public override String ToString()

            => Value ?? "<no signature>";

        #endregion


    }


    /// <summary>
    /// An ECDSA signature carried as its two integers r and s, rather than as a
    /// single DER encoded blob.
    /// </summary>
    /// <param name="R">The r value of the signature.</param>
    /// <param name="S">The s value of the signature.</param>
    /// <param name="Value">An optional combined representation of the signature.</param>
    /// <param name="Algorithm">An optional signature algorithm.</param>
    /// <param name="Format">An optional signature format.</param>
    /// <param name="PreviousValue">An optional signature of the previous measurement.</param>
    public class SignatureRS(String   R,
                             String   S,
                             String?  Value          = null,
                             String?  Algorithm      = null,
                             String?  Format         = null,
                             String?  PreviousValue  = null)

        : Signature(Value,
                    Algorithm,
                    Format,
                    PreviousValue)

    {

        #region Properties

        /// <summary>The r value of the signature.</summary>
        public String  R    { get; } = R;

        /// <summary>The s value of the signature.</summary>
        public String  S    { get; } = S;

        #endregion


        #region (static) TryParse(JSON, out SignatureRS)

        /// <summary>
        /// Try to parse the given JSON as an r/s signature.
        /// </summary>
        /// <param name="JSON">A JSON representation of an r/s signature.</param>
        /// <param name="SignatureRS">The parsed r/s signature.</param>
        public static Boolean TryParse(JObject JSON, out SignatureRS? SignatureRS)
        {

            SignatureRS = null;

            var r = JSON["r"]?.Value<String>();
            var s = JSON["s"]?.Value<String>();

            if (r is null || s is null)
                return false;

            SignatureRS = new SignatureRS(
                              r,
                              s,
                              JSON["value"]?.        Value<String>(),
                              JSON["algorithm"]?.    Value<String>(),
                              JSON["format"]?.       Value<String>(),
                              JSON["previousValue"]?.Value<String>()
                          );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this r/s signature.
        /// </summary>
        public override JObject ToJSON()
        {

            var json = base.ToJSON();

            json.Add(new JProperty("r", R));
            json.Add(new JProperty("s", S));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this r/s signature.
        /// </summary>
        public override String ToString()

            => $"r: {R}, s: {S}";

        #endregion


    }

}
