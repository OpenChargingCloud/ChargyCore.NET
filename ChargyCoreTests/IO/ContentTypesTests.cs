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

#endregion

namespace cloud.charging.open.chargy.tests.IO
{

    /// <summary>
    /// Tests for working out what a pile of bytes actually is.
    /// </summary>
    [TestFixture]
    public class ContentTypesTests : AChargyTests
    {

        #region FromContent_RecognizesTheArchiveFixtures(Fixture, ExpectedMIMEType)

        /// <summary>
        /// Every container fixture is recognised from its content alone.
        /// </summary>
        [TestCase("chargeIT/chargeIT-Testdata-02.zip",                                     ContentTypes.Zip)]
        [TestCase("chargeIT/chargeIT-Testdata-02.tar",                                     ContentTypes.Tar)]
        [TestCase("chargeIT/chargeIT-Testdata-02.tar.gz",                                  ContentTypes.GZip)]
        [TestCase("chargeIT/chargeIT-Testdata-02.tar.bz2",                                 ContentTypes.BZip2)]
        [TestCase("SAFE/SAFE-Testdata-02_withXMLNamespace.pdf",                            ContentTypes.PDF)]
        [TestCase("ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_asQRCode.png",                 "image/png")]
        [TestCase("ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_asQRCode.jpg",                 "image/jpeg")]
        [TestCase("ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_asQRCode.svg",                 "image/svg+xml")]
        [TestCase("ChargeTransparencyLive/ChargeTransparencyLiveLink_2.svg",               "image/svg+xml")]
        [TestCase("ALFEN/ALFEN-Testdata-03_SAFEXMLContainer.xml",                          ContentTypes.XML)]
        [TestCase("chargeIT/chargeIT-Testdata-02.chargy",                                  ContentTypes.JSON)]
        public void FromContent_RecognizesTheArchiveFixtures(String  Fixture,
                                                             String  ExpectedMIMEType)
        {

            Assert.That(ContentTypes.FromContent(ReadBinaryFixture(Fixture)),
                        Is.EqualTo(ExpectedMIMEType));

        }

        #endregion

        #region FromContent_PrefersSVGOverPlainXML()

        /// <summary>
        /// An SVG is an XML document, so the more specific answer has to win —
        /// otherwise a QR code image would be handed to the XML parser.
        /// </summary>
        [Test]
        public void FromContent_PrefersSVGOverPlainXML()
        {

            Assert.That(ContentTypes.FromContent(Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\"/>")),
                        Is.EqualTo("image/svg+xml"));

        }

        #endregion

        #region FromContent_OfUnknownData_IsNull()

        /// <summary>
        /// Bytes that match nothing yield no answer, rather than a wrong one.
        /// </summary>
        [Test]
        public void FromContent_OfUnknownData_IsNull()
        {

            Assert.Multiple(() => {
                Assert.That(ContentTypes.FromContent(new Byte[] { 0x01, 0x02, 0x03, 0x04 }),  Is.Null);
                Assert.That(ContentTypes.FromContent([]),                                     Is.Null);
                Assert.That(ContentTypes.FromContent(Encoding.UTF8.GetBytes("OCMF|{...}")),   Is.Null);
            });

        }

        #endregion


        #region Normalize_DropsParametersAndCase()

        /// <summary>
        /// Whoever produced the file decided how to spell its MIME type.
        /// </summary>
        [TestCase("text/xml; charset=utf-8",  "text/xml")]
        [TestCase("TEXT/XML",                 "text/xml")]
        [TestCase("  image/PNG  ",            "image/png")]
        [TestCase("application/json;q=0.9",   "application/json")]
        public void Normalize_DropsParametersAndCase(String  MIMEType,
                                                     String  Expected)
        {

            Assert.That(ContentTypes.Normalize(MIMEType), Is.EqualTo(Expected));

        }

        #endregion

        #region Normalize_OfNothing_IsNull()

        /// <summary>
        /// An absent or empty MIME type stays absent.
        /// </summary>
        [Test]
        public void Normalize_OfNothing_IsNull()
        {

            Assert.Multiple(() => {
                Assert.That(ContentTypes.Normalize(null),   Is.Null);
                Assert.That(ContentTypes.Normalize(""),     Is.Null);
                Assert.That(ContentTypes.Normalize("  "),   Is.Null);
                Assert.That(ContentTypes.Normalize(";x=1"), Is.Null);
            });

        }

        #endregion


        #region ForQRCodeImage_PrefersTheSniffedType()

        /// <summary>
        /// What the bytes say beats what the file claims to be.
        /// </summary>
        [Test]
        public void ForQRCodeImage_PrefersTheSniffedType()
        {

            var fileInfo = new FileInfo("download", ReadBinaryFixture("SimpleURLs/chargy.charging.cloud_QRCode.png"), Type: "application/octet-stream");

            Assert.That(ContentTypes.ForQRCodeImage(fileInfo, "image/png"),
                        Is.EqualTo("image/png"));

        }

        #endregion

        #region ForQRCodeImage_FallsBackToTheFileName()

        /// <summary>
        /// A file dropped in by an EV driver often carries no usable MIME type at
        /// all, so its name is the last thing left to go on.
        /// </summary>
        [Test]
        public void ForQRCodeImage_FallsBackToTheFileName()
        {

            var fileInfo = new FileInfo("chargy.charging.cloud_QRCode.png", ReadOnlyMemory<Byte>.Empty, Type: "application/octet-stream");

            Assert.That(ContentTypes.ForQRCodeImage(fileInfo),
                        Is.EqualTo("image/png"));

        }

        #endregion

        #region IsQRCodeImage_AcceptsTheCommonImageTypes()

        /// <summary>
        /// "image/jpg" is not a registered MIME type, but cameras emit it.
        /// </summary>
        [Test]
        public void IsQRCodeImage_AcceptsTheCommonImageTypes()
        {

            Assert.Multiple(() => {

                Assert.That(ContentTypes.IsQRCodeImage("image/png"),       Is.True);
                Assert.That(ContentTypes.IsQRCodeImage("image/jpeg"),      Is.True);
                Assert.That(ContentTypes.IsQRCodeImage("image/jpg"),       Is.True);
                Assert.That(ContentTypes.IsQRCodeImage("image/svg+xml"),   Is.True);

                Assert.That(ContentTypes.IsQRCodeImage("application/pdf"), Is.False);
                Assert.That(ContentTypes.IsQRCodeImage("image/tiff"),      Is.False);
                Assert.That(ContentTypes.IsQRCodeImage(null),              Is.False);

            });

        }

        #endregion

        #region IsArchive_AcceptsOnlyTheFourContainerFormats()

        /// <summary>
        /// Only containers Chargy can actually unpack count as archives.
        /// </summary>
        [Test]
        public void IsArchive_AcceptsOnlyTheFourContainerFormats()
        {

            Assert.Multiple(() => {

                Assert.That(ContentTypes.IsArchive(ContentTypes.Zip),    Is.True);
                Assert.That(ContentTypes.IsArchive(ContentTypes.Tar),    Is.True);
                Assert.That(ContentTypes.IsArchive(ContentTypes.GZip),   Is.True);
                Assert.That(ContentTypes.IsArchive(ContentTypes.BZip2),  Is.True);

                Assert.That(ContentTypes.IsArchive(ContentTypes.PDF),    Is.False);
                Assert.That(ContentTypes.IsArchive("application/x-7z"),  Is.False);
                Assert.That(ContentTypes.IsArchive(null),                Is.False);

            });

        }

        #endregion


    }

}
