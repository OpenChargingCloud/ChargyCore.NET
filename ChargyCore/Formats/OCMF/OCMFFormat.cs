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

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.IO;

#endregion

namespace cloud.charging.open.chargy.Formats.OCMF
{

    /// <summary>
    /// The Open Charge Metering Format.
    ///
    /// A meter signs a JSON payload describing one or more readings, and the
    /// charging station hands that over as "OCMF|{payload}|{signature}". Unlike
    /// the binary formats, everything an EV driver is shown comes out of the
    /// payload itself — which is also why so little of it is optional to get right.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    public partial class OCMFFormat(I18NDictionary I18N) : ITextChargeTransparencyFormat
    {

        #region Data

        /// <summary>The JSON-LD context of an OCMF charging session.</summary>
        public const String SessionContext      = "https://open.charging.cloud/contexts/SessionSignatureFormats/OCMFv1.0+json";

        /// <summary>The JSON-LD context of an OCMF measurement.</summary>
        public const String MeasurementContext  = "https://open.charging.cloud/contexts/EnergyMeterSignatureFormats/OCMFv1.0+json";

        private readonly I18NDictionary           i18n       = I18N;
        private readonly OCMFDocumentScanner      scanner    = new ();
        private readonly OCMFSignatureValidator   validator  = new (I18N);

        /// <summary>The units an OCMF reading may be measured in.</summary>
        private static readonly String[] knownUnits         = [ "kWh", "Wh", "mOhm", "uOhm" ];

        /// <summary>The transaction types an OCMF reading may carry.</summary>
        private static readonly String[] knownTransactions  = [ "B", "C", "X", "E", "L", "R", "A", "P", "S", "T" ];

        /// <summary>How the meter's clock was synchronised.</summary>
        private static readonly String[] knownTimeSyncs     = [ "U", "I", "S", "R" ];

        #endregion

        #region Properties

        /// <summary>The name of the data format.</summary>
        public String Name
            => "OCMF";

        #endregion


        #region CanParse    (Text)

        /// <summary>
        /// Whether the given text holds an OCMF document.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        public Boolean CanParse(String Text)

            => Text.Contains("OCMF|", StringComparison.Ordinal);

        #endregion

        #region TryParseText(Text, PublicKeyHEX = null)

        /// <summary>
        /// Try to read a charge transparency record from OCMF text.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        /// <param name="PublicKeyHEX">The public key that arrived alongside the data.</param>
        public Object TryParseText(String   Text,
                                   String?  PublicKeyHEX = null)

            => TryParse(
                   [ Text ],
                   PublicKeyHEX,
                   PublicKeyHEX is not null ? "hex" : null,
                   null
               );

        #endregion

        #region TryParse    (OCMFDocuments, PublicKey, PublicKeyEncoding, ContainerInfos)

        /// <summary>
        /// Try to read a charge transparency record from one or more OCMF documents.
        /// </summary>
        /// <param name="OCMFDocuments">Texts holding OCMF documents.</param>
        /// <param name="PublicKey">The public key of the energy meter, when one arrived.</param>
        /// <param name="PublicKeyEncoding">How the public key is encoded, when known.</param>
        /// <param name="ContainerInfos">What the surrounding container knew, if anything.</param>
        public Object TryParse(IEnumerable<String>  OCMFDocuments,
                               String?              PublicKey,
                               String?              PublicKeyEncoding,
                               ContainerInfos?      ContainerInfos)
        {

            var scanned = scanner.Scan(OCMFDocuments);

            if (!scanned.Success)
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSessionFormat,
                           i18n.GetMultilanguageText(scanned.ErrorMessage ?? "UnknownOrInvalidChargingSessionFormat")
                       );

            // The signature is checked before anything is made of the payload, so
            // that what an EV driver is shown and whether it is trustworthy are
            // decided from the same reading of the document.
            foreach (var document in scanned.Documents)
                validator.Validate(document, PublicKey, PublicKeyEncoding);

            // Documents that disagree about who was charging, on which meter, are
            // not one charging session however they arrived. The first group is
            // what gets returned: a file holding two unrelated sessions is a case
            // the format detector resolves, not this parser.
            var groups = GroupByIdentity(scanned.Documents);

            return BuildChargeTransparencyRecord(groups[0], ContainerInfos);

        }

