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

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.Formats.BSM
{

    /// <summary>
    /// One entry of the "additionalValues" list of a BSM snapshot: a quantity the
    /// meter signed, flattened out of its nested JSON.
    /// </summary>
    public class BSMAdditionalValue
    {

        /// <summary>The OBIS code of the quantity, when it has one.</summary>
        public String?   OBIS                { get; init; }

        /// <summary>The short name the meter gives it, e.g. "RCR" or "Meta1".</summary>
        public String?   Name                { get; init; }

        /// <summary>The power of ten the value is scaled by.</summary>
        public Int32?    Scale               { get; init; }

        /// <summary>The unit of the value, e.g. "WATT_HOUR".</summary>
        public String?   Unit                { get; init; }

        /// <summary>The unit as a DLMS/COSEM code.</summary>
        public Int32?    UnitEncoded         { get; init; }

        /// <summary>The value itself, which is a number for most quantities and a string for the metadata.</summary>
        public JToken?   Value               { get; init; }

        /// <summary>How the value is encoded, e.g. "UnsignedInteger32".</summary>
        public String?   ValueType           { get; init; }

        /// <summary>An optional display scaling, e.g. "kilo".</summary>
        public String?   DisplayPrefix       { get; init; }

        /// <summary>An optional number of decimal places to display.</summary>
        public Int32?    DisplayPrecision    { get; init; }


        #region NumericValue

        /// <summary>The value as a number, or zero when it is not one.</summary>
        public Int64 NumericValue

            => Value?.Type == JTokenType.Integer ||
               Value?.Type == JTokenType.Float
                   ? (Int64) Value.Value<Decimal>()
                   : 0;

        #endregion

        #region DecimalValue

        /// <summary>The value as an exact number, or null when it is not one.</summary>
        public Decimal? DecimalValue

            => Value?.Type == JTokenType.Integer ||
               Value?.Type == JTokenType.Float
                   ? Value.Value<Decimal>()
                   : null;

        #endregion

        #region TextValue

        /// <summary>The value as text, or null when it is not text.</summary>
        public String? TextValue

            => Value?.Type == JTokenType.String
                   ? Value.Value<String>()
                   : null;

        #endregion

        #region (static) Parse(JSON)

        /// <summary>
        /// Read one entry of the "additionalValues" list.
        /// </summary>
        /// <param name="JSON">One entry of the list.</param>
        public static BSMAdditionalValue Parse(JObject JSON)
        {

            var measurand       = BSMFormat.Object(JSON, "measurand");
            var measuredValue   = BSMFormat.Object(JSON, "measuredValue");
            var displayedFormat = BSMFormat.Object(JSON, "displayedFormat");

            return new BSMAdditionalValue {
                       OBIS              = BSMFormat.Text  (measurand,       "id"),
                       Name              = BSMFormat.Text  (measurand,       "name"),
                       Scale             = (Int32?) BSMFormat.Number(measuredValue,   "scale"),
                       Unit              = BSMFormat.Text  (measuredValue,   "unit"),
                       UnitEncoded       = (Int32?) BSMFormat.Number(measuredValue,   "unitEncoded"),
                       Value             = measuredValue?["value"],
                       ValueType         = BSMFormat.Text  (measuredValue,   "valueType"),
                       DisplayPrefix     = BSMFormat.Text  (displayedFormat, "prefix"),
                       DisplayPrecision  = (Int32?) BSMFormat.Number(displayedFormat, "precision")
                   };

        }

        #endregion

    }


    /// <summary>
    /// One signed snapshot of a BSM meter, read out of its JSON and ready to be
    /// checked against its neighbours.
    /// </summary>
    public class BSMSnapshot
    {

        #region Properties — the header, which is not signed

        /// <summary>The identification the charging station gave this snapshot.</summary>
        public String?   Id                 { get; init; }

        /// <summary>When the snapshot was taken, as the header spells it.</summary>
        public String    Time               { get; init; } = "";

        /// <summary>The snapshot counter the header repeats.</summary>
        public Decimal?  MeasurementId      { get; init; }

        /// <summary>The energy reading the header repeats.</summary>
        public Decimal?  Value              { get; init; }

        /// <summary>The signature over the snapshot, DER encoded.</summary>
        public String    Signature          { get; init; } = "";

        /// <summary>What the header says about the meter.</summary>
        public JObject?  MeterInfo          { get; init; }

        /// <summary>What the header says about the contract.</summary>
        public JObject?  Contract           { get; init; }

        /// <summary>What the header says about the charging station.</summary>
        public JObject?  ChargePoint        { get; init; }

        /// <summary>What the header says was measured.</summary>
        public JObject?  Measurand          { get; init; }

        /// <summary>What the header says the reading was.</summary>
        public JObject?  MeasuredValue      { get; init; }

        /// <summary>An optional display scaling from the header.</summary>
        public String?   DisplayPrefix      { get; init; }

        /// <summary>An optional display precision from the header.</summary>
        public Int32?    DisplayPrecision   { get; init; }

        #endregion

        #region Properties — the snapshot the meter actually signed

        /// <summary>Every quantity the meter signed, by its short name.</summary>
        public IReadOnlyList<BSMAdditionalValue>  AdditionalValues    { get; init; } = [];

        /// <summary>Which kind of snapshot this is.</summary>
        public Int64     Type               { get; init; }

        /// <summary>Real energy imported since the last turn-on sequence.</summary>
        public BSMAdditionalValue?  RCR     { get; init; }

        /// <summary>Total real energy imported over the meter's lifetime.</summary>
        public BSMAdditionalValue?  TotWhImp { get; init; }

        /// <summary>Total real power at the moment of the snapshot.</summary>
        public BSMAdditionalValue?  W       { get; init; }

        /// <summary>The meter's own identification, as the meter signed it.</summary>
        public String?   MA1                { get; init; }

        /// <summary>A counter incremented with every snapshot.</summary>
        public Int64     RCnt               { get; init; }

        /// <summary>How many seconds the meter has been in operation.</summary>
        public Int64     OS                 { get; init; }

        /// <summary>The meter's local time, in seconds since the UNIX epoch.</summary>
        public Int64     Epoch              { get; init; }

        /// <summary>The offset of that local time to UTC, in minutes.</summary>
        public Int64     TZO                { get; init; }

        /// <summary>How often the time has been set.</summary>
        public Int64     EpochSetCnt        { get; init; }

        /// <summary>The operation seconds at which the time was last set.</summary>
        public Int64     EpochSetOS         { get; init; }

        /// <summary>The state of the digital inputs.</summary>
        public Int64     DI                 { get; init; }

        /// <summary>The state of the digital outputs.</summary>
        public Int64     DO                 { get; init; }

        /// <summary>User metadata 1.</summary>
        public String?   Meta1              { get; init; }

        /// <summary>User metadata 2.</summary>
        public String?   Meta2              { get; init; }

        /// <summary>User metadata 3.</summary>
        public String?   Meta3              { get; init; }

        /// <summary>The meter's event flags.</summary>
        public Int64     Evt                { get; init; }

        #endregion

        #region Properties — what did not add up about this snapshot

        /// <summary>Everything inconsistent about this snapshot.</summary>
        public List<Error>    Errors      { get; } = [];

        /// <summary>Everything that looked suspicious about this snapshot.</summary>
        public List<Warning>  Warnings    { get; } = [];

        #endregion

        #region TypeName / RCRValue

        /// <summary>What kind of snapshot this is, in words.</summary>
        public String TypeName
            => BSMFormat.SnapshotTypeName(Type);

        /// <summary>The energy reading the meter signed.</summary>
        public Int64 RCRValue
            => RCR?.NumericValue ?? 0;

        #endregion


        #region MetaStartingWith(Prefix)

        /// <summary>
        /// The metadata field whose text starts with the given prefix.
        ///
        /// The charging station writes what it knows about itself — the contract,
        /// the EVSE, its own software version — into the meter's three free-text
        /// fields, so that the meter signs it too. Which of the three is used is
        /// not fixed, so they are searched rather than indexed.
        /// </summary>
        /// <param name="Prefix">A prefix, e.g. "evse-id:".</param>
        public String? MetaStartingWith(String Prefix)
        {

            var matches = AdditionalValues.
                              Where (value => value.Name?.StartsWith("Meta", StringComparison.Ordinal) == true &&
                                              value.TextValue?.StartsWith(Prefix, StringComparison.Ordinal) == true).
                              ToArray();

            // Exactly one, because two fields making the same claim is not a
            // stronger claim — it is an ambiguity, and reading the first would
            // hide it.
            return matches.Length == 1
                       ? matches[0].TextValue
                       : null;

        }

        #endregion

        #region ToMeasurementValue()

        /// <summary>
        /// This snapshot as a signed reading of a charge transparency record.
        /// </summary>
        public BSMMeasurementValue ToMeasurementValue()
        {

            var measurementValue = new BSMMeasurementValue(
                                       Time,
                                       Value ?? 0,
                                       Type,
                                       Signatures:             SignatureOf(Signature),
                                       ValueDisplayPrefix:     BSMFormat.DisplayPrefixOf(DisplayPrefix ?? "kilo"),
                                       ValueDisplayPrecision:  (UInt16) (DisplayPrecision ?? 2)
                                   ) {

                                       RCR            = RCR?.     NumericValue ?? 0,
                                       RCRScale       = RCR?.     Scale        ?? 0,
                                       RCRUnit        = BSMFormat.UnitNameOf(RCR?.Unit),

                                       TotWhImp       = TotWhImp?.NumericValue ?? 0,
                                       TotWhImpScale  = TotWhImp?.Scale        ?? 0,
                                       TotWhImpUnit   = BSMFormat.UnitNameOf(TotWhImp?.Unit),

                                       W              = W?.       NumericValue ?? 0,
                                       WScale         = W?.       Scale        ?? 0,
                                       WUnit          = BSMFormat.UnitNameOf(W?.Unit),

                                       MA1            = MA1   ?? "",
                                       RCnt           = RCnt,
                                       OS             = OS,
                                       Epoch          = Epoch,
                                       TZO            = TZO,
                                       EpochSetCnt    = EpochSetCnt,
                                       EpochSetOS     = EpochSetOS,
                                       DI             = DI,
                                       DO             = DO,
                                       Meta1          = Meta1 ?? "",
                                       Meta2          = Meta2 ?? "",
                                       Meta3          = Meta3 ?? "",
                                       Evt            = Evt

                                   };

            foreach (var error   in Errors)    measurementValue.AddError  (error);
            foreach (var warning in Warnings)  measurementValue.AddWarning(warning);

            return measurementValue;

        }

        #endregion

        #region (static) Parse(JSON, SnapshotNumber)

        /// <summary>
        /// Read one signed snapshot.
        /// </summary>
        /// <param name="JSON">A signed meter value.</param>
        /// <param name="SnapshotNumber">Which snapshot of the session this is, for the error messages.</param>
        /// <exception cref="BSMValidationException">When the snapshot is not one.</exception>
        public static BSMSnapshot Parse(JObject  JSON,
                                        Int32    SnapshotNumber)
        {

            #region The quantities the meter signed

            if (JSON["additionalValues"] is not JArray additionalValuesArray)
                throw new BSMValidationException($"Missing or invalid additional values within signed meter value #{SnapshotNumber}!");

            var additionalValues = new List<BSMAdditionalValue>();

            foreach (var element in additionalValuesArray)
            {

                if (element is not JObject elementObject ||
                    BSMFormat.Object(elementObject, "measurand") is null)
                {
                    throw new BSMValidationException($"Invalid additional value #{additionalValues.Count + 1} within signed meter value #{SnapshotNumber}!");
                }

                additionalValues.Add(BSMAdditionalValue.Parse(elementObject));

            }

            BSMAdditionalValue? Named(String Name)
                => additionalValues.LastOrDefault(value => value.Name == Name);

            Int64 NumberNamed(String Name)
                => Named(Name)?.NumericValue ?? 0;

            #endregion

            #region ..., and the header the charging station wrote around them

            if (JSON["value"] is null)
                throw new BSMValidationException($"Missing value within signed meter value #{SnapshotNumber}!");

            var value            = BSMFormat.Object(JSON,  "value");
            var measuredValue    = BSMFormat.Object(value, "measuredValue");
            var displayedFormat  = BSMFormat.Object(value, "displayedFormat");

            #endregion

            return new BSMSnapshot {

                       Id                 = BSMFormat.Text  (JSON, "@id"),
                       Time               = BSMFormat.Text  (JSON, "time") ?? "",
                       MeasurementId      = BSMFormat.Number(JSON, "measurementId"),
                       Signature          = BSMFormat.Text  (JSON, "signature") ?? "",
                       MeterInfo          = BSMFormat.Object(JSON, "meterInfo"),
                       Contract           = BSMFormat.Object(JSON, "contract"),
                       ChargePoint        = BSMFormat.Object(JSON, "chargePoint"),

                       Measurand          = BSMFormat.Object(value, "measurand"),
                       MeasuredValue      = measuredValue,
                       Value              = BSMFormat.Number(measuredValue, "value"),
                       DisplayPrefix      = BSMFormat.Text  (displayedFormat, "prefix"),
                       DisplayPrecision   = (Int32?) BSMFormat.Number(displayedFormat, "precision"),

                       AdditionalValues   = additionalValues,

                       Type               = NumberNamed("Typ"),
                       RCR                = Named      ("RCR"),
                       TotWhImp           = Named      ("TotWhImp"),
                       W                  = Named      ("W"),
                       MA1                = Named      ("MA1")?.TextValue,
                       RCnt               = NumberNamed("RCnt"),
                       OS                 = NumberNamed("OS"),
                       Epoch              = NumberNamed("Epoch"),
                       TZO                = NumberNamed("TZO"),
                       EpochSetCnt        = NumberNamed("EpochSetCnt"),
                       EpochSetOS         = NumberNamed("EpochSetOS"),
                       DI                 = NumberNamed("DI"),
                       DO                 = NumberNamed("DO"),
                       Meta1              = Named      ("Meta1")?.TextValue,
                       Meta2              = Named      ("Meta2")?.TextValue,
                       Meta3              = Named      ("Meta3")?.TextValue,
                       Evt                = NumberNamed("Evt")

                   };

        }

        #endregion

        #region (private, static) SignatureOf(SignatureHEX)

        /// <summary>
        /// Take the DER encoded signature of a snapshot apart into its two
        /// integers.
        ///
        /// A signature that will not decode does not stop the record from being
        /// built: the snapshot travels on with an unusable signature and is
        /// reported as invalid when it is checked, which tells an EV driver more
        /// than refusing to read the file at all.
        /// </summary>
        /// <param name="SignatureHEX">A DER encoded ECDSA signature, hexadecimal.</param>
        private static IEnumerable<Signature> SignatureOf(String SignatureHEX)
        {

            try
            {

                var decoded = ECCurveVerifier.TryDecodeDERSignature(
                                  Convert.FromHexString(ChargyLib.CleanHex(SignatureHEX))
                              );

                if (decoded is not null)
                    return [ new SignatureRS(decoded.Value.R, decoded.Value.S, Value: SignatureHEX) ];

            }
            catch (Exception)
            { }

            return [ new SignatureRS("-", "-", Value: SignatureHEX) ];

        }

        #endregion

    }


    /// <summary>
    /// Everything a sequence of BSM snapshots has to agree about, taken from the
    /// first of them.
    /// </summary>
    public class BSMCommonValues
    {

        #region Properties

        /// <summary>The JSON-LD context every snapshot declares.</summary>
        public String   Context                     { get; init; } = "";

        /// <summary>The firmware version of the meter.</summary>
        public String   MeterFirmwareVersion        { get; init; } = "";

        /// <summary>The public key of the meter.</summary>
        public String   MeterPublicKey              { get; init; } = "";

        /// <summary>The identification of the meter.</summary>
        public String   MeterId                     { get; init; } = "";

        /// <summary>The manufacturer of the meter.</summary>
        public String   MeterManufacturer           { get; init; } = "";

        /// <summary>The type of the meter.</summary>
        public String   MeterType                   { get; init; } = "";

        /// <summary>The contract the charging session was authorized with.</summary>
        public String   ContractId                  { get; init; } = "";

        /// <summary>The kind of contract, e.g. "rfid".</summary>
        public String?  ContractType                { get; init; }

        /// <summary>The OBIS code of the energy reading.</summary>
        public String   MeasurandId                 { get; init; } = "";

        /// <summary>The name of the energy reading.</summary>
        public String   MeasurandName               { get; init; } = "";

        /// <summary>The power of ten the readings are scaled by.</summary>
        public Int32?   Scale                       { get; init; }

        /// <summary>The unit of the readings.</summary>
        public String   Unit                        { get; init; } = "";

        /// <summary>The unit as a DLMS/COSEM code.</summary>
        public Int32?   UnitEncoded                 { get; init; }

        /// <summary>How the readings are encoded.</summary>
        public String?  ValueType                   { get; init; }

        /// <summary>An optional display scaling.</summary>
        public String   DisplayPrefix               { get; init; } = "";

        /// <summary>An optional number of decimal places to display.</summary>
        public Int32?   DisplayPrecision            { get; init; }

        /// <summary>The software version of the charging station, as the header states it.</summary>
        public String?  ChargePointSoftwareVersion  { get; init; }

        /// <summary>The meter identification the meter itself signed; filled while walking the snapshots.</summary>
        public String?  MA1            { get; set; }

        /// <summary>How often the clock was set; filled while walking the snapshots.</summary>
        public Int64    EpochSetCnt    { get; set; } = -1;

        /// <summary>When the clock was last set; filled while walking the snapshots.</summary>
        public Int64    EpochSetOS     { get; set; } = -1;

        #endregion

        #region (static) From(FirstMeterValue, I18N, Errors)

        /// <summary>
        /// Take from the first snapshot everything the others have to agree about.
        ///
        /// A missing mandatory field is recorded as an error rather than thrown:
        /// the caller counts them, and the count is what tells an EV driver
        /// whether this is a broken BSM record or not a BSM record at all.
        /// </summary>
        /// <param name="FirstMeterValue">The first signed meter value.</param>
        /// <param name="I18N">The dictionary used to describe what went wrong.</param>
        /// <param name="Errors">Where to record what is missing.</param>
        public static BSMCommonValues From(JObject         FirstMeterValue,
                                           I18NDictionary  I18N,
                                           List<Error>     Errors)
        {

            void Missing(String MessageKey)
                => Errors.Add(new Error(I18N.GetMultilanguageTextWithParameter(MessageKey, 1)));

            if (BSMFormat.Text(FirstMeterValue, "@context") is null)
                Missing("MissingOrInvalid_SignedMeterValue_JSONContextP");

            #region The meter

            var meterInfo = BSMFormat.Object(FirstMeterValue, "meterInfo");

            if (meterInfo is null)
                Missing("MissingOrInvalid_SignedMeterValue_MeterInfoP");

            else
            {
                if (BSMFormat.Text(meterInfo, "firmwareVersion") is null)  Missing("MissingOrInvalid_SignedMeterValue_MeterInfo_FirmwareVersionP");
                if (BSMFormat.Text(meterInfo, "publicKey")       is null)  Missing("MissingOrInvalid_SignedMeterValue_MeterInfo_PublicKeyP");
                if (BSMFormat.Text(meterInfo, "meterId")         is null)  Missing("MissingOrInvalid_SignedMeterValue_MeterInfo_MeterIdP");
                if (BSMFormat.Text(meterInfo, "manufacturer")    is null)  Missing("MissingOrInvalid_SignedMeterValue_MeterInfo_ManufacturerP");
                if (BSMFormat.Text(meterInfo, "type")            is null)  Missing("MissingOrInvalid_SignedMeterValue_MeterInfo_TypeP");
            }

            #endregion

            #region The contract

            var contract = BSMFormat.Object(FirstMeterValue, "contract");

            if (contract is null)
                Missing("MissingOrInvalid_SignedMeterValue_ContractP");

            else if (BSMFormat.Text(contract, "id") is null)
                Missing("MissingOrInvalid_SignedMeterValue_Contract_IdP");

            #endregion

            #region What was measured

            var value          = BSMFormat.Object(FirstMeterValue, "value");
            var measurand      = BSMFormat.Object(value, "measurand");
            var measuredValue  = BSMFormat.Object(value, "measuredValue");

            if (value is null)
                Missing("MissingOrInvalid_SignedMeterValue_ValueP");

            else
            {

                if (measurand is null)
                    Missing("MissingOrInvalid_SignedMeterValue_MeasurandP");

                else
                {
                    if (BSMFormat.Text(measurand, "id")   is null)  Missing("MissingOrInvalid_Measurand_IdentificationP");
                    if (BSMFormat.Text(measurand, "name") is null)  Missing("MissingOrInvalid_Measurand_NameP");
                }

                if (measuredValue is null)
                    Missing("MissingOrInvalid_SignedMeterValue_MeasuredValueP");

            }

            #endregion

            var displayedFormat = BSMFormat.Object(value, "displayedFormat");
            var chargePoint     = BSMFormat.Object(FirstMeterValue, "chargePoint");

            return new BSMCommonValues {

                       Context                     = BSMFormat.Text(FirstMeterValue, "@context")        ?? "",

                       MeterFirmwareVersion        = BSMFormat.Text(meterInfo,       "firmwareVersion")  ?? "",
                       MeterPublicKey              = BSMFormat.Text(meterInfo,       "publicKey")        ?? "",
                       MeterId                     = BSMFormat.Text(meterInfo,       "meterId")          ?? "",
                       MeterManufacturer           = BSMFormat.Text(meterInfo,       "manufacturer")     ?? "",
                       MeterType                   = BSMFormat.Text(meterInfo,       "type")             ?? "",

                       ContractId                  = BSMFormat.Text(contract,        "id")               ?? "",
                       ContractType                = BSMFormat.Text(contract,        "type"),

                       MeasurandId                 = BSMFormat.Text(measurand,       "id")               ?? "",
                       MeasurandName               = BSMFormat.Text(measurand,       "name")             ?? "",

                       Scale                       = (Int32?) BSMFormat.Number(measuredValue,   "scale"),
                       Unit                        = BSMFormat.Text  (measuredValue,   "unit")           ?? "",
                       UnitEncoded                 = (Int32?) BSMFormat.Number(measuredValue,   "unitEncoded"),
                       ValueType                   = BSMFormat.Text  (measuredValue,   "valueType"),

                       DisplayPrefix               = BSMFormat.Text  (displayedFormat, "prefix")         ?? "",
                       DisplayPrecision            = (Int32?) BSMFormat.Number(displayedFormat, "precision"),

                       ChargePointSoftwareVersion  = BSMFormat.Text  (chargePoint,     "softwareVersion")

                   };

        }

        #endregion

    }

}
