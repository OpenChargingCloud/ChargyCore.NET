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
using System.Text;

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.Formats.BSM
{

    /// <summary>
    /// Checks the signatures of a BAUER Electronic BSM-WS36A energy meter.
    ///
    /// The meter signs a snapshot of its whole state, and the block it signs has
    /// no fixed length: three of its fields are free text, so the block grows with
    /// them. Numbers are written as a 32 bit value, a signed 8 bit scale exponent
    /// and the DLMS code of the unit; strings as a 32 bit length followed by their
    /// bytes. Rebuilding that block is what verification means here.
    ///
    /// See https://github.com/chargeITmobility/bsm-python-private/blob/master/doc/examples/snapshots.md
    /// for the worked example the layout below follows.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="MeterLookup">How to find the energy meter that carries the public key.</param>
    public class BSMCrypt01(I18NDictionary               I18N,
                            Func<String, EnergyMeter?>?  MeterLookup = null)

        : ACrypt("ECC secp256r1", I18N, MeterLookup)

    {

        #region VerifyChargingSession(ChargingSession)

        /// <summary>
        /// Verify every measurement of a BSM charging session.
        ///
        /// A charging session needs at least a start and a stop snapshot: a single
        /// snapshot proves that a meter stood at some value at some moment, which
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

                // The session is only as good as its worst snapshot.
                sessionResult = measurement.Values.All(value => value.Result?.Status == VerificationResult.ValidSignature)
                                    ? SessionVerificationResult.ValidSignature
                                    : SessionVerificationResult.InvalidSignature;

            }

            return new SessionCryptoResult(
                       sessionResult,
                       Certainty: 0
                   );

        }

        #endregion

        #region VerifyMeasurement    (MeasurementValue)

        /// <summary>
        /// Verify a single signed BSM snapshot.
        /// </summary>
        /// <param name="MeasurementValue">A signed energy meter reading.</param>
        public override CryptoResult VerifyMeasurement(MeasurementValue MeasurementValue)
        {

            var result = new CryptoResult(VerificationResult.InvalidSignature);

            if (MeasurementValue is not BSMMeasurementValue snapshot ||
                MeasurementValue.Measurement is null)
            {
                return new CryptoResult(VerificationResult.InvalidMeasurement);
            }

            MeasurementValue.Result = result;

            #region Reassemble the exact bytes the meter signed

            var buffer = BuildSignedData(snapshot);

            #endregion

            #region The signature, the meter and its public key

            if (snapshot.Signatures.Count == 0 ||
                snapshot.Signatures[0] is not SignatureRS signature)
            {
                AddVerificationError(result, "Verification_SignatureMissing");
                return result;
            }

            var hash         = Convert.ToHexStringLower(SHA256.HashData(buffer));
            var energyMeter  = GetEnergyMeter(MeasurementValue.Measurement.EnergyMeterId);

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

            #region Decode the meter's public key, which arrives as a SubjectPublicKeyInfo

            ECVerificationKey? verificationKey;

            try
            {
                verificationKey = PublicKeyOf(publicKey);
            }
            catch (Exception exception)
            {
                AddVerificationError(result, "Verification_PublicKeyDecodingFailed", exception);
                result.Status = VerificationResult.InvalidPublicKey;
                return result;
            }

            if (verificationKey is null)
            {
                AddVerificationError(result, "Verification_PublicKeyDecodingFailed");
                result.Status = VerificationResult.InvalidPublicKey;
                return result;
            }

            #endregion

            #region Check the signature over the hashed plain text

            Boolean signatureValid;

            try
            {
                signatureValid = verificationKey.Verify(hash, signature.R, signature.S);
            }
            catch (Exception exception)
            {
                AddVerificationError(result, "Verification_SignatureMalformed", exception);
                result.Status = VerificationResult.InvalidSignature;
                return result;
            }

            // A snapshot whose surroundings did not add up is reported as such even
            // when its signature holds: the meter can vouch for what it measured,
            // not for the sequence of snapshots it was placed in.
            if (MeasurementValue.Errors.Count > 0)
            {
                result.Status = VerificationResult.ValidationError;
                return result;
            }

            if (signatureValid)
            {
                result.Status = VerificationResult.ValidSignature;
                return result;
            }

            AddVerificationError(result, "Verification_SignatureMismatch");
            result.Status = VerificationResult.InvalidSignature;
            return result;

            #endregion

        }

        #endregion


        #region (static) BuildSignedData(Snapshot)

        /// <summary>
        /// Rebuild the bytes a BSM meter signed for one snapshot.
        ///
        /// The order is the meter's, and it is not negotiable. Each numeric field
        /// occupies six bytes — value, scale, unit — and each text field four plus
        /// its own length, which is why the offsets past the meter address have to
        /// be computed rather than written down.
        /// </summary>
        /// <param name="Snapshot">A signed snapshot.</param>
        public static Byte[] BuildSignedData(BSMMeasurementValue Snapshot)
        {

            var ma1Length    = Encoding.UTF8.GetByteCount(Snapshot.MA1)   + 4;
            var meta1Length  = Encoding.UTF8.GetByteCount(Snapshot.Meta1) + 4;
            var meta2Length  = Encoding.UTF8.GetByteCount(Snapshot.Meta2) + 4;
            var meta3Length  = Encoding.UTF8.GetByteCount(Snapshot.Meta3) + 4;

            var buffer       = new Byte[13 * 6 + ma1Length + meta1Length + meta2Length + meta3Length];
            var span         = buffer.AsSpan();

            ChargyLib.SetUInt32WithCode(span, Snapshot.Type,         0,                        255,   0);                                              //  the kind of snapshot
            ChargyLib.SetUInt32WithCode(span, Snapshot.RCR,          Snapshot.RCRScale,         30,   6);                                              //  energy since the session began
            ChargyLib.SetUInt32WithCode(span, Snapshot.TotWhImp,     Snapshot.TotWhImpScale,    30,  12);                                              //  the meter's lifetime total
            ChargyLib.SetUInt32WithCode(span, Snapshot.W,            Snapshot.WScale,           27,  18);                                              //  momentary power
            ChargyLib.SetTextWithLength(span, Snapshot.MA1,                                          24);                                              //  the meter itself
            ChargyLib.SetUInt32WithCode(span, Snapshot.RCnt,         0,                        255,  24 + ma1Length);                                  //  snapshot counter
            ChargyLib.SetUInt32WithCode(span, Snapshot.OS,           0,                          7,  30 + ma1Length);                                  //  operation seconds
            ChargyLib.SetUInt32WithCode(span, Snapshot.Epoch,        0,                          7,  36 + ma1Length);                                  //  the meter's local time
            ChargyLib.SetUInt32WithCode(span, Snapshot.TZO,          0,                          6,  42 + ma1Length);                                  //  and its offset to UTC
            ChargyLib.SetUInt32WithCode(span, Snapshot.EpochSetCnt,  0,                        255,  48 + ma1Length);                                  //  how often the clock was set
            ChargyLib.SetUInt32WithCode(span, Snapshot.EpochSetOS,   0,                          7,  54 + ma1Length);                                  //  and when it last was
            ChargyLib.SetUInt32WithCode(span, Snapshot.DI,           0,                        255,  60 + ma1Length);                                  //  digital inputs
            ChargyLib.SetUInt32WithCode(span, Snapshot.DO,           0,                        255,  66 + ma1Length);                                  //  digital outputs
            ChargyLib.SetTextWithLength(span, Snapshot.Meta1,                                        72 + ma1Length);                                  //  the contract
            ChargyLib.SetTextWithLength(span, Snapshot.Meta2,                                        72 + ma1Length + meta1Length);                    //  the EVSE
            ChargyLib.SetTextWithLength(span, Snapshot.Meta3,                                        72 + ma1Length + meta1Length + meta2Length);      //  the charging station software
            ChargyLib.SetUInt32WithCode(span, Snapshot.Evt,          0,                        255,  72 + ma1Length + meta1Length + meta2Length + meta3Length);  //  what the meter noticed

            return buffer;

        }

        #endregion

        #region (private, static) PublicKeyOf(PublicKey)

        /// <summary>
        /// The meter's public key, ready to verify with.
        ///
        /// A BSM meter files its key as a DER encoded SubjectPublicKeyInfo rather
        /// than as bare coordinates, so the point has to be taken out of it first.
        /// </summary>
        /// <param name="PublicKey">The public key of the energy meter.</param>
        private static ECVerificationKey? PublicKeyOf(PublicKey PublicKey)
        {

            var curve = ECCurveVerifier.secp256r1;

            if (!String.Equals(PublicKey.Format, "DER", StringComparison.OrdinalIgnoreCase))
                return curve.ParsePublicKey(PublicKey.Value);

            var parsed = PublicKeyParser.TryParseDER(Convert.FromHexString(ChargyLib.CleanHex(PublicKey.Value)));

            return parsed is not null
                       ? curve.ParsePublicKey(parsed.ValueHEX)
                       : null;

        }

        #endregion


    }

}
