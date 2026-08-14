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
    /// The result of verifying a single energy meter measurement.
    ///
    /// Note: The member names are the wire format. They appear verbatim in the
    /// golden verification reports shared with ChargyCore.TS, so renaming one
    /// silently breaks the parity contract between both implementations.
    /// </summary>
    public enum VerificationResult
    {

        /// <summary>The measurement has not been verified yet.</summary>
        Unvalidated,

        /// <summary>Nothing had to be verified.</summary>
        NoOperation,

        /// <summary>The format of the charge transparency record is unknown.</summary>
        UnknownCTRFormat,

        /// <summary>The energy meter of the measurement could not be found.</summary>
        EnergyMeterNotFound,

        /// <summary>The measurement is invalid.</summary>
        InvalidMeasurement,

        /// <summary>The start measurement is invalid.</summary>
        InvalidStartValue,

        /// <summary>The measurement is the start of a charging session.</summary>
        StartValue,

        /// <summary>The start measurement is valid.</summary>
        ValidStartValue,

        /// <summary>An intermediate measurement is invalid.</summary>
        InvalidIntermediateValue,

        /// <summary>The measurement is an intermediate value of a charging session.</summary>
        IntermediateValue,

        /// <summary>The intermediate measurement is valid.</summary>
        ValidIntermediateValue,

        /// <summary>The stop measurement is invalid.</summary>
        InvalidStopValue,

        /// <summary>The measurement is the end of a charging session.</summary>
        StopValue,

        /// <summary>The stop measurement is valid.</summary>
        ValidStopValue,

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
        ValidSignature,

        /// <summary>The verification itself failed.</summary>
        ValidationError

    }


    /// <summary>
    /// Extension methods for measurement verification results.
    /// </summary>
    public static class VerificationResultExtensions
    {

        #region Parse       (Text)

        /// <summary>
        /// Parse the given text as a measurement verification result.
        /// </summary>
        /// <param name="Text">A text representation of a measurement verification result.</param>
        public static VerificationResult Parse(String Text)

            => TryParse(Text, out var result)
                   ? result
                   : VerificationResult.Unvalidated;

        #endregion

        #region TryParse    (Text)

        /// <summary>
        /// Try to parse the given text as a measurement verification result.
        /// </summary>
        /// <param name="Text">A text representation of a measurement verification result.</param>
        public static VerificationResult? TryParse(String Text)

            => TryParse(Text, out var result)
                   ? result
                   : null;

        #endregion

        #region TryParse    (Text, out VerificationResult)

        /// <summary>
        /// Try to parse the given text as a measurement verification result.
        /// </summary>
        /// <param name="Text">A text representation of a measurement verification result.</param>
        /// <param name="VerificationResult">The parsed measurement verification result.</param>
        public static Boolean TryParse(String Text, out VerificationResult VerificationResult)

            => Enum.TryParse(Text.Trim(), out VerificationResult);

        #endregion

        #region AsText      (this VerificationResult)

        /// <summary>
        /// The wire representation of the given measurement verification result.
        /// </summary>
        /// <param name="VerificationResult">A measurement verification result.</param>
        public static String AsText(this VerificationResult VerificationResult)

            // The member names are the wire format, so this cannot drift apart.
            // VerificationResultTests pins the complete set of strings.
            => VerificationResult.ToString();

        #endregion

    }

}
