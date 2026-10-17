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

#endregion

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Tests for the kinds of input Chargy accepts besides a charge transparency
    /// record: files, URLs and live links.
    /// </summary>
    [TestFixture]
    public class InputTypeTests : AChargyTests
    {

        #region Only_http_and_https_URLs_are_accepted(...)

        // A QR code on a charging station is scanned by a phone. A "file:" or
        // "javascript:" URL arriving from an untrusted printed sticker has no
        // business being followed.
        [TestCase("https://chargy.charging.cloud/verify?id=1",  true)]
        [TestCase("http://localhost:8000/ctr.json",             true)]
        [TestCase("file:///etc/passwd",                         false)]
        [TestCase("javascript:alert(1)",                        false)]
        [TestCase("ftp://example.org/ctr.json",                 false)]
        [TestCase("not a url",                                  false)]
        [TestCase("/relative/path",                             false)]
        public void Only_http_and_https_URLs_are_accepted(String URL, Boolean Expected)
        {

            Assert.That(SimpleURL.IsValidURL(URL),  Is.EqualTo(Expected));

        }

        #endregion

        #region A_URL_needs_its_JSONLD_context()

        [Test]
        public void A_URL_needs_its_JSONLD_context()
        {

            var withContext    = JObject.Parse("""
                { "@context": "https://open.charging.cloud/contexts/URL", "url": "https://chargy.charging.cloud/x" }
                """);

            var withoutContext = JObject.Parse("""
                { "url": "https://chargy.charging.cloud/x" }
                """);

            Assert.Multiple(() => {
                Assert.That(SimpleURL.TryParse(withContext,    out var url),  Is.True);
                Assert.That(url!.URL,  Is.EqualTo("https://chargy.charging.cloud/x"));
                Assert.That(SimpleURL.TryParse(withoutContext, out _),        Is.False);
            });

        }

        #endregion

        #region A_URL_round_trips_through_JSON()

        [Test]
        public void A_URL_round_trips_through_JSON()
        {

            var original = new SimpleURL(
                               "https://chargy.charging.cloud/verify",
                               Method:        "POST",
                               AcceptType:    "application/json",
                               Actions:       [ "verify", "download" ],
                               ServiceTypes:  [ "chargy" ]
                           );

            Assert.That(SimpleURL.TryParse(original.ToJSON(), out var roundTrip),  Is.True);

            Assert.Multiple(() => {
                Assert.That(roundTrip!.URL,           Is.EqualTo("https://chargy.charging.cloud/verify"));
                Assert.That(roundTrip. Method,        Is.EqualTo("POST"));
                Assert.That(roundTrip. AcceptType,    Is.EqualTo("application/json"));
                Assert.That(roundTrip. Actions,       Is.EqualTo(new[] { "verify", "download" }));
                Assert.That(roundTrip. ServiceTypes,  Is.EqualTo(new[] { "chargy" }));
            });

        }

        #endregion


        #region A_file_strips_a_UTF8_byte_order_mark()

        [Test]
        public void A_file_strips_a_UTF8_byte_order_mark()
        {

            // A BOM in front of an OCMF payload would otherwise make the format
            // detection fail on the very first character.
            var withBOM = new FileInfo(
                              "OCMF-Testdata-01.ocmf",
                              new Byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("OCMF|{}|{}")).ToArray()
                          );

            var withWhitespace = new FileInfo(
                                     "OCMF-Testdata-01.ocmf",
                                     Encoding.UTF8.GetBytes("\n  OCMF|{}|{}  \n")
                                 );

            Assert.Multiple(() => {
                Assert.That(withBOM.       AsText(),  Is.EqualTo("OCMF|{}|{}"));
                Assert.That(withWhitespace.AsText(),  Is.EqualTo("OCMF|{}|{}"));
            });

        }

        #endregion

        #region A_real_fixture_is_read_as_a_file()

        [Test]
        public void A_real_fixture_is_read_as_a_file()
        {

            var fileInfo = new FileInfo(
                               "OCMF-Testdata-01.ocmf",
                               ReadBinaryFixture("OCMF/OCMF-Testdata-01.ocmf"),
                               MIMETypeOf("OCMF-Testdata-01.ocmf")
                           );

            Assert.Multiple(() => {
                Assert.That(fileInfo.Type,      Is.EqualTo("application/ocmf"));
                Assert.That(fileInfo.AsText(),  Does.StartWith("OCMF|"));
            });

        }

        #endregion


        #region A_live_link_needs_its_context_a_creation_time_and_transports()

        /// <summary>
        /// What makes a document a live link since upstream 0.15.0: its context,
        /// when it was created, and where its updates can be fetched — the three
        /// things every one of them has. Nothing optional decides it.
        /// </summary>
        [Test]
        public void A_live_link_needs_its_context_a_creation_time_and_transports()
        {

            static JObject Document(String Context, Boolean Created = true, Boolean LiveTransports = true)
            {

                var json = new JObject(new JProperty("@context", JToken.Parse(Context)));

                if (Created)
                    json.Add(new JProperty("created",         "2026-08-14T12:00:00.000Z"));

                if (LiveTransports)
                    json.Add(new JProperty("liveTransports",  new JArray()));

                return json;

            }

            const String context = "\"https://open.charging.cloud/contexts/chargeTransparency/live/link/1.0\"";

            Assert.Multiple(() => {

                Assert.That(ChargeTransparencyLiveLink.TryParse(Document(context),                         out _),  Is.True);

                Assert.That(ChargeTransparencyLiveLink.TryParse(Document(context, Created:        false),  out _),  Is.False);
                Assert.That(ChargeTransparencyLiveLink.TryParse(Document(context, LiveTransports: false),  out _),  Is.False);
                Assert.That(ChargeTransparencyLiveLink.TryParse(JObject.Parse("""{ }"""),                  out _),  Is.False);

                // A document may name an extension context next to this one.
                Assert.That(ChargeTransparencyLiveLink.TryParse(Document($"[ \"https://example.com/extension\", {context} ]"),  out _),  Is.True);
                Assert.That(ChargeTransparencyLiveLink.TryParse(Document( "[ \"https://example.com/extension\" ]"),             out _),  Is.False);

            });

        }

        #endregion

        #region A_live_link_reads_all_three_transports()

        [Test]
        public void A_live_link_reads_all_three_transports()
        {

            var json = JObject.Parse("""
                {
                    "@context":    "https://open.charging.cloud/contexts/chargeTransparency/live/link/1.0",
                    "created":     "2026-08-14T12:00:00.000Z",
                    "description": { "en": "Charging station 1" },
                    "geoLocation": { "lat": 50.9, "lng": 11.6 },
                    "connector":   { "standard": "IEC_62196_T2", "powerType": "AC_3_PHASE" },
                    "liveTransports": [
                        { "type": "https",     "urls": [ "https://station.example/live" ], "refresh": 15,
                          "customHeaders": { "X-Key": "abc", "X-TOTP": { "valueProvider": "TOTP", "parameters": { "sharedSecret": "ABC" } } } },
                        { "type": "httpSSE",   "urls": [ "https://station.example/sse", { "url": "https://backup.example/sse", "priority": 10 } ] },
                        { "type": "websocket", "urls": [ "wss://station.example/ws" ],
                          "totp": { "sharedSecret": "ABC", "timeStep": 30, "totpLength": 8, "hashAlgorithm": "SHA-256" } }
                    ]
                }
                """);

            Assert.That(ChargeTransparencyLiveLink.TryParse(json, out var linkOne),  Is.True);
            Assert.That(ChargeTransparencyLiveLink.TryParse(linkOne!.ToJSON(), out var link),  Is.True);

            Assert.Multiple(() => {

                Assert.That(link!.LiveTransports,                  Has.Count.EqualTo(3));
                Assert.That(link. LiveTransports[0].Type,          Is.EqualTo(TransportType.HTTPS));
                Assert.That(link. LiveTransports[1].Type,          Is.EqualTo(TransportType.HTTPSSE));
                Assert.That(link. LiveTransports[2].Type,          Is.EqualTo(TransportType.WebSocket));

                // How often to ask again belongs to https, and only there is it read.
                Assert.That(link. LiveTransports[0].Refresh,       Is.EqualTo(TimeSpan.FromSeconds(15)));
                Assert.That(link. LiveTransports[1].Refresh,       Is.Null);

                // A literal header is sent as it is; a provider computes its value.
                Assert.That(link. LiveTransports[0].CustomHeaders["X-Key"].Literal,         Is.EqualTo("abc"));
                Assert.That(link. LiveTransports[0].CustomHeaders["X-TOTP"].ValueProvider,  Is.EqualTo("TOTP"));

                // A bare URL string and an object with a priority are both endpoints.
                Assert.That(link. LiveTransports[1].URLs,          Has.Count.EqualTo(2));
                Assert.That(link. LiveTransports[1].URLs[0].URL,   Is.EqualTo("https://station.example/sse"));
                Assert.That(link. LiveTransports[1].URLs[1].Priority,  Is.EqualTo(10));

                Assert.That(link. LiveTransports[2].TOTP?.SharedSecret,   Is.EqualTo("ABC"));
                Assert.That(link. LiveTransports[2].TOTP?.TimeStep,       Is.EqualTo(30));
                Assert.That(link. LiveTransports[2].TOTP?.TOTPLength,     Is.EqualTo(8));
                Assert.That(link. LiveTransports[2].TOTP?.HashAlgorithm,  Is.EqualTo("SHA-256"));

                Assert.That(link. Connector?.Standard,                 Is.EqualTo("IEC_62196_T2"));
                Assert.That(link. GeoLocation?.Latitude.Value,         Is.EqualTo(50.9));

            });

        }

        #endregion

        #region An_unreadable_transport_is_dropped_and_the_live_link_kept()

        [Test]
        public void An_unreadable_transport_is_dropped_and_the_live_link_kept()
        {

            // Upstream 0.15.0: a broken transport must not turn the document
            // into an unrecognised one that then fails as an "unknown format".
            // It is dropped where the transports are read, and the others stay.
            var json = JObject.Parse("""
                {
                    "@context":       "https://open.charging.cloud/contexts/chargeTransparency/live/link/1.0",
                    "created":        "2026-08-14T12:00:00.000Z",
                    "liveTransports": [
                        { "type": "carrier-pigeon", "urls": [ "https://station.example/live" ] },
                        { "type": "https",          "urls": [ "https://station.example/live" ], "refresh": "often" },
                        { "type": "https",          "urls": [ "https://station.example/live" ], "customHeaders": { "X-Key": 42 } },
                        { "type": "https",          "urls": [ "https://station.example/live" ] }
                    ]
                }
                """);

            Assert.That(ChargeTransparencyLiveLink.TryParse(json, out var link),  Is.True);
            Assert.That(link!.LiveTransports,                                     Has.Count.EqualTo(1));

        }

        #endregion

        #region The_old_property_names_are_read_by_nobody()

        [Test]
        public void The_old_property_names_are_read_by_nobody()
        {

            // Upstream renamed "timestamp" to "created" and "transports" to
            // "liveTransports" in 0.13.0, "initialSharedSecret" to "sharedSecret"
            // and the endpoints into "urls" alone in 0.15.0 — every time without
            // a fallback. Since the first two are required now, a document still
            // using the old names is not a live link at all.
            var oldNames = JObject.Parse("""
                {
                    "@context":   "https://open.charging.cloud/contexts/chargeTransparency/live/link/1.0",
                    "timestamp":  "2026-08-14T12:00:00.000Z",
                    "transports": [ { "type": "https", "urls": [ "https://station.example/live" ] } ]
                }
                """);

            // A transport with the old TOTP spelling is dropped, and one naming
            // its endpoint in "url" names nothing this reader looks at.
            var oldTransports = JObject.Parse("""
                {
                    "@context":       "https://open.charging.cloud/contexts/chargeTransparency/live/link/1.0",
                    "created":        "2026-08-14T12:00:00.000Z",
                    "liveTransports": [
                        { "type": "websocket", "urls": [ "wss://station.example/ws" ], "totp": { "initialSharedSecret": "ABC", "timeStep": 30 } },
                        { "type": "https",     "url":  "https://station.example/live" }
                    ]
                }
                """);

            Assert.That(ChargeTransparencyLiveLink.TryParse(oldNames,      out _),     Is.False);
            Assert.That(ChargeTransparencyLiveLink.TryParse(oldTransports, out var link),  Is.True);

            Assert.Multiple(() => {
                Assert.That(link!.LiveTransports,          Has.Count.EqualTo(1));
                Assert.That(link. LiveTransports[0].Type,  Is.EqualTo(TransportType.HTTPS));
                Assert.That(link. LiveTransports[0].URL,   Is.Null);
                Assert.That(link. LiveTransports[0].URLs,  Is.Empty);
            });

        }

        #endregion

        #region Live_link_transport_types_use_their_wire_spelling()

        [Test]
        public void Live_link_transport_types_use_their_wire_spelling()
        {

            Assert.Multiple(() => {
                Assert.That(TransportType.HTTPS.    AsText(),  Is.EqualTo("https"));
                Assert.That(TransportType.HTTPSSE.  AsText(),  Is.EqualTo("httpSSE"));
                Assert.That(TransportType.WebSocket.AsText(),  Is.EqualTo("websocket"));
            });

        }

        #endregion


        #region Charging_costs_round_trip_through_JSON()

        [Test]
        public void Charging_costs_round_trip_through_JSON()
        {

            var json = JObject.Parse("""
                {
                    "total":    12.34,
                    "currency": "EUR",
                    "energy":   { "amount": 42.5, "unit": "kWh", "cost": 12.32 },
                    "flat":     { "cost": 0.02 }
                }
                """);

            Assert.That(ChargingCosts.TryParse(json, out var costsOne),  Is.True);
            Assert.That(ChargingCosts.TryParse(costsOne!.ToJSON(), out var costs),  Is.True);

            Assert.Multiple(() => {
                Assert.That(costs!.Total,           Is.EqualTo(12.34m));
                Assert.That(costs. Currency,        Is.EqualTo("EUR"));
                Assert.That(costs. Energy?.Amount,  Is.EqualTo(42.5m));
                Assert.That(costs. Energy?.Unit,    Is.EqualTo("kWh"));
                Assert.That(costs. Flat?.Value,     Is.EqualTo(0.02m));
                Assert.That(costs. Time,            Is.Null);
            });

        }

        #endregion

        #region A_legally_relevant_log_message_keeps_its_code_and_signatures()

        [Test]
        public void A_legally_relevant_log_message_keeps_its_code_and_signatures()
        {

            var json = JObject.Parse("""
                {
                    "timestamp":  "2026-08-14T12:00:00.000Z",
                    "code":       "ClockAdjusted",
                    "text":       { "en": "The meter clock was corrected." },
                    "signatures": [ { "r": "00aa", "s": "00bb" } ]
                }
                """);

            Assert.That(LegallyRelevantLogMessage.TryParse(json, out var messageOne),  Is.True);
            Assert.That(LegallyRelevantLogMessage.TryParse(messageOne!.ToJSON(), out var message),  Is.True);

            Assert.Multiple(() => {
                Assert.That(message!.Code,                Is.EqualTo("ClockAdjusted"));
                Assert.That(message. Signatures,          Has.Count.EqualTo(1));
                Assert.That(message. Signatures[0],       Is.TypeOf<SignatureRS>());
            });

        }

        #endregion


    }

}
