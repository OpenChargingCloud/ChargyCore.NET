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

namespace cloud.charging.open.chargy.Formats.GDF
{

    /// <summary>
    /// Checks the signatures of a GDF energy meter.
    ///
    /// Close kin to the EMH format: the same 320 byte block, the same idea of
    /// rebuilding it field by field. Three things differ, and all three matter.
    /// The meter identification goes in as text rather than as hexadecimal, the
    /// signature lives on secp256r1 and therefore covers the whole SHA-256 hash
    /// rather than its first 24 bytes, and the block carries no status word,
    /// seconds index, pagination counter or log book index at all.
    ///
    /// Note that no test fixture in either this port or ChargyCore.TS exercises
    /// this format. It is here because a charge transparency record may name it,
    /// and reporting "unknown session format" for a record whose format is known
    /// would be misleading — but it has never been checked against a real meter.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="MeterLookup">How to find the energy meter that carries the public key.</param>
    public class GDFCrypt01(I18NDictionary               I18N,
                            Func<String, EnergyMeter?>?  MeterLookup = null)

        : ACrypt("ECC secp256r1", I18N, MeterLookup)

    {

        #region Data

        /// <summary>The JSON-LD context of a GDF charging session.</summary>
        public const String SessionContext    = "https://open.charging.cloud/contexts/SessionSignatureFormats/GDFCrypt01+json";

        /// <summary>The length of the block a GDF energy meter signs.</summary>
        public const Int32  SignedDataLength  = 320;

        #endregion


        #region VerifyChargingSession(ChargingSession)

        /// <summary>
        /// Verify every measurement of a GDF charging session.
        ///
        /// A charging session needs at least a start and a stop reading: a single
        /// reading proves that a meter stood at some value at some moment, which
        /// says nothing at all about how much energy was delivered.
        /// </summary>
        /// <param name="ChargingSession">A charging session.</param>
        public override SessionCryptoResult VerifyChargingSession(ChargingSession ChargingSession)
        {

            var sessionResult = SessionVerificationResult.UnknownSessionFormat;

            foreach (var measurement in ChargingSession.Measurements)
            {

                if (measurement.Values.Count <= 1)
                {
                    sessionResult = SessionVerificationResult.AtLeastTwoMeasurementsRequired;
                    continue;
                }

                foreach (var measurementValue in measurement.Values)
                    VerifyMeasurement(measurementValue);

                // The session is only as good as its worst reading.
                sessionResult = measurement.Values.All(value => value.Result?.Status == VerificationResult.ValidSignature)
                                    ? SessionVerificationResult.ValidSignature
                                    : SessionVerificationResult.InvalidSignature;

            }

            return new SessionCryptoResult(
                       sessionResult,
                       Certainty: 0.5
                   );

        }

        #endregion

        #region VerifyMeasurement    (MeasurementValue)

        /// <summary>
        /// Verify a single signed GDF meter reading.
        /// </summary>
        /// <param name="MeasurementValue">A signed energy meter reading.</param>
        public override CryptoResult VerifyMeasurement(MeasurementValue MeasurementValue)
        {

            var result = new CryptoResult(VerificationResult.InvalidSignature);

            #region A reading that does not belong to a charging session cannot be verified

            var measurement      = MeasurementValue.Measurement;
            var chargingSession  = measurement?.ChargingSession;

            if (measurement is null || chargingSession is null)
                return new CryptoResult(VerificationResult.InvalidMeasurement);

            MeasurementValue.Result = result;

            #endregion

            #region Reassemble the exact bytes the meter signed

            Span<Byte> buffer = stackalloc Byte[SignedDataLength];

            ChargyLib.SetText     (buffer, measurement.EnergyMeterId,                          0);
            ChargyLib.SetTimestamp(buffer, MeasurementValue.Timestamp,                        10);
            // The OBIS code goes in as the meter spells it, not converted: this
            // format writes the code itself where EMH writes its hexadecimal form.
            ChargyLib.SetHex      (buffer, measurement.OBIS ?? "",                            23, false);
            ChargyLib.SetInt8     (buffer, measurement.UnitEncoded ?? 0,                      29);
            ChargyLib.SetInt8     (buffer, measurement.Scale,                                 30);
            ChargyLib.SetUInt64   (buffer, MeasurementValue.Value,                            31, true);
            ChargyLib.SetHex      (buffer, chargingSession.AuthorizationStart?.Id        ?? "", 41);
            ChargyLib.SetTimestamp(buffer, chargingSession.AuthorizationStart?.Timestamp ?? "", 169);

            #endregion

            #region The signature, the meter and its public key

            if (MeasurementValue.Signatures.Count == 0 ||
                MeasurementValue.Signatures[0] is not SignatureRS signature)
            {
                AddVerificationError(result, "Verification_SignatureMissing");
                return result;
            }

            // secp256r1 is as wide as SHA-256, so unlike the EMH format nothing
            // of the hash is dropped.
            var hash         = Convert.ToHexStringLower(SHA256.HashData(buffer));
            var energyMeter  = GetEnergyMeter(measurement.EnergyMeterId);

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

            #endregion

            #region Decode the meter's public key, and check the signature

            var verificationKey = Curve256r1.ParsePublicKey(energyMeter.PublicKeys[0].Value.ToLowerInvariant());

            if (verificationKey is null)
            {
                AddVerificationError(result, "Verification_PublicKeyDecodingFailed");
                result.Status = VerificationResult.InvalidPublicKey;
                return result;
            }

            try
            {

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
                AddVerificationError(result, "Verification_SignatureMalformed", exception);
                result.Status = VerificationResult.InvalidSignature;
                return result;
            }

            #endregion

        }

        #endregion


    }

}