        #endregion


        #region (private, static) GroupByIdentity(Documents)

        /// <summary>
        /// Group the documents by what they say about the charging session they
        /// belong to.
        ///
        /// Everything that identifies the session takes part: the gateway, the
        /// meter, and above all the identification of who authorised the charging.
        /// Two documents from the same meter that name different drivers are two
        /// charging sessions, and merging their readings would produce a record
        /// that bills one driver for the other's energy.
        ///
        /// The readings themselves are deliberately not part of the key — they
        /// are what differs between the documents of one session.
        /// </summary>
        /// <param name="Documents">The OCMF documents.</param>
        private static List<List<OCMFDocument>> GroupByIdentity(IReadOnlyList<OCMFDocument> Documents)
        {

            var order   = new List<String>();
            var groups  = new Dictionary<String, List<OCMFDocument>>(StringComparer.Ordinal);

            foreach (var document in Documents)
            {

                var payload = document.Payload;

                var key = String.Join("|", [
                              Text(payload, "FV") ?? "",
                              Text(payload, "GI") ?? "",
                              Text(payload, "GS") ?? "",
                              Text(payload, "GV") ?? "",
                              Text(payload, "MV") ?? "",
                              Text(payload, "MM") ?? "",
                              Text(payload, "MS") ?? "",
                              Text(payload, "MF") ?? "",
                              payload["IS"]?.Type == JTokenType.Boolean && payload["IS"]!.Value<Boolean>() ? "1" : "0",
                              Text(payload, "IL") ?? "",
                              Text(payload, "IT") ?? "",
                              Text(payload, "ID") ?? "",
                              Text(payload, "TT") ?? "",
                              Text(payload, "CF") ?? "",
                              Text(payload, "CT") ?? "",
                              Text(payload, "CI") ?? ""
                          ]);

                if (!groups.TryGetValue(key, out var group))
                {
                    group = [];
                    groups.Add(key, group);
                    order.Add(key);
                }

                group.Add(document);

            }

            return [.. order.Select(key => groups[key]) ];

        }

        #endregion

        #region (private) BuildChargeTransparencyRecord(Documents, ContainerInfos)

