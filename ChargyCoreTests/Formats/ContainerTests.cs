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

using Newtonsoft.Json.Linq;

using cloud.charging.open.chargy.Formats.OCMF;
using cloud.charging.open.chargy.Formats.PTB;
using cloud.charging.open.chargy.Formats.XMLContainer;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the containers that carry somebody else's signed data.
    /// </summary>
    [TestFixture]
    public class ContainerTests : AChargyTests
    {

        #region KEBAWithinASAFEContainer()

        /// <summary>
        /// A KEBA charging station's readings inside a SAFE XML container.
        ///
        /// One charging session with 190 readings under a single signature, which
        /// is what makes it worth having: every other OCMF fixture holds a handful.
        /// </summary>
        [Test]
        public Task KEBAWithinASAFEContainer()

            => ExpectReport(
                   "KEBA/KEBA_container.xml",
                   "KEBA/KEBA_container.expected.txt"
               );

        #endregion


        #region PTBCarriesKnownGoodOCMFDataThrough()

        /// <summary>
        /// A PTB envelope around an OCMF record that is already known to verify.
        ///
        /// The fixture deliberately uses the same record for both the opening and
        /// the closing position, so that anything that fails here is the envelope's
        /// doing and not the record's.
        /// </summary>
        [Test]
        public async Task PTBCarriesKnownGoodOCMFDataThrough()
        {

            var container   = ChargyLib.ParseJSON(ReadTextFixture("PTBContainer/ptb-ocmf-testdata-01.json"));
            var sourceOCMF  = ReadTextFixture("OCMF/OCMF-Testdata-01.ocmf");
            var sourceKey   = ReadTextFixture("OCMF/OCMF-Testdata-01_publicKey.txt");

            Assert.Multiple(() => {

                Assert.That(container["ocmfBegin"]?.Value<String>(),  Is.EqualTo(sourceOCMF));
                Assert.That(container["ocmfEnd"]?.  Value<String>(),  Is.EqualTo(sourceOCMF));

                // PTB files the key as base64 where OCMF files it as hexadecimal —
                // the same key, spelled for a different transport.
                Assert.That(container["publicKey"]?.Value<String>(),
                            Is.EqualTo(Convert.ToBase64String(Convert.FromHexString(sourceKey))));

            });

            var result = await VerifyFixtures([ "PTBContainer/ptb-ocmf-testdata-01.json" ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var record           = (ChargeTransparencyRecord) result;
            var chargeboxId      = container["chargeboxIdentifier"]!.Value<String>();
            var chargingStation  = record.ChargingStations[0];
            var chargingSession  = record.ChargingSessions[0];
            var measurement      = chargingSession.Measurements[0];

            Assert.Multiple(() => {

                #region The place, which is the only thing the envelope adds

                Assert.That(chargingStation.Id,                       Is.EqualTo(chargeboxId));
                Assert.That(chargingStation.Address?.City,            Is.EqualTo("Berlin"));
                Assert.That(chargingStation.Address?.Street,          Is.EqualTo("Teststrasse 1"));
                Assert.That(chargingStation.Address?.PostalCode,      Is.EqualTo("10115"));
                Assert.That(chargingStation.Address?.Country,         Is.EqualTo("DE"));
                Assert.That(chargingStation.GeoLocation?.Latitude. Value,  Is.EqualTo(52.5).Within(0.0001));
                Assert.That(chargingStation.GeoLocation?.Longitude.Value,  Is.EqualTo(13.4).Within(0.0001));
                Assert.That(chargingStation.EVSEs[0].Id,              Is.EqualTo(chargeboxId));

                #endregion

                Assert.That(chargingSession.EVSEId,                   Is.EqualTo(chargeboxId));
                Assert.That(measurement.EnergyMeterId,                Is.EqualTo("******240084S"));
                Assert.That(measurement.Unit,                         Is.EqualTo("kWh"));
                Assert.That(measurement.Values,                       Has.Count.EqualTo(2));

                Assert.That(measurement.Values.Select(value => value.Result?.Status),
                            Is.EqualTo(new[] {
                                VerificationResult.ValidSignature,
                                VerificationResult.ValidSignature
                            }));

                Assert.That(measurement.Values.OfType<OCMFMeasurementValue>().
                                               Select(value => value.Document.Raw),
                            Is.EqualTo(new[] { sourceOCMF, sourceOCMF }));

            });

        }

        #endregion

        #region PTBReportsEveryContainerViolationAtOnce()

        /// <summary>
        /// A container that gets eight separate things wrong.
        ///
        /// All eight are reported. A schema that gave up after the first would make
        /// fixing such a file a matter of eight round trips — and, worse, would
        /// hide that the envelope is wrong about the *place* while the reader was
        /// still busy with its version number.
        /// </summary>
        [Test]
        public void PTBReportsEveryContainerViolationAtOnce()
        {

            var container = ChargyLib.ParseJSON(ReadTextFixture("PTBContainer/ptb-simple.json"));

            container["formatVersion"]  = "2.0";
            container["publicKey"]      = "not base64!";
            container["ocmfBegin"]      = "changed";
            container["address"]        = new JObject(
                                              new JProperty("street", ""),
                                              new JProperty("town",   "")
                                          );
            container["geoLocation"]    = new JObject(
                                              new JProperty("lat",      91),
                                              new JProperty("lng",      "13.4"),
                                              new JProperty("altitude", 34)
                                          );

            var result = new PTBContainer(
                             I18NDictionary.Default(),
                             new OCMFFormat(I18NDictionary.Default())
                         ).TryParseJSON(container);

            Assert.That(result, Is.InstanceOf<PTBValidationResult>());

            var validationResult = (PTBValidationResult) result;

            Assert.Multiple(() => {

                Assert.That(validationResult.Status,  Is.EqualTo(SessionVerificationResult.InvalidSessionFormat));

                Assert.That(validationResult.Issues.Select(issue => issue.Path),
                            Is.SupersetOf(new[] {
                                "$.formatVersion",
                                "$.publicKey",
                                "$.address.street",
                                "$.address",
                                "$.geoLocation.lat",
                                "$.geoLocation.lng",
                                "$.geoLocation.altitude",
                                "$.ocmfBegin"
                            }));

            });

        }

        #endregion


        #region PTBAcceptsTheEnvelopeAndOCMFRejectsTheDemoDocuments(Fixture)

        /// <summary>
        /// The two "ptb-simple" fixtures, whose OCMF documents are hand-made demo
        /// data that OCMF itself does not accept.
        ///
        /// The envelope passes validation — the rejection comes from OCMF, one
        /// layer further in, and says so. This is why the three ChargyCore.TS tests
        /// that use these fixtures are `test.skip`, and it is worth pinning rather
        /// than leaving unsaid: the fixtures are checked in, look usable, and are
        /// not.
        ///
        /// Note that both fixtures also carry a "*.expected.txt" golden report
        /// upstream, written in a report format ("format: ptb", "energyDifferenceWh")
        /// that no code in either implementation produces or reads. Those two files
        /// are leftovers from an earlier tool, which is why nothing here compares
        /// against them.
        /// </summary>
        /// <param name="Fixture">A fixture path relative to "TestData".</param>
        [TestCase("PTBContainer/ptb-simple.json")]
        [TestCase("PTBContainer/ptb-simple-signature_invalid.json")]
        public async Task PTBAcceptsTheEnvelopeAndOCMFRejectsTheDemoDocuments(String Fixture)
        {

            var result = await VerifyFixtures([ Fixture ]);

            Assert.That(result, Is.InstanceOf<SessionCryptoResult>());

            var sessionResult = (SessionCryptoResult) result;

            Assert.Multiple(() => {

                Assert.That(sessionResult.Status,  Is.EqualTo(SessionVerificationResult.InvalidSessionFormat));

                // Had the envelope been at fault, this would say "Invalid PTB OCMF
                // container!" instead — so the message is what tells the two layers
                // apart.
                Assert.That(result,                Is.Not.InstanceOf<PTBValidationResult>());

            });

        }

        #endregion


        #region TheXMLContainerRejectsInconsistentValues(Fixture, MessageFragment)

        /// <summary>
        /// A generic XML container whose signed values disagree with each other.
        ///
        /// One public key throughout, one signature method, one encoding: a
        /// container that mixes them is a file assembled out of several charging
        /// sessions, and saying which of the three went wrong is the useful part.
        /// </summary>
        /// <param name="XML">A generic XML container.</param>
        /// <param name="MessageFragment">What the rejection has to say.</param>
        [TestCase("<signedMeterValues>" +
                  "<signedMeterValue><publicKey>AAAA</publicKey><encodedMeterValue>one</encodedMeterValue></signedMeterValue>" +
                  "<signedMeterValue><publicKey>BBBB</publicKey><encodedMeterValue>two</encodedMeterValue></signedMeterValue>" +
                  "</signedMeterValues>",
                  "different public keys")]
        [TestCase("<signedMeterValues>" +
                  "<signedMeterValue><signatureMethod>ECDSA</signatureMethod><encodedMeterValue>one</encodedMeterValue></signedMeterValue>" +
                  "<signedMeterValue><signatureMethod>EdDSA</signatureMethod><encodedMeterValue>two</encodedMeterValue></signedMeterValue>" +
                  "</signedMeterValues>",
                  "different signature methods")]
        [TestCase("<signedMeterValues>" +
                  "<signedMeterValue><encodingMethod>ocmf</encodingMethod><encodedMeterValue>one</encodedMeterValue></signedMeterValue>" +
                  "<signedMeterValue><encodingMethod>alfen</encodingMethod><encodedMeterValue>two</encodedMeterValue></signedMeterValue>" +
                  "</signedMeterValues>",
                  "different signed data formats")]
        [TestCase("<signedMeterValues><signedMeterValue><signatureMethod>ECDSA</signatureMethod></signedMeterValue></signedMeterValues>",
                  "signed data tag")]
        public void TheXMLContainerRejectsInconsistentValues(String  XML,
                                                             String  MessageFragment)
        {

            var result = new XMLContainerFormat(I18NDictionary.Default()).
                             TryParseXML(XDocument.Parse($"<container>{XML}</container>"));

            Assert.That(result, Is.InstanceOf<SessionCryptoResult>());

            var sessionResult = (SessionCryptoResult) result;

            Assert.Multiple(() => {

                Assert.That(sessionResult.Status,           Is.EqualTo(SessionVerificationResult.InvalidSessionFormat));
                Assert.That(sessionResult.Message?.ToString(), Does.Contain(MessageFragment));

            });

        }

        #endregion

        #region AWellFormedXMLContainerStillCannotBeRead()

        /// <summary>
        /// A container that holds together perfectly and still yields nothing.
        ///
        /// The format does not say what its signed values are, and ChargyCore.TS
        /// leaves that conversion as a ToDo — so this port stops at the same point
        /// rather than inventing a reading the reference implementation does not
        /// make. This test pins that: the day upstream finishes the conversion, it
        /// fails and says so.
        /// </summary>
        [Test]
        public void AWellFormedXMLContainerStillCannotBeRead()
        {

            var result = new XMLContainerFormat(I18NDictionary.Default()).
                             TryParseXML(
                                 XDocument.Parse(
                                     "<container><signedMeterValues>" +
                                     "<signedMeterValue><publicKey>AAAA</publicKey><encodingMethod>ocmf</encodingMethod><encodedMeterValue>one</encodedMeterValue></signedMeterValue>" +
                                     "<signedMeterValue><publicKey>AAAA</publicKey><encodingMethod>ocmf</encodingMethod><encodedMeterValue>two</encodedMeterValue></signedMeterValue>" +
                                     "</signedMeterValues></container>"
                                 )
                             );

            Assert.That(result, Is.InstanceOf<SessionCryptoResult>());

            Assert.That(
                ((SessionCryptoResult) result).Status,
                Is.EqualTo(SessionVerificationResult.InvalidSessionFormat)
            );

        }

        #endregion

    }

}
