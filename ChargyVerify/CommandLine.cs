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

#endregion

namespace cloud.charging.open.chargy.cli
{

    /// <summary>
    /// What the user asked for.
    /// </summary>
    /// <param name="Files">The files to verify.</param>
    /// <param name="Languages">The languages to report in, in order of preference.</param>
    /// <param name="AsJSON">Whether to print the charge transparency record itself rather than a report.</param>
    /// <param name="ResolveURLs">Whether a URL found in the input may be contacted.</param>
    /// <param name="ShowHelp">Whether the user asked what this program does.</param>
    /// <param name="ShowVersion">Whether the user asked which version this is.</param>
    /// <param name="Error">Why the command line could not be understood.</param>
    public class CommandLine(IEnumerable<String>?     Files        = null,
                             IEnumerable<Languages>?  Languages    = null,
                             Boolean                  AsJSON       = false,
                             Boolean                  ResolveURLs  = false,
                             Boolean                  ShowHelp     = false,
                             Boolean                  ShowVersion  = false,
                             String?                  Error        = null)
    {

        #region Properties

        /// <summary>The files to verify.</summary>
        public IReadOnlyList<String>     Files          { get; } = Files?.    ToArray() ?? [];

        /// <summary>The languages to report in, in order of preference.</summary>
        public IReadOnlyList<Languages>  Languages      { get; } = Languages?.ToArray() ?? [];

        /// <summary>Whether to print the charge transparency record itself rather than a report.</summary>
        public Boolean                   AsJSON         { get; } = AsJSON;

        /// <summary>Whether a URL found in the input may be contacted.</summary>
        public Boolean                   ResolveURLs    { get; } = ResolveURLs;

        /// <summary>Whether the user asked what this program does.</summary>
        public Boolean                   ShowHelp       { get; } = ShowHelp;

        /// <summary>Whether the user asked which version this is.</summary>
        public Boolean                   ShowVersion    { get; } = ShowVersion;

        /// <summary>Why the command line could not be understood.</summary>
        public String?                   Error          { get; } = Error;

        #endregion


        #region (static) Parse(Arguments)

        /// <summary>
        /// Read a command line.
        /// </summary>
        /// <param name="Arguments">What the shell handed over.</param>
        public static CommandLine Parse(String[] Arguments)
        {

            var files        = new List<String>();
            var languages    = new List<Languages>();
            var asJSON       = false;
            var resolveURLs  = false;
            var showHelp     = false;
            var showVersion  = false;

            for (var i = 0; i < Arguments.Length; i++)
            {

                var argument = Arguments[i];

                switch (argument)
                {

                    case "-h":
                    case "--help":
                        showHelp = true;
                        break;

                    case "-V":
                    case "--version":
                        showVersion = true;
                        break;

                    case "--json":
                        asJSON = true;
                        break;

                    case "--resolve-urls":
                        resolveURLs = true;
                        break;

                    case "-l":
                    case "--language":

                        if (i + 1 >= Arguments.Length)
                            return new CommandLine(Error: $"'{argument}' needs a language, e.g. 'de' or 'en'.");

                        if (!Enum.TryParse<Languages>(Arguments[++i], true, out var language))
                            return new CommandLine(Error: $"'{Arguments[i]}' is not a language this build knows.");

                        languages.Add(language);
                        break;

                    // Everything else is a file — including something that looks
                    // like an option and is not one, because a charge transparency
                    // file may well be called anything at all.
                    default:

                        if (argument.StartsWith('-') && argument.Length > 1)
                            return new CommandLine(Error: $"'{argument}' is not an option this program has.");

                        files.Add(argument);
                        break;

                }

            }

            return new CommandLine(
                       files,
                       languages,
                       asJSON,
                       resolveURLs,
                       showHelp,
                       showVersion
                   );

        }

        #endregion

        #region (static) HelpText

        /// <summary>
        /// What this program does, for somebody who has just typed its name.
        /// </summary>
        public static String HelpText

            => """
               Verifies charge transparency files, as defined by the German Calibration
               Law ("Eichrecht"), and says what they contain and whether their signatures
               hold.

               Usage:
                 chargy-verify [options] <file>...

               Any file a charging station operator hands out will do: an OCMF string, a
               chargeIT or SAFE XML container, a ZIP or tar.gz of several, a PDF/A-3
               invoice with the data embedded, or a photograph of the QR code on the
               station. Public key files may be passed alongside the records they belong
               to; without them a signature cannot be checked at all.

               Options:
                 -l, --language <code>  The language for the messages the library produces,
                                        e.g. "de" or "en". May be given more than once,
                                        most preferred first. The headings this program
                                        writes itself stay English.
                     --json             Print the charge transparency record as JSON
                                        instead of a report.
                     --resolve-urls     Allow a URL found in the input to be contacted.
                                        Off by default: following such a link tells the
                                        operator that you are looking at this charging
                                        session, right now.
                 -h, --help             Show this text.
                 -V, --version          Show the version.

               Exit codes:
                 0   Everything that was read verified.
                 1   Something was read and did not verify.
                 2   The input could not be read as charge transparency data.
                 64  The command line could not be understood.
               """;

        #endregion

    }

}
