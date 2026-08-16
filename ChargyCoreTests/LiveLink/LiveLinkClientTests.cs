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

using System.Net;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

using cloud.charging.open.chargy.IO;
using cloud.charging.open.chargy.LiveLink;
using cloud.charging.open.chargy.qrcodes;

#endregion

namespace cloud.charging.open.chargy.tests.LiveLink
{

    /// <summary>
    /// Tests for watching a charging session while it is happening.
    ///
    /// Every one of these runs against a real server over a real socket, because
    /// a transport is precisely the part that a stub cannot tell you anything
    /// about: whether the request is the right request, whether the events are
    /// read as they arrive rather than after the stream ends, and whether an
    /// unreachable host is passed over instead of ending the whole attempt.
    ///
    /// The payload throughout is an ordinary chargeIT container — a signed
    /// charging session that verifies on its own. That is the point of the
    /// design: what arrives over a WebSocket is not more trustworthy for having
    /// arrived quickly, so it goes through the same pipeline as a file, and these
    /// tests assert a verified signature at the end of every transport.
    /// </summary>
    [TestFixture]
    public class LiveLinkClientTests : AChargyTests
    {

        #region Data

        /// <summary>A signed charging session, as a charging station would send one.</summary>
        private static String Payload
            => ReadTextFixture("chargeIT/new_container_format/bsm-ws36a-good-new-style-header.json");

        #endregion


        #region PollingDeliversAVerifiedChargingSession()

        /// <summary>
        /// The plain HTTPS transport, which has no way of being told that
        /// something happened and therefore asks again.
        /// </summary>
        [Test]
        public async Task PollingDeliversAVerifiedChargingSession()
        {

            using var service = new LocalHTTPService("application/json", Payload);

            var updates = await Take(
                                    Client(PollingInterval: TimeSpan.FromMilliseconds(50)).Connect(
                                        LiveLinkFor(new Transport(TransportType.HTTPS, service.URL))
                                    ),
                                    2
                                );

            Assert.That(updates, Has.Count.EqualTo(2));

            Assert.Multiple(() => {
                foreach (var update in updates)
                {
                    Assert.That(update.Transport,  Is.EqualTo(TransportType.HTTPS));
                    Assert.That(update.Endpoint,   Is.EqualTo(service.URL));
                    AssertVerified(update);
                }
            });

            // Asked twice, because polling is asking again.
            Assert.That(service.Requests, Is.GreaterThanOrEqualTo(2));

        }

        #endregion

        #region AnEventStreamDeliversEachEventAsItArrives()

        /// <summary>
        /// The server-sent event transport, where the response never ends.
        ///
        /// This is the test that would fail if the body were read to completion
        /// before being parsed: a charging session that is still running has no
        /// end, so a client that waited for one would show an EV driver nothing
        /// at all. The server here deliberately keeps the connection open after
        /// its events.
        /// </summary>
        [Test]
        public async Task AnEventStreamDeliversEachEventAsItArrives()
        {

            using var service = new LocalHTTPService(Payload, EventCount: 3);

            var updates = await Take(
                                    Client().Connect(
                                        LiveLinkFor(new Transport(TransportType.HTTPSSE, service.URL))
                                    ),
                                    3
                                );

            Assert.That(updates, Has.Count.EqualTo(3));

            Assert.Multiple(() => {
                foreach (var update in updates)
                {
                    Assert.That(update.Transport,  Is.EqualTo(TransportType.HTTPSSE));
                    AssertVerified(update);
                }
            });

            // One request, three updates — which is the whole difference between
            // this transport and polling.
            Assert.That(service.Requests, Is.EqualTo(1));

        }

        #endregion

        #region AWebSocketDeliversEveryMessage()

        /// <summary>
        /// The WebSocket transport, where the station speaks whenever it has
        /// something to say.
        /// </summary>
        [Test]
        public async Task AWebSocketDeliversEveryMessage()
        {

            using var service = new LocalWebSocketService(Payload, MessageCount: 3);

            var updates = await Take(
                                    Client().Connect(
                                        LiveLinkFor(new Transport(TransportType.WebSocket, service.URL))
                                    ),
                                    3
                                );

            Assert.That(updates, Has.Count.EqualTo(3));

            Assert.Multiple(() => {
                foreach (var update in updates)
                {
                    Assert.That(update.Transport,  Is.EqualTo(TransportType.WebSocket));
                    AssertVerified(update);
                }
            });

        }

        #endregion

        #region AnUnreachableEndpointIsPassedOver()

