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
using System.Xml.Linq;

using cloud.charging.open.chargy.Formats.OCMF;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for finding OCMF documents in a piece of text and checking their
    /// signatures.
    /// </summary>
    [TestFixture]
    public class OCMFScannerTests : AChargyTests
    {

        #region Data

        private readonly OCMFDocumentScanner     scanner   = new ();
        private          OCMFSignatureValidator  validator = null!;

        [SetUp]
        public void Setup()
        {
            validator = new OCMFSignatureValidator(I18NDictionary.Default());
        }

        #endregion

        #region (private, static) SignedDataOf(Fixture)

        /// <summary>
        /// The signed data of a SAFE XML container, which for these fixtures is
        /// plain OCMF text.
        /// </summary>
        /// <param name="Fixture">A fixture path relative to "TestData".</param>
        private static IEnumerable<String> SignedDataOf(String Fixture)

            => XDocument.Parse(ReadTextFixture(Fixture)).
                         Descendants().
                         Where  (element => element.Name.LocalName == "signedData").
                         Select (element => element.Value.Trim());

        #endregion


        #region Testdata01_IsFound()

        /// <summary>
        /// The OCMF test data holds exactly one document.
        /// </summary>
        [Test]
        public void Testdata01_IsFound()
        {

            var result = scanner.Scan([ ReadTextFixture("OCMF/OCMF-Testdata-01.ocmf") ]);

            Assert.That(result.Success,          Is.True, result.ErrorMessage);
            Assert.That(result.Documents,        Has.Count.EqualTo(1));

            var document = result.Documents[0];

            Assert.Multiple(() => {
                Assert.That(document.RawPayload,              Does.StartWith("{").And.EndWith("}"));
                Assert.That((String?) document.Payload["FV"], Is.EqualTo("1.0"));
                Assert.That((String?) document.Payload["GI"], Is.EqualTo("SEAL AG"));
                Assert.That(document.SignatureData,           Does.StartWith("3044"));
            });

        }

        #endregion

        #region Testdata01_PayloadIsPreservedCharacterForCharacter()

        /// <summary>
        /// The raw payload is the exact text between the two separators.
        ///
        /// This is what the signature covers, so it must never be rebuilt from
        /// the parsed JSON: reordering a key or changing the spacing would break
        /// a perfectly good signature.
        /// </summary>
        [Test]
        public void Testdata01_PayloadIsPreservedCharacterForCharacter()
        {

            var fixture   = ReadTextFixture("OCMF/OCMF-Testdata-01.ocmf");
            var expected  = fixture[(fixture.IndexOf('|') + 1)..fixture.LastIndexOf('|')];

            var document  = scanner.Scan([ fixture ]).Documents[0];

            Assert.That(document.RawPayload, Is.EqualTo(expected));

        }

        #endregion

        #region Testdata01_HashesThePayloadWithSHA256()

        /// <summary>
        /// The default OCMF algorithm hashes the payload with SHA-256.
        /// </summary>
        [Test]
        public void Testdata01_HashesThePayloadWithSHA256()
        {

            var document = scanner.Scan([ ReadTextFixture("OCMF/OCMF-Testdata-01.ocmf") ]).Documents[0];

            var expected = Convert.ToHexStringLower(
                               SHA256.HashData(Encoding.UTF8.GetBytes(document.RawPayload))
                           );

            Assert.Multiple(() => {
                Assert.That(document.HashValue,                       Is.EqualTo(expected));
                Assert.That(document.SignatureAlgorithm?.Name,        Is.EqualTo("ECDSA-secp256r1-SHA256"));
                Assert.That(document.SignatureRS,                     Is.Not.Null);
                Assert.That(document.ValidationStatus,                Is.EqualTo(VerificationResult.Unvalidated));
            });

        }

        #endregion

        #region Testdata01_VerifiesAgainstItsPublicKey()

        /// <summary>
        /// The signature verifies against the public key that ships with it.
        /// </summary>
        [Test]
        public void Testdata01_VerifiesAgainstItsPublicKey()
        {

            var document   = scanner.Scan([ ReadTextFixture("OCMF/OCMF-Testdata-01.ocmf") ]).Documents[0];
            var publicKey  = ReadTextFixture("OCMF/OCMF-Testdata-01_publicKey.txt");

            Assert.That(validator.Validate(document, publicKey),
                        Is.EqualTo(VerificationResult.ValidSignature),
                        String.Join("; ", document.Errors.Select(error => error.ToString())));

        }

        #endregion

        #region Testdata01_ATamperedPayloadFails()

        /// <summary>
        /// Changing a single digit of the meter reading invalidates the signature.
        ///
        /// This is the whole promise of the format, so it is worth asserting
        /// rather than assuming.
        /// </summary>
        [Test]
        public void Testdata01_ATamperedPayloadFails()
        {

            var tampered   = ReadTextFixture("OCMF/OCMF-Testdata-01.ocmf").Replace("268.978", "268.979");
            var document   = scanner.Scan([ tampered ]).Documents[0];
            var publicKey  = ReadTextFixture("OCMF/OCMF-Testdata-01_publicKey.txt");

            Assert.That(validator.Validate(document, publicKey),
                        Is.EqualTo(VerificationResult.InvalidSignature));

        }

        #endregion

        #region Testdata01_WithoutAPublicKey_IsUnverifiable()

        /// <summary>
        /// Without a public key nothing can be concluded — which is a different
        /// statement from "the signature is wrong".
        /// </summary>
        [Test]
        public void Testdata01_WithoutAPublicKey_IsUnverifiable()
        {

            var document = scanner.Scan([ ReadTextFixture("OCMF/OCMF-Testdata-01.ocmf") ]).Documents[0];

            Assert.That(validator.Validate(document, null),
                        Is.EqualTo(VerificationResult.PublicKeyNotFound));

        }

        #endregion


        #region SAFEContainer_HoldsSeveralOCMFDocuments()

        /// <summary>
        /// A SAFE XML container carries one OCMF document per reading, and all of
        /// them have to be found in one pass.
        /// </summary>
        [Test]
        public void SAFEContainer_HoldsSeveralOCMFDocuments()
        {

            var result = scanner.Scan(SignedDataOf("SAFE/SAFE-Testdata-02_withXMLNamespace.xml"));

            Assert.That(result.Success,    Is.True, result.ErrorMessage);
            Assert.That(result.Documents,  Has.Count.EqualTo(3));

            foreach (var document in result.Documents)
                Assert.That(document.RawPayload, Does.StartWith("{").And.EndWith("}"));

        }

        #endregion

        #region SAFEContainer_AllDocumentsVerify(Fixture)

        /// <summary>
        /// Every reading in the SAFE container verifies, whichever way the
        /// container declared its XML namespace.
        /// </summary>
        [TestCase("SAFE/SAFE-Testdata-02_withXMLNamespace.xml")]
        [TestCase("SAFE/SAFE-Testdata-02_withoutXMLNamespace.xml")]
        [TestCase("SAFE/SAFE-Testdata-02_emptyXMLNamespace.xml")]
        public void SAFEContainer_AllDocumentsVerify(String Fixture)
        {

            var documents  = scanner.Scan(SignedDataOf(Fixture)).Documents;
            var publicKey  = PublicKeyOf(Fixture);

            Assert.That(documents, Is.Not.Empty);

            Assert.Multiple(() => {
                foreach (var document in documents)
                    Assert.That(validator.Validate(document, publicKey),
                                Is.EqualTo(VerificationResult.ValidSignature),
                                String.Join("; ", document.Errors.Select(error => error.ToString())));
            });

        }

        #endregion

        #region (private, static) PublicKeyOf(Fixture)

        /// <summary>
        /// The public key a SAFE XML container carries.
        /// </summary>
        /// <param name="Fixture">A fixture path relative to "TestData".</param>
        private static String? PublicKeyOf(String Fixture)

            => XDocument.Parse(ReadTextFixture(Fixture)).
                         Descendants().
                         Where  (element => element.Name.LocalName == "publicKey").
                         Select (element => System.Text.RegularExpressions.Regex.Replace(element.Value.Trim(), @"\s+", "")).
                         FirstOrDefault();

        #endregion


        #region AnUnknownAlgorithm_IsReportedAsSuch()

        /// <summary>
        /// An algorithm Chargy does not know is reported as an unknown signature
        /// format, not as a bad signature.
        /// </summary>
        [Test]
        public void AnUnknownAlgorithm_IsReportedAsSuch()
        {

            var document = scanner.Scan([ "OCMF|{\"FV\":\"1.0\"}|{\"SD\":\"3044\",\"SA\":\"ECDSA-nonesuch-SHA256\"}" ]).Documents[0];

            Assert.That(document.ValidationStatus, Is.EqualTo(VerificationResult.UnknownSignatureFormat));

        }

        #endregion

        #region ARecognizedButUnsupportedCurve_IsNotReportedAsABadSignature()

        /// <summary>
        /// Chargy recognises more OCMF algorithm names than it can verify — the
        /// brainpool curves among them. Those have to be reported as a key
        /// problem, because calling them an invalid signature would accuse a
        /// charging station of something this library simply cannot check.
        /// </summary>
        [Test]
        public void ARecognizedButUnsupportedCurve_IsNotReportedAsABadSignature()
        {

            var document = scanner.Scan([ "OCMF|{\"FV\":\"1.0\"}|{\"SD\":\"3006020101020102\",\"SA\":\"ECDSA-brainpool256r1-SHA256\"}" ]).Documents[0];

            Assert.That(document.SignatureAlgorithm?.Name,  Is.EqualTo("ECDSA-brainpool256r1-SHA256"));
            Assert.That(validator.Validate(document, "abcdef"),
                        Is.EqualTo(VerificationResult.InvalidPublicKey));

        }

        #endregion

        #region TheWrongDigestCombinations_AreKeptAsTheyAre()

        /// <summary>
        /// Two OCMF combinations pair a 384 bit curve with SHA-256, which is the
        /// wrong digest for that curve. Meters signed real charging sessions that
        /// way, so the mapping must stay wrong or those receipts stop verifying.
        /// </summary>
        [Test]
        public void TheWrongDigestCombinations_AreKeptAsTheyAre()
        {

            Assert.Multiple(() => {

                Assert.That(OCMFSignatureAlgorithm.TryGet("ECDSA-secp384r1-SHA256")?.HashName,       Is.EqualTo("SHA256"));
                Assert.That(OCMFSignatureAlgorithm.TryGet("ECDSA-brainpool384r1-SHA256")?.HashName,  Is.EqualTo("SHA256"));

                // ..., while the non-standard variants use the matching digest.
                Assert.That(OCMFSignatureAlgorithm.TryGet("ECDSA-secp384r1-SHA384")?.HashName,       Is.EqualTo("SHA384"));
                Assert.That(OCMFSignatureAlgorithm.TryGet("ECDSA-secp521r1-SHA512")?.HashName,       Is.EqualTo("SHA512"));

            });

        }

        #endregion

        #region NoOCMFData_IsReported()

        /// <summary>
        /// Text without OCMF documents yields a reason rather than nothing.
        /// </summary>
        [Test]
        public void NoOCMFData_IsReported()
        {

            Assert.Multiple(() => {
                Assert.That(scanner.Scan([ "This is not OCMF data." ]).ErrorMessage,  Is.EqualTo("No valid OCMF data found!"));
                Assert.That(scanner.Scan([ "OCMF|{not json}|{}" ]).ErrorMessage,      Does.Contain("not a valid JSON document"));
            });

        }

        #endregion


    }

}
