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

using cloud.charging.open.chargy.Formats.Alfen;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the Alfen format, against the golden reports shared with
    /// ChargyCore.TS.
    ///
    /// These are the first tests in this port that verify a real signature over
    /// real charging data: a reading that a real energy meter really produced,
    /// and an answer that has to match the TypeScript implementation exactly.
    /// </summary>
    [TestFixture]
    public class AlfenTests : AChargyTests
    {

        #region Testdata03_SAFEXMLContainer()

        /// <summary>
        /// An Alfen charging session inside a SAFE XML container.
        /// </summary>
        [Test]
        public Task Testdata03_SAFEXMLContainer()

            => ExpectReport(
                   "ALFEN/ALFEN-Testdata-03_SAFEXMLContainer.xml",
                   "ALFEN/ALFEN-Testdata-03_SAFEXMLContainer.expected.txt"
               );

        #endregion

        #region Testdata03_AsQRCode(Fixture)

        /// <summary>
        /// The same charging session, photographed as a QR code.
        ///
        /// The image goes through the whole pipeline — decode, XML, container,
        /// Alfen, signature — and has to end at the very same report as the file.
        /// An EV driver photographing their receipt must not get a different
        /// answer from one downloading it.
        /// </summary>
        [TestCase("ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_asQRCode.png")]
        [TestCase("ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_asQRCode.jpg")]
        [TestCase("ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_asQRCode.svg")]
        public Task Testdata03_AsQRCode(String Fixture)

            => ExpectReport(
                   Fixture,
                   "ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_asQRCode.expected.txt"
               );

        #endregion

        #region Testdata03_WithChargyExtensions()

        /// <summary>
        /// The same charging session, with the Chargy extensions to the container.
        /// </summary>
        [Test]
        public Task Testdata03_WithChargyExtensions()

            => ExpectReport(
                   "ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_withExtensions.xml",
                   "ALFEN/ALFEN-Testdata-03_SAFEXMLContainer_withExtensions.expected.txt"
               );

        #endregion


        #region Testdata04_SignedYetImplausible()

        /// <summary>
        /// A correctly signed charging session that nevertheless claims 2.1 MWh.
        ///
        /// The signature is genuine, so the cryptography has nothing to object to
        /// — and that is exactly why the plausibility rules exist. A meter can be
        /// broken, or misread, and still sign what it believes.
        /// </summary>
        [Test]
        public Task Testdata04_SignedYetImplausible()

            => ExpectReport(
                   "ALFEN/ALFEN-Testdata-04_2_1MWh_SAFEXMLContainer.xml",
                   "ALFEN/ALFEN-Testdata-04_2_1MWh_SAFEXMLContainer.expected.txt"
               );

        #endregion

        #region Testdata05_EightIntermediateValues()

        /// <summary>
        /// A charging session with eight intermediate readings.
        /// </summary>
        [Test]
        public Task Testdata05_EightIntermediateValues()

            => ExpectReport(
                   "ALFEN/ALFEN-Testdata-05_1_9MWh_8Intermediates_SAFEXMLContainer.xml",
                   "ALFEN/ALFEN-Testdata-05_1_9MWh_8Intermediates_SAFEXMLContainer.expected.txt"
               );

        #endregion


        #region ASignatureBeginningWith0x30_Verifies()

        /// <summary>
        /// A reading whose signature begins with 0x30, as one Alfen signature in
        /// 256 does.
        ///
        /// The signature is r and s side by side, 48 bytes on secp192r1, and an r
        /// beginning with the byte that begins every DER SEQUENCE made it look
        /// like one. It was read as DER, and a genuine reading was reported as
        /// signed invalidly.
        ///
        /// Written by the Modbus/TLS energy meter simulator, whose tests found
        /// this (OpenChargingCloud/ModbusTLSEnergyMeter): its Alfen writer signs
        /// on secp192r1 with a key of its own, and this record is one whose
        /// signature happens to begin with 0x30.
        /// </summary>
        [Test]
        public void ASignatureBeginningWith0x30_Verifies()
        {

            const String record = "AP;0;3;AKNLSR2RNJASZAVKXX4RWLWGM4PB5SCEI75NWLVA;" +
                                  "BIFQYDIOB4IBCEQTOYYS4MFLZUAQEAYEAUDAOCAJBIAAAAAA5ABQAAGACC2WUAIAAEEAAAA6ABAOEAIAAAAAAACUIVJVIVCBI4YDCAAAAAAAAAAAAAAAAAAHAAAAAAIAAAAA====;" +
                                  "GCNYRNJLOZGYGS5WSHKR5RVPECFUQ4LFKRHZQPTO6YPBPQGDXMVELH5ZK3JRXV6GNKADORTPZTN2E===;";

            var i18n    = I18NDictionary.Default();
            var parsed  = new AlfenFormat(i18n).TryParseText(record);

            Assert.That(parsed, Is.InstanceOf<ChargeTransparencyRecord>());

            var transparencyRecord  = (ChargeTransparencyRecord) parsed;
            var value               = transparencyRecord.ChargingSessions.First().Measurements.First().Values.First();
            var energyMeter         = transparencyRecord.ChargingStations.First().EVSEs.First().EnergyMeters.First();

            var verdict             = new AlfenCrypt01(i18n, _ => energyMeter).VerifyMeasurement(value);

            Assert.Multiple(() => {

                Assert.That(record.Split(';')[5].FromBASE32()[0],  Is.EqualTo(0x30),  "the case this is about");

                Assert.That(verdict.Status,                        Is.EqualTo(VerificationResult.ValidSignature),
                            String.Join("; ", verdict.Errors.Select(problem => problem.ToString())));

            });

        }

        #endregion


    }

}
