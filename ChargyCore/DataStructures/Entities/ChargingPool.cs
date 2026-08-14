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
    /// A group of charging stations at the same location, e.g. all stations of a
    /// car park.
    /// </summary>
    /// <param name="Id">The identification of the charging pool.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="Address">An optional postal address.</param>
    /// <param name="GeoLocation">An optional geographical location.</param>
    /// <param name="ChargingStations">The charging stations of this charging pool.</param>
    /// <param name="ChargingTariffs">Optional charging tariffs.</param>
    /// <param name="PublicKeys">Optional public keys of the charging pool.</param>
    public class ChargingPool(String                          Id,
                              IEnumerable<String>?            Context           = null,
                              I18NString?                     Description       = null,
                              Address?                        Address           = null,
                              GeoCoordinate?                  GeoLocation       = null,
                              IEnumerable<ChargingStation>?   ChargingStations  = null,
                              IEnumerable<ChargingTariff>?    ChargingTariffs   = null,
                              IEnumerable<PublicKey>?         PublicKeys        = null)
    {

        #region Properties

        /// <summary>The identification of the charging pool.</summary>
        public String                            Id                         { get; }               = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>             JSONLDContext              { get; }               = Context?.         ToArray() ?? [];

        /// <summary>An optional multi-language description.</summary>
        public I18NString?                       Description                { get; }               = Description;

        /// <summary>An optional postal address.</summary>
        public Address?                          Address                    { get; }               = Address;

        /// <summary>An optional geographical location.</summary>
        public GeoCoordinate?                    GeoLocation                { get; }               = GeoLocation;

        /// <summary>The charging stations of this charging pool.</summary>
        public IReadOnlyList<ChargingStation>    ChargingStations           { get; }               = ChargingStations?.ToArray() ?? [];

        /// <summary>Optional charging tariffs.</summary>
        public IReadOnlyList<ChargingTariff>     ChargingTariffs            { get; }               = ChargingTariffs?. ToArray() ?? [];

        /// <summary>Optional public keys of the charging pool.</summary>
        public IReadOnlyList<PublicKey>          PublicKeys                 { get; }               = PublicKeys?.      ToArray() ?? [];

        /// <summary>An optional identification of the charging station operator.</summary>
        public String?                           ChargingStationOperatorId  { get; internal set; }

        /// <summary>
        /// The operator of this charging pool.
        /// Resolved while a charge transparency record is being assembled, and
        /// never serialized — see <see cref="ToJSON"/>.
        /// </summary>
        public ChargingStationOperator?          ChargingStationOperator    { get; internal set; }

        #endregion


        #region (static) TryParse(JSON, out ChargingPool)

        /// <summary>
        /// Try to parse the given JSON as a charging pool.
        /// </summary>
        /// <param name="JSON">A JSON representation of a charging pool.</param>
        /// <param name="ChargingPool">The parsed charging pool.</param>
        public static Boolean TryParse(JObject JSON, out ChargingPool? ChargingPool)
        {

            ChargingPool = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            Address? address = null;

            if (JSON["address"] is JObject addressJSON)
                chargy.Address.TryParse(addressJSON, out address);

            var chargingStations = new List<ChargingStation>();

            if (JSON["chargingStations"] is JArray chargingStationArray)
                foreach (var chargingStationJSON in chargingStationArray.OfType<JObject>())
                    if (ChargingStation.TryParse(chargingStationJSON, out var chargingStation))
                        chargingStations.Add(chargingStation!);

            ChargingPool = new ChargingPool(
                               id,
                               PublicKey.ParseContext(JSON["@context"]),
                               JSON["description"] is JObject descriptionJSON
                                   ? I18NString.Parse(descriptionJSON)
                                   : null,
                               address,
                               JSON["geoLocation"] is JObject geoLocationJSON
                                   ? GeoCoordinate.TryParse(geoLocationJSON)
                                   : null,
                               chargingStations,
                               EntityLists.ParseChargingTariffs(JSON["chargingTariffs"]),
                               PublicKeyList.Parse(JSON["publicKeys"])
                           );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this charging pool.
        ///
        /// Note: The resolved <see cref="ChargingStationOperator"/> reference is
        /// deliberately not serialized, only its identification: the operator
        /// contains this pool, so writing the reference out would not terminate.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",                   JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",                   new JArray(JSONLDContext)));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",                Description.ToJSON()));

            if (Address                    is not null)
                json.Add(new JProperty("address",                    Address.ToJSON()));

            if (GeoLocation.HasValue)
                json.Add(new JProperty("geoLocation",                GeoLocation.Value.ToJSON()));

            if (ChargingStationOperatorId  is not null)
                json.Add(new JProperty("chargingStationOperatorId",  ChargingStationOperatorId));

            if (ChargingStations.Count > 0)
                json.Add(new JProperty("chargingStations",           new JArray(ChargingStations.Select(station   => station.  ToJSON()))));

            if (ChargingTariffs. Count > 0)
                json.Add(new JProperty("chargingTariffs",            new JArray(ChargingTariffs. Select(tariff    => tariff.   ToJSON()))));

            if (PublicKeys.      Count > 0)
                json.Add(new JProperty("publicKeys",                 new JArray(PublicKeys.      Select(publicKey => publicKey.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this charging pool.
        /// </summary>
        public override String ToString()

            => Id;

        #endregion


    }

}
