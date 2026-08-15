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

using System.Text;

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Security;

using cloud.charging.open.chargy.Formats.OCMF;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the nine BET tariff text fixtures: three complete OCMF 1.4
    /// documents for each of the three Bonn tariff profiles.
    ///
    /// Each fixture comes with an expected mapping shared byte-for-byte with
    /// ChargyCore.TS, and that file is the point of the exercise: the tariff a
    /// meter signed as a semicolon-separated string has to reach a charge
    /// transparency record as the same prices in both implementations, or an EV
    /// driver gets two different invoices from one signed document.
    /// </summary>
    [TestFixture]
    public class OCMFBETTariffTests : AChargyTests
    {

        #region Data

        private const String FixtureRoot = "OCMF/BET_TariffTextExtension/";

        #endregion


        #region EveryFixtureMatchesItsExpectedMapping(Fixture)

        /// <summary>
        /// A BET fixture, read through the whole pipeline, has to produce exactly
        /// the mapping its expected file describes.
        /// </summary>
        /// <param name="Fixture">A fixture path below the BET directory, without its extension.</param>
        [TestCase("001/001-01")]
        [TestCase("001/001-02")]
        [TestCase("001/001-03")]
        [TestCase("002/002-01")]
        [TestCase("002/002-02")]
        [TestCase("002/002-03")]
        [TestCase("003/003-01")]
        [TestCase("003/003-02")]
        [TestCase("003/003-03")]
        public async Task EveryFixtureMatchesItsExpectedMapping(String Fixture)
        {

            var result = await VerifyFixtures([ $"{FixtureRoot}{Fixture}.ocmf" ]);

            Assert.That(result, Is.InstanceOf<OCMFChargeTransparencyRecord>(), VerificationReport.Format(result));

            var actual    = Normalize((OCMFChargeTransparencyRecord) result);
            var expected  = ChargyLib.ParseJSON(ReadTextFixture($"{FixtureRoot}{Fixture}.expected.json"));

            AssertEquivalent(actual, expected, "");

        }

        #endregion

        #region EveryFixtureIsSignedByTheKeyItShipsWith(Fixture)

        /// <summary>
        /// Every fixture's signature checks out against the public key of its
        /// directory — verified without Chargy, by BouncyCastle directly.
        ///
        /// A fixture is only evidence of anything if it is what it claims to be.
        /// Were one of these signatures wrong, every assertion made against it
        /// would be an assertion about a document no meter could have produced,
        /// and the mapping tests above would go on passing regardless.
        /// </summary>
        /// <param name="Fixture">A fixture path below the BET directory, without its extension.</param>
        [TestCase("001/001-01")]
        [TestCase("001/001-02")]
        [TestCase("001/001-03")]
        [TestCase("002/002-01")]
        [TestCase("002/002-02")]
        [TestCase("002/002-03")]
        [TestCase("003/003-01")]
        [TestCase("003/003-02")]
        [TestCase("003/003-03")]
        public void EveryFixtureIsSignedByTheKeyItShipsWith(String Fixture)
        {

            var document   = ReadTextFixture($"{FixtureRoot}{Fixture}.ocmf").Trim();
            var parts      = document.Split('|');

            Assert.That(parts,    Has.Length.EqualTo(3));
            Assert.That(parts[0], Is.EqualTo("OCMF"));

            var signature  = ChargyLib.ParseJSON(parts[2])["SD"]?.Value<String>();

            Assert.That(signature, Is.Not.Null);

            var publicKey  = PublicKeyFactory.CreateKey(
                                 ReadPEM($"{FixtureRoot}{Fixture[..3]}/publicKey.pem")
                             );

            var verifier   = SignerUtilities.GetSigner("SHA-256withECDSA");
            var payload    = Encoding.UTF8.GetBytes(parts[1]);

            verifier.Init(false, publicKey);
            verifier.BlockUpdate(payload, 0, payload.Length);

            Assert.That(
                verifier.VerifySignature(Convert.FromHexString(signature!)),
                Is.True,
                $"{Fixture} is not signed by the key it ships with!"
            );

        }

        #endregion

        #region ATariffTextIsOnlyAsTrustworthyAsTheSignatureAroundIt()

        /// <summary>
        /// A BET fixture read on its own says "PublicKeyNotFound" for every
        /// reading, and still delivers the full tariff.
        ///
        /// That combination is the whole point of a signed tariff text: the
        /// prices are there to be read, and the record says plainly that nothing
        /// has yet been proven about them. OCMF does not carry the public key
        /// inside the document it signs, so the key has to arrive separately —
        /// and until it does, a tariff is a claim rather than evidence.
        /// </summary>
        [Test]
        public async Task ATariffTextIsOnlyAsTrustworthyAsTheSignatureAroundIt()
        {

            var withoutKey  = await VerifyFixtures([ $"{FixtureRoot}001/001-01.ocmf" ]);
            var withKey     = await VerifyFixtures([ $"{FixtureRoot}001/001-01.ocmf",
                                                     $"{FixtureRoot}publicKey.txt" ]);

            Assert.That(withoutKey, Is.InstanceOf<OCMFChargeTransparencyRecord>(), VerificationReport.Format(withoutKey));
            Assert.That(withKey,    Is.InstanceOf<OCMFChargeTransparencyRecord>(), VerificationReport.Format(withKey));

            Assert.Multiple(() => {

                Assert.That(
                    ((OCMFChargeTransparencyRecord) withoutKey).ChargingSessions[0].Measurements[0].Values[0].Result?.Status,
                    Is.EqualTo(VerificationResult.PublicKeyNotFound)
                );

                Assert.That(
                    ((OCMFChargeTransparencyRecord) withoutKey).OCMF.TariffTextInterpretation?.EnergyFeeCentsPerKWh,
                    Is.EqualTo(35)
                );

                // The same document with its key: same tariff, proven.
                Assert.That(
                    ((OCMFChargeTransparencyRecord) withKey).ChargingSessions[0].Measurements[0].Values[0].Result?.Status,
                    Is.EqualTo(VerificationResult.ValidSignature)
                );

                Assert.That(
                    ((OCMFChargeTransparencyRecord) withKey).OCMF.TariffTextInterpretation?.EnergyFeeCentsPerKWh,
                    Is.EqualTo(35)
                );

            });

        }

        #endregion


        #region MarkupInsideAPayloadIsCarriedThroughUntouched()

        /// <summary>
        /// A document whose gateway fields hold HTML and JavaScript.
        ///
        /// The library must not clean any of it up. What the meter signed is
        /// those exact characters, and a reader that quietly stripped them would
        /// be showing a payload that no longer matches the signature it is
        /// vouched for by — the strings would come out different and the
        /// signature check, which sees the untouched text, would go on saying
        /// "valid". Escaping this is the presentation layer's job, at the moment
        /// of presentation, and it can only do that job if the characters arrive.
        ///
        /// Neither implementation had a test for this fixture; the fixture was
        /// sitting in the test data unused.
        /// </summary>
        [Test]
        public async Task MarkupInsideAPayloadIsCarriedThroughUntouched()
        {

            var result = await VerifyFixtures([ $"{FixtureRoot}001-01__xss.ocmf" ]);

            Assert.That(result, Is.InstanceOf<OCMFChargeTransparencyRecord>(), VerificationReport.Format(result));

            var record   = (OCMFChargeTransparencyRecord) result;
            var session  = record.ChargingSessions[0];

            Assert.Multiple(() => {

                Assert.That(
                    record.OCMF.GatewayInformation,
                    Is.EqualTo("BET Fixture Generator<img src=\"https://git.graphdefined.com/uploads/-/system/user/avatar/2/avatar.png\">")
                );

                Assert.That(
                    record.OCMF.GatewaySerial,
                    Is.EqualTo("BET-GW-001-01<img src=x onerror=\"javascript:alert('here!')\">")
                );

                Assert.That(session.AuthorizationStart?.IdentificationLevel,  Does.StartWith("<object data=\"data:text/html;base64,"));
                Assert.That(session.AuthorizationStart?.IdentificationLevel,  Does.EndWith("\"></object>"));

                // The rest of the document is read as usual: the markup sits in
                // three string fields and says nothing about the readings.
                Assert.That(record.OCMF.TariffTextInterpretation?.Code,       Is.EqualTo("001"));
                Assert.That(session.Measurements[0].Values,                   Has.Count.EqualTo(2));

            });

        }

        #endregion


        #region (private, static) Normalize      (Record)

        /// <summary>
        /// Reduce a record to the fields the expected files describe.
        ///
        /// Written out by hand rather than taken from the model's own
        /// serialization, so that the shape being compared is the shape
        /// ChargyCore.TS compares and not whatever this port happens to emit.
        /// </summary>
        /// <param name="Record">A charge transparency record.</param>
        private static JObject Normalize(OCMFChargeTransparencyRecord Record)
        {

            var session      = Record.ChargingSessions[0];
            var measurement  = session.Measurements[0];
            var cable        = session.Connector?.Cable;

            return new JObject(

                       new JProperty("ocmf", new JObject(
                           new JProperty("formatVersion",             Record.OCMF.FormatVersion),
                           new JProperty("tariffText",                Record.OCMF.TariffText),
                           new JProperty("tariffTextInterpretation",  Record.OCMF.TariffTextInterpretation?.ToJSON())
                       )),

                       new JProperty("chargingTariff",  NormalizeTariff(Record.ChargingTariffs.FirstOrDefault())),

                       new JProperty("session", new JObject(
                           new JProperty("chargingStationId",         session.ChargingStationId),
                           new JProperty("EVSEId",                    session.EVSEId),
                           new JProperty("ConnectorId",               session.ConnectorId),
                           new JProperty("chargingStationFirmware",   session.ChargingStation?.Firmware?.Version),
                           new JProperty("cable",                     cable is null
                                                                          ? JValue.CreateNull()
                                                                          : new JObject(
                                                                                new JProperty("lossCompensation",    cable.LossCompensation),
                                                                                new JProperty("lossCompensationId",  cable.LossCompensationId),
                                                                                new JProperty("resistance",          cable.Resistance),
                                                                                new JProperty("resistanceUnit",      cable.ResistanceUnit)
                                                                            )),
                           new JProperty("tariffId",                  session.TariffId),
                           new JProperty("chargingTariff",            NormalizeTariff(session.ChargingTariffs.FirstOrDefault()))
                       )),

                       new JProperty("measurement", new JObject(
                           new JProperty("energyMeterId",  measurement.EnergyMeterId),
                           new JProperty("obis",           measurement.OBIS),
                           new JProperty("unit",           measurement.Unit),
                           new JProperty("values",         new JArray(
                               measurement.Values.Select(value => new JObject(
                                   new JProperty("timestamp",  value.Timestamp),
                                   new JProperty("value",      value.Value),
                                   new JProperty("status",     value.Result?.Status.AsText())
                               ))
                           ))
                       ))

                   );

        }

        #endregion

        #region (private, static) NormalizeTariff(Tariff)

        /// <summary>
        /// Reduce a charging tariff to the fields the expected files describe.
        /// </summary>
        /// <param name="Tariff">A charging tariff, when there is one.</param>
        private static JToken NormalizeTariff(ChargingTariff? Tariff)

            => Tariff is null
                   ? JValue.CreateNull()
                   : new JObject(
                         new JProperty("@id",       Tariff.Id),
                         new JProperty("currency",  Tariff.Currency),
                         new JProperty("elements",  new JArray(
                             Tariff.Elements.Select(element => {

                                 var json = new JObject(
                                                new JProperty("price_components", new JArray(
                                                    element.PriceComponents.Select(component => new JObject(
                                                        new JProperty("type",       component.Type),
                                                        new JProperty("price",      component.Price),
                                                        new JProperty("step_size",  component.StepSize)
                                                    ))
                                                ))
                                            );

                                 if (element.Restrictions?.MinDuration is Int64 minDuration)
                                     json.Add(new JProperty("restrictions", new JObject(
                                         new JProperty("min_duration", minDuration)
                                     )));

                                 return json;

                             })
                         ))
                     );

        #endregion

        #region (private, static) ReadPEM        (FixtureName)

        /// <summary>
        /// Read the DER bytes out of a PEM encoded public key file.
        /// </summary>
        /// <param name="FixtureName">A fixture path relative to "TestData".</param>
        private static Byte[] ReadPEM(String FixtureName)

            => Convert.FromBase64String(
                   String.Concat(
                       ReadTextFixture(FixtureName).
                           Split('\n').
                           Where(line => !line.StartsWith("-----", StringComparison.Ordinal)).
                           Select(line => line.Trim())
                   )
               );

        #endregion

        #region (private, static) AssertEquivalent(Actual, Expected, Path)

        /// <summary>
        /// Compare two JSON documents by value.
        ///
        /// Numbers are compared as numbers rather than as text: a price of three
        /// euros computed as "5 cents a minute times sixty" is the decimal 3.00,
        /// and the expected file writes 3. Nothing here is signed, so how many
        /// trailing zeros a number was written with is not a claim about it — the
        /// amount is.
        /// </summary>
        /// <param name="Actual">What this port produced.</param>
        /// <param name="Expected">What the expected file describes.</param>
        /// <param name="Path">Where in the document we are, for the failure message.</param>
        private static void AssertEquivalent(JToken?  Actual,
                                             JToken?  Expected,
                                             String   Path)
        {

            if (Expected is null || Expected.Type == JTokenType.Null)
            {
                Assert.That(Actual is null || Actual.Type == JTokenType.Null, Is.True, $"{Path} should be null, but is '{Actual}'");
                return;
            }

            Assert.That(Actual, Is.Not.Null, $"{Path} is missing");

            switch (Expected.Type)
            {

                case JTokenType.Object:
                    {

                        var expectedObject  = (JObject) Expected;
                        var actualObject    = Actual as JObject;

                        Assert.That(actualObject, Is.Not.Null, $"{Path} should be an object, but is '{Actual}'");

                        foreach (var property in expectedObject.Properties())
                            AssertEquivalent(actualObject![property.Name], property.Value, $"{Path}.{property.Name}");

                        Assert.That(
                            actualObject!.Properties().Select(property => property.Name).Except(expectedObject.Properties().Select(property => property.Name)),
                            Is.Empty,
                            $"{Path} has properties the expected file does not describe"
                        );

                    }
                    return;

                case JTokenType.Array:
                    {

                        var expectedArray  = (JArray) Expected;
                        var actualArray    = Actual as JArray;

                        Assert.That(actualArray,        Is.Not.Null,                        $"{Path} should be an array, but is '{Actual}'");
                        Assert.That(actualArray!.Count, Is.EqualTo(expectedArray.Count),    $"{Path} has {actualArray.Count} element(s) instead of {expectedArray.Count}");

                        for (var i = 0; i < expectedArray.Count; i++)
                            AssertEquivalent(actualArray[i], expectedArray[i], $"{Path}[{i}]");

                    }
                    return;

                case JTokenType.Integer:
                case JTokenType.Float:
                    Assert.That(Actual!.Type is JTokenType.Integer or JTokenType.Float, Is.True, $"{Path} should be a number, but is '{Actual}'");
                    Assert.That(Actual.Value<Decimal>(), Is.EqualTo(Expected.Value<Decimal>()), $"{Path}");
                    return;

                default:
                    Assert.That(Actual!.Value<String>(), Is.EqualTo(Expected.Value<String>()), $"{Path}");
                    return;

            }

        }

        #endregion

    }

}
