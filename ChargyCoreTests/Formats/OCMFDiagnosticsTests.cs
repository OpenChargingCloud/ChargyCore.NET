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

using System.Text;

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.X509;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.Formats.OCMF;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for what Chargy says when an OCMF signature does not check out.
    ///
    /// "Invalid" on its own is close to useless to the person holding the file.
    /// A signature that genuinely does not match the data means somebody has
    /// something to answer for; a curve this library does not implement means
    /// nothing about the charging session at all. Both come out of the same
    /// verification, and telling them apart is the whole job of these
    /// diagnostics.
    ///
    /// Each reason is therefore recorded three ways at once: a stable key an
    /// application can switch on, the sentence to show a driver in their own
    /// language, and — only where there is one — a technical detail that belongs
    /// in a bug report rather than on a receipt.
    /// </summary>
    [TestFixture]
    public class OCMFDiagnosticsTests : AChargyTests
    {

        #region AWrongPublicKeyReachesTheReadingAsAMismatch()

        /// <summary>
        /// A valid public key that simply is not this meter's.
        ///
        /// The key decodes, the point lies on the curve, the signature is
        /// well-formed — and it does not match. That is the one outcome that
        /// says something about the charging session rather than about the
        /// tooling, and it has to arrive at the individual reading, because the
        /// reading is what an EV driver is looking at.
        /// </summary>
        [Test]
        public async Task AWrongPublicKeyReachesTheReadingAsAMismatch()
        {

            var result = await Verify([
                             new FileInfo(
                                 "OCMF-Testdata-01.ocmf",
                                 Encoding.UTF8.GetBytes(ReadTextFixture("OCMF/OCMF-Testdata-01.ocmf").Trim()),
                                 "application/ocmf"
                             ),
                             new FileInfo(
                                 "publicKey.txt",
                                 Encoding.UTF8.GetBytes(UnrelatedButValidPublicKey()),
                                 "binary/octet-stream"
                             )
                         ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var value = ((ChargeTransparencyRecord) result).ChargingSessions[0].Measurements[0].Values[0];
            var error = value.Result?.Errors.FirstOrDefault();

            Assert.Multiple(() => {

                Assert.That(value.Result?.Status,  Is.EqualTo(VerificationResult.InvalidSignature));

                Assert.That(error,                 Is.Not.Null);
                Assert.That(error?.Code,           Is.EqualTo("Verification_SignatureMismatch"));
                Assert.That(error?.Message?[Languages.en],  Is.EqualTo("The signature does not match the signed data!"));
                Assert.That(error?.Message?[Languages.de],  Is.EqualTo("Die Signatur passt nicht zu den signierten Daten!"));

                // No technical detail: nothing went wrong technically. The
                // signature was checked and it did not match, and there is
                // nothing further to say about that.
                Assert.That(error?.Details,        Is.Null);

            });

        }

        #endregion

        #region ACurveChargyCannotCheckIsNotABadSignature()

        /// <summary>
        /// A document naming a curve this library does not implement.
        ///
        /// The verdict must not be "invalid signature": nothing about the
        /// charging session has been established either way, and reporting a
        /// perfectly good record as broken because of a gap in the verifier
        /// would be the worst answer available. The detail names the curve, so
        /// that whoever reads the bug report knows what is missing.
        /// </summary>
        [Test]
        public void ACurveChargyCannotCheckIsNotABadSignature()
        {

            var result = new OCMFFormat(I18NDictionary.Default()).TryParse(
                             [ WithSignatureAlgorithm("ECDSA-brainpool256r1-SHA256") ],
                             ReadTextFixture("OCMF/OCMF-Testdata-01_publicKey.txt").Trim(),
                             "hex",
                             null
                         );

            Assert.That(result, Is.InstanceOf<OCMFChargeTransparencyRecord>(), VerificationReport.Format(result));

            var value = ((OCMFChargeTransparencyRecord) result).ChargingSessions[0].Measurements[0].Values[0];
            var error = value.Result?.Errors.FirstOrDefault();

            Assert.Multiple(() => {

                Assert.That(value.Result?.Status,  Is.EqualTo(VerificationResult.InvalidPublicKey));

                Assert.That(error,                 Is.Not.Null);
                Assert.That(error?.Code,           Is.EqualTo("Verification_PublicKeyDecodingFailed"));
                Assert.That(error?.Message?[Languages.en],  Is.EqualTo("The public key could not be decoded!"));
                Assert.That(error?.Message?[Languages.de],  Is.EqualTo("Der öffentliche Schlüssel konnte nicht dekodiert werden!"));

                // ..., and here there is a technical detail, because here
                // something technical is missing.
                Assert.That(error?.Details,        Is.EqualTo("Unsupported ECC curve 'ECDSA-brainpool256r1-SHA256'!"));

            });

        }

        #endregion

        #region EveryReadingOfADocumentInheritsItsVerdict()

        /// <summary>
        /// One signature covers a whole OCMF document, so every reading inside
        /// carries the same verdict and the same reason.
        ///
        /// Nothing else would be honest: a document holding a start and an end
        /// reading is vouched for as a document, and there is no arrangement of
        /// the two under which one of them is proven and the other is not.
        /// </summary>
        [Test]
        public void EveryReadingOfADocumentInheritsItsVerdict()
        {

            var result = new OCMFFormat(I18NDictionary.Default()).TryParse(
                             [ ReadTextFixture("OCMF/BET_TariffTextExtension/001/001-01.ocmf").Trim() ],
                             UnrelatedButValidPublicKey(),
                             "hex",
                             null
                         );

            Assert.That(result, Is.InstanceOf<OCMFChargeTransparencyRecord>(), VerificationReport.Format(result));

            var values = ((OCMFChargeTransparencyRecord) result).ChargingSessions[0].Measurements[0].Values;

            Assert.That(values, Has.Count.EqualTo(2));

            Assert.Multiple(() => {
                foreach (var value in values)
                {
                    Assert.That(value.Result?.Status,                       Is.EqualTo(VerificationResult.InvalidSignature));
                    Assert.That(value.Result?.Errors.Select(error => error.Code),  Does.Contain("Verification_SignatureMismatch"));
                }
            });

        }

        #endregion


        #region (private, static) UnrelatedButValidPublicKey()

        /// <summary>
        /// A secp256r1 public key that is genuinely one, and genuinely not the
        /// meter's.
        ///
        /// Built from a fixed scalar rather than a random one, so that a failing
        /// test fails the same way twice, and by multiplying the curve's
        /// generator rather than by inventing coordinates, so that the point
        /// really lies on the curve. A key that merely looked like one would be
        /// rejected a step earlier and would test the wrong thing.
        /// </summary>
        private static String UnrelatedButValidPublicKey()
        {

            var curve      = ECNamedCurveTable.GetByName("secp256r1")!;

            // The curve is named rather than spelled out. Both are valid DER,
            // and only the named form is what meters and their key files use —
            // spelling the parameters out would test a shape no OCMF document
            // has ever arrived in.
            var domain     = new ECNamedDomainParameters(SecObjectIdentifiers.SecP256r1, curve);

            var publicKey  = new ECPublicKeyParameters(
                                 domain.G.Multiply(BigInteger.ValueOf(0x43484152475921L)).Normalize(),
                                 domain
                             );

            return Convert.ToHexString(
                       SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey).GetDerEncoded()
                   );

        }

        #endregion

        #region (private, static) WithSignatureAlgorithm(Algorithm)

        /// <summary>
        /// The OCMF test document, with its signature block claiming a different
        /// algorithm.
        ///
        /// Only the claim is changed; the signature itself stays as it was. That
        /// is the situation to test — a document naming an algorithm Chargy has
        /// no verifier for — and it is reached without needing a meter that
        /// signs on that curve.
        /// </summary>
        /// <param name="Algorithm">The OCMF name the document should claim.</param>
        private static String WithSignatureAlgorithm(String Algorithm)
        {

            var parts      = ReadTextFixture("OCMF/OCMF-Testdata-01.ocmf").Trim().Split('|');
            var signature  = ChargyLib.ParseJSON(parts[2]);

            signature["SA"] = Algorithm;

            return $"{parts[0]}|{parts[1]}|{signature.ToString(Newtonsoft.Json.Formatting.None)}";

        }

        #endregion

    }

}
