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

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

using cloud.charging.open.chargy.Formats.OCMF;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the two secp192 curves of OCMF.
    ///
    /// This is a deliberate difference from ChargyCore.TS, which recognises
    /// "ECDSA-secp192k1-SHA256" and "ECDSA-secp192r1-SHA256" but cannot verify
    /// either, because its JavaScript curve library does not carry them.
    /// BouncyCastle does, so this port verifies them rather than declining to
    /// look at a charging session that is perfectly checkable.
    ///
    /// The difference is one-directional and safe: a record that verifies in
    /// ChargyCore.TS verifies here, and the only records treated differently are
    /// the ones the TypeScript implementation refuses to judge at all.
    ///
    /// There are no secp192 OCMF fixtures to compare against, so the documents
    /// are signed here and verified through the ordinary path — public key as
    /// DER, signature as DER, hashed exactly as the algorithm name prescribes.
    /// A round trip proves the wiring; it cannot prove agreement with a third
    /// party, and if a real secp192 record ever turns up it should become a
    /// fixture.
    /// </summary>
    [TestFixture]
    public class OCMFSecp192Tests : AChargyTests
    {

        #region Data

        private const String payload = "{\"FV\":\"1.0\",\"GI\":\"CI-Tests\",\"RD\":[{\"TM\":\"2026-08-15T10:00:00,000+0000 U\",\"RV\":42.5,\"RI\":\"1-b:1.8.0\",\"RU\":\"kWh\"}]}";

        private readonly OCMFDocumentScanner     scanner   = new ();
        private          OCMFSignatureValidator  validator = null!;

        [SetUp]
        public void Setup()
        {
            validator = new OCMFSignatureValidator(I18NDictionary.Default());
        }

        #endregion


        #region BothSecp192Curves_AreRecognizedAndVerifiable(CurveName, SA)

        /// <summary>
        /// Both secp192 curves now carry a curve name rather than none, and a
        /// document signed on them verifies.
        /// </summary>
        [TestCase("secp192k1", "ECDSA-secp192k1-SHA256")]
        [TestCase("secp192r1", "ECDSA-secp192r1-SHA256")]
        public void BothSecp192Curves_AreRecognizedAndVerifiable(String  CurveName,
                                                                 String  SA)
        {

            Assert.That(OCMFSignatureAlgorithm.TryGet(SA)?.CurveName,  Is.EqualTo(CurveName),
                        $"'{SA}' should name a curve this port can verify.");

            var (publicKeyHEX, signatureHEX) = Sign(CurveName, payload);

            var document = scanner.Scan([ $"OCMF|{payload}|{{\"SD\":\"{signatureHEX}\",\"SA\":\"{SA}\"}}" ]).Documents[0];

            Assert.That(validator.Validate(document, publicKeyHEX, "hex"),
                        Is.EqualTo(VerificationResult.ValidSignature),
                        String.Join("; ", document.Errors.Select(error => error.ToString())));

        }

        #endregion

        #region BothSecp192Curves_RejectATamperedPayload(CurveName, SA)

        /// <summary>
        /// Changing the meter reading breaks the signature on these curves too.
        ///
        /// Being able to verify a curve is only worth something if it can also
        /// refuse: a check that always says yes is worse than no check at all.
        /// </summary>
        [TestCase("secp192k1", "ECDSA-secp192k1-SHA256")]
        [TestCase("secp192r1", "ECDSA-secp192r1-SHA256")]
        public void BothSecp192Curves_RejectATamperedPayload(String  CurveName,
                                                             String  SA)
        {

            var (publicKeyHEX, signatureHEX) = Sign(CurveName, payload);

            var tampered = payload.Replace("42.5", "43.5");

            var document = scanner.Scan([ $"OCMF|{tampered}|{{\"SD\":\"{signatureHEX}\",\"SA\":\"{SA}\"}}" ]).Documents[0];

            Assert.That(validator.Validate(document, publicKeyHEX, "hex"),
                        Is.EqualTo(VerificationResult.InvalidSignature));

        }

        #endregion

        #region BothSecp192Curves_RejectAKeyFromTheOtherCurve()

        /// <summary>
        /// A secp192k1 key must not verify a secp192r1 signature.
        ///
        /// The two curves have the same field size, so their keys and signatures
        /// are the same length and neither is rejected on shape alone — which is
        /// exactly why this is worth asserting.
        /// </summary>
        [Test]
        public void BothSecp192Curves_RejectAKeyFromTheOtherCurve()
        {

            var (_,            signatureHEX) = Sign("secp192r1", payload);
            var (wrongKeyHEX,  _)            = Sign("secp192k1", payload);

            var document = scanner.Scan([ $"OCMF|{payload}|{{\"SD\":\"{signatureHEX}\",\"SA\":\"ECDSA-secp192r1-SHA256\"}}" ]).Documents[0];

            Assert.That(validator.Validate(document, wrongKeyHEX, "hex"),
                        Is.Not.EqualTo(VerificationResult.ValidSignature));

        }

        #endregion

        #region TheBrainpoolCurves_RemainUnverified()

        /// <summary>
        /// The brainpool curves stay as ChargyCore.TS leaves them.
        ///
        /// BouncyCastle carries them too, so this is a scope decision rather than
        /// a limitation: only the secp192 pair was asked for. The test records
        /// that, so that the next person does not read the gap as an oversight.
        /// </summary>
        [Test]
        public void TheBrainpoolCurves_RemainUnverified()
        {

            Assert.Multiple(() => {
                Assert.That(OCMFSignatureAlgorithm.TryGet("ECDSA-brainpool256r1-SHA256")?.CurveName,  Is.Null);
                Assert.That(OCMFSignatureAlgorithm.TryGet("ECDSA-brainpool384r1-SHA256")?.CurveName,  Is.Null);
                Assert.That(OCMFSignatureAlgorithm.TryGet("ECDSA-brainpool384r1-SHA384")?.CurveName,  Is.Null);
            });

        }

        #endregion


        #region (private, static) Sign(CurveName, Payload)

        /// <summary>
        /// Sign an OCMF payload the way a meter on the given curve would: the
        /// public key as a DER SubjectPublicKeyInfo, the signature as DER over
        /// the SHA-256 of the payload.
        /// </summary>
        /// <param name="CurveName">The name of an elliptic curve.</param>
        /// <param name="Payload">The raw OCMF payload.</param>
        private static (String PublicKeyHEX, String SignatureHEX) Sign(String  CurveName,
                                                                       String  Payload)
        {

            var curveOID    = ECNamedCurveTable.GetOid(CurveName)
                                  ?? throw new InvalidOperationException($"BouncyCastle does not know the curve '{CurveName}'!");

            var curve       = ECNamedCurveTable.GetByOid(curveOID);

            // Named domain parameters, not plain ones: a key built from plain
            // parameters is written out with the whole curve spelled out inline,
            // while a real energy meter names the curve by its object identifier.
            // Signing with the wrong one would test a key shape nobody emits.
            var domain      = new ECNamedDomainParameters(curveOID, curve);

            var generator   = new ECKeyPairGenerator();
            generator.Init(new ECKeyGenerationParameters(domain, new SecureRandom()));

            var keyPair     = generator.GenerateKeyPair();

            var signer      = new ECDsaSigner();
            signer.Init(true, new ParametersWithRandom((ECPrivateKeyParameters) keyPair.Private, new SecureRandom()));

            var hash        = SHA256.HashData(Encoding.UTF8.GetBytes(Payload));
            var signature   = signer.GenerateSignature(hash);

            var der         = new DerSequence(
                                  new DerInteger(signature[0]),
                                  new DerInteger(signature[1])
                              ).GetDerEncoded();

            var publicKey   = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public).GetDerEncoded();

            return (
                       Convert.ToHexStringLower(publicKey),
                       Convert.ToHexStringLower(der)
                   );

        }

        #endregion


    }

}
