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
using System.Text.RegularExpressions;

using cloud.charging.open.chargy.qrcodes;

#endregion

namespace cloud.charging.open.chargy.tests.IO
{

    /// <summary>
    /// Tests for reading charge transparency data out of QR code images.
    ///
    /// The same ALFEN record exists as PNG, JPEG and SVG. All three have to yield
    /// the very same XML document — a charging station's receipt is trustworthy
    /// only if photographing it, downloading it or printing it are equivalent.
    /// </summary>
    [TestFixture]
    public class QRCodeDecoderTests : AChargyTests
    {

        #region Data

        private readonly QRCodeDecoder decoder = new ();

        private static readonly String[] alfenQRCodes = [
                                                            "ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_asQRCode.png",
                                                            "ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_asQRCode.jpg",
                                                            "ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_asQRCode.svg"
                                                        ];

        #endregion


        #region ALFENQRCodes_AllYieldTheSameSignedXMLDocument(Fixture)

        /// <summary>
        /// Every rendering of the ALFEN QR code holds the same SAFE XML container.
        /// </summary>
        [Test]
        public void ALFENQRCodes_AllYieldTheSameSignedXMLDocument([ValueSource(nameof(alfenQRCodes))] String Fixture)
        {

            var expected  = NormalizeXML(ReadTextFixture("ALFEN/ALFEN-Testdata-03_SAFEXMLContainer.xml"));

            var decoded   = decoder.DecodeQRCode(
                                ReadBinaryFixture(Fixture),
                                MIMETypeOf(Fixture)
                            );

            Assert.That(decoded,                 Is.Not.Null,           $"No QR code was found in '{Fixture}'!");
            Assert.That(NormalizeXML(decoded!),  Is.EqualTo(expected),  $"'{Fixture}' holds a different document!");

        }

        #endregion

        #region SimpleURLQRCode_YieldsTheChargyURL()

        /// <summary>
        /// A QR code may hold nothing but a URL pointing at the transparency data.
        /// </summary>
        [Test]
        public void SimpleURLQRCode_YieldsTheChargyURL()
        {

            var decoded = decoder.DecodeQRCode(
                              ReadBinaryFixture("SimpleURLs/chargy.charging.cloud_QRCode.png"),
                              "image/png"
                          );

            Assert.That(decoded, Is.Not.Null);
            Assert.That(decoded, Does.Contain("chargy.charging.cloud"));

        }

        #endregion

        #region SimpleURLQRCodes_PNGAndSVGAgree()

        /// <summary>
        /// The PNG and the SVG rendering of the same URL agree.
        /// </summary>
        [Test]
        public void SimpleURLQRCodes_PNGAndSVGAgree()
        {

            var fromPNG = decoder.DecodeQRCode(ReadBinaryFixture("SimpleURLs/chargy.charging.cloud_QRCode.png"), "image/png");
            var fromSVG = decoder.DecodeQRCode(ReadBinaryFixture("SimpleURLs/chargy.charging.cloud_QRCode.svg"), "image/svg+xml");

            Assert.That(fromPNG,  Is.Not.Null);
            Assert.That(fromSVG,  Is.EqualTo(fromPNG));

        }

        #endregion

        #region ChargeTransparencyLiveLinkQRCodes_YieldTheSameLiveLink()

        /// <summary>
        /// A charge transparency live link travels as a QR code too, so that an
        /// EV driver can follow a still running charging session from the display
        /// of the charging station.
        /// </summary>
        [Test]
        public void ChargeTransparencyLiveLinkQRCodes_YieldTheSameLiveLink()
        {

            var fromPNG = decoder.DecodeQRCode(ReadBinaryFixture("ChargeTransparencyLive/ChargeTransparencyLiveLink_2.png"), "image/png");
            var fromSVG = decoder.DecodeQRCode(ReadBinaryFixture("ChargeTransparencyLive/ChargeTransparencyLiveLink_2.svg"), "image/svg+xml");

            Assert.That(fromPNG,  Is.Not.Null,  "No QR code was found in the PNG!");
            Assert.That(fromPNG,  Does.Contain("open.charging.cloud"));
            Assert.That(fromSVG,  Is.EqualTo(fromPNG));

        }

        #endregion


        #region DecodeQRCode_WithoutAMIMEType_StillWorks()

        /// <summary>
        /// A file dropped in by an EV driver often carries no MIME type at all,
        /// so the image type has to be recognised from its content.
        /// </summary>
        [Test]
        public void DecodeQRCode_WithoutAMIMEType_StillWorks()
        {

            Assert.Multiple(() => {

                Assert.That(decoder.DecodeQRCode(ReadBinaryFixture("SimpleURLs/chargy.charging.cloud_QRCode.png")),
                            Is.Not.Null);

                Assert.That(decoder.DecodeQRCode(ReadBinaryFixture("SimpleURLs/chargy.charging.cloud_QRCode.svg")),
                            Is.Not.Null);

            });

        }

        #endregion

        #region DecodeQRCode_OfSomethingElse_IsNull()

        /// <summary>
        /// An image without a QR code, and anything that is not an image at all,
        /// yield nothing rather than an exception.
        /// </summary>
        [Test]
        public void DecodeQRCode_OfSomethingElse_IsNull()
        {

            Assert.Multiple(() => {

                Assert.That(decoder.DecodeQRCode(ReadOnlyMemory<Byte>.Empty),                                 Is.Null);
                Assert.That(decoder.DecodeQRCode(Encoding.UTF8.GetBytes("This is not an image!")),            Is.Null);
                Assert.That(decoder.DecodeQRCode(ReadBinaryFixture("chargeIT/chargeIT-Testdata-02.chargy")),  Is.Null);
                Assert.That(decoder.DecodeQRCode(ReadBinaryFixture("SAFE/SAFE-Testdata-02_withXMLNamespace.pdf"), "application/pdf"),
                            Is.Null);

            });

        }

        #endregion


        #region (private, static) NormalizeXML(XML)

        /// <summary>
        /// Reduce runs of whitespace between XML elements, so that a QR code that
        /// dropped the pretty-printing of the original document still compares equal.
        /// </summary>
        /// <param name="XML">An XML document.</param>
        private static String NormalizeXML(String XML)

            => Regex.Replace(
                   Regex.Replace(XML, @">\s+<", "><"),
                   @"\s+",
                   " "
               ).Trim();

        #endregion


    }

}
