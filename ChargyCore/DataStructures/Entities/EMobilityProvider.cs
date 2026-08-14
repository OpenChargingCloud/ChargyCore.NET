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

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// The e-mobility provider an EV driver has their charging contract with —
    /// usually not the same company as the operator of the charging station.
    /// </summary>
    /// <param name="Id">The identification of the e-mobility provider.</param>
    /// <param name="Description">A multi-language description.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="ChargingTariffs">The charging tariffs of this provider.</param>
    /// <param name="PublicKeys">Optional public keys of the provider.</param>
    public class EMobilityProvider(String                        Id,
                                   I18NString                    Description,
                                   IEnumerable<String>?          Context          = null,
                                   IEnumerable<ChargingTariff>?  ChargingTariffs  = null,
                                   IEnumerable<PublicKey>?       PublicKeys       = null)
    {

        #region Properties

        /// <summary>The identification of the e-mobility provider.</summary>
        public String                          Id                 { get; } = Id;

        /// <summary>A multi-language description.</summary>
        public I18NString                      Description        { get; } = Description;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>           JSONLDContext      { get; } = Context?.        ToArray() ?? [];

        /// <summary>The charging tariffs of this provider.</summary>
        public IReadOnlyList<ChargingTariff>   ChargingTariffs    { get; } = ChargingTariffs?.ToArray() ?? [];

        /// <summary>Optional public keys of the provider.</summary>
        public IReadOnlyList<PublicKey>        PublicKeys         { get; } = PublicKeys?.     ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out EMobilityProvider)

        /// <summary>
        /// Try to parse the given JSON as an e-mobility provider.
        /// </summary>
        /// <param name="JSON">A JSON representation of an e-mobility provider.</param>
        /// <param name="EMobilityProvider">The parsed e-mobility provider.</param>
        public static Boolean TryParse(JObject JSON, out EMobilityProvider? EMobilityProvider)
        {

            EMobilityProvider = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            EMobilityProvider = new EMobilityProvider(
                                    id,
                                    JSON["description"] is JObject descriptionJSON
                                        ? I18NString.Parse(descriptionJSON) ?? I18NString.Empty
                                        : I18NString.Empty,
                                    PublicKey.ParseContext(JSON["@context"]),
                                    EntityLists.ParseChargingTariffs(JSON["chargingTariffs"]),
                                    PublicKeyList.Parse(JSON["publicKeys"])
                                );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this e-mobility provider.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",         JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",         new JArray(JSONLDContext)));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",      Description.ToJSON()));

            if (ChargingTariffs.Count > 0)
                json.Add(new JProperty("chargingTariffs",  new JArray(ChargingTariffs.Select(tariff    => tariff.   ToJSON()))));

            if (PublicKeys.     Count > 0)
                json.Add(new JProperty("publicKeys",       new JArray(PublicKeys.     Select(publicKey => publicKey.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this e-mobility provider.
        /// </summary>
        public override String ToString()

            => Id;

        #endregion


    }


    /// <summary>
    /// The charging contract a charge transparency record was produced under.
    /// </summary>
    /// <param name="Id">The identification of the contract.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="Type">An optional contract type.</param>
    /// <param name="Username">An optional user name.</param>
    /// <param name="EMail">An optional e-mail address.</param>
    public class Contract(String                Id,
                          IEnumerable<String>?  Context      = null,
                          I18NString?           Description  = null,
                          String?               Type         = null,
                          String?               Username     = null,
                          String?               EMail        = null)
    {

        #region Properties

        /// <summary>The identification of the contract.</summary>
        public String                 Id               { get; } = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>  JSONLDContext    { get; } = Context?.ToArray() ?? [];

        /// <summary>An optional multi-language description.</summary>
        public I18NString?            Description      { get; } = Description;

        /// <summary>An optional contract type.</summary>
        public String?                Type             { get; } = Type;

        /// <summary>An optional user name.</summary>
        public String?                Username         { get; } = Username;

        /// <summary>An optional e-mail address.</summary>
        public String?                EMail            { get; } = EMail;

        #endregion


        #region (static) TryParse(JSON, out Contract)

        /// <summary>
        /// Try to parse the given JSON as a contract.
        /// </summary>
        /// <param name="JSON">A JSON representation of a contract.</param>
        /// <param name="Contract">The parsed contract.</param>
        public static Boolean TryParse(JObject JSON, out Contract? Contract)
        {

            Contract = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            Contract = new Contract(
                           id,
                           PublicKey.ParseContext(JSON["@context"]),
                           JSON["description"] is JObject descriptionJSON
                               ? I18NString.Parse(descriptionJSON)
                               : null,
                           JSON["type"]?.    Value<String>(),
                           JSON["username"]?.Value<String>(),
                           JSON["email"]?.   Value<String>()
                       );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this contract.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",     JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",     new JArray(JSONLDContext)));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",  Description.ToJSON()));

            if (Type     is not null)
                json.Add(new JProperty("type",         Type));

            if (Username is not null)
                json.Add(new JProperty("username",     Username));

            if (EMail    is not null)
                json.Add(new JProperty("email",        EMail));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this contract.
        /// </summary>
        public override String ToString()

            => Id;

        #endregion


    }

}
