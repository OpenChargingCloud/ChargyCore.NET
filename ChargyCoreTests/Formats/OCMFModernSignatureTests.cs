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

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Asn1;

using cloud.charging.open.chargy.Formats.OCMF;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the signature algorithms OCMF gained after ECDSA.
    ///
    /// The same charging session is signed five more ways: with the two Edwards
    /// curves and with the three parameter sets of ML-DSA. The Edwards curves are
    /// there because they are simpler and harder to implement wrongly; ML-DSA is
    /// there because a charging record has to stay checkable for years, and a
    /// signature made today with an elliptic curve is a signature a large enough
    /// quantum computer could forge later.
    ///
    /// All five sign the payload itself rather than a digest of it, which is why
    /// they need their own path through the verification and their own way of
    /// being shown.
    /// </summary>
    [TestFixture]
    public class OCMFModernSignatureTests : AChargyTests
    {

        #region Data

        private const String FixtureRoot = "OCMF/BET_TariffTextExtension/001/";

        #endregion


        #region TheSignatureIsShownTheWayItsAlgorithmIsBuilt(Fixture, ComponentByteLength)

        /// <summary>
        /// How a signature is shown follows how its algorithm builds one.
        ///
        /// An EdDSA signature is r and s written one after the other, so it can
        /// be split in half and compared component by component against a second
        /// tool. An ML-DSA signature has no components to split, and pretending
        /// otherwise would invite a reader to compare two halves that mean
        /// nothing.
        /// </summary>
        /// <param name="Fixture">A fixture file name below the BET 001 directory.</param>
        /// <param name="ComponentByteLength">How long one half of the signature is, when there are halves.</param>
        [TestCase("001-01_Ed25519.ocmf",    32)]
        [TestCase("001-01_Ed448.ocmf",      57)]
        [TestCase("001-01_ML-DSA-44.ocmf",  -1)]
        [TestCase("001-01_ML-DSA-65.ocmf",  -1)]
        [TestCase("001-01_ML-DSA-87.ocmf",  -1)]
        public void TheSignatureIsShownTheWayItsAlgorithmIsBuilt(String  Fixture,
                                                                 Int32   ComponentByteLength)
        {

            var document   = ReadTextFixture($"{FixtureRoot}{Fixture}").Trim();
            var signature  = ChargyLib.ParseJSON(document.Split('|')[2]);
            var display    = OCMFSignatureDisplay.Of(signature);

            Assert.Multiple(() => {

                Assert.That(display.ValueLabel,  Is.EqualTo("raw"));
                Assert.That(display.Value,       Is.EqualTo(signature["SD"]?.Value<String>()));

                if (ComponentByteLength < 0)
                {
                    Assert.That(display.Format,  Is.EqualTo("raw, hex"));
                    Assert.That(display.R,       Is.Null);
                    Assert.That(display.S,       Is.Null);
                }

                else
                {

                    var componentHexLength = ComponentByteLength * 2;

                    Assert.That(display.Format,  Is.EqualTo("RS, hex"));
                    Assert.That(display.R,       Is.EqualTo(display.Value[..componentHexLength]));
                    Assert.That(display.S,       Is.EqualTo(display.Value[componentHexLength..]));

                }

            });

        }

        #endregion

        #region TheDocumentAndItsPEMKeyAreEnough(Fixture, PublicKeyPEM)

        /// <summary>
        /// A document and the standards-based form of its key, handed over
        /// together as an EV driver would hand them over.
        ///
        /// The point is the detection: nothing tells Chargy which of the two
        /// files is the key or which algorithm signed the other, and it has to
        /// work that out from the files themselves.
        /// </summary>
        /// <param name="Fixture">A fixture file name below the BET 001 directory.</param>
        /// <param name="PublicKeyPEM">The PEM file that goes with it.</param>
        [TestCase("001-01_Ed25519.ocmf",    "001-01_Ed25519.publicKey.pem")]
        [TestCase("001-01_Ed448.ocmf",      "001-01_Ed448.publicKey.pem")]
        [TestCase("001-01_ML-DSA-44.ocmf",  "001-01_ML-DSA-44.publicKey.pem")]
        [TestCase("001-01_ML-DSA-65.ocmf",  "001-01_ML-DSA-65.publicKey.pem")]
        [TestCase("001-01_ML-DSA-87.ocmf",  "001-01_ML-DSA-87.publicKey.pem")]
        public async Task TheDocumentAndItsPEMKeyAreEnough(String  Fixture,
                                                           String  PublicKeyPEM)
        {

            var result = await VerifyFixtures([
                             $"{FixtureRoot}{Fixture}",
                             $"{FixtureRoot}{PublicKeyPEM}"
                         ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            Assert.That(
                ((ChargeTransparencyRecord) result).ChargingSessions[0].VerificationResult?.Status,
                Is.EqualTo(SessionVerificationResult.ValidSignature),
                VerificationReport.Format(result)
            );

        }

        #endregion

        #region TheSameSessionVerifiesUnderEveryAlgorithm(Fixture, PublicKeyHEX, Algorithm)

        /// <summary>
        /// The same charging session, signed five different ways, has to come out
        /// as the same charging session five times.
        ///
        /// The tariff and the two readings are asserted alongside the verdict on
        /// purpose: an implementation that verified the signature and then read
        /// the payload differently would be no use, and the signature says
        /// nothing about how the bytes it covers are interpreted.
        /// </summary>
        /// <param name="Fixture">A fixture file name below the BET 001 directory.</param>
        /// <param name="PublicKeyHEX">The raw public key that goes with it.</param>
        /// <param name="Algorithm">The OCMF name of the signature algorithm.</param>
        [TestCase("001-01_Ed25519.ocmf",    "001-01_Ed25519.publicKey.hex",    "EdDSA-Ed25519")]
        [TestCase("001-01_Ed448.ocmf",      "001-01_Ed448.publicKey.hex",      "EdDSA-Ed448")]
        [TestCase("001-01_ML-DSA-44.ocmf",  "001-01_ML-DSA-44.publicKey.hex",  "ML-DSA-44")]
        [TestCase("001-01_ML-DSA-65.ocmf",  "001-01_ML-DSA-65.publicKey.hex",  "ML-DSA-65")]
        [TestCase("001-01_ML-DSA-87.ocmf",  "001-01_ML-DSA-87.publicKey.hex",  "ML-DSA-87")]
        public void TheSameSessionVerifiesUnderEveryAlgorithm(String  Fixture,
                                                              String  PublicKeyHEX,
                                                              String  Algorithm)
        {

            var document   = ReadTextFixture($"{FixtureRoot}{Fixture}").Trim();
            var publicKey  = ReadTextFixture($"{FixtureRoot}{PublicKeyHEX}").Trim();

            Assert.That(OCMFSignatureAlgorithm.TryGet(Algorithm), Is.Not.Null, $"{Algorithm} is not a known OCMF signature algorithm!");

            var result = new OCMFFormat(I18NDictionary.Default()).TryParse(
                             [ document ],
                             publicKey,
                             "hex",
                             null
                         );

            Assert.That(result, Is.InstanceOf<OCMFChargeTransparencyRecord>(), VerificationReport.Format(result));

            var record = (OCMFChargeTransparencyRecord) result;

            Assert.Multiple(() => {
                Assert.That(record.Status,                                    Is.EqualTo(SessionVerificationResult.ValidSignature));
                Assert.That(record.OCMF.TariffText,                           Is.EqualTo("001;EUR;0;35;5;30"));
                Assert.That(record.ChargingSessions[0].Measurements[0].Values, Has.Count.EqualTo(2));
            });

        }

        #endregion

        #region ThePEMKeyIsTheRawKeyInItsStandardWrapper(PublicKeyHEX, PublicKeyPEM, OID)

        /// <summary>
        /// Each fixture ships its key twice: raw, and wrapped in the
        /// SubjectPublicKeyInfo structure every other tool expects.
        ///
        /// The test builds the wrapper itself from the raw key and the algorithm
        /// identifier and compares it with the shipped one. Two files claiming to
        /// hold the same key while holding different keys would make one half of
        /// these tests verify a document the other half cannot.
        /// </summary>
        /// <param name="PublicKeyHEX">The raw public key.</param>
        /// <param name="PublicKeyPEM">The same key as a SubjectPublicKeyInfo.</param>
        /// <param name="OID">The object identifier of the signature algorithm.</param>
        [TestCase("001-01_Ed25519.publicKey.hex",    "001-01_Ed25519.publicKey.pem",    "1.3.101.112")]
        [TestCase("001-01_Ed448.publicKey.hex",      "001-01_Ed448.publicKey.pem",      "1.3.101.113")]
        [TestCase("001-01_ML-DSA-44.publicKey.hex",  "001-01_ML-DSA-44.publicKey.pem",  "2.16.840.1.101.3.4.3.17")]
        [TestCase("001-01_ML-DSA-65.publicKey.hex",  "001-01_ML-DSA-65.publicKey.pem",  "2.16.840.1.101.3.4.3.18")]
        [TestCase("001-01_ML-DSA-87.publicKey.hex",  "001-01_ML-DSA-87.publicKey.pem",  "2.16.840.1.101.3.4.3.19")]
        public void ThePEMKeyIsTheRawKeyInItsStandardWrapper(String  PublicKeyHEX,
                                                             String  PublicKeyPEM,
                                                             String  OID)
        {

            var rawPublicKey  = Convert.FromHexString(ReadTextFixture($"{FixtureRoot}{PublicKeyHEX}").Trim());

            var der           = Convert.FromBase64String(
                                    String.Concat(
                                        ReadTextFixture($"{FixtureRoot}{PublicKeyPEM}").
                                            Split('\n').
                                            Where (line => !line.StartsWith("-----", StringComparison.Ordinal)).
                                            Select(line => line.Trim())
                                    )
                                );

            // A SubjectPublicKeyInfo is a sequence of "which algorithm" and "the
            // key", and for these five the algorithm identifier is nothing but
            // the object identifier — no parameters, which is exactly what makes
            // them simpler than the elliptic curves they replace.
            var expected      = new DerSequence(
                                    new DerSequence(new DerObjectIdentifier(OID)),
                                    new DerBitString(rawPublicKey)
                                ).GetDerEncoded();

            Assert.That(Convert.ToHexString(der), Is.EqualTo(Convert.ToHexString(expected)));

        }

        #endregion

    }

}
