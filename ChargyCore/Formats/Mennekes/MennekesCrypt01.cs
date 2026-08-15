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

using System.Security.Cryptography;

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.Formats.Mennekes
{

    /// <summary>
    /// Checks the signatures of a Mennekes EDL40 charging station.
    ///
    /// The verification rebuilds the 320 bytes the meter signed and checks them
    /// against the meter's public key on secp192r1, over the first 24 bytes of the
    /// SHA-256 hash — the curve's order is 192 bits wide and cannot cover more.
    ///
    /// A valid signature is not the end of it here. A Mennekes charging process
    /// makes claims a signature cannot settle: that both readings came from the
    /// same uninterrupted process, that the meter counted forward, that the
    /// reading did not fall. Those are checked separately, and a process that
    /// fails them is reported as an implausible measurement rather than as a bad
    /// signature — because the signatures are genuine, and saying otherwise would
    /// accuse the meter of something it did not do.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="MeterLookup">How to find the energy meter that carries the public key.</param>
    public class MennekesCrypt01(I18NDictionary               I18N,
                                 Func<String, EnergyMeter?>?  MeterLookup = null)

        : ACrypt("Mennekes EDL40", I18N, MeterLookup)

    {

        #region Data

        /// <summary>How many bytes of the hash secp192r1 signs.</summary>
        public const Int32 HashTruncation = 24;

        #endregion


        #region VerifyChargingSession(ChargingSession)

        /// <summary>
        /// Verify every measurement of a Mennekes charging session.
        /// </summary>
        /// <param name="ChargingSession">A charging session.</param>
        public override SessionCryptoResult VerifyChargingSession(ChargingSession ChargingSession)
        {

            var sessionResult = SessionVerificationResult.UnknownSessionFormat;

            foreach (var measurement in ChargingSession.Measurements)
            {

                if (measurement.Values.Count < 2)
                {
                    sessionResult = SessionVerificationResult.AtLeastTwoMeasurementsRequired;
                    continue;
                }

                sessionResult = SessionVerificationResult.ValidSignature;

                foreach (var measurementValue in measurement.Values)
                    if (VerifyMeasurement(measurementValue).Status != VerificationResult.ValidSignature)
                        sessionResult = SessionVerificationResult.InvalidSignature;

                if (sessionResult != SessionVerificationResult.ValidSignature)
                    continue;

                #region The signatures hold — now do the readings describe one charging process?

                var conformityErrors = CheckLawConformity(measurement).ToArray();

                if (conformityErrors.Length > 0)
                {

                    var measurementResult = new CryptoResult(VerificationResult.InvalidMeasurement);

                    foreach (var error in conformityErrors)
                        measurementResult.AddError(error);

                    measurement.VerificationResult  = measurementResult;

                    // Not "implausible": that value is for a reading whose size
                    // is hard to believe. This is a reading that verifies and
                    // simply does not belong to the process it was filed under.
                    sessionResult                   = SessionVerificationResult.InvalidMeasurement;

                }

                else
                    measurement.VerificationResult = new CryptoResult(VerificationResult.ValidSignature);

                #endregion

            }

            return new SessionCryptoResult(
                       sessionResult,
                       Certainty: 0.5
                   );

        }

        #endregion

        #region VerifyMeasurement    (MeasurementValue)

        /// <summary>
        /// Verify a single signed Mennekes reading.
        /// </summary>
        /// <param name="MeasurementValue">A signed energy meter reading.</param>
        public override CryptoResult VerifyMeasurement(MeasurementValue MeasurementValue)
        {

            var result = new CryptoResult(VerificationResult.InvalidSignature);

            #region A reading only means something inside the charging process it came from

            if (MeasurementValue is not MennekesMeasurementValue measurementValue ||
                MeasurementValue.Measurement is not MennekesChargyMeasurement measurement)
            {
                return new CryptoResult(VerificationResult.InvalidMeasurement);
            }

            MeasurementValue.Result = result;

            #endregion

            #region The meter and its public key

            var energyMeter = GetEnergyMeter(measurement.EnergyMeterId);

            if (energyMeter is null)
            {
                result.Status = VerificationResult.EnergyMeterNotFound;
                return result;
            }

            if (energyMeter.PublicKeys.Count == 0)
            {
                result.Status = VerificationResult.PublicKeyNotFound;
                return result;
            }

            var publicKey = ChargyLib.CleanHex(energyMeter.PublicKeys[0].Value);

            // 96 hexadecimal digits is a pair of secp192r1 coordinates. Anything
            // else is not a key this format can have been signed with, and saying
            // so is more useful than letting the curve reject it later.
            if (publicKey.Length != 96)
            {
                AddVerificationError(result, "Verification_PublicKeyDecodingFailed");
                result.Status = VerificationResult.InvalidPublicKey;
                return result;
            }

            #endregion

            try
            {

                #region Reassemble the exact bytes the meter signed, and hash them

                var signedData = measurement.ChargingProcess.BuildSignedData(measurementValue.Reading);

                var hash       = Convert.ToHexStringLower(
                                     SHA256.HashData(signedData).AsSpan(0, HashTruncation)
                                 );

                #endregion

                var verificationKey = Curve192r1.ParsePublicKey(publicKey);

                if (verificationKey is null)
                {
                    AddVerificationError(result, "Verification_PublicKeyNotOnCurve");
                    result.Status = VerificationResult.InvalidPublicKey;
                    return result;
                }

                var signature = measurementValue.Reading.SignatureRS;

                if (verificationKey.Verify(hash, signature.R, signature.S))
                {
                    result.Status = VerificationResult.ValidSignature;
                    return result;
                }

                AddVerificationError(result, "Verification_SignatureMismatch");
                result.Status = VerificationResult.InvalidSignature;
                return result;

            }
            catch (Exception exception)
            {
                AddVerificationError(result, "Verification_UnexpectedError", exception);
                result.Status = VerificationResult.InvalidSignature;
                return result;
            }

        }

        #endregion


        #region (private) CheckLawConformity(Measurement)

        /// <summary>
        /// Whether the two readings really describe one uninterrupted charging
        /// process.
        ///
        /// Each of these is something a valid signature cannot rule out. The meter
        /// signs each reading on its own, so two perfectly genuine readings from
        /// two different charging processes would both verify — and billing an EV
        /// driver for the difference between them would be wrong. The counters are
        /// what ties them together.
        /// </summary>
        /// <param name="Measurement">A measurement with at least two readings.</param>
        private IEnumerable<Error> CheckLawConformity(Measurement Measurement)
        {

            var errors = new List<Error>();

            if (Measurement.Values[0]  is not MennekesMeasurementValue start ||
                Measurement.Values[^1] is not MennekesMeasurementValue end)
            {
                errors.Add(new Error(I18N.GetMultilanguageText("Mennekes EDL40 requires at least two measurement values.")));
                return errors;
            }

            // The meter counts events; a different count means something happened
            // between the two readings that this process does not account for.
            if (start.Reading.EventCounter != end.Reading.EventCounter)
                errors.Add(new Error(I18N.GetMultilanguageText("Event counter mismatch.")));

            if (start.Reading.Pagination >= end.Reading.Pagination)
                errors.Add(new Error(I18N.GetMultilanguageText("Pagination must increase from start to end.")));

            if (start.Value > end.Value)
                errors.Add(new Error(I18N.GetMultilanguageText("Meter value must not decrease.")));

            return errors;

        }

        #endregion


    }

}
