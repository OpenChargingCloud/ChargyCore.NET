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

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.cli;

#endregion

namespace cloud.charging.open.chargy.tests.CLI
{

    /// <summary>
    /// Tests for the command line of "chargy-verify".
    ///
    /// A command line is an interface like any other: somebody's script passes
    /// arguments and reads an exit code, and both have to keep meaning what they
    /// meant yesterday.
    /// </summary>
    [TestFixture]
    public class CommandLineTests
    {

        #region NothingAskedForIsNothingSwitchedOn()

        /// <summary>
        /// An empty command line asks for nothing, and in particular asks for no
        /// network.
        ///
        /// `--resolve-urls` defaulting to off is the one flag here whose default
        /// is a promise rather than a convenience: following a link found in a
        /// charge transparency file tells its operator that somebody is looking
        /// at that charging session, right now.
        /// </summary>
        [Test]
        public void NothingAskedForIsNothingSwitchedOn()
        {

            var commandLine = CommandLine.Parse([]);

            Assert.Multiple(() => {
                Assert.That(commandLine.Files,        Is.Empty);
                Assert.That(commandLine.Languages,    Is.Empty);
                Assert.That(commandLine.AsJSON,       Is.False);
                Assert.That(commandLine.ResolveURLs,  Is.False);
                Assert.That(commandLine.ShowHelp,     Is.False);
                Assert.That(commandLine.ShowVersion,  Is.False);
                Assert.That(commandLine.Error,        Is.Null);
            });

        }

        #endregion

        #region EveryFlagIsRecognizedUnderBothOfItsNames(Arguments)

        /// <summary>
        /// The short and the long spelling of a flag mean the same thing.
        /// </summary>
        /// <param name="Arguments">A command line.</param>
        [TestCase("-h")]
        [TestCase("--help")]
        public void HelpIsRecognizedUnderBothOfItsNames(String Arguments)
        {
            Assert.That(CommandLine.Parse([ Arguments ]).ShowHelp,  Is.True);
        }

        /// <summary>
        /// The short and the long spelling of the version flag.
        /// </summary>
        /// <param name="Arguments">A command line.</param>
        [TestCase("-V")]
        [TestCase("--version")]
        public void VersionIsRecognizedUnderBothOfItsNames(String Arguments)
        {
            Assert.That(CommandLine.Parse([ Arguments ]).ShowVersion,  Is.True);
        }

        #endregion

        #region TheFilesAreKeptInTheOrderTheyWereGiven()

        /// <summary>
        /// Everything that is not an option is a file, and the order survives.
        ///
        /// It matters: a public key file is paired with the record it belongs to,
        /// and somebody passing a record and its key expects both to arrive.
        /// </summary>
        [Test]
        public void TheFilesAreKeptInTheOrderTheyWereGiven()
        {

            var commandLine = CommandLine.Parse([ "record.ocmf", "--json", "publicKey.txt" ]);

            Assert.Multiple(() => {
                Assert.That(commandLine.Files,   Is.EqualTo(new[] { "record.ocmf", "publicKey.txt" }));
                Assert.That(commandLine.AsJSON,  Is.True);
            });

        }

        #endregion

        #region TheLanguagesKeepTheirOrderOfPreference()

        /// <summary>
        /// "--language" may be given more than once, most preferred first.
        /// </summary>
        [Test]
        public void TheLanguagesKeepTheirOrderOfPreference()
        {

            var commandLine = CommandLine.Parse([ "-l", "de", "--language", "en", "record.ocmf" ]);

            Assert.Multiple(() => {
                Assert.That(commandLine.Languages,  Is.EqualTo(new[] { Languages.de, Languages.en }));
                Assert.That(commandLine.Files,      Is.EqualTo(new[] { "record.ocmf" }));
                Assert.That(commandLine.Error,      Is.Null);
            });

        }

        #endregion

        #region ALanguageIsAcceptedInAnyCase()

        /// <summary>
        /// "DE" is the same language as "de".
        /// </summary>
        [Test]
        public void ALanguageIsAcceptedInAnyCase()
        {
            Assert.That(CommandLine.Parse([ "-l", "DE" ]).Languages,  Is.EqualTo(new[] { Languages.de }));
        }

        #endregion

        #region WhatCannotBeUnderstoodIsSaidRatherThanGuessedAt(Arguments, Expected)

