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

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.Formats.Alfen
{

    /// <summary>
    /// Checks the signatures of an Alfen charging station.
    ///
    /// The verification rebuilds the exact 82 bytes the signing adapter put its
    /// signature on, from the values that were parsed out of them. That
    /// round trip is the point: if the reassembled buffer differed from the
    /// original in a single bit — a timestamp read as local time, a meter reading
    /// rounded, a status word byte-swapped — the signature would fail, and it
    /// would fail for a reason that has nothing to do with the charging station
    /// being dishonest. So the layout below has to be right for the same reason
    /// the parser's does.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="MeterLookup">How to find the energy meter that carries the public key.</param>
    public class AlfenCrypt01(I18NDictionary               I18N,
                              Func<String, EnergyMeter?>?  MeterLookup = null)

        : ACrypt("Alfen", I18N, MeterLookup)

    {

        #region VerifyChargingSession(ChargingSession)

        /// <summary>
        /// Verify every measurement of an Alfen charging session.
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
        /// Verify a single signed Alfen meter reading.
        /// </summary>
        /// <param name="MeasurementValue">A signed energy meter reading.</param>
        public override CryptoResult VerifyMeasurement(MeasurementValue MeasurementValue)
        {

            var result = new CryptoResult(VerificationResult.InvalidSignature);

            MeasurementValue.Result = result;

            #region Reassemble the exact bytes the adapter signed

            if (MeasurementValue.Measurement is not AlfenMeasurement measurement)
            {
                AddVerificationError(result, "Verification_UnexpectedError", "Not an Alfen measurement!");
                return result;
            }

            var chargingSession = measurement.ChargingSession;

            Span<Byte> buffer = stackalloc Byte[82];

            ChargyLib.SetHex        (buffer, measurement.AdapterId,                                    0);
            ChargyLib.SetText       (buffer, measurement.AdapterFWVersion,                            10);
            ChargyLib.SetHex        (buffer, measurement.AdapterFWChecksum,                           14);
            ChargyLib.SetHex        (buffer, measurement.EnergyMeterId,                               16);
            ChargyLib.SetHex        (buffer, MeasurementValue.StatusMeter   ?? "",                    26, true);
            ChargyLib.SetHex        (buffer, MeasurementValue.StatusAdapter ?? "",                    28, true);
            ChargyLib.SetUInt32     (buffer, (UInt32) (MeasurementValue.SecondsIndex ?? 0),           30, true);
            // An Alfen data set stores a plain UTC UNIX timestamp, so no local
            // offset is added — the parser already turned those seconds into an
            // ISO 8601 timestamp in UTC, and adding an offset here would move the
            // reading by exactly the meter's summer time.
            ChargyLib.SetTimestamp32(buffer, MeasurementValue.Timestamp,                              34, AddMeterOffset: false);
            // An Alfen data set always names what it measured. A measurement that
            // did not would leave these bytes empty and fail its own signature,
            // which is the right answer: the OBIS code is part of what was signed.
            ChargyLib.SetHex        (buffer, ChargyLib.OBIS2Hex(measurement.OBIS ?? ""),             38);
            ChargyLib.SetInt8       (buffer, measurement.UnitEncoded ?? 0,                            44);
            ChargyLib.SetInt8       (buffer, measurement.Scale,                                       45);
            ChargyLib.SetUInt64     (buffer, MeasurementValue.Value,                                  46, true);
            ChargyLib.SetText       (buffer, chargingSession?.AuthorizationStart?.Id ?? "",           54);
            ChargyLib.SetUInt32     (buffer, ParseUInt32(chargingSession?.InternalSessionId),         74, true);
            ChargyLib.SetUInt32     (buffer, ParseUInt32(MeasurementValue.PaginationId),              78, true);

            #endregion

            #region The signature, the meter and its public key

            if (MeasurementValue.Signatures.Count == 0 ||
                MeasurementValue.Signatures[0] is not SignatureRS signature)
            {
                AddVerificationError(result, "Verification_SignatureMissing");
                return result;
            }

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

            var publicKey = energyMeter.PublicKeys[0];

            #endregion

            #region Decode the meter's public key

            Byte[] publicKeyBytes;

            try
            {
                publicKeyBytes = publicKey.Value.FromBASE32();
            }
            catch (Exception exception)
            {
                AddVerificationError(result, "Verification_PublicKeyDecodingFailed", exception);
                result.Status = VerificationResult.InvalidPublicKey;
                return result;
            }

            #endregion

            #region Check the signature over the hash of the signed buffer

            try
            {

                var curve = ECCurveVerifier.TryGet(publicKey.Algorithm?.Name);

                if (curve is null)
                {
                    AddVerificationError(result, "UnknownOrInvalidPublicKeyFormat", publicKey.Algorithm?.Name);
                    result.Status = VerificationResult.InvalidPublicKey;
                    return result;
                }

                var verificationKey = curve.ParsePublicKey(Convert.ToHexStringLower(publicKeyBytes));

                if (verificationKey is null)
                {
                    AddVerificationError(result, "Verification_PublicKeyDecodingFailed");
                    result.Status = VerificationResult.InvalidPublicKey;
                    return result;
                }

                var hash = HashOf(curve, buffer);

                result.Status = verificationKey.Verify(hash, signature.R, signature.S)
                                    ? VerificationResult.ValidSignature
                                    : VerificationResult.InvalidSignature;

                if (result.Status != VerificationResult.ValidSignature)
                    AddVerificationError(result, "Verification_SignatureMismatch");

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


        #region (private, static) HashOf     (Curve, Buffer)

        /// <summary>
        /// Hash the signed buffer with the digest that belongs to the curve.
        ///
        /// The digest is not a free choice: it has to be the one the signing
        /// adapter used, and for the elliptic curves that is the one whose output
        /// matches the size of the curve's order.
        /// </summary>
        /// <param name="Curve">The elliptic curve of the public key.</param>
        /// <param name="Buffer">The bytes that were signed.</param>
        private static Byte[] HashOf(ECCurveVerifier     Curve,
                                     ReadOnlySpan<Byte>  Buffer)

            => Curve.CurveName switch {
                   "secp384r1"  => SHA384.HashData(Buffer),
                   "secp521r1"  => SHA512.HashData(Buffer),
                   _            => SHA256.HashData(Buffer)
               };

        #endregion

        #region (private, static) ParseUInt32(Text)

        /// <summary>
        /// Parse a number that a data format keeps as text.
        /// </summary>
        /// <param name="Text">A number as text.</param>
        private static UInt32 ParseUInt32(String? Text)

            => UInt32.TryParse(Text, out var value)
                   ? value
                   : 0;

        #endregion


    }

}
