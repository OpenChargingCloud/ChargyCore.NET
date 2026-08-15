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
using cloud.charging.open.chargy.Formats;
using cloud.charging.open.chargy.Formats.GDF;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the GDF energy meter signatures.
    ///
    /// Neither this port nor ChargyCore.TS has a fixture from a real GDF meter, so
    /// what can honestly be checked here is the plumbing rather than the format:
    /// that the charge transparency record processor reaches GDFCrypt01 at all,
    /// that it verifies on secp256r1 over the whole SHA-256 hash rather than a
    /// truncated one, and that a wrong signature is reported as wrong.
    ///
    /// What these tests cannot confirm is the byte layout of the signed block —
    /// they sign the very buffer this port assembles, so a layout that is wrong
    /// in the same way twice would still pass. Only a real meter can settle that.
    /// </summary>
    [TestFixture]
    public class GDFCrypt01Tests : AChargyTests
    {

        #region Data

        private readonly I18NDictionary i18n = I18NDictionary.Default();

        #endregion


        #region TheProcessorReachesGDFForItsOwnSessionContext()

        /// <summary>
        /// A charging session that names the GDF format is handed to GDFCrypt01.
        ///
        /// Without this, a GDF record would be reported as an unknown session
        /// format — which would tell an EV driver that Chargy does not know their
        /// meter, when in truth it was simply never asked.
        /// </summary>
        [Test]
        public void TheProcessorReachesGDFForItsOwnSessionContext()
        {

            var record = BuildRecord(SignCorrectly: true);

            new ChargeTransparencyRecordProcessor(i18n).Process(record);

            Assert.That(
                record.ChargingSessions[0].VerificationResult?.Status,
                Is.EqualTo(SessionVerificationResult.ValidSignature)
            );

        }

        #endregion

        #region ASignatureOverOtherDataIsReportedAsInvalid()

        /// <summary>
        /// A well-formed signature that does not belong to these readings.
        /// </summary>
        [Test]
        public void ASignatureOverOtherDataIsReportedAsInvalid()
        {

            var record = BuildRecord(SignCorrectly: false);

            new ChargeTransparencyRecordProcessor(i18n).Process(record);

            Assert.Multiple(() => {

                Assert.That(record.ChargingSessions[0].VerificationResult?.Status,
                            Is.EqualTo(SessionVerificationResult.InvalidSignature));

                Assert.That(record.ChargingSessions[0].Measurements[0].Values.
                                   SelectMany(value => value.Result?.Errors ?? []).
                                   Select    (error => error.Code),
                            Does.Contain("Verification_SignatureMismatch"));

            });

        }

        #endregion

        #region ASingleReadingIsNotAChargingSession()

        /// <summary>
        /// One reading says a meter stood at a value at a moment, which is not a
        /// statement about delivered energy.
        /// </summary>
        [Test]
        public void ASingleReadingIsNotAChargingSession()
        {

            var record = BuildRecord(SignCorrectly: true, Readings: 1);

            new ChargeTransparencyRecordProcessor(i18n).Process(record);

            Assert.That(
                record.ChargingSessions[0].VerificationResult?.Status,
                Is.EqualTo(SessionVerificationResult.AtLeastTwoMeasurementsRequired)
            );

        }

        #endregion


        #region (private) BuildRecord(SignCorrectly, Readings = 2)

        /// <summary>
        /// A synthetic GDF charge transparency record, signed with a key generated
        /// for this test.
        /// </summary>
        /// <param name="SignCorrectly">Whether to sign the readings or something else.</param>
        /// <param name="Readings">How many readings the charging session holds.</param>
        private static ChargeTransparencyRecord BuildRecord(Boolean  SignCorrectly,
                                                            Int32    Readings = 2)
        {

            const String energyMeterId  = "GDF-METER-1";
            const String evseId         = "DE*GEF*EVSE*GDF*1";

            var suite    = ECCurveVerifier.secp256r1.Suite;
            var keyPair  = suite.GenerateKeyPair();

            var measurement = new Measurement(
                                  energyMeterId,
                                  "ENERGY_TOTAL",
                                  "1-0:1.8.0*255",
                                  0,
                                  UnitEncoded: 30
                              );

            var chargingSession = new ChargingSession(
                                      "gdf-session-1",
                                      Context:  [ GDFCrypt01.SessionContext ],
                                      EVSEId:   evseId
                                  ) {
                                      AuthorizationStart = new Authorization(
                                                               "AABBCCDD",
                                                               Timestamp: "2024-01-01T10:00:00Z"
                                                           )
                                  };

            chargingSession.AddMeasurement(measurement);

            #region The readings, each signed over the very bytes this port assembles

            for (var reading = 0; reading < Readings; reading++)
            {

                var timestamp  = $"2024-01-01T1{reading}:00:00Z";
                var value      = (Decimal) (1000 + reading * 500);

                var hash       = SHA256.HashData(
                                     SignedDataOf(timestamp, value, measurement, chargingSession)
                                 );

                var signature  = suite.Sign(
                                     SignCorrectly
                                         ? hash
                                         : SHA256.HashData("something else entirely"u8),
                                     keyPair.PrivateKey,
                                     new SignatureOptions(
                                         Prehashed:  true,
                                         Encoding:   SignatureEncoding.Compact
                                     )
                                 );

                measurement.AddValue(
                    new MeasurementValue(
                        timestamp,
                        value,
                        [
                            new SignatureRS(
                                Convert.ToHexStringLower(signature.AsSpan( 0, 32)),
                                Convert.ToHexStringLower(signature.AsSpan(32, 32))
                            )
                        ]
                    )
                );

            }

            #endregion

            var record = new ChargeTransparencyRecord(
                             "gdf-record-1",
                             [ "https://open.charging.cloud/contexts/CTR+json" ]
                         );

            record.AddChargingSession(chargingSession);

            record.AddChargingStation(
                new ChargingStation(
                    "DE*GEF*STATION*GDF*1",
                    EVSEs: [
                               new EVSE(
                                   evseId,
                                   EnergyMeters: [
                                                     new EnergyMeter(
                                                         energyMeterId,
                                                         PublicKeys: [
                                                                         new PublicKey(
                                                                             Convert.ToHexStringLower(keyPair.PublicKey),
                                                                             new OIDInfo("secp256r1")
                                                                         )
                                                                     ]
                                                     )
                                                 ]
                               )
                           ]
                )
            );

            return record;

        }

        #endregion

        #region (private, static) SignedDataOf(Timestamp, Value, Measurement, ChargingSession)

        /// <summary>
        /// The 320 bytes a GDF meter would sign for this reading.
        /// </summary>
        private static Byte[] SignedDataOf(String           Timestamp,
                                           Decimal          Value,
                                           Measurement      Measurement,
                                           ChargingSession  ChargingSession)
        {

            var buffer = new Byte[GDFCrypt01.SignedDataLength];
            var span   = buffer.AsSpan();

            ChargyLib.SetText     (span, Measurement.EnergyMeterId,                           0);
            ChargyLib.SetTimestamp(span, Timestamp,                                          10);
            ChargyLib.SetHex      (span, Measurement.OBIS ?? "",                             23, false);
            ChargyLib.SetInt8     (span, Measurement.UnitEncoded ?? 0,                       29);
            ChargyLib.SetInt8     (span, Measurement.Scale,                                  30);
            ChargyLib.SetUInt64   (span, Value,                                              31, true);
            ChargyLib.SetHex      (span, ChargingSession.AuthorizationStart?.Id        ?? "", 41);
            ChargyLib.SetTimestamp(span, ChargingSession.AuthorizationStart?.Timestamp ?? "", 169);

            return buffer;

        }

        #endregion

    }

}
