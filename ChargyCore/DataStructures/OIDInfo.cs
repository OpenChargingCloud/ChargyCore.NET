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
    /// An algorithm or key type, given either as a bare name or as an ASN.1
    /// object identifier together with its name.
    ///
    /// Charge transparency records in the wild use both spellings for the very
    /// same thing — "ECDSA-secp256r1-SHA256" as a string, or
    /// { "oid": "1.2.840.10045.4.3.2", "name": "ecdsa-with-SHA256" } as an object.
    /// This type accepts either and always answers with a name.
    /// </summary>
    /// <param name="Name">The name of the algorithm or key type.</param>
    /// <param name="OID">An optional ASN.1 object identifier.</param>
    public class OIDInfo(String   Name,
                         String?  OID = null)
    {

        #region Properties

        /// <summary>The name of the algorithm or key type.</summary>
        public String   Name    { get; } = Name;

        /// <summary>An optional ASN.1 object identifier.</summary>
        public String?  OID     { get; } = OID;

        #endregion


        #region (static) TryParse(JSON)

        /// <summary>
        /// Try to parse the given JSON as an algorithm or key type, accepting both
        /// a bare string and an { oid, name } object.
        /// </summary>
        /// <param name="JSON">A JSON representation of an algorithm or key type.</param>
        public static OIDInfo? TryParse(JToken? JSON)
        {

            if (JSON is null)
                return null;

            if (JSON.Type == JTokenType.String)
            {

                var name = JSON.Value<String>();

                return String.IsNullOrWhiteSpace(name)
                           ? null
                           : new OIDInfo(name);

            }

            if (JSON is JObject json)
            {

                var oid   = json["oid"]?. Value<String>();
                var name  = json["name"]?.Value<String>();

                if (oid is not null && name is not null)
                    return new OIDInfo(name, oid);

            }

            return null;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this algorithm or key type: a bare
        /// string when there is no object identifier, an object otherwise.
        /// </summary>
        public JToken ToJSON()

            => OID is null
                   ? new JValue(Name)
                   : new JObject(
                         new JProperty("oid",   OID),
                         new JProperty("name",  Name)
                     );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this algorithm or key type.
        /// </summary>
        public override String ToString()

            => Name;

        #endregion


    }

}
