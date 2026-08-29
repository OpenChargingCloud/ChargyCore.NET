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

using System.Globalization;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.IO;
using cloud.charging.open.chargy.Formats.OCMF;
using cloud.charging.open.chargy.qrcodes;

#endregion

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Tests for the charge transparency live link.
    ///
    /// A live link is not charging data — it is an instruction to go and fetch
    /// some, from URLs printed on a sticker on a charging station. That makes it
    /// the one input where being lenient is the wrong instinct: everything else
    /// Chargy reads is evidence to be judged, while this is a list of addresses
    /// somebody is being invited to contact.
    ///
    /// It may nevertheless carry the signed meter values read so far, and those
    /// are evidence like any other — read, verified and shown alongside the link
    /// rather than in its place.
    /// </summary>
    [TestFixture]
    public class ChargeTransparencyLiveLinkTests : AChargyTests
    {

        #region Data

        /// <summary>
        /// The last document of the OCMF-Test-01 series: a completed 22 kW AC
        /// charging session of three minutes, with a signed OCMF meter value
        /// every ten seconds.
        /// </summary>
        private const String CompletedSession  = "ChargeTransparencyLive/ChargeTransparencyLiveLink_1.json";

        /// <summary>
        /// The first document of the same series: everything about the station is
        /// there, and not one meter value yet.
        /// </summary>
        private const String NothingMeasuredYet = "ChargeTransparencyLive/OCMF-Test-01/OCMF-Test-01__0000.json";

        #endregion


        #region ALiveLinkIsRecognisedByItsContext()

        /// <summary>
        /// What is and is not a live link.
        /// </summary>
        [Test]
        public void ALiveLinkIsRecognisedByItsContext()
        {

            var liveLink = ChargyLib.ParseJSON(ReadTextFixture(CompletedSession));

            Assert.Multiple(() => {

                Assert.That(ChargeTransparencyLiveLink.IsAChargeTransparencyLiveLink(liveLink),  Is.True);

                // Another context is another kind of document, whatever else it
                // may look like.
                var otherContext = (JObject) liveLink.DeepClone();
                otherContext["@context"] = "https://example.com/other";
                Assert.That(ChargeTransparencyLiveLink.IsAChargeTransparencyLiveLink(otherContext),  Is.False);

                // A transport Chargy cannot speak makes the link useless as a
                // whole: it claims to say how to reach the station and the answer
                // is one nobody can follow. Accepting the document and quietly
                // dropping that transport would leave an application believing it
                // had been told everything.
                var unknownTransport = (JObject) liveLink.DeepClone();
                unknownTransport["liveTransports"] = new JArray(
                                                         new JObject(
                                                             new JProperty("type",  "ftp"),
                                                             new JProperty("url",   "https://example.com")
                                                         )
                                                     );
                Assert.That(ChargeTransparencyLiveLink.IsAChargeTransparencyLiveLink(unknownTransport),  Is.False);

                // A refresh period is a number of seconds, and on https it is
                // read rather than ignored — so a value that is not a number
                // makes the document unusable like any other malformed field.
                var textualRefresh = (JObject) liveLink.DeepClone();
                textualRefresh["liveTransports"]![0]!["refresh"] = "often";
                Assert.That(ChargeTransparencyLiveLink.IsAChargeTransparencyLiveLink(textualRefresh),  Is.False);

                Assert.That(ChargeTransparencyLiveLink.IsAChargeTransparencyLiveLink([]),  Is.False);

            });

        }

        #endregion

        #region ALiveLinkIsReadWithEverythingItStates()

        /// <summary>
        /// A live link read through the whole pipeline, with everything it says
        /// about where the station is and how to reach it.
        ///
        /// The transports are asserted in detail because they are the point of
        /// the document: three ways of reaching the same charging session, two of
        /// them behind a one-time password, one of them a weighted list to spread
        /// the load across two hosts. A reader that lost the weights or the
        /// shared secret would leave an application with addresses it cannot use.
        /// </summary>
        [Test]
        public async Task ALiveLinkIsReadWithEverythingItStates()
        {

            var result = await VerifyFixtures([ CompletedSession ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyLiveLink>(), VerificationReport.Format(result));

            var liveLink = (ChargeTransparencyLiveLink) result;

            Assert.Multiple(() => {

                Assert.That(liveLink.Created,                        Is.EqualTo("2026-08-28T11:59:59Z"));
                Assert.That(liveLink.Description?[Languages.de],     Is.EqualTo("OCMF-Test-01 Transparenz Live-Link"));

                // Upstream 3a2d3ad moved the position and the connector onto the
                // charging station and its EVSE, where a charge transparency
                // record has always kept them. Both are still read.
                Assert.That(liveLink.GeoLocation?.Latitude. Value,   Is.EqualTo(50.9279287).Within(0.000001));
                Assert.That(liveLink.GeoLocation?.Longitude.Value,   Is.EqualTo(11.5731785).Within(0.000001));

                Assert.That(liveLink.Connector?.Standard,            Is.EqualTo("Type 2"));
                Assert.That(liveLink.Connector?.Format,              Is.EqualTo("Socket"));
                Assert.That(liveLink.Connector?.PowerType,           Is.EqualTo("AC"));
                Assert.That(liveLink.Connector?.MaxPower,            Is.EqualTo("22 kW"));

                Assert.That(liveLink.LiveTransports,                 Has.Count.EqualTo(3));

                #region One URL, no one-time password, and how often to ask again

                Assert.That(liveLink.LiveTransports[0].Type,             Is.EqualTo(TransportType.HTTPS));
                Assert.That(liveLink.LiveTransports[0].URL,              Is.EqualTo("https://api1.example.com/chargingSessions/OCMF-Test-01/transparency/live?token=abcdef"));
                Assert.That(liveLink.LiveTransports[0].URLs,             Is.Empty);
                Assert.That(liveLink.LiveTransports[0].TOTP,             Is.Null);

                // Ten seconds, which is the interval at which this station's
                // meter takes a reading.
                Assert.That(liveLink.LiveTransports[0].Refresh,          Is.EqualTo(TimeSpan.FromSeconds(10)));

                #endregion

                #region Two endpoints, weighted, behind a one-time password

                Assert.That(liveLink.LiveTransports[1].Type,             Is.EqualTo(TransportType.WebSocket));
                Assert.That(liveLink.LiveTransports[1].URLs,             Has.Count.EqualTo(2));
                Assert.That(liveLink.LiveTransports[1].URLs[0].URL,      Is.EqualTo("wss://api1.example.com/chargingSessions/OCMF-Test-01/transparency/live"));
                Assert.That(liveLink.LiveTransports[1].URLs[0].Priority, Is.EqualTo(10));
                Assert.That(liveLink.LiveTransports[1].URLs[0].Weight,   Is.EqualTo(60));
                Assert.That(liveLink.LiveTransports[1].URLs[1].Weight,   Is.EqualTo(40));

                Assert.That(liveLink.LiveTransports[1].TOTP?.InitialSharedSecret,  Is.EqualTo("abcdefghijklmnopqrstuvwxyz1234567890"));
                Assert.That(liveLink.LiveTransports[1].TOTP?.TimeStep,             Is.EqualTo(10));

                // Only https says how often to ask again. On a transport that
                // pushes, a period would mean something else, so it is not read.
                Assert.That(liveLink.LiveTransports[1].Refresh,          Is.Null);

                #endregion

                #region ..., and endpoints written as bare strings

                Assert.That(liveLink.LiveTransports[2].Type,             Is.EqualTo(TransportType.HTTPSSE));
                Assert.That(liveLink.LiveTransports[2].URLs,             Has.Count.EqualTo(2));
                Assert.That(liveLink.LiveTransports[2].URLs[0].URL,      Is.EqualTo("https://api1.example.com/chargingSessions/OCMF-Test-01/transparency/live"));
                Assert.That(liveLink.LiveTransports[2].URLs[0].Priority, Is.Null);

                #endregion

            });

        }

        #endregion

        #region ALiveLinkStaysALiveLinkWhateverItCarries()

        /// <summary>
        /// A live link describes one charging session that is still running, a
        /// charge transparency record a collection of finished ones. Carrying
        /// signed meter values does not turn the one into the other, and an
        /// application shows the two differently.
        /// </summary>
        [Test]
        public async Task ALiveLinkStaysALiveLinkWhateverItCarries()
        {

            var withMeterValues     = await VerifyFixtures([ CompletedSession ]);
            var withoutMeterValues  = await VerifyFixtures([ NothingMeasuredYet ]);

            Assert.Multiple(() => {

                Assert.That(withMeterValues,     Is.InstanceOf<ChargeTransparencyLiveLink>(), VerificationReport.Format(withMeterValues));
                Assert.That(withMeterValues,     Is.Not.InstanceOf<ChargeTransparencyRecord>());

                Assert.That(withoutMeterValues,  Is.InstanceOf<ChargeTransparencyLiveLink>(), VerificationReport.Format(withoutMeterValues));

                Assert.That(((ChargeTransparencyLiveLink) withoutMeterValues).Created,         Is.EqualTo("2026-08-28T11:59:59Z"));
                Assert.That(((ChargeTransparencyLiveLink) withoutMeterValues).LiveTransports,  Has.Count.EqualTo(3));

            });

        }

        #endregion

        #region TheSignedMeterValuesBecomeAVerifiedRecord()

        /// <summary>
        /// The signed meter values a live link carries, read on demand and
        /// verified with the public keys of the very same document.
        ///
        /// This is the case a single public key cannot serve: the meter signs the
        /// start and the end value of the session, the operator signs the
        /// nineteen readings in between, and every one of them has to verify. So
        /// each document is tried against every key the document names for that
        /// purpose, and keeps the first signature that holds.
        /// </summary>
        [Test]
        public void TheSignedMeterValuesBecomeAVerifiedRecord()
        {

            Assert.That(ChargeTransparencyLiveLink.TryParse(ChargyLib.ParseJSON(ReadTextFixture(CompletedSession)), out var liveLink), Is.True);

            var record = Detector().TryToParseLiveLinkMeterValues(liveLink!);

            Assert.That(record, Is.Not.Null);

            var chargingSession = record!.ChargingSessions[0];

            Assert.Multiple(() => {

                Assert.That(record.ChargingSessions,        Has.Count.EqualTo(1));
                Assert.That(chargingSession.EVSEId,         Is.EqualTo("DE*GEF*E12345678*1"));
                Assert.That(chargingSession.Measurements,   Has.Count.EqualTo(1));

                var measurement = chargingSession.Measurements[0];

                // 19 OCMF documents, but the last one repeats the start value
                // next to the end value.
                Assert.That(measurement.Name,               Is.EqualTo("ENERGY_TOTAL"));
                Assert.That(measurement.Values,             Has.Count.EqualTo(20));

                // The live link carries the public keys, so unlike a bare OCMF
                // file every one of these readings can actually be verified.
                Assert.That(measurement.Values.Select(value => value.Result?.Status),
                            Is.All.EqualTo(VerificationResult.ValidSignature),
                            VerificationReport.Format(record));

            });

        }

        #endregion

        #region NoSingleKeyVerifiesTheWholeSession()

        /// <summary>
        /// Why the meter values are checked against every candidate key rather
        /// than against one: neither key of this session verifies all of it.
        ///
        /// The energy meter signs the start value and the end value, the operator
        /// signs the seventeen readings in between. Either key alone leaves most
        /// of the session unverified, and a reader that took only the first key
        /// it found would report exactly that to an EV driver whose session is
        /// entirely sound.
        /// </summary>
        [Test]
        public void NoSingleKeyVerifiesTheWholeSession()
        {

            var json         = ChargyLib.ParseJSON(ReadTextFixture(CompletedSession));

            var documents    = json["signedMeterValues"]!["values"]!.Select(value => value.Value<String>()!).ToArray();

            var operatorKey  = json["chargingStationOperator"]!["publicKeys"]!.
                                   OfType<JObject>().
                                   First(key => key["keyUsage"]!.Any(usage => usage.Value<String>() == "signEnergyMeterValues"))
                                   ["value"]!.Value<String>()!;

            var meterKey     = json["chargingStation"]!["EVSE"]!["energyMeter"]!["publicKeys"]![0]!["value"]!.Value<String>()!;

            Assert.Multiple(() => {
                Assert.That(ValidReadings([ operatorKey ]),             Is.EqualTo(17));
                Assert.That(ValidReadings([ meterKey    ]),             Is.EqualTo(3));
                Assert.That(ValidReadings([ operatorKey, meterKey ]),   Is.EqualTo(20));

                // The order the keys are offered in must not decide the outcome.
                Assert.That(ValidReadings([ meterKey,    operatorKey ]),   Is.EqualTo(20));
            });

            Int32 ValidReadings(String[] PublicKeys)
            {

                var result = new OCMFFormat(I18NDictionary.Default()).
                                 TryParseTexts(documents, PublicKeys);

                Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

                return ((ChargeTransparencyRecord) result).ChargingSessions[0].Measurements[0].Values.
                           Count(value => value.Result?.Status == VerificationResult.ValidSignature);

            }

        }

        #endregion

        #region ALiveLinkWithoutMeterValuesHasNoneToShow()

        /// <summary>
        /// The normal state of the first document of a series: the station is
        /// described, the addresses work, and nothing has been measured yet.
        /// </summary>
        [Test]
        public void ALiveLinkWithoutMeterValuesHasNoneToShow()
        {

            Assert.That(ChargeTransparencyLiveLink.TryParse(ChargyLib.ParseJSON(ReadTextFixture(NothingMeasuredYet)), out var liveLink), Is.True);

            Assert.That(Detector().TryToParseLiveLinkMeterValues(liveLink!),  Is.Null);

        }

        #endregion

        #region MeterValuesInAFormatNobodyReadsDoNotCostTheLink()

        /// <summary>
        /// A meter value section this library cannot read is left alone rather
        /// than guessed at, and the live link survives it: the transports still
        /// work, there is just nothing to show.
        /// </summary>
        [Test]
        public void MeterValuesInAFormatNobodyReadsDoNotCostTheLink()
        {

            var json = ChargyLib.ParseJSON(ReadTextFixture(CompletedSession));

            json["signedMeterValues"]!["encodings"] = new JArray("SomeFutureFormat", "plain");

            Assert.That(ChargeTransparencyLiveLink.TryParse(json, out var liveLink),  Is.True);

            Assert.Multiple(() => {
                Assert.That(Detector().TryToParseLiveLinkMeterValues(liveLink!),  Is.Null);
                Assert.That(liveLink!.LiveTransports,                             Has.Count.EqualTo(3));
            });

        }

        #endregion

        #region ALiveLinkWithoutATimestampIsStampedOnArrival()

        /// <summary>
        /// A live link that does not say when it was created is stamped when it
        /// is read.
        ///
        /// Live data is only worth anything if its age is known, and a sticker on
        /// a charging station carries no clock. The moment it was read is the
        /// earliest thing anyone can honestly say about it.
        /// </summary>
        [Test]
        public async Task ALiveLinkWithoutATimestampIsStampedOnArrival()
        {

            var before  = DateTimeOffset.UtcNow.AddSeconds(-1);
            var result  = await VerifyFixtures([ "ChargeTransparencyLive/ChargeTransparencyLiveLink_2.json" ]);
            var after   = DateTimeOffset.UtcNow.AddSeconds(1);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyLiveLink>(), VerificationReport.Format(result));

            var created = ((ChargeTransparencyLiveLink) result).Created;

            Assert.That(created, Is.Not.Null);

            Assert.Multiple(() => {

                // Written the way the rest of Chargy writes an instant, so that a
                // stamped link and a signed one can be compared without knowing
                // which is which.
                Assert.That(
                    DateTimeOffset.TryParseExact(
                        created,
                        "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var parsed
                    ),
                    Is.True,
                    $"'{created}' is not an ISO 8601 UTC timestamp"
                );

                Assert.That(parsed, Is.InRange(before, after));

                // The stamp reaches the document as well, not only the property:
                // it is what a caller storing the link will read back later.
                Assert.That(((ChargeTransparencyLiveLink) result).ToJSON()["created"]?.Value<String>(),  Is.EqualTo(created));

            });

        }

        #endregion

        #region ALiveLinkSurvivesBeingReadAndWrittenBack()

        /// <summary>
        /// A live link that was read is written back exactly as it arrived —
        /// including everything this reader does not model.
        ///
        /// That is not tidiness. The document is signed over the whole of itself
        /// with the signatures excluded, so a reader that reassembled it from its
        /// own model would drop the station, the operator, the meter and the
        /// meter values, and with them the bytes those signatures cover. An
        /// application that stores a scanned link and reads it back later must
        /// find the same document, not a summary of the parts this library
        /// happens to understand.
        /// </summary>
        [Test]
        public void ALiveLinkSurvivesBeingReadAndWrittenBack()
        {

            var original = ChargyLib.ParseJSON(ReadTextFixture(CompletedSession));

            Assert.That(ChargeTransparencyLiveLink.TryParse(original, out var liveLink), Is.True);
            Assert.That(liveLink, Is.Not.Null);

            var roundTripped = liveLink!.ToJSON();

            Assert.Multiple(() => {

                // An endpoint written as a bare string comes back as a bare
                // string: the two forms carry the same address, and rewriting one
                // into the other would make a stored link differ from the one
                // that was scanned, for no gain.
                Assert.That(roundTripped["liveTransports"]?[2]?["urls"]?[0]?.Type,  Is.EqualTo(JTokenType.String));

                // The parts nobody here models, and the signatures taken over them.
                Assert.That(roundTripped["chargingStationOperator"],               Is.Not.Null);
                Assert.That(roundTripped["signedMeterValues"],                     Is.Not.Null);
                Assert.That(roundTripped["signatures"]?.Count(),                   Is.EqualTo(2));

                Assert.That(
                    JToken.DeepEquals(roundTripped, original),
                    Is.True,
                    $"Round-tripped:\n{roundTripped.ToString(Newtonsoft.Json.Formatting.Indented)}\n\nOriginal:\n{original.ToString(Newtonsoft.Json.Formatting.Indented)}"
                );

            });

        }

        #endregion

        #region ALiveLinkBuiltInCodeWritesTheCurrentNames()

        /// <summary>
        /// A live link that was never read from a document has nothing to write
        /// back, so it is assembled from what it holds — under the names upstream
        /// 0.13.0 settled on, which is what a reader will be looking for.
        /// </summary>
        [Test]
        public void ALiveLinkBuiltInCodeWritesTheCurrentNames()
        {

            var json = new ChargeTransparencyLiveLink(
                           Created:         "2026-08-28T11:59:59Z",
                           LiveTransports:  [
                                                new Transport(
                                                    TransportType.HTTPS,
                                                    URLs:     [ new TransportURL("https://station.example/live") ],
                                                    Refresh:  TimeSpan.FromSeconds(10)
                                                )
                                            ]
                       ).ToJSON();

            Assert.Multiple(() => {

                Assert.That(json["created"]?.       Value<String>(),  Is.EqualTo("2026-08-28T11:59:59Z"));
                Assert.That(json["timestamp"],                        Is.Null);
                Assert.That(json["transports"],                       Is.Null);

                Assert.That(json["liveTransports"]?[0]?["refresh"]?.Value<Int32>(),  Is.EqualTo(10));

                // An endpoint that says nothing but its address is written as
                // that address, the way the fixtures write one.
                Assert.That(json["liveTransports"]?[0]?["urls"]?[0]?.Type,           Is.EqualTo(JTokenType.String));

            });

            Assert.That(ChargeTransparencyLiveLink.IsAChargeTransparencyLiveLink(json),  Is.True);

        }

        #endregion


        #region (private, static) Detector()

        /// <summary>
        /// The same pipeline <see cref="AChargyTests.Verify"/> runs files through.
        /// </summary>
        private static ContentFormatDetector Detector()

            => new (
                   I18NDictionary.Default(),
                   ChargeTransparencyFormats.All(I18NDictionary.Default()),
                   new PDFAttachmentExtractor(),
                   new QRCodeDecoder()
               );

        #endregion


    }

}
