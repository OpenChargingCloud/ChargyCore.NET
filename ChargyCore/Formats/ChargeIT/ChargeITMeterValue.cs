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

#endregion

namespace cloud.charging.open.chargy.Formats.ChargeIT
{

    /// <summary>
    /// One signed meter value of the format chargeIT defined before OCMF existed.
    ///
    /// Every reading repeats everything: which meter took it, that meter's public
    /// key and firmware, which contract authorized the session, and what software
    /// the charging station was running. That is redundant on purpose — a reading
    /// that arrives on its own still says who vouched for it — and it is why the
    /// container checks that the copies agree.
    ///
    /// The timestamps are the delicate part. Each reading carries a UTC instant
    /// *and* the meter's local offset separately, and the EMH meter signs the local
    /// reading. Keeping both is therefore not duplication: dropping the offset
    /// would make every signature fail for half the year.
    /// </summary>
    public class ChargeITMeterValue
    {

        #region Properties

        /// <summary>The identification of the charging session.</summary>
        public required String   TransactionId              { get; init; }

        /// <summary>The identification of this reading, which is also its pagination counter.</summary>
        public required String   MeasurementId              { get; init; }

        /// <summary>The identification of the energy meter.</summary>
        public required String   MeterId                    { get; init; }

        /// <summary>The type of the energy meter.</summary>
        public required String   MeterType                  { get; init; }

        /// <summary>The manufacturer of the energy meter.</summary>
        public required String   Manufacturer               { get; init; }

        /// <summary>The firmware version of the energy meter.</summary>
        public required String   FirmwareVersion            { get; init; }

        /// <summary>The public key of the energy meter.</summary>
        public required String   PublicKey                  { get; init; }

        /// <summary>The contract the charging session was authorized with.</summary>
        public required String   ContractId                 { get; init; }

        /// <summary>The kind of contract, e.g. "RFID_TAG_ID".</summary>
        public required String   ContractType               { get; init; }

        /// <summary>When the driver authorized, in the meter's own local time.</summary>
        public required String   ContractTimestamp          { get; init; }

        /// <summary>When the reading was taken, in seconds since the UNIX epoch.</summary>
        public required Int64    MeasuredAt                 { get; init; }

        /// <summary>When the reading was taken, in the meter's own local time.</summary>
        public required String   Timestamp                  { get; init; }

        /// <summary>The reading itself.</summary>
        public required Decimal  Value                      { get; init; }

        /// <summary>The unit of the reading, e.g. "WATT_HOUR".</summary>
        public required String   Unit                       { get; init; }

        /// <summary>The unit as a DLMS/COSEM code.</summary>
        public required UInt16   UnitEncoded                { get; init; }

        /// <summary>The power of ten the reading is scaled by.</summary>
        public required Int32    Scale                      { get; init; }

        /// <summary>How the reading is encoded, e.g. "Integer64".</summary>
        public required String   ValueType                  { get; init; }

        /// <summary>The OBIS code of what was measured.</summary>
        public required String   MeasurandId                { get; init; }

        /// <summary>The name of what was measured, e.g. "ENERGY_TOTAL".</summary>
        public required String   MeasurandName              { get; init; }

        /// <summary>The status word of the meter.</summary>
        public required String   Status                     { get; init; }

        /// <summary>How many seconds the meter has been in operation.</summary>
        public required Int64    SecondsIndex               { get; init; }

        /// <summary>The log book index of the meter.</summary>
        public required String   LogBookIndex               { get; init; }

        /// <summary>The signature over the reading.</summary>
        public required String   Signature                  { get; init; }

        /// <summary>The software the charging station was running, when it said.</summary>
        public          String?  ChargePointSoftwareVersion { get; init; }

        #endregion


        #region ToMeasurementValue()

        /// <summary>
        /// This reading as a signed reading of a charge transparency record.
        /// </summary>
        public MeasurementValue ToMeasurementValue()

            => new (
                   Timestamp,
                   Value,
                   Signatures:    [
                                      // The meter concatenates r and s, each 24
                                      // bytes on secp192r1, and splitting at 48
                                      // hexadecimal digits is what takes them apart.
                                      new SignatureRS(
                                          Signature[..48],
                                          Signature[48..],
                                          Value: Signature
                                      )
                                  ],
                   StatusMeter:   Status,
                   SecondsIndex:  SecondsIndex,
                   PaginationId:  MeasurementId,
                   LogBookIndex:  LogBookIndex
               );

        #endregion

        #region (static) TryParse(JSON, out MeterValue)

