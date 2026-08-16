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

using System.Globalization;

using Newtonsoft.Json.Linq;

using cloud.charging.open.chargy.cli;

#endregion

namespace cloud.charging.open.chargy.tests.CLI
{

    /// <summary>
    /// Tests for "chargy-verify" as a whole: arguments in, exit code and printed
    /// report out.
    ///
    /// The program is a sample, but the exit code is not. It is the only thing a
    /// script can act on, and the difference between 1 and 2 is the difference
    /// between "this charging session is not proven" and "I could not read this
    /// at all" — a billing pipeline treats those differently, or should.
    ///
    /// Main is called in process rather than through a shell. That keeps the test
    /// honest about what it covers: argument parsing, the wiring of the detector,
    /// the report and the exit code, but not the packaging of the executable.
    /// </summary>
    [TestFixture]
    public class ChargyVerifyTests : AChargyTests
    {

        #region Setup / Teardown

        private StringWriter  output   = null!;
        private StringWriter  errors   = null!;
        private TextWriter    stdout   = null!;
        private TextWriter    stderr   = null!;
        private CultureInfo?  culture;
        private String        temporaryDirectory = null!;

        [SetUp]
        public void RedirectTheConsole()
        {

            output              = new StringWriter();
            errors              = new StringWriter();
            stdout              = Console.Out;
            stderr              = Console.Error;
            culture             = CultureInfo.DefaultThreadCurrentCulture;

            // The program takes paths, so some of these tests need real files.
            // They go somewhere temporary rather than next to the fixtures: the
            // scaffolding tests count what is in there, and a file this fixture
            // left behind would be counted as a 205th fixture.
            temporaryDirectory  = Directory.CreateTempSubdirectory("chargy-verify-tests-").FullName;

            Console.SetOut  (output);
            Console.SetError(errors);

        }

        /// <summary>
        /// Run the program, on a console of its own.
        ///
        /// Each call starts with empty output, so that a test comparing two runs
        /// is not reading the first one twice.
        /// </summary>
        /// <param name="Arguments">What a shell would have handed over.</param>
        private async Task<Int32> Run(params String[] Arguments)
        {

            output = new StringWriter();
            errors = new StringWriter();

            Console.SetOut  (output);
            Console.SetError(errors);

            return await Program.Main(Arguments);

        }

        [TearDown]
        public void RestoreTheConsole()
        {

            Console.SetOut  (stdout);
            Console.SetError(stderr);

            // Main pins the culture to invariant on purpose, so that a meter
            // reading is printed the same way wherever it runs. That is right for
            // a program and wrong to leave behind in a test process, where the
            // next fixture did not ask for it.
            CultureInfo.DefaultThreadCurrentCulture = culture;

            output.Dispose();
            errors.Dispose();

            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, true);

        }

        #endregion


        #region ARecordThatVerifiesIsReportedAndExitsZero()

        /// <summary>
        /// A signed Alfen record verifies, and the program says so and exits 0.
        /// </summary>
        [Test]
        public async Task ARecordThatVerifiesIsReportedAndExitsZero()
        {

            var exitCode = await Run(FixturePath("ALFEN/ALFEN-Testdata-03_SAFEXMLContainer.xml"));

            Assert.Multiple(() => {

                Assert.That(exitCode,          Is.EqualTo(Program.ExitVerified), Printed);

                Assert.That(Printed,           Does.Contain("Charge transparency record"));
                Assert.That(Printed,           Does.Contain("Charging session 1: verified"));
                Assert.That(Printed,           Does.Contain("with a valid signature"));

                Assert.That(Printed,           Does.Not.Contain("NOT verified"));
                Assert.That(errors.ToString(), Is.Empty);

            });

        }

        #endregion

        #region APhotographOfAQRCodeIsAlsoAChargeTransparencyFile()

