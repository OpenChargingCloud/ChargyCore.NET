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

            #region What the meter says about itself

            // "MS" is the meter's own serial; "GS" the gateway's. A record that
            // names neither cannot say which device produced the readings.
            var meterSerial  = Text(payload, "MS") ?? Text(payload, "GS");
            var paging       = Text(payload, "PG");

            if (meterSerial is null || paging is null)
                return Invalid("UnknownOrInvalidChargingSessionFormat");

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
                        new MeasurementValue(
                            ocmfReading.Timestamp,
                            ocmfReading.Value
                        ) {
                            // The reading is not signed on its own: the signature
                            // covers the whole OCMF document it arrived in, so its
                            // verdict is what every reading inside inherits.
                            Result = new CryptoResult(document.ValidationStatus)
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
                                              Unit: reading.Unit
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
                                      AuthorizationStart = new Authorization(Text(payload, "ID") ?? "?")
                                  };

            var record = new ChargeTransparencyRecord(
                             sessionId,
                             [ "https://open.charging.cloud/contexts/CTR+json" ],
                             begin,
                             end,
                             Certainty:  1,
                             Status:     SessionVerificationResult.Unvalidated
                         );

            record.AddChargingSession(chargingSession);

            #region The energy meter, so that the record says which device signed

            var energyMeter = new EnergyMeter(
                                  meterSerial,
                                  Manufacturer:     Text(payload, "MV") is String vendor ? new Manufacturer(vendor) : null,
                                  Model:            Text(payload, "MM") is String model  ? new DeviceModel (model)  : null,
                                  Firmware:         Text(payload, "MF") is String meterFirmware ? new Firmware(meterFirmware) : null,
                                  SignatureFormat:  MeasurementContext
                              );

            // The meter hangs off the EVSE when the record names one, and off the
            // charging station otherwise — so that a verification can find it
            // either way rather than depending on how complete the record is.
            var evseId          = signedEVSEId ?? ContainerInfos?.FirstEVSEId;

            var chargingStation = new ChargingStation(
                                      signedChargingStationId ?? ContainerInfos?.FirstChargingStationId ?? meterSerial,
                                      EVSEs:         evseId is not null
                                                         ? [ new EVSE(evseId, EnergyMeters: [ energyMeter ]) ]
                                                         : null,
                                      EnergyMeters:  evseId is null
                                                         ? [ energyMeter ]
                                                         : null
                                  );

            record.AddChargingStation(chargingStation);

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
                           currentType
                       ),
                       null
                   );

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
    internal readonly record struct OCMFReading(String   Timestamp,
                                                Decimal  Value,
                                                String   OBIS,
                                                String   Unit,
                                                String?  CurrentType);

}