        /// <summary>
        /// A charging station that lists three addresses expects a client to try
        /// the next one when the first does not answer.
        ///
        /// That is what a list of several was for, and getting it wrong would
        /// make the fallback addresses decoration.
        /// </summary>
        [Test]
        public async Task AnUnreachableEndpointIsPassedOver()
        {

            using var service = new LocalHTTPService("application/json", Payload);

            // A loopback port the operating system has just handed back, so the
            // connection is refused at once rather than filtered and left to time
            // out. The attempt never leaves this machine either way.
            var deadPort  = FreeLoopbackPort();

            var transport = new Transport(
                                TransportType.HTTPS,
                                URLs: [
                                    new TransportURL($"http://127.0.0.1:{deadPort}/dead",  Priority: 10),
                                    new TransportURL(service.URL,                          Priority: 20)
                                ]
                            );

            var updates = await Take(
                                    Client(
                                        PollingInterval: TimeSpan.FromMilliseconds(50),
                                        // Short, because the point of the test is
                                        // what happens after the first address
                                        // has been given up on.
                                        Timeout:         TimeSpan.FromSeconds(2)
                                    ).Connect(LiveLinkFor(transport)),
                                    1
                                );

            Assert.That(updates,             Has.Count.EqualTo(1));
            Assert.That(updates[0].Endpoint, Is.EqualTo(service.URL));

            AssertVerified(updates[0]);

        }

        #endregion

        #region TheOneTimePasswordTravelsInTheHeader()

        /// <summary>
        /// A transport protected by a one-time password sends the current one
        /// with every request — in the "TOTP" header, not in the address.
        ///
        /// The address is what ends up in every server log along the way, so a
        /// password smuggled into it would outlive the ten seconds it is supposed
        /// to be good for.
        /// </summary>
        [Test]
        public async Task TheOneTimePasswordTravelsInTheHeader()
        {

            using var service = new LocalHTTPService("application/json", Payload);

            var totp      = new TOTPConfig("abcdefghijklmnopqrstuvwxyz1234567890", 10);
            var transport = new Transport(TransportType.HTTPS, service.URL, TOTP: totp);

            await Take(
                      Client(PollingInterval: TimeSpan.FromMilliseconds(50)).Connect(LiveLinkFor(transport)),
                      1
                  );

            var header   = service.Headers.FirstOrDefault(header => header.Key == "TOTP").Value;

            // The station computes the same three passwords for this moment, and
            // accepts any of them: a phone and a charging station never agree on
            // the clock to the second.
            var expected = TOTPGenerator.GenerateTOTPs(totp.InitialSharedSecret, TimeSpan.FromSeconds(totp.TimeStep));

            Assert.That(header, Is.Not.Null, "no TOTP header was sent");

            Assert.Multiple(() => {

                // "0 <password>" — the leading digit says the password is the raw
                // one rather than bound to the TLS channel.
                Assert.That(header,  Does.StartWith("0 "));

                Assert.That(
                    header![2..],
                    Is.AnyOf(expected.Previous, expected.Current, expected.Next)
                );

                // ..., and the address is untouched.
                Assert.That(service.Paths, Has.All.EqualTo("/live"));

            });

        }

        #endregion

        #region AStationThatSaysNothingUsableIsStillReported()

        /// <summary>
        /// Whatever a charging station sends is reported, including when it is
        /// not charge transparency data.
        ///
        /// Reporting it is the honest answer: an application watching a session
        /// needs to know that the station is answering with something it cannot
        /// use, which is a different situation from a station that has gone
        /// quiet.
        /// </summary>
        [Test]
        public async Task AStationThatSaysNothingUsableIsStillReported()
        {

            using var service = new LocalHTTPService("text/plain", "the coffee machine is out of order");

            var updates = await Take(
                                    Client(PollingInterval: TimeSpan.FromMilliseconds(50)).Connect(
                                        LiveLinkFor(new Transport(TransportType.HTTPS, service.URL))
                                    ),
                                    1
                                );

            Assert.That(updates,           Has.Count.EqualTo(1));
            Assert.That(updates[0].Result, Is.InstanceOf<SessionCryptoResult>());

        }

        #endregion

        #region TheTransportsAreTriedInTheOrderTheCallerAsksFor()

