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
    /// The charging cable of a connector, including the resistance used to
    /// compensate for the energy lost in it.
    ///
    /// The loss compensation matters for the bill: some charging stations meter
    /// before the cable and then subtract the calculated loss, and an EV driver
    /// has to be able to see that this happened.
    /// </summary>
    /// <param name="Length">An optional cable length.</param>
    /// <param name="Resistance">An optional cable resistance.</param>
    /// <param name="ResistanceUnit">An optional unit of the resistance.</param>
    /// <param name="LossCompensation">An optional description of the loss compensation.</param>
    /// <param name="LossCompensationId">An optional identification of the loss compensation.</param>
    public class Cable(Decimal?  Length              = null,
                       Decimal?  Resistance          = null,
                       String?   ResistanceUnit      = null,
                       String?   LossCompensation    = null,
                       String?   LossCompensationId  = null)
    {

        #region Properties

        /// <summary>An optional cable length.</summary>
        public Decimal?  Length                { get; } = Length;

        /// <summary>An optional cable resistance.</summary>
        public Decimal?  Resistance            { get; } = Resistance;

        /// <summary>An optional unit of the resistance.</summary>
        public String?   ResistanceUnit        { get; } = ResistanceUnit;

        /// <summary>An optional description of the loss compensation.</summary>
        public String?   LossCompensation      { get; } = LossCompensation;

        /// <summary>An optional identification of the loss compensation.</summary>
        public String?   LossCompensationId    { get; } = LossCompensationId;

        #endregion


        #region (static) TryParse(JSON, out Cable)

        /// <summary>
        /// Try to parse the given JSON as a charging cable.
        /// </summary>
        /// <param name="JSON">A JSON representation of a charging cable.</param>
        /// <param name="Cable">The parsed charging cable.</param>
        public static Boolean TryParse(JObject JSON, out Cable? Cable)
        {

            Cable = new Cable(
                        JSON["length"]?.            Value<Decimal>(),
                        JSON["resistance"]?.        Value<Decimal>(),
                        JSON["resistanceUnit"]?.    Value<String>(),
                        JSON["lossCompensation"]?.  Value<String>(),
                        JSON["lossCompensationId"]?.Value<String>()
                    );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this charging cable.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Length.    HasValue)
                json.Add(new JProperty("length",              Length.    Value));

            if (LossCompensation   is not null)
                json.Add(new JProperty("lossCompensation",    LossCompensation));

            if (LossCompensationId is not null)
                json.Add(new JProperty("lossCompensationId",  LossCompensationId));

            if (Resistance.HasValue)
                json.Add(new JProperty("resistance",          Resistance.Value));

            if (ResistanceUnit     is not null)
                json.Add(new JProperty("resistanceUnit",      ResistanceUnit));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this charging cable.
        /// </summary>
        public override String ToString()

            => $"{Length?.ToString() ?? "?"} m, {Resistance?.ToString() ?? "?"} {ResistanceUnit ?? ""}".Trim();

        #endregion


    }


    /// <summary>
    /// A socket or a tethered cable of an EVSE.
    /// </summary>
    /// <param name="Id">An optional identification of the connector.</param>
    /// <param name="Type">An optional connector type, e.g. "IEC_62196_T2".</param>
    /// <param name="Cable">An optional charging cable.</param>
    public class Connector(String?  Id     = null,
                           String?  Type   = null,
                           Cable?   Cable  = null)
    {

        #region Properties

        /// <summary>An optional identification of the connector.</summary>
        public String?  Id       { get; } = Id;

        /// <summary>An optional connector type, e.g. "IEC_62196_T2".</summary>
        public String?  Type     { get; } = Type;

        /// <summary>An optional charging cable.</summary>
        public Cable?   Cable    { get; } = Cable;

        #endregion


        #region (static) TryParse(JSON, out Connector)

        /// <summary>
        /// Try to parse the given JSON as a connector.
        /// </summary>
        /// <param name="JSON">A JSON representation of a connector.</param>
        /// <param name="Connector">The parsed connector.</param>
        public static Boolean TryParse(JObject JSON, out Connector? Connector)
        {

            Cable? cable = null;

            if (JSON["cable"] is JObject cableJSON)
                chargy.Cable.TryParse(cableJSON, out cable);

            Connector = new Connector(
                            JSON["@id"]?. Value<String>(),
                            JSON["type"]?.Value<String>(),
                            cable
                        );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this connector.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Id    is not null)
                json.Add(new JProperty("@id",    Id));

            if (Type  is not null)
                json.Add(new JProperty("type",   Type));

            if (Cable is not null)
                json.Add(new JProperty("cable",  Cable.ToJSON()));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this connector.
        /// </summary>
        public override String ToString()

            => Id ?? Type ?? "<unknown connector>";

        #endregion


    }


    /// <summary>
    /// An Electric Vehicle Supply Equipment: the part of a charging station that
    /// can charge one vehicle at a time, and the unit an EV driver's bill refers to.
    /// </summary>
    /// <param name="Id">The identification of the EVSE.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="ChargingPoolId">An optional identification of the charging pool.</param>
    /// <param name="ChargingStationId">An optional identification of the charging station.</param>
    /// <param name="EnergyMeters">The energy meters of this EVSE.</param>
    /// <param name="Connectors">The connectors of this EVSE.</param>
    /// <param name="ChargingTariffs">Optional charging tariffs.</param>
    /// <param name="PublicKeys">Optional public keys of the EVSE.</param>
    public class EVSE(String                        Id,
                      IEnumerable<String>?          Context            = null,
                      I18NString?                   Description        = null,
                      String?                       ChargingPoolId     = null,
                      String?                       ChargingStationId  = null,
                      IEnumerable<EnergyMeter>?     EnergyMeters       = null,
                      IEnumerable<Connector>?       Connectors         = null,
                      IEnumerable<ChargingTariff>?  ChargingTariffs    = null,
                      IEnumerable<PublicKey>?       PublicKeys         = null)
    {

        #region Properties

        /// <summary>The identification of the EVSE.</summary>
        public String                          Id                   { get; }               = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>           JSONLDContext        { get; }               = Context?.        ToArray() ?? [];

        /// <summary>An optional multi-language description.</summary>
        public I18NString?                     Description          { get; }               = Description;

        /// <summary>The energy meters of this EVSE.</summary>
        public IReadOnlyList<EnergyMeter>      EnergyMeters         { get; }               = EnergyMeters?.   ToArray() ?? [];

        /// <summary>The connectors of this EVSE.</summary>
        public IReadOnlyList<Connector>        Connectors           { get; }               = Connectors?.     ToArray() ?? [];

        /// <summary>Optional charging tariffs.</summary>
        public IReadOnlyList<ChargingTariff>   ChargingTariffs      { get; }               = ChargingTariffs?.ToArray() ?? [];

        /// <summary>Optional public keys of the EVSE.</summary>
        public IReadOnlyList<PublicKey>        PublicKeys           { get; }               = PublicKeys?.     ToArray() ?? [];

        /// <summary>An optional identification of the charging pool.</summary>
        public String?                         ChargingPoolId       { get; internal set; } = ChargingPoolId;

        /// <summary>An optional identification of the charging station.</summary>
        public String?                         ChargingStationId    { get; internal set; } = ChargingStationId;

        /// <summary>
        /// The charging station this EVSE belongs to.
        /// Resolved while a charge transparency record is being assembled, and
        /// never serialized — see <see cref="ToJSON"/>.
        /// </summary>
        public ChargingStation?                ChargingStation      { get; internal set; }

        #endregion


        #region (static) TryParse(JSON, out EVSE)

        /// <summary>
        /// Try to parse the given JSON as an EVSE.
        /// </summary>
        /// <param name="JSON">A JSON representation of an EVSE.</param>
        /// <param name="EVSE">The parsed EVSE.</param>
        public static Boolean TryParse(JObject JSON, out EVSE? EVSE)
        {

            EVSE = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            var energyMeters = new List<EnergyMeter>();
            var connectors   = new List<Connector>();

            if (JSON["energyMeters"] is JArray energyMeterArray)
                foreach (var energyMeterJSON in energyMeterArray.OfType<JObject>())
                    if (EnergyMeter.TryParse(energyMeterJSON, out var energyMeter))
                        energyMeters.Add(energyMeter!);

            if (JSON["connectors"]   is JArray connectorArray)
                foreach (var connectorJSON in connectorArray.OfType<JObject>())
                    if (Connector.  TryParse(connectorJSON,   out var connector))
                        connectors.  Add(connector!);

            EVSE = new EVSE(
                       id,
                       PublicKey.ParseContext(JSON["@context"]),
                       JSON["description"] is JObject descriptionJSON
                           ? I18NString.Parse(descriptionJSON)
                           : null,
                       JSON["chargingPoolId"]?.   Value<String>(),
                       JSON["chargingStationId"]?.Value<String>(),
                       energyMeters,
                       connectors,
                       EntityLists.ParseChargingTariffs(JSON["chargingTariffs"]),
                       PublicKeyList.Parse(JSON["publicKeys"])
                   );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this EVSE.
        ///
        /// Note: The resolved <see cref="ChargingStation"/> reference is
        /// deliberately not serialized, only its identification: the station
        /// contains this EVSE, so writing the reference out would not terminate.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",           JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",           new JArray(JSONLDContext)));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",        Description.ToJSON()));

            if (ChargingPoolId    is not null)
                json.Add(new JProperty("chargingPoolId",     ChargingPoolId));

            if (ChargingStationId is not null)
                json.Add(new JProperty("chargingStationId",  ChargingStationId));

            if (EnergyMeters.   Count > 0)
                json.Add(new JProperty("energyMeters",       new JArray(EnergyMeters.   Select(meter     => meter.    ToJSON()))));

            if (Connectors.     Count > 0)
                json.Add(new JProperty("connectors",         new JArray(Connectors.     Select(connector => connector.ToJSON()))));

            if (ChargingTariffs.Count > 0)
                json.Add(new JProperty("chargingTariffs",    new JArray(ChargingTariffs.Select(tariff    => tariff.   ToJSON()))));

            if (PublicKeys.     Count > 0)
                json.Add(new JProperty("publicKeys",         new JArray(PublicKeys.     Select(publicKey => publicKey.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this EVSE.
        /// </summary>
        public override String ToString()

            => Id;

        #endregion


    }


    /// <summary>
    /// Helpers for the repeating list shapes of the charging infrastructure entities.
    /// </summary>
    internal static class EntityLists
    {

        #region ParseChargingTariffs(JSON)

        internal static List<ChargingTariff> ParseChargingTariffs(JToken? JSON)
        {

            var tariffs = new List<ChargingTariff>();

            if (JSON is JArray array)
                foreach (var tariffJSON in array.OfType<JObject>())
                    if (ChargingTariff.TryParse(tariffJSON, out var tariff))
                        tariffs.Add(tariff!);

            return tariffs;

        }

        #endregion

        #region ParseParkingTariffs (JSON)

        internal static List<ParkingTariff> ParseParkingTariffs(JToken? JSON)
        {

            var tariffs = new List<ParkingTariff>();

            if (JSON is JArray array)
                foreach (var tariffJSON in array.OfType<JObject>())
                    if (ParkingTariff.TryParse(tariffJSON, out var tariff))
                        tariffs.Add(tariff!);

            return tariffs;

        }

        #endregion

        #region ParseEVSEs          (JSON)

        internal static List<EVSE> ParseEVSEs(JToken? JSON)
        {

            var evses = new List<EVSE>();

            if (JSON is JArray array)
                foreach (var evseJSON in array.OfType<JObject>())
                    if (EVSE.TryParse(evseJSON, out var evse))
                        evses.Add(evse!);

            return evses;

        }

        #endregion

        #region ParseEnergyMeters   (JSON)

        internal static List<EnergyMeter> ParseEnergyMeters(JToken? JSON)
        {

            var energyMeters = new List<EnergyMeter>();

            if (JSON is JArray array)
                foreach (var energyMeterJSON in array.OfType<JObject>())
                    if (EnergyMeter.TryParse(energyMeterJSON, out var energyMeter))
                        energyMeters.Add(energyMeter!);

            return energyMeters;

        }

        #endregion

    }

}
