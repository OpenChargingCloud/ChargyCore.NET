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
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the identity and the time span of an OCMF charging session.
    ///
    /// OCMF carries neither a session identifier nor an explicit start and end, so
    /// all three are derived from the documents — and all three have to be
    /// reproducible, because verifying the same record twice has to produce the
    /// same report. An identifier that moved would make two verifications of one
    /// charging session look like two charging sessions.
    ///
    /// It has moved once already, which is why these tests exist as their own
    /// file: the identifier used to be hashed over the document text, and a
    /// checkout that rewrote line endings — the default on Windows — gave an
    /// otherwise identical record a different identity. Signature verification
    /// never noticed, because it canonicalises the payload before checking.
    /// </summary>
    [TestFixture]
    public partial class OCMFSessionIdentityTests : AChargyTests
    {

        #region TheIdentityIsDerivedFromTheDocumentRatherThanFixed()

        /// <summary>
        /// The identifier is a hash of the record, not a constant somebody wrote
        /// into the parser.
        /// </summary>
        [Test]
        public async Task TheIdentityIsDerivedFromTheDocumentRatherThanFixed()

            => Assert.That(
                   (await SessionOf("OCMF/OCMF-Testdata-01.ocmf")).Id,
                   Does.Match(SessionIdRegex())
               );

        #endregion

        #region TheSameRecordAlwaysGetsTheSameIdentity()

        /// <summary>
        /// Reading the same file twice yields the same charging session.
        /// </summary>
        [Test]
        public async Task TheSameRecordAlwaysGetsTheSameIdentity()

            => Assert.That(
                   (await SessionOf("OCMF/OCMF-Testdata-01.ocmf")).Id,
                   Is.EqualTo((await SessionOf("OCMF/OCMF-Testdata-01.ocmf")).Id)
               );

        #endregion

        #region TheLineEndingsOfTheFileDoNotChangeTheIdentity()

        /// <summary>
        /// The identifier describes the record, not the bytes it happened to
        /// arrive as.
        ///
        /// The DZG fixture is pretty-printed JSON spanning dozens of lines, so it
        /// is the one that shows this: rewriting every newline changes the file
        /// substantially and must change nothing about the charging session it
        /// describes. This is the regression that already happened once.
        /// </summary>
        [Test]
        public async Task TheLineEndingsOfTheFileDoNotChangeTheIdentity()
        {

            var unix     = ReadTextFixture("OCMF/OCMF-DZG-01.ocmf").Replace("\r\n", "\n");
            var windows  = unix.Replace("\n", "\r\n");

            Assert.That(windows, Is.Not.EqualTo(unix), "the fixture spans no lines, so this proves nothing");

            Assert.That(
                (await SessionOfText(unix)).   Id,
                Is.EqualTo((await SessionOfText(windows)).Id)
            );

        }

        #endregion

        #region TheFormattingOfThePayloadDoesNotChangeTheIdentity()

        /// <summary>
        /// Neither does reformatting the signed payload itself.
        ///
        /// This is the mechanism the line-ending test only shows a symptom of:
        /// the identifier is hashed over the canonical form of the parsed payload
        /// and signature, never over their text. Reformatting does break the
        /// signature — that covers the payload character for character — and the
        /// point is precisely that the two answers are independent. A record can
        /// stop verifying without becoming a different charging session.
        ///
        /// No counterpart upstream; the mechanism is only tested there through
        /// the line endings.
        /// </summary>
        [Test]
        public async Task TheFormattingOfThePayloadDoesNotChangeTheIdentity()
        {

            // The single-document fixture, because splitting on the separator is
            // only unambiguous for one document — the DZG file holds two, which
            // is what makes it the right fixture for the test above and the
            // wrong one for this.
            var original     = ReadTextFixture("OCMF/OCMF-Testdata-01.ocmf");
            var parts        = original.Split('|');

            Assert.That(parts, Has.Length.EqualTo(3), "this fixture is no longer a single OCMF document");

            var reformatted  = $"{parts[0]}|{ChargyLib.ParseJSON(parts[1]).ToString(Formatting.Indented)}|{parts[2]}";

            Assert.That(reformatted, Is.Not.EqualTo(original));

            Assert.That(
                (await SessionOfText(reformatted, "OCMF-Testdata-01.ocmf")).Id,
                Is.EqualTo((await SessionOf("OCMF/OCMF-Testdata-01.ocmf")).Id)
            );

        }

        #endregion

        #region DifferentRecordsGetDifferentIdentities()

        /// <summary>
        /// Two different charging sessions are told apart.
        /// </summary>
        [Test]
        public async Task DifferentRecordsGetDifferentIdentities()
        {

            var testdata  = (await SessionOf("OCMF/OCMF-Testdata-01.ocmf")).Id;
            var dzg       = (await SessionOf("OCMF/OCMF-DZG-01.ocmf")).     Id;

            Assert.Multiple(() => {
                Assert.That(testdata, Is.Not.Empty);
                Assert.That(dzg,      Is.Not.Empty);
                Assert.That(testdata, Is.Not.EqualTo(dzg));
            });

        }

        #endregion

        #region TheSessionSpansItsReadings()

        /// <summary>
        /// The start and the end of the charging session are the earliest and the
        /// latest reading it holds.
        ///
        /// Ordered by instant rather than as text: the timestamps keep the offset
        /// the meter reported, so sorting them as strings would order them by
        /// their local reading, which is not the same order.
        /// </summary>
        [Test]
        public async Task TheSessionSpansItsReadings()
        {

            var session     = await SessionOf("OCMF/OCMF-DZG-01.ocmf");

            var timestamps  = session.Measurements.
                                  SelectMany(measurement => measurement.Values).
                                  Select    (value       => DateTimeOffset.Parse(value.Timestamp, CultureInfo.InvariantCulture)).
                                  ToArray();

            Assert.That(timestamps, Is.Not.Empty);

            Assert.Multiple(() => {

                Assert.That(session.Begin, Is.Not.Null.And.Not.EqualTo("?"));
                Assert.That(session.End,   Is.Not.Null.And.Not.EqualTo("?"));

                Assert.That(DateTimeOffset.Parse(session.Begin!, CultureInfo.InvariantCulture),  Is.EqualTo(timestamps.Min()));
                Assert.That(DateTimeOffset.Parse(session.End!,   CultureInfo.InvariantCulture),  Is.EqualTo(timestamps.Max()));

            });

        }

        #endregion

        #region TheRecordSpansTheSameTimeAsItsSession()

        /// <summary>
        /// What the record says about its time span is what its charging session
        /// says.
        /// </summary>
        [Test]
        public async Task TheRecordSpansTheSameTimeAsItsSession()
        {

            var record   = await RecordOf("OCMF/OCMF-DZG-01.ocmf");
            var session  = record.ChargingSessions[0];

            Assert.Multiple(() => {
                Assert.That(record.Begin,  Is.EqualTo(session.Begin));
                Assert.That(record.End,    Is.EqualTo(session.End));
                Assert.That(record.Begin,  Is.Not.Null.And.Not.EqualTo("?"));
            });

        }

        #endregion


        #region (private, static) RecordOf / SessionOf / SessionOfText

        /// <summary>
        /// Read a fixture into a charge transparency record.
        /// </summary>
        /// <param name="FixtureName">A fixture path relative to "TestData".</param>
        private static async Task<ChargeTransparencyRecord> RecordOf(String FixtureName)
        {

            var result = await VerifyFixtures([ FixtureName ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            return (ChargeTransparencyRecord) result;

        }

        /// <summary>
        /// Read a fixture into its one charging session.
        /// </summary>
        /// <param name="FixtureName">A fixture path relative to "TestData".</param>
        private static async Task<ChargingSession> SessionOf(String FixtureName)

            => (await RecordOf(FixtureName)).ChargingSessions[0];

        /// <summary>
        /// Read OCMF text — rather than a file — into its one charging session.
        /// </summary>
        /// <param name="Text">An OCMF document, as it would have arrived.</param>
        /// <param name="FileName">The name it would have arrived under.</param>
        private static async Task<ChargingSession> SessionOfText(String  Text,
                                                                 String  FileName = "OCMF-DZG-01.ocmf")
        {

            var result = await Verify([
                                   new FileInfo(
                                       FileName,
                                       Encoding.UTF8.GetBytes(Text),
                                       "application/ocmf"
                                   )
                               ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            return ((ChargeTransparencyRecord) result).ChargingSessions[0];

        }

        #endregion

        #region (private) Regular expressions

        [GeneratedRegex("^OCMF-[0-9a-f]{64}$")]
        private static partial Regex SessionIdRegex();

        #endregion

    }

}
