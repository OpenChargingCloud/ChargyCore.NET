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

using System.Formats.Tar;
using System.IO.Compression;

using Org.BouncyCastle.Utilities.Bzip2;

#endregion

namespace cloud.charging.open.chargy.IO
{

    /// <summary>
    /// One file taken out of an archive.
    /// </summary>
    /// <param name="Path">The full path of the file within the archive.</param>
    /// <param name="Data">The contents of the file.</param>
    public readonly record struct ArchiveEntry(String                Path,
                                               ReadOnlyMemory<Byte>  Data)
    {

        /// <summary>
        /// The name of the file, without its directories.
        /// </summary>
        public String Name

            => Path[(Path.LastIndexOf('/') + 1)..];

        /// <summary>Return a text representation of this archive entry.</summary>
        public override String ToString()

            => $"{Path} ({Data.Length} byte(s))";

    }


    /// <summary>
    /// Unpacks the containers charge point operators wrap their transparency data in.
    ///
    /// Charge transparency records reach an EV driver as ZIP downloads, as
    /// "tar.bz2" payloads from a ChargePoint charging station, or as a single
    /// gzipped file. Chargy unpacks them all before it even tries to work out
    /// which data format is inside.
    ///
    /// Every method here fails softly: a container that cannot be read yields no
    /// entries rather than an exception, because a broken archive among several
    /// good files must not take down the whole verification.
    /// </summary>
    public static class ArchiveReader
    {

        #region Extract       (FileName, Data, MIMEType)

        /// <summary>
        /// Extract every file from an archive.
        /// </summary>
        /// <param name="FileName">The name of the archive, used to name a single unnamed entry.</param>
        /// <param name="Data">The contents of the archive.</param>
        /// <param name="MIMEType">The type of the archive.</param>
        public static IReadOnlyList<ArchiveEntry> Extract(String                FileName,
                                                          ReadOnlyMemory<Byte>  Data,
                                                          String                MIMEType)
        {

            switch (MIMEType)
            {

                case ContentTypes.Zip:
                    return ExtractZip(Data);

                case ContentTypes.Tar:
                    return ExtractTar(Data);

                case ContentTypes.GZip:
                    return ExtractCompressedStream(FileName, Decompress(Data, ContentTypes.GZip));

                case ContentTypes.BZip2:
                    return ExtractCompressedStream(FileName, Decompress(Data, ContentTypes.BZip2));

            }

            return [];

        }

        #endregion

        #region ExtractZip    (Data)

        /// <summary>
        /// Extract every file from a ZIP archive, ignoring its directory entries.
        /// </summary>
        /// <param name="Data">The contents of a ZIP archive.</param>
        public static IReadOnlyList<ArchiveEntry> ExtractZip(ReadOnlyMemory<Byte> Data)
        {

            var entries = new List<ArchiveEntry>();

            try
            {

                using var stream   = AsStream(Data);
                using var zipFile  = new ZipArchive(stream, ZipArchiveMode.Read);

                foreach (var zipEntry in zipFile.Entries)
                {

                    // A directory entry has a trailing slash and no content.
                    if (zipEntry.FullName.EndsWith('/'))
                        continue;

                    try
                    {

                        using var entryStream  = zipEntry.Open();
                        using var buffer       = new MemoryStream();

                        entryStream.CopyTo(buffer);

                        entries.Add(
                            new ArchiveEntry(
                                zipEntry.FullName,
                                buffer.ToArray()
                            )
                        );

                    }
                    catch (Exception)
                    {
                        // An entry using an unsupported compression method is skipped,
                        // exactly like ChargyCore.TS, which only handles "stored" and
                        // "deflate". The remaining entries are still worth having.
                    }

                }

            }
            catch (Exception)
            {
                return entries;
            }

            return entries;

        }

        #endregion

        #region ExtractTar    (Data)

