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

using System.Reflection;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.IO;
using cloud.charging.open.chargy.qrcodes;

#endregion

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Common base class for all ChargyCore tests, providing access to the charge
    /// transparency test fixtures below "TestData".
    ///
    /// Those fixtures are shared byte-for-byte with ChargyCore.TS. Signature
    /// verification depends on their exact bytes, therefore they are always read
    /// as raw bytes; the text overloads decode UTF-8 explicitly instead of relying
    /// on the ambient encoding of the test host.
    /// </summary>
    public abstract class AChargyTests
    {

        #region Data

        /// <summary>
        /// The directory holding the charge transparency test fixtures.
        /// </summary>
        public static readonly String TestDataDirectory = Path.Combine(
                                                              Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                                                              "TestData"
                                                          );

        #endregion


        #region FixturePath          (FixtureName)

        /// <summary>
        /// The absolute path of the given test fixture.
        /// </summary>
        /// <param name="FixtureName">A fixture path relative to "TestData", e.g. "OCMF/OCMF-Testdata-01.ocmf".</param>
        public static String FixturePath(String FixtureName)

            => Path.Combine(
                   TestDataDirectory,
                   FixtureName.Replace('/', Path.DirectorySeparatorChar)
               );

        #endregion

        #region ReadBinaryFixture    (FixtureName)

        /// <summary>
        /// Read the given test fixture as raw bytes.
        /// </summary>
        /// <param name="FixtureName">A fixture path relative to "TestData", e.g. "OCMF/OCMF-Testdata-01.zip".</param>
        /// <exception cref="FileNotFoundException">When the fixture does not exist.</exception>
        public static Byte[] ReadBinaryFixture(String FixtureName)
        {

            var path = FixturePath(FixtureName);

            if (!File.Exists(path))
                throw new FileNotFoundException(
                          $"The test fixture '{FixtureName}' could not be found at '{path}'!",
                          path
                      );

            return File.ReadAllBytes(path);

        }

        #endregion

        #region ReadTextFixture      (FixtureName)

        /// <summary>
        /// Read the given test fixture as UTF-8 text, with leading and trailing
        /// whitespace removed.
        ///
        /// ChargyCore.TS trims its fixtures the same way, so that a trailing
        /// newline in a checked-in file never changes a verification result.
        /// </summary>
        /// <param name="FixtureName">A fixture path relative to "TestData", e.g. "OCMF/OCMF-Testdata-01.ocmf".</param>
        public static String ReadTextFixture(String FixtureName)

            => System.Text.Encoding.UTF8.GetString(ReadBinaryFixture(FixtureName)).Trim();

        #endregion

        #region ReadExpectedReport   (FixtureName)

        /// <summary>
        /// Read a golden verification report ("*.expected.txt").
        ///
        /// These files are shared byte-for-byte with ChargyCore.TS and are the
        /// contract that keeps both implementations semantically identical.
        /// They must never be edited to make a test pass.
        /// </summary>
        /// <param name="FixtureName">A fixture path relative to "TestData", e.g. "OCMF/OCMF-Testdata-01.expected.txt".</param>
        public static String ReadExpectedReport(String FixtureName)

            => ReadTextFixture(FixtureName);

        #endregion


        #region Verify               (Files, ValidationRules = null)

        /// <summary>
        /// Run the given files through the whole Chargy pipeline, exactly as an
        /// application would.
        /// </summary>
        /// <param name="Files">The files an EV driver handed over.</param>
        public static Task<Object> Verify(IEnumerable<FileInfo> Files)

            => new ContentFormatDetector(
                   I18NDictionary.Default(),
                   ChargeTransparencyFormats.All(),
                   new PDFAttachmentExtractor(),
                   new QRCodeDecoder()
               ).DetectAndConvertContentFormat(Files);

        #endregion

        #region VerifyFixtures       (FixtureNames, ValidationRules = null)

        /// <summary>
        /// Run the given test fixtures through the whole Chargy pipeline.
        /// </summary>
        /// <param name="FixtureNames">Fixture paths relative to "TestData".</param>
        public static Task<Object> VerifyFixtures(IEnumerable<String> FixtureNames)

            => Verify(
                   FixtureNames.Select(fixtureName => {

                       // ChargyCore.TS hands the fixture path over as the file
                       // name, and some formats look at it, so the tests must not
                       // quietly shorten it.
                       return new FileInfo(
                                  fixtureName,
                                  ReadBinaryFixture(fixtureName),
                                  MIMETypeOf(fixtureName)
                              );

                   })
               );

        #endregion

        #region ExpectReport         (FixtureName, ExpectedFixture, ValidationRules = null)

        /// <summary>
        /// Verify a fixture and compare the report against its golden file.
        /// </summary>
        /// <param name="FixtureName">A fixture path relative to "TestData".</param>
        /// <param name="ExpectedFixture">The golden report to compare against.</param>
        public static Task ExpectReport(String  FixtureName,
                                        String  ExpectedFixture)

            => ExpectReport([ FixtureName ], ExpectedFixture);

        #endregion

        #region ExpectReport         (FixtureNames, ExpectedFixture, ValidationRules = null)

        /// <summary>
        /// Verify several fixtures together and compare the report against its
        /// golden file.
        /// </summary>
        /// <param name="FixtureNames">Fixture paths relative to "TestData".</param>
        /// <param name="ExpectedFixture">The golden report to compare against.</param>
        public static async Task ExpectReport(IEnumerable<String>  FixtureNames,
                                              String               ExpectedFixture)
        {

            var result    = await VerifyFixtures(FixtureNames);
            var actual    = VerificationReport.Format(result);
            var expected  = ReadExpectedReport(ExpectedFixture);

            AssertReportLines(actual, expected);

        }

        #endregion

        #region AssertReportLines    (Actual, Expected)

        /// <summary>
        /// Compare two verification reports line by line.
        ///
        /// Every differing line is reported, not just the first: when a format
        /// port goes wrong it usually goes wrong in several places at once, and
        /// seeing all of them together is what tells you which single mistake
        /// caused them. This mirrors the "expect.soft" of ChargyCore.TS.
        /// </summary>
        /// <param name="Actual">The report Chargy produced.</param>
        /// <param name="Expected">The golden report.</param>
        public static void AssertReportLines(String  Actual,
                                             String  Expected)
        {

            var actualLines    = Actual.  Replace("\r\n", "\n").Split('\n');
            var expectedLines  = Expected.Replace("\r\n", "\n").Split('\n');
            var lineCount      = Math.Max(actualLines.Length, expectedLines.Length);

            Assert.Multiple(() => {

                for (var i = 0; i < lineCount; i++)
                    Assert.That(
                        i < actualLines.  Length ? actualLines[i]   : null,
                        Is.EqualTo(
                        i < expectedLines.Length ? expectedLines[i] : null),
                        $"verification report line {i + 1}"
                    );

            });

        }

        #endregion


        #region MIMETypeOf           (FileName)

        /// <summary>
        /// The MIME type Chargy would be given for a file of this name.
        ///
        /// This mirrors "archiveMimeType()" of the ChargyCore.TS test helper: the
        /// applications hand the MIME type they received alongside the file to
        /// Chargy, and the tests have to simulate that.
        /// </summary>
        /// <param name="FileName">The name of a charge transparency file.</param>
        public static String MIMETypeOf(String FileName)

            => FileName switch {
                   _ when FileName.EndsWith(".chargy",  StringComparison.OrdinalIgnoreCase)  => "application/chargy",
                   _ when FileName.EndsWith(".ocmf",    StringComparison.OrdinalIgnoreCase)  => "application/ocmf",
                   _ when FileName.EndsWith(".json",    StringComparison.OrdinalIgnoreCase)  => "application/json",
                   _ when FileName.EndsWith(".xml",     StringComparison.OrdinalIgnoreCase)  => "application/xml",
                   _ when FileName.EndsWith(".zip",     StringComparison.OrdinalIgnoreCase)  => "application/zip",
                   _ when FileName.EndsWith(".tar.gz",  StringComparison.OrdinalIgnoreCase)  => "application/gzip",
                   _ when FileName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase)  => "application/x-bzip2",
                   _ when FileName.EndsWith(".tar",     StringComparison.OrdinalIgnoreCase)  => "application/x-tar",
                   _ when FileName.EndsWith(".pdf",     StringComparison.OrdinalIgnoreCase)  => "application/pdf",
                   _ when FileName.EndsWith(".png",     StringComparison.OrdinalIgnoreCase)  => "image/png",
                   _ when FileName.EndsWith(".jpg",     StringComparison.OrdinalIgnoreCase)  => "image/jpeg",
                   _ when FileName.EndsWith(".jpeg",    StringComparison.OrdinalIgnoreCase)  => "image/jpeg",
                   _ when FileName.EndsWith(".svg",     StringComparison.OrdinalIgnoreCase)  => "image/svg+xml",
                   _ when FileName.EndsWith(".webp",    StringComparison.OrdinalIgnoreCase)  => "image/webp",
                   _ when FileName.EndsWith(".gif",     StringComparison.OrdinalIgnoreCase)  => "image/gif",
                   _ when FileName.EndsWith(".bmp",     StringComparison.OrdinalIgnoreCase)  => "image/bmp",
                   _                                                                         => "binary/octet-stream"
               };

        #endregion


    }

}
