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

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.Formats.PCDF
{

    /// <summary>
    /// Reports what the PCDF signature concluded about a charging session.
    ///
    /// Nothing is verified here. A PCDF signature covers the whole document, and it
    /// was checked when the document was read — so the single reading inside it
    /// already carries that verdict.
    ///
    /// Unlike the meter formats this one does not insist on two readings. A PCDF
    /// document reports the energy delivered during the session directly rather
    /// than as the difference between two meter states, so one reading is the whole
    /// answer.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="MeterLookup">Unused: a PCDF reading carries its verdict already.</param>
    public class PCDFCrypt01(I18NDictionary               I18N,
                             Func<String, EnergyMeter?>?  MeterLookup = null)

        : ACrypt("PCDF", I18N, MeterLookup)

    {

        #region VerifyChargingSession(ChargingSession)

        /// <summary>
        /// Work out what the signature says about a whole charging session.
        /// </summary>
        /// <param name="ChargingSession">A charging session.</param>
        public override SessionCryptoResult VerifyChargingSession(ChargingSession ChargingSession)
        {

            var sessionResult  = SessionVerificationResult.ValidSignature;
            var valueCount     = 0;

            foreach (var measurement in ChargingSession.Measurements)
                foreach (var measurementValue in measurement.Values)
                {

                    valueCount++;

                    if (VerifyMeasurement(measurementValue).Status != VerificationResult.ValidSignature)
                        sessionResult = SessionVerificationResult.InvalidSignature;

                }

            // No readings at all is a malformed document, not a failed signature.
            if (valueCount == 0)
                sessionResult = SessionVerificationResult.InvalidSessionFormat;

            return new SessionCryptoResult(
                       sessionResult,
                       Certainty: 0.9
                   );

        }

        #endregion

        #region VerifyMeasurement    (MeasurementValue)

        /// <summary>
        /// Report what the PCDF signature concluded about the reading.
        /// </summary>
        /// <param name="MeasurementValue">A signed energy meter reading.</param>
        public override CryptoResult VerifyMeasurement(MeasurementValue MeasurementValue)
        {

            if (MeasurementValue is not PCDFMeasurementValue pcdfMeasurementValue)
            {
                var invalid = new CryptoResult(VerificationResult.InvalidMeasurement);
                MeasurementValue.Result = invalid;
                return invalid;
            }

            var result = new CryptoResult(pcdfMeasurementValue.Document.ValidationStatus);

            MeasurementValue.Result = result;

            return result;

        }

        #endregion


    }

}
