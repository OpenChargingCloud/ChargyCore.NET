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

#endregion

namespace cloud.charging.open.chargy.IO
{

    /// <summary>
    /// What kind of thing a pile of bytes actually is.
    ///
    /// An EV driver hands Chargy whatever their charge point operator gave them,
    /// and the file name is rarely a reliable guide: a ".chargy" file may be a
    /// ZIP archive, and a QR code photo may arrive as "download". So the content
    /// is sniffed rather than trusted, exactly as ChargyCore.TS does with the
    /// "file-type" package.
    /// </summary>
    public static class ContentTypes
    {

        #region Constants

        /// <summary>A ZIP archive.</summary>
        public const String Zip           = "application/zip";

        /// <summary>A GZip compressed stream.</summary>
        public const String GZip          = "application/gzip";

        /// <summary>A BZip2 compressed stream.</summary>
        public const String BZip2         = "application/x-bzip2";

        /// <summary>An uncompressed TAR archive.</summary>
        public const String Tar           = "application/x-tar";

        /// <summary>A PDF document, possibly a PDF/A-3 with attachments.</summary>
        public const String PDF           = "application/pdf";

        /// <summary>An XML document.</summary>
        public const String XML           = "application/xml";

        /// <summary>A JSON document.</summary>
        public const String JSON          = "application/json";

        /// <summary>A Chargy charge transparency record.</summary>
        public const String Chargy        = "application/chargy";

        /// <summary>Comma separated values.</summary>
        public const String CSV           = "text/csv";

        /// <summary>Plain text.</summary>
        public const String Text          = "text/plain";

        #endregion

        #region Data

        /// <summary>
        /// The image types a QR code can be read from.
        ///
        /// "image/jpg" is not a registered MIME type, but browsers and camera
        /// apps emit it often enough that refusing it would only annoy users.
        /// </summary>
        private static readonly HashSet<String> qrCodeImageTypes = new (StringComparer.Ordinal) {
            "image/png",
            "image/jpeg",
            "image/jpg",
            "image/gif",
            "image/webp",
            "image/bmp",
            "image/svg",
            "image/svg+xml"
        };

        #endregion


        #region Normalize          (MIMEType)

        /// <summary>
        /// Reduce a MIME type to its bare type, dropping any parameters.
        ///
        /// "text/xml; charset=utf-8" and "TEXT/XML" both have to compare equal
        /// to "text/xml", because whoever produced the file decided how to spell it.
        /// </summary>
        /// <param name="MIMEType">An optional MIME type.</param>
        public static String? Normalize(String? MIMEType)
        {

            if (MIMEType is null)
                return null;

            var separator  = MIMEType.IndexOf(';');
            var bareType   = (separator >= 0
                                  ? MIMEType[..separator]
                                  : MIMEType).Trim();

            return bareType.Length > 0
                       ? bareType.ToLowerInvariant()
                       : null;

        }

        #endregion

        #region FromFileName       (FileName)

        /// <summary>
        /// Guess the MIME type of a file from its extension.
        ///
        /// Only used as a fallback: an extension is a claim, not evidence.
        /// </summary>
        /// <param name="FileName">The name of a file.</param>
        public static String? FromFileName(String FileName)
        {

            var fileName = FileName.ToLowerInvariant();

            if (fileName.EndsWith(".png",    StringComparison.Ordinal))  return "image/png";
            if (fileName.EndsWith(".jpeg",   StringComparison.Ordinal))  return "image/jpeg";
            if (fileName.EndsWith(".jpg",    StringComparison.Ordinal))  return "image/jpg";
            if (fileName.EndsWith(".gif",    StringComparison.Ordinal))  return "image/gif";
            if (fileName.EndsWith(".webp",   StringComparison.Ordinal))  return "image/webp";
            if (fileName.EndsWith(".bmp",    StringComparison.Ordinal))  return "image/bmp";
            if (fileName.EndsWith(".svg",    StringComparison.Ordinal))  return "image/svg+xml";

            if (fileName.EndsWith(".pdf",    StringComparison.Ordinal))  return PDF;
            if (fileName.EndsWith(".xml",    StringComparison.Ordinal))  return XML;
            if (fileName.EndsWith(".json",   StringComparison.Ordinal))  return JSON;
            if (fileName.EndsWith(".csv",    StringComparison.Ordinal))  return CSV;
            if (fileName.EndsWith(".chargy", StringComparison.Ordinal))  return Chargy;

            if (fileName.EndsWith(".zip",    StringComparison.Ordinal))  return Zip;
            if (fileName.EndsWith(".gz",     StringComparison.Ordinal))  return GZip;
            if (fileName.EndsWith(".bz2",    StringComparison.Ordinal))  return BZip2;
            if (fileName.EndsWith(".tar",    StringComparison.Ordinal))  return Tar;

            return null;

        }

        #endregion

        #region FromContent        (Data)

