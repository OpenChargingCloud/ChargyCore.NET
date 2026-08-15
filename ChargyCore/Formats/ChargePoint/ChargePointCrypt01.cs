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

namespace cloud.charging.open.chargy.Formats.ChargePoint
{

    /// <summary>
    /// Checks the signature of a ChargePoint charging session.
    ///
    /// ChargePoint signs the session document as a whole rather than each reading,
    /// so there is exactly one signature to check and it covers the file's exact
    /// bytes — whitespace included. Which is why the document travels through the
    /// record verbatim: re-serialising the parsed JSON would produce different
    /// bytes and a signature that fails for a reason having nothing to do with the
    /// charging station.
    ///
    /// Older charging station firmwares sign on the Koblitz curve secp224k1, newer
    /// ones on secp256r1, and the record does not say which — so the key decides,
    /// and each available key is tried in turn.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="MeterLookup">Unused: ChargePoint signs the document, not the meter readings.</param>
    public class ChargePointCrypt01(I18NDictionary               I18N,
                                    Func<String, EnergyMeter?>?  MeterLookup = null)

        : ACrypt("ECC secp224k1/secp256r1", I18N, MeterLookup)

    {

        #region VerifyChargingSession(ChargingSession)

        /// <summary>
        /// Verify the signature over a whole ChargePoint charging session.
        /// </summary>
        /// <param name="ChargingSession">A charging session.</param>
        public override SessionCryptoResult VerifyChargingSession(ChargingSession ChargingSession)
        {

            if (ChargingSession.CTR is null)
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSignature,
                           I18N.GetMultilanguageText("InvalidSignature")
                       );

            try
            {

                var sessionResult  = SessionVerificationResult.UnknownSessionFormat;

                var plainText      = ChargingSession.Original is not null
                                         ? Convert.FromBase64String(ChargingSession.Original)
                                         : [];

                #region The signature arrives DER encoded, and has to become r and s

                var signature = AsSignatureRS(ChargingSession.Signature);

                if (signature is not null)
                    ChargingSession.Signature = signature;

                #endregion

                #region Which keys could have signed this? The ones naming this EVSE, or all of them

                var publicKeyId  = ChargingSession.EVSEId?.Replace(":", "").Replace("-", "_");

                var publicKeys   = ChargingSession.CTR.PublicKeys.
                                       Where(publicKey => publicKey.Identifications.Contains(publicKeyId)).
                                       ToArray();

                // No key claims this EVSE, so every available key is tried instead.
                // A key that verifies the document proves it belongs to it, whatever
                // the key file says about itself.
                if (publicKeys.Length == 0)
                    publicKeys = [.. ChargingSession.CTR.PublicKeys];

                if (publicKeys.Length == 0)
                    return new SessionCryptoResult(
                               SessionVerificationResult.PublicKeyNotFound,
                               I18N.GetMultilanguageText("Public key notFound")
                           );

                #endregion

                #region Check the signature over the document

                if (plainText.Length > 0 && signature is not null)
                    sessionResult = VerifyDocument(ChargingSession, plainText, signature, publicKeys);

                #endregion

                #region The readings themselves carry no signatures, and are labelled by their place

                foreach (var measurement in ChargingSession.Measurements)
                {

                    if (measurement.Values.Count <= 1)
                    {
                        sessionResult = SessionVerificationResult.AtLeastTwoMeasurementsRequired;
                        continue;
                    }

                    foreach (var measurementValue in measurement.Values)
                        VerifyMeasurement(measurementValue);

                    foreach (var measurementValue in measurement.Values)
                        if (measurementValue.Result?.Status != VerificationResult.ValidSignature &&
                            measurementValue.Result?.Status != VerificationResult.NoOperation)
                        {
                            return new SessionCryptoResult(
                                       SessionVerificationResult.InvalidSignature,
                                       I18N.GetMultilanguageText("InvalidSignature")
                                   );
                        }

                    LabelValuesByPosition(measurement);

                }

                #endregion

                return new SessionCryptoResult(
                           sessionResult,
                           Certainty: 0.5
                       );

            }
            catch (Exception exception)
            {
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSignature,
                           I18N.GetMultilanguageText("InvalidSignature"),
                           Exception: exception
                       );
            }

        }

        #endregion

        #region VerifyMeasurement    (MeasurementValue)

        /// <summary>
        /// Report that a single reading was not verified.
        ///
        /// ChargePoint does not sign individual readings, and saying "valid" about
        /// one would claim evidence that does not exist. The session's signature is
        /// what vouches for them, collectively.
        /// </summary>
        /// <param name="MeasurementValue">An energy meter reading.</param>
        public override CryptoResult VerifyMeasurement(MeasurementValue MeasurementValue)
        {

            var result = new CryptoResult(VerificationResult.NoOperation);

            MeasurementValue.Result = result;

            return result;

        }

        #endregion


        #region (private) VerifyDocument(ChargingSession, PlainText, Signature, PublicKeys)

        /// <summary>
        /// Try each candidate public key against the signed document.
        /// </summary>
        /// <param name="ChargingSession">The charging session being verified.</param>
        /// <param name="PlainText">The exact bytes that were signed.</param>
        /// <param name="Signature">The signature, as its two integers.</param>
        /// <param name="PublicKeys">The keys that could have signed it.</param>
        private static SessionVerificationResult VerifyDocument(ChargingSession  ChargingSession,
                                                                Byte[]           PlainText,
                                                                SignatureRS      Signature,
                                                                PublicKey[]      PublicKeys)
        {

            var sessionResult = SessionVerificationResult.UnknownSessionFormat;

            String? sha256Value = null;
            String? sha384Value = null;
            String? sha512Value = null;

            foreach (var publicKey in PublicKeys)
            {

                var curve = ECCurveVerifier.TryGet(publicKey.Algorithm?.Name);

                if (curve is null)
                    continue;

                #region The hash follows the curve, and is computed at most once per kind

                var hash = curve.CurveName switch {

                               // secp224k1 has a 225 bit order, so the 256 bit hash
                               // is truncated to its leftmost 225 bits — which the
                               // ECDSA implementation does on its own.
                               "secp224k1"  => sha256Value ??= Convert.ToHexStringLower(SHA256.HashData(PlainText)),
                               "secp256r1"  => sha256Value ??= Convert.ToHexStringLower(SHA256.HashData(PlainText)),
                               "secp384r1"  => sha384Value ??= Convert.ToHexStringLower(SHA384.HashData(PlainText)),
                               "secp521r1"  => sha512Value ??= Convert.ToHexStringLower(SHA512.HashData(PlainText)),

                               _            => null

                           };

                if (hash is null)
                    continue;

                #endregion

                var verificationKey = curve.ParsePublicKey(publicKey.Value);

                if (verificationKey is null)
                {
                    sessionResult = SessionVerificationResult.InvalidSignature;
                    continue;
                }

                sessionResult = verificationKey.Verify(hash, Signature.R, Signature.S)
                                    ? SessionVerificationResult.ValidSignature
                                    : SessionVerificationResult.InvalidSignature;

                if (sessionResult == SessionVerificationResult.ValidSignature)
                {

                    ChargingSession.PublicKey  = publicKey;
                    ChargingSession.HashValue  = hash;

                    // Stop at the first key that works. When no key named this EVSE
                    // and all of them were tried as a fallback, carrying on would
                    // let a later, unrelated key overwrite a result that already
                    // holds.
                    break;

                }

            }

            return sessionResult;

        }

        #endregion

        #region (private, static) LabelValuesByPosition(Measurement)

        /// <summary>
        /// Say of each reading whether it opened, closed or sat inside the charging
        /// session.
        ///
        /// The readings are unsigned, so what an EV driver can be told about them is
        /// their place in the session rather than their authenticity — and a start
        /// value is the thing they were billed from.
        /// </summary>
        /// <param name="Measurement">A measurement with at least two readings.</param>
        private static void LabelValuesByPosition(Measurement Measurement)
        {

            for (var index = 0; index < Measurement.Values.Count; index++)
            {

                var result = Measurement.Values[index].Result;

                if (result is null)
                    continue;

                var position = index == 0                            ? Position.Start
                             : index == Measurement.Values.Count - 1 ? Position.Stop
                             :                                        Position.Intermediate;

                result.Status = (position, result.Status) switch {

                                    (Position.Start,        VerificationResult.ValidSignature)    => VerificationResult.ValidStartValue,
                                    (Position.Start,        VerificationResult.NoOperation)       => VerificationResult.StartValue,
                                    (Position.Start,        VerificationResult.InvalidSignature)  => VerificationResult.InvalidStartValue,

                                    (Position.Stop,         VerificationResult.ValidSignature)    => VerificationResult.ValidStopValue,
                                    (Position.Stop,         VerificationResult.NoOperation)       => VerificationResult.StopValue,
                                    (Position.Stop,         VerificationResult.InvalidSignature)  => VerificationResult.InvalidStopValue,

                                    (Position.Intermediate, VerificationResult.ValidSignature)    => VerificationResult.ValidIntermediateValue,
                                    (Position.Intermediate, VerificationResult.NoOperation)       => VerificationResult.IntermediateValue,
                                    // Note: an invalid intermediate value is reported
                                    // as an invalid *stop* value, which is what the
                                    // reference implementation does.
                                    (Position.Intermediate, VerificationResult.InvalidSignature)  => VerificationResult.InvalidStopValue,

                                    _                                                            => result.Status

                                };

            }

        }

        /// <summary>Where a reading sits within a charging session.</summary>
        private enum Position
        {
            Start,
            Intermediate,
            Stop
        }

        #endregion

        #region (private, static) AsSignatureRS(Signature)

        /// <summary>
        /// The session's signature as its two integers.
        ///
        /// ChargePoint files it DER encoded, but a record that has already been
        /// through Chargy carries it taken apart — so both are accepted.
        /// </summary>
        /// <param name="Signature">The signature of a charging session.</param>
        private static SignatureRS? AsSignatureRS(Signature? Signature)
        {

            if (Signature is SignatureRS signatureRS)
                return signatureRS;

            if (Signature?.Value is null || Signature.Value.Length == 0)
                return null;

            try
            {

                var decoded = ECCurveVerifier.TryDecodeDERSignature(
                                  Convert.FromHexString(ChargyLib.CleanHex(Signature.Value))
                              );

                return decoded is not null
                           ? new SignatureRS(decoded.Value.R, decoded.Value.S, Value: Signature.Value)
                           : null;

            }
            catch (Exception)
            {
                return null;
            }

        }

        #endregion


    }

}