        /// <summary>
        /// A photograph of the QR code on a charging station verifies exactly like
        /// the XML behind it.
        ///
        /// This is the claim the help text makes, and the only test that exercises
        /// the QR decoder through the program rather than through the library: an
        /// application should not have to know that a .png and an .xml are two
        /// different problems.
        /// </summary>
        [Test]
        public async Task APhotographOfAQRCodeIsAlsoAChargeTransparencyFile()
        {

            var exitCode = await Run(FixturePath("ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_asQRCode.png"));

            Assert.Multiple(() => {
                Assert.That(exitCode,  Is.EqualTo(Program.ExitVerified), Printed);
                Assert.That(Printed,   Does.Contain("Charging session 1: verified"));
            });

        }

        #endregion

        #region ARecordThatDoesNotVerifyExitsOneRatherThanTwo()

        /// <summary>
        /// A record that was read but is not proven exits 1, and says why.
        ///
        /// The OCMF test data holds a single meter reading, and one reading is not
        /// a charging session: there is nothing to subtract it from. The file is
        /// perfectly readable and its signature is intact, so "could not read
        /// this" would be wrong — this is the case the exit codes separate.
        /// </summary>
        [Test]
        public async Task ARecordThatDoesNotVerifyExitsOneRatherThanTwo()
        {

            var exitCode = await Run(
                                     FixturePath("OCMF/OCMF-Testdata-01.ocmf"),
                                     FixturePath("OCMF/OCMF-Testdata-01_publicKey.txt")
                                 );

            Assert.Multiple(() => {
                Assert.That(exitCode,  Is.EqualTo(Program.ExitNotVerified), Printed);
                Assert.That(Printed,   Does.Contain("Charging session 1: not verified"));
            });

        }

        #endregion

        #region ATamperedSignatureIsSaidOutLoudAndExitsOne()

        /// <summary>
        /// A record whose signature was altered exits 1 and says what is wrong
        /// with it.
        ///
        /// This is the case the whole program exists for, and the one where a
        /// vague answer would do real harm: the file parses, the readings are all
        /// there, the charging session looks complete, and none of it is proven.
        /// The verdict has to name the signature rather than merely withhold the
        /// word "verified".
        ///
        /// One character of the signature is changed, not of the payload — the
        /// same length, the same structure, and a signature that no longer belongs
        /// to the data it covers.
        /// </summary>
        [Test]
        public async Task ATamperedSignatureIsSaidOutLoudAndExitsOne()
        {

            var original  = ReadTextFixture("ALFEN/ALFEN-Testdata-03_SAFEXMLContainer.xml");
            var tampered  = original.Replace(";X736PV2AD3IVH5LQJ4SPMZLMYNZNGOUBNLF23B7UBYAFOV6KXVPIMIVZJLKBNLJVSQAN7DJLMTWL2===;",
                                             ";Y736PV2AD3IVH5LQJ4SPMZLMYNZNGOUBNLF23B7UBYAFOV6KXVPIMIVZJLKBNLJVSQAN7DJLMTWL2===;");

            Assert.That(tampered,  Is.Not.EqualTo(original),
                        "The fixture changed shape, so this test is no longer tampering with anything.");

            var exitCode  = await Run(TemporaryFile("ALFEN-Testdata-03_tampered.xml", tampered));

            Assert.Multiple(() => {

                Assert.That(exitCode,  Is.EqualTo(Program.ExitNotVerified), Printed);

                Assert.That(Printed,   Does.Contain("NOT verified — the signature does not match"));
                Assert.That(Printed,   Does.Contain("0 with a valid signature"));
                Assert.That(Printed,   Does.Contain("The signature does not match the signed data!"));

            });

        }

        #endregion

        #region SomethingThatIsNotChargingDataExitsTwo()

        /// <summary>
        /// A file that is not charge transparency data at all exits 2.
        /// </summary>
        [Test]
        public async Task SomethingThatIsNotChargingDataExitsTwo()
        {

            var path      = TemporaryFile("shopping-list.txt", "milk, bread, a new charging cable");

            var exitCode  = await Run(path);

            Assert.Multiple(() => {
                Assert.That(exitCode,  Is.EqualTo(Program.ExitUnreadable), Printed);
                Assert.That(Printed,   Is.Not.Empty, "Exiting 2 in silence tells nobody what was wrong.");
            });

        }

        #endregion

