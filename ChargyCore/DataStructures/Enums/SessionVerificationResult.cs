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
    /// The result of verifying an entire charging session.
    ///
    /// Note: The member names are the wire format. They appear verbatim in the
    /// golden verification reports shared with ChargyCore.TS, so renaming one
    /// silently breaks the parity contract between both implementations.
    /// </summary>
    public enum SessionVerificationResult
    {

        /// <summary>The charging session has not been verified yet.</summary>
        Unvalidated,

        /// <summary>The format of the charge transparency record is unknown.</summary>
        UnknownCTRFormat,

        /// <summary>The given data contained no charge transparency records.</summary>
        NoChargeTransparencyRecordsFound,

        /// <summary>The format of the charging session is unknown.</summary>
        UnknownSessionFormat,

        /// <summary>The format of the charging session is invalid.</summary>
        InvalidSessionFormat,

        /// <summary>A charging session needs at least a start and a stop measurement.</summary>
        AtLeastTwoMeasurementsRequired,

        /// <summary>The timestamps of the measurements are not in ascending order.</summary>
        InconsistentTimestamps,

        /// <summary>The charging session has no start measurement.</summary>
        MissingStartValue,

        /// <summary>The start measurement of the charging session is invalid.</summary>
        InvalidStartValue,

        /// <summary>An intermediate measurement of the charging session is invalid.</summary>
        InvalidIntermediateValue,

        /// <summary>The charging session has no stop measurement.</summary>
        MissingStopValue,

        /// <summary>The stop measurement of the charging session is invalid.</summary>
        InvalidStopValue,

        /// <summary>The energy meter of the charging session could not be found.</summary>
        EnergyMeterNotFound,

        /// <summary>A measurement of the charging session is invalid.</summary>
        InvalidMeasurement,

        /// <summary>A measurement of the charging session is implausible.</summary>
        InplausibleMeasurement,

        /// <summary>The public key of the energy meter could not be found.</summary>
        PublicKeyNotFound,

        /// <summary>The format of the public key is unknown.</summary>
        UnknownPublicKeyFormat,

        /// <summary>The public key is invalid.</summary>
        InvalidPublicKey,

        /// <summary>The format of the signature is unknown.</summary>
        UnknownSignatureFormat,

        /// <summary>The signature is invalid.</summary>
        InvalidSignature,

        /// <summary>The signature is valid.</summary>
        ValidSignature

    }


    /// <summary>
    /// Extension methods for charging session verification results.
    /// </summary>
    public static class SessionVerificationResultExtensions
    {

        #region Parse       (Text)

        /// <summary>
        /// Parse the given text as a charging session verification result.
        /// </summary>
        /// <param name="Text">A text representation of a charging session verification result.</param>
        public static SessionVerificationResult Parse(String Text)

            => TryParse(Text, out var result)
                   ? result
                   : SessionVerificationResult.Unvalidated;

        #endregion

        #region TryParse    (Text)

        /// <summary>
        /// Try to parse the given text as a charging session verification result.
        /// </summary>
        /// <param name="Text">A text representation of a charging session verification result.</param>
        public static SessionVerificationResult? TryParse(String Text)

            => TryParse(Text, out var result)
                   ? result
                   : null;

        #endregion

        #region TryParse    (Text, out SessionVerificationResult)

        /// <summary>
        /// Try to parse the given text as a charging session verification result.
        /// </summary>
        /// <param name="Text">A text representation of a charging session verification result.</param>
        /// <param name="SessionVerificationResult">The parsed charging session verification result.</param>
        public static Boolean TryParse(String Text, out SessionVerificationResult SessionVerificationResult)

            // Case-sensitive on purpose: the wire format is exact, and silently
            // accepting "validsignature" would hide a malformed record.
            => Enum.TryParse(Text.Trim(), out SessionVerificationResult);

        #endregion

        #region AsText      (this SessionVerificationResult)

        /// <summary>
        /// The wire representation of the given charging session verification result.
        /// </summary>
        /// <param name="SessionVerificationResult">A charging session verification result.</param>
        public static String AsText(this SessionVerificationResult SessionVerificationResult)

            // The member names are the wire format, so this cannot drift apart.
            // SessionVerificationResultTests pins the complete set of strings.
            => SessionVerificationResult.ToString();

        #endregion

    }

}
