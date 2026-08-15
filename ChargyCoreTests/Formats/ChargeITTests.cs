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

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the chargeIT mobility container formats and the meters they carry,
    /// against the golden reports shared with ChargyCore.TS.
    /// </summary>
    [TestFixture]
    public class ChargeITTests : AChargyTests
    {

        #region ChargeITTestdata02(Fixture)

        /// <summary>
        /// The chargeIT container format that predates OCMF, carrying readings an
        /// EMH meter signed one by one.
        ///
        /// The same record is offered in five wrappings — bare, and inside four
        /// kinds of archive — and all five have to reach the identical report:
        /// how a file travelled says nothing about what it contains.
        /// </summary>
        /// <param name="Fixture">A fixture path relative to "TestData".</param>
        [TestCase("chargeIT/chargeIT-Testdata-02.chargy")]
        [TestCase("chargeIT/chargeIT-Testdata-02.zip")]
        [TestCase("chargeIT/chargeIT-Testdata-02.tar")]
        [TestCase("chargeIT/chargeIT-Testdata-02.tar.gz")]
        [TestCase("chargeIT/chargeIT-Testdata-02.tar.bz2")]
        public Task ChargeITTestdata02(String Fixture)

            => ExpectReport(
                   Fixture,
                   "chargeIT/chargeIT-Testdata-02.expected.txt"
               );

        #endregion

        #region BSMWithinAChargeITContainer(Fixture, ExpectedFixture)

        /// <summary>
        /// A BSM meter inside a chargeIT container, in both generations of that
        /// container: the early one that declares no context at all, and the later
        /// one that names itself and adds the charging station and the costs.
        /// </summary>
        /// <param name="Fixture">A fixture path relative to "TestData".</param>
        /// <param name="ExpectedFixture">The golden report to compare against.</param>
        [TestCase("chargeIT/bsm/bsm-ws36a-good.json",
                  "chargeIT/bsm/bsm-ws36a-good.expected.txt")]
        [TestCase("chargeIT/new_container_format/bsm-ws36a-good-new-style-header.json",
                  "chargeIT/new_container_format/bsm-ws36a-good-new-style-header.expected.txt")]
        [TestCase("chargeIT/new_container_format/bsm-ws36a-good-with-non-zero-scale-factors.json",
                  "chargeIT/new_container_format/bsm-ws36a-good-with-non-zero-scale-factors.expected.txt")]
        [TestCase("chargeIT/new_container_format/ev-charging-chargy-with-display-format-hints.json",
                  "chargeIT/new_container_format/ev-charging-chargy-with-display-format-hints.expected.txt")]
        public Task BSMWithinAChargeITContainer(String  Fixture,
                                                String  ExpectedFixture)

            => ExpectReport(Fixture, ExpectedFixture);

        #endregion

        #region BSMSignsItsReadingsAsOCMF(Fixture, ExpectedFixture)

        /// <summary>
        /// A BSM WS36A meter that signs its readings as OCMF inside a SAFE XML
        /// container.
        ///
        /// Despite living among the chargeIT fixtures, neither of these touches the
        /// chargeIT container at all: the path is SAFE XML to OCMF, and what makes
        /// them worth having here is the meter, not the format.
        /// </summary>
        /// <param name="Fixture">A fixture path relative to "TestData".</param>
        /// <param name="ExpectedFixture">The golden report to compare against.</param>
        [TestCase("chargeIT/bsm/ocmf.xml",         "chargeIT/bsm/ocmf.expected.txt")]
        [TestCase("chargeIT/bsm/ocmf_withIF.xml",  "chargeIT/bsm/ocmf_withIF.expected.txt")]
        public Task BSMSignsItsReadingsAsOCMF(String  Fixture,
                                              String  ExpectedFixture)

            => ExpectReport(Fixture, ExpectedFixture);

        #endregion

        #region TheIdentificationFlagsReachTheRecord(Fixture, ExpectedFlags)

        /// <summary>
        /// How the driver identified themselves has to arrive in the record.
        ///
        /// The verification report does not print the identification flags, so the
        /// two golden files above would pass whether this worked or not — they did,
        /// and it did not. Asserted directly instead: flags that are there have to
        /// reach the record, and a meter that sent none has to yield the empty list
        /// the OCMF specification asks for rather than nothing at all.
        /// </summary>
        /// <param name="Fixture">A fixture path relative to "TestData".</param>
        /// <param name="ExpectedFlags">The identification flags the document carries.</param>
        [TestCase("chargeIT/bsm/ocmf_withIF.xml",  new[] { "RFID_PLAIN", "OCPP_AUTH" })]
        [TestCase("chargeIT/bsm/ocmf.xml",         new String[0])]
        public async Task TheIdentificationFlagsReachTheRecord(String    Fixture,
                                                               String[]  ExpectedFlags)
        {

            var result = await VerifyFixtures([ Fixture ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            Assert.That(
                ((ChargeTransparencyRecord) result).ChargingSessions[0].AuthorizationStart?.IdentificationFlags,
                Is.EqualTo(ExpectedFlags)
            );

        }

        #endregion

    }

}
