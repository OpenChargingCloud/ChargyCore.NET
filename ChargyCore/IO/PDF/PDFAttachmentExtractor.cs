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

namespace cloud.charging.open.chargy.IO.PDF
{

    /// <summary>
    /// A file embedded in a PDF document.
    /// </summary>
    /// <param name="Name">The name of the embedded file.</param>
    /// <param name="Data">The contents of the embedded file.</param>
    /// <param name="Subtype">The MIME type the PDF claims the file has, if any.</param>
    public readonly record struct PDFEmbeddedFile(String                Name,
                                                  ReadOnlyMemory<Byte>  Data,
                                                  String?               Subtype = null)
    {

        /// <summary>Return a text representation of this embedded file.</summary>
        public override String ToString()

            => $"{Name} ({Data.Length} byte(s))";

    }


    /// <summary>
    /// Extracts the charge transparency data embedded in a PDF invoice.
    /// </summary>
    public interface IPDFAttachmentExtractor
    {

        /// <summary>
        /// Extract the attachments of a PDF document that could hold charge
        /// transparency data.
        /// </summary>
        /// <param name="Data">The bytes of a PDF file.</param>
        IEnumerable<FileInfo> ExtractAttachments(ReadOnlyMemory<Byte> Data);

    }


    /// <summary>
    /// Reads the files embedded in a PDF/A-3 document.
    ///
    /// A charge point operator can hand an EV driver a single PDF that is both a
    /// human-readable invoice and a verifiable charge transparency record: the
    /// record travels as an embedded attachment. Chargy therefore looks inside
    /// every PDF it is given before deciding it is not transparency data.
    /// </summary>
    public class PDFAttachmentExtractor : IPDFAttachmentExtractor
    {

        #region Data

        /// <summary>
        /// Which embedded file types are taken out of a PDF/A-3 container.
        ///
        /// Deliberately a closed list: a PDF invoice may also carry a company
        /// logo or a ZUGFeRD bookkeeping file, and handing those to the charge
        /// transparency format detection would only produce noise.
        /// </summary>
        private static readonly (String Extension, String Type, String Info)[] attachmentTypes = [
            (".chargy",  ContentTypes.Chargy,  "A CHARGY file extracted from a PDF/A-3 or newer attachment"),
            (".xml",     ContentTypes.XML,     "A XML file extracted from a PDF/A-3 or newer attachment"),
            (".json",    ContentTypes.JSON,    "A JSON file extracted from a PDF/A-3 or newer attachment"),
            (".csv",     ContentTypes.CSV,     "A CSV file extracted from a PDF/A-3 or newer attachment")
        ];

        #endregion


        #region ExtractAttachments(Data)

        /// <summary>
        /// Extract the attachments of a PDF document that could hold charge
        /// transparency data.
        /// </summary>
        /// <param name="Data">The bytes of a PDF file.</param>
        public IEnumerable<FileInfo> ExtractAttachments(ReadOnlyMemory<Byte> Data)
        {

            if (!PDFDocument.TryOpen(Data, out var document))
                return [];

            var attachments = new List<FileInfo>();

            foreach (var embeddedFile in document.EmbeddedFiles())
            {

                var attachmentType = attachmentTypes.FirstOrDefault(
                                         candidate => embeddedFile.Name.EndsWith(
                                                          candidate.Extension,
                                                          StringComparison.OrdinalIgnoreCase
                                                      )
                                     );

                if (attachmentType.Extension is null)
                    continue;

                attachments.Add(
                    new FileInfo(
                        embeddedFile.Name,
                        embeddedFile.Data,
                        Type:  attachmentType.Type,
                        Info:  attachmentType.Info
                    )
                );

            }

            return attachments;

        }

        #endregion


    }

}
