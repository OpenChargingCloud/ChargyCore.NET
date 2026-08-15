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

using cloud.charging.open.chargy.IO;
using org.GraphDefined.Vanaheimr.Illias;
using cloud.charging.open.chargy.qrcodes;

#endregion

namespace cloud.charging.open.chargy.tests.IO
{

    /// <summary>
    /// Tests for the front door of the library: working out what an EV driver
    /// actually handed over.
    ///
    /// No charge transparency data format is registered here — those arrive with
    /// the format work. Everything around them has to work regardless: unpacking
    /// containers, reading PDF attachments and QR codes, recognising public key
    /// files, and reporting honestly when a format is missing.
    /// </summary>
    [TestFixture]
    public class ContentFormatDetectorTests : AChargyTests
    {

        #region Data

        private ContentFormatDetector detector = null!;

        #endregion

        #region Setup()

        [SetUp]
        public void Setup()
        {

            detector = new ContentFormatDetector(
                           I18NDictionary.Default(),
                           ChargeTransparencyFormats.None,
                           new PDFAttachmentExtractor(),
                           new QRCodeDecoder()
                       );

        }

        #endregion

        #region (private) FixtureFile(FixtureName)

        /// <summary>
        /// A test fixture as Chargy would receive it from an application.
        /// </summary>
        /// <param name="FixtureName">A fixture path relative to "TestData".</param>
        private static FileInfo FixtureFile(String FixtureName)
        {

            var name = FixtureName[(FixtureName.LastIndexOf('/') + 1)..];

            return new FileInfo(
                       name,
                       ReadBinaryFixture(FixtureName),
                       MIMETypeOf(name)
                   );

        }

        #endregion


        #region NoFiles_ReportThatNothingWasFound()

        /// <summary>
        /// Handing over nothing is answered, not ignored.
        /// </summary>
        [Test]
        public async Task NoFiles_ReportThatNothingWasFound()
        {

            var result = await detector.DetectAndConvertContentFormat([]);

            Assert.That(result, Is.InstanceOf<SessionCryptoResult>());
            Assert.That(((SessionCryptoResult) result).Status,
                        Is.EqualTo(SessionVerificationResult.NoChargeTransparencyRecordsFound));

        }

        #endregion


        #region ABareURL_BecomesAURL()

        /// <summary>
        /// A text file holding nothing but a link becomes a URL — and nothing is
        /// fetched, because no resolver was registered.
        /// </summary>
        [Test]
        public async Task ABareURL_BecomesAURL()
        {

            var result = await detector.DetectAndConvertContentFormat([
                             new FileInfo(
                                 "url.txt",
                                 Encoding.UTF8.GetBytes("https://chargy.charging.cloud/charging-session?id=123#details"),
                                 "text/plain"
                             )
                         ]);

            Assert.That(result, Is.InstanceOf<SimpleURL>());
            Assert.That(((SimpleURL) result).URL,
                        Is.EqualTo("https://chargy.charging.cloud/charging-session?id=123#details"));

        }

        #endregion

        #region AQRCodeHoldingAURL_BecomesAURL()

        /// <summary>
        /// A photograph of a QR code on a charging station becomes the link it
        /// holds — the whole pipeline in one step: image, QR code, text, URL.
        /// </summary>
        [Test]
        public async Task AQRCodeHoldingAURL_BecomesAURL()
        {

            var result = await detector.DetectAndConvertContentFormat([
                             FixtureFile("SimpleURLs/chargy.charging.cloud_QRCode.png")
                         ]);

            Assert.That(result, Is.InstanceOf<SimpleURL>());
            Assert.That(((SimpleURL) result).URL, Does.Contain("chargy.charging.cloud"));

        }

        #endregion


        #region APEMPublicKeyFile_BecomesAPublicKeyLookup()

        /// <summary>
        /// A public key file on its own is a meaningful thing to hand over: the
        /// application remembers the key for the records that follow.
        /// </summary>
        [Test]
        public async Task APEMPublicKeyFile_BecomesAPublicKeyLookup()
        {

            var result = await detector.DetectAndConvertContentFormat([
                             FixtureFile("ChargePoint/Testdata-2020-02/0024b1000002e300_2.pem")
                         ]);

            Assert.That(result, Is.InstanceOf<PublicKeyLookup>());
            Assert.That(((PublicKeyLookup) result).PublicKeys, Has.Count.EqualTo(1));

        }