        /// <summary>
        /// Try to read one signed meter value.
        ///
        /// Every field is mandatory, and a reading missing one is skipped rather
        /// than filled in with a default: the fields are what the meter signed, and
        /// a guessed value would produce an invalid signature that looks like
        /// tampering rather than like a missing field.
        /// </summary>
        /// <param name="JSON">A signed meter value.</param>
        /// <param name="MeterValue">The parsed meter value.</param>
        public static Boolean TryParse(JObject JSON, out ChargeITMeterValue? MeterValue)
        {

            MeterValue = null;

            var meterInfo               = JSON["meterInfo"]      as JObject;
            var contract                = JSON["contract"]       as JObject;
            var measuredValue           = JSON["measuredValue"]  as JObject;
            var measurand               = JSON["measurand"]      as JObject;
            var additionalInfo          = JSON["additionalInfo"] as JObject;
            var indexes                 = additionalInfo?["indexes"] as JObject;
            var chargePoint             = JSON["chargePoint"]    as JObject;

            var contractTimestampLocal  = contract?     ["timestampLocal"] as JObject;
            var measuredTimestampLocal  = measuredValue?["timestampLocal"] as JObject;

            var measurementId           = Text  (JSON, "measurementId");
            var transactionId           = Text  (JSON, "transactionId");
            var signature               = Text  (JSON, "signature");

            var meterId                 = Text  (meterInfo, "meterId");
            var meterType               = Text  (meterInfo, "type");
            var firmwareVersion         = Text  (meterInfo, "firmwareVersion");
            var publicKey               = Text  (meterInfo, "publicKey");
            var manufacturer            = Text  (meterInfo, "manufacturer");

            var contractId              = Text  (contract, "id");
            var contractType            = Text  (contract, "type");

            var value                   = Text  (measuredValue, "value");
            var unit                    = Text  (measuredValue, "unit");
            var scale                   = Number(measuredValue, "scale");
            var valueType               = Text  (measuredValue, "valueType");
            var unitEncoded             = Number(measuredValue, "unitEncoded");

            var measurandId             = Text  (measurand, "id");
            var measurandName           = Text  (measurand, "name");

            var status                  = Text  (additionalInfo, "status");
            var timer                   = Number(indexes, "timer");
            var logBook                 = Text  (indexes, "logBook");

            if (measurementId  is null || transactionId is null || signature   is null ||
                meterId        is null || meterType     is null || firmwareVersion is null ||
                publicKey      is null || manufacturer  is null ||
                contractId     is null || contractType  is null ||
                value          is null || unit          is null || valueType   is null ||
                measurandId    is null || measurandName is null ||
                status         is null || logBook       is null ||
                !scale.      HasValue  || !unitEncoded.HasValue || !timer.HasValue ||
                contractTimestampLocal is null || measuredTimestampLocal is null)
            {
                return false;
            }

            if (!Decimal.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var measuredNumber))
                return false;

            var measuredAt = (Int64?) Number(measuredTimestampLocal, "timestamp");

            if (!measuredAt.HasValue)
                return false;

            MeterValue = new ChargeITMeterValue {

                             TransactionId               = transactionId,
                             MeasurementId               = measurementId,

                             MeterId                     = meterId,
                             MeterType                   = meterType,
                             Manufacturer                = manufacturer,
                             FirmwareVersion             = firmwareVersion,
                             PublicKey                   = publicKey,

                             ContractId                  = contractId,
                             ContractType                = contractType,
                             ContractTimestamp           = LocalTimestampOf(contractTimestampLocal) ?? "",

                             MeasuredAt                  = measuredAt.Value,
                             Timestamp                   = LocalTimestampOf(measuredTimestampLocal) ?? "",

                             Value                       = measuredNumber,
                             Unit                        = unit,
                             UnitEncoded                 = (UInt16) unitEncoded.Value,
                             Scale                       = (Int32)  scale.Value,
                             ValueType                   = valueType,

                             MeasurandId                 = measurandId,
                             MeasurandName               = measurandName,

                             Status                      = status,
                             SecondsIndex                = (Int64) timer.Value,
                             LogBookIndex                = logBook,
                             Signature                   = signature,

                             ChargePointSoftwareVersion  = Text(chargePoint, "softwareVersion")

                         };

            return true;

        }

        #endregion

        #region (private, static) LocalTimestampOf(JSON)

        /// <summary>
        /// A moment as the meter's own clock read it.
        ///
        /// The meter reports the UTC instant and its two offsets — the standard one
        /// and the summer time one — separately, and it signs the sum. So the
        /// offsets are kept in the timestamp rather than folded away: a reading
        /// written as UTC would no longer say what the meter signed.
        /// </summary>
        /// <param name="JSON">A "timestampLocal" object.</param>
        private static String? LocalTimestampOf(JObject JSON)
        {

            var timestamp     = (Int64?) Number(JSON, "timestamp");
            var localOffset   = (Int32?) Number(JSON, "localOffset");
            var seasonOffset  = (Int32?) Number(JSON, "seasonOffset");

            if (!timestamp.HasValue || !localOffset.HasValue || !seasonOffset.HasValue)
                return null;

            var offset = TimeSpan.FromMinutes(localOffset.Value + seasonOffset.Value);

            return DateTimeOffset.FromUnixTimeSeconds(timestamp.Value).
                                  ToOffset(offset).
                                  ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

        }

        #endregion

        #region (private, static) JSON helpers

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