        /// <summary>
        /// Extract every file from an uncompressed TAR archive, ignoring its
        /// directory entries.
        ///
        /// Also used to probe whether a decompressed stream is a TAR archive at
        /// all, so anything that is not one has to yield no entries rather than
        /// throw.
        /// </summary>
        /// <param name="Data">The contents of a TAR archive.</param>
        public static IReadOnlyList<ArchiveEntry> ExtractTar(ReadOnlyMemory<Byte> Data)
        {

            var entries = new List<ArchiveEntry>();

            try
            {

                using var stream     = AsStream(Data);
                using var tarReader  = new TarReader(stream);

                while (tarReader.GetNextEntry() is TarEntry tarEntry)
                {

                    if (tarEntry.EntryType is TarEntryType.Directory
                                           or TarEntryType.DirectoryList)
                        continue;

                    if (tarEntry.DataStream is not Stream dataStream)
                        continue;

                    using var buffer = new MemoryStream();
                    dataStream.CopyTo(buffer);

                    entries.Add(
                        new ArchiveEntry(
                            tarEntry.Name,
                            buffer.ToArray()
                        )
                    );

                }

            }
            catch (Exception)
            {
                // Not a TAR archive, or truncated. Whatever was read before the
                // damaged header is kept, mirroring the TypeScript reader, which
                // stops at the first unreadable header instead of failing.
                return entries;
            }

            return entries;

        }

        #endregion

        #region Decompress    (Data, MIMEType)

        /// <summary>
        /// Decompress a GZip or BZip2 stream.
        /// </summary>
        /// <param name="Data">The compressed data.</param>
        /// <param name="MIMEType">The type of compression.</param>
        public static ReadOnlyMemory<Byte> Decompress(ReadOnlyMemory<Byte>  Data,
                                                      String                MIMEType)
        {

            try
            {

                using var input   = AsStream(Data);
                using var output  = new MemoryStream();

                switch (MIMEType)
                {

                    case ContentTypes.GZip:
                        {
                            using var gzip = new GZipStream(input, CompressionMode.Decompress);
                            gzip.CopyTo(output);
                        }
                        break;

                    case ContentTypes.BZip2:
                        {
                            // Unlike the original Apache reader this one expects the
                            // "BZh" magic number to still be there and reads it itself.
                            using var bzip2 = new CBZip2InputStream(input);
                            bzip2.CopyTo(output);
                        }
                        break;

                    default:
                        return ReadOnlyMemory<Byte>.Empty;

                }

                return output.ToArray();

            }
            catch (Exception)
            {
                return ReadOnlyMemory<Byte>.Empty;
            }

        }

        #endregion


        #region (private) ExtractCompressedStream(FileName, Decompressed)

        /// <summary>
        /// Interpret a decompressed stream as a TAR archive, or — when it is not
        /// one — as a single file that had simply been compressed.
        ///
        /// "chargeIT-Testdata-02.tar.gz" holds a TAR archive; a plain ".gz" holds
        /// one file whose name is the archive's name without the last extension.
        /// Only the content can tell the two apart.
        /// </summary>
        /// <param name="FileName">The name of the archive.</param>
        /// <param name="Decompressed">The decompressed data.</param>
        private static IReadOnlyList<ArchiveEntry> ExtractCompressedStream(String                FileName,
                                                                           ReadOnlyMemory<Byte>  Decompressed)
        {

            if (Decompressed.Length == 0)
                return [];

            var tarEntries = ExtractTar(Decompressed);

            if (tarEntries.Count > 0)
                return tarEntries;

            var lastDot = FileName.LastIndexOf('.');

            return [
                       new ArchiveEntry(
                           lastDot > 0
                               ? FileName[..lastDot]
                               : FileName,
                           Decompressed
                       )
                   ];

        }

        #endregion

        #region (private) AsStream(Data)

        /// <summary>
        /// Read a block of memory as a seekable stream, without copying it.
        /// </summary>
        /// <param name="Data">A block of memory.</param>
        private static Stream AsStream(ReadOnlyMemory<Byte> Data)

            => System.Runtime.InteropServices.MemoryMarshal.TryGetArray(Data, out var segment) &&
               segment.Array is not null

                   ? new MemoryStream(segment.Array,
                                      segment.Offset,
                                      segment.Count,
                                      writable: false)

                   : new MemoryStream(Data.ToArray(),
                                      writable: false);

        #endregion


    }

}
