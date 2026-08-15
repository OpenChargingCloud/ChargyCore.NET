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

namespace cloud.charging.open.chargy.Formats.OCMF
{

    /// <summary>
    /// Reports what the OCMF signatures concluded about a charging session.
    ///
    /// Unlike the binary formats, nothing is verified here. An OCMF signature
    /// covers a whole document rather than a single reading, so it was already
    /// checked when the document was read — and every reading inside it inherits
    /// that one verdict. This walks the readings and works out what the session
    /// as a whole can be said to be.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="MeterLookup">Unused: an OCMF reading carries its verdict already.</param>
    public class OCMFCrypt01(I18NDictionary               I18N,
                             Func<String, EnergyMeter?>?  MeterLookup = null)

        : ACrypt("OCMF", I18N, MeterLookup)

    {

        #region VerifyChargingSession(ChargingSession)

        /// <summary>
        /// Work out what the signatures say about a whole charging session.
        ///
        /// A session needs at least two readings: one reading proves a meter
        /// stood at some value at some moment, which says nothing about how much
        /// energy was delivered. And the session is only as good as its worst
        /// reading — the first reading that is not a valid signature decides,
        /// because a session where half the evidence holds is not half-verified.
        /// </summary>
        /// <param name="ChargingSession">A charging session.</param>
        public override SessionCryptoResult VerifyChargingSession(ChargingSession ChargingSession)
        {

            var sessionResult = SessionVerificationResult.Unvalidated;

            foreach (var measurement in ChargingSession.Measurements)
            {

                if (measurement.Values.Count <= 1)
                {
                    sessionResult = SessionVerificationResult.AtLeastTwoMeasurementsRequired;
                    continue;
                }

                foreach (var measurementValue in measurement.Values)
                {

                    sessionResult = measurementValue.Result?.Status switch {

                                        VerificationResult.EnergyMeterNotFound       => SessionVerificationResult.EnergyMeterNotFound,
                                        VerificationResult.UnknownSignatureFormat    => SessionVerificationResult.UnknownSignatureFormat,
                                        VerificationResult.PublicKeyNotFound         => SessionVerificationResult.PublicKeyNotFound,
                                        VerificationResult.UnknownPublicKeyFormat    => SessionVerificationResult.UnknownPublicKeyFormat,
                                        VerificationResult.InvalidPublicKey          => SessionVerificationResult.InvalidPublicKey,
                                        VerificationResult.InvalidSignature          => SessionVerificationResult.InvalidSignature,
                                        VerificationResult.ValidSignature            => SessionVerificationResult.ValidSignature,

                                        _                                            => sessionResult

                                    };

                    if (sessionResult != SessionVerificationResult.ValidSignature)
                        break;

                }

            }

            return new SessionCryptoResult(
                       sessionResult,
                       Certainty: 0.5
                   );

        }

        #endregion

        #region VerifyMeasurement    (MeasurementValue)

        /// <summary>
        /// Report what the OCMF signature concluded about a single reading.
        ///
        /// Nothing is recomputed: the reading was covered by the signature over
        /// the document it arrived in, and that was checked when the document was
        /// read.
        /// </summary>
        /// <param name="MeasurementValue">A signed energy meter reading.</param>
        public override CryptoResult VerifyMeasurement(MeasurementValue MeasurementValue)

            => MeasurementValue.Result
                   ?? new CryptoResult(VerificationResult.Unvalidated);

        #endregion


    }

}
