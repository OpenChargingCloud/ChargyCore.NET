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

using cloud.charging.open.chargy.IO;
using cloud.charging.open.chargy.qrcodes;

#endregion

namespace cloud.charging.open.chargy.cli
{

    /// <summary>
    /// Verifies charge transparency files from the command line.
    ///
    /// This is a worked example of the ChargyCore API rather than a product: the
    /// whole of it is "read the files, hand them to the detector, print what came
    /// back". Everything interesting happens inside the library, which is the
    /// point — an application should not have to know that an OCMF string, a ZIP
    /// of chargeIT containers and a photograph of a QR code are three different
    /// problems.
    /// </summary>
    public static class Program
    {

        #region Data

        /// <summary>Everything that was read verified.</summary>
        public const Int32 ExitVerified     = 0;

        /// <summary>Something was read and did not verify.</summary>
        public const Int32 ExitNotVerified  = 1;

        /// <summary>The input could not be read as charge transparency data.</summary>
        public const Int32 ExitUnreadable   = 2;

        /// <summary>The command line could not be understood.</summary>
        public const Int32 ExitUsage        = 64;

        #endregion


        #region Main(Arguments)

        /// <summary>
        /// Verify the given files.
        /// </summary>
        /// <param name="Arguments">What the shell handed over.</param>
        public static async Task<Int32> Main(String[] Arguments)
        {

            // Numbers are printed the same way wherever this runs. A meter
            // reading written as "50,387945" on one machine and "50.387945" on
            // the next cannot be compared, pasted into a bug report, or piped
            // into anything — and the language of the messages is chosen
            // separately, with "--language".
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            #region What was asked for

            var commandLine = CommandLine.Parse(Arguments);

            if (commandLine.Error is String error)
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine("Try 'chargy-verify --help'.");
                return ExitUsage;
            }

            if (commandLine.ShowVersion)
            {
                Console.WriteLine(VersionText);
                return ExitVerified;
            }

            if (commandLine.ShowHelp || commandLine.Files.Count == 0)
            {
                Console.WriteLine(CommandLine.HelpText);
                return commandLine.ShowHelp ? ExitVerified : ExitUsage;
            }

            #endregion

            #region The files, as an application would hand them over

            var files = new List<FileInfo>();

            foreach (var path in commandLine.Files)
            {

                if (!File.Exists(path))
                {
                    Console.Error.WriteLine($"There is no file '{path}'.");
                    return ExitUsage;
                }

                files.Add(
                    new FileInfo(
                        // The name is handed over, not just the bytes: some
                        // formats are recognised by it, and a public key file is
                        // paired with its record by name.
                        Path.GetFileName(path),
                        await File.ReadAllBytesAsync(path).ConfigureAwait(false),
                        // Chargy sniffs the content type itself; a guess from the
                        // extension is only a starting point.
                        ContentTypes.FromFileName(Path.GetFileName(path)),
                        Path.GetFullPath(path)
                    )
                );

            }

            #endregion

            var i18n      = I18NDictionary.Default(
                                commandLine.Languages.Count > 0
                                    ? commandLine.Languages
                                    : null
                            );

            var detector  = new ContentFormatDetector(
                                i18n,
                                ChargeTransparencyFormats.All(i18n),
                                new PDFAttachmentExtractor(),
                                new QRCodeDecoder(),
                                // Without a resolver nothing is fetched, which is
                                // the privacy-preserving default.
                                commandLine.ResolveURLs
                                    ? new HTTPURLResolver()
                                    : null
                            );

            var result    = await detector.DetectAndConvertContentFormat(files).ConfigureAwait(false);

            return new Report(i18n, Console.Out).Print(result, commandLine.AsJSON);

        }

        #endregion

        #region (private, static) VersionText

        /// <summary>
        /// Which version of the library is doing the verifying.
        /// </summary>
        private static String VersionText

            => $"chargy-verify {typeof(Program).      Assembly.GetName().Version?.ToString(3) ?? "?"}, " +
               $"ChargyCore {typeof(ChargeTransparencyRecord).Assembly.GetName().Version?.ToString(3) ?? "?"} " +
               $"({typeof(ChargeTransparencyRecord).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "no build metadata"})";

        #endregion

    }

}
