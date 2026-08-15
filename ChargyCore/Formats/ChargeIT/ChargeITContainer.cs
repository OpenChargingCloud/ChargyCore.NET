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

using cloud.charging.open.chargy.IO;
using cloud.charging.open.chargy.Formats.Alfen;
using cloud.charging.open.chargy.Formats.BSM;

#endregion

namespace cloud.charging.open.chargy.Formats.ChargeIT
{

    /// <summary>
    /// The chargeIT mobility container formats.
    ///
    /// A chargeIT file is a wrapper: it says where a charging station stands and
    /// which EVSE was used, and it carries signed meter values of somebody else's
    /// format inside. Two generations of the wrapper are in the field — an early
    /// one that declares no context at all, and a later one that names itself —
    /// and both carry the same three kinds of payload.
    ///
    /// Because the early format declares nothing, recognising it means checking
    /// whether it has the right shape. That is what the counting below is for: a
    /// file that satisfies most of the checks but not all is reported as a broken
    /// chargeIT record rather than as an unknown format, which is the difference
    /// between telling an EV driver "this file is damaged" and "I have no idea
    /// what this is".
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="Alfen">The Alfen format, for containers carrying Alfen data.</param>
    /// <param name="BSM">The BSM format, for containers carrying BSM snapshots.</param>
    public class ChargeITContainer(I18NDictionary  I18N,
                                   AlfenFormat?    Alfen  = null,
                                   BSMFormat?      BSM    = null) : IJSONChargeTransparencyFormat
    {

        #region Data

        /// <summary>The JSON-LD contexts of the newer chargeIT container format.</summary>
        public static readonly IReadOnlySet<String> ContainerContexts = new HashSet<String>(StringComparer.Ordinal) {
            "https://www.lichtblick.de/contexts/charging-station-json-v0",
            "https://www.lichtblick.de/contexts/charging-station-json-v1",
            "https://www.eneco.com/contexts/charging-station-json-v0",
            "https://www.eneco.com/contexts/charging-station-json-v1",
            "https://www.chargeit-mobility.com/contexts/charging-station-json-v0",
            "https://www.chargeit-mobility.com/contexts/charging-station-json-v1"
        };

        private readonly I18NDictionary  i18n   = I18N;
        private readonly AlfenFormat?    alfen  = Alfen;
        private readonly BSMFormat?      bsm    = BSM;

        #endregion

        #region Properties

        /// <summary>The name of the data format.</summary>
        public String Name
            => "chargeIT mobility";

        #endregion


        #region TryParseJSON(JSON)

        /// <summary>
        /// Try to read a charge transparency record from a chargeIT container.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        public Object TryParseJSON(JObject JSON)
        {

            var context = JSON["@context"]?.Value<String>()?.Trim();

            // A container that names nothing is the older format. That is not a
            // guess with an alternative: the older format simply predates the
            // convention of naming oneself.
            if (context is null || context.Length == 0)
                return ParseOldContainer(JSON);

            if (ContainerContexts.Contains(context))
                return ParseNewContainer(JSON);

            return new SessionCryptoResult(
                       SessionVerificationResult.InvalidSessionFormat,
                       i18n.GetMultilanguageText("No chargeIT charge transparency record")
                   );

        }

        #endregion


        #region (private) ParseOldContainer(JSON)

        /// <summary>
        /// Read the older chargeIT container, which describes the place and
        /// nothing else.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        private Object ParseOldContainer(JObject JSON)
        {

            var checks = new ChargeITFormatChecks(i18n, 14);

            try
            {

                #region Where the charging station stands

                var placeInfo = JSON["placeInfo"] as JObject;

                if (placeInfo is null)
                    checks.Missing("MissingOrInvalidPlaceInfo", 13);

                else
                {

                    if (Text(placeInfo, "evseId") is null)
                        checks.Missing("MissingOrInvalidEVSEId");

                    var addressJSON = placeInfo["address"] as JObject;

                    if (addressJSON is null)
                        checks.Missing("MissingOrInvalidAddress", 3);

                    else
                    {
                        if (Text(addressJSON, "street")  is null)  checks.Missing("MissingOrInvalidAddressStreetName");
                        if (Text(addressJSON, "zipCode") is null)  checks.Missing("MissingOrInvalidAddressZIPCode");
                        if (Text(addressJSON, "town")    is null)  checks.Missing("MissingOrInvalidAddressCityName");
                    }

                    var geoLocationJSON = placeInfo["geoLocation"] as JObject;

                    if (geoLocationJSON is null)
                        checks.Missing("MissingOrInvalidGeoLocation", 2);

                    else
                    {
                        if (Number(geoLocationJSON, "lat") is null)  checks.Missing("MissingOrInvalidGeoLocationLatitude");
                        if (Number(geoLocationJSON, "lon") is null)  checks.Missing("MissingOrInvalidGeoLocationLongitude");
                    }

                }

                #endregion

                #region ..., and the signed meter values it carries

                var signedMeterValues = JSON["signedMeterValues"] as JArray;

                if (signedMeterValues is null || signedMeterValues.Count < 2)
                {
                    checks.Missing("MissingOrInvalidSignedMeterValues", 2 * ChargeITFormatChecks.ChecksPerMeterValue);
                    return checks.Failed();
                }

                checks.AddMeterValues(signedMeterValues.Count);

                var meterValueContext = MeterValueContextOf(signedMeterValues);

                if (!AllAgreeOnTheirContext(signedMeterValues, meterValueContext))
                    checks.Missing("InconsistentJSONContextInformation");

                #endregion

                if (checks.HasErrors)
                    return checks.Failed();

                #region The place, which only the container knows

                var evseId       = Text  (placeInfo, "evseId")!;
                var address      = AddressOf(placeInfo!["address"] as JObject, DefaultCountry: "Deutschland");
                var geoLocation  = GeoLocationOf(placeInfo!["geoLocation"] as JObject);

                #endregion

                return Dispatch(
                           meterValueContext,
                           signedMeterValues,
                           evseId,
                           address,
                           geoLocation,
                           ControllerSoftwareVersion:  null,
                           ChargingTariffs:            null,
                           Checks:                     checks
                       );

            }
            catch (Exception exception)
            {
                return checks.Failed(exception);
            }

        }

        #endregion

        #region (private) ParseNewContainer(JSON)

        /// <summary>
        /// Read the newer chargeIT container, which also describes the charging
        /// station, the meter and what the charging session cost.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        private Object ParseNewContainer(JObject JSON)
        {

            var checks = new ChargeITFormatChecks(i18n, 81);

            try
            {

                if (Text(JSON, "@id") is null)
                    checks.Missing("Missing or invalid charge transparency record identification!");

                #region Where the charging station stands

                var chargePointInfo = JSON["chargePointInfo"] as JObject;

                if (chargePointInfo is null)
                    checks.Missing("MissingOrInvalidChargePointInfo", 19);

                else
                {

                    if (Text(chargePointInfo, "evseId") is null)
                        checks.Missing("MissingOrInvalidEVSEIdentification");

                    var placeInfo = chargePointInfo["placeInfo"] as JObject;

                    if (placeInfo is null)
                        checks.Missing("MissingOrInvalidPlaceInfo", 8);

                    else
                    {

                        var geoLocationJSON = placeInfo["geoLocation"] as JObject;

                        if (geoLocationJSON is null)
                            checks.Missing("MissingOrInvalidGeoLocation", 2);

                        else
                        {
                            if (Number(geoLocationJSON, "lat") is null)  checks.Missing("MissingOrInvalidGeoLocationLatitude");
                            if (Number(geoLocationJSON, "lon") is null)  checks.Missing("MissingOrInvalidGeoLocationLongitude");
                        }

                        var addressJSON = placeInfo["address"] as JObject;

                        if (addressJSON is null)
                            checks.Missing("MissingOrInvalidAddress", 4);

                        else
                        {
                            if (Text(addressJSON, "street")  is null)  checks.Missing("MissingOrInvalidAddressStreetName");
                            if (Text(addressJSON, "zipCode") is null)  checks.Missing("MissingOrInvalidAddressZIPCode");
                            if (Text(addressJSON, "town")    is null)  checks.Missing("MissingOrInvalidAddressCityName");
                            if (Text(addressJSON, "country") is null)  checks.Missing("MissingOrInvalidAddressCountryName");
                        }

                    }

                }

                #endregion

                #region ..., what the charging station is

                var chargingStationInfo = JSON["chargingStationInfo"] as JObject;

                if (chargingStationInfo is null)
                    checks.Missing("MissingOrInvalidChargingStationInfo", 5);

                #endregion

                #region ..., and the signed meter values it carries

                var signedMeterValues = JSON["signedMeterValues"] as JArray;

                if (signedMeterValues is null || signedMeterValues.Count < 2)
                {
                    checks.Missing("MissingOrInvalidSignedMeterValues", 2 * ChargeITFormatChecks.ChecksPerMeterValue);
                    return checks.Failed();
                }

                var meterValueContext = MeterValueContextOf(signedMeterValues);

                if (!AllAgreeOnTheirContext(signedMeterValues, meterValueContext))
                    return new SessionCryptoResult(
                               SessionVerificationResult.InvalidSessionFormat,
                               i18n.GetMultilanguageText("Inconsistent signed meter value format!"),
                               Certainty: 1
                           );

                #endregion

                var evseId          = Text(chargePointInfo, "evseId") ?? "";
                var placeInfoJSON   = chargePointInfo?["placeInfo"] as JObject;

                return Dispatch(
                           meterValueContext,
                           signedMeterValues,
                           evseId,
                           AddressOf    (placeInfoJSON?["address"]     as JObject, DefaultCountry: ""),
                           GeoLocationOf(placeInfoJSON?["geoLocation"] as JObject),
                           ControllerSoftwareVersion:  Text(chargingStationInfo, "controllerSoftwareVersion"),
                           ChargingTariffs:            JSON["chargingTariffs"] as JArray,
                           Checks:                     checks
                       );

            }
            catch (Exception exception)
            {
                return checks.Failed(exception);
            }

        }

        #endregion

        #region (private) Dispatch(...)

        /// <summary>
        /// Hand the signed meter values to the format that produced them.
        /// </summary>
        /// <param name="MeterValueContext">What the signed meter values say they are.</param>
        /// <param name="SignedMeterValues">The signed meter values.</param>
        /// <param name="EVSEId">The identification of the EVSE the readings came from.</param>
        /// <param name="Address">Where the charging station stands.</param>
        /// <param name="GeoLocation">Where the charging station stands, exactly.</param>
        /// <param name="ControllerSoftwareVersion">The charging station software version the container claims, if any.</param>
        /// <param name="ChargingTariffs">The charging tariffs the container declared, if any.</param>
        /// <param name="Checks">How many of the format checks have passed so far.</param>
        private Object Dispatch(String?                MeterValueContext,
                                JArray                 SignedMeterValues,
                                String                 EVSEId,
                                Address?               Address,
                                GeoCoordinate?         GeoLocation,
                                String?                ControllerSoftwareVersion,
                                JArray?                ChargingTariffs,
                                ChargeITFormatChecks   Checks)
        {

            #region BSM, which signs snapshots of the whole meter

            if (MeterValueContext is not null &&
                BSMFormat.MeterValueContexts.Contains(MeterValueContext))
            {

                if (bsm is null)
                    return Unsupported();

                var parsed = bsm.TryParse(EVSEId, ControllerSoftwareVersion, SignedMeterValues);

                return parsed is BSMChargingSession session
                           ? BuildRecord(session, EVSEId, Address, GeoLocation, ControllerSoftwareVersion, ChargingTariffs)
                           : parsed;

            }

            #endregion

            #region Alfen, whose readings the container carries as plain text payloads

            if (MeterValueContext?.StartsWith("ALFEN", StringComparison.Ordinal) == true ||
                Text(SignedMeterValues.FirstOrDefault() as JObject, "format") == "ALFEN")
            {

                if (alfen is null)
                    return Unsupported();

                var containerInfos = new ContainerInfos();

                containerInfos.AddChargingStation(
                    ChargeITOperator.BuildChargingStation(EVSEId, Address, GeoLocation)
                );

                return alfen.TryParse(
                           SignedMeterValues.Select(value => Text(value as JObject, "payload") ?? ""),
                           containerInfos
                       );

            }

            #endregion

            #region ..., and the meter values chargeIT itself defined, which an EMH meter signs

            if (MeterValueContext is null)
                return ParseChargeITMeterValues(
                           SignedMeterValues,
                           EVSEId,
                           Address,
                           GeoLocation,
                           Checks
                       );

            #endregion

            return Checks.Failed();

        }

        #endregion


        #region (private) ParseChargeITMeterValues(SignedMeterValues, EVSEId, Address, GeoLocation, Checks)

        /// <summary>
        /// Read the meter values chargeIT defined before OCMF existed.
        ///
        /// Every reading carries its own copy of everything — the meter, its public
        /// key, the contract, the charging station software — so a charging session
        /// is only a charging session if all of those copies agree.
        /// </summary>
        /// <param name="SignedMeterValues">The signed meter values.</param>
        /// <param name="EVSEId">The identification of the EVSE the readings came from.</param>
        /// <param name="Address">Where the charging station stands.</param>
        /// <param name="GeoLocation">Where the charging station stands, exactly.</param>
        /// <param name="Checks">How many of the format checks have passed so far.</param>
        private Object ParseChargeITMeterValues(JArray                SignedMeterValues,
                                                String                EVSEId,
                                                Address?              Address,
                                                GeoCoordinate?        GeoLocation,
                                                ChargeITFormatChecks  Checks)
        {

            var meterValues = new List<ChargeITMeterValue>();

            foreach (var element in SignedMeterValues)
                if (element is JObject meterValueJSON &&
                    ChargeITMeterValue.TryParse(meterValueJSON, out var meterValue))
                {
                    meterValues.Add(meterValue!);
                }

            if (meterValues.Count == 0)
                return Checks.Failed();

            var first  = meterValues[0];
            var last   = meterValues[^1];

            #region The energy meter and its public key

            var energyMeter = new EnergyMeter(
                                  first.MeterId,
                                  Manufacturer:     new Manufacturer(
                                                        first.Manufacturer,
                                                        Contact: new Contact(Web: "https://www.emh-metering.de")
                                                    ),
                                  Model:            new DeviceModel(first.MeterType),
                                  Hardware:         new Hardware(Revision: "1.0"),
                                  Firmware:         new Firmware(first.FirmwareVersion),
                                  SignatureFormat:  "https://open.charging.cloud/contexts/EnergyMeterSignatureFormats/EMHCrypt01",
                                  PublicKeys:       [
                                                        new PublicKey(
                                                            // The meter files its key without the SEC1 marker
                                                            // that says the point is uncompressed, and adding
                                                            // it is what makes the key readable at all.
                                                            first.PublicKey.StartsWith("04", StringComparison.OrdinalIgnoreCase)
                                                                ?        first.PublicKey
                                                                : "04" + first.PublicKey,
                                                            new OIDInfo("secp192r1"),
                                                            Format:    "DER",
                                                            Encoding:  "hex"
                                                        )
                                                    ]
                              );

            #endregion

            #region The measurement, whose readings an EMH meter signed one by one

            var measurement = new Measurement(
                                  first.MeterId,
                                  first.MeasurandName,
                                  ChargyLib.ParseOBIS(first.MeasurandId),
                                  first.Scale,
                                  Values:          meterValues.Select(meterValue => meterValue.ToMeasurementValue()),
                                  Context:         [ "https://open.charging.cloud/contexts/EnergyMeterSignatureFormats/EMHCrypt01+json" ],
                                  Unit:            first.Unit,
                                  UnitEncoded:     first.UnitEncoded,
                                  ValueType:       first.ValueType,
                                  SignatureInfos:  new SignatureInfos(
                                                       Hash:            CryptoHashAlgorithm.SHA256,
                                                       Algorithm:       CryptoAlgorithm.ECC,
                                                       Curve:           ECCurve.secp192r1,
                                                       Format:          SignatureFormat.RS,
                                                       HashTruncation:  24
                                                   )
                              );

            #endregion

            var chargingSession = new ChargingSession(
                                      last.TransactionId,
                                      Context:        [ EMH.EMHCrypt01.SessionContext ],
                                      Begin:          ChargyLib.UnixTimestampToISO8601(first.MeasuredAt),
                                      End:            ChargyLib.UnixTimestampToISO8601(last. MeasuredAt),
                                      EVSEId:         EVSEId,
                                      EnergyMeterId:  first.MeterId,
                                      Measurements:   [ measurement ]
                                  ) {
                                      AuthorizationStart = new Authorization(
                                                               first.ContractId,
                                                               Type:       first.ContractType,
                                                               Timestamp:  first.ContractTimestamp
                                                           )
                                  };

            var record = new ChargeTransparencyRecord(
                             last.TransactionId,
                             [ "https://open.charging.cloud/contexts/CTR+json" ],
                             chargingSession.Begin,
                             chargingSession.End,
                             I18NString.Create(Languages.de, "Alle Ladevorgänge"),
                             Certainty:  Checks.Certainty,
                             Status:     SessionVerificationResult.Unvalidated
                         );

            record.AddChargingStationOperator(
                ChargeITOperator.Build(
                    ChargeITOperator.BuildChargingStation(
                        EVSEId,
                        Address,
                        GeoLocation,
                        Firmware:      first.ChargePointSoftwareVersion is not null
                                           ? new Firmware(first.ChargePointSoftwareVersion)
                                           : null,
                        EnergyMeters:  [ energyMeter ]
                    )
                )
            );

            record.AddChargingSession(chargingSession);
            record.AddContract(new Contract(first.ContractId, [ first.ContractType ]));

            return record;

        }

        #endregion

        #region (private) BuildRecord(Session, EVSEId, Address, GeoLocation, ControllerSoftwareVersion, ChargingTariffs)

        /// <summary>
        /// Put a charging session read from signed meter values into a charge
        /// transparency record, together with everything only the container knew.
        /// </summary>
        private static ChargeTransparencyRecord BuildRecord(BSMChargingSession  Session,
                                                            String              EVSEId,
                                                            Address?            Address,
                                                            GeoCoordinate?      GeoLocation,
                                                            String?             ControllerSoftwareVersion,
                                                            JArray?             ChargingTariffs)
        {

            var record = new ChargeTransparencyRecord(
                             Session.ChargingSession.Id,
                             [ "https://open.charging.cloud/contexts/CTR+json" ],
                             Session.ChargingSession.Begin,
                             Session.ChargingSession.End,
                             I18NString.Create(Languages.de, "Alle Ladevorgänge").
                                              Set(Languages.en, "All charging sessions"),
                             Certainty:  Session.Certainty,
                             Status:     Session.Errors.Count == 0
                                             ? SessionVerificationResult.Unvalidated
                                             : SessionVerificationResult.InvalidSessionFormat
                         );

            record.AddChargingStationOperator(
                ChargeITOperator.Build(
                    ChargeITOperator.BuildChargingStation(
                        EVSEId,
                        Address,
                        GeoLocation,
                        Firmware:      ControllerSoftwareVersion is not null
                                           ? new Firmware(ControllerSoftwareVersion)
                                           : null,
                        EnergyMeters:  [ Session.EnergyMeter ]
                    ),
                    ParseChargingTariffs(ChargingTariffs)
                )
            );

            record.AddChargingSession(Session.ChargingSession);
            record.AddContract(
                new Contract(
                    Session.ContractId,
                    Session.ContractType is not null
                        ? [ Session.ContractType ]
                        : null
                )
            );

            foreach (var error   in Session.Errors)    record.AddError  (error);
            foreach (var warning in Session.Warnings)  record.AddWarning(warning);

            return record;

        }

        #endregion


        #region (private) Unsupported()

        /// <summary>
        /// Report that the container carries a format Chargy was not built with.
        /// </summary>
        private SessionCryptoResult Unsupported()

            => new (
                   SessionVerificationResult.UnknownCTRFormat,
                   i18n.GetMultilanguageText("UnknownOrInvalidChargingSessionFormat")
               );

        #endregion

        #region (private, static) Helpers

        /// <summary>
        /// What the signed meter values say they are, or null when they say
        /// nothing — which is itself the answer, because the meter value format
        /// chargeIT defined first predates the convention of naming oneself.
        /// </summary>
        private static String? MeterValueContextOf(JArray SignedMeterValues)

            => Text(SignedMeterValues.FirstOrDefault() as JObject, "@context");

        /// <summary>
        /// Whether every signed meter value declares the same context.
        ///
        /// A file whose readings disagree about their own format is not a charging
        /// session made of two kinds of evidence — it is a file somebody assembled.
        /// </summary>
        private static Boolean AllAgreeOnTheirContext(JArray   SignedMeterValues,
                                                      String?  Context)

            => SignedMeterValues.All(value => Text(value as JObject, "@context") == Context);

        /// <summary>The address a container describes.</summary>
        private static Address? AddressOf(JObject?  JSON,
                                          String    DefaultCountry)

            => JSON is not null
                   ? new Address(
                         Street:      Text(JSON, "street"),
                         PostalCode:  Text(JSON, "zipCode"),
                         City:        Text(JSON, "town"),
                         Country:     Text(JSON, "country") ?? DefaultCountry
                     )
                   : null;

        /// <summary>The geographical location a container describes.</summary>
        private static GeoCoordinate? GeoLocationOf(JObject? JSON)
        {

            if (JSON is null)
                return null;

            var latitude   = Number(JSON, "lat");
            var longitude  = Number(JSON, "lon");

            return latitude.HasValue && longitude.HasValue
                       ? GeoCoordinate.Create(
                             Latitude. Parse((Double) latitude.Value),
                             Longitude.Parse((Double) longitude.Value)
                         )
                       : null;

        }

        /// <summary>The charging tariffs a container declares, which are not validated yet.</summary>
        private static IEnumerable<ChargingTariff>? ParseChargingTariffs(JArray? JSON)
        {

            if (JSON is null)
                return null;

            var tariffs = new List<ChargingTariff>();

            foreach (var element in JSON.OfType<JObject>())
                if (ChargingTariff.TryParse(element, out var tariff))
                    tariffs.Add(tariff!);

            return tariffs;

        }

        /// <summary>A string property, or null when it is absent or not a string.</summary>
        private static String? Text(JObject?  JSON,
                                    String    Key)

            => JSON?[Key]?.Type == JTokenType.String
                   ? JSON[Key]!.Value<String>()
                   : null;

        /// <summary>A numeric property, or null when it is absent or not a number.</summary>
        private static Decimal? Number(JObject?  JSON,
                                       String    Key)

            => JSON?[Key]?.Type == JTokenType.Integer ||
               JSON?[Key]?.Type == JTokenType.Float
                   ? JSON[Key]!.Value<Decimal>()
                   : null;

        #endregion


    }

}