        #endregion

        #region ModernPublicKeyFiles_AreRecognized(Fixture, OID, Algorithm, KeyType, KeyLength)

        /// <summary>
        /// The post-quantum and Edwards curve public keys are recognised by their
        /// object identifiers, and their key material has the length the standard
        /// prescribes.
        /// </summary>
        [TestCase("001-01_Ed25519.publicKey.pem",    "1.3.101.112",             "Ed25519",   "EdDSA",    32)]
        [TestCase("001-01_Ed448.publicKey.pem",      "1.3.101.113",             "Ed448",     "EdDSA",    57)]
        [TestCase("001-01_ML-DSA-44.publicKey.pem",  "2.16.840.1.101.3.4.3.17", "ML-DSA-44", "ML-DSA", 1312)]
        [TestCase("001-01_ML-DSA-65.publicKey.pem",  "2.16.840.1.101.3.4.3.18", "ML-DSA-65", "ML-DSA", 1952)]
        [TestCase("001-01_ML-DSA-87.publicKey.pem",  "2.16.840.1.101.3.4.3.19", "ML-DSA-87", "ML-DSA", 2592)]
        public async Task ModernPublicKeyFiles_AreRecognized(String  Fixture,
                                                             String  OID,
                                                             String  Algorithm,
                                                             String  KeyType,
                                                             Int32   KeyLength)
        {

            var result = await detector.DetectAndConvertContentFormat([
                             FixtureFile($"OCMF/BET_TariffTextExtension/001/{Fixture}")
                         ]);

            Assert.That(result, Is.InstanceOf<PublicKeyLookup>());

            var publicKeys = ((PublicKeyLookup) result).PublicKeys;

            Assert.That(publicKeys, Has.Count.EqualTo(1));

            var publicKey = publicKeys[0];

            Assert.Multiple(() => {
                Assert.That(publicKey.Algorithm?.OID,   Is.EqualTo(OID));
                Assert.That(publicKey.Algorithm?.Name,  Is.EqualTo(Algorithm));
                Assert.That(publicKey.Type?.Name,       Is.EqualTo(KeyType));
                Assert.That(publicKey.Value,            Has.Length.EqualTo(KeyLength * 2));
            });

        }

        #endregion

        #region SeveralPublicKeyFiles_BecomeOneLookup()

        /// <summary>
        /// Several key files combine into a single lookup.
        /// </summary>
        [Test]
        public async Task SeveralPublicKeyFiles_BecomeOneLookup()
        {

            var result = await detector.DetectAndConvertContentFormat([
                             FixtureFile("ChargePoint/Testdata-2020-02/0024b1000002e300_2.pem"),
                             FixtureFile("ChargePoint/Testdata-secp256r1/1/compressed/0024b10000027b29_1-publicKey.pem")
                         ]);

            Assert.That(result, Is.InstanceOf<PublicKeyLookup>());
            Assert.That(((PublicKeyLookup) result).PublicKeys, Has.Count.EqualTo(2));

        }

        #endregion

        #region AHexEncodedPublicKeyFile_BecomesAPublicKeyLookup()

        /// <summary>
        /// A public key may also arrive as a hexadecimal blob rather than as PEM,
        /// in which case the file name has to carry the intent.
        /// </summary>
        [Test]
        public async Task AHexEncodedPublicKeyFile_BecomesAPublicKeyLookup()
        {

            var result = await detector.DetectAndConvertContentFormat([
                             FixtureFile("OCMF/OCMF-Testdata-01_publicKey.txt")
                         ]);

            Assert.That(result, Is.InstanceOf<PublicKeyLookup>());
            Assert.That(((PublicKeyLookup) result).PublicKeys, Has.Count.EqualTo(1));

        }

        #endregion

        #region RawKeyMaterial_IsNotMistakenForAPublicKeyFile()

