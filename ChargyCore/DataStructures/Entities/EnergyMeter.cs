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
    /// The energy meter that produced the signed readings of a charging session.
    ///
    /// This is the device the German Calibration Law is actually about: its
    /// calibration certificate, its firmware and its public key are what makes a
    /// charging bill verifiable.
    /// </summary>
    /// <param name="Id">The identification of the energy meter.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="Manufacturer">An optional manufacturer.</param>
    /// <param name="Model">An optional device model.</param>
    /// <param name="Firmware">An optional firmware.</param>
    /// <param name="Hardware">An optional hardware revision.</param>
    /// <param name="LegalCompliance">Optional conformity and calibration certificates.</param>
    /// <param name="ChargingPoolId">An optional identification of the charging pool.</param>
    /// <param name="ChargingStationId">An optional identification of the charging station.</param>
    /// <param name="EVSEId">An optional identification of the EVSE.</param>
    /// <param name="SignatureInfos">Optional information about how this meter signs.</param>
    /// <param name="SignatureFormat">An optional signature format identifier.</param>
    /// <param name="PublicKeys">Optional public keys of the energy meter.</param>
    public class EnergyMeter(String                   Id,
                             IEnumerable<String>?     Context            = null,
                             I18NString?              Description        = null,
                             Manufacturer?            Manufacturer       = null,
                             DeviceModel?             Model              = null,
                             Firmware?                Firmware           = null,
                             Hardware?                Hardware           = null,
                             LegalCompliance?         LegalCompliance    = null,
                             String?                  ChargingPoolId     = null,
                             String?                  ChargingStationId  = null,
                             String?                  EVSEId             = null,
                             SignatureInfos?          SignatureInfos     = null,
                             String?                  SignatureFormat    = null,
                             IEnumerable<PublicKey>?  PublicKeys         = null)
    {

        #region Properties

        /// <summary>The identification of the energy meter.</summary>
        public String                    Id                   { get; }          = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>     JSONLDContext        { get; }          = Context?.   ToArray() ?? [];

        /// <summary>An optional multi-language description.</summary>
        public I18NString?               Description          { get; }          = Description;

        /// <summary>An optional manufacturer.</summary>
        public Manufacturer?             Manufacturer         { get; }          = Manufacturer;

        /// <summary>An optional device model.</summary>
        public DeviceModel?              Model                { get; }          = Model;

        /// <summary>An optional firmware.</summary>
        public Firmware?                 Firmware             { get; }          = Firmware;

        /// <summary>An optional hardware revision.</summary>
        public Hardware?                 Hardware             { get; }          = Hardware;

        /// <summary>Optional conformity and calibration certificates.</summary>
        public LegalCompliance?          LegalCompliance      { get; }          = LegalCompliance;

        /// <summary>Optional information about how this meter signs.</summary>
        public SignatureInfos?           SignatureInfos       { get; }          = SignatureInfos;

        /// <summary>An optional signature format identifier.</summary>
        public String?                   SignatureFormat      { get; }          = SignatureFormat;

        /// <summary>Optional public keys of the energy meter.</summary>
        public IReadOnlyList<PublicKey>  PublicKeys           { get; }          = PublicKeys?.ToArray() ?? [];

        /// <summary>An optional identification of the charging pool.</summary>
        public String?                   ChargingPoolId       { get; internal set; } = ChargingPoolId;

        /// <summary>An optional identification of the charging station.</summary>
        public String?                   ChargingStationId    { get; internal set; } = ChargingStationId;

        /// <summary>An optional identification of the EVSE.</summary>
        public String?                   EVSEId               { get; internal set; } = EVSEId;

        /// <summary>
        /// The charging pool this energy meter belongs to.
        /// Resolved while a charge transparency record is being assembled, and
        /// never serialized — see <see cref="ToJSON"/>.
        /// </summary>
        public ChargingPool?             ChargingPool         { get; internal set; }

        /// <summary>
        /// The charging station this energy meter belongs to.
        /// Resolved while a charge transparency record is being assembled.
        /// </summary>
        public ChargingStation?          ChargingStation      { get; internal set; }

        /// <summary>
        /// The EVSE this energy meter belongs to.
        /// Resolved while a charge transparency record is being assembled.
        /// </summary>
        public EVSE?                     EVSE                 { get; internal set; }

        #endregion


        #region (static) TryParse(JSON, out EnergyMeter)

        /// <summary>
        /// Try to parse the given JSON as an energy meter.
        /// </summary>
        /// <param name="JSON">A JSON representation of an energy meter.</param>
        /// <param name="EnergyMeter">The parsed energy meter.</param>
        public static Boolean TryParse(JObject JSON, out EnergyMeter? EnergyMeter)
        {

            EnergyMeter = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            Manufacturer?    manufacturer    = null;
            DeviceModel?     model           = null;
            Firmware?        firmware        = null;
            Hardware?        hardware        = null;
            LegalCompliance? legalCompliance = null;
            SignatureInfos?  signatureInfos  = null;

            if (JSON["manufacturer"]    is JObject manufacturerJSON)
                chargy.Manufacturer.   TryParse(manufacturerJSON,    out manufacturer);

            if (JSON["model"]           is JObject modelJSON)
                chargy.DeviceModel.    TryParse(modelJSON,           out model);

            if (JSON["firmware"]        is JObject firmwareJSON)
                chargy.Firmware.       TryParse(firmwareJSON,        out firmware);

            if (JSON["hardware"]        is JObject hardwareJSON)
                chargy.Hardware.       TryParse(hardwareJSON,        out hardware);

            if (JSON["legalCompliance"] is JObject legalComplianceJSON)
                chargy.LegalCompliance.TryParse(legalComplianceJSON, out legalCompliance);

            if (JSON["signatureInfos"]  is JObject signatureInfosJSON)
                chargy.SignatureInfos. TryParse(signatureInfosJSON,  out signatureInfos);

            EnergyMeter = new EnergyMeter(
                              id,
                              PublicKey.ParseContext(JSON["@context"]),
                              JSON["description"] is JObject descriptionJSON
                                  ? I18NString.Parse(descriptionJSON)
                                  : null,
                              manufacturer,
                              model,
                              firmware,
                              hardware,
                              legalCompliance,
                              JSON["chargingPoolId"]?.   Value<String>(),
                              JSON["chargingStationId"]?.Value<String>(),
                              JSON["EVSEId"]?.           Value<String>(),
                              signatureInfos,
                              JSON["signatureFormat"]?.  Value<String>(),
                              PublicKeyList.Parse(JSON["publicKeys"])
                          );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this energy meter.
        ///
        /// Note: The resolved <see cref="ChargingPool"/>, <see cref="ChargingStation"/>
        /// and <see cref="EVSE"/> references are deliberately not serialized, only
        /// their identifications. Those references point back at objects that
        /// contain this meter, so writing them out would not terminate.
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

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",       Description.ToJSON()));

            if (Manufacturer      is not null)
                json.Add(new JProperty("manufacturer",      Manufacturer.ToJSON()));

            if (Model             is not null)
                json.Add(new JProperty("model",             Model.ToJSON()));

            if (Firmware          is not null)
                json.Add(new JProperty("firmware",          Firmware.ToJSON()));

            if (Hardware          is not null)
                json.Add(new JProperty("hardware",          Hardware.ToJSON()));

            if (LegalCompliance   is not null)
                json.Add(new JProperty("legalCompliance",   LegalCompliance.ToJSON()));

            if (ChargingPoolId    is not null)
                json.Add(new JProperty("chargingPoolId",    ChargingPoolId));

            if (ChargingStationId is not null)
                json.Add(new JProperty("chargingStationId", ChargingStationId));

            if (EVSEId            is not null)
                json.Add(new JProperty("EVSEId",            EVSEId));

            if (SignatureInfos    is not null)
                json.Add(new JProperty("signatureInfos",    SignatureInfos.ToJSON()));

            if (SignatureFormat   is not null)
                json.Add(new JProperty("signatureFormat",   SignatureFormat));

            if (PublicKeys.Count > 0)
                json.Add(new JProperty("publicKeys",        new JArray(PublicKeys.Select(publicKey => publicKey.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this energy meter.
        /// </summary>
        public override String ToString()

            => Id;

        #endregion


    }

}