        /// <summary>
        /// Turn the OCMF documents into a charge transparency record.
        /// </summary>
        /// <param name="Documents">The OCMF documents.</param>
        /// <param name="ContainerInfos">What the surrounding container knew, if anything.</param>
        private Object BuildChargeTransparencyRecord(List<OCMFDocument>           Documents,
                                                     ContainerInfos?              ContainerInfos)
        {

            var first    = Documents[0];
            var payload  = first.Payload;

            #region What the meter and the gateway say about themselves

            // OCMF 0.1 called the signing device "VI"/"VV"; from 1.0 on the same
            // two facts are "GI"/"GV". Either way they describe the gateway that
            // relayed the readings, not the meter that took them.
            var formatVersion       = Text(payload, "FV");
            var gatewayInformation  = Text(payload, "GI") ?? Text(payload, "VI");
            var gatewaySerial       = Text(payload, "GS");
            var gatewayVersion      = Text(payload, "GV") ?? Text(payload, "VV");

            var meterVendor         = Text(payload, "MV");
            var meterModel          = Text(payload, "MM");
            var meterSerialNumber   = Text(payload, "MS");
            var meterFirmware       = Text(payload, "MF");

            // "MS" is the meter's own serial; "GS" the gateway's. A record that
            // names neither cannot say which device produced the readings.
            var meterSerial  = meterSerialNumber ?? gatewaySerial;
            var paging       = Text(payload, "PG");

            if (meterSerial is null || paging is null)
                return Invalid("UnknownOrInvalidChargingSessionFormat");

            #endregion

            #region What the pagination counter counts, and how far it has counted

            // The prefix decides what the document is: "T" counts the charging
            // sessions the meter has signed, "F" the fiscal readings it takes on
            // its own. They are separate sequences, so a gap in one is not a gap
            // in the other — and a counter that says neither leaves a reading in
            // no sequence at all, where no gap could ever be noticed.
            var paged = ParsePaging(paging);

            if (paged is not (OCMFTransactionType, UInt64) firstPaging)
                return Invalid(
                           paging.Length > 0 && Char.ToLowerInvariant(paging[0]) is 't' or 'f'
                               ? "The given OCMF data does not have a valid pagination counter!"
                               : "The given OCMF data does not have a valid transaction type!"
                       );

            #endregion

            #region The tariff, which OCMF signs as free text

            var tariffText      = Text(payload, "TT");

            var bonnTariff      = tariffText is not null &&
                                  OCMFBonnTariff.TryParse(tariffText, out var parsedBonnTariff)
                                      ? parsedBonnTariff
                                      : null;

            // A tariff text that is not one of the Bonn profiles still names the
            // tariff that was applied — it just names one nothing can price. It
            // is kept as a bare identification, so that a receipt can say what it
            // was billed under instead of saying nothing at all.
            var chargingTariff  = tariffText is not null && tariffText.Length > 0
                                      ? bonnTariff?.ToChargingTariff() ?? new ChargingTariff(tariffText)
                                      : null;

            #endregion

            #region What the meter compensates for the charging cable

            var controllerFirmwareVersion  = Text(payload, "CF");

            var lossCompensation           = OCMFLossCompensation.TryParse(payload["LC"] as JObject, out var parsedLossCompensation)
                                                 ? parsedLossCompensation
                                                 : null;

            #endregion

            #region Where the charging station says the readings came from

            // "CI" carries an identification whose meaning "CT" decides. Only
            // these two spellings are signed; everything else about the
            // installation comes from the container and is not.
            String? signedEVSEId            = null;
            String? signedChargingStationId = null;
            String? signedConnectorId       = null;

            var chargePointId      = Text(payload, "CI")?.Trim();
            var chargePointIdType  = Text(payload, "CT")?.ToUpperInvariant();

            if (chargePointId is not null && chargePointId.Length > 0)
                switch (chargePointIdType)
                {

                    case "EVSEID":
                        signedEVSEId = chargePointId;
                        break;

                    case "CBIDC":
                        {
                            var match = ChargePointAndConnectorRegex().Match(chargePointId);

                            if (match.Success)
                            {
                                signedChargingStationId = match.Groups[1].Value;
                                signedConnectorId       = match.Groups[2].Value;
                            }
                        }
                        break;

                }

            #endregion

            #region The readings

            // The readings are collected first and the measurements built from
            // them afterwards, because a Measurement copies its values when it is
            // constructed: appending to the list later would never reach it.
            var order  = new List<String>();
            var byKey  = new Dictionary<String, (OCMFReading First, List<MeasurementValue> Values)>(StringComparer.Ordinal);

            foreach (var document in Documents)
            {

                // Each document carries its own counter, and each reading is
                // stamped with the one it actually arrived under. ChargyCore.TS
                // stamps every reading with the first document's counter, which
                // is invisible for a single-document record and wrong for the
                // KEBA records, where a hundred documents with a hundred
                // different counters make up one charging session.
                var (transactionType, pagination) = ParsePaging(Text(document.Payload, "PG")) ?? firstPaging;

                // OCMF lets a reading omit a field that has not changed since the
                // previous one — but only within the same signed record. Dropping
                // such a reading would lose a signed meter value.
                var inherited = new JObject();

                foreach (var reading in document.Payload["RD"] as JArray ?? [])
                {

                    if (reading is not JObject readingJSON)
                        continue;

                    var effective = (JObject) inherited.DeepClone();
                    effective.Merge(readingJSON);
                    inherited = effective;

                    var parsed = ParseReading(effective);

                    if (parsed.Error is not null)
                        return Invalid(parsed.Error);

                    if (parsed.Value is not OCMFReading ocmfReading)
                        continue;

                    var key = $"{ocmfReading.OBIS}|{ocmfReading.Unit}|{ocmfReading.CurrentType ?? ""}";

                    if (!byKey.TryGetValue(key, out var group))
                    {
                        group = (ocmfReading, []);
                        byKey.Add(key, group);
                        order.Add(key);
                    }

                    group.Values.Add(
                        new OCMFMeasurementValue(
                            ocmfReading.Timestamp,
                            ocmfReading.Value,
                            document,
                            ocmfReading.TimeSync,
                            ocmfReading.Transaction,
                            transactionType,
                            pagination,
                            ocmfReading.ErrorIndex,
                            ocmfReading.ErrorFlags,
                            // A compensation of nothing is what every reading
                            // before the first loss carries. Showing "0 kWh
                            // compensated" would suggest that a compensation
                            // took place.
                            ocmfReading.CumulatedLoss != 0 ? ocmfReading.CumulatedLoss : null,
                            ocmfReading.Status
                        ) {
                            // The reading is not signed on its own: the signature
                            // covers the whole OCMF document it arrived in, so its
                            // verdict is what every reading inside inherits — and
                            // so is the reason behind the verdict, which is the
                            // difference between telling an EV driver "invalid"
                            // and telling them why.
                            Result = ResultOf(document)
                        }
                    );

                }

            }

            var measurements = order.Select(key => {

                                   var (reading, values) = byKey[key];

                                   return new Measurement(
                                              meterSerial,
                                              ChargyLib.OBIS2MeasurementName(reading.OBIS),
                                              reading.OBIS,
                                              0,
                                              values,
                                              [ MeasurementContext ],
                                              Unit:         reading.Unit,
                                              CurrentType:  reading.CurrentType
                                          );

                               }).ToList();

            if (measurements.Count == 0)
                return Invalid("UnknownOrInvalidChargingSessionFormat");

            #endregion

            #region The time span, ordered by instant rather than lexically

            // The timestamps keep the offset the meter reported, so sorting them
            // as text would order them by their local reading, which is not the
            // same order.
            var timestamps = measurements.SelectMany(measurement => measurement.Values).
                                          Select    (value       => value.Timestamp).
                                          Where     (timestamp   => DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)).
                                          OrderBy   (timestamp   => DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture)).
                                          ToArray();

            var begin = timestamps.Length > 0 ? timestamps[0]  : null;
            var end   = timestamps.Length > 0 ? timestamps[^1] : null;

            #endregion

            var sessionId = SessionIdOf(Documents);

            var chargingSession = new ChargingSession(
                                      sessionId,
                                      [ SessionContext ],
                                      begin,
                                      end,
                                      ChargingStationId:  signedChargingStationId ?? ContainerInfos?.FirstChargingStationId,
                                      EVSEId:             signedEVSEId            ?? ContainerInfos?.FirstEVSEId,
                                      ConnectorId:        signedConnectorId,
                                      EnergyMeterId:      meterSerial,
                                      Measurements:       measurements
                                  ) {
                                      AuthorizationStart = new Authorization(
                                                               Text(payload, "ID") ?? "?",
                                                               Type:                  Text(payload, "IT") ?? "?",
                                                               IdentificationStatus:  payload["IS"]?.Type == JTokenType.Boolean
                                                                                          ? payload["IS"]!.Value<Boolean>()
                                                                                          : null,
                                                               IdentificationLevel:   Text(payload, "IL"),
                                                               IdentificationFlags:   TextArray(payload, "IF"),
                                                               // OCMF 0.1 wrote a word here where 1.x writes a
                                                               // boolean, and the word is what that meter said.
                                                               IdentificationStatusText: Text(payload, "IS")
                                                           )
                                  };

            if (chargingTariff is not null)
            {
                chargingSession.TariffId = chargingTariff.Id;
                chargingSession.AddChargingTariff(chargingTariff);
            }

            var record = new OCMFChargeTransparencyRecord(
                             sessionId,
                             new OCMFInfo {
                                 FormatVersion                  = formatVersion,
                                 GatewayInformation             = gatewayInformation,
                                 GatewaySerial                  = gatewaySerial,
                                 GatewayVersion                 = gatewayVersion,
                                 MeterVendor                    = meterVendor,
                                 MeterModel                     = meterModel,
                                 MeterSerial                    = meterSerialNumber,
                                 MeterFirmware                  = meterFirmware,
                                 TariffText                     = tariffText,
                                 TariffTextInterpretation       = bonnTariff,
                                 ControllerFirmwareVersion      = controllerFirmwareVersion,
                                 LossCompensation               = lossCompensation,
                                 ChargePointIdentificationType  = Text(payload, "CT"),
                                 ChargePointIdentification      = Text(payload, "CI")
                             },
                             [ "https://open.charging.cloud/contexts/CTR+json" ],
                             begin,
                             end,
                             Certainty:  1,
                             // The record is as good as the weakest document it
                             // was built from: one signature that does not hold
                             // is enough to make the whole account of the
                             // charging session unproven.
                             Status:     Documents.All(document => document.ValidationStatus == VerificationResult.ValidSignature)
                                             ? SessionVerificationResult.ValidSignature
                                             : SessionVerificationResult.InvalidSignature
                         );

            record.AddChargingSession(chargingSession);

            if (chargingTariff is not null)
                record.AddChargingTariff(chargingTariff);

            #region The energy meter, so that the record says which device signed

            // A container may describe the meter too, and the two descriptions are
            // not equal: what the meter signed about itself wins, and the container
            // may only fill the gaps. OCMF has no field for a manufacturer's web
            // address or a hardware revision, so those can only come from the
            // container — but its idea of the model must never override the signed
            // one.
            var containerMeter = ContainerInfos?.EnergyMeter;

            var energyMeter = new EnergyMeter(
                                  meterSerial,
                                  Manufacturer:     meterVendor is not null
                                                        ? new Manufacturer(
                                                              meterVendor,
                                                              containerMeter?.Manufacturer?.URL,
                                                              Contact: containerMeter?.Manufacturer?.Contact
                                                          )
                                                        : containerMeter?.Manufacturer,
                                  Model:            meterModel is not null
                                                        ? new DeviceModel(meterModel, URL: containerMeter?.Model?.URL)
                                                        : containerMeter?.Model,
                                  Firmware:         meterFirmware is not null
                                                        ? new Firmware(meterFirmware)
                                                        : containerMeter?.Firmware,
                                  Hardware:         containerMeter?.Hardware,
                                  SignatureFormat:  MeasurementContext
                              );

            // Where the station stands, what it is called and what software it
            // runs are things an OCMF document never says. They come from the
            // container and have to be carried through, because they are the whole
            // reason a container exists — and an EV driver who is shown only a
            // meter serial number has been told less than the file contained.
            var containerStation    = ContainerInfos?.ChargingStations.FirstOrDefault();
            var containerEVSE       = ContainerInfos?.FirstEVSE;
            var containerConnector  = ContainerInfos?.FirstConnector;

            // The meter hangs off the EVSE when the record names one, and off the
            // charging station otherwise — so that a verification can find it
            // either way rather than depending on how complete the record is.
            var evseId              = signedEVSEId ?? containerEVSE?.Id;

            // "CF" is the firmware of the charging controller, and it is signed,
            // so it wins over anything the container claims. It goes onto a
            // station the record actually names and no further: where nothing
            // named one, Chargy invents a station to hold the meter, and giving
            // that invention a signed version would attach a real fact to a
            // device the file never mentioned.
            var namedStationId      = signedChargingStationId ?? containerStation?.Id;

            var chargingStation     = new ChargingStation(
                                          namedStationId ?? meterSerial,
                                          Description:   containerStation?.Description,
                                          Firmware:      namedStationId is not null && controllerFirmwareVersion is not null
                                                             ? new Firmware(
                                                                   controllerFirmwareVersion,
                                                                   containerStation?.Firmware?.ReleaseDate,
                                                                   containerStation?.Firmware?.URL,
                                                                   containerStation?.Firmware?.Checksum,
                                                                   containerStation?.Firmware?.Description
                                                               )
                                                             : containerStation?.Firmware,
                                          Address:       containerStation?.Address,
                                          GeoLocation:   containerStation?.GeoLocation,
                                          EVSEs:         evseId is not null
                                                             ? [
                                                                   new EVSE(
                                                                       evseId,
                                                                       Description:   containerEVSE?.Description,
                                                                       EnergyMeters:  [ energyMeter ],
                                                                       Connectors:    containerEVSE?.Connectors
                                                                   )
                                                               ]
                                                             : null,
                                          EnergyMeters:  evseId is null
                                                             ? [ energyMeter ]
                                                             : null
                                      );

            record.AddChargingStation(chargingStation);

            #endregion

            #region The connector, and the cable whose losses the meter accounted for

            // A cable is described from two sides: the container knows how long
            // it is, the meter knows what it compensated for. Only the second is
            // signed, so only the second may overwrite — and where the meter said
            // nothing, e.g. gave the compensation no name, the container's answer
            // stands.
            var connectorId  = signedConnectorId ?? containerConnector?.Id;

            if (connectorId is not null || lossCompensation is not null)
            {

                var matchingConnector = containerConnector is not null &&
                                        containerConnector.Id == connectorId
                                            ? containerConnector
                                            : null;

                chargingSession.ConnectorId ??= connectorId;
                chargingSession.Connector     = new Connector(
                                                    connectorId,
                                                    matchingConnector?.Type,
                                                    lossCompensation is not null
                                                        ? new Cable(
                                                              matchingConnector?.Cable?.Length,
                                                              lossCompensation.Resistance,
                                                              lossCompensation.Unit,
                                                              lossCompensation.Name                     ?? matchingConnector?.Cable?.LossCompensation,
                                                              lossCompensation.Identification?.ToString(CultureInfo.InvariantCulture) ?? matchingConnector?.Cable?.LossCompensationId
                                                          )
                                                        : matchingConnector?.Cable
                                                );

            }

            #endregion

            #region What the session points at, so a report never has to search for it

            chargingSession.ChargingStation  = chargingStation;
            chargingSession.EVSE             = chargingStation.EVSEs.FirstOrDefault();
            chargingSession.EnergyMeter      = energyMeter;

            #endregion

            foreach (var warning in ContainerInfos?.Warnings ?? [])
                record.AddWarning(warning);

            return record;

        }

        #endregion

        #region (private) ParseReading(Reading)

        /// <summary>
        /// Read and validate one OCMF reading.
        ///
        /// Every field is checked against what the standard allows rather than
        /// taken as given: a reading whose unit or transaction type is not one of
        /// the permitted values is not a reading Chargy can present to an EV
        /// driver, however well it may be signed.
        /// </summary>
        /// <param name="Reading">A reading, with any inherited fields already merged in.</param>
        private static (OCMFReading? Value, String? Error) ParseReading(JObject Reading)
        {

            var time         = Text(Reading, "TM");
            var transaction  = Text(Reading, "TX");
            var obis         = Text(Reading, "RI");
            var unit         = Text(Reading, "RU");
            var currentType  = Text(Reading, "RT");
            var status       = Text(Reading, "ST");

            if (time is null || unit is null || status is null)
                return (null, "UnknownOrInvalidChargingSessionFormat");

            #region The timestamp and how the meter's clock was synchronised

            // "2019-06-26T08:57:44,337+0000 U": the instant and one letter saying
            // how far the clock behind it can be trusted.
            var timeParts = time.Split(' ');

            if (timeParts.Length != 2)
                return (null, "The given OCMF meter value does not have a valid time format!");

            if (!OCMFTimestampRegex().IsMatch(timeParts[0]) ||
                !knownTimeSyncs.Contains(timeParts[1]))
                return (null, "The given OCMF meter value does not have a valid time sync format!");

            #endregion

            #region The value, which some vendors write as a JSON string

            var readingValue = Reading["RV"];

            if (readingValue is null ||
                !Decimal.TryParse(readingValue.Value<String>(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return (null, "UnknownOrInvalidChargingSessionFormat");

            #endregion

            if (transaction is not null && transaction.Length > 0 && !knownTransactions.Contains(transaction))
                return (null, "The given OCMF meter value does not have a valid transaction type!");

            if (!knownUnits.Contains(unit))
                return (null, "The given OCMF meter value does not have a valid current type!");

            if (currentType is not null && currentType.Length > 0 && currentType is not ("AC" or "DC"))
                return (null, "The given OCMF meter value does not have a valid current type!");

            return (
                       new OCMFReading(
                           ToISO8601(timeParts[0]),
                           value,
                           obis ?? "?",
                           unit,
                           currentType,
                           timeParts[1],
                           transaction,
                           Number(Reading, "EI"),
                           Text  (Reading, "EF"),
                           Number(Reading, "CL"),
                           status
                       ),
                       null
                   );

        }

        #endregion

        #region (private, static) ParsePaging(Paging)

        /// <summary>
        /// Read an OCMF pagination counter, e.g. "T12345".
        /// </summary>
        /// <param name="Paging">An OCMF "PG" value.</param>
        private static (OCMFTransactionType TransactionType, UInt64 Pagination)? ParsePaging(String? Paging)
        {

            if (Paging is null || Paging.Length < 2)
                return null;

            var transactionType = Char.ToLowerInvariant(Paging[0]) switch {
                                      't'  => OCMFTransactionType.Transaction,
                                      'f'  => OCMFTransactionType.Fiscal,
                                      _    => OCMFTransactionType.Undefined
                                  };

            if (transactionType == OCMFTransactionType.Undefined ||
                !UInt64.TryParse(Paging.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var pagination))
            {
                return null;
            }

            return (transactionType, pagination);

        }

        #endregion

        #region (private, static) ResultOf   (Document)

        /// <summary>
        /// The verdict a reading inherits from the document it arrived in,
        /// together with everything that was found wrong along the way.
        /// </summary>
        /// <param name="Document">An OCMF document.</param>
        private static CryptoResult ResultOf(OCMFDocument Document)
        {

            var result = new CryptoResult(Document.ValidationStatus);

            foreach (var error in Document.Errors)
                result.AddError(error);

            return result;

        }

        #endregion

        #region (private, static) SessionIdOf(Documents)

        /// <summary>
        /// Derive an identifier for the charging session from the documents it was
        /// built from.
        ///
        /// OCMF carries no session identifier of its own. The hash is deliberate
        /// rather than random: verifying the same record twice has to produce the
        /// same report, and two different records still have to be told apart.
        ///
        /// Hashed over the canonical form of the parsed payload and signature,
        /// never over the document text. The text carries formatting the record
        /// does not — the DZG test data is pretty-printed JSON spanning dozens of
        /// lines, and a checkout that rewrites line endings, which is the default
        /// on Windows, gave an otherwise identical record a different identifier.
        /// Minified against pretty-printed would diverge just as much on one
        /// machine. Signature verification never noticed, because it canonicalises
        /// the payload before checking — which is exactly why the identifier had
        /// to stop depending on the text too.
        ///
        /// Deliberately not the document's own hash value, which looks like the
        /// obvious candidate but follows the signature algorithm — SHA-384 or
        /// SHA-512 for some, and empty for Ed25519, Ed448 and ML-DSA, which sign
        /// the message directly. An identifier that is constantly empty for three
        /// algorithms identifies nothing.
        /// </summary>
        /// <param name="Documents">The OCMF documents.</param>
        private static String SessionIdOf(List<OCMFDocument> Documents)

            => "OCMF-" + Convert.ToHexStringLower(
                             SHA256.HashData(
                                 Encoding.UTF8.GetBytes(
                                     String.Join(
                                         "\n",
                                         Documents.Select(document => CanonicalJSON.Serialize(
                                                                          new JObject(
                                                                              new JProperty("payload",   document.Payload),
                                                                              new JProperty("signature", document.Signature)
                                                                          )
                                                                      ))
                                     )
                                 )
                             )
                         );

        #endregion

        #region (private, static) ToISO8601 (Timestamp)

        /// <summary>
        /// Turn an OCMF timestamp into ISO 8601.
        ///
        /// OCMF writes "2019-06-26T08:57:44,337+0000"; ISO 8601 wants a dot before
        /// the milliseconds and a colon in the offset. The offset itself is kept
        /// as the meter reported it rather than normalised to UTC, because it says
        /// what the meter's own clock read.
        /// </summary>
        /// <param name="Timestamp">An OCMF timestamp.</param>
        private static String ToISO8601(String Timestamp)

            => OCMFOffsetRegex().Replace(Timestamp.Replace(',', '.'), "$1$2:$3");

        #endregion

        #region (private, static) Text      (JSON, Key)

        /// <summary>
        /// A string property, or null when it is absent or not a string.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        /// <param name="Key">The name of a property.</param>
        private static String? Text(JObject  JSON,
                                    String   Key)

            => JSON[Key]?.Type == JTokenType.String
                   ? JSON[Key]!.Value<String>()
                   : null;

        #endregion

        #region (private, static) Number    (JSON, Key)

        /// <summary>
        /// A numeric property, or null when it is absent or not a number.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        /// <param name="Key">The name of a property.</param>
        private static Decimal? Number(JObject  JSON,
                                       String   Key)

            => JSON[Key]?.Type is JTokenType.Integer or JTokenType.Float
                   ? JSON[Key]!.Value<Decimal>()
                   : null;

        #endregion

        #region (private, static) TextArray (JSON, Key)

        /// <summary>
        /// An array of strings, or null when it is absent or not one.
        ///
        /// Note that null and an empty array are different answers here: the OCMF
        /// specification makes the identification flags mandatory, several vendors
        /// omit them anyway, and an empty list is what a reader is supposed to see
        /// in that case. Which is a rule about presentation, not about evidence —
        /// a meter that said nothing about how the driver identified themselves
        /// has not thereby said that they identified themselves in no way.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        /// <param name="Key">The name of a property.</param>
        private static IEnumerable<String>? TextArray(JObject  JSON,
                                                      String   Key)

            => JSON[Key] is JArray array
                   ? array.Where (element => element.Type == JTokenType.String).
                           Select(element => element.Value<String>()!)
                   : null;

        #endregion

        #region (private) Invalid           (MessageKey)

        /// <summary>
        /// Report that the data is not a valid OCMF charging session.
        /// </summary>
        /// <param name="MessageKey">The i18n key of the reason.</param>
        private SessionCryptoResult Invalid(String MessageKey)

            => new (
                   SessionVerificationResult.InvalidSessionFormat,
                   i18n.GetMultilanguageText(MessageKey)
               );

        #endregion

        #region (private) Regular expressions

        [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2},\d{3}[+-]\d{4}$")]
        private static partial Regex OCMFTimestampRegex();

        [GeneratedRegex(@"([+-])(\d{2})(\d{2})$")]
        private static partial Regex OCMFOffsetRegex();

        [GeneratedRegex(@"^(\S+)\s+(\S+)$")]
        private static partial Regex ChargePointAndConnectorRegex();

        #endregion


    }


    /// <summary>
    /// One reading of an OCMF document.
    /// </summary>
    /// <param name="Timestamp">When the meter read this value.</param>
    /// <param name="Value">What it read.</param>
    /// <param name="OBIS">Which register it read, as an OBIS code.</param>
    /// <param name="Unit">The unit of the value.</param>
    /// <param name="CurrentType">Whether the charging was AC or DC.</param>
    /// <param name="TimeSync">How far the meter's clock can be trusted.</param>
    /// <param name="Transaction">Where in the charging session the reading was taken.</param>
    /// <param name="ErrorIndex">An error index, as OCMF 0.1 wrote it.</param>
    /// <param name="ErrorFlags">Error flags, as OCMF 1.x writes them.</param>
    /// <param name="CumulatedLoss">The energy compensated for the charging cable so far.</param>
    /// <param name="Status">The status word of the energy meter.</param>
    internal readonly record struct OCMFReading(String    Timestamp,
                                                Decimal   Value,
                                                String    OBIS,
                                                String    Unit,
                                                String?   CurrentType,
                                                String?   TimeSync       = null,
                                                String?   Transaction    = null,
                                                Decimal?  ErrorIndex     = null,
                                                String?   ErrorFlags     = null,
                                                Decimal?  CumulatedLoss  = null,
                                                String?   Status         = null);

}
