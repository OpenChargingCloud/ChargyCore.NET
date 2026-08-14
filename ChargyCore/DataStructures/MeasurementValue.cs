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

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// The scaling of a measured value as it is presented to the user.
    /// </summary>
    public enum DisplayPrefix
    {

        /// <summary>No prefix.</summary>
        NULL,

        /// <summary>Kilo.</summary>
        KILO,

        /// <summary>Mega.</summary>
        MEGA,

        /// <summary>Giga.</summary>
        GIGA

    }


    /// <summary>
    /// A single signed reading of an energy meter: what it measured, when, and
    /// the signature that proves the reading was not tampered with.
    /// </summary>
    /// <param name="Timestamp">
    /// When the value was measured, as an ISO 8601 string.
    ///
    /// Note: This deliberately keeps the textual form the parser produced instead
    /// of a DateTimeOffset. Each charge transparency data format normalizes its
    /// timestamps slightly differently — OCMF emits "…+00:00" where Alfen emits
    /// "…Z" — and the verification reports shared with ChargyCore.TS print this
    /// string verbatim. Re-formatting a parsed timestamp would silently change it.
    /// Use <see cref="ParsedTimestamp"/> to compute with it.
    /// </param>
    /// <param name="Value">The measured value, scaled by the scale factor of the measurement.</param>
    /// <param name="Signatures">The signatures over this measurement value.</param>
    /// <param name="ValueDisplayPrefix">An optional display scaling.</param>
    /// <param name="ValueDisplayPrecision">An optional number of decimal places to display.</param>
    /// <param name="StatusMeter">An optional status word of the energy meter.</param>
    /// <param name="SecondsIndex">An optional seconds index of the energy meter.</param>
    /// <param name="PaginationId">An optional pagination counter of the energy meter.</param>
    /// <param name="LogBookIndex">An optional log book index of the energy meter.</param>
    /// <param name="StatusAdapter">An optional status word of the meter adapter.</param>
    public class MeasurementValue(String                   Timestamp,
                                  Decimal                  Value,
                                  IEnumerable<Signature>?  Signatures            = null,
                                  DisplayPrefix?           ValueDisplayPrefix    = null,
                                  UInt16?                  ValueDisplayPrecision = null,
                                  String?                  StatusMeter           = null,
                                  Int64?                   SecondsIndex          = null,
                                  String?                  PaginationId          = null,
                                  String?                  LogBookIndex          = null,
                                  String?                  StatusAdapter         = null)
    {

        #region Data

        private readonly List<Error>    errors    = [];
        private readonly List<Warning>  warnings  = [];

        #endregion

        #region Properties

        /// <summary>When the value was measured, as an ISO 8601 string.</summary>
        public String                    Timestamp                { get; } = Timestamp;

        /// <summary>The measured value.</summary>
        public Decimal                   Value                    { get; } = Value;

        /// <summary>The signatures over this measurement value.</summary>
        public IReadOnlyList<Signature>  Signatures               { get; } = Signatures?.ToArray() ?? [];

        /// <summary>An optional display scaling.</summary>
        public DisplayPrefix?            ValueDisplayPrefix       { get; } = ValueDisplayPrefix;

        /// <summary>An optional number of decimal places to display.</summary>
        public UInt16?                   ValueDisplayPrecision    { get; } = ValueDisplayPrecision;

        /// <summary>An optional status word of the energy meter.</summary>
        public String?                   StatusMeter              { get; } = StatusMeter;

        /// <summary>An optional seconds index of the energy meter.</summary>
        public Int64?                    SecondsIndex             { get; } = SecondsIndex;

        /// <summary>An optional pagination counter of the energy meter.</summary>
        public String?                   PaginationId             { get; } = PaginationId;

        /// <summary>An optional log book index of the energy meter.</summary>
        public String?                   LogBookIndex             { get; } = LogBookIndex;

        /// <summary>An optional status word of the meter adapter.</summary>
        public String?                   StatusAdapter            { get; } = StatusAdapter;

        /// <summary>
        /// The measurement this value belongs to. Set while a charge transparency
        /// record is being assembled.
        /// </summary>
        public Measurement?              Measurement              { get; internal set; }

        /// <summary>
        /// The preceding measurement value of the same measurement, for the hash
        /// chained data formats. Set while a charge transparency record is being
        /// assembled.
        /// </summary>
        public MeasurementValue?         PreviousValue            { get; internal set; }

        /// <summary>The result of verifying this measurement value.</summary>
        public CryptoResult?             Result                   { get; set; }

        /// <summary>Everything that made the verification of this value fail.</summary>
        public IReadOnlyList<Error>      Errors
            => errors;

        /// <summary>Everything that looked suspicious about this value.</summary>
        public IReadOnlyList<Warning>    Warnings
            => warnings;

        #endregion


        #region ParsedTimestamp

        /// <summary>
        /// <see cref="Timestamp"/> parsed into a point in time.
        /// </summary>
        /// <exception cref="FormatException">When the timestamp is not a valid ISO 8601 timestamp.</exception>
        public DateTimeOffset ParsedTimestamp

            => ChargyLib.ParseUTC(Timestamp);

        #endregion

        #region AddError    (Error)

        /// <summary>
        /// Add an error to this measurement value.
        /// </summary>
        /// <param name="Error">An error.</param>
        public MeasurementValue AddError(Error Error)
        {
            errors.Add(Error);
            return this;
        }

        #endregion

        #region AddWarning  (Warning)

        /// <summary>
        /// Add a warning to this measurement value.
        /// </summary>
        /// <param name="Warning">A warning.</param>
        public MeasurementValue AddWarning(Warning Warning)
        {
            warnings.Add(Warning);
            return this;
        }

        #endregion


        #region (static) TryParse(JSON, out MeasurementValue)

        /// <summary>
        /// Try to parse the given JSON as a measurement value.
        /// </summary>
        /// <param name="JSON">A JSON representation of a measurement value.</param>
        /// <param name="MeasurementValue">The parsed measurement value.</param>
        public static Boolean TryParse(JObject JSON, out MeasurementValue? MeasurementValue)
        {

            MeasurementValue = null;

            var timestamp = JSON["timestamp"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(timestamp))
                return false;

            if (!TryParseValue(JSON["value"], out var value))
                return false;

            var signatures = new List<Signature>();

            if (JSON["signatures"] is JArray signatureArray)
                foreach (var signatureJSON in signatureArray.OfType<JObject>())
                    if (Signature.TryParse(signatureJSON, out var signature))
                        signatures.Add(signature!);

            MeasurementValue = new MeasurementValue(
                                   timestamp,
                                   value,
                                   signatures,
                                   Enum.TryParse<DisplayPrefix>(JSON["value_displayPrefix"]?.Value<String>(), out var prefix)
                                       ? prefix
                                       : null,
                                   JSON["value_displayPrecision"]?.Value<UInt16>(),
                                   JSON["statusMeter"]?.  Value<String>(),
                                   JSON["secondsIndex"]?. Value<Int64>(),
                                   // Some formats number their pages, others label them.
                                   JSON["paginationId"]?. ToString(),
                                   JSON["logBookIndex"]?. Value<String>(),
                                   JSON["statusAdapter"]?.Value<String>()
                               );

            return true;

        }

        #endregion

        #region (private) TryParseValue(JSON, out Value)

        /// <summary>
        /// A measured value, which charge transparency records carry as a JSON
        /// number as well as as a string.
        /// </summary>
        private static Boolean TryParseValue(JToken? JSON, out Decimal Value)
        {

            Value = 0;

            if (JSON is null)
                return false;

            if (JSON.Type == JTokenType.Integer ||
                JSON.Type == JTokenType.Float)
            {
                Value = JSON.Value<Decimal>();
                return true;
            }

            return JSON.Type == JTokenType.String &&
                   Decimal.TryParse(
                       JSON.Value<String>(),
                       NumberStyles.Number,
                       CultureInfo.InvariantCulture,
                       out Value
                   );

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this measurement value.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("timestamp",               Timestamp),
                           new JProperty("value",                   Value)
                       );

            if (ValueDisplayPrefix.HasValue)
                json.Add(new JProperty("value_displayPrefix",       ValueDisplayPrefix.Value.ToString()));

            if (ValueDisplayPrecision.HasValue)
                json.Add(new JProperty("value_displayPrecision",    ValueDisplayPrecision.Value));

            if (StatusMeter   is not null)
                json.Add(new JProperty("statusMeter",               StatusMeter));

            if (SecondsIndex.HasValue)
                json.Add(new JProperty("secondsIndex",              SecondsIndex.Value));

            if (PaginationId  is not null)
                json.Add(new JProperty("paginationId",              PaginationId));

            if (LogBookIndex  is not null)
                json.Add(new JProperty("logBookIndex",              LogBookIndex));

            if (StatusAdapter is not null)
                json.Add(new JProperty("statusAdapter",             StatusAdapter));

            if (Signatures.Count > 0)
                json.Add(new JProperty("signatures",  new JArray(Signatures.Select(signature => signature.ToJSON()))));

            if (errors.  Count > 0)
                json.Add(new JProperty("errors",      new JArray(errors.  Select(error   => error.  ToJSON()))));

            if (warnings.Count > 0)
                json.Add(new JProperty("warnings",    new JArray(warnings.Select(warning => warning.ToJSON()))));

            if (Result is not null)
                json.Add(new JProperty("result",      Result.ToJSON()));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this measurement value.
        /// </summary>
        public override String ToString()

            => $"{Value} @ {Timestamp}";

        #endregion


    }

}