        /// <summary>
        /// A live link offering several transports is followed over the one the
        /// application prefers.
        ///
        /// An application knows things the charging station does not: whether it
        /// is behind a proxy that breaks long-lived connections, whether it wants
        /// one reading or a live view, whether it is running on a metered
        /// connection.
        /// </summary>
        [Test]
        public async Task TheTransportsAreTriedInTheOrderTheCallerAsksFor()
        {

            using var polling      = new LocalHTTPService("application/json", Payload);
            using var eventStream  = new LocalHTTPService(Payload, EventCount: 3);

            var liveLink = new ChargeTransparencyLiveLink(
                               Transports: [
                                   new Transport(TransportType.HTTPS,    polling.URL),
                                   new Transport(TransportType.HTTPSSE,  eventStream.URL)
                               ]
                           );

            var updates = await Take(
                                    Client().Connect(liveLink, [ TransportType.HTTPSSE, TransportType.HTTPS ]),
                                    1
                                );

            Assert.Multiple(() => {
                Assert.That(updates[0].Transport,  Is.EqualTo(TransportType.HTTPSSE));
                Assert.That(polling.Requests,      Is.Zero, "the transport the caller did not ask for was contacted");
            });

        }

        #endregion


        #region (private, static) Client / LiveLinkFor / Take / AssertVerified

        /// <summary>
        /// A live link client with the whole verification pipeline behind it.
        /// </summary>
        private static ChargeTransparencyLiveLinkClient Client(TimeSpan?  PollingInterval  = null,
                                                               TimeSpan?  Timeout          = null)
        {

            var i18n = I18NDictionary.Default();

            return new ChargeTransparencyLiveLinkClient(
                       new ContentFormatDetector(
                           i18n,
                           ChargeTransparencyFormats.All(i18n),
                           new PDFAttachmentExtractor(),
                           new QRCodeDecoder()
                       ),
                       PollingInterval,
                       Timeout ?? TimeSpan.FromSeconds(10)
                   );

        }

        /// <summary>A live link offering exactly one transport.</summary>
        private static ChargeTransparencyLiveLink LiveLinkFor(Transport Transport)

            => new (Transports: [ Transport ]);

        /// <summary>A loopback TCP port nobody is using, asked of the operating system.</summary>
        private static Int32 FreeLoopbackPort()
        {

            var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);

            probe.Start();

            var port = ((IPEndPoint) probe.LocalEndpoint).Port;

            probe.Stop();

            return port;

        }

        /// <summary>
        /// The first few updates, and then stop listening — which is what an
        /// application closing a live view does.
        /// </summary>
        private static async Task<IReadOnlyList<LiveLinkUpdate>> Take(IAsyncEnumerable<LiveLinkUpdate>  Updates,
                                                                      Int32                             Count)
        {

            var collected = new List<LiveLinkUpdate>();

            using var stopWatching = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await foreach (var update in Updates.WithCancellation(stopWatching.Token).ConfigureAwait(false))
            {

                collected.Add(update);

                if (collected.Count >= Count)
                    break;

            }

            return collected;

        }

        /// <summary>
        /// Assert that an update is a charging session whose signature holds.
        /// </summary>
        private static void AssertVerified(LiveLinkUpdate Update)
        {

            Assert.That(Update.Result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(Update.Result));

            Assert.That(
                ((ChargeTransparencyRecord) Update.Result).ChargingSessions[0].VerificationResult?.Status,
                Is.EqualTo(SessionVerificationResult.ValidSignature),
                VerificationReport.Format(Update.Result)
            );

        }

        #endregion

        #region (class) LocalHTTPService

        /// <summary>
        /// A charging station's transparency endpoint, on the loopback interface.
        ///
        /// Answers either with a single body, or — when an event count is given —
        /// with a server-sent event stream that stays open after its events, as a
        /// station serving a charging session that has not finished would.
        /// </summary>
        private sealed class LocalHTTPService : IDisposable
        {

            #region Data

            private readonly HttpListener                        listener;
            private readonly CancellationTokenSource             stopped        = new ();
            private readonly List<KeyValuePair<String, String>>  headers        = [];
            private readonly List<String>                        paths          = [];
            private          Int32                               requests;

            #endregion

            #region Properties

            /// <summary>The address this service answers on.</summary>
            public String  URL         { get; }

            /// <summary>How often it was asked.</summary>
            public Int32   Requests
                => Volatile.Read(ref requests);

            /// <summary>The headers it was asked with.</summary>
            public IReadOnlyList<KeyValuePair<String, String>> Headers
            {
                get
                {
                    lock (headers)
                        return [.. headers];
                }
            }

            /// <summary>The paths it was asked for.</summary>
            public IReadOnlyList<String> Paths
            {
                get
                {
                    lock (paths)
                        return [.. paths];
                }
            }

            #endregion

            #region Constructor(s)

            /// <summary>A service answering every request with one body.</summary>
            public LocalHTTPService(String  ContentType,
                                    String  Body)

                : this(ContentType, Body, null)

            { }

            /// <summary>A service answering with a server-sent event stream.</summary>
            public LocalHTTPService(String  Body,
                                    Int32   EventCount)

                : this("text/event-stream", Body, EventCount)