        #region AKeyOnItsOwnProvesNothingAndSaysSo()

        /// <summary>
        /// A public key without the record it belongs to is reported as such.
        ///
        /// It exits 0 because nothing failed to verify — but the text has to make
        /// clear that nothing was verified either, which is the more useful half
        /// of that answer.
        /// </summary>
        [Test]
        public async Task AKeyOnItsOwnProvesNothingAndSaysSo()
        {

            var exitCode = await Run(FixturePath("OCMF/OCMF-Testdata-01_publicKey.txt"));

            Assert.Multiple(() => {
                Assert.That(exitCode,  Is.EqualTo(Program.ExitVerified), Printed);
                Assert.That(Printed,   Does.Contain("public key(s) and no charging data"));
                Assert.That(Printed,   Does.Contain("A key on its own proves nothing"));
            });

        }

        #endregion

        #region AURLIsNotFollowedUnlessItWasAskedFor()

        /// <summary>
        /// A URL in the input is printed, not fetched.
        ///
        /// This is the privacy default of the whole program: contacting the
        /// address would tell the charging station operator that somebody is
        /// looking at this charging session right now. The test cannot prove that
        /// no packet was sent, but it pins the two things that would have to
        /// change first — the flag defaulting to off, and the program saying out
        /// loud that it fetched nothing.
        /// </summary>
        [Test]
        public async Task AURLIsNotFollowedUnlessItWasAskedFor()
        {

            var path      = TemporaryFile("link.txt", "https://chargy.charging.cloud/charging-session?id=123#details");

            var exitCode  = await Run(path);

            Assert.Multiple(() => {

                Assert.That(exitCode,  Is.EqualTo(Program.ExitVerified), Printed);

                Assert.That(Printed,   Does.Contain("A link to charge transparency data, and no data itself"));
                Assert.That(Printed,   Does.Contain("https://chargy.charging.cloud/charging-session?id=123#details"));
                Assert.That(Printed,   Does.Contain("Nothing was fetched."));

                Assert.That(CommandLine.Parse([ path ]).ResolveURLs,  Is.False,
                            "Contacting an address found in a file must stay something that was asked for.");

            });

        }

        #endregion

        #region TheJSONOutputIsTheRecordItselfRatherThanAReport()

        /// <summary>
        /// "--json" prints the charge transparency record, and it parses.
        ///
        /// The exit code stays what the verdict deserves — the flag changes what
        /// is printed, not what was found.
        /// </summary>
        [Test]
        public async Task TheJSONOutputIsTheRecordItselfRatherThanAReport()
        {

            var exitCode = await Run("--json", FixturePath("ALFEN/ALFEN-Testdata-03_SAFEXMLContainer.xml"));

            Assert.That(exitCode,  Is.EqualTo(Program.ExitVerified), Printed);

            var json = JObject.Parse(Printed);

            Assert.Multiple(() => {
                Assert.That(json["@id"]?.Value<String>(),        Is.Not.Null.And.Not.Empty);
                Assert.That(json["chargingSessions"] as JArray,  Is.Not.Null.And.Not.Empty);
                Assert.That(Printed,                             Does.Not.Contain("Charging session 1:"),
                            "--json prints the record, not the report as well.");
            });

        }

        #endregion

        #region TheMessagesFollowTheLanguageThatWasAskedFor()

        /// <summary>
        /// "--language de" reports in German.
        ///
        /// The same input twice, once with the flag and once without, because a
        /// test that only looks at the German run cannot tell a translation from
        /// a message that happens to read the same in both languages.
        ///
        /// Only what the library produces is translated. The headings this program
        /// writes itself stay English, exactly as its help text promises, which is
        /// why the verified-record tests can assert on them at all.
        /// </summary>
        [Test]
        public async Task TheMessagesFollowTheLanguageThatWasAskedFor()
        {

            var path         = TemporaryFile("not-charging-data.txt", "milk, bread, a new charging cable");

            var inEnglish    = await Run(path);
            var englishText  = Printed;

            var inGerman     = await Run("-l", "de", path);
            var germanText   = Printed;

            Assert.Multiple(() => {

                Assert.That(inEnglish,    Is.EqualTo(Program.ExitUnreadable), englishText);
                Assert.That(inGerman,     Is.EqualTo(Program.ExitUnreadable), germanText);

                Assert.That(englishText,  Does.Contain("Unknown or invalid charge transparency record!"));
                Assert.That(germanText,   Does.Contain("Unbekannter oder ungültiger Transparenzdatensatz!"));

                // The verdict is the same either way. Only the words move — a
                // language must never change what was found.
                Assert.That(germanText,   Is.Not.EqualTo(englishText));

            });

        }

