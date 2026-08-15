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

using Newtonsoft.Json.Linq;

using cloud.charging.open.chargy.Formats;
using cloud.charging.open.chargy.Formats.Alfen;
using cloud.charging.open.chargy.Formats.BSM;
using cloud.charging.open.chargy.Formats.ChargeIT;
using cloud.charging.open.chargy.Formats.OCMF;
using cloud.charging.open.chargy.Formats.OCPI;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the OCPI charge transparency container.
    /// </summary>
    [TestFixture]
    public class OCPITests : AChargyTests
    {

        #region Data

        /// <summary>
        /// An OCPI envelope around one signed OCMF value, describing the place and
        /// the meter as the roaming protocol knows them.
        /// </summary>
        private const String Container = """
            {
              "placeInfo": {
                "evseId": "DE*GEF*EVSE*CI*TESTS*2*B*1",
                "geoLocation": { "lat": 50.387945, "lon": 10.4304 },
                "address": { "street": "Biberweg 18", "zipCode": "53111", "town": "Bonn" }
              },
              "meterInfo": {
                "manufacturerURL": "https://www.phoenixcontact.com",
                "hardwareVersion": "r1.0"
              },
              "encoding_method": "OCMF",
              "public_key": "3056301006072A8648CE3D020106052B8104000A034200044E4970098EEFF5E0E286E3A38552679771B89315A49DDDF66EBAC6F176FB02DF9841091010E6850510540DAD0CF967FD8DE0AB25198282B39597DDCE09EDF459",
              "signed_values": [
                {
                  "signed_data": "OCMF|{\"FV\":\"0.1\",\"VI\":\"ABL\",\"VV\":\"1.4p3\",\"PG\":\"T12345\",\"MV\":\"Phoenix Contact\",\"MM\":\"EEM-350-D-MCB\",\"MS\":\"BQ27400330016\",\"MF\":\"1.0\",\"IS\":\"VERIFIED\",\"IF\":[\"RFID_PLAIN\",\"OCPP_RS_TLS\"],\"IT\":\"ISO14443\",\"ID\":\"1F2D3A4F5506C7\",\"RD\":[{\"TM\":\"2018-07-24T13:22:04,000+0200 S\",\"TX\":\"B\",\"RV\":2935.6,\"RI\":\"1-b:1.8.e\",\"RU\":\"kWh\",\"EI\":567,\"ST\":\"G\"}]}|{\"SA\":\"ECDSA-secp256k1-SHA256\",\"SD\":\"3046022100A7F1FD39278A88432E1AB81229C34CE1066885D0EAD8810DB900018A4960888302210089004420623749BF75561F29685CD87D6853EC08E83BD1A15C5DAFF9F03F4115\"}"
                }
              ]
            }
            """;

        #endregion


        #region TheContainerFillsTheGapsTheSignedPayloadLeaves()

        /// <summary>
        /// An OCPI envelope around signed OCMF data.
        ///
        /// Two sources describe the same meter here and they are not equal: what
        /// the meter signed about itself wins, and the container may only fill the
        /// gaps. OCMF has no field for a manufacturer's web address or a hardware
        /// revision, so those can only come from the container — which is also the
        /// clearest evidence that the merge happened at all.
        /// </summary>
        [Test]
        public void TheContainerFillsTheGapsTheSignedPayloadLeaves()
        {

            var result = ParseContainer(Container);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var record           = (ChargeTransparencyRecord) result;
            var chargingStation  = record.ChargingStations[0];
            var chargingSession  = record.ChargingSessions[0];
            var energyMeter      = chargingStation.EVSEs[0].EnergyMeters[0];

            Assert.Multiple(() => {

                #region The place, which the signed payload never states

                Assert.That(chargingStation.GeoLocation?.Latitude. Value,  Is.EqualTo(50.387945).Within(0.000001));
                Assert.That(chargingStation.GeoLocation?.Longitude.Value,  Is.EqualTo(10.4304).  Within(0.000001));
                Assert.That(chargingStation.Address?.Street,               Is.EqualTo("Biberweg 18"));
                Assert.That(chargingStation.Address?.PostalCode,           Is.EqualTo("53111"));
                Assert.That(chargingStation.Address?.City,                 Is.EqualTo("Bonn"));

                // Comes from the container's placeInfo, not from the signed payload.
                Assert.That(chargingSession.EVSEId,                        Is.EqualTo("DE*GEF*EVSE*CI*TESTS*2*B*1"));
                Assert.That(chargingStation.EVSEs[0].Id,                   Is.EqualTo("DE*GEF*EVSE*CI*TESTS*2*B*1"));

                #endregion

                #region The meter, where the signed payload wins wherever the two overlap

                Assert.That(energyMeter.Id,                        Is.EqualTo("BQ27400330016"));
                Assert.That(energyMeter.Manufacturer?.Name,        Is.EqualTo("Phoenix Contact"));
                Assert.That(energyMeter.Model?.Name,               Is.EqualTo("EEM-350-D-MCB"));
                Assert.That(energyMeter.Firmware?.Version,         Is.EqualTo("1.0"));

                // ..., and the container fills what OCMF has no field for.
                Assert.That(energyMeter.Hardware?.Revision,        Is.EqualTo("r1.0"));
                Assert.That(energyMeter.Manufacturer?.Contact?.Web, Is.EqualTo("https://www.phoenixcontact.com"));

                #endregion

            });

        }

        #endregion

        #region TheContainerNeverOverridesTheSignedMeter()

        /// <summary>
        /// A container that disagrees with the signed payload about the meter.
        ///
        /// It loses, on every field the payload states. A roaming platform's idea
        /// of which meter is installed is a claim by the platform; the payload is a
        /// claim by the meter, and only one of the two is signed.
        /// </summary>
        [Test]
        public void TheContainerNeverOverridesTheSignedMeter()
        {

            var container = ChargyLib.ParseJSON(Container);

            container["meterInfo"] = new JObject(
                                         new JProperty("meterId",         "SOMETHING-ELSE"),
                                         new JProperty("manufacturer",    "Not Phoenix Contact"),
                                         new JProperty("model",           "Not an EEM-350"),
                                         new JProperty("firmwareVersion", "9.9"),
                                         new JProperty("hardwareVersion", "r1.0")
                                     );

            var result = ParseContainer(container);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var energyMeter = ((ChargeTransparencyRecord) result).ChargingStations[0].EVSEs[0].EnergyMeters[0];

            Assert.Multiple(() => {

                Assert.That(energyMeter.Id,                  Is.EqualTo("BQ27400330016"));
                Assert.That(energyMeter.Manufacturer?.Name,  Is.EqualTo("Phoenix Contact"));
                Assert.That(energyMeter.Model?.Name,         Is.EqualTo("EEM-350-D-MCB"));
                Assert.That(energyMeter.Firmware?.Version,   Is.EqualTo("1.0"));

                // The one thing OCMF does not state is still the container's to say.
                Assert.That(energyMeter.Hardware?.Revision,  Is.EqualTo("r1.0"));

            });

        }

        #endregion

        #region MissingEnvelopeFieldsAreAllReported()

        /// <summary>
        /// An envelope missing all three of the fields that make it one.
        /// </summary>
        [Test]
        public void MissingEnvelopeFieldsAreAllReported()
        {

            var result = ParseContainer(new JObject());

            Assert.That(result, Is.InstanceOf<SessionCryptoResult>());

            var sessionResult = (SessionCryptoResult) result;

            Assert.Multiple(() => {
                Assert.That(sessionResult.Status,  Is.EqualTo(SessionVerificationResult.InvalidSessionFormat));
                Assert.That(sessionResult.Errors,  Has.Count.EqualTo(3));
            });

        }

        #endregion

        #region TheNewerOCPIContainerIsTheChargeITContainer()

        /// <summary>
        /// A container declaring itself as "ocpi-2.1" is, field for field, the
        /// newer chargeIT container — so it is read by that reader rather than by a
        /// second copy of it.
        ///
        /// The test proves the hand-off happened at all: without it the context
        /// would fall through to "not a charge transparency record".
        /// </summary>
        [Test]
        public void TheNewerOCPIContainerIsTheChargeITContainer()
        {

            var container = ChargyLib.ParseJSON(
                                ReadTextFixture("chargeIT/new_container_format/bsm-ws36a-good-new-style-header.json")
                            );

            container["@context"] = OCPIFormat.ContainerContext;

            var result = ParseContainer(container);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var record = new ChargeTransparencyRecordProcessor(I18NDictionary.Default()).
                             Process((ChargeTransparencyRecord) result);

            Assert.That(
                record.ChargingSessions[0].VerificationResult?.Status,
                Is.EqualTo(SessionVerificationResult.ValidSignature)
            );

        }

        #endregion


        #region (private, static) ParseContainer(Container)

        /// <summary>
        /// Read an OCPI container with everything it may need to hand on to.
        /// </summary>
        private static Object ParseContainer(String Container)

            => ParseContainer(ChargyLib.ParseJSON(Container));

        /// <summary>
        /// Read an OCPI container with everything it may need to hand on to.
        /// </summary>
        private static Object ParseContainer(JObject Container)
        {

            var i18n = I18NDictionary.Default();

            return new OCPIFormat(
                       i18n,
                       new OCMFFormat(i18n),
                       new ChargeITContainer(
                           i18n,
                           new AlfenFormat(i18n),
                           new BSMFormat  (i18n)
                       )
                   ).TryParseJSON(Container);

        }

        #endregion

    }

}