        /// <summary>
        /// A command line that makes no sense produces a message naming what was
        /// wrong with it — never a silent assumption.
        ///
        /// Guessing would be the worse failure by far: "--languge de" quietly
        /// treated as a file name would report on a charging session in the wrong
        /// language while looking like it worked, and "-l" with nothing after it
        /// would silently mean "no preference".
        /// </summary>
        /// <param name="Arguments">A command line.</param>
        /// <param name="Expected">Part of the message it deserves.</param>
        [TestCase(new[] { "--languge", "de" },   "'--languge' is not an option this program has.")]
        [TestCase(new[] { "-x" },                "'-x' is not an option this program has.")]
        [TestCase(new[] { "-l" },                "'-l' needs a language, e.g. 'de' or 'en'.")]
        [TestCase(new[] { "--language" },        "'--language' needs a language, e.g. 'de' or 'en'.")]
        [TestCase(new[] { "-l", "klingon" },     "'klingon' is not a language this build knows.")]
        public void WhatCannotBeUnderstoodIsSaidRatherThanGuessedAt(String[]  Arguments,
                                                                    String    Expected)
        {

            var commandLine = CommandLine.Parse(Arguments);

            Assert.Multiple(() => {
                Assert.That(commandLine.Error,  Is.EqualTo(Expected));
                Assert.That(commandLine.Files,  Is.Empty,
                            "A command line that was not understood must not half-apply.");
            });

        }

        #endregion

        #region AFileNamedLikeNothingElseIsStillAFile(FileName)

        /// <summary>
        /// A lone "-" is a file name here, not a flag and not standard input.
        ///
        /// This program does not read standard input, so it takes the name it was
        /// literally given and reports that there is no such file — which is the
        /// truth, rather than "'-' is not an option this program has", which would
        /// send the reader looking for a typo they did not make.
        /// </summary>
        /// <param name="FileName">A file name.</param>
        [TestCase("-")]
        [TestCase("record.ocmf")]
        [TestCase("2026-08-16.ocmf")]
        public void AFileNamedLikeNothingElseIsStillAFile(String FileName)
        {

            var commandLine = CommandLine.Parse([ FileName ]);

            Assert.Multiple(() => {
                Assert.That(commandLine.Error,  Is.Null);
                Assert.That(commandLine.Files,  Is.EqualTo(new[] { FileName }));
            });

        }

        #endregion

        #region SomethingThatLooksLikeAnOptionIsTreatedAsOne()

        /// <summary>
        /// The deliberate limit of the rule above: a name that looks like a flag
        /// is read as one, even though a charge transparency file could be called
        /// that.
        ///
        /// A mistyped flag is far more common than a file named after one, and the
        /// two failures are not equally bad — "there is no file '--jsn'" sends
        /// somebody hunting for a missing file, while "'--jsn' is not an option
        /// this program has" names the actual mistake. Anyone with such a file can
        /// still pass "./--jsn".
        /// </summary>
        [Test]
        public void SomethingThatLooksLikeAnOptionIsTreatedAsOne()
        {

            Assert.Multiple(() => {

                Assert.That(CommandLine.Parse([ "--jsn" ]).Error,     Is.EqualTo("'--jsn' is not an option this program has."));

                Assert.That(CommandLine.Parse([ "./--jsn" ]).Files,   Is.EqualTo(new[] { "./--jsn" }),
                            "A path is a way out for a file that really is called that.");

            });

        }

        #endregion

        #region TheHelpTextStatesEveryExitCodeTheProgramCanReturn()

        /// <summary>
        /// Every exit code the program has is documented in its help.
        ///
        /// The exit code is the whole interface for a script, and a code that is
        /// returned but not written down cannot be handled by anyone who did not
        /// read the source.
        /// </summary>
        [Test]
        public void TheHelpTextStatesEveryExitCodeTheProgramCanReturn()
        {

            var help = CommandLine.HelpText;

            Assert.Multiple(() => {

                foreach (var exitCode in new[] { Program.ExitVerified,
                                                 Program.ExitNotVerified,
                                                 Program.ExitUnreadable,
                                                 Program.ExitUsage })
                {
                    Assert.That(help,  Does.Match($@"(?m)^\s+{exitCode}\s+\S"),
                                $"Exit code {exitCode} is not explained in the help text.");
                }

                foreach (var option in new[] { "-l, --language", "--json", "--resolve-urls", "-h, --help", "-V, --version" })
                    Assert.That(help,  Does.Contain(option),
                                $"'{option}' is not mentioned in the help text.");

            });

        }

        #endregion

    }

}