        /// <summary>
        /// Determine the MIME type of a file from its leading bytes.
        ///
        /// This is what ChargyCore.TS gets from the "file-type" package, reduced
        /// to the handful of types Chargy actually acts on. Everything else
        /// returns null and is then judged by its text content instead.
        /// </summary>
        /// <param name="Data">The contents of a file.</param>
        public static String? FromContent(ReadOnlySpan<Byte> Data)
        {

            #region Archives and documents

            if (Data.Length >= 4 &&
                Data[0] == 0x50 && Data[1] == 0x4B &&                          // "PK"
               (Data[2] == 0x03 || Data[2] == 0x05 || Data[2] == 0x07))
                return Zip;

            if (Data.Length >= 3 &&
                Data[0] == 0x1F && Data[1] == 0x8B && Data[2] == 0x08)
                return GZip;

            if (Data.Length >= 3 &&
                Data[0] == 0x42 && Data[1] == 0x5A && Data[2] == 0x68)         // "BZh"
                return BZip2;

            if (Data.Length >= 5 &&
                Data[0] == 0x25 && Data[1] == 0x50 && Data[2] == 0x44 &&       // "%PDF-"
                Data[3] == 0x46 && Data[4] == 0x2D)
                return PDF;

            // A TAR archive has no magic number of its own: the "ustar" marker
            // sits 257 bytes into the first header block.
            if (Data.Length >= 262 &&
                Data[257] == 0x75 && Data[258] == 0x73 && Data[259] == 0x74 &&
                Data[260] == 0x61 && Data[261] == 0x72)                        // "ustar"
                return Tar;

            #endregion

            #region Raster images

            if (Data.Length >= 8 &&
                Data[0] == 0x89 && Data[1] == 0x50 && Data[2] == 0x4E && Data[3] == 0x47 &&
                Data[4] == 0x0D && Data[5] == 0x0A && Data[6] == 0x1A && Data[7] == 0x0A)
                return "image/png";

            if (Data.Length >= 3 &&
                Data[0] == 0xFF && Data[1] == 0xD8 && Data[2] == 0xFF)
                return "image/jpeg";

            if (Data.Length >= 6 &&
                Data[0] == 0x47 && Data[1] == 0x49 && Data[2] == 0x46 &&       // "GIF8"
                Data[3] == 0x38)
                return "image/gif";

            if (Data.Length >= 12 &&
                Data[0] == 0x52 && Data[ 1] == 0x49 && Data[ 2] == 0x46 && Data[ 3] == 0x46 &&   // "RIFF"
                Data[8] == 0x57 && Data[ 9] == 0x45 && Data[10] == 0x42 && Data[11] == 0x50)     // "WEBP"
                return "image/webp";

            if (Data.Length >= 2 &&
                Data[0] == 0x42 && Data[1] == 0x4D)                            // "BM"
                return "image/bmp";

            #endregion

            #region Text based formats

            var text = LeadingText(Data);

            if (text.Length == 0)
                return null;

            // An SVG is XML, so it has to be recognised before the generic XML case.
            if (text.Contains("<svg", StringComparison.OrdinalIgnoreCase))
                return "image/svg+xml";

            if (text.StartsWith("<?xml", StringComparison.Ordinal) ||
                text.StartsWith("<",     StringComparison.Ordinal))
                return XML;

            if (text.StartsWith("{",     StringComparison.Ordinal) ||
                text.StartsWith("[",     StringComparison.Ordinal))
                return JSON;

            #endregion

            return null;

        }

        #endregion

        #region ForQRCodeImage     (FileInfo, DetectedMIMEType = null)

        /// <summary>
        /// Decide which image type to hand to the QR code decoder.
        ///
        /// Sniffed content wins over the declared type, which wins over the file
        /// name — but only while the candidate is an image type we can actually
        /// decode. A declared "application/octet-stream" must not shadow a file
        /// name that says ".png".
        /// </summary>
        /// <param name="FileInfo">A file.</param>
        /// <param name="DetectedMIMEType">An optional MIME type sniffed from the content.</param>
        public static String? ForQRCodeImage(FileInfo  FileInfo,
                                             String?   DetectedMIMEType = null)
        {

            var detected  = Normalize(DetectedMIMEType);
            var declared  = Normalize(FileInfo.Type);
            var byName    = FromFileName(FileInfo.Name);

            if (IsQRCodeImage(detected))
                return detected;

            if (IsQRCodeImage(declared))
                return declared;

            if (byName is not null)
                return byName;

            return detected ?? declared;

        }

        #endregion

        #region IsQRCodeImage      (MIMEType)

        /// <summary>
        /// Whether a QR code can be read from an image of this type.
        /// </summary>
        /// <param name="MIMEType">An optional MIME type.</param>
        public static Boolean IsQRCodeImage(String? MIMEType)

            => MIMEType is not null &&
               qrCodeImageTypes.Contains(MIMEType);

        #endregion

        #region IsArchive          (MIMEType)

        /// <summary>
        /// Whether files can be extracted from a container of this type.
        /// </summary>
        /// <param name="MIMEType">An optional MIME type.</param>
        public static Boolean IsArchive(String? MIMEType)

            => MIMEType == Zip   ||
               MIMEType == GZip  ||
               MIMEType == BZip2 ||
               MIMEType == Tar;

        #endregion


        #region (private) LeadingText(Data)

        /// <summary>
        /// Decode the first few hundred bytes as UTF-8 text, for sniffing only.
        ///
        /// Reading the whole file would mean decoding megabytes of a format we
        /// have not identified yet; and a truncated multi-byte sequence at the
        /// end does no harm, because only the beginning is ever examined.
        /// </summary>
        /// <param name="Data">The contents of a file.</param>
        private static String LeadingText(ReadOnlySpan<Byte> Data)
        {

            var head = Data[..Math.Min(Data.Length, 512)];

            // Skip an UTF-8 byte order mark.
            if (head.Length >= 3 &&
                head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
                head = head[3..];

            return Encoding.UTF8.GetString(head).TrimStart();

        }

        #endregion


    }

}
