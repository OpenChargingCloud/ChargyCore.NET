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
    /// Tests for OCMF and for the SAFE XML containers that carry it, against the
    /// golden reports shared with ChargyCore.TS.
    /// </summary>
    [TestFixture]
    public class OCMFTests : AChargyTests
    {

        #region Testdata01_WithItsPublicKey()

        /// <summary>
        /// One OCMF document with its public key alongside.
        ///
        /// It holds a single reading, so the signature is valid while the session
        /// is not: one reading says a meter stood at a value at a moment, which
        /// is not a statement about delivered energy.
        /// </summary>
        [Test]
        public Task Testdata01_WithItsPublicKey()

            => ExpectReport(
                   [ "OCMF/OCMF-Testdata-01.ocmf", "OCMF/OCMF-Testdata-01_publicKey.txt" ],
                   "OCMF/OCMF-Testdata-01.expected.txt"
               );

        #endregion

        #region DZG01_WithoutAPublicKey()

        /// <summary>
        /// Without a public key nothing can be concluded about the signature.
        /// </summary>
        [Test]
        public Task DZG01_WithoutAPublicKey()

            => ExpectReport(
                   "OCMF/OCMF-DZG-01.ocmf",
                   "OCMF/OCMF-DZG-01.expected.txt"
               );

        #endregion


        #region SAFETestdata02_AllNamespaceVariants(Fixture)

        /// <summary>
        /// The same charging session, in a container that declares its XML
        /// namespace, one that declares an empty one, and one that declares none.
        ///
        /// The SAFE transparency software v1.0 does not understand its own
        /// namespace, so all three occur in the wild and all three have to reach
        /// the same report.
        /// </summary>
        [TestCase("SAFE/SAFE-Testdata-02_withXMLNamespace.xml")]
        [TestCase("SAFE/SAFE-Testdata-02_withoutXMLNamespace.xml")]
        [TestCase("SAFE/SAFE-Testdata-02_emptyXMLNamespace.xml")]
        public Task SAFETestdata02_AllNamespaceVariants(String Fixture)

            => ExpectReport(
                   Fixture,
                   "SAFE/SAFE-Testdata-02.expected.txt"
               );

        #endregion

        #region SAFETestdata02_AsAPDFInvoice()

        /// <summary>
        /// The same record again, this time embedded in a PDF/A-3 invoice.
        ///
        /// The whole pipeline in one step — PDF, attachment, XML, container,
        /// OCMF, signature — and it has to end at the very same report as the
        /// standalone XML file.
        /// </summary>
        [Test]
        public Task SAFETestdata02_AsAPDFInvoice()

            => ExpectReport(
                   "SAFE/SAFE-Testdata-02_withXMLNamespace.pdf",
                   "SAFE/SAFE-Testdata-02.expected.txt"
               );

        #endregion

        #region SAFETestdata03_ASingleMeasurementIsNotASession()

        /// <summary>
        /// A container with one signed reading. The signature holds; the session
        /// does not.
        /// </summary>
        [Test]
        public Task SAFETestdata03_ASingleMeasurementIsNotASession()

            => ExpectReport(
                   "SAFE/SAFE-Testdata-03_singleMeasurement_ShouldFail.xml",
                   "SAFE/SAFE-Testdata-03.expected.txt"
               );

        #endregion

        #region SAFETestdata04()

        /// <summary>
        /// A further SAFE container carrying OCMF.
        /// </summary>
        [Test]
        public Task SAFETestdata04()

            => ExpectReport(
                   "SAFE/SAFE-Testdata-04.xml",
                   "SAFE/SAFE-Testdata-04.expected.txt"
               );

        #endregion

        #region SAFETestdata01_OCMFv01()

        /// <summary>
        /// An OCMF v0.1 payload in a SAFE container.
        /// </summary>
        [Test]
        public Task SAFETestdata01_OCMFv01()

            => ExpectReport(
                   "SAFE/SAFE-Testdata-01_OCMFv0.1.xml",
                   "SAFE/SAFE-Testdata-01_OCMFv0.1.expected.txt"
               );

        #endregion


    }

}
