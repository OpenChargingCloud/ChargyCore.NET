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
    /// Tests for the Charge Transparency Record data model, ported from
    /// data-structures.test.ts.
    /// </summary>
    [TestFixture]
    public class DataStructureTests : AChargyTests
    {

        #region A_measurement_value_keeps_the_timestamp_text_verbatim()

        [Test]
        public void A_measurement_value_keeps_the_timestamp_text_verbatim()
        {

            // The charge transparency data formats normalize their timestamps
            // differently — OCMF emits "+00:00" where Alfen emits "Z" — and the
            // verification reports print this string verbatim. Round-tripping it
            // through a DateTimeOffset would silently rewrite it.
            var ocmf  = new MeasurementValue("2019-06-26T08:57:44.337+00:00", 268.978m);
            var alfen = new MeasurementValue("2019-04-05T14:54:50.000Z",      22675m);

            Assert.Multiple(() => {

                Assert.That(ocmf. Timestamp,                          Is.EqualTo("2019-06-26T08:57:44.337+00:00"));
                Assert.That(alfen.Timestamp,                          Is.EqualTo("2019-04-05T14:54:50.000Z"));

                // ... while both still parse to the same kind of instant.
                Assert.That(ocmf. ParsedTimestamp.ToUnixTimeSeconds(),  Is.EqualTo(1561539464));
                Assert.That(alfen.ParsedTimestamp.ToUnixTimeSeconds(),  Is.EqualTo(1554476090));

            });

        }

        #endregion

        #region A_measurement_value_parses_a_numeric_or_a_textual_value()

        [Test]
        public void A_measurement_value_parses_a_numeric_or_a_textual_value()
        {

            var numeric = JObject.Parse("""{ "timestamp": "2019-04-05T14:54:50.000Z", "value": 22675 }""");
            var textual = JObject.Parse("""{ "timestamp": "2019-04-05T14:54:50.000Z", "value": "268.978" }""");

            Assert.Multiple(() => {

                Assert.That(MeasurementValue.TryParse(numeric, out var a),  Is.True);
                Assert.That(a!.Value,  Is.EqualTo(22675m));

                Assert.That(MeasurementValue.TryParse(textual, out var b),  Is.True);
                Assert.That(b!.Value,  Is.EqualTo(268.978m));

            });

        }

        #endregion

        #region A_measurement_value_without_a_timestamp_or_value_is_rejected()

        [Test]
        public void A_measurement_value_without_a_timestamp_or_value_is_rejected()
        {

            Assert.Multiple(() => {

                Assert.That(MeasurementValue.TryParse(JObject.Parse("""{ "value": 1 }"""), out _),
                            Is.False);

                Assert.That(MeasurementValue.TryParse(JObject.Parse("""{ "timestamp": "2019-04-05T14:54:50.000Z" }"""), out _),
                            Is.False);

            });

        }

        #endregion


        #region Adding_measurement_values_links_them_into_a_chain()

        [Test]
        public void Adding_measurement_values_links_them_into_a_chain()
        {

            var measurement = new Measurement("METER-1", "ENERGY_TOTAL", "1-0:1.8.0*255", 0);

            var first  = new MeasurementValue("2019-04-05T14:54:50.000Z", 22675m);
            var second = new MeasurementValue("2019-04-05T15:54:50.000Z", 23675m);

            measurement.AddValue(first);
            measurement.AddValue(second);

            Assert.Multiple(() => {

                // The hash chained data formats verify each value against its
                // predecessor, so the links have to exist before verification.
                Assert.That(first. PreviousValue,  Is.Null);
                Assert.That(second.PreviousValue,  Is.SameAs(first));

                Assert.That(first. Measurement,    Is.SameAs(measurement));
                Assert.That(second.Measurement,    Is.SameAs(measurement));

            });

        }

        #endregion

        #region A_charging_session_falls_back_to_the_meter_of_its_first_measurement()

        [Test]
        public void A_charging_session_falls_back_to_the_meter_of_its_first_measurement()
        {

            // Several charge transparency data formats name the energy meter only
            // per measurement, never at session level.
            var withoutMeterId = new ChargingSession("session-1");
            withoutMeterId.AddMeasurement(new Measurement("METER-1", "ENERGY_TOTAL", "1-0:1.8.0*255", 0));

            var withMeterId    = new ChargingSession("session-2", EnergyMeterId: "METER-2");
            withMeterId.AddMeasurement(new Measurement("METER-1", "ENERGY_TOTAL", "1-0:1.8.0*255", 0));

            Assert.Multiple(() => {
                Assert.That(withoutMeterId.MeterId,  Is.EqualTo("METER-1"));
                Assert.That(withMeterId.   MeterId,  Is.EqualTo("METER-2"));
                Assert.That(new ChargingSession("session-3").MeterId,  Is.Null);
            });

        }

        #endregion

        #region A_charge_transparency_record_round_trips_through_JSON()

        [Test]
        public void A_charge_transparency_record_round_trips_through_JSON()
        {

            var measurement = new Measurement("METER-1", "ENERGY_TOTAL", "1-0:1.8.0*255", 0, Unit: "kWh");
            measurement.AddValue(new MeasurementValue(
                                     "2019-04-05T14:54:50.000Z",
                                     22675m,
                                     [ new SignatureRS("00aa", "00bb") ]
                                 ));

            var session = new ChargingSession("session-1", EVSEId: "DE*GEF*EVSE*CHARGY*1");
            session.AddMeasurement(measurement);

            var original = new ChargeTransparencyRecord("ctr-1", Begin: "2019-04-05T14:00:00.000Z");
            original.AddChargingSession(session);

            Assert.That(ChargeTransparencyRecord.TryParse(original.ToJSON(), out var roundTrip),  Is.True);

            var roundTripSession     = roundTrip!.ChargingSessions[0];
            var roundTripMeasurement = roundTripSession.Measurements[0];
            var roundTripValue       = roundTripMeasurement.Values[0];

            Assert.Multiple(() => {

                Assert.That(roundTrip.Id,                      Is.EqualTo("ctr-1"));
                Assert.That(roundTrip.Begin,                   Is.EqualTo("2019-04-05T14:00:00.000Z"));
                Assert.That(roundTripSession.Id,               Is.EqualTo("session-1"));
                Assert.That(roundTripSession.EVSEId,           Is.EqualTo("DE*GEF*EVSE*CHARGY*1"));
                Assert.That(roundTripMeasurement.OBIS,         Is.EqualTo("1-0:1.8.0*255"));
                Assert.That(roundTripMeasurement.Unit,         Is.EqualTo("kWh"));
                Assert.That(roundTripValue.Timestamp,          Is.EqualTo("2019-04-05T14:54:50.000Z"));
                Assert.That(roundTripValue.Value,              Is.EqualTo(22675m));
                Assert.That(roundTripValue.Signatures,         Has.Count.EqualTo(1));

                // An r/s signature must survive as an r/s signature, or its two
                // halves would be lost on the way through JSON.
                Assert.That(roundTripValue.Signatures[0],      Is.TypeOf<SignatureRS>());
                Assert.That(((SignatureRS) roundTripValue.Signatures[0]).R,  Is.EqualTo("00aa"));
                Assert.That(((SignatureRS) roundTripValue.Signatures[0]).S,  Is.EqualTo("00bb"));

            });

        }

        #endregion

        #region A_charge_transparency_record_is_recognised_by_its_shape()

        [Test]
        public void A_charge_transparency_record_is_recognised_by_its_shape()
        {

            Assert.Multiple(() => {

                Assert.That(ChargeTransparencyRecord.IsAChargeTransparencyRecord(
                                JObject.Parse("""{ "begin": "2019-04-05T14:00:00Z", "chargingSessions": [] }""")),
                            Is.True);

                Assert.That(ChargeTransparencyRecord.IsAChargeTransparencyRecord(
                                JObject.Parse("""{ "begin": "2019-04-05T14:00:00Z" }""")),
                            Is.False);

                Assert.That(ChargeTransparencyRecord.IsAChargeTransparencyRecord(
                                JObject.Parse("""{ "chargingSessions": [] }""")),
                            Is.False);

            });

        }

        #endregion


        #region A_public_key_is_either_a_value_or_a_coordinate_pair()

        [Test]
        public void A_public_key_is_either_a_value_or_a_coordinate_pair()
        {

            Assert.Multiple(() => {

                Assert.That(PublicKey.TryParse(JObject.Parse("""{ "value": "04aabb" }"""), out var value),  Is.True);
                Assert.That(value!.IsXY,  Is.False);

                Assert.That(PublicKey.TryParse(JObject.Parse("""{ "x": "aa", "y": "bb" }"""), out var xy),  Is.True);
                Assert.That(xy!.IsXY,  Is.True);

                // Neither a value nor a complete coordinate pair.
                Assert.That(PublicKey.TryParse(JObject.Parse("""{ "x": "aa" }"""), out _),  Is.False);
                Assert.That(PublicKey.TryParse(JObject.Parse("""{ }"""),           out _),  Is.False);

            });

        }

        #endregion

        #region A_public_key_algorithm_is_a_name_or_an_object_identifier()

        [Test]
        public void A_public_key_algorithm_is_a_name_or_an_object_identifier()
        {

            var named = JObject.Parse("""{ "value": "04aa", "algorithm": "ECDSA-secp256r1-SHA256" }""");
            var oid   = JObject.Parse("""{ "value": "04aa", "algorithm": { "oid": "1.2.840.10045.4.3.2", "name": "ecdsa-with-SHA256" } }""");

            Assert.Multiple(() => {

                Assert.That(PublicKey.TryParse(named, out var a),  Is.True);
                Assert.That(a!.Algorithm?.Name,  Is.EqualTo("ECDSA-secp256r1-SHA256"));
                Assert.That(a. Algorithm?.OID,   Is.Null);

                Assert.That(PublicKey.TryParse(oid,   out var b),  Is.True);
                Assert.That(b!.Algorithm?.Name,  Is.EqualTo("ecdsa-with-SHA256"));
                Assert.That(b. Algorithm?.OID,   Is.EqualTo("1.2.840.10045.4.3.2"));

            });

        }

        #endregion

        #region A_public_key_subject_may_be_a_string_an_array_or_an_object()

        [Test]
        public void A_public_key_subject_may_be_a_string_an_array_or_an_object()
        {

            Assert.Multiple(() => {

                Assert.That(PublicKey.IsAPublicKeySubject(null),                                              Is.True);
                Assert.That(PublicKey.IsAPublicKeySubject(new JValue("DE*GEF*EVSE*1")),                       Is.True);
                Assert.That(PublicKey.IsAPublicKeySubject(new JArray("a", "b")),                              Is.True);
                Assert.That(PublicKey.IsAPublicKeySubject(JObject.Parse("""{ "evse": "DE*GEF*EVSE*1" }""")),  Is.True);
                Assert.That(PublicKey.IsAPublicKeySubject(JObject.Parse("""{ "evse": ["a", "b"] }""")),       Is.True);

                // A number is not an identification.
                Assert.That(PublicKey.IsAPublicKeySubject(new JValue(42)),                                    Is.False);
                Assert.That(PublicKey.IsAPublicKeySubject(JObject.Parse("""{ "evse": 42 }""")),               Is.False);

            });

        }

        #endregion

        #region An_empty_public_key_signature_is_rejected()

        [Test]
        public void An_empty_public_key_signature_is_rejected()
        {

            // Without this, every stray JSON object next to a public key would be
            // read as an empty certification.
            Assert.Multiple(() => {

                Assert.That(PublicKeySignature.TryParse(JObject.Parse("""{ }"""), out _),  Is.False);

                Assert.That(PublicKeySignature.TryParse(
                                JObject.Parse("""{ "signer": "DE*GEF", "value": "3045" }"""), out var signature),
                            Is.True);

                Assert.That(signature!.Signer,  Is.EqualTo("DE*GEF"));

            });

        }

        #endregion


        #region The_legacy_misspelling_secp512r1_is_read_as_secp521r1()

        [Test]
        public void The_legacy_misspelling_secp512r1_is_read_as_secp521r1()
        {

            // "secp512r1" is not a curve that exists. It was a typo in the
            // IECCurves enum of ChargyCore.TS, fixed there in the meantime, but
            // charge transparency records written before that carry the
            // misspelling. Accepted on input, never written back out.
            Assert.Multiple(() => {

                Assert.That(ECCurveExtensions.TryParse("secp512r1"),  Is.EqualTo(ECCurve.secp521r1));
                Assert.That(ECCurveExtensions.TryParse("secp521r1"),  Is.EqualTo(ECCurve.secp521r1));
                Assert.That(ECCurve.secp521r1.AsText(),               Is.EqualTo("secp521r1"));

            });

        }

        #endregion

        #region Signature_infos_round_trip_through_JSON()

        [Test]
        public void Signature_infos_round_trip_through_JSON()
        {

            var json = JObject.Parse("""
                {
                    "hash":      "SHA256",
                    "algorithm": "ECC",
                    "curve":     "secp256r1",
                    "format":    "DER",
                    "encoding":  "hex"
                }
                """);

            Assert.That(SignatureInfos.TryParse(json, out var infos),  Is.True);

            Assert.Multiple(() => {

                Assert.That(infos!.Hash,       Is.EqualTo(CryptoHashAlgorithm.SHA256));
                Assert.That(infos. Algorithm,  Is.EqualTo(CryptoAlgorithm.ECC));
                Assert.That(infos. Curve,      Is.EqualTo(ECCurve.secp256r1));
                Assert.That(infos. Format,     Is.EqualTo(SignatureFormat.DER));
                Assert.That(infos. Encoding,   Is.EqualTo(SignatureEncoding.Hex));

                Assert.That(SignatureInfos.TryParse(infos.ToJSON(), out var roundTrip),  Is.True);
                Assert.That(roundTrip!.ToString(),  Is.EqualTo(infos.ToString()));

            });

        }

        #endregion

        #region Both_SHA256_and_SHA_256_are_understood()

        [Test]
        public void Both_SHA256_and_SHA_256_are_understood()
        {

            Assert.Multiple(() => {
                Assert.That(CryptoHashAlgorithmExtensions.TryParse("SHA256"),   Is.EqualTo(CryptoHashAlgorithm.SHA256));
                Assert.That(CryptoHashAlgorithmExtensions.TryParse("SHA-256"),  Is.EqualTo(CryptoHashAlgorithm.SHA256));
                Assert.That(CryptoHashAlgorithmExtensions.TryParse("sha-512"),  Is.EqualTo(CryptoHashAlgorithm.SHA512));
                Assert.That(CryptoHashAlgorithmExtensions.TryParse("MD5"),      Is.Null);
            });

        }

        #endregion


    }

}
