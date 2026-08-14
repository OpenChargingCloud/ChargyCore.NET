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

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// How severe a warning or an error is.
    ///
    /// ChargyCore.TS declares two structurally identical enums, "WarningLevel"
    /// and "ErrorLevel". They carry the same three values and are never mixed,
    /// so this port models them as one type.
    ///
    /// Note: The wire format is lower case and appears verbatim in the golden
    /// verification reports, e.g. "warning 1: low: ...".
    /// </summary>
    public enum SeverityLevel
    {

        /// <summary>Low severity.</summary>
        Low,

        /// <summary>Medium severity.</summary>
        Medium,

        /// <summary>High severity.</summary>
        High

    }


    /// <summary>
    /// Extension methods for severity levels.
    /// </summary>
    public static class SeverityLevelExtensions
    {

        #region Parse       (Text)

        /// <summary>
        /// Parse the given text as a severity level.
        /// </summary>
        /// <param name="Text">A text representation of a severity level.</param>
        public static SeverityLevel Parse(String Text)

            => TryParse(Text, out var level)
                   ? level
                   : SeverityLevel.Low;

        #endregion

        #region TryParse    (Text)

        /// <summary>
        /// Try to parse the given text as a severity level.
        /// </summary>
        /// <param name="Text">A text representation of a severity level.</param>
        public static SeverityLevel? TryParse(String Text)

            => TryParse(Text, out var level)
                   ? level
                   : null;

        #endregion

        #region TryParse    (Text, out SeverityLevel)

        /// <summary>
        /// Try to parse the given text as a severity level.
        /// </summary>
        /// <param name="Text">A text representation of a severity level.</param>
        /// <param name="SeverityLevel">The parsed severity level.</param>
        public static Boolean TryParse(String Text, out SeverityLevel SeverityLevel)
        {

            switch (Text.Trim().ToLowerInvariant())
            {

                case "low":
                    SeverityLevel = SeverityLevel.Low;
                    return true;

                case "medium":
                    SeverityLevel = SeverityLevel.Medium;
                    return true;

                case "high":
                    SeverityLevel = SeverityLevel.High;
                    return true;

                default:
                    SeverityLevel = SeverityLevel.Low;
                    return false;

            }

        }

        #endregion

        #region AsText      (this SeverityLevel)

        /// <summary>
        /// The wire representation of the given severity level.
        /// </summary>
        /// <param name="SeverityLevel">A severity level.</param>
        public static String AsText(this SeverityLevel SeverityLevel)

            => SeverityLevel switch {
                   SeverityLevel.High    => "high",
                   SeverityLevel.Medium  => "medium",
                   _                     => "low"
               };

        #endregion

    }

}
