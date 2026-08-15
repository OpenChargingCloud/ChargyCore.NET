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

namespace cloud.charging.open.chargy.Formats.EMH
{

    /// <summary>
    /// Checks the signatures of an EMH energy meter.
    ///
    /// The meter signs a 320 byte block it assembles from its own values, and the
    /// charge transparency record carries the values rather than the block — so
    /// verifying a reading means rebuilding those 320 bytes field by field and
    /// arriving at the same bytes the meter had. Only the first 24 bytes of the
    /// SHA-256 hash are signed, because secp192r1 has a 192 bit order.
    ///
    /// Every offset below is therefore load-bearing. A field written one byte off,
    /// a timestamp read as UTC where the meter meant its own local time, a meter
    /// reading rounded on the way in: any of those produces "invalid signature" on
    /// a charging session where nothing whatsoever was wrong.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="MeterLookup">How to find the energy meter that carries the public key.</param>
    public class EMHCrypt01(I18NDictionary               I18N,
                            Func<String, EnergyMeter?>?  MeterLookup = null)

        : ACrypt("ECC secp192r1", I18N, MeterLookup)

    {

        #region Data

        /// <summary>The length of the block an EMH energy meter signs.</summary>
        public const Int32 SignedDataLength = 320;

        /// <summary>How many bytes of the hash secp192r1 signs.</summary>
        public const Int32 HashTruncation   = 24;

        #endregion


        #region VerifyChargingSession(ChargingSession)

        /// <summary>
        /// Verify every measurement of an EMH charging session.
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
                       Certainty: 1
                   );

        }

        #endregion

        #region VerifyMeasurement    (MeasurementValue)

        /// <summary>
        /// Verify a single signed EMH meter reading.
        /// </summary>
        /// <param name="MeasurementValue">A signed energy meter reading.</param>
        public override CryptoResult VerifyMeasurement(MeasurementValue MeasurementValue)
        {

            var result = new EMHCryptoResult(VerificationResult.InvalidSignature);

            #region A reading that does not belong to a charging session cannot be verified

            var measurement      = MeasurementValue.Measurement;
            var chargingSession  = measurement?.ChargingSession;

            if (measurement is null || chargingSession is null)
                return new CryptoResult(VerificationResult.InvalidMeasurement);

            MeasurementValue.Result = result;

            #endregion

            #region Reassemble the exact bytes the meter signed

            Span<Byte> buffer = stackalloc Byte[SignedDataLength];

            result.EnergyMeterId                = ChargyLib.SetHex        (buffer, measurement.EnergyMeterId,                                    0);
            result.Timestamp                    = ChargyLib.SetTimestamp32(buffer, MeasurementValue.Timestamp,                                   10);
            result.InfoStatus                   = ChargyLib.SetHex        (buffer, MeasurementValue.StatusMeter ?? "",                           14, false);
            result.SecondsIndex                 = ChargyLib.SetUInt32     (buffer, (UInt32) (MeasurementValue.SecondsIndex ?? 0),                15, true);
            result.PaginationId                 = ChargyLib.SetHex        (buffer, MeasurementValue.PaginationId ?? "",                          19, true);
            result.OBIS                         = ChargyLib.SetHex        (buffer, ChargyLib.OBIS2Hex(measurement.OBIS),                         23, false);
            result.UnitEncoded                  = ChargyLib.SetInt8       (buffer, measurement.UnitEncoded ?? 0,                                 29);
            result.Scale                        = ChargyLib.SetInt8       (buffer, measurement.Scale,                                            30);
            result.Value                        = ChargyLib.SetUInt64     (buffer, MeasurementValue.Value,                                       31, true);
            result.LogBookIndex                 = ChargyLib.SetHex        (buffer, MeasurementValue.LogBookIndex ?? "",                          39, false);
            result.AuthorizationStart           = ChargyLib.SetText       (buffer, chargingSession.AuthorizationStart?.Id ?? "",                 41);
            result.AuthorizationStartTimestamp  = ChargyLib.SetTimestamp32(buffer, chargingSession.AuthorizationStart?.Timestamp
                                                                                       ?? MeasurementValue.Timestamp,                          169);

            #endregion

            #region The signature, the meter and its public key

            if (MeasurementValue.Signatures.Count == 0 ||
                MeasurementValue.Signatures[0] is not SignatureRS signature)
            {
                AddVerificationError(result, "Verification_SignatureMissing");
                return result;
            }

            result.Signature = signature;

            // Only the first 24 bytes of the hash are signed: secp192r1 cannot
            // sign more than its own order is wide.
            result.SHA256Value = Convert.ToHexStringLower(
                                     SHA256.HashData(buffer).AsSpan(0, HashTruncation)
                                 );

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

            result.EnergyMeter      = energyMeter;
            result.PublicKey        = publicKey.Value.ToLowerInvariant();
            result.PublicKeyFormat  = publicKey.Format;

            #endregion

            #region Decode the meter's public key, and make sure it is a point on the curve

            ECVerificationKey? verificationKey;

            try
            {
                verificationKey = Curve192r1.ParsePublicKey(result.PublicKey);
            }
            catch (Exception exception)
            {
                AddVerificationError(result, "Verification_PublicKeyDecodingFailed", exception);
                result.Status = VerificationResult.InvalidPublicKey;
                return result;
            }

            if (verificationKey is null)
            {
                // A key that does not decode at all and a key that decodes into a
                // point somewhere off the curve are different failures, and an EV
                // driver reading a bug report deserves to be told which one.
                AddVerificationError(
                    result,
                    LooksLikeAPoint(result.PublicKey)
                        ? "Verification_PublicKeyNotOnCurve"
                        : "Verification_PublicKeyDecodingFailed"
                );

                result.Status = VerificationResult.InvalidPublicKey;
                return result;

            }

            #endregion

            #region Check the signature over the hashed plain text

            try
            {

                if (verificationKey.Verify(result.SHA256Value, signature.R, signature.S))
                {
                    result.Status = VerificationResult.ValidSignature;
                    return result;
                }

                // Structurally valid, but the signature does not match the signed
                // data — which carries no exception detail, because nothing failed.
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


        #region (static) LooksLikeAPoint(PublicKeyHEX)

        /// <summary>
        /// Whether a public key at least has the shape of an uncompressed point
        /// on secp192r1.
        /// </summary>
        /// <param name="PublicKeyHEX">A public key, hexadecimal.</param>
        private static Boolean LooksLikeAPoint(String? PublicKeyHEX)
        {

            var hex = ChargyLib.CleanHex(PublicKeyHEX ?? "");

            return hex.Length == 96 ||
                   hex.Length == 98 && hex.StartsWith("04", StringComparison.OrdinalIgnoreCase);

        }

        #endregion

        #region (static) DecodeStatus  (StatusValue)

        /// <summary>
        /// Say in words what the status word of an EMH energy meter reports.
        ///
        /// The status is hexadecimal, and it has to be read as such: parsing "40"
        /// as decimal yields 0b101000 and never sets bit 64 — so a meter reporting
        /// a detected magnetic field would silently be reported as reporting
        /// nothing.
        ///
        /// The texts stay in German because they are the meter's own vocabulary
        /// from the German Calibration Law conformity documents, not messages this
        /// library authors. Translating them here would put words into the
        /// meter's mouth.
        /// </summary>
        /// <param name="StatusValue">The status word of the meter, hexadecimal.</param>
        public static IEnumerable<String> DecodeStatus(String StatusValue)
        {

            var statusFlags = new List<String>();

            if (!Int64.TryParse(StatusValue?.Trim(),
                                System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var status))
            {
                statusFlags.Add("Invalid status!");
                return statusFlags;
            }

            if ((status &  1) ==  1)
                statusFlags.Add("Fehler erkannt");

            if ((status &  2) ==  2)
                statusFlags.Add("Synchrone Messwertübermittlung");

            // Bit 3 is reserved!

            statusFlags.Add((status & 8) == 8
                                ? "System-Uhr ist synchron"
                                : "System-Uhr ist nicht synchron");

            if ((status & 16) == 16)
                statusFlags.Add("Rücklaufsperre aktiv");

            if ((status & 32) == 32)
                statusFlags.Add("Energierichtung -A");

            if ((status & 64) == 64)
                statusFlags.Add("Magnetfeld erkannt");

            return statusFlags;

        }

        #endregion


    }


    /// <summary>
    /// What verifying an EMH meter reading concluded, field by field.
    ///
    /// Each property holds the hexadecimal form of one field as it went into the
    /// signed block, so that a user interface can show an EV driver which bytes
    /// were signed — not merely that the signature did or did not hold.
    /// </summary>
    /// <param name="Status">What the verification concluded.</param>
    public class EMHCryptoResult(VerificationResult Status) : CryptoResult(Status)
    {

        #region Properties

        /// <summary>The truncated SHA-256 hash of the signed block.</summary>
        public String?        SHA256Value                    { get; set; }

        /// <summary>The energy meter that carried the public key.</summary>
        public EnergyMeter?   EnergyMeter                    { get; set; }

        /// <summary>The identification of the energy meter, as signed.</summary>
        public String?        EnergyMeterId                  { get; set; }

        /// <summary>The timestamp of the reading, as signed.</summary>
        public String?        Timestamp                      { get; set; }

        /// <summary>The status word of the meter, as signed.</summary>
        public String?        InfoStatus                     { get; set; }

        /// <summary>The seconds index, as signed.</summary>
        public String?        SecondsIndex                   { get; set; }

        /// <summary>The pagination counter, as signed.</summary>
        public String?        PaginationId                   { get; set; }

        /// <summary>The OBIS code, as signed.</summary>
        public String?        OBIS                           { get; set; }

        /// <summary>The unit of the reading, as signed.</summary>
        public String?        UnitEncoded                    { get; set; }

        /// <summary>The scale of the reading, as signed.</summary>
        public String?        Scale                          { get; set; }

        /// <summary>The reading itself, as signed.</summary>
        public String?        Value                          { get; set; }

        /// <summary>The log book index, as signed.</summary>
        public String?        LogBookIndex                   { get; set; }

        /// <summary>The token the driver authorized with, as signed.</summary>
        public String?        AuthorizationStart             { get; set; }

        /// <summary>When the driver authorized, as signed.</summary>
        public String?        AuthorizationStartTimestamp    { get; set; }

        /// <summary>The public key of the meter.</summary>
        public String?        PublicKey                      { get; set; }

        /// <summary>The format of the public key.</summary>
        public String?        PublicKeyFormat                { get; set; }

        /// <summary>The signature that was checked.</summary>
        public SignatureRS?   Signature                      { get; set; }

        #endregion

    }

}