        /// <summary>
        /// A file holding bare key material — no ASN.1 structure around it — is
        /// deliberately *not* taken for a public key file.
        ///
        /// The ".hex" fixtures next to the modern OCMF test data hold exactly
        /// that: 32 bytes of an Ed25519 key and nothing else. Sixty-four
        /// hexadecimal characters could be anything, so Chargy only recognises a
        /// key file when the content actually is a SubjectPublicKeyInfo. Those
        /// files are handed to the OCMF verification as a key, never detected as one.
        /// </summary>
        [Test]
        public void RawKeyMaterial_IsNotMistakenForAPublicKeyFile()
        {

            var rawKey = ReadTextFixture("OCMF/BET_TariffTextExtension/001/001-01_Ed25519.publicKey.hex");

            Assert.Multiple(() => {

                Assert.That(Crypto.PublicKeyParser.LooksLikeAPublicKeyFile("001-01_Ed25519.publicKey.hex", rawKey),
                            Is.False);

                Assert.That(PublicKeyFiles.TryGetPublicKeyHEX("001-01_Ed25519.publicKey.hex", rawKey),
                            Is.Null);

            });

        }

        #endregion


        #region AnArchive_IsUnpackedBeforeItsContentIsJudged()

        /// <summary>
        /// A container is unpacked before anything is decided about its content:
        /// all four chargeIT containers hold the same record, so all four have to
        /// fail in the same way while no format is registered.
        /// </summary>
        [TestCase("chargeIT/chargeIT-Testdata-02.zip")]
        [TestCase("chargeIT/chargeIT-Testdata-02.tar")]
        [TestCase("chargeIT/chargeIT-Testdata-02.tar.gz")]
        [TestCase("chargeIT/chargeIT-Testdata-02.tar.bz2")]
        [TestCase("chargeIT/chargeIT-Testdata-02.chargy")]
        public async Task AnArchive_IsUnpackedBeforeItsContentIsJudged(String Fixture)
        {

            var result = await detector.DetectAndConvertContentFormat([ FixtureFile(Fixture) ]);

            // The chargeIT format is not registered yet, so the honest answer is
            // "this is a format I was not built with" — and crucially the same
            // answer for every container.
            Assert.That(result, Is.InstanceOf<SessionCryptoResult>());
            Assert.That(((SessionCryptoResult) result).Status,
                        Is.EqualTo(SessionVerificationResult.UnknownCTRFormat));

        }

        #endregion

        #region ANestedArchive_IsUnpackedRepeatedly()

        /// <summary>
        /// A ZIP of "tar.bz2" payloads has to be unpacked more than once, or the
        /// charging data would still be packed when the formats are asked about it.
        /// </summary>
        [Test]
        public void ANestedArchive_IsUnpackedRepeatedly()
        {

            var expanded = detector.DecompressFiles([
                               FixtureFile("ChargePoint/Testdata-secp256r1/1/all-combined/Testdata-secp256r1.zip")
                           ]);

            Assert.That(expanded, Is.Not.Empty);

            foreach (var file in expanded)
                Assert.That(ContentTypes.IsArchive(ContentTypes.FromContent(file.Data.Span)),
                            Is.False,
                            $"'{file.Name}' is still an archive!");

        }

        #endregion

        #region AChargePointArchive_CombinesTheRecordAndItsSignature()

        /// <summary>
        /// ChargePoint ships the record and its detached signature as two files.
        /// They only mean something together, so they are combined into one — and
        /// the record's exact bytes are carried along, because that is what was signed.
        /// </summary>
        [Test]
        public void AChargePointArchive_CombinesTheRecordAndItsSignature()
        {

            var expanded = detector.DecompressFiles([
                               FixtureFile("ChargePoint/Testdata-2020-02/0024b1000002e300_2_123017065_payload.tar.bz2")
                           ]);

            Assert.That(expanded, Has.Count.EqualTo(1),
                        "The two files of the archive should have been combined into one!");

            var json       = Newtonsoft.Json.Linq.JObject.Parse(expanded[0].AsText());
            var original64 = (String?) json["original"];
            var signature  = (String?) json["signature"];

            Assert.Multiple(() => {

                Assert.That(original64,  Is.Not.Null.And.Not.Empty,
                            "The exact bytes that were signed should have been kept!");

                Assert.That(signature,   Is.Not.Null.And.Not.Empty,
                            "The detached signature should have been kept!");

            });

            // The preserved original has to be the untouched "secrrct" file.
            var original = Encoding.UTF8.GetString(Convert.FromBase64String(original64!));

            Assert.That(original.TrimStart(), Does.StartWith("{"));

        }

