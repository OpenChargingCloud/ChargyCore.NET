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

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the ChargePoint format, against the golden reports shared with
    /// ChargyCore.TS.
    /// </summary>
    [TestFixture]
    public class ChargePointTests : AChargyTests
    {

        #region Data

        private const String Payload = "ChargePoint/Testdata-2020-02/0024b1000002e300_2_123017065_payload.tar.bz2";

        #endregion


        #region WithoutAPublicKeyNothingCanBeConcluded()

        /// <summary>
        /// The signed record on its own.
        ///
        /// ChargePoint signs the whole document rather than the individual
        /// readings, and without the operator's key there is nothing to check that
        /// signature against — so the readings are shown, and the session is
        /// honestly reported as having no key rather than as invalid.
        /// </summary>
        [Test]
        public Task WithoutAPublicKeyNothingCanBeConcluded()

            => ExpectReport(
                   Payload,
                   "ChargePoint/Testdata-2020-02/0024b1000002e300_2_123017065_payload.expected.txt"
               );

        #endregion

        #region WithAPublicKeyTheSessionVerifies(KeyFixture)

        /// <summary>
        /// The same record together with the operator's public key, which arrives
        /// in three shapes: a PEM file, a Chargy public key file, and a minimal one
        /// carrying nothing but the key and who it belongs to.
        ///
        /// All three have to reach the same report. How a key was filed is not
        /// evidence about anything.
        /// </summary>
        /// <param name="KeyFixture">A fixture path relative to "TestData".</param>
        [TestCase("ChargePoint/Testdata-2020-02/0024b1000002e300_2.pem")]
        [TestCase("ChargePoint/Testdata-2020-02/0024b1000002e300_2-publicKey.chargy")]
        [TestCase("ChargePoint/Testdata-2020-02/0024b1000002e300_2-publicKey_minimal.chargy")]
        public Task WithAPublicKeyTheSessionVerifies(String KeyFixture)

            => ExpectReport(
                   [ Payload, KeyFixture ],
                   "ChargePoint/Testdata-2020-02/0024b1000002e300_2_123017065_payload-withPublicKey.expected.txt"
               );

        #endregion

        #region RecordAndKeyShippedTogether(Fixture)

        /// <summary>
        /// The record and its key packed into one download, as a ".chargy" file and
        /// as a ZIP archive.
        /// </summary>
        /// <param name="Fixture">A fixture path relative to "TestData".</param>
        [TestCase("ChargePoint/Testdata-2020-02/0024b1000002e300_2.chargy")]
        [TestCase("ChargePoint/Testdata-2020-02/0024b1000002e300_2_123017065_withPublicKey.zip")]
        public Task RecordAndKeyShippedTogether(String Fixture)

            => ExpectReport(
                   Fixture,
                   "ChargePoint/Testdata-2020-02/0024b1000002e300_2_123017065_payload-withPublicKey.expected.txt"
               );

        #endregion


        #region TheInvoiceFormatVerifies(Curve, KeyFixture, Tariff)

        /// <summary>
        /// The older ChargePoint shape, which is an invoice with the session record
        /// buried in its "additional_info".
        ///
        /// Neither implementation has a golden file for any of these, so nothing
        /// held that half of the format up until now — and it is the half that
        /// carries the tariffs, the parking periods and a charging session whose
        /// span has to be worked out from the line items rather than read off a
        /// field. Six tariff shapes, on both curves the format uses.
        /// </summary>
        /// <param name="Curve">The directory of the curve the records are signed on.</param>
        /// <param name="KeyFixture">The public key belonging to those records.</param>
        /// <param name="Tariff">The tariff variant of one record.</param>
        /// <param name="HasParking">
        /// Whether this variant records a parking period. The ones billed purely by
        /// time or by energy do not, which is why it is stated per case rather than
        /// assumed: a test that expected parking everywhere would have to be
        /// weakened to pass, and would then stop checking anything.
        /// </param>
        [TestCase("Testdata-secp224k1", "0024b10000027b29_1.pem",           "FLAT_SESSION",          true)]
        [TestCase("Testdata-secp224k1", "0024b10000027b29_1.pem",           "Per_Min",               false)]
        [TestCase("Testdata-secp224k1", "0024b10000027b29_1.pem",           "_Per_KWh",              false)]
        [TestCase("Testdata-secp224k1", "0024b10000027b29_1.pem",           "Min_Variation_",        true)]
        [TestCase("Testdata-secp224k1", "0024b10000027b29_1.pem",           "_TOU_",                 false)]
        [TestCase("Testdata-secp224k1", "0024b10000027b29_1.pem",           "Parking_Tap_ToCharge",  true)]
        [TestCase("Testdata-secp256r1", "0024b10000027b29_1-publicKey.pem", "FLAT_SESSION",          true)]
        [TestCase("Testdata-secp256r1", "0024b10000027b29_1-publicKey.pem", "Per_Min",               false)]
        [TestCase("Testdata-secp256r1", "0024b10000027b29_1-publicKey.pem", "_Per_KWh",              false)]
        [TestCase("Testdata-secp256r1", "0024b10000027b29_1-publicKey.pem", "Min_Variation_",        true)]
        [TestCase("Testdata-secp256r1", "0024b10000027b29_1-publicKey.pem", "_TOU_",                 false)]
        [TestCase("Testdata-secp256r1", "0024b10000027b29_1-publicKey.pem", "Parking_Tap_ToCharge",  true)]
        public async Task TheInvoiceFormatVerifies(String   Curve,
                                                   String   KeyFixture,
                                                   String   Tariff,
                                                   Boolean  HasParking)
        {

            var directory  = $"ChargePoint/{Curve}/1/compressed";

            var result     = await VerifyFixtures([
                                       $"{directory}/{TariffFixtures[Tariff]}",
                                       $"{directory}/{KeyFixture}"
                                   ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var record           = (ChargeTransparencyRecord) result;
            var chargingSession  = record.ChargingSessions[0];

            Assert.Multiple(() => {

                Assert.That(chargingSession.VerificationResult?.Status,
                            Is.EqualTo(SessionVerificationResult.ValidSignature),
                            VerificationReport.Format(result));

                // The readings are not signed individually, and are labelled by
                // where they sit in the session rather than by their authenticity.
                Assert.That(chargingSession.Measurements[0].Values.Select(value => value.Result?.Status),
                            Is.EqualTo(new[] {
                                VerificationResult.StartValue,
                                VerificationResult.StopValue
                            }));

                // The invoice-only parts have to have been read too, otherwise this
                // would only be testing the signature path a second time.
                Assert.That(chargingSession.Parking.Count > 0,                              Is.EqualTo(HasParking));
                Assert.That(record.ChargingStationOperators[0].ChargingTariffs,             Is.Not.Empty);
                Assert.That(record.ChargingStationOperators[0].ChargingTariffs[0].Currency, Is.Not.Null);

                // The session's span is worked out from the line items, not read
                // off a field of its own.
                Assert.That(chargingSession.Begin,                                          Is.Not.Null.And.Not.Empty);
                Assert.That(chargingSession.End,                                            Is.Not.Null.And.Not.Empty);

            });

        }

        /// <summary>The archive each tariff variant was filed under.</summary>
        private static readonly Dictionary<String, String> TariffFixtures = new () {
            [ "FLAT_SESSION"          ] = "0024b10000027b29_1_121708795_payload.FLAT_SESSION.tar.bz2",
            [ "Per_Min"               ] = "0024b10000027b29_1_121708845_payload.Per_Min.tar.bz2",
            [ "_Per_KWh"              ] = "0024b10000027b29_1_121709375_payload._Per_KWh.tar.bz2",
            [ "Min_Variation_"        ] = "0024b10000027b29_1_121709405_payload_Min_Variation_.tar.bz2",
            [ "_TOU_"                 ] = "0024b10000027b29_1_121709415_payload._TOU_.tar.bz2",
            [ "Parking_Tap_ToCharge"  ] = "0024b10000027b29_1_121709465_payload_Parking_Tap_ToCharge.tar.bz2"
        };

        #endregion

    }

}
