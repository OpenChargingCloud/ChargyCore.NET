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

using cloud.charging.open.chargy.LiveLink;

#endregion

namespace cloud.charging.open.chargy.tests.LiveLink
{

    /// <summary>
    /// Tests for choosing which of a charging station's addresses to contact, and
    /// what to ask it for.
    /// </summary>
    [TestFixture]
    public class LiveLinkEndpointTests : AChargyTests
    {

        #region ASingleURLIsTheEndpoint()

        /// <summary>
        /// A transport that names one URL outright has named its endpoint.
        /// </summary>
        [Test]
        public void ASingleURLIsTheEndpoint()

            => Assert.That(
                   LiveLinkEndpoints.InPreferenceOrder(
                       new Transport(TransportType.HTTPS, "https://api1.example.com/live")
                   ),
                   Is.EqualTo(new[] { "https://api1.example.com/live" })
               );

        #endregion

        #region TheLowerPriorityIsTriedFirst()

        /// <summary>
        /// A priority is a fallback, not a preference among equals: everything at
        /// priority 10 has to be exhausted before anything at 20 is touched.
        /// </summary>
        [Test]
        public void TheLowerPriorityIsTriedFirst()
        {

            var transport = new Transport(
                                TransportType.WebSocket,
                                URLs: [
                                    new TransportURL("wss://fallback.example.com/live",  Priority: 20),
                                    new TransportURL("wss://api1.example.com/live",      Priority: 10, Weight: 60),
                                    new TransportURL("wss://api2.example.com/live",      Priority: 10, Weight: 40)
                                ]
                            );

            // Whatever the draw does inside a priority, the fallback comes last,
            // every time.
            for (var attempt = 0; attempt < 50; attempt++)
            {

                var endpoints = LiveLinkEndpoints.InPreferenceOrder(transport, new Random(attempt));

                Assert.That(endpoints,     Has.Count.EqualTo(3));
                Assert.That(endpoints[^1], Is.EqualTo("wss://fallback.example.com/live"));
                Assert.That(endpoints[0],  Is.Not.EqualTo("wss://fallback.example.com/live"));

            }

        }

        #endregion

        #region TheWeightsDecideHowOftenAHostIsTriedFirst()

        /// <summary>
        /// Within one priority the weights are a split of the load, so they have
        /// to be drawn rather than sorted.
        ///
        /// A client that simply took the heaviest endpoint first would send every
        /// driver to the same host and leave the weights meaning nothing — the
        /// one failure mode this test exists to catch. The bounds are wide enough
        /// that a fair draw will not trip them and narrow enough that a sort
        /// will.
        /// </summary>
        [Test]
        public void TheWeightsDecideHowOftenAHostIsTriedFirst()
        {

            var transport = new Transport(
                                TransportType.WebSocket,
                                URLs: [
                                    new TransportURL("wss://api1.example.com/live", Priority: 10, Weight: 60),
                                    new TransportURL("wss://api2.example.com/live", Priority: 10, Weight: 40)
                                ]
                            );

            var random  = new Random(20260816);
            var firsts  = new Dictionary<String, Int32>();

            for (var attempt = 0; attempt < 2000; attempt++)
            {
                var first = LiveLinkEndpoints.InPreferenceOrder(transport, random)[0];
                firsts[first] = firsts.GetValueOrDefault(first) + 1;
            }

            Assert.Multiple(() => {
                Assert.That(firsts["wss://api1.example.com/live"], Is.InRange(1000, 1400), "the 60-weight host");
                Assert.That(firsts["wss://api2.example.com/live"], Is.InRange( 600, 1000), "the 40-weight host");
            });

        }

        #endregion

        #region UnweightedEndpointsShareTheLoadEvenly()

        /// <summary>
        /// Endpoints that state no weight expressed no preference, so none is
        /// invented for them.
        /// </summary>
        [Test]
        public void UnweightedEndpointsShareTheLoadEvenly()
        {

            var transport = new Transport(
                                TransportType.HTTPSSE,
                                URLs: [
                                    new TransportURL("https://api1.example.com/live"),
                                    new TransportURL("https://api2.example.com/live")
                                ]
                            );

            var random  = new Random(20260816);
            var firsts  = new Dictionary<String, Int32>();

            for (var attempt = 0; attempt < 2000; attempt++)
            {
                var first = LiveLinkEndpoints.InPreferenceOrder(transport, random)[0];
                firsts[first] = firsts.GetValueOrDefault(first) + 1;
            }

            Assert.Multiple(() => {
                Assert.That(firsts["https://api1.example.com/live"], Is.InRange(800, 1200));
                Assert.That(firsts["https://api2.example.com/live"], Is.InRange(800, 1200));
            });

        }

        #endregion

        #region TheFixtureIsOrderedTheWayItReads()

