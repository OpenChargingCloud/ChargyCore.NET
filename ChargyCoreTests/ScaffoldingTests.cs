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

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Verifies the project scaffolding itself: the embedded resources are reachable
    /// and the charge transparency test fixtures actually reach the output directory.
    ///
    /// Without these, a later test failure would be indistinguishable from a broken
    /// build setup, and a missing fixture would silently turn into a parser bug hunt.
    /// </summary>
    [TestFixture]
    public class ScaffoldingTests : AChargyTests
    {

        #region The_embedded_i18n_dictionary_can_be_loaded()

        [Test]
        public void The_embedded_i18n_dictionary_can_be_loaded()
        {

            var i18n = ChargyResources.GetI18NJSON();

            Assert.Multiple(() => {

                Assert.That(i18n,                             Is.Not.Null);
                Assert.That(i18n.Count,                       Is.GreaterThan(100),
                            "The i18n dictionary of ChargyCore.TS holds several hundred entries.");

                // A key the verification code actually looks up.
                Assert.That((String?) i18n["GeneralError"]?["en"],  Is.EqualTo("General Error!"));
                Assert.That((String?) i18n["GeneralError"]?["de"],  Is.EqualTo("Allgemeiner Fehler!"));

            });

        }

        #endregion

        #region The_embedded_default_validation_rules_can_be_loaded()

        [Test]
        public void The_embedded_default_validation_rules_can_be_loaded()
        {

            var validationRules = ChargyResources.GetDefaultValidationRulesJSON();

            var totalEnergyRule = validationRules["chargingSession"]?["totalEnergy"];

            Assert.Multiple(() => {

                Assert.That(totalEnergyRule,                          Is.Not.Null);
                Assert.That((String?) totalEnergyRule?["rule"]?[0],   Is.EqualTo(">"));
                Assert.That((String?) totalEnergyRule?["rule"]?[1],   Is.EqualTo("500"));
                Assert.That((String?) totalEnergyRule?["rule"]?[2],   Is.EqualTo("kWh"));
                Assert.That((String?) totalEnergyRule?["level"],      Is.EqualTo("low"));

            });

        }

        #endregion


        #region All_test_fixtures_reached_the_output_directory()

        [Test]
        public void All_test_fixtures_reached_the_output_directory()
        {

            Assert.That(Directory.Exists(TestDataDirectory),  Is.True,
                        $"The test fixtures were not copied to '{TestDataDirectory}'!");

            var fixtures = Directory.GetFiles(TestDataDirectory, "*", SearchOption.AllDirectories);

            Assert.That(fixtures.Length,  Is.EqualTo(204),
                        "ChargyCore.TS ships 204 test fixtures, all of which are shared with this port.");

        }

        #endregion

        #region All_golden_verification_reports_reached_the_output_directory()

        [Test]
        public void All_golden_verification_reports_reached_the_output_directory()
        {

            var expectedReports = Directory.GetFiles(TestDataDirectory, "*.expected.txt", SearchOption.AllDirectories);

            Assert.That(expectedReports.Length,  Is.EqualTo(25),
                        "The 25 golden verification reports are the parity contract with ChargyCore.TS.");

        }

        #endregion

        #region A_binary_fixture_is_read_byte_exactly()

        [Test]
        public void A_binary_fixture_is_read_byte_exactly()
        {

            // A ZIP archive: the local file header magic must survive the copy
            // to the output directory unmodified. If .gitattributes or MSBuild
            // ever applied a text transformation, this is where it shows up.
            var zip = ReadBinaryFixture("OCMF/OCMF-Testdata-01.zip");

            Assert.Multiple(() => {

                Assert.That(zip.Length,  Is.GreaterThan(0));
                Assert.That(zip[0],      Is.EqualTo(0x50));   // 'P'
                Assert.That(zip[1],      Is.EqualTo(0x4B));   // 'K'

            });

        }

        #endregion

        #region A_text_fixture_is_read_as_UTF8()

        [Test]
        public void A_text_fixture_is_read_as_UTF8()
        {

            var ocmf = ReadTextFixture("OCMF/OCMF-Testdata-01.ocmf");

            Assert.That(ocmf,  Does.StartWith("OCMF|"));

        }

        #endregion

        #region A_golden_report_is_read_with_its_expected_first_lines()

        [Test]
        public void A_golden_report_is_read_with_its_expected_first_lines()
        {

            var report = ReadExpectedReport("OCMF/OCMF-Testdata-01.expected.txt");
            var lines  = report.Split('\n');

            Assert.Multiple(() => {

                Assert.That(lines[0],  Is.EqualTo("format: ctr"));
                Assert.That(lines[1],  Is.EqualTo("sessions: 1"));

                // No stray carriage returns: the reports are compared line by line
                // against the very same files in ChargyCore.TS.
                Assert.That(report,    Does.Not.Contain("\r"));

            });

        }

        #endregion

        #region MIME_types_are_derived_from_the_file_name()

        [Test]
        public void MIME_types_are_derived_from_the_file_name()
        {

            Assert.Multiple(() => {

                Assert.That(MIMETypeOf("OCMF-Testdata-01.ocmf"),        Is.EqualTo("application/ocmf"));
                Assert.That(MIMETypeOf("chargeIT-Testdata-02.chargy"),  Is.EqualTo("application/chargy"));
                Assert.That(MIMETypeOf("chargeIT-Testdata-02.tar.gz"),  Is.EqualTo("application/gzip"));
                Assert.That(MIMETypeOf("chargeIT-Testdata-02.tar.bz2"), Is.EqualTo("application/x-bzip2"));
                Assert.That(MIMETypeOf("chargeIT-Testdata-02.tar"),     Is.EqualTo("application/x-tar"));
                Assert.That(MIMETypeOf("edl-40-01.xml"),                Is.EqualTo("application/xml"));
                Assert.That(MIMETypeOf("something.unknown"),            Is.EqualTo("binary/octet-stream"));

            });

        }

        #endregion


    }

}
