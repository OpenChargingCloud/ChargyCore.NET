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

using org.GraphDefined.Vanaheimr.Aegir;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// The operator of a charging station: whom an EV driver has a contract with
    /// about the charging process, and whom to ask about a disputed bill.
    /// </summary>
    /// <param name="Id">The identification of the charging station operator.</param>
    /// <param name="Contact">How to reach the operator.</param>
    /// <param name="Support">Where an EV driver can get help.</param>
    /// <param name="Privacy">Whom to ask about the personal data of a record.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="SubCSOIds">Optional identifications of sub-operators.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="GeoLocation">An optional geographical location.</param>
    /// <param name="ChargingPools">The charging pools of this operator.</param>
    /// <param name="ChargingStations">The charging stations of this operator.</param>
    /// <param name="EVSEs">The EVSEs of this operator.</param>
    /// <param name="ChargingTariffs">Optional charging tariffs.</param>
    /// <param name="ParkingTariffs">Optional parking tariffs.</param>
    /// <param name="PublicKeys">Optional public keys of the operator.</param>
    public class ChargingStationOperator(String                         Id,
                                         Contact                        Contact,
                                         Support                        Support,
                                         PrivacyContact                 Privacy,
                                         IEnumerable<String>?           Context           = null,
                                         IEnumerable<String>?           SubCSOIds         = null,
                                         I18NString?                    Description       = null,
                                         GeoCoordinate?                 GeoLocation       = null,
                                         IEnumerable<ChargingPool>?     ChargingPools     = null,
                                         IEnumerable<ChargingStation>?  ChargingStations  = null,
                                         IEnumerable<EVSE>?             EVSEs             = null,
                                         IEnumerable<ChargingTariff>?   ChargingTariffs   = null,
                                         IEnumerable<ParkingTariff>?    ParkingTariffs    = null,
                                         IEnumerable<PublicKey>?        PublicKeys        = null)
    {

        #region Properties

        /// <summary>The identification of the charging station operator.</summary>
        public String                            Id                  { get; } = Id;

        /// <summary>How to reach the operator.</summary>
        public Contact                           Contact             { get; } = Contact;

        /// <summary>Where an EV driver can get help.</summary>
        public Support                           Support             { get; } = Support;

        /// <summary>Whom to ask about the personal data of a record.</summary>
        public PrivacyContact                    Privacy             { get; } = Privacy;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>             JSONLDContext       { get; } = Context?.         ToArray() ?? [];

        /// <summary>Optional identifications of sub-operators.</summary>
        public IReadOnlyList<String>             SubCSOIds           { get; } = SubCSOIds?.       ToArray() ?? [];

        /// <summary>An optional multi-language description.</summary>
        public I18NString?                       Description         { get; } = Description;

        /// <summary>An optional geographical location.</summary>
        public GeoCoordinate?                    GeoLocation         { get; } = GeoLocation;

        /// <summary>The charging pools of this operator.</summary>
        public IReadOnlyList<ChargingPool>       ChargingPools       { get; } = ChargingPools?.   ToArray() ?? [];

        /// <summary>The charging stations of this operator.</summary>
        public IReadOnlyList<ChargingStation>    ChargingStations    { get; } = ChargingStations?.ToArray() ?? [];

        /// <summary>The EVSEs of this operator.</summary>
        public IReadOnlyList<EVSE>               EVSEs               { get; } = EVSEs?.           ToArray() ?? [];

        /// <summary>Optional charging tariffs.</summary>
        public IReadOnlyList<ChargingTariff>     ChargingTariffs     { get; } = ChargingTariffs?. ToArray() ?? [];

        /// <summary>Optional parking tariffs.</summary>
        public IReadOnlyList<ParkingTariff>      ParkingTariffs      { get; } = ParkingTariffs?.  ToArray() ?? [];

        /// <summary>Optional public keys of the operator.</summary>
        public IReadOnlyList<PublicKey>          PublicKeys          { get; } = PublicKeys?.      ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out ChargingStationOperator)

        /// <summary>
        /// Try to parse the given JSON as a charging station operator.
        /// </summary>
        /// <param name="JSON">A JSON representation of a charging station operator.</param>
        /// <param name="ChargingStationOperator">The parsed charging station operator.</param>
        public static Boolean TryParse(JObject JSON, out ChargingStationOperator? ChargingStationOperator)
        {

            ChargingStationOperator = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            Contact?        contact = null;
            Support?        support = null;
            PrivacyContact? privacy = null;

            if (JSON["contact"] is JObject contactJSON)
                chargy.Contact.       TryParse(contactJSON, out contact);

            if (JSON["support"] is JObject supportJSON)
                chargy.Support.       TryParse(supportJSON, out support);

            if (JSON["privacy"] is JObject privacyJSON)
                chargy.PrivacyContact.TryParse(privacyJSON, out privacy);

            // Contact, support and privacy are mandatory in the CTR format: an
            // operator an EV driver cannot reach is of no use in a dispute.
            if (contact is null || support is null || privacy is null)
                return false;

            var chargingPools    = new List<ChargingPool>();
            var chargingStations = new List<ChargingStation>();

            if (JSON["chargingPools"]    is JArray chargingPoolArray)
                foreach (var chargingPoolJSON in chargingPoolArray.OfType<JObject>())
                    if (ChargingPool.   TryParse(chargingPoolJSON,    out var chargingPool))
                        chargingPools.   Add(chargingPool!);

            if (JSON["chargingStations"] is JArray chargingStationArray)
                foreach (var chargingStationJSON in chargingStationArray.OfType<JObject>())
                    if (ChargingStation.TryParse(chargingStationJSON, out var chargingStation))
                        chargingStations.Add(chargingStation!);

            ChargingStationOperator = new ChargingStationOperator(
                                          id,
                                          contact,
                                          support,
                                          privacy,
                                          PublicKey.ParseContext(JSON["@context"]),
                                          StringList.Parse(JSON["subCSOIds"]),
                                          JSON["description"] is JObject descriptionJSON
                                              ? I18NString.Parse(descriptionJSON)
                                              : null,
                                          JSON["geoLocation"] is JObject geoLocationJSON
                                              ? GeoCoordinate.TryParse(geoLocationJSON)
                                              : null,
                                          chargingPools,
                                          chargingStations,
                                          EntityLists.ParseEVSEs          (JSON["EVSEs"]),
                                          EntityLists.ParseChargingTariffs(JSON["chargingTariffs"]),
                                          EntityLists.ParseParkingTariffs (JSON["parkingTariffs"]),
                                          PublicKeyList.Parse             (JSON["publicKeys"])
                                      );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this charging station operator.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",          JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",          new JArray(JSONLDContext)));

            if (SubCSOIds.Count > 0)
                json.Add(new JProperty("subCSOIds",         new JArray(SubCSOIds)));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",       Description.ToJSON()));

            json.Add(new JProperty("contact",               Contact.ToJSON()));
            json.Add(new JProperty("support",               Support.ToJSON()));
            json.Add(new JProperty("privacy",               Privacy.ToJSON()));

            if (GeoLocation.HasValue)
                json.Add(new JProperty("geoLocation",       GeoLocation.Value.ToJSON()));

            if (ChargingPools.   Count > 0)
                json.Add(new JProperty("chargingPools",     new JArray(ChargingPools.   Select(pool      => pool.     ToJSON()))));

            if (ChargingStations.Count > 0)
                json.Add(new JProperty("chargingStations",  new JArray(ChargingStations.Select(station   => station.  ToJSON()))));

            if (EVSEs.           Count > 0)
                json.Add(new JProperty("EVSEs",             new JArray(EVSEs.           Select(evse      => evse.     ToJSON()))));

            if (ChargingTariffs. Count > 0)
                json.Add(new JProperty("chargingTariffs",   new JArray(ChargingTariffs. Select(tariff    => tariff.   ToJSON()))));

            if (ParkingTariffs.  Count > 0)
                json.Add(new JProperty("parkingTariffs",    new JArray(ParkingTariffs.  Select(tariff    => tariff.   ToJSON()))));

            if (PublicKeys.      Count > 0)
                json.Add(new JProperty("publicKeys",        new JArray(PublicKeys.      Select(publicKey => publicKey.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this charging station operator.
        /// </summary>
        public override String ToString()

            => Id;

        #endregion


    }

}
