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
    /// A charging station: the physical device an EV driver plugs into, with one
    /// or more EVSEs.
    /// </summary>
    /// <param name="Id">The identification of the charging station.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="Manufacturer">An optional manufacturer.</param>
    /// <param name="Model">An optional device model.</param>
    /// <param name="Hardware">An optional hardware revision.</param>
    /// <param name="Firmware">An optional firmware.</param>
    /// <param name="LegalCompliance">Optional conformity and calibration certificates.</param>
    /// <param name="Address">An optional postal address.</param>
    /// <param name="GeoLocation">An optional geographical location.</param>
    /// <param name="ChargingPoolId">An optional identification of the charging pool.</param>
    /// <param name="EVSEs">The EVSEs of this charging station.</param>
    /// <param name="EVSEIds">Optional identifications of EVSEs belonging to this charging station.</param>
    /// <param name="EnergyMeters">The energy meters of this charging station.</param>
    /// <param name="ChargingTariffs">Optional charging tariffs.</param>
    /// <param name="PublicKeys">Optional public keys of the charging station.</param>
    public class ChargingStation(String                        Id,
                                 IEnumerable<String>?          Context          = null,
                                 I18NString?                   Description      = null,
                                 Manufacturer?                 Manufacturer     = null,
                                 DeviceModel?                  Model            = null,
                                 Hardware?                     Hardware         = null,
                                 Firmware?                     Firmware         = null,
                                 LegalCompliance?              LegalCompliance  = null,
                                 Address?                      Address          = null,
                                 GeoCoordinate?                GeoLocation      = null,
                                 String?                       ChargingPoolId   = null,
                                 IEnumerable<EVSE>?            EVSEs            = null,
                                 IEnumerable<String>?          EVSEIds          = null,
                                 IEnumerable<EnergyMeter>?     EnergyMeters     = null,
                                 IEnumerable<ChargingTariff>?  ChargingTariffs  = null,
                                 IEnumerable<PublicKey>?       PublicKeys       = null)
    {

        #region Properties

        /// <summary>The identification of the charging station.</summary>
        public String                          Id                        { get; }               = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>           JSONLDContext             { get; }               = Context?.        ToArray() ?? [];

        /// <summary>An optional multi-language description.</summary>
        public I18NString?                     Description               { get; }               = Description;

        /// <summary>An optional manufacturer.</summary>
        public Manufacturer?                   Manufacturer              { get; }               = Manufacturer;

        /// <summary>An optional device model.</summary>
        public DeviceModel?                    Model                     { get; }               = Model;

        /// <summary>An optional hardware revision.</summary>
        public Hardware?                       Hardware                  { get; }               = Hardware;

        /// <summary>An optional firmware.</summary>
        public Firmware?                       Firmware                  { get; }               = Firmware;

        /// <summary>Optional conformity and calibration certificates.</summary>
        public LegalCompliance?                LegalCompliance           { get; }               = LegalCompliance;

        /// <summary>An optional postal address.</summary>
        public Address?                        Address                   { get; }               = Address;

        /// <summary>An optional geographical location.</summary>
        public GeoCoordinate?                  GeoLocation               { get; }               = GeoLocation;

        /// <summary>The EVSEs of this charging station.</summary>
        public IReadOnlyList<EVSE>             EVSEs                     { get; }               = EVSEs?.          ToArray() ?? [];

        /// <summary>Optional identifications of EVSEs belonging to this charging station.</summary>
        public IReadOnlyList<String>           EVSEIds                   { get; }               = EVSEIds?.        ToArray() ?? [];

        /// <summary>The energy meters of this charging station.</summary>
        public IReadOnlyList<EnergyMeter>      EnergyMeters              { get; }               = EnergyMeters?.   ToArray() ?? [];

        /// <summary>Optional charging tariffs.</summary>
        public IReadOnlyList<ChargingTariff>   ChargingTariffs           { get; }               = ChargingTariffs?.ToArray() ?? [];

        /// <summary>Optional public keys of the charging station.</summary>
        public IReadOnlyList<PublicKey>        PublicKeys                { get; }               = PublicKeys?.     ToArray() ?? [];

        /// <summary>An optional identification of the charging pool.</summary>
        public String?                         ChargingPoolId            { get; internal set; } = ChargingPoolId;

        /// <summary>An optional identification of the charging station operator.</summary>
        public String?                         ChargingStationOperatorId { get; internal set; }

        /// <summary>
        /// The charging pool this charging station belongs to.
        /// Resolved while a charge transparency record is being assembled, and
        /// never serialized — see <see cref="ToJSON"/>.
        /// </summary>
        public ChargingPool?                   ChargingPool              { get; internal set; }

        /// <summary>
        /// The operator of this charging station.
        /// Resolved while a charge transparency record is being assembled.
        /// </summary>
        public ChargingStationOperator?        ChargingStationOperator   { get; internal set; }

        #endregion


        #region (static) TryParse(JSON, out ChargingStation)

        /// <summary>
        /// Try to parse the given JSON as a charging station.
        /// </summary>
        /// <param name="JSON">A JSON representation of a charging station.</param>
        /// <param name="ChargingStation">The parsed charging station.</param>
        public static Boolean TryParse(JObject JSON, out ChargingStation? ChargingStation)
        {

            ChargingStation = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            Manufacturer?    manufacturer    = null;
            DeviceModel?     model           = null;
            Hardware?        hardware        = null;
            Firmware?        firmware        = null;
            LegalCompliance? legalCompliance = null;
            Address?         address         = null;

            if (JSON["manufacturer"]    is JObject manufacturerJSON)
                chargy.Manufacturer.   TryParse(manufacturerJSON,    out manufacturer);

            if (JSON["model"]           is JObject modelJSON)
                chargy.DeviceModel.    TryParse(modelJSON,           out model);

            if (JSON["hardware"]        is JObject hardwareJSON)
                chargy.Hardware.       TryParse(hardwareJSON,        out hardware);

            if (JSON["firmware"]        is JObject firmwareJSON)
                chargy.Firmware.       TryParse(firmwareJSON,        out firmware);

            if (JSON["legalCompliance"] is JObject legalComplianceJSON)
                chargy.LegalCompliance.TryParse(legalComplianceJSON, out legalCompliance);

            if (JSON["address"]         is JObject addressJSON)
                chargy.Address.        TryParse(addressJSON,         out address);

            ChargingStation = new ChargingStation(
                                  id,
                                  PublicKey.ParseContext(JSON["@context"]),
                                  JSON["description"] is JObject descriptionJSON
                                      ? I18NString.Parse(descriptionJSON)
                                      : null,
                                  manufacturer,
                                  model,
                                  hardware,
                                  firmware,
                                  legalCompliance,
                                  address,
                                  JSON["geoLocation"] is JObject geoLocationJSON
                                      ? GeoCoordinate.TryParse(geoLocationJSON)
                                      : null,
                                  JSON["chargingPoolId"]?.Value<String>(),
                                  EntityLists.ParseEVSEs          (JSON["EVSEs"]),
                                  StringList. Parse               (JSON["EVSEIds"]),
                                  EntityLists.ParseEnergyMeters   (JSON["energyMeters"]),
                                  EntityLists.ParseChargingTariffs(JSON["chargingTariffs"]),
                                  PublicKeyList.Parse             (JSON["publicKeys"])
                              );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this charging station.
        ///
        /// Note: The resolved <see cref="ChargingPool"/> and
        /// <see cref="ChargingStationOperator"/> references are deliberately not
        /// serialized, only their identifications: both contain this station, so
        /// writing the references out would not terminate.
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

            if (Manufacturer               is not null)
                json.Add(new JProperty("manufacturer",               Manufacturer.ToJSON()));

            if (Model                      is not null)
                json.Add(new JProperty("model",                      Model.ToJSON()));

            if (Hardware                   is not null)
                json.Add(new JProperty("hardware",                   Hardware.ToJSON()));

            if (Firmware                   is not null)
                json.Add(new JProperty("firmware",                   Firmware.ToJSON()));

            if (LegalCompliance            is not null)
                json.Add(new JProperty("legalCompliance",            LegalCompliance.ToJSON()));

            if (Address                    is not null)
                json.Add(new JProperty("address",                    Address.ToJSON()));

            if (GeoLocation.HasValue)
                json.Add(new JProperty("geoLocation",                GeoLocation.Value.ToJSON()));

            if (ChargingStationOperatorId  is not null)
                json.Add(new JProperty("chargingStationOperatorId",  ChargingStationOperatorId));

            if (ChargingPoolId             is not null)
                json.Add(new JProperty("chargingPoolId",             ChargingPoolId));

            if (EVSEs.          Count > 0)
                json.Add(new JProperty("EVSEs",                      new JArray(EVSEs.          Select(evse      => evse.     ToJSON()))));

            if (EVSEIds.        Count > 0)
                json.Add(new JProperty("EVSEIds",                    new JArray(EVSEIds)));

            if (EnergyMeters.   Count > 0)
                json.Add(new JProperty("energyMeters",               new JArray(EnergyMeters.   Select(meter     => meter.    ToJSON()))));

            if (ChargingTariffs.Count > 0)
                json.Add(new JProperty("chargingTariffs",            new JArray(ChargingTariffs.Select(tariff    => tariff.   ToJSON()))));

            if (PublicKeys.     Count > 0)
                json.Add(new JProperty("publicKeys",                 new JArray(PublicKeys.     Select(publicKey => publicKey.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this charging station.
        /// </summary>
        public override String ToString()

            => Id;

        #endregion


    }

}
