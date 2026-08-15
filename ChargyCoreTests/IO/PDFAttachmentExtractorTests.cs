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

#endregion

namespace cloud.charging.open.chargy.tests.IO
{

    /// <summary>
    /// Tests for reading the charge transparency data embedded in a PDF invoice.
    ///
    /// PDF/A-3 is what lets a charge point operator hand an EV driver a single
    /// document that is both a readable receipt and a verifiable record.
    /// </summary>
    [TestFixture]
    public class PDFAttachmentExtractorTests : AChargyTests
    {

        #region Data

        private const String safePDF = "SAFE/SAFE-Testdata-02_withXMLNamespace.pdf";

        private readonly PDFAttachmentExtractor extractor = new ();

        #endregion


        #region SAFEInvoice_YieldsItsEmbeddedTransparencyRecord()

        /// <summary>
        /// The SAFE test invoice carries its charge transparency record as an
        /// embedded XML file.
        /// </summary>
        [Test]
        public void SAFEInvoice_YieldsItsEmbeddedTransparencyRecord()
        {

            var attachments = extractor.ExtractAttachments(ReadBinaryFixture(safePDF)).ToArray();

            Assert.That(attachments, Has.Length.EqualTo(1),
                        "The invoice should carry exactly one charge transparency attachment!");

            var attachment = attachments[0];

            Assert.Multiple(() => {
                Assert.That(attachment.Name,        Does.EndWith(".xml"));
                Assert.That(attachment.Type,        Is.EqualTo(ContentTypes.XML));
                Assert.That(attachment.Info,        Is.EqualTo("A XML file extracted from a PDF/A-3 or newer attachment"));
                Assert.That(attachment.Data.Length, Is.GreaterThan(0));
            });

        }

        #endregion

        #region SAFEInvoice_AttachmentIsTheSignedXMLDocument()

        /// <summary>
        /// The extracted attachment is a SAFE XML container — the same document
        /// the standalone XML fixture holds, so verifying either has to give the
        /// same answer.
        /// </summary>
        [Test]
        public void SAFEInvoice_AttachmentIsTheSignedXMLDocument()
        {

            var attachment = extractor.ExtractAttachments(ReadBinaryFixture(safePDF)).Single();
            var xml        = Encoding.UTF8.GetString(attachment.Data.Span).Trim();

            Assert.Multiple(() => {

                Assert.That(xml,  Does.StartWith("<?xml").Or.StartWith("<"),
                            "The attachment should be an XML document!");

                Assert.That(xml,  Does.Contain("http://transparenz.software/schema/2018/07"),
                            "The attachment should be a SAFE transparency XML container!");

                Assert.That(xml,  Does.Contain("signedData").IgnoreCase,
                            "The attachment should carry signed data!");

            });

        }

        #endregion

        #region SAFEInvoice_IsRecognizedAsAPDF()

        /// <summary>
        /// The invoice is recognised as a PDF from its content alone.
        /// </summary>
        [Test]
        public void SAFEInvoice_IsRecognizedAsAPDF()
        {

            Assert.That(ContentTypes.FromContent(ReadBinaryFixture(safePDF)),
                        Is.EqualTo(ContentTypes.PDF));

        }

        #endregion


        #region ADocumentThatIsNotAPDF_YieldsNoAttachments()

        /// <summary>
        /// Anything that is not a PDF yields no attachments rather than an exception.
        /// </summary>
        [Test]
        public void ADocumentThatIsNotAPDF_YieldsNoAttachments()
        {

            Assert.Multiple(() => {

                Assert.That(extractor.ExtractAttachments(Encoding.UTF8.GetBytes("This is not a PDF!")),
                            Is.Empty);

                Assert.That(extractor.ExtractAttachments(ReadOnlyMemory<Byte>.Empty),
                            Is.Empty);

                Assert.That(extractor.ExtractAttachments(ReadBinaryFixture("chargeIT/chargeIT-Testdata-02.chargy")),
                            Is.Empty);

            });

        }

        #endregion

        #region ATruncatedPDF_YieldsNoAttachments()

        /// <summary>
        /// A PDF that was cut short must not throw: an EV driver's incomplete
        /// download deserves an error message, not a crash.
        /// </summary>
        [Test]
        public void ATruncatedPDF_YieldsNoAttachments()
        {

            var truncated = ReadBinaryFixture(safePDF).AsMemory(0, 4096);

            Assert.That(extractor.ExtractAttachments(truncated), Is.Empty);

        }

        #endregion

        #region TryOpen_OfANonPDF_Fails()

        /// <summary>
        /// Opening something that does not begin with the PDF header fails.
        /// </summary>
        [Test]
        public void TryOpen_OfANonPDF_Fails()
        {

            Assert.Multiple(() => {
                Assert.That(PDFDocument.TryOpen(Encoding.UTF8.GetBytes("%PD"),               out _),  Is.False);
                Assert.That(PDFDocument.TryOpen(Encoding.UTF8.GetBytes("<?xml version=?>"),  out _),  Is.False);
                Assert.That(PDFDocument.TryOpen(ReadBinaryFixture(safePDF),                  out _),  Is.True);
            });

        }

        #endregion

        #region SAFEInvoice_IsNotEncrypted()

        /// <summary>
        /// An encrypted PDF yields nothing, so the test invoice had better not
        /// look encrypted.
        /// </summary>
        [Test]
        public void SAFEInvoice_IsNotEncrypted()
        {

            Assert.That(PDFDocument.TryOpen(ReadBinaryFixture(safePDF), out var document),  Is.True);
            Assert.That(document!.IsEncrypted,                                              Is.False);

        }

        #endregion


    }

}