        /// <summary>
        /// The live link fixture, ordered: two hosts sharing the load and a third
        /// one behind them.
        /// </summary>
        [Test]
        public async Task TheFixtureIsOrderedTheWayItReads()
        {

            var result = await VerifyFixtures([ "ChargeTransparencyLive/ChargeTransparencyLiveLink_1.json" ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyLiveLink>(), VerificationReport.Format(result));

            var endpoints = LiveLinkEndpoints.InPreferenceOrder(
                                ((ChargeTransparencyLiveLink) result).Transports[1],
                                new Random(1)
                            );

            Assert.Multiple(() => {
                Assert.That(endpoints,             Has.Count.EqualTo(3));
                Assert.That(endpoints.Take(2),     Is.EquivalentTo(new[] {
                                                       "wss://api1.example.com/chargingSessions/1234567890/transparency/live",
                                                       "wss://api2.example.com/chargingSessions/1234567890/transparency/live"
                                                   }));
                Assert.That(endpoints[2],          Is.EqualTo("wss://api3.example.com/chargingSessions/1234567890/transparency/live"));
            });

        }

        #endregion


        #region APlaceholderIsWhereThePasswordGoes()

        /// <summary>
        /// A templated URL says where the one-time password belongs, and that is
        /// where it goes.
        /// </summary>
        [Test]
        public void APlaceholderIsWhereThePasswordGoes()
        {

            var totp      = new TOTPConfig("secure!Charging!", 30);
            var timestamp = new DateTimeOffset(2024, 5, 23, 0, 23, 5, TimeSpan.Zero);

            Assert.Multiple(() => {

                Assert.That(
                    LiveLinkEndpoints.ResolveTOTPPlaceholder("https://api.example.com/{totp}/live", totp, timestamp),
                    Is.EqualTo("https://api.example.com/CN63y502maVh/live")
                );

                Assert.That(
                    LiveLinkEndpoints.ResolveTOTPPlaceholder("https://api.example.com/live?t={totp}", totp, timestamp),
                    Is.EqualTo("https://api.example.com/live?t=CN63y502maVh")
                );

            });

        }

        #endregion

        #region WithoutAPlaceholderTheAddressIsLeftForTheHeaderToCarryIt()

        /// <summary>
        /// An address with no place for the password keeps its address, because
        /// the password travels in the "TOTP" request header instead.
        ///
        /// The placeholder exists for QR codes, where the address is all there
        /// is. A live link is fetched with a request, and a request has headers —
        /// so nothing has to be smuggled into the URL, where it would end up in
        /// every server log along the way.
        /// </summary>
        [Test]
        public void WithoutAPlaceholderTheAddressIsLeftForTheHeaderToCarryIt()
        {

            var totp      = new TOTPConfig("secure!Charging!", 30);
            var timestamp = new DateTimeOffset(2024, 5, 23, 0, 23, 5, TimeSpan.Zero);

            Assert.Multiple(() => {

                Assert.That(
                    LiveLinkEndpoints.ResolveTOTPPlaceholder("https://api.example.com/live", totp, timestamp),
                    Is.EqualTo("https://api.example.com/live")
                );

                Assert.That(
                    LiveLinkEndpoints.ResolveTOTPPlaceholder("https://api.example.com/live?token=abcdef", totp, timestamp),
                    Is.EqualTo("https://api.example.com/live?token=abcdef")
                );

            });

        }

        #endregion

        #region TheLiveLinkSecretBecomesTheOneHermodSends()

        /// <summary>
        /// A transport's one-time password configuration, translated into the
        /// shape Hermod takes it in — which is all that is needed for its clients
        /// to send the current password with every request.
        /// </summary>
        [Test]
        public void TheLiveLinkSecretBecomesTheOneHermodSends()
        {

            var hermod = LiveLinkEndpoints.ToHermodTOTPConfig(new TOTPConfig("abcdefghijklmnopqrstuvwxyz1234567890", 10));

            Assert.Multiple(() => {
                Assert.That(hermod?.SharedSecret,  Is.EqualTo("abcdefghijklmnopqrstuvwxyz1234567890"));
                Assert.That(hermod?.ValidityTime,  Is.EqualTo(TimeSpan.FromSeconds(10)));

                // No configuration is no password, not a default one.
                Assert.That(LiveLinkEndpoints.ToHermodTOTPConfig(null),  Is.Null);
            });

        }

        #endregion

        #region WithoutASharedSecretTheAddressIsLeftAlone()

        /// <summary>
        /// A transport with no one-time password configuration is reached at the
        /// address it stated, unchanged.
        /// </summary>
        [Test]
        public void WithoutASharedSecretTheAddressIsLeftAlone()

            => Assert.That(
                   LiveLinkEndpoints.ResolveTOTPPlaceholder("https://api.example.com/live?token=abcdef", null),
                   Is.EqualTo("https://api.example.com/live?token=abcdef")
               );

        #endregion

    }

}
