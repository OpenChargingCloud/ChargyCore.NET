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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// The test cases of "data-structures.test.ts" and "chargyInterfaces.tests.ts",
    /// ported one by one.
    ///
    /// These are kept as a separate fixture from the C#-native model tests on
    /// purpose: they are the cases ChargyCore.TS asserts, and when a case is
    /// dropped or changed upstream it should be visible here rather than buried
    /// among tests written for this port.
    /// </summary>
    [TestFixture]
    public class PortedDataStructureTests : AChargyTests
    {

        #region Recognizes_a_charge_transparency_record_by_its_required_structural_fields()

        [Test]
        public void Recognizes_a_charge_transparency_record_by_its_required_structural_fields()
        {

            var ctr = JObject.Parse("""
                { "@id": "ctr-1", "begin": "2019-04-05T14:00:00.000Z", "chargingSessions": [] }
                """);

            var withoutSessions = JObject.Parse("""
                { "@id": "ctr-1", "begin": "2019-04-05T14:00:00.000Z" }
                """);

            Assert.Multiple(() => {
                Assert.That(ChargeTransparencyRecord.IsAChargeTransparencyRecord(ctr),              Is.True);
                Assert.That(ChargeTransparencyRecord.IsAChargeTransparencyRecord(withoutSessions),  Is.False);
            });

        }

        #endregion

        #region Recognizes_public_key_info_and_rejects_incomplete_keys()

        [Test]
        public void Recognizes_public_key_info_and_rejects_incomplete_keys()
        {

            var complete = JObject.Parse("""
                { "algorithm": "secp192r1", "value": "04aabb", "encoding": "hex" }
                """);

            var withoutValue = JObject.Parse("""
                { "algorithm": "secp192r1", "encoding": "hex" }
                """);

            Assert.Multiple(() => {
                Assert.That(PublicKey.TryParse(complete,     out _),  Is.True);
                Assert.That(PublicKey.TryParse(withoutValue, out _),  Is.False);
            });

        }

        #endregion

        #region Recognizes_public_key_lookup_containers()

        [Test]
        public void Recognizes_public_key_lookup_containers()
        {

            var lookup = JObject.Parse("""
                { "publicKeys": [ { "algorithm": "secp192r1", "value": "04aabb" } ] }
                """);

            Assert.Multiple(() => {

                Assert.That(PublicKeyLookup.TryParse(lookup, out var parsed),  Is.True);
                Assert.That(parsed!.PublicKeys,  Has.Count.EqualTo(1));

                // Without a "publicKeys" array it is not a lookup container.
                Assert.That(PublicKeyLookup.TryParse(JObject.Parse("""{ }"""), out _),  Is.False);

            });

        }

        #endregion

        #region Recognizes_session_and_measurement_crypto_results()

        [Test]
        public void Recognizes_session_and_measurement_crypto_results()
        {

            var valid   = new SessionCryptoResult(SessionVerificationResult.ValidSignature);
            var invalid = new SessionCryptoResult(SessionVerificationResult.InvalidSessionFormat);

            Assert.Multiple(() => {

                Assert.That(valid.  IsUsable,  Is.True);

                // "InvalidSessionFormat" means "this is not my format", not
                // "this record is broken", so it must not win over a parser that
                // did understand the file.
                Assert.That(invalid.IsUsable,  Is.False);

            });

        }

        #endregion

        #region Recognizes_OIDs()

        [Test]
        public void Recognizes_OIDs()
        {

            Assert.Multiple(() => {

                var complete = OIDInfo.TryParse(JObject.Parse("""{ "oid": "1.2.3.4", "name": "Example OID" }"""));

                Assert.That(complete,       Is.Not.Null);
                Assert.That(complete!.OID,  Is.EqualTo("1.2.3.4"));
                Assert.That(complete. Name, Is.EqualTo("Example OID"));

                // An object without an "oid" is not an OID info.
                Assert.That(OIDInfo.TryParse(JObject.Parse("""{ "name": "Missing OID" }""")),  Is.Null);

            });

        }

        #endregion

        #region Recognizes_XY_public_keys()

        [Test]
        public void Recognizes_XY_public_keys()
        {

            Assert.Multiple(() => {

                Assert.That(PublicKey.TryParse(JObject.Parse("""{ "x": "aa", "y": "bb" }"""), out var xy),  Is.True);
                Assert.That(xy!.IsXY,  Is.True);

                Assert.That(PublicKey.TryParse(JObject.Parse("""{ "x": "aa" }"""), out _),  Is.False);
                Assert.That(PublicKey.TryParse(JObject.Parse("""{ "y": "bb" }"""), out _),  Is.False);

            });

        }

        #endregion

        #region Recognizes_in_memory_file_infos()

        [Test]
        public void Recognizes_in_memory_file_infos()
        {

            // ChargyCore.TS needs a runtime guard here because a caller could hand
            // it any object. In C# the constructor already requires a name and the
            // data, so the equivalent assertion is that both survive.
            var fileInfo = new FileInfo("record.chargy", new Byte[] { 1, 2, 3 });

            Assert.Multiple(() => {
                Assert.That(fileInfo.Name,         Is.EqualTo("record.chargy"));
                Assert.That(fileInfo.Data.Length,  Is.EqualTo(3));
            });

        }

        #endregion


        #region Keeps_verification_result_strings_stable_for_persisted_and_displayed_results()

        [Test]
        public void Keeps_verification_result_strings_stable_for_persisted_and_displayed_results()
        {

            Assert.Multiple(() => {
                Assert.That(SessionVerificationResult.ValidSignature.  AsText(),  Is.EqualTo("ValidSignature"));
                Assert.That(SessionVerificationResult.InvalidSignature.AsText(),  Is.EqualTo("InvalidSignature"));
                Assert.That(VerificationResult.       ValidStartValue. AsText(),  Is.EqualTo("ValidStartValue"));
                Assert.That(VerificationResult.       ValidationError. AsText(),  Is.EqualTo("ValidationError"));
            });

        }

        #endregion

        #region Keeps_crypto_and_display_enum_values_stable()

        [Test]
        public void Keeps_crypto_and_display_enum_values_stable()
        {

            Assert.Multiple(() => {

                Assert.That(CryptoAlgorithm.    ECC.        AsText(),  Is.EqualTo("ECC"));
                Assert.That(CryptoHashAlgorithm.SHA256.     AsText(),  Is.EqualTo("SHA256"));
                Assert.That(PublicKeyFormat.    XY.         ToString(),Is.EqualTo("XY"));
                Assert.That(SignatureFormat.    RS.         AsText(),  Is.EqualTo("RS"));
                Assert.That(InformationRelevance.Important. AsText(),  Is.EqualTo("Important"));

                // DisplayPrefix is numeric, unlike every other Chargy enum.
                Assert.That((Int32) DisplayPrefix.KILO,  Is.EqualTo(1));

            });

        }

        #endregion

        #region A_display_prefix_is_carried_as_a_number_not_as_a_name()

        [Test]
        public void A_display_prefix_is_carried_as_a_number_not_as_a_name()
        {

            // DisplayPrefixes is one of the few numeric enums of ChargyCore.TS, so
            // JSON.stringify writes 1, not "KILO". Writing the name here would have
            // produced records the TypeScript implementation cannot read back.
            var value = new MeasurementValue(
                            "2019-04-05T14:54:50.000Z",
                            22675m,
                            ValueDisplayPrefix: DisplayPrefix.KILO
                        );

            var json = value.ToJSON();

            Assert.Multiple(() => {

                Assert.That(json["value_displayPrefix"]?.Type,   Is.EqualTo(JTokenType.Integer));
                Assert.That((Int32?) json["value_displayPrefix"], Is.EqualTo(1));

                Assert.That(MeasurementValue.TryParse(json, out var roundTrip),  Is.True);
                Assert.That(roundTrip!.ValueDisplayPrefix,  Is.EqualTo(DisplayPrefix.KILO));

            });

        }

        #endregion


        #region Converts_OBIS_values_between_human_and_hex_forms()

        [Test]
        public void Converts_OBIS_values_between_human_and_hex_forms()
        {

            Assert.Multiple(() => {
                Assert.That(ChargyLib.OBIS2Hex             ("1-0:1.8.0*255"),  Is.EqualTo("0100010800ff"));
                Assert.That(ChargyLib.ParseOBIS            ("0100010800ff"),   Is.EqualTo("1-0:1.8.0*255"));
                Assert.That(ChargyLib.OBIS2MeasurementName ("1-0:1.8.0*255"),  Is.EqualTo("ENERGY_TOTAL"));
                Assert.That(ChargyLib.MeasurementName2Human("ENERGY_TOTAL"),   Is.EqualTo("Bezogene Energiemenge"));
            });

        }

        #endregion

        #region Round_trips_byte_arrays_and_hex_strings()

        [Test]
        public void Round_trips_byte_arrays_and_hex_strings()
        {

            Byte[] bytes = [ 0, 1, 15, 16, 254, 255 ];
            var    hex   = "00010f10feff";

            Assert.Multiple(() => {

                Assert.That(ChargyLib.ParseHexString  (hex),                        Is.EqualTo(bytes));
                Assert.That(ChargyLib.CreateHexString ([ 0, 1, 15, 16, 254, 255 ]), Is.EqualTo(hex));
                Assert.That(ChargyLib.ToHex           (ChargyLib.HexToBytes(hex)),  Is.EqualTo(hex));
                Assert.That(ChargyLib.IntFromBytes    ([ 0x01, 0x00, 0x00 ]),       Is.EqualTo(65536));

            });

        }

        #endregion

        #region Rejects_odd_length_hex_strings()

        [Test]
        public void Rejects_odd_length_hex_strings()
        {

            // ChargyCore.TS throws a RangeError here; the C# equivalent is an
            // ArgumentException.
            Assert.That(() => ChargyLib.HexToBytes("abc"),  Throws.ArgumentException);

        }

        #endregion

        #region CreateHexString_truncates_values_above_a_byte()

        [Test]
        public void CreateHexString_truncates_values_above_a_byte()
        {

            // "createHexString()" keeps only the last two hexadecimal digits
            // instead of rejecting the value.
            Assert.That(ChargyLib.CreateHexString([ 4096, 511 ]),  Is.EqualTo("00ff"));

        }

        #endregion


        #region Completes_generated_multilanguage_texts_for_all_configured_UI_languages()

        [Test]
        public void Completes_generated_multilanguage_texts_for_all_configured_UI_languages()
        {

            var i18n = I18NDictionary.Parse(
                           JObject.Parse("""
                               {
                                   "Greeting":      { "de": "Hallo", "en": "Hello" },
                                   "OnlyGerman":    { "de": "Nur deutsch" },
                                   "WithParameter": { "de": "Wert %p", "en": "Value %p" }
                               }
                               """),
                           [ Languages.de, Languages.en ]
                       );

            var missing       = i18n.GetMultilanguageText("Missing");
            var onlyGerman    = i18n.GetMultilanguageText("OnlyGerman");
            var withParameter = i18n.GetMultilanguageTextWithParameter("WithParameter", 7);

            Assert.Multiple(() => {

                Assert.That(i18n.GetLocalizedMessage("Greeting"),  Is.EqualTo("Hallo"));

                Assert.That(missing[Languages.de],        Is.EqualTo("Missing"));
                Assert.That(missing[Languages.en],        Is.EqualTo("Missing"));

                Assert.That(onlyGerman[Languages.de],     Is.EqualTo("Nur deutsch"));
                Assert.That(onlyGerman[Languages.en],     Is.EqualTo("Nur deutsch"));

                Assert.That(withParameter[Languages.de],  Is.EqualTo("Wert 7"));
                Assert.That(withParameter[Languages.en],  Is.EqualTo("Value 7"));

            });

        }

        #endregion


    }

}
