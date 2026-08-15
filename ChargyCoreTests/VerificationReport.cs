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

using System.Text;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Renders what Chargy made of a file into a plain text report.
    ///
    /// This is the parity contract with ChargyCore.TS. The "*.expected.txt"
    /// fixtures are shared byte-for-byte between both implementations, and this
    /// formatter is a line-for-line port of "formatChargeDataVerificationReport"
    /// in tests/testHelper.ts. Its output is implementation-independent by
    /// construction: no type names, no formatting choices of the host platform,
    /// nothing that could differ between TypeScript and C# without also being a
    /// genuine difference in what the two implementations concluded about a
    /// charging session.
    ///
    /// Which means: when a line differs, one of the two is wrong about somebody's
    /// electricity bill. The expected files are never edited to make a test pass.
    /// </summary>
    public static class VerificationReport
    {

        #region Format(Result)

        /// <summary>
        /// Render what Chargy made of a file.
        /// </summary>
        /// <param name="Result">
        /// A <see cref="ChargeTransparencyRecord"/>, a
        /// <see cref="ChargeTransparencyLiveLink"/>, a <see cref="SimpleURL"/> or
        /// a <see cref="SessionCryptoResult"/>.
        /// </param>
        public static String Format(Object? Result)
        {

            if (Result is ChargeTransparencyLiveLink liveLink)
                return String.Join("\n", [
                           "format: charge-transparency-live-link",
                           $"timestamp: {liveLink.Timestamp ?? ""}",
                           $"transports: {liveLink.Transports.Count}"
                       ]);

            if (Result is SimpleURL url)
                return $"url: {url.URL}";

            if (Result is not ChargeTransparencyRecord record)
            {

                var sessionResult = Result as SessionCryptoResult;

                return String.Join("\n", [
                           "format: session-result",
                           $"status: {sessionResult?.Status.AsText() ?? ""}",
                           $"message: {FormatOptionalMultilanguageText(sessionResult?.Message)}"
                       ]);

            }

            var lines = new List<String> {
                            "format: ctr",
                            $"sessions: {record.ChargingSessions.Count}"
                        };

            #region Warnings, when there are any

            if (record.Warnings.Count > 0)
            {

                lines.Add($"warnings: {record.Warnings.Count}");

                for (var warningIndex = 0; warningIndex < record.Warnings.Count; warningIndex++)
                {

                    var warning = record.Warnings[warningIndex];

                    lines.Add($"warning {warningIndex + 1}: {warning.Level.AsText()}: {FormatMultilanguageText(warning.Message)}");

                }

            }

            #endregion

            #region The charging sessions

            for (var sessionIndex = 0; sessionIndex < record.ChargingSessions.Count; sessionIndex++)
            {

                var session       = record.ChargingSessions[sessionIndex];
                var sessionNumber = sessionIndex + 1;

                // A session may name its energy meter itself, or leave it to the
                // measurements to say which meter produced them.
                var meterId       = session.EnergyMeterId
                                        ?? (session.Measurements.Count > 0
                                                ? session.Measurements[0].EnergyMeterId
                                                : null)
                                        ?? "";

                lines.Add($"session {sessionNumber}: {session.Id}");
                lines.Add($"session {sessionNumber} evseId: {session.EVSEId ?? "unknown"}");
                lines.Add($"session {sessionNumber} meterId: {meterId}");
                lines.Add($"session {sessionNumber} status: {session.VerificationResult?.Status.AsText() ?? "unknown"}");
                lines.Add($"session {sessionNumber} measurements: {session.Measurements.Count}");

                for (var measurementIndex = 0; measurementIndex < session.Measurements.Count; measurementIndex++)
                    AppendMeasurement(
                        lines,
                        sessionNumber,
                        measurementIndex + 1,
                        session.Measurements[measurementIndex]
                    );

            }

            #endregion

            return String.Join("\n", lines);

        }

        #endregion


        #region (private) AppendMeasurement     (Lines, SessionNumber, MeasurementNumber, Measurement)

        /// <summary>
        /// Render one measurement of a charging session.
        /// </summary>
        private static void AppendMeasurement(List<String>  Lines,
                                              Int32         SessionNumber,
                                              Int32         MeasurementNumber,
                                              Measurement   Measurement)
        {

            var prefix = $"measurement {SessionNumber}.{MeasurementNumber}";

            // A measurement that reports several quantities at once has no single
            // name or OBIS code, and the report says so in the same word
            // JavaScript prints for an absent property.
            Lines.Add($"{prefix} name: {Measurement.Name ?? "undefined"}");
            Lines.Add($"{prefix} obis: {Measurement.OBIS ?? "undefined"}");
            Lines.Add($"{prefix} status: {Measurement.VerificationResult?.Status.AsText() ?? "unknown"}");
            Lines.Add($"{prefix} values: {Measurement.Values.Count}");

            for (var valueIndex = 0; valueIndex < Measurement.Values.Count; valueIndex++)
                AppendMeasurementValue(
                    Lines,
                    SessionNumber,
                    MeasurementNumber,
                    valueIndex + 1,
                    Measurement.Values[valueIndex]
                );

        }

        #endregion

        #region (private) AppendMeasurementValue(Lines, SessionNumber, MeasurementNumber, ValueNumber, Value)

        /// <summary>
        /// Render one reading of a measurement.
        /// </summary>
        private static void AppendMeasurementValue(List<String>      Lines,
                                                   Int32             SessionNumber,
                                                   Int32             MeasurementNumber,
                                                   Int32             ValueNumber,
                                                   MeasurementValue  Value)
        {

            var prefix = $"value {SessionNumber}.{MeasurementNumber}.{ValueNumber}";

            Lines.Add($"{prefix} timestamp: {Value.Timestamp}");
            Lines.Add($"{prefix} value: {FormatNumber(Value.Value)}");
            Lines.Add($"{prefix} signatures: {Value.Signatures.Count}");
            Lines.Add($"{prefix} status: {Value.Result?.Status.AsText() ?? "unknown"}");

        }

        #endregion

        #region (private) FormatNumber(Value)

        /// <summary>
        /// Render a meter reading the way JavaScript's Number.toString() does.
        ///
        /// The expected reports were produced by TypeScript, where 22675 prints
        /// as "22675" and never as "22675.0" or "22675,0". A decimal that carries
        /// its scale — as one parsed from "22675.00" would — has to be reduced to
        /// the same shortest form, and the culture of the test host must not get
        /// a say in the decimal separator.
        /// </summary>
        /// <param name="Value">A meter reading.</param>
        private static String FormatNumber(Decimal Value)
        {

            var text = Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (text.Contains('.'))
                text = text.TrimEnd('0').TrimEnd('.');

            return text.Length > 0
                       ? text
                       : "0";

        }

        #endregion

        #region (private) FormatMultilanguageText        (Text)

        /// <summary>
        /// Render a multi-language text, preferring English.
        ///
        /// The reports are compared across two implementations, so they need one
        /// agreed language rather than whichever the test host happens to run in.
        /// </summary>
        /// <param name="Text">A multi-language text.</param>
        private static String FormatMultilanguageText(I18NString Text)
        {

            var english = Text[Languages.en];

            if (english is not null && english.Length > 0)
                return english;

            foreach (var text in Text)
                return text.Text;

            return "";

        }

        #endregion

        #region (private) FormatOptionalMultilanguageText(Text)

        /// <summary>
        /// Render an optional multi-language text.
        /// </summary>
        /// <param name="Text">An optional multi-language text.</param>
        private static String FormatOptionalMultilanguageText(I18NString? Text)

            => Text is not null
                   ? FormatMultilanguageText(Text)
                   : "";

        #endregion


    }

}
