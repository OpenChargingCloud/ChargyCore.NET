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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy.Formats.BSM
{

    /// <summary>
    /// The BAUER Electronic BSM-WS36A meter value format.
    ///
    /// The meter publishes signed snapshots of its whole state, and a charging
    /// session is a sequence of them: one marking the start, any number of
    /// intermediate ones, and one marking the end. Most of the work here is
    /// checking that the sequence hangs together — that the counters advance by
    /// one, that the clock moves forward, that every snapshot names the same
    /// meter, the same contract and the same EVSE.
    ///
    /// None of that is a cryptographic check, and it is not redundant with one.
    /// A signature proves that the meter really produced a snapshot; it says
    /// nothing about whether somebody handed over only the snapshots that suited
    /// them. The counters are what makes a removed snapshot noticeable.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    public class BSMFormat(I18NDictionary I18N)
    {

        #region Data

        /// <summary>The JSON-LD context of a BSM charging session.</summary>
        public const String SessionContext = "https://open.charging.cloud/contexts/SessionSignatureFormats/bsm-ws36a-v0+json";

        /// <summary>The JSON-LD contexts of a BSM signed meter value.</summary>
        public static readonly IReadOnlySet<String> MeterValueContexts = new HashSet<String>(StringComparer.Ordinal) {
            "https://www.lichtblick.de/contexts/bsm-ws36a-json-v0",
            "https://www.lichtblick.de/contexts/bsm-ws36a-json-v1",
            "https://www.eneco.com/contexts/bsm-ws36a-json-v0",
            "https://www.eneco.com/contexts/bsm-ws36a-json-v1",
            "https://www.chargeit-mobility.com/contexts/bsm-ws36a-json-v0",
            "https://www.chargeit-mobility.com/contexts/bsm-ws36a-json-v1"
        };

        /// <summary>
        /// How many checks a pair of signed meter values is put through.
        ///
        /// The certainty a record is reported with is the share of these that
        /// passed, which is how Chargy can say "this looks like a BSM record but
        /// a broken one" rather than only "unknown format".
        /// </summary>
        private const Int32 ChecksPerMeterValue = 39;

        private readonly I18NDictionary i18n = I18N;

        #endregion

        #region Properties

        /// <summary>The name of the data format.</summary>
        public String Name
            => "BSM-WS36A";

        #endregion


        #region TryParse(ExpectedEVSEId, ExpectedControllerSoftwareVersion, SignedMeterValues)

        /// <summary>
        /// Read a charging session out of a sequence of signed BSM snapshots.
        ///
        /// What comes back is the session and the meter that signed it, not a
        /// whole charge transparency record: who operates the charging station,
        /// where it stands and who to complain to are the container's business,
        /// and a meter format has no way of knowing any of it.
        /// </summary>
        /// <param name="ExpectedEVSEId">The EVSE the container says these snapshots came from.</param>
        /// <param name="ExpectedControllerSoftwareVersion">The charging station software version the container claims, if any.</param>
        /// <param name="SignedMeterValues">The signed snapshots.</param>
        /// <returns>A <see cref="BSMChargingSession"/>, or a <see cref="SessionCryptoResult"/> saying why it is not one.</returns>
        public Object TryParse(String   ExpectedEVSEId,
                               String?  ExpectedControllerSoftwareVersion,
                               JArray   SignedMeterValues)
        {

            var errors    = new List<Error>();
            var warnings  = new List<Warning>();

            var numberOfFormatChecks = 2 * ChecksPerMeterValue;

            #region A charging session is a sequence of snapshots, not a single one

            if (SignedMeterValues.Count < 2)
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSessionFormat,
                           i18n.GetMultilanguageText("AtLeastTwoSignedMeterValuesRequired")
                       );

            if (SignedMeterValues[0] is not JObject firstMeterValue)
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSessionFormat,
                           i18n.GetMultilanguageTextWithParameter("MissingOrInvalidSignedMeterValueP", 1)
                       );

            #endregion

            try
            {

                #region Everything the snapshots have to agree about, taken from the first one

                var common = BSMCommonValues.From(firstMeterValue, i18n, errors);

                if (errors.Count > 0)
                    return Failed(
                               errors,
                               warnings,
                               (numberOfFormatChecks - errors.Count) / (Double) numberOfFormatChecks
                           );

                #endregion

                #region Walk the snapshots, checking that they form one uninterrupted session

                var snapshots = new List<BSMSnapshot>();

                String?  previousId                  = null;
                String   previousTime                = "";
                Decimal? previousValue               = null;
                Int64    previousRCR                 = -1;
                Int64    previousRCnt                = -1;
                Int64    previousOS                  = -1;
                Int64    previousEpoch               = -1;
                String?  previousControllerVersion   = null;
                var      snapshotCounter             = 0;

                foreach (var meterValueToken in SignedMeterValues)
                {

                    snapshotCounter++;

                    if (meterValueToken is not JObject meterValue)
                        throw new BSMValidationException($"Invalid signed meter value #{snapshotCounter}!");

                    var snapshot = BSMSnapshot.Parse(meterValue, snapshotCounter);

                    void Inconsistent(String MessageKey)
                        => snapshot.Errors.Add(new Error(i18n.GetMultilanguageTextWithParameter(MessageKey, snapshotCounter)));

                    #region The identification is "prefix-counter", and the counter has to climb

                    if (previousId is not null && snapshot.Id is not null)
                    {

                        var previousParts  = previousId.  Split('-');
                        var currentParts   = snapshot.Id. Split('-');

                        if (previousParts.Length != 2 ||
                            currentParts. Length != 2 ||
                            previousParts[0] != currentParts[0] ||
                            !Int64.TryParse(previousParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var previousCounter) ||
                            !Int64.TryParse(currentParts [1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentCounter)  ||
                            previousCounter >= currentCounter)
                        {
                            Inconsistent("Inconsistent_SignedMeterValue_MeasurementIdP");
                        }

                    }

                    previousId = snapshot.Id;

                    #endregion

                    #region ..., time moves forward, and the meter reading never falls

                    if (previousTime.Length > 0 &&
                        String.CompareOrdinal(snapshot.Time, previousTime) <= 0)
                    {
                        Inconsistent("Inconsistent_SignedMeterValue_TimestampP");
                    }

                    previousTime = snapshot.Time;

                    if (previousValue.HasValue && snapshot.Value.HasValue && snapshot.Value < previousValue)
                        Inconsistent("Inconsistent_Measurement_ValueP");

                    if (snapshot.Value.HasValue)
                        previousValue = snapshot.Value;

                    #endregion

                    #region ..., every snapshot describes the same meter

                    if (snapshot.MeterInfo is not null)
                    {

                        if (Text(snapshot.MeterInfo, "firmwareVersion") != common.MeterFirmwareVersion)  Inconsistent("Inconsistent_SignedMeterValue_MeterInfo_FirmwareVersionP");
                        if (Text(snapshot.MeterInfo, "publicKey")       != common.MeterPublicKey)        Inconsistent("Inconsistent_SignedMeterValue_MeterInfo_PublicKeyP");
                        if (Text(snapshot.MeterInfo, "manufacturer")    != common.MeterManufacturer)     Inconsistent("Inconsistent_SignedMeterValue_MeterInfo_ManufacturerP");
                        if (Text(snapshot.MeterInfo, "type")            != common.MeterType)             Inconsistent("Inconsistent_SignedMeterValue_MeterInfo_TypeP");

                        // The meter identification in the header has to agree with
                        // the one the meter itself signed as "MA1" — otherwise the
                        // header is describing a different device than the one that
                        // produced the evidence.
                        if (Text(snapshot.MeterInfo, "meterId")         != common.MeterId)               Inconsistent("Inconsistent_SignedMeterValue_MeterInfo_MeterIdP");
                        if (Text(snapshot.MeterInfo, "meterId")         != snapshot.MA1)                 Inconsistent("Inconsistent_SignedMeterValue_MeterInfo_MeterIdP");

                    }

                    #endregion

                    #region ..., and the same contract, which the meter also signed into its metadata

                    if (snapshot.Contract is not null)
                    {

                        var signedContract = snapshot.MetaStartingWith("contract-id:");

                        if (signedContract is null)
                            Inconsistent("Inconsistent_SignedMeterValue_Contract_IdP");

                        if (Text(snapshot.Contract, "id")   != common.ContractId)    Inconsistent("Inconsistent_SignedMeterValue_Contract_IdP");
                        if (Text(snapshot.Contract, "type") != common.ContractType)  Inconsistent("Inconsistent_SignedMeterValue_Contract_TypeP");

                        if (signedContract is not null)
                        {

                            var expected = common.ContractType is not null
                                               ? $"contract-id: {common.ContractType ?? "-"}:{common.ContractId}"
                                               : $"contract-id: {common.ContractId}";

                            if (signedContract != expected)
                                Inconsistent("Inconsistent_SignedMeterValue_Contract_IdP");

                        }

                    }

                    #endregion

                    #region The header's reading has to be the very one the meter signed as "RCR"

                    if (snapshot.RCR is null && (snapshot.Measurand is not null || snapshot.MeasuredValue is not null))
                        throw new BSMValidationException($"Missing 'RCR' within the additional values of signed meter value #{snapshotCounter}!");

                    if (snapshot.Measurand is not null)
                    {

                        if (Text(snapshot.Measurand, "id")   != common.MeasurandId   || Text(snapshot.Measurand, "id")   != snapshot.RCR?.OBIS)
                            Inconsistent("Inconsistent_Measurand_IdentificationP");

                        if (Text(snapshot.Measurand, "name") != common.MeasurandName || Text(snapshot.Measurand, "name") != snapshot.RCR?.Name)
                            Inconsistent("Inconsistent_Measurand_NameP");

                    }

                    if (snapshot.MeasuredValue is not null)
                    {

                        if (snapshot.Value                                  != snapshot.RCR?.DecimalValue)                                          Inconsistent("Inconsistent_SignedMeterValueP");
                        if (Number(snapshot.MeasuredValue, "scale")         != common.Scale       || Number(snapshot.MeasuredValue, "scale")       != snapshot.RCR?.Scale)        Inconsistent("Inconsistent_SignedMeterValue_ScaleP");
                        if (Text  (snapshot.MeasuredValue, "unit")          != common.Unit        || Text  (snapshot.MeasuredValue, "unit")        != snapshot.RCR?.Unit)         Inconsistent("Inconsistent_SignedMeterValue_UnitP");
                        if (Number(snapshot.MeasuredValue, "unitEncoded")   != common.UnitEncoded || Number(snapshot.MeasuredValue, "unitEncoded") != snapshot.RCR?.UnitEncoded)  Inconsistent("Inconsistent_SignedMeterValue_UnitEncodedP");
                        if (Text  (snapshot.MeasuredValue, "valueType")     != common.ValueType   || Text  (snapshot.MeasuredValue, "valueType")   != snapshot.RCR?.ValueType)    Inconsistent("Inconsistent_SignedMeterValue_TypeP");

                    }

                    #endregion

                    #region ..., and the EVSE and the charging station software the meter signed have to be the ones the container names

                    if (snapshot.ChargePoint is not null &&
                        Text(snapshot.ChargePoint, "softwareVersion") != common.ChargePointSoftwareVersion)
                    {
                        snapshot.Errors.Add(new Error(i18n.GetMultilanguageText("Inconsistent_ChargingStation_FirmwareVersion")));
                    }

                    if (snapshot.MetaStartingWith("evse-id:") is String signedEVSEId)
                    {

                        var evseId = signedEVSEId.Replace("evse-id:", "").Trim();

                        // "unknown" is what a station writes when it has not been
                        // told its own EVSE identification, and it is not a
                        // contradiction of the container's claim — only silence.
                        if (evseId != "unknown" && evseId != ExpectedEVSEId)
                            snapshot.Errors.Add(new Error(i18n.GetMultilanguageText("Inconsistent_EVSE_Identification")));

                    }

                    if (snapshot.MetaStartingWith("csc-sw-version:") is String signedVersion)
                    {

                        var controllerVersion = signedVersion.Replace("csc-sw-version:", "").Trim();

                        if (previousControllerVersion is not null &&
                            previousControllerVersion != controllerVersion)
                        {
                            snapshot.Errors.Add(new Error(i18n.GetMultilanguageText("Inconsistent_ChargingStation_FirmwareVersion")));
                        }

                        // The container states this version too, but combined with a
                        // build timestamp, and it is not signed. Comparing the two
                        // soundly is hopeless once release candidates and betas are
                        // involved, so a mismatch is only worth a warning.
                        if (ExpectedControllerSoftwareVersion is not null &&
                            ExpectedControllerSoftwareVersion != controllerVersion)
                        {
                            snapshot.Warnings.Add(
                                new Warning(
                                    i18n.GetMultilanguageText("Inconsistent_ChargingStation_FirmwareVersion"),
                                    SeverityLevel.Medium
                                )
                            );
                        }

                        previousControllerVersion = controllerVersion;

                    }

                    #endregion

                    #region The counters are what makes a removed snapshot noticeable

                    if (previousRCR != -1 && snapshot.RCRValue < previousRCR)
                        Inconsistent("Inconsistent_Measurement_ValueP");
                    previousRCR = snapshot.RCRValue;

                    if (snapshot.RCnt != snapshot.MeasurementId)
                        Inconsistent("Inconsistent_SignedMeterValue_MeasurementIdP");

                    if (previousRCnt != -1 && snapshot.RCnt != previousRCnt + 1)
                        Inconsistent("Inconsistent_SignedMeterValue_CounterP");
                    previousRCnt = snapshot.RCnt;

                    if (previousOS != -1 && snapshot.OS <= previousOS)
                        Inconsistent("Inconsistent_SignedMeterValue_OperationSecondsCounterP");
                    previousOS = snapshot.OS;

                    if (previousEpoch != -1 && snapshot.Epoch <= previousEpoch)
                        Inconsistent("Inconsistent_SignedMeterValue_UNIXEpochP");
                    previousEpoch = snapshot.Epoch;

                    #endregion

                    #region ..., and the header's timestamp has to be the meter's own clock, spelled out

                    if (snapshot.Time != TimestampOf(snapshot.Epoch, snapshot.TZO))
                        Inconsistent("Inconsistent_SignedMeterValue_TimestampP");

                    if (common.MA1 is not null && snapshot.MA1 != common.MA1)
                        Inconsistent("Inconsistent_SignedMeterValue_MeterInfo_MeterIdP");
                    common.MA1 = snapshot.MA1;

                    if (common.EpochSetCnt != -1 && snapshot.EpochSetCnt != common.EpochSetCnt)
                        Inconsistent("Inconsistent_SignedMeterValue_EpochSet_CounterP");
                    common.EpochSetCnt = snapshot.EpochSetCnt;

                    if (common.EpochSetOS != -1 && snapshot.EpochSetOS != common.EpochSetOS)
                        Inconsistent("Inconsistent_SignedMeterValue_EpochSet_OperationSecondsP");
                    common.EpochSetOS = snapshot.EpochSetOS;

                    #endregion

                    snapshots.Add(snapshot);

                }

                #endregion

                #region A session opens with a start and closes with an end

                var first = snapshots[0];
                var last  = snapshots[^1];

                if (first.TypeName != "START" && first.TypeName != "TURN ON")
                    throw new BSMValidationException(i18n.GetLocalizedMessageWithParameter("Inconsistent_EnergyMeterValueP", 1));

                for (var i = 1; i < snapshots.Count - 1; i++)
                    if (snapshots[i].TypeName != "CURRENT")
                        errors.Add(new Error(i18n.GetMultilanguageTextWithParameter("Inconsistent_EnergyMeterValueP", i + 1)));

                if (last.TypeName != "END" && last.TypeName != "TURN OFF")
                    throw new BSMValidationException(i18n.GetLocalizedMessageWithParameter("Inconsistent_EnergyMeterValueP", snapshots.Count));

                #endregion

                #region Build the charging session

                var chargingSessionId  = $"{common.MeterId}-{first.Epoch}";

                var measurement = new Measurement(
                                      common.MeterId,
                                      // A BSM snapshot signs three quantities at
                                      // once, so the group is named by its parts
                                      // rather than by a name of its own.
                                      Name:            null,
                                      OBIS:            null,
                                      Scale:           common.Scale ?? 0,
                                      Values:          snapshots.Select(snapshot => snapshot.ToMeasurementValue()),
                                      Unit:            common.Unit,
                                      UnitEncoded:     (UInt16?) common.UnitEncoded,
                                      ValueType:       common.ValueType,
                                      SignatureInfos:  new SignatureInfos(
                                                           Hash:            CryptoHashAlgorithm.SHA256,
                                                           Algorithm:       CryptoAlgorithm.ECC,
                                                           Curve:           ECCurve.secp256r1,
                                                           Format:          SignatureFormat.RS,
                                                           HashTruncation:  0
                                                       ),
                                      Phenomena:       [
                                                           new Phenomenon(
                                                               "Real Energy Imported",
                                                               "value",
                                                               common.MeasurandId,
                                                               UnitNameOf(common.Unit),
                                                               (UInt16?) common.UnitEncoded,
                                                               common.ValueType,
                                                               common.Scale,
                                                               DisplayPrefixOf(common.DisplayPrefix),
                                                               (UInt16?) common.DisplayPrecision
                                                           ),
                                                           PhenomenonOf("Total Watt-hours Imported", "TotWhImp", first.TotWhImp),
                                                           PhenomenonOf("Total Real Power",          "W",        first.W)
                                                       ]
                                  );

                var chargingSession = new ChargingSession(
                                          chargingSessionId,
                                          Context:        [ SessionContext ],
                                          Begin:          first.Time,
                                          End:            last. Time,
                                          EVSEId:         ExpectedEVSEId,
                                          EnergyMeterId:  common.MeterId,
                                          Measurements:   [ measurement ]
                                      ) {
                                          AuthorizationStart = new Authorization(
                                                                   common.ContractId,
                                                                   Context: common.ContractType is not null
                                                                                ? [ common.ContractType ]
                                                                                : null
                                                               )
                                      };

                #endregion

                #region ..., and the meter that signed it

                var energyMeter = new EnergyMeter(
                                      common.MeterId,
                                      Manufacturer:     new Manufacturer(
                                                            common.MeterManufacturer,
                                                            Contact: new Contact(Web: "https://www.bzr-bauer.de")
                                                        ),
                                      Model:            new DeviceModel(common.MeterType),
                                      Firmware:         new Firmware(common.MeterFirmwareVersion),
                                      SignatureInfos:   new SignatureInfos(
                                                            Hash:            CryptoHashAlgorithm.SHA256,
                                                            Algorithm:       CryptoAlgorithm.ECC,
                                                            Curve:           ECCurve.secp256r1,
                                                            Format:          SignatureFormat.RS,
                                                            HashTruncation:  0
                                                        ),
                                      SignatureFormat:  "BSMCrypt01",
                                      PublicKeys:       [
                                                            new PublicKey(
                                                                common.MeterPublicKey,
                                                                new OIDInfo("secp256r1"),
                                                                Format:    "DER",
                                                                Encoding:  "hex"
                                                            )
                                                        ]
                                  );

                #endregion

                return new BSMChargingSession {
                           ChargingSession  = chargingSession,
                           EnergyMeter      = energyMeter,
                           ContractId       = common.ContractId,
                           ContractType     = common.ContractType,
                           Errors           = errors,
                           Warnings         = warnings,
                           Certainty        = 1 - errors.Count / (Double) numberOfFormatChecks
                       };

            }
            catch (Exception exception)
            {

                errors.Add(new Error(I18NString.Create(Languages.en, $"Exception occured: {exception.Message}")));

                return Failed(errors, warnings, 0, exception);

            }

        }

        #endregion


        #region (private, static) Failed(Errors, Warnings, Certainty, Exception = null)

        /// <summary>
        /// Report that the snapshots do not make up a BSM charging session, with
        /// everything found wrong along the way.
        /// </summary>
        /// <param name="Errors">What was missing or inconsistent.</param>
        /// <param name="Warnings">What looked suspicious but was not fatal.</param>
        /// <param name="Certainty">The share of the format checks that passed.</param>
        /// <param name="Exception">An optional exception that ended the reading.</param>
        private static SessionCryptoResult Failed(IEnumerable<Error>    Errors,
                                                  IEnumerable<Warning>  Warnings,
                                                  Double                Certainty,
                                                  Exception?            Exception = null)
        {

            var result = new SessionCryptoResult(
                             SessionVerificationResult.InvalidSessionFormat,
                             Certainty:  Certainty,
                             Exception:  Exception
                         );

            foreach (var error   in Errors)    result.AddError  (error);
            foreach (var warning in Warnings)  result.AddWarning(warning);

            return result;

        }

        #endregion

        #region (static) SnapshotTypeName(Type)

        /// <summary>
        /// What kind of snapshot a type code names.
        /// </summary>
        /// <param name="Type">The snapshot type an "Typ" field carries.</param>
        public static String SnapshotTypeName(Int64 Type)

            => Type switch {
                   0  => "CURRENT",    // the meter's state at the moment the snapshot was taken
                   1  => "TURN ON",    // taken while switching an external contactor on
                   2  => "TURN OFF",   // taken while switching it off
                   3  => "START",      // the start of a charging session without switching a contactor
                   4  => "END",        // the end of one
                   _  => "<unknown>"
               };

        #endregion

        #region (static) ParseEvents    (Events)

        /// <summary>
        /// What the meter's event flags report, in words.
        ///
        /// The reserved and OEM bits are named rather than skipped: a meter that
        /// sets one is saying something, and dropping it silently would hide the
        /// fact that it did.
        /// </summary>
        /// <param name="Events">The event word of a snapshot.</param>
        public static IEnumerable<String> ParseEvents(Int64 Events)
        {

            var names = new List<String>();

            void Flag(Int32 Bit, String Name)
            {
                if ((Events & (1L << Bit)) != 0)
                    names.Add(Name);
            }

            Flag( 1, "Power Failure");
            Flag( 2, "Under Voltage");
            Flag( 3, "Low PF");
            Flag( 4, "Over Current");
            Flag( 5, "Over Voltage");
            Flag( 6, "Missing Sensor");

            for (var bit = 7; bit <= 14; bit++)
                Flag(bit, $"Reserved {bit - 6}");

            Flag(15, "Meter Fatal Error");
            Flag(16, "CM Init Failed");
            Flag(17, "CM Firmware Hash Mismatch");
            Flag(18, "CM Development Mode");

            for (var bit = 19; bit <= 29; bit++)
                Flag(bit, $"OEM {bit - 14:D2}");

            return names;

        }

        #endregion

        #region (static) DisplayPrefixOf(Prefix)

        /// <summary>
        /// The display scaling a "displayedFormat" prefix names.
        /// </summary>
        /// <param name="Prefix">A prefix, e.g. "kilo".</param>
        public static DisplayPrefix DisplayPrefixOf(String? Prefix)

            => (Prefix ?? "").ToLowerInvariant() switch {
                   "kilo"  => DisplayPrefix.KILO,
                   "mega"  => DisplayPrefix.MEGA,
                   "giga"  => DisplayPrefix.GIGA,
                   _       => DisplayPrefix.NULL
               };

        #endregion

        #region (static) UnitNameOf     (Unit)

        /// <summary>
        /// The symbol of a unit a BSM meter names in words.
        /// </summary>
        /// <param name="Unit">A unit, e.g. "WATT_HOUR".</param>
        public static String UnitNameOf(String? Unit)

            => (Unit ?? "").ToUpperInvariant() switch {
                   "WATT_HOUR"  => "Wh",
                   "WATT"       => "W",
                   _            => ""
               };

        #endregion

        #region (static) TimestampOf    (Epoch, TimeZoneOffset)

        /// <summary>
        /// The meter's own clock spelled out as an ISO 8601 timestamp.
        ///
        /// The meter reports the moment and its offset to UTC separately, and the
        /// header of a snapshot repeats them as text. Rebuilding that text is how
        /// the two are checked against each other — a header that disagrees with
        /// the signed clock is describing a different moment than the meter did.
        /// </summary>
        /// <param name="Epoch">The meter's local time, in seconds since the UNIX epoch.</param>
        /// <param name="TimeZoneOffset">Its offset to UTC, in minutes.</param>
        public static String TimestampOf(Int64  Epoch,
                                         Int64  TimeZoneOffset)
        {

            var local = DateTimeOffset.FromUnixTimeSeconds(Epoch + TimeZoneOffset * 60).UtcDateTime;
            var sign  = TimeZoneOffset > 0 ? "+" : "-";

            return local.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) +
                   sign +
                   (Math.Abs(TimeZoneOffset) / 60).ToString("D2", CultureInfo.InvariantCulture) +
                   ":" +
                   (Math.Abs(TimeZoneOffset) % 60).ToString("D2", CultureInfo.InvariantCulture);

        }

        #endregion


        #region (private, static) PhenomenonOf(Name, Value, AdditionalValue)

        /// <summary>
        /// One of the quantities a snapshot reports alongside the energy reading.
        /// </summary>
        private static Phenomenon PhenomenonOf(String                Name,
                                               String                Value,
                                               BSMAdditionalValue?   AdditionalValue)

            => new (
                   Name,
                   Value,
                   AdditionalValue?.OBIS,
                   UnitNameOf(AdditionalValue?.Unit),
                   (UInt16?) AdditionalValue?.UnitEncoded,
                   AdditionalValue?.ValueType,
                   AdditionalValue?.Scale,
                   DisplayPrefixOf(AdditionalValue?.DisplayPrefix),
                   (UInt16?) AdditionalValue?.DisplayPrecision
               );

        #endregion

        #region (internal, static) JSON helpers

        /// <summary>A string property, or null when it is absent or not a string.</summary>
        internal static String? Text(JObject?  JSON,
                                     String    Key)

            => JSON?[Key]?.Type == JTokenType.String
                   ? JSON[Key]!.Value<String>()
                   : null;

        /// <summary>A numeric property, or null when it is absent or not a number.</summary>
        internal static Decimal? Number(JObject?  JSON,
                                        String    Key)

            => JSON?[Key]?.Type == JTokenType.Integer ||
               JSON?[Key]?.Type == JTokenType.Float
                   ? JSON[Key]!.Value<Decimal>()
                   : null;

        /// <summary>An object property, or null when it is absent or not an object.</summary>
        internal static JObject? Object(JObject?  JSON,
                                        String    Key)

            => JSON?[Key] as JObject;

        #endregion


    }


    /// <summary>
    /// A charging session read out of a sequence of signed BSM snapshots,
    /// together with the meter that signed them.
    ///
    /// The two are handed back separately because they belong in different places
    /// of a charge transparency record: the session under the record, the meter
    /// under the EVSE it is installed in — and only the container knows which EVSE
    /// that is.
    /// </summary>
    public class BSMChargingSession
    {

        /// <summary>The charging session.</summary>
        public required ChargingSession  ChargingSession    { get; init; }

        /// <summary>The energy meter that signed it.</summary>
        public required EnergyMeter      EnergyMeter        { get; init; }

        /// <summary>The contract the charging session was authorized with.</summary>
        public required String           ContractId         { get; init; }

        /// <summary>The kind of contract, e.g. "rfid".</summary>
        public          String?          ContractType       { get; init; }

        /// <summary>Everything inconsistent about the sequence of snapshots.</summary>
        public required List<Error>      Errors             { get; init; }

        /// <summary>Everything that looked suspicious about it.</summary>
        public required List<Warning>    Warnings           { get; init; }

        /// <summary>The share of the format checks that passed.</summary>
        public required Double           Certainty          { get; init; }

    }


    /// <summary>
    /// Something about a sequence of BSM snapshots does not hold up.
    /// </summary>
    /// <param name="Message">What went wrong.</param>
    public class BSMValidationException(String Message) : Exception(Message)
    { }

}
