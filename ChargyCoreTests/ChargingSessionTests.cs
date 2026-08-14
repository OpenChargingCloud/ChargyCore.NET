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

#endregion

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Tests for the full charging session and charge transparency record,
    /// including the billing details an EV driver checks a bill against.
    /// </summary>
    [TestFixture]
    public class ChargingSessionTests : AChargyTests
    {

        #region A_fully_populated_charging_session_round_trips_through_JSON()

        [Test]
        public void A_fully_populated_charging_session_round_trips_through_JSON()
        {

            var json = JObject.Parse("""
                {
                    "@id":                "session-1",
                    "begin":              "2019-04-05T14:00:00.000Z",
                    "end":                "2019-04-05T16:00:00.000Z",
                    "EVSEId":             "DE*GEF*EVSE*CHARGY*1",
                    "meterId":            "METER-1",
                    "tariffId":           "DE*GEF*TARIFF*1",
                    "product":            { "@id": "AC-Charging" },
                    "chargingProductRelevance": { "energy": "Important", "parking": "Informative" },
                    "authorizationStart": { "@id": "RFID-1234", "type": "RFID", "timestamp": "2019-04-05T14:00:00.000Z" },
                    "authorizationStop":  { "@id": "RFID-1234", "type": "RFID" },
                    "totalCosts":         { "total": 12.34, "currency": "EUR", "energy": { "amount": 42.5, "unit": "kWh", "cost": 12.32 } },
                    "chargingPeriods":    [{
                        "startTimestamp":   "2019-04-05T14:00:00.000Z",
                        "chargingTariffId": "DE*GEF*TARIFF*1",
                        "costs":            { "total": 12.34, "currency": "EUR" }
                    }],
                    "parking":            [{ "@id": "parking-1", "begin": "2019-04-05T16:00:00.000Z", "overstay": true }],
                    "transparencyInfos":  { "chargingSessionURL": "https://chargy.charging.cloud/s/1" },
                    "legallyRelevantLogMessages": [
                        { "timestamp": "2019-04-05T15:00:00.000Z", "code": "ClockAdjusted" }
                    ],
                    "measurements":       [{
                        "energyMeterId": "METER-1",
                        "name":          "ENERGY_TOTAL",
                        "obis":          "1-0:1.8.0*255",
                        "scale":         0,
                        "values":        [{ "timestamp": "2019-04-05T14:54:50.000Z", "value": 22675 }]
                    }]
                }
                """);

            Assert.That(ChargingSession.TryParse(json, out var sessionOne),  Is.True);
            Assert.That(ChargingSession.TryParse(sessionOne!.ToJSON(), out var session),  Is.True);

            Assert.Multiple(() => {

                Assert.That(session!.Id,                                    Is.EqualTo("session-1"));
                Assert.That(session. EVSEId,                                Is.EqualTo("DE*GEF*EVSE*CHARGY*1"));
                Assert.That(session. TariffId,                              Is.EqualTo("DE*GEF*TARIFF*1"));
                Assert.That(session. Product?.Id,                           Is.EqualTo("AC-Charging"));

                Assert.That(session. ChargingProductRelevance?.Energy,      Is.EqualTo(InformationRelevance.Important));
                Assert.That(session. ChargingProductRelevance?.Parking,     Is.EqualTo(InformationRelevance.Informative));

                Assert.That(session. AuthorizationStart?.Id,                Is.EqualTo("RFID-1234"));
                Assert.That(session. AuthorizationStop?. Type,              Is.EqualTo("RFID"));

                Assert.That(session. TotalCosts?.Total,                     Is.EqualTo(12.34m));
                Assert.That(session. TotalCosts?.Energy?.Amount,            Is.EqualTo(42.5m));

                Assert.That(session. ChargingPeriods,                       Has.Count.EqualTo(1));
                Assert.That(session. ChargingPeriods[0].ChargingTariffId,   Is.EqualTo("DE*GEF*TARIFF*1"));

                Assert.That(session. Parking,                               Has.Count.EqualTo(1));
                Assert.That(session. Parking[0].Overstay,                   Is.True);

                Assert.That(session. TransparencyInfos?.ChargingSessionURL, Is.EqualTo("https://chargy.charging.cloud/s/1"));

                Assert.That(session. LegallyRelevantLogMessages,            Has.Count.EqualTo(1));
                Assert.That(session. LegallyRelevantLogMessages[0].Code,    Is.EqualTo("ClockAdjusted"));

                Assert.That(session. Measurements[0].Values[0].Value,       Is.EqualTo(22675m));

            });

        }

        #endregion

        #region The_back_reference_to_the_record_is_never_serialized()

        [Test]
        public void The_back_reference_to_the_record_is_never_serialized()
        {

            // A record contains its sessions and each session points back at the
            // record. ChargyCore.TS gets away with that object graph only because
            // CloneCTR() runs before the reference is set; here ToJSON() simply
            // never writes it.
            var record  = new ChargeTransparencyRecord("ctr-1", Begin: "2019-04-05T14:00:00.000Z");
            var session = new ChargingSession("session-1");

            record.AddChargingSession(session);
            session.CTR = record;

            var json = session.ToJSON();

            Assert.Multiple(() => {
                Assert.That(json["ctr"],                    Is.Null);
                Assert.That(record.ToJSON().ToString(),     Is.Not.Empty);
            });

        }

        #endregion

        #region Resolved_infrastructure_references_are_never_serialized()

        [Test]
        public void Resolved_infrastructure_references_are_never_serialized()
        {

            // These would duplicate entities the record already carries under its
            // charging station operators.
            var session = new ChargingSession("session-1", EVSEId: "DE*GEF*EVSE*1") {
                              EVSE             = new EVSE           ("DE*GEF*EVSE*1"),
                              ChargingStation  = new ChargingStation("DE*GEF*STATION*1"),
                              EnergyMeter      = new EnergyMeter    ("METER-1")
                          };

            var json = session.ToJSON();

            Assert.Multiple(() => {
                Assert.That((String?) json["EVSEId"],  Is.EqualTo("DE*GEF*EVSE*1"));
                Assert.That(json["EVSE"],              Is.Null);
                Assert.That(json["chargingStation"],   Is.Null);
                Assert.That(json["meter"],             Is.Null);
            });

        }

        #endregion


        #region A_charge_transparency_record_carries_its_whole_infrastructure()

        [Test]
        public void A_charge_transparency_record_carries_its_whole_infrastructure()
        {

            var json = JObject.Parse("""
                {
                    "@id":       "ctr-1",
                    "begin":     "2019-04-05T14:00:00.000Z",
                    "contracts": [{ "@id": "contract-1", "username": "achim" }],
                    "chargingStationOperators": [{
                        "@id":     "DE*GEF",
                        "contact": { "email": "mail@graphdefined.com" },
                        "support": { "email": "support@graphdefined.com" },
                        "privacy": { "contact": "Achim", "email": "dsgvo@graphdefined.com", "web": "https://graphdefined.com/dsgvo" }
                    }],
                    "eMobilityProviders": [{ "@id": "DE*GDF", "description": { "en": "GraphDefined Mobility" } }],
                    "mediationServices":  [{ "@id": "DE*MED", "description": { "en": "Schlichtungsstelle" } }],
                    "chargingTariffs":    [{ "@id": "DE*GEF*TARIFF*1", "currency": "EUR" }],
                    "chargingSessions":   [{ "@id": "session-1" }]
                }
                """);

            Assert.That(ChargeTransparencyRecord.TryParse(json, out var recordOne),  Is.True);
            Assert.That(ChargeTransparencyRecord.TryParse(recordOne!.ToJSON(), out var record),  Is.True);

            Assert.Multiple(() => {
                Assert.That(record!.Contracts[0].Username,                Is.EqualTo("achim"));
                Assert.That(record. ChargingStationOperators[0].Id,       Is.EqualTo("DE*GEF"));
                Assert.That(record. EMobilityProviders[0].Id,             Is.EqualTo("DE*GDF"));
                Assert.That(record. MediationServices[0].Id,              Is.EqualTo("DE*MED"));
                Assert.That(record. ChargingTariffs[0].Currency,          Is.EqualTo("EUR"));
                Assert.That(record. ChargingSessions[0].Id,               Is.EqualTo("session-1"));
            });

        }

        #endregion

        #region Files_that_were_not_understood_are_kept()

        [Test]
        public void Files_that_were_not_understood_are_kept()
        {

            // An application has to be able to tell an EV driver which of the files
            // they provided were not understood, instead of silently verifying only
            // some of them.
            var record = new ChargeTransparencyRecord("ctr-1");

            record.AddInvalidDataSet(
                new ExtendedFileInfo(
                    new FileInfo("holiday-photo.jpg", new Byte[] { 0xFF, 0xD8 }),
                    new SessionCryptoResult(SessionVerificationResult.UnknownCTRFormat)
                )
            );

            Assert.Multiple(() => {
                Assert.That(record.InvalidDataSets,                     Has.Count.EqualTo(1));
                Assert.That(record.InvalidDataSets[0].FileInfo.Name,    Is.EqualTo("holiday-photo.jpg"));
                Assert.That(record.InvalidDataSets[0].Result,           Is.TypeOf<SessionCryptoResult>());
            });

        }

        #endregion


    }

}
