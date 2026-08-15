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
    /// Tests for unpacking the containers charge point operators ship their
    /// transparency data in.
    ///
    /// "chargeIT-Testdata-02" exists as a ZIP, a TAR, a "tar.gz" and a "tar.bz2"
    /// holding the very same charge transparency record. That makes it the ideal
    /// proof that the container format is irrelevant to the data inside it —
    /// which is exactly the promise Chargy makes to an EV driver.
    /// </summary>
    [TestFixture]
    public class ArchiveReaderTests : AChargyTests
    {

        #region Data

        private static readonly String[] chargeITArchives = [
                                                                "chargeIT/chargeIT-Testdata-02.zip",
                                                                "chargeIT/chargeIT-Testdata-02.tar",
                                                                "chargeIT/chargeIT-Testdata-02.tar.gz",
                                                                "chargeIT/chargeIT-Testdata-02.tar.bz2"
                                                            ];

        #endregion


        #region ChargeITArchives_ContainTheSameChargeTransparencyRecord(Fixture)

        /// <summary>
        /// Every chargeIT container holds the same charge transparency record.
        /// </summary>
        [Test]
        public void ChargeITArchives_ContainTheSameChargeTransparencyRecord([ValueSource(nameof(chargeITArchives))] String Fixture)
        {

            var expected  = ReadTextFixture("chargeIT/chargeIT-Testdata-02.chargy");

            var entries   = ArchiveReader.Extract(
                                Path.GetFileName(Fixture),
                                ReadBinaryFixture(Fixture),
                                MIMETypeOf(Fixture)
                            );

            Assert.That(entries,        Has.Count.EqualTo(1),  $"'{Fixture}' should hold exactly one file!");

            var actual = Encoding.UTF8.GetString(entries[0].Data.Span).Trim();

            Assert.That(actual,         Is.EqualTo(expected),  $"'{Fixture}' holds different data!");

        }

        #endregion

        #region ChargeITArchives_NameTheExtractedFile(Fixture)

        /// <summary>
        /// The extracted file keeps the name it had inside the archive.
        /// </summary>
        [Test]
        public void ChargeITArchives_NameTheExtractedFile([ValueSource(nameof(chargeITArchives))] String Fixture)
        {

            var entries = ArchiveReader.Extract(
                              Path.GetFileName(Fixture),
                              ReadBinaryFixture(Fixture),
                              MIMETypeOf(Fixture)
                          );

            Assert.That(entries,          Is.Not.Empty);
            Assert.That(entries[0].Name,  Is.EqualTo("chargeIT-Testdatensatz-02.chargy"));

        }

        #endregion


        #region ChargePointPayload_HoldsTheSignedRecordAndItsSignature()

        /// <summary>
        /// A ChargePoint charging station ships its record as a "tar.bz2" holding
        /// two files: the record itself and its detached signature.
        /// </summary>
        [Test]
        public void ChargePointPayload_HoldsTheSignedRecordAndItsSignature()
        {

            const String fixture = "ChargePoint/Testdata-2020-02/0024b1000002e300_2_123017065_payload.tar.bz2";

            var entries = ArchiveReader.Extract(
                              Path.GetFileName(fixture),
                              ReadBinaryFixture(fixture),
                              ContentTypes.BZip2
                          );

            Assert.That(entries.Select(entry => entry.Path),
                        Is.EquivalentTo(new[] { "secrrct", "secrrct.sign" }));

            var record = entries.First(entry => entry.Path == "secrrct");

            Assert.That(Encoding.UTF8.GetString(record.Data.Span).TrimStart(),
                        Does.StartWith("{"),
                        "The 'secrrct' file should hold a JSON document!");

        }

        #endregion

        #region ChargePointArchive_HoldsTheRecordAndItsPublicKey()

        /// <summary>
        /// A ZIP archive carrying several files yields all of them.
        /// </summary>
        [Test]
        public void ChargePointArchive_HoldsTheRecordAndItsPublicKey()
        {

            const String fixture = "ChargePoint/Testdata-2020-02/0024b1000002e300_2_123017065_withPublicKey.zip";

            var entries = ArchiveReader.Extract(
                              Path.GetFileName(fixture),
                              ReadBinaryFixture(fixture),
                              ContentTypes.Zip
                          );

            Assert.That(entries.Select(entry => entry.Path),
                        Is.EquivalentTo(new[] {
                            "0024b1000002e300_2.pem",
                            "0024b1000002e300_2_123017065_payload.tar.bz2"
                        }));

        }

        #endregion

        #region OCMFArchive_CanBeExtracted()

        /// <summary>
        /// The OCMF test data is a plain ZIP archive.
        /// </summary>
        [Test]
        public void OCMFArchive_CanBeExtracted()
        {

            var entries = ArchiveReader.Extract(
                              "OCMF-Testdata-01.zip",
                              ReadBinaryFixture("OCMF/OCMF-Testdata-01.zip"),
                              ContentTypes.Zip
                          );

            Assert.That(entries, Is.Not.Empty);

            foreach (var entry in entries)
                Assert.That(entry.Data.Length, Is.GreaterThan(0), $"'{entry.Path}' is empty!");

        }

        #endregion


        #region NestedArchive_YieldsTheInnerArchives()

        /// <summary>
        /// A ZIP archive of archives yields the inner archives, which the
        /// decompression loop of the format detector then unpacks in turn.
        /// </summary>
        [Test]
        public void NestedArchive_YieldsTheInnerArchives()
        {

            const String fixture = "ChargePoint/Testdata-secp256r1/1/all-combined/Testdata-secp256r1.zip";

            var entries = ArchiveReader.Extract(
                              Path.GetFileName(fixture),
                              ReadBinaryFixture(fixture),
                              ContentTypes.Zip
                          );

            Assert.That(entries, Is.Not.Empty);

            Assert.That(entries.Any(entry => ContentTypes.IsArchive(ContentTypes.FromContent(entry.Data.Span))),
                        Is.True,
                        "The archive should hold at least one further archive!");

        }

        #endregion


        #region Decompress_OfDamagedData_YieldsNothing()

        /// <summary>
        /// Damaged compressed data yields nothing rather than an exception: one
        /// broken file among several must not abort the whole verification.
        /// </summary>
        [Test]
        public void Decompress_OfDamagedData_YieldsNothing()
        {

            var damagedGZip   = ArchiveReader.Decompress(new Byte[] { 0x1F, 0x8B, 0x08, 0x00, 0x17, 0x42 },  ContentTypes.GZip);
            var damagedBZip2  = ArchiveReader.Decompress(new Byte[] { 0x42, 0x5A, 0x68, 0x39, 0x17, 0x42 },  ContentTypes.BZip2);

            Assert.Multiple(() => {
                Assert.That(damagedGZip. Length,  Is.Zero);
                Assert.That(damagedBZip2.Length,  Is.Zero);
            });

        }

        #endregion

        #region Extract_OfANonArchive_YieldsNothing()

        /// <summary>
        /// Data that is not an archive at all yields no entries.
        /// </summary>
        [Test]
        public void Extract_OfANonArchive_YieldsNothing()
        {

            var entries = ArchiveReader.Extract(
                              "not-an-archive.zip",
                              Encoding.UTF8.GetBytes("This is not an archive!"),
                              ContentTypes.Zip
                          );

            Assert.That(entries, Is.Empty);

        }

        #endregion

        #region ExtractTar_OfANonTar_YieldsNothing()

        /// <summary>
        /// The TAR reader is also used to probe whether a decompressed stream is
        /// a TAR archive, so it must report "no" rather than throw.
        /// </summary>
        [Test]
        public void ExtractTar_OfANonTar_YieldsNothing()
        {

            Assert.That(ArchiveReader.ExtractTar(Encoding.UTF8.GetBytes("{ \"@id\": \"not a tar archive\" }")),
                        Is.Empty);

        }

        #endregion


    }

}
