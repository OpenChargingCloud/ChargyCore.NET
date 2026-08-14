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
    /// Tests for the charging infrastructure entities and the tariff data structures.
    /// </summary>
    [TestFixture]
    public class EntityTests : AChargyTests
    {

        #region An_entity_without_an_identification_is_rejected()

        [Test]
        public void An_entity_without_an_identification_is_rejected()
        {

            var empty = JObject.Parse("""{ }""");

            Assert.Multiple(() => {
                Assert.That(EVSE.                   TryParse(empty, out _),  Is.False);
                Assert.That(ChargingStation.        TryParse(empty, out _),  Is.False);
                Assert.That(ChargingPool.           TryParse(empty, out _),  Is.False);
                Assert.That(ChargingStationOperator.TryParse(empty, out _),  Is.False);
                Assert.That(EnergyMeter.            TryParse(empty, out _),  Is.False);
                Assert.That(ChargingTariff.         TryParse(empty, out _),  Is.False);
            });

        }

        #endregion

        #region An_operator_without_a_reachable_contact_is_rejected()

        [Test]
        public void An_operator_without_a_reachable_contact_is_rejected()
        {

            // An operator an EV driver cannot reach is of no use in a dispute,
            // which is why the CTR format makes these three mandatory.
            var withoutContacts = JObject.Parse("""{ "@id": "DE*GEF" }""");

            Assert.That(ChargingStationOperator.TryParse(withoutContacts, out _),  Is.False);

        }

        #endregion

        #region A_charging_station_operator_round_trips_through_JSON()

        [Test]
        public void A_charging_station_operator_round_trips_through_JSON()
        {

            var json = JObject.Parse("""
                {
                    "@id":          "DE*GEF",
                    "description":  { "en": "GraphDefined" },
                    "contact":      { "email": "mail@graphdefined.com", "web": "https://graphdefined.com" },
                    "support":      { "email": "support@graphdefined.com", "hotline": "+49 123 456" },
                    "privacy":      { "contact": "Achim", "email": "dsgvo@graphdefined.com", "web": "https://graphdefined.com/dsgvo" },
                    "geoLocation":  { "lat": 50.9, "lng": 11.6 },
                    "chargingPools": [{
                        "@id": "DE*GEF*POOL*1",
                        "chargingStations": [{
                            "@id": "DE*GEF*STATION*1",
                            "EVSEs": [{
                                "@id": "DE*GEF*EVSE*CHARGY*1",
                                "energyMeters": [{ "@id": "METER-1" }],
                                "connectors":   [{ "@id": "1", "type": "IEC_62196_T2", "cable": { "length": 5, "resistance": 0.01, "resistanceUnit": "Ohm" } }]
                            }]
                        }]
                    }]
                }
                """);

            Assert.That(ChargingStationOperator.TryParse(json, out var operatorOne),  Is.True);
            Assert.That(ChargingStationOperator.TryParse(operatorOne!.ToJSON(), out var operatorTwo),  Is.True);

            var evse = operatorTwo!.ChargingPools[0].ChargingStations[0].EVSEs[0];

            Assert.Multiple(() => {

                Assert.That(operatorTwo.Id,                                Is.EqualTo("DE*GEF"));
                Assert.That(operatorTwo.Contact.EMail,                     Is.EqualTo("mail@graphdefined.com"));
                Assert.That(operatorTwo.Support.Hotline,                   Is.EqualTo("+49 123 456"));
                Assert.That(operatorTwo.Privacy.Contact,                   Is.EqualTo("Achim"));
                Assert.That(operatorTwo.GeoLocation?.Latitude. Value,      Is.EqualTo(50.9));
                Assert.That(operatorTwo.GeoLocation?.Longitude.Value,      Is.EqualTo(11.6));

                Assert.That(evse.Id,                                       Is.EqualTo("DE*GEF*EVSE*CHARGY*1"));
                Assert.That(evse.EnergyMeters[0].Id,                       Is.EqualTo("METER-1"));
                Assert.That(evse.Connectors[0].Type,                       Is.EqualTo("IEC_62196_T2"));
                Assert.That(evse.Connectors[0].Cable?.Resistance,          Is.EqualTo(0.01m));
                Assert.That(evse.Connectors[0].Cable?.ResistanceUnit,      Is.EqualTo("Ohm"));

            });

        }

        #endregion

        #region Resolved_back_references_are_never_serialized()

        [Test]
        public void Resolved_back_references_are_never_serialized()
        {

            // A charging station contains its EVSEs, and each EVSE points back at
            // the station. Serializing that reference would not terminate, which
            // is why only the identification is written out.
            var station = new ChargingStation("DE*GEF*STATION*1", EVSEs: [ new EVSE("DE*GEF*EVSE*1") ]);
            var evse    = station.EVSEs[0];

            evse.ChargingStation    = station;
            evse.ChargingStationId  = station.Id;

            var json = evse.ToJSON();

            Assert.Multiple(() => {

                Assert.That((String?) json["chargingStationId"],  Is.EqualTo("DE*GEF*STATION*1"));
                Assert.That(json["chargingStation"],              Is.Null);

                // The whole station still serializes without running away.
                Assert.That(station.ToJSON().ToString(),          Is.Not.Empty);

            });

        }

        #endregion


        #region A_charging_tariff_round_trips_through_JSON()

        [Test]
        public void A_charging_tariff_round_trips_through_JSON()
        {

            var json = JObject.Parse("""
                {
                    "@id":           "DE*GEF*TARIFF*1",
                    "country_code":  "DE",
                    "party_id":      "GEF",
                    "currency":      "EUR",
                    "taxes":         [{ "@id": "VAT", "percentage": 19 }],
                    "elements":      [{
                        "price_components": [
                            { "type": "ENERGY", "price": 0.29, "step_size": 1 },
                            { "type": "TIME",   "price": 2.00, "step_size": 60 }
                        ],
                        "restrictions": { "start_time": "08:00", "end_time": "18:00", "day_of_week": [1, 2, 3, 4, 5] }
                    }]
                }
                """);

            Assert.That(ChargingTariff.TryParse(json, out var tariffOne),  Is.True);
            Assert.That(ChargingTariff.TryParse(tariffOne!.ToJSON(), out var tariffTwo),  Is.True);

            var element = tariffTwo!.Elements[0];

            Assert.Multiple(() => {

                Assert.That(tariffTwo.Id,                             Is.EqualTo("DE*GEF*TARIFF*1"));
                Assert.That(tariffTwo.Currency,                       Is.EqualTo("EUR"));
                Assert.That(tariffTwo.Taxes[0].Percentage,            Is.EqualTo(19m));

                Assert.That(element.PriceComponents,                  Has.Count.EqualTo(2));
                Assert.That(element.PriceComponents[0].Price,         Is.EqualTo(0.29m));
                Assert.That(element.PriceComponents[1].StepSize,      Is.EqualTo(60));

                Assert.That(element.Restrictions?.StartTime,          Is.EqualTo("08:00"));
                Assert.That(element.Restrictions?.DayOfWeek,          Is.EqualTo(new[] {
                                                                          DayOfWeek.Monday,
                                                                          DayOfWeek.Tuesday,
                                                                          DayOfWeek.Wednesday,
                                                                          DayOfWeek.Thursday,
                                                                          DayOfWeek.Friday
                                                                      }));

            });

        }

        #endregion

        #region Tariff_restriction_days_are_read_as_numbers_or_names()

        [Test]
        public void Tariff_restriction_days_are_read_as_numbers_or_names()
        {

            // OCPI numbers the days, but Chargy extensions have been seen naming them.
            var numeric = JObject.Parse("""{ "day_of_week": [0, 6] }""");
            var named   = JObject.Parse("""{ "day_of_week": ["Sunday", "saturday"] }""");

            Assert.Multiple(() => {

                Assert.That(TariffRestriction.TryParse(numeric, out var a),  Is.True);
                Assert.That(a!.DayOfWeek,  Is.EqualTo(new[] { DayOfWeek.Sunday, DayOfWeek.Saturday }));

                Assert.That(TariffRestriction.TryParse(named,   out var b),  Is.True);
                Assert.That(b!.DayOfWeek,  Is.EqualTo(new[] { DayOfWeek.Sunday, DayOfWeek.Saturday }));

            });

        }

        #endregion

        #region An_energy_meter_carries_its_legal_compliance()

        [Test]
        public void An_energy_meter_carries_its_legal_compliance()
        {

            // The calibration certificate is what makes a meter reading usable for
            // billing under the German Calibration Law.
            var json = JObject.Parse("""
                {
                    "@id":  "METER-1",
                    "manufacturer": { "name": "EMH" },
                    "firmware": { "version": "1.2.3", "checksum": "abcdef" },
                    "legalCompliance": {
                        "freeText":    "Eichrechtskonform",
                        "calibration": [{ "certificateId": "DE-19-M-PTB-0123", "notBefore": "2019-01-01", "notAfter": "2027-12-31", "freeText": "" }],
                        "conformity":  [{ "certificateId": "DE-M-AT-19-00001", "notBefore": "2019-01-01", "notAfter": "2027-12-31", "freeText": "" }]
                    },
                    "signatureInfos": { "hash": "SHA256", "algorithm": "ECC", "curve": "secp192r1", "format": "RS" }
                }
                """);

            Assert.That(EnergyMeter.TryParse(json, out var meterOne),  Is.True);
            Assert.That(EnergyMeter.TryParse(meterOne!.ToJSON(), out var meterTwo),  Is.True);

            Assert.Multiple(() => {

                Assert.That(meterTwo!.Id,                                        Is.EqualTo("METER-1"));
                Assert.That(meterTwo.Manufacturer?.Name,                         Is.EqualTo("EMH"));
                Assert.That(meterTwo.Firmware?.Checksum,                         Is.EqualTo("abcdef"));
                Assert.That(meterTwo.LegalCompliance?.Calibration[0].CertificateId,  Is.EqualTo("DE-19-M-PTB-0123"));
                Assert.That(meterTwo.LegalCompliance?.Conformity[0].CertificateId,   Is.EqualTo("DE-M-AT-19-00001"));
                Assert.That(meterTwo.SignatureInfos?.Curve,                      Is.EqualTo(ECCurve.secp192r1));
                Assert.That(meterTwo.SignatureInfos?.Format,                     Is.EqualTo(SignatureFormat.RS));

            });

        }

        #endregion


    }

}