            { }

            private LocalHTTPService(String  ContentType,
                                     String  Body,
                                     Int32?  EventCount)
            {

                var port  = FreePort();

                URL       = $"http://127.0.0.1:{port}/live";

                listener  = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Start();

                _ = Task.Run(async () => {

                    while (listener.IsListening)
                    {

                        HttpListenerContext context;

                        try
                        {
                            context = await listener.GetContextAsync().ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            return;
                        }

                        Interlocked.Increment(ref requests);

                        lock (paths)
                            paths.Add(context.Request.Url?.AbsolutePath ?? "");

                        lock (headers)
                            foreach (var name in context.Request.Headers.AllKeys.OfType<String>())
                                headers.Add(new KeyValuePair<String, String>(name, context.Request.Headers[name] ?? ""));

                        _ = Task.Run(() => Answer(context, ContentType, Body, EventCount));

                    }

                });

            }

            #endregion

            #region (private) Answer(Context, ContentType, Body, EventCount)

            /// <summary>
            /// Answer one request, either at once or as a stream of events.
            /// </summary>
            private async Task Answer(HttpListenerContext  Context,
                                      String               ContentType,
                                      String               Body,
                                      Int32?               EventCount)
            {

                try
                {

                    Context.Response.StatusCode   = 200;
                    Context.Response.ContentType  = ContentType;

                    if (EventCount is null)
                    {

                        var body = Encoding.UTF8.GetBytes(Body);

                        Context.Response.ContentLength64 = body.Length;

                        await Context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);

                        Context.Response.Close();

                        return;

                    }

                    // An event stream: the payload on one "data" line each, and
                    // the connection left open afterwards, because the charging
                    // session it describes has not finished either.
                    Context.Response.SendChunked = true;

                    for (var i = 0; i < EventCount.Value; i++)
                    {

                        var frame = Encoding.UTF8.GetBytes($": keep-alive\ndata: {Body.Replace("\n", "").Replace("\r", "")}\n\n");

                        await Context.Response.OutputStream.WriteAsync(frame).ConfigureAwait(false);
                        await Context.Response.OutputStream.FlushAsync().ConfigureAwait(false);

                    }

                    await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, stopped.Token).ConfigureAwait(false);

                }
                catch (Exception)
                {
                    // The client stopped listening, or this service was disposed.
                }

            }

            #endregion

            #region (private, static) FreePort()

            /// <summary>A TCP port nobody is using, asked of the operating system.</summary>
            private static Int32 FreePort()
            {

                var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);

                probe.Start();

                var port = ((IPEndPoint) probe.LocalEndpoint).Port;

                probe.Stop();

                return port;

            }

            #endregion

            #region Dispose()

            public void Dispose()
            {

                try
                {
                    stopped.Cancel();
                    listener.Stop();
                    listener.Close();
                }
                catch (Exception)
                { }

                stopped.Dispose();

            }

            #endregion

        }

        #endregion

        #region (class) LocalWebSocketService

        /// <summary>
        /// A charging station speaking over a WebSocket, on the loopback
        /// interface, sending its readings as soon as somebody connects.
        /// </summary>
        private sealed class LocalWebSocketService : IDisposable
        {

            #region Data

            private readonly WebSocketServer server;

            #endregion

            #region Properties

            /// <summary>The address this service answers on.</summary>
            public String URL { get; }

            #endregion

            #region Constructor(s)

            public LocalWebSocketService(String  Message,
                                         Int32   MessageCount)
            {

                var port = FreePort();

                URL      = $"ws://127.0.0.1:{port}";

                server   = new WebSocketServer(
                               IPv4Address.Localhost,
                               IPPort.Parse(port),
                               RequireAuthentication:  false,
                               AutoStart:              true
                           );

                server.OnNewWebSocketConnection += async (timestamp, webSocketServer, connection, sharedSubprotocols, selectedSubprotocol, eventTrackingId, cancellationToken) => {

                    for (var i = 0; i < MessageCount; i++)
                        await webSocketServer.SendTextMessage(connection, Message).ConfigureAwait(false);

                };

            }

            #endregion

            #region (private, static) FreePort()

            /// <summary>A TCP port nobody is using, asked of the operating system.</summary>
            private static Int32 FreePort()
            {

                var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);

                probe.Start();

                var port = ((IPEndPoint) probe.LocalEndpoint).Port;

                probe.Stop();

                return port;

            }

            #endregion

            #region Dispose()

            public void Dispose()
            {

                try
                {
                    server.Shutdown().GetAwaiter().GetResult();
                }
                catch (Exception)
                { }

            }

            #endregion

        }

        #endregion

    }

}
