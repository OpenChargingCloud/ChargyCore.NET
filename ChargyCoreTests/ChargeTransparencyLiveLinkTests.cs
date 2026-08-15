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
    /// </summary>
    [TestFixture]
    public class ChargeTransparencyLiveLinkTests : AChargyTests
    {

        #region ALiveLinkIsRecognisedByItsContext()

        /// <summary>
        /// What is and is not a live link.
        /// </summary>
        [Test]
        public void ALiveLinkIsRecognisedByItsContext()
        {

            var liveLink = ChargyLib.ParseJSON(ReadTextFixture("ChargeTransparencyLive/ChargeTransparencyLiveLink_1.json"));

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
                unknownTransport["transports"] = new JArray(
                                                     new JObject(
                                                         new JProperty("type",  "ftp"),
                                                         new JProperty("url",   "https://example.com")
                                                     )
                                                 );
                Assert.That(ChargeTransparencyLiveLink.IsAChargeTransparencyLiveLink(unknownTransport),  Is.False);

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
        /// the load across three hosts. A reader that lost the weights or the
        /// shared secret would leave an application with addresses it cannot use.
        /// </summary>
        [Test]
        public async Task ALiveLinkIsReadWithEverythingItStates()
        {

            var result = await VerifyFixtures([ "ChargeTransparencyLive/ChargeTransparencyLiveLink_1.json" ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyLiveLink>(), VerificationReport.Format(result));

            var liveLink = (ChargeTransparencyLiveLink) result;

            Assert.Multiple(() => {

                Assert.That(liveLink.Timestamp,                      Is.EqualTo("2026-06-12T14:03:12Z"));
                Assert.That(liveLink.Description?[Languages.de],     Is.EqualTo("Ladestation 1234567890 Transparenz Live-Link"));
                Assert.That(liveLink.ImageURLs,                      Is.EqualTo(new[] { "https://api1.example.com/images/logo_transparencyLinks.svg" }));
                Assert.That(liveLink.GeoLocation?.Latitude. Value,   Is.EqualTo(50.387945).Within(0.000001));
                Assert.That(liveLink.GeoLocation?.Longitude.Value,   Is.EqualTo(10.4304).  Within(0.000001));

                Assert.That(liveLink.Connector?.Standard,            Is.EqualTo("CCS"));
                Assert.That(liveLink.Connector?.Format,              Is.EqualTo("Type 2"));
                Assert.That(liveLink.Connector?.PowerType,           Is.EqualTo("DC"));
                Assert.That(liveLink.Connector?.MaxPower,            Is.EqualTo("150 kW"));

                Assert.That(liveLink.Transports,                     Has.Count.EqualTo(3));

                #region One URL, no one-time password

                Assert.That(liveLink.Transports[0].Type,             Is.EqualTo(TransportType.HTTPS));
                Assert.That(liveLink.Transports[0].URL,              Is.EqualTo("https://api1.example.com/chargingSessions/1234567890/transparency/live?token=abcdef"));
                Assert.That(liveLink.Transports[0].URLs,             Is.Empty);
                Assert.That(liveLink.Transports[0].TOTP,             Is.Null);

                #endregion

                #region Three endpoints, weighted, behind a one-time password

                Assert.That(liveLink.Transports[1].Type,             Is.EqualTo(TransportType.WebSocket));
                Assert.That(liveLink.Transports[1].URLs,             Has.Count.EqualTo(3));
                Assert.That(liveLink.Transports[1].URLs[0].URL,      Is.EqualTo("wss://api1.example.com/chargingSessions/1234567890/transparency/live"));
                Assert.That(liveLink.Transports[1].URLs[0].Priority, Is.EqualTo(10));
                Assert.That(liveLink.Transports[1].URLs[0].Weight,   Is.EqualTo(60));
                Assert.That(liveLink.Transports[1].URLs[1].Weight,   Is.EqualTo(40));

                // The third names a priority and no weight, which is not the same
                // as a weight of zero — it is a fallback to be used when the two
                // above it cannot be reached.
                Assert.That(liveLink.Transports[1].URLs[2].Priority, Is.EqualTo(20));
                Assert.That(liveLink.Transports[1].URLs[2].Weight,   Is.Null);

                Assert.That(liveLink.Transports[1].TOTP?.InitialSharedSecret,  Is.EqualTo("abcdefghijklmnopqrstuvwxyz1234567890"));
                Assert.That(liveLink.Transports[1].TOTP?.TimeStep,             Is.EqualTo(10));

                #endregion

                #region ..., and endpoints written as bare strings

                Assert.That(liveLink.Transports[2].Type,             Is.EqualTo(TransportType.HTTPSSE));
                Assert.That(liveLink.Transports[2].URLs,             Has.Count.EqualTo(2));
                Assert.That(liveLink.Transports[2].URLs[0].URL,      Is.EqualTo("https://api1.example.com/chargingSessions/1234567890/transparency/live"));
                Assert.That(liveLink.Transports[2].URLs[0].Priority, Is.Null);

                #endregion

            });

        }

        #endregion

        #region ALiveLinkWithoutATimestampIsStampedOnArrival()

        /// <summary>
        /// A live link that does not say when it was written is stamped when it
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

            var timestamp = ((ChargeTransparencyLiveLink) result).Timestamp;

            Assert.That(timestamp, Is.Not.Null);

            Assert.Multiple(() => {

                // Written the way the rest of Chargy writes an instant, so that a
                // stamped link and a signed one can be compared without knowing
                // which is which.
                Assert.That(
                    DateTimeOffset.TryParseExact(
                        timestamp,
                        "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var parsed
                    ),
                    Is.True,
                    $"'{timestamp}' is not an ISO 8601 UTC timestamp"
                );

                Assert.That(parsed, Is.InRange(before, after));

            });

        }

        #endregion

        #region ALiveLinkSurvivesBeingReadAndWrittenBack()

        /// <summary>
        /// Nothing in a live link is signed, so a round trip is the only claim
        /// available — but it is the one that matters here: an application that
        /// stores a scanned link and reads it back later must find the same
        /// addresses, weights and secrets it was given.
        /// </summary>
        [Test]
        public void ALiveLinkSurvivesBeingReadAndWrittenBack()
        {

            var original = ChargyLib.ParseJSON(ReadTextFixture("ChargeTransparencyLive/ChargeTransparencyLiveLink_1.json"));

            Assert.That(ChargeTransparencyLiveLink.TryParse(original, out var liveLink), Is.True);
            Assert.That(liveLink, Is.Not.Null);

            var roundTripped = liveLink!.ToJSON();

            // An endpoint written as a bare string comes back as a bare string:
            // the two forms carry the same address, and rewriting one into the
            // other would make a stored link differ from the one that was
            // scanned, for no gain.
            Assert.That(roundTripped["transports"]?[2]?["urls"]?[0]?.Type, Is.EqualTo(JTokenType.String));

            // An empty signature array says as little as no signature array.
            original.Remove("signatures");

            Assert.That(
                JToken.DeepEquals(roundTripped, original),
                Is.True,
                $"Round-tripped:\n{roundTripped.ToString(Newtonsoft.Json.Formatting.Indented)}\n\nOriginal:\n{original.ToString(Newtonsoft.Json.Formatting.Indented)}"
            );

        }

        #endregion

    }

}