        #endregion


        #region APDFInvoice_YieldsItsEmbeddedRecord()

        /// <summary>
        /// A PDF invoice is opened and its embedded charge transparency record is
        /// what gets judged, not the PDF.
        /// </summary>
        [Test]
        public void APDFInvoice_YieldsItsEmbeddedRecord()
        {

            var expanded = detector.ExpandPDFAttachments([
                               FixtureFile("SAFE/SAFE-Testdata-02_withXMLNamespace.pdf")
                           ]);

            Assert.That(expanded, Has.Count.EqualTo(1));

            Assert.Multiple(() => {
                Assert.That(expanded[0].Name,  Does.EndWith(".xml"));
                Assert.That(expanded[0].Type,  Is.EqualTo(ContentTypes.XML));
                Assert.That(expanded[0].Info,  Does.Contain("PDF/A-3"));
            });

        }

        #endregion

        #region WithoutAPDFReader_ThePDFIsPassedThrough()

        /// <summary>
        /// Without a PDF reader the document is passed through rather than lost.
        /// </summary>
        [Test]
        public void WithoutAPDFReader_ThePDFIsPassedThrough()
        {

            var withoutReader = new ContentFormatDetector(I18NDictionary.Default());

            var expanded      = withoutReader.ExpandPDFAttachments([
                                    FixtureFile("SAFE/SAFE-Testdata-02_withXMLNamespace.pdf")
                                ]);

            Assert.That(expanded,          Has.Count.EqualTo(1));
            Assert.That(expanded[0].Name,  Does.EndWith(".pdf"));

        }

        #endregion

        #region WithoutAQRCodeReader_TheImageIsPassedThrough()

        /// <summary>
        /// Without a QR code reader the image is passed through untouched, exactly
        /// as ChargyCore.TS does when its optional image modules are absent.
        /// </summary>
        [Test]
        public void WithoutAQRCodeReader_TheImageIsPassedThrough()
        {

            var withoutReader = new ContentFormatDetector(I18NDictionary.Default());

            var expanded      = withoutReader.ExpandQRCodeImages([
                                    FixtureFile("SimpleURLs/chargy.charging.cloud_QRCode.png")
                                ]);

            Assert.That(expanded,          Has.Count.EqualTo(1));
            Assert.That(expanded[0].Name,  Does.EndWith(".png"));

        }

        #endregion

        #region AQRCodeImage_IsNamedAfterWhatItHolds()

        /// <summary>
        /// The text taken out of a QR code is named after what it turned out to
        /// be, so that the format detection sees a sensible file name rather than
        /// "photo.png".
        /// </summary>
        [Test]
        public void AQRCodeImage_IsNamedAfterWhatItHolds()
        {

            var expanded = detector.ExpandQRCodeImages([
                               FixtureFile("ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_asQRCode.png")
                           ]);

            Assert.That(expanded,          Has.Count.EqualTo(1));
            Assert.That(expanded[0].Name,  Is.EqualTo("ALFEN-Testdata-03_SAFEXMLContainer_asQRCode.xml"));
            Assert.That(expanded[0].Info,  Is.EqualTo("Text extracted from QR code image"));

        }

        #endregion

        #region AnImageWithoutAQRCode_IsReportedAsSuch()

