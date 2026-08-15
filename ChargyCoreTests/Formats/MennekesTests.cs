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

using System.Xml.Linq;

using cloud.charging.open.chargy.Formats.Mennekes;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the Mennekes EDL40 XML format: the parser, the 320 byte signature
    /// block, and the charge transparency records built from them.
    /// </summary>
    [TestFixture]
    public class MennekesTests : AChargyTests
    {

        #region ParsesAStandaloneChargingProcess()

        /// <summary>
        /// A bare "ChargingProcess" element, without an XML namespace.
        ///
        /// Checked against the values the document plainly contains before any
        /// signature is involved: a signature failure cannot tell you which of the
        /// two halves went wrong.
        /// </summary>
        [Test]
        public void ParsesAStandaloneChargingProcess()
        {

            var chargingProcesses = MennekesChargingProcess.ExtractFrom(
                                        ReadXMLFixture("Mennekes/test1.xml")
                                    ).ToArray();

            Assert.That(chargingProcesses, Has.Length.EqualTo(1));

            var chargingProcess = chargingProcesses[0];

            Assert.Multiple(() => {

                Assert.That(chargingProcess.MeterId,                 Is.EqualTo("0901454D4800005BAE2F"));
                Assert.That(chargingProcess.PublicKey,               Is.EqualTo("6DACB9C5466A25B3EB9F6466B53457C84A27448B01A64A278C0A28DAC95F2B45DF39B79918A9A4D2E3551F3FE925D09D"));
                Assert.That(chargingProcess.MeteringPoint,           Is.EqualTo("DE*PWC*E00003*005"));
                Assert.That(chargingProcess.CustomerIdent,           Is.EqualTo("874AD0FE"));
                Assert.That(chargingProcess.TimestampCustomerIdent,  Is.EqualTo("2018-09-04T12:22:10+02:00"));

                Assert.That(chargingProcess.MeasurementStart.Timestamp,     Is.EqualTo("2018-09-04T12:22:14+02:00"));
                Assert.That(chargingProcess.MeasurementStart.EventCounter,  Is.EqualTo(8));
                Assert.That(chargingProcess.MeasurementStart.MeterStatus,   Is.EqualTo(65800));
                Assert.That(chargingProcess.MeasurementStart.Value,         Is.EqualTo(519116));
                Assert.That(chargingProcess.MeasurementStart.Scaler,        Is.EqualTo(-1));
                Assert.That(chargingProcess.MeasurementStart.Pagination,    Is.EqualTo(25));
                Assert.That(chargingProcess.MeasurementStart.SecondIndex,   Is.EqualTo(74650));

            });

        }

        #endregion

        #region ParsesABillingWrapperWithTheMennekesNamespace()

        /// <summary>
        /// A "Billing" wrapper that does declare the Mennekes namespace.
        ///
        /// Both shapes are read by local name, because whether a document declares
        /// its own namespace says nothing about what it contains.
        /// </summary>
        [Test]
        public void ParsesABillingWrapperWithTheMennekesNamespace()
        {

            var chargingProcesses = MennekesChargingProcess.ExtractFrom(
                                        ReadXMLFixture("Mennekes/test2.xml")
                                    ).ToArray();

            Assert.That(chargingProcesses, Has.Length.EqualTo(1));

            var end = chargingProcesses[0].MeasurementEnd;

            Assert.Multiple(() => {

                Assert.That(end.Timestamp,     Is.EqualTo("2018-09-04T12:26:45+02:00"));
                Assert.That(end.EventCounter,  Is.EqualTo(8));
                Assert.That(end.MeterStatus,   Is.EqualTo(65800));
                Assert.That(end.Value,         Is.EqualTo(520535));
                Assert.That(end.Scaler,        Is.EqualTo(-1));
                Assert.That(end.Pagination,    Is.EqualTo(26));
                Assert.That(end.SecondIndex,   Is.EqualTo(74921));

            });

        }

        #endregion

        #region ATimestampBecomesTheClockTheMeterDisplayed()

        /// <summary>
        /// A Mennekes meter signs the time it displays, not the UTC instant behind
        /// it, so the stated offset is added rather than applied.
        ///
        /// The difference is exactly the meter's summer time — two hours here — and
        /// getting it backwards invalidates every signature for half the year.
        /// </summary>
        [Test]
        public void ATimestampBecomesTheClockTheMeterDisplayed()

            => Assert.That(
                   MennekesChargingProcess.LocalEpochSeconds("2018-09-04T12:22:14+02:00"),
                   Is.EqualTo(1536063734)
               );

        #endregion

        #region BuildsThe320ByteBlockAtItsDocumentedOffsets()

        /// <summary>
        /// The signed block, field by field, at the offsets the format prescribes.
        ///
        /// This is checked against literal bytes rather than only through the
        /// signature, because a signature failure says only that something is
        /// wrong somewhere in 320 bytes.
        /// </summary>
        [Test]
        public void BuildsThe320ByteBlockAtItsDocumentedOffsets()
        {

            var chargingProcess = MennekesChargingProcess.ExtractFrom(
                                      ReadXMLFixture("Mennekes/test1.xml")
                                  ).First();

            var signedData = chargingProcess.BuildSignedData(chargingProcess.MeasurementStart);

            Assert.Multiple(() => {

                Assert.That(signedData,             Has.Length.EqualTo(320));

                Assert.That(signedData[ 0..10],     Is.EqualTo(Convert.FromHexString("0901454D4800005BAE2F")));   // the meter
                Assert.That(signedData[14],         Is.EqualTo(0x08));                                           // the status word, lowest byte
                Assert.That(signedData[15..19],     Is.EqualTo(new Byte[] { 0x9A, 0x23, 0x01, 0x00 }));           // the seconds index
                Assert.That(signedData[19..23],     Is.EqualTo(new Byte[] { 0x19, 0x00, 0x00, 0x00 }));           // the page number
                Assert.That(signedData[23..29],     Is.EqualTo(new Byte[] { 0x01, 0x00, 0x01, 0x11, 0x00, 0xFF })); // the OBIS code
                Assert.That(signedData[29],         Is.EqualTo(30));                                             // 1e => 30 => Wh
                Assert.That(signedData[30],         Is.EqualTo(0xFF));                                           // the scale, -1
                Assert.That(signedData[39..41],     Is.EqualTo(new Byte[] { 0x00, 0x08 }));                       // the event counter
                Assert.That(signedData[41..45],     Is.EqualTo(Convert.FromHexString("874AD0FE")));               // the token the driver used

                // Everything past the authorization timestamp stays zero, and is
                // signed all the same — which is why the block is 320 bytes long
                // rather than 173.
                Assert.That(signedData[173..],      Is.EqualTo(new Byte[147]));

            });

        }

        #endregion


        #region VerifiesAStandaloneMennekesDocument()

        /// <summary>
        /// A bare Mennekes document, all the way through the pipeline.
        /// </summary>
        [Test]
        public async Task VerifiesAStandaloneMennekesDocument()
        {

            var result = await VerifyFixtures([ "Mennekes/test1.xml" ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var record = (ChargeTransparencyRecord) result;

            Assert.That(record.ChargingSessions, Has.Count.EqualTo(1));

            var chargingSession = record.ChargingSessions[0];

            Assert.Multiple(() => {

                Assert.That(chargingSession.VerificationResult?.Status,
                            Is.EqualTo(SessionVerificationResult.ValidSignature));

                Assert.That(chargingSession.Measurements[0].VerificationResult?.Status,
                            Is.EqualTo(VerificationResult.ValidSignature));

                Assert.That(chargingSession.Measurements[0].Values.Select(value => value.Result?.Status),
                            Is.EqualTo(new[] {
                                VerificationResult.ValidSignature,
                                VerificationResult.ValidSignature
                            }));

            });

        }

        #endregion

        #region VerifiesABillingWrappedMennekesDocument()

        /// <summary>
        /// The same, wrapped in an invoice.
        /// </summary>
        [Test]
        public async Task VerifiesABillingWrappedMennekesDocument()
        {

            var result = await VerifyFixtures([ "Mennekes/test2.xml" ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            Assert.That(
                ((ChargeTransparencyRecord) result).ChargingSessions[0].VerificationResult?.Status,
                Is.EqualTo(SessionVerificationResult.ValidSignature)
            );

        }

        #endregion

        #region ATamperedMeterValueIsRejected()

        /// <summary>
        /// One digit changed in the meter reading.
        ///
        /// This is the case the whole format exists for, and the one an EV driver
        /// is entitled to have caught: everything else about the document still
        /// reads perfectly well, and only the signature says the number is not the
        /// one the meter reported.
        /// </summary>
        [Test]
        public async Task ATamperedMeterValueIsRejected()
        {

            var tampered = ReadTextFixture("Mennekes/test1.xml").
                               Replace("<Value>519116</Value>", "<Value>519117</Value>");

            var result   = await Verify([
                                     new FileInfo(
                                         "test1-tampered.xml",
                                         System.Text.Encoding.UTF8.GetBytes(tampered),
                                         "application/xml"
                                     )
                                 ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var chargingSession = ((ChargeTransparencyRecord) result).ChargingSessions[0];

            Assert.Multiple(() => {

                Assert.That(chargingSession.VerificationResult?.Status,
                            Is.EqualTo(SessionVerificationResult.InvalidSignature));

                Assert.That(chargingSession.Measurements[0].Values[0].Result?.Status,
                            Is.EqualTo(VerificationResult.InvalidSignature));

            });

        }

        #endregion


        #region (private, static) ReadXMLFixture(FixtureName)

        /// <summary>
        /// Read a test fixture as an XML document.
        /// </summary>
        /// <param name="FixtureName">A fixture path relative to "TestData".</param>
        private static XDocument ReadXMLFixture(String FixtureName)

            => XDocument.Parse(
                   ReadTextFixture(FixtureName),
                   LoadOptions.PreserveWhitespace
               );

        #endregion

    }

}