        #endregion

        #region AskingForHelpIsNotAnError()

        /// <summary>
        /// "--help" prints the help and exits 0; asking for nothing prints the
        /// same help and exits 64.
        ///
        /// The distinction is the point. A user who typed "chargy-verify --help"
        /// got what they asked for; a script that ran the program with no files
        /// made a mistake, and only the exit code can tell it so.
        /// </summary>
        [Test]
        public async Task AskingForHelpIsNotAnError()
        {

            var help = await Run("--help");

            Assert.Multiple(() => {
                Assert.That(help,     Is.EqualTo(Program.ExitVerified));
                Assert.That(Printed,  Does.Contain("Usage:"));
                Assert.That(Printed,  Does.Contain("chargy-verify [options] <file>..."));
            });

        }

        /// <summary>
        /// No arguments at all is a usage error, however helpful the output.
        /// </summary>
        [Test]
        public async Task AskingForNothingIsAUsageError()
        {

            var exitCode = await Run();

            Assert.Multiple(() => {
                Assert.That(exitCode,  Is.EqualTo(Program.ExitUsage));
                Assert.That(Printed,   Does.Contain("Usage:"));
            });

        }

        #endregion

        #region TheVersionNamesTheLibraryDoingTheVerifying()

        /// <summary>
        /// "--version" names both the program and the library.
        ///
        /// The library version is the one that matters in a bug report: the
        /// verifying happens there, and the two are versioned separately.
        /// </summary>
        [Test]
        public async Task TheVersionNamesTheLibraryDoingTheVerifying()
        {

            var exitCode = await Run("--version");

            Assert.Multiple(() => {
                Assert.That(exitCode,  Is.EqualTo(Program.ExitVerified));
                Assert.That(Printed,   Does.StartWith("chargy-verify "));
                Assert.That(Printed,   Does.Contain("ChargyCore "));
            });

        }

        #endregion

        #region AMistakeOnTheCommandLineIsAUsageError(Arguments)

        /// <summary>
        /// Everything the program cannot act on exits 64, and says why on standard
        /// error rather than into the report.
        /// </summary>
        /// <param name="Arguments">A command line.</param>
        [TestCase("--jsn")]
        [TestCase("-l")]
        [TestCase("no-such-file.ocmf")]
        public async Task AMistakeOnTheCommandLineIsAUsageError(String Arguments)
        {

            var exitCode = await Run(Arguments);

            Assert.Multiple(() => {

                Assert.That(exitCode,           Is.EqualTo(Program.ExitUsage));

                Assert.That(errors.ToString(),  Is.Not.Empty,
                            "A usage error belongs on standard error, where it cannot be mistaken for a report.");

                Assert.That(Printed,            Is.Empty,
                            "Nothing was verified, so nothing should have been printed as if it had been.");

            });

        }

        #endregion


        #region (private) Printed

        /// <summary>Everything the program wrote to standard output.</summary>
        private String Printed
            => output.ToString();

        #endregion

        #region (private) TemporaryFile(Name, Content)

        /// <summary>
        /// A file with the given content, in this test's own directory.
        ///
        /// Named rather than random: the program hands the file name to the
        /// library, and several formats are recognised by it.
        /// </summary>
        /// <param name="Name">The file name.</param>
        /// <param name="Content">What is in it.</param>
        private String TemporaryFile(String  Name,
                                     String  Content)
        {

            var path = Path.Combine(temporaryDirectory, Name);

            File.WriteAllText(path, Content);

            return path;

        }

        #endregion

    }

}
