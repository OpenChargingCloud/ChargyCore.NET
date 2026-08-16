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

using cloud.charging.open.chargy.Crypto;
using cloud.charging.open.chargy.Formats.OCMF;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the brainpool curves of OCMF.
    ///
    /// OCMF names three algorithms on the RFC 5639 curves —
    /// "ECDSA-brainpool256r1-SHA256", "ECDSA-brainpool384r1-SHA256" and
    /// "ECDSA-brainpool384r1-SHA384" — and ChargyCore.TS recognises all three
    /// without being able to check any of them, because its JavaScript curve
    /// library does not carry them. BouncyCastle does. This is the same decision
    /// as for the secp192 pair, and the same direction: a record that verifies
    /// there verifies here, and the records treated differently are the ones the
    /// TypeScript implementation declines to judge at all.
    ///
    /// The names are the trap. OCMF writes "brainpool256r1" while RFC 5639 and
    /// the object identifier registry write "brainpoolP256r1" — one letter
    /// apart, and a lookup that misses simply reports a charging session as
    /// unverifiable.
    ///
    /// There are no brainpool OCMF fixtures in either implementation, so the
    /// documents are signed here and verified through the ordinary path. A round
    /// trip proves the wiring; it cannot prove agreement with a third party, and
    /// if a real brainpool record ever turns up it should become a fixture.
    /// </summary>
    [TestFixture]
    public class OCMFBrainpoolTests : AChargyTests
    {

        #region Data

        private const String payload = "{\"FV\":\"1.0\",\"GI\":\"CI-Tests\",\"RD\":[{\"TM\":\"2026-08-16T10:00:00,000+0000 U\",\"RV\":42.5,\"RI\":\"1-b:1.8.0\",\"RU\":\"kWh\"}]}";

        private readonly OCMFDocumentScanner     scanner   = new ();
        private          OCMFSignatureValidator  validator = null!;

        [SetUp]
        public void Setup()
        {
            validator = new OCMFSignatureValidator(I18NDictionary.Default());
        }

        #endregion


        #region EveryBrainpoolAlgorithm_IsRecognizedAndVerifiable(SA, CurveName, HashName)

        /// <summary>
        /// All three brainpool algorithms name a curve now, and a document signed
        /// on that curve with the digest the name prescribes verifies.
        ///
        /// The two 384 bit entries differ only in their digest, and one of them
        /// pairs a 384 bit curve with SHA-256 — cryptographically the wrong
        /// digest for the curve, but meters were built that way and their
        /// signatures have to keep verifying.
        /// </summary>
        /// <param name="SA">The OCMF signature algorithm.</param>
        /// <param name="CurveName">The curve it should name.</param>
        /// <param name="HashName">The digest it prescribes.</param>
        [TestCase("ECDSA-brainpool256r1-SHA256",  "brainpoolP256r1",  "SHA256")]
        [TestCase("ECDSA-brainpool384r1-SHA256",  "brainpoolP384r1",  "SHA256")]
        [TestCase("ECDSA-brainpool384r1-SHA384",  "brainpoolP384r1",  "SHA384")]
        public void EveryBrainpoolAlgorithm_IsRecognizedAndVerifiable(String  SA,
                                                                      String  CurveName,
                                                                      String  HashName)
        {

            Assert.That(OCMFSignatureAlgorithm.TryGet(SA)?.CurveName,  Is.EqualTo(CurveName),
                        $"'{SA}' should name a curve this port can verify.");

            var (publicKeyHEX, signatureHEX) = OCMFTestSigner.Sign(CurveName, payload, HashName);

            var document = scanner.Scan([ $"OCMF|{payload}|{{\"SD\":\"{signatureHEX}\",\"SA\":\"{SA}\"}}" ]).Documents[0];

            Assert.That(validator.Validate(document, publicKeyHEX, "hex"),
                        Is.EqualTo(VerificationResult.ValidSignature),
                        String.Join("; ", document.Errors.Select(error => error.ToString())));

        }

        #endregion

        #region EveryBrainpoolAlgorithm_RejectsATamperedPayload(SA, CurveName, HashName)

        /// <summary>
        /// Changing the meter reading breaks the signature on these curves too.
        ///
        /// Being able to verify a curve is only worth something if it can also
        /// refuse: a check that always says yes is worse than no check at all.
        /// </summary>
        /// <param name="SA">The OCMF signature algorithm.</param>
        /// <param name="CurveName">The curve it names.</param>
        /// <param name="HashName">The digest it prescribes.</param>
        [TestCase("ECDSA-brainpool256r1-SHA256",  "brainpoolP256r1",  "SHA256")]
        [TestCase("ECDSA-brainpool384r1-SHA256",  "brainpoolP384r1",  "SHA256")]
        [TestCase("ECDSA-brainpool384r1-SHA384",  "brainpoolP384r1",  "SHA384")]
        public void EveryBrainpoolAlgorithm_RejectsATamperedPayload(String  SA,
                                                                    String  CurveName,
                                                                    String  HashName)
        {

            var (publicKeyHEX, signatureHEX) = OCMFTestSigner.Sign(CurveName, payload, HashName);

            var tampered = payload.Replace("42.5", "43.5");

            var document = scanner.Scan([ $"OCMF|{tampered}|{{\"SD\":\"{signatureHEX}\",\"SA\":\"{SA}\"}}" ]).Documents[0];

            Assert.That(validator.Validate(document, publicKeyHEX, "hex"),
                        Is.EqualTo(VerificationResult.InvalidSignature));

        }

        #endregion

        #region TheTwo384BitAlgorithms_DoNotAcceptEachOthersDigest()

        /// <summary>
        /// A document signed over SHA-384 does not verify while claiming SHA-256.
        ///
        /// Both names point at the same curve, so nothing about key or signature
        /// length gives this away — only the digest differs, and it is the digest
        /// that is signed. If the hash were taken from the curve rather than from
        /// the algorithm name, this would pass and both 384 bit entries would
        /// silently mean the same thing.
        /// </summary>
        [Test]
        public void TheTwo384BitAlgorithms_DoNotAcceptEachOthersDigest()
        {

            var (publicKeyHEX, signatureHEX) = OCMFTestSigner.Sign("brainpoolP384r1", payload, "SHA384");

            var document = scanner.Scan([ $"OCMF|{payload}|{{\"SD\":\"{signatureHEX}\",\"SA\":\"ECDSA-brainpool384r1-SHA256\"}}" ]).Documents[0];

            Assert.That(validator.Validate(document, publicKeyHEX, "hex"),
                        Is.EqualTo(VerificationResult.InvalidSignature));

        }

        #endregion

        #region ABrainpoolDocument_RejectsAKeyFromTheNISTCurveOfTheSameSize()

        /// <summary>
        /// A secp256r1 key must not verify a brainpoolP256r1 signature.
        ///
        /// The two curves are the same size, so their keys and signatures are the
        /// same length and neither is rejected on shape alone. The key is turned
        /// down because its point does not lie on the curve the algorithm named.
        ///
        /// The second assertion is what makes the first one mean anything. A
        /// curve this library cannot resolve at all is reported as
        /// `InvalidPublicKey` too, so the verdict alone would be satisfied by
        /// the very gap this whole fixture exists to close — it passed before
        /// the brainpool curves were wired up. What has to be true is that the
        /// key was rejected by the point check, and the absent technical detail
        /// is what says so.
        /// </summary>
        [Test]
        public void ABrainpoolDocument_RejectsAKeyFromTheNISTCurveOfTheSameSize()
        {

            var (_,           signatureHEX) = OCMFTestSigner.Sign("brainpoolP256r1", payload);
            var (wrongKeyHEX, _)            = OCMFTestSigner.Sign("secp256r1",       payload);

            var document = scanner.Scan([ $"OCMF|{payload}|{{\"SD\":\"{signatureHEX}\",\"SA\":\"ECDSA-brainpool256r1-SHA256\"}}" ]).Documents[0];

            var status   = validator.Validate(document, wrongKeyHEX, "hex");

            Assert.Multiple(() => {

                Assert.That(status,                                Is.EqualTo(VerificationResult.InvalidPublicKey));

                Assert.That(document.Errors.FirstOrDefault()?.Details,  Is.Null,
                            "The key has to be rejected by the point check, not by a curve this port cannot resolve.");

            });

        }

        #endregion

        #region OCMFSpellsTheCurvesWithoutTheirP(OCMFName, RFCName)

        /// <summary>
        /// Both spellings of a brainpool curve resolve to the same verifier.
        ///
        /// OCMF drops the "P" that RFC 5639, the object identifier registry and
        /// BouncyCastle all carry. Whichever of the two a file happens to use
        /// says nothing about the curve, so both have to arrive at it.
        /// </summary>
        /// <param name="OCMFName">The name as OCMF spells it.</param>
        /// <param name="RFCName">The name as RFC 5639 spells it.</param>
        [TestCase("brainpool256r1",  "brainpoolP256r1")]
        [TestCase("brainpool384r1",  "brainpoolP384r1")]
        public void OCMFSpellsTheCurvesWithoutTheirP(String  OCMFName,
                                                     String  RFCName)
        {

            var fromOCMF = ECCurveVerifier.TryGet(OCMFName);
            var fromRFC  = ECCurveVerifier.TryGet(RFCName);

            Assert.Multiple(() => {
                Assert.That(fromOCMF,             Is.Not.Null);
                Assert.That(fromOCMF,             Is.SameAs(fromRFC));
                Assert.That(fromOCMF?.CurveName,  Is.EqualTo(RFCName));
            });

        }

        #endregion

        #region EveryECDSAAlgorithmOCMFNames_ResolvesToAVerifier()

        /// <summary>
        /// Every OCMF algorithm that hashes before signing names a curve, and
        /// every one of those names reaches a verifier.
        ///
        /// This is the invariant the brainpool work established, and the reason
        /// to state it as a rule rather than curve by curve: the next algorithm
        /// added to the table gets a curve name from whoever adds it, and a name
        /// the verifier cannot resolve would turn a checkable charging session
        /// into an unverifiable one without any test going red.
        /// </summary>
        [Test]
        public void EveryECDSAAlgorithmOCMFNames_ResolvesToAVerifier()
        {

            Assert.Multiple(() => {
                foreach (var algorithm in OCMFSignatureAlgorithm.All.Where(algorithm => !algorithm.SignsMessageDirectly))
                {

                    Assert.That(algorithm.CurveName,                             Is.Not.Null,
                                $"'{algorithm.Name}' hashes before signing and therefore has to name a curve.");

                    Assert.That(ECCurveVerifier.TryGet(algorithm.CurveName),     Is.Not.Null,
                                $"'{algorithm.Name}' names the curve '{algorithm.CurveName}', which no verifier answers to.");

                }
            });

        }

        #endregion

    }

}
