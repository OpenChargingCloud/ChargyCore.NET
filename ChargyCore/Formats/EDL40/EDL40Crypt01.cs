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

namespace cloud.charging.open.chargy.Formats.EDL40
{

    /// <summary>
    /// Reports what the EDL40 and ISA signatures concluded about a charging
    /// session.
    ///
    /// Nothing is verified here. An SML signature covers a whole document, and it
    /// was checked when the document was read — so every reading inside it already
    /// carries that one verdict, and re-deriving it would only create a second
    /// opportunity to get it wrong.
    ///
    /// Unlike the other formats this one does not insist on two readings: an ISA
    /// document carries a start and a stop reading in a single signed block, so
    /// one document can already be a complete charging session.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="MeterLookup">Unused: an EDL40 reading carries its verdict already.</param>
    public class EDL40Crypt01(I18NDictionary               I18N,
                              Func<String, EnergyMeter?>?  MeterLookup = null)

        : ACrypt("EDL40/ISA-EDL40", I18N, MeterLookup)

    {

        #region VerifyChargingSession(ChargingSession)

        /// <summary>
        /// Work out what the signatures say about a whole charging session.
        /// </summary>
        /// <param name="ChargingSession">A charging session.</param>
        public override SessionCryptoResult VerifyChargingSession(ChargingSession ChargingSession)
        {

            var sessionResult  = SessionVerificationResult.ValidSignature;
            var valueCount     = 0;

            foreach (var measurement in ChargingSession.Measurements)
            {

                foreach (var measurementValue in measurement.Values)
                {

                    valueCount++;

                    if (VerifyMeasurement(measurementValue).Status != VerificationResult.ValidSignature)
                        sessionResult = SessionVerificationResult.InvalidSignature;

                }

                // A measurement is valid when every one of its readings is, and a
                // measurement without readings is not valid by default.
                measurement.VerificationResult = new CryptoResult(
                                                     measurement.Values.Count > 0 &&
                                                     measurement.Values.All(value => value.Result?.Status == VerificationResult.ValidSignature)
                                                         ? VerificationResult.ValidSignature
                                                         : VerificationResult.InvalidSignature
                                                 );

            }

            // No readings at all is not an invalid signature — there is nothing to
            // have a signature over, and saying otherwise would blame the meter for
            // a malformed file.
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
        /// Report what the SML signature concluded about a single reading.
        /// </summary>
        /// <param name="MeasurementValue">A signed energy meter reading.</param>
        public override CryptoResult VerifyMeasurement(MeasurementValue MeasurementValue)
        {

            if (MeasurementValue is EDL40MeasurementValue edl40MeasurementValue)
            {

                var result = new CryptoResult(edl40MeasurementValue.Document.ValidationStatus);

                MeasurementValue.Result = result;

                return result;

            }

            return MeasurementValue.Result
                       ?? new CryptoResult(VerificationResult.Unvalidated);

        }

        #endregion


    }

}
