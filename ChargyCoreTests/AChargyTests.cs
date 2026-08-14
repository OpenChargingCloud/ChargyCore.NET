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
