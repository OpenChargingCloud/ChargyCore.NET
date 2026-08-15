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

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.Crypto;
using cloud.charging.open.chargy.Formats.EMH;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the EMH energy meter signatures.
    ///
    /// These work on synthetic readings rather than on fixtures, because what is
    /// being checked here is how the verification tells its failure modes apart —
    /// and a fixture can only ever exhibit one of them.
    /// </summary>
    [TestFixture]
    public class EMHCrypt01Tests : AChargyTests
    {

        #region Data

        private readonly I18NDictionary i18n = I18NDictionary.Default();

        #endregion

        #region DecodeStatus_ReadsTheStatusAsHexadecimal()

        /// <summary>
        /// The status word is hexadecimal, and reading it as decimal loses bits.
        ///
        /// 0x40 is 64, which is the "magnetic field detected" bit. Read as decimal,
        /// "40" is 0b101000 and that bit is never set — so a meter reporting that
        /// somebody held a magnet against it would be reported as reporting
        /// nothing at all.
        /// </summary>
        [Test]
        public void DecodeStatus_ReadsTheStatusAsHexadecimal()

            => Assert.That(
                   EMHCrypt01.DecodeStatus("40"),
                   Does.Contain("Magnetfeld erkannt")
               );

        #endregion

        #region DecodeStatus_ReadsTheHexadecimalDigitsAToF()

        /// <summary>
        /// 0x1A is 0b11010, which sets bits 2, 8 and 16 — and leaves bit 1 clear.
        ///
        /// A decimal parse stops at the 'A' and yields 1, which is exactly the
        /// "error detected" bit: the wrong reading does not merely lose
        /// information here, it invents an error the meter never reported.
        /// </summary>
        [Test]
        public void DecodeStatus_ReadsTheHexadecimalDigitsAToF()
        {

            var statusFlags = EMHCrypt01.DecodeStatus("1A");

            Assert.Multiple(() => {

                Assert.That(statusFlags, Does.Contain    ("Synchrone Messwertübermittlung"));  // bit  2
                Assert.That(statusFlags, Does.Contain    ("System-Uhr ist synchron"));         // bit  8
                Assert.That(statusFlags, Does.Contain    ("Rücklaufsperre aktiv"));            // bit 16
                Assert.That(statusFlags, Does.Not.Contain("Fehler erkannt"));                  // bit  1 must stay clear

            });

        }

        #endregion


        #region RecordsAStructuredReasonForAVerificationError()

        /// <summary>
        /// A failed verification keeps a stable key, a translated message and the
        /// raw technical detail apart.
        ///
        /// The key is what a user interface can branch on, the message is what an
        /// EV driver reads, and the detail belongs in a bug report — putting an
        /// exception message in front of a driver explains nothing to them.
        /// </summary>
        [Test]
        public void RecordsAStructuredReasonForAVerificationError()
        {

            var result = new CryptoResult(VerificationResult.InvalidSignature);

            new TestableEMHCrypt01(i18n).Record(
                result,
                "Verification_SignatureMismatch",
                new Exception("boom")
            );

            Assert.That(result.Errors, Has.Count.EqualTo(1));

            var error = result.Errors[0];

            Assert.Multiple(() => {

                Assert.That(error.Code,                       Is.EqualTo("Verification_SignatureMismatch"));
                Assert.That(error.Details,                    Is.EqualTo("boom"));
                Assert.That(error.Message[Languages.en],      Is.EqualTo("The signature does not match the signed data!"));
                Assert.That(error.Message[Languages.de],      Is.EqualTo("Die Signatur passt nicht zu den signierten Daten!"));

            });

        }

        #endregion

        #region ReportsAnUndecodablePublicKeyAsSuch()

        /// <summary>
        /// A public key that is not a key at all.
        /// </summary>
        [Test]
        public void ReportsAnUndecodablePublicKeyAsSuch()
        {

            var result = VerifyWith(
                             PublicKeyHEX: new String('f', 40),
                             R:            "01",
                             S:            "01"
                         );

            Assert.Multiple(() => {

                Assert.That(result.Status,                                   Is.EqualTo(VerificationResult.InvalidPublicKey));
                Assert.That(result.Errors.Select(error => error.Code),       Does.Contain("Verification_PublicKeyDecodingFailed"));

            });

        }

        #endregion

        #region ReportsAPublicKeyThatIsNotOnTheCurveAsSuch()

        /// <summary>
        /// A public key of exactly the right shape whose point does not lie on
        /// secp192r1.
        ///
        /// This is told apart from an undecodable key deliberately: the two have
        /// different causes — a corrupted file versus a key from the wrong curve —
        /// and reporting both as "broken key" hides which.
        /// </summary>
        [Test]
        public void ReportsAPublicKeyThatIsNotOnTheCurveAsSuch()
        {

            var result = VerifyWith(
                             PublicKeyHEX: "04" + new String('0', 96),
                             R:            "01",
                             S:            "01"
                         );

            Assert.Multiple(() => {

                Assert.That(result.Status,                                   Is.EqualTo(VerificationResult.InvalidPublicKey));
                Assert.That(result.Errors.Select(error => error.Code),       Does.Contain("Verification_PublicKeyNotOnCurve"));

            });

        }

        #endregion

        #region TellsAGenuineSignatureMismatchFromMalformedInput()

        /// <summary>
        /// A cryptographically well-formed signature over unrelated data.
        ///
        /// Everything here is valid — the key is on the curve, the signature is a
        /// real signature — and it still does not match. That is the one case
        /// where the meter or the record really is saying something untrue, so it
        /// must not be confused with a parsing problem: a genuine mismatch carries
        /// no exception detail, because nothing threw.
        /// </summary>
        [Test]
        public void TellsAGenuineSignatureMismatchFromMalformedInput()
        {

            var suite      = ECCurveVerifier.secp192r1.Suite;
            var keyPair    = suite.GenerateKeyPair();

            var signature  = suite.Sign(
                                 Convert.FromHexString(String.Concat(Enumerable.Repeat("ab", 24))),
                                 keyPair.PrivateKey,
                                 new SignatureOptions(
                                     Prehashed:  true,
                                     Encoding:   SignatureEncoding.Compact
                                 )
                             );

            var result     = VerifyWith(
                                 PublicKeyHEX: Convert.ToHexStringLower(keyPair.PublicKey),
                                 R:            Convert.ToHexStringLower(signature.AsSpan( 0, 24)),
                                 S:            Convert.ToHexStringLower(signature.AsSpan(24, 24))
                             );

            Assert.Multiple(() => {

                Assert.That(result.Status,                              Is.EqualTo(VerificationResult.InvalidSignature));
                Assert.That(result.Errors.Select(error => error.Code),  Does.Contain("Verification_SignatureMismatch"));

                Assert.That(result.Errors.First(error => error.Code == "Verification_SignatureMismatch").Details,
                            Is.Null);

            });

        }

        #endregion


        #region (private) VerifyWith(PublicKeyHEX, R, S)

        /// <summary>
        /// Verify one synthetic reading against the given public key and signature.
        /// </summary>
        /// <param name="PublicKeyHEX">The public key of the meter, hexadecimal.</param>
        /// <param name="R">The r value of the signature, hexadecimal.</param>
        /// <param name="S">The s value of the signature, hexadecimal.</param>
        private CryptoResult VerifyWith(String  PublicKeyHEX,
                                        String  R,
                                        String  S)
        {

            const String energyMeterId = "METER-1";

            var measurementValue  = new MeasurementValue(
                                        "2024-01-01T00:00:00Z",
                                        123000,
                                        [ new SignatureRS(R, S) ],
                                        StatusMeter:   "08",
                                        SecondsIndex:  0,
                                        PaginationId:  "00000001",
                                        LogBookIndex:  "0000"
                                    );

            var measurement       = new Measurement(
                                        energyMeterId,
                                        "ENERGY_TOTAL",
                                        "1-0:1.8.0*255",
                                        0,
                                        UnitEncoded: 30
                                    );

            measurement.AddValue(measurementValue);

            new ChargingSession("session-1").
                AddMeasurement(measurement);

            var energyMeter       = new EnergyMeter(
                                        energyMeterId,
                                        PublicKeys: [
                                                        new PublicKey(
                                                            PublicKeyHEX,
                                                            new OIDInfo("secp192r1"),
                                                            Format: "rs"
                                                        )
                                                    ]
                                    );

            return new EMHCrypt01(
                       i18n,
                       meterId => meterId == energyMeterId ? energyMeter : null
                   ).VerifyMeasurement(measurementValue);

        }

        #endregion

        #region (private) TestableEMHCrypt01

        /// <summary>
        /// Reaches the failure recording of <see cref="ACrypt"/>, which is
        /// deliberately not part of the public surface.
        /// </summary>
        private sealed class TestableEMHCrypt01(I18NDictionary I18N) : EMHCrypt01(I18N)
        {

            /// <summary>Record why a verification step failed.</summary>
            public void Record(CryptoResult  CryptoResult,
                               String        ReasonKey,
                               Object?       Detail = null)

                => AddVerificationError(CryptoResult, ReasonKey, Detail);

        }

        #endregion

    }

}