        /// <summary>
        /// An image that holds no QR code is reported as such rather than
        /// silently dropped.
        /// </summary>
        [Test]
        public void AnImageWithoutAQRCode_IsReportedAsSuch()
        {

            // A plain white PNG.
            var blankPNG = Convert.FromBase64String(
                               "iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAFUlEQVR42mP8" +
                               "/5+hnoEIwDiqkL4KAcT9A/0IjPWfAAAAAElFTkSuQmCC"
                           );

            var expanded = detector.ExpandQRCodeImages([
                               new FileInfo("blank.png", blankPNG, "image/png")
                           ]);

            Assert.That(expanded,           Has.Count.EqualTo(1));
            Assert.That(expanded[0].Error,  Is.EqualTo("No QR code with charge transparency data found!"));

        }

        #endregion


        #region CollectPublicKeys_PairsAKeyWithItsRecordByFileName()

        /// <summary>
        /// A key file and the record it belongs to are paired through their file
        /// names, because the record does not carry the key that signed it — and
        /// should not.
        /// </summary>
        [Test]
        public void CollectPublicKeys_PairsAKeyWithItsRecordByFileName()
        {

            var publicKeys = detector.CollectPublicKeys([
                                 FixtureFile("ChargePoint/Testdata-2020-02/0024b1000002e300_2.pem")
                             ]);

            Assert.That(publicKeys,                                  Has.Count.EqualTo(1));
            Assert.That(publicKeys.ContainsKey("0024b1000002e300_2"), Is.True,
                        $"Expected the key to be filed under '0024b1000002e300_2', but found: {String.Join(", ", publicKeys.Keys)}");

        }

        #endregion

        #region IdFromFileName_StripsTheExtensionAndTheMarker(FileName, Expected)

        /// <summary>
        /// Every spelling of "public key" in a file name reduces to the same
        /// identifier as the record it belongs to.
        /// </summary>
        [TestCase("0024b1000002e300_2-publicKey.pem",   "0024b1000002e300_2")]
        [TestCase("0024b1000002e300_2_publicKey.pem",   "0024b1000002e300_2")]
        [TestCase("0024b1000002e300_2-public-key.txt",  "0024b1000002e300_2")]
        [TestCase("0024b1000002e300_2.chargy",          "0024b1000002e300_2")]
        [TestCase("0024b1000002e300_2.tar.bz2",         "0024b1000002e300_2")]
        [TestCase("001-01_Ed25519.publicKey.pem",       "001-01_Ed25519")]
        [TestCase("noExtension",                        "noExtension")]
        public void IdFromFileName_StripsTheExtensionAndTheMarker(String  FileName,
                                                                  String  Expected)
        {

            Assert.That(PublicKeyFiles.IdFromFileName(FileName), Is.EqualTo(Expected));

        }

        #endregion


        #region AnUnknownFile_IsReportedAsUnreadable()

        /// <summary>
        /// A file that is nothing Chargy knows yields a result saying so, rather
        /// than nothing at all.
        /// </summary>
        [Test]
        public async Task AnUnknownFile_IsReportedAsUnreadable()
        {

            var result = await detector.DetectAndConvertContentFormat([
                             new FileInfo("mystery.dat", new Byte[] { 0x01, 0x02, 0x03, 0x04 })
                         ]);

            Assert.That(result, Is.InstanceOf<SessionCryptoResult>());
            Assert.That(((SessionCryptoResult) result).Status,
                        Is.EqualTo(SessionVerificationResult.InvalidSessionFormat));

        }

        #endregion

        #region AnUnregisteredFormat_IsReportedHonestly()

        /// <summary>
        /// An OCMF document while the OCMF format is not registered has to say
        /// "I was not built with this", not "this is invalid". The difference
        /// matters: one is a deployment problem, the other an accusation against
        /// the charging station.
        /// </summary>
        [Test]
        public async Task AnUnregisteredFormat_IsReportedHonestly()
        {

            var result = await detector.DetectAndConvertContentFormat([
                             new FileInfo(
                                 "session.ocmf",
                                 Encoding.UTF8.GetBytes("OCMF|{\"FV\":\"1.0\"}|{\"SD\":\"…\"}"),
                                 "application/ocmf"
                             )
                         ]);

            Assert.That(result, Is.InstanceOf<SessionCryptoResult>());
            Assert.That(((SessionCryptoResult) result).Status,
                        Is.EqualTo(SessionVerificationResult.UnknownCTRFormat));

        }

        #endregion


    }

}
