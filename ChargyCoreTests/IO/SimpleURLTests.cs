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

using Newtonsoft.Json.Linq;

using cloud.charging.open.chargy.IO;
using cloud.charging.open.chargy.qrcodes;

#endregion

namespace cloud.charging.open.chargy.tests.IO
{

    /// <summary>
    /// Tests for the bare URLs printed on charging stations.
    ///
    /// A QR code on a charging station usually holds nothing but a link, and
    /// scanning it yields a pointer to charging data rather than the data itself.
    /// Following that pointer is useful and it is also an observation: it tells
    /// the operator that this particular driver is looking at this particular
    /// charging session, at this moment. So the question these tests circle is
    /// not only "does resolution work" but "does it happen only when asked".
    /// </summary>
    [TestFixture]
    public class SimpleURLTests : AChargyTests
    {

        #region OnlyHTTPAndHTTPSURLsAreFollowed()

        /// <summary>
        /// What counts as a URL worth offering to an EV driver.
        ///
        /// The scheme restriction is the point: this text arrives from a printed
        /// sticker that anybody can replace, and "javascript:" or "file:" URLs
        /// from such a source have no business being handed to an application
        /// that may open them.
        /// </summary>
        [Test]
        public void OnlyHTTPAndHTTPSURLsAreFollowed()

            => Assert.Multiple(() => {

                   Assert.That(SimpleURL.IsValidURL("https://chargy.charging.cloud/charging-session?id=123#details"),  Is.True);
                   Assert.That(SimpleURL.IsValidURL("http://example.com/path"),                                        Is.True);

                   Assert.That(SimpleURL.IsValidURL("chargy.charging.cloud"),                                          Is.False);
                   Assert.That(SimpleURL.IsValidURL("javascript:alert(1)"),                                            Is.False);
                   Assert.That(SimpleURL.IsValidURL("file:///etc/passwd"),                                             Is.False);
                   Assert.That(SimpleURL.IsValidURL("ordinary text"),                                                  Is.False);
                   Assert.That(SimpleURL.IsValidURL(""),                                                               Is.False);

               });

        #endregion

        #region ATextFileHoldingAURLBecomesAChargyURL()

        /// <summary>
        /// A file whose entire content is a URL is a pointer, not a record.
        /// </summary>
        [Test]
        public async Task ATextFileHoldingAURLBecomesAChargyURL()
        {

            var result = await DetectText("https://chargy.charging.cloud/charging-session?id=123#details");

            Assert.That(result, Is.InstanceOf<SimpleURL>(), VerificationReport.Format(result));

            var url = (SimpleURL) result;

            Assert.Multiple(() => {

                Assert.That(url.URL,           Is.EqualTo("https://chargy.charging.cloud/charging-session?id=123#details"));
                Assert.That(url.ServiceTypes,  Is.Empty);
                Assert.That(url.ServiceData,   Is.Null);

                Assert.That(
                    JToken.DeepEquals(
                        url.ToJSON(),
                        new JObject(
                            new JProperty("@context",  SimpleURL.JSONLDContext),
                            new JProperty("url",       "https://chargy.charging.cloud/charging-session?id=123#details")
                        )
                    ),
                    Is.True,
                    url.ToJSON().ToString()
                );

            });

        }

        #endregion

        #region AURLIsRecognisedInAQRCode()

        /// <summary>
        /// The same thing photographed off a charging station.
        ///
        /// This is how a driver actually encounters a link, so the path from a
        /// PNG to a Chargy URL has to work end to end and not only from a text
        /// file somebody typed.
        /// </summary>
        [Test]
        public async Task AURLIsRecognisedInAQRCode()
        {

            var result = await VerifyFixtures([ "SimpleURLs/chargy.charging.cloud_QRCode.png" ]);

            Assert.That(result, Is.InstanceOf<SimpleURL>(), VerificationReport.Format(result));
            Assert.That(((SimpleURL) result).URL, Is.EqualTo("https://chargy.charging.cloud/"));

        }

        #endregion

        #region TheOptionalPropertiesAreReadAndWrittenBack()

        /// <summary>
        /// Everything a resolved URL may carry, read and written back.
        /// </summary>
        [Test]
        public void TheOptionalPropertiesAreReadAndWrittenBack()
        {

            var json = ChargyLib.ParseJSON($$"""
                {
                  "@context":     "{{SimpleURL.JSONLDContext}}",
                  "url":          "https://chargy.charging.cloud/",
                  "method":       "GET",
                  "acceptType":   "application/json",
                  "actions":      [ "open", "copy" ],
                  "serviceTypes": [ "chargy" ],
                  "serviceData":  { "version": 1 }
                }
                """);

            Assert.That(SimpleURL.IsASimpleURL(json),                Is.True);
            Assert.That(SimpleURL.TryParse(json, out var url),       Is.True);
            Assert.That(url,                                         Is.Not.Null);

            Assert.Multiple(() => {

                Assert.That(url!.Method,        Is.EqualTo("GET"));
                Assert.That(url. AcceptType,    Is.EqualTo("application/json"));
                Assert.That(url. Actions,       Is.EqualTo(new[] { "open", "copy" }));
                Assert.That(url. ServiceTypes,  Is.EqualTo(new[] { "chargy" }));
                Assert.That(url. ServiceData?["version"]?.Value<Int32>(),  Is.EqualTo(1));

                Assert.That(JToken.DeepEquals(url.ToJSON(), json), Is.True, url.ToJSON().ToString());

                // A URL that is not one is not a Chargy URL, however well the
                // rest of the document is formed.
                var notAURL = (JObject) json.DeepClone();
                notAURL["url"] = "javascript:alert(1)";
                Assert.That(SimpleURL.IsASimpleURL(notAURL),  Is.False);

                var otherContext = (JObject) json.DeepClone();
                otherContext["@context"] = "https://example.com/other";
                Assert.That(SimpleURL.IsASimpleURL(otherContext),  Is.False);

            });

        }

        #endregion

        #region AURLIsNotContactedUnlessTheApplicationAsksForIt()

        /// <summary>
        /// Nothing is fetched by default, and everything is fetched once a
        /// resolver is supplied.
        ///
        /// Both halves matter. Resolving by default would make merely scanning a
        /// code tell the operator who is looking; never resolving would make the
        /// resolver dead code that quietly stopped being called.
        /// </summary>
        [Test]
        public async Task AURLIsNotContactedUnlessTheApplicationAsksForIt()
        {

            var spy       = new SpyURLResolver();

            var untouched = await DetectText("https://chargy.charging.cloud/service");
            var resolved  = await DetectText("https://chargy.charging.cloud/service", spy);

            Assert.Multiple(() => {

                Assert.That(untouched,        Is.InstanceOf<SimpleURL>());
                Assert.That(resolved,         Is.InstanceOf<SimpleURL>());

                // Two detections, one resolver: asked exactly once, by the run
                // that was given one.
                Assert.That(spy.ResolvedURLs, Has.Count.EqualTo(1));
                Assert.That(spy.ResolvedURLs[0],  Is.EqualTo("https://chargy.charging.cloud/service"));

            });

        }

        #endregion

        #region TheWholeResolutionCanBeReplaced()

        /// <summary>
        /// An application may answer for a URL out of its own knowledge instead
        /// of going online at all — a lookup table of known services, a cache, or
        /// a proxy it trusts more than the link.
        /// </summary>
        [Test]
        public async Task TheWholeResolutionCanBeReplaced()
        {

            var result = await DetectText(
                             "https://chargy.charging.cloud/service",
                             new StaticURLResolver(
                                 "https://chargy.charging.cloud/service",
                                 [ "chargy" ],
                                 new JObject(new JProperty("source", "static lookup"))
                             )
                         );

            Assert.That(result, Is.InstanceOf<SimpleURL>(), VerificationReport.Format(result));

            var url = (SimpleURL) result;

            Assert.Multiple(() => {
                Assert.That(url.URL,           Is.EqualTo("https://chargy.charging.cloud/service"));
                Assert.That(url.ServiceTypes,  Is.EqualTo(new[] { "chargy" }));
                Assert.That(url.ServiceData?["source"]?.Value<String>(),  Is.EqualTo("static lookup"));
            });

        }

        #endregion

        #region AChargyServiceIsRecognisedByWhatItAnswersWith()

        /// <summary>
        /// The HTTP resolver against a real server, over a real socket.
        ///
        /// This is the one part of the live-link work that is new code rather
        /// than a port, so it is worth exercising for real: the request has to
        /// ask for "application/chargy", and a service that answers with that
        /// content type has to come back marked as a Chargy service with its
        /// answer attached. A stub would prove that the interface is wired up and
        /// nothing about whether the request is the right request.
        /// </summary>
        [Test]
        public async Task AChargyServiceIsRecognisedByWhatItAnswersWith()
        {

            using var service = new LocalHTTPService(
                                    ContentTypes.Chargy,
                                    """{ "name": "Chargy service", "version": 1 }"""
                                );

            var resolved = await new HTTPURLResolver(Timeout: TimeSpan.FromSeconds(5)).
                                     ResolveURL(new SimpleURL(service.URL));

            Assert.Multiple(() => {

                Assert.That(service.AcceptHeaders,  Has.Count.EqualTo(1));
                Assert.That(service.AcceptHeaders[0],  Does.Contain(ContentTypes.Chargy),
                            "the resolver did not ask for charge transparency data");

                Assert.That(resolved.ServiceTypes,  Is.EqualTo(new[] { "chargy" }));
                Assert.That(resolved.ServiceData?["name"]?.Value<String>(),     Is.EqualTo("Chargy service"));
                Assert.That(resolved.ServiceData?["version"]?.Value<Int32>(),   Is.EqualTo(1));

            });

        }

        #endregion

        #region AServiceThatIsNotAChargyServiceStaysABareURL()

        /// <summary>
        /// Something answered, and it was not charge transparency data.
        ///
        /// The URL comes back as it went in rather than as a failure: an EV
        /// driver who scanned a code that leads to an ordinary web page should be
        /// shown the link, not an error about their charging session.
        /// </summary>
        [Test]
        public async Task AServiceThatIsNotAChargyServiceStaysABareURL()
        {

            using var service = new LocalHTTPService("text/html", "<html><body>Hello</body></html>");

            var resolved = await new HTTPURLResolver(Timeout: TimeSpan.FromSeconds(5)).
                                     ResolveURL(new SimpleURL(service.URL));

            Assert.Multiple(() => {
                Assert.That(resolved.URL,           Is.EqualTo(service.URL));
                Assert.That(resolved.ServiceTypes,  Is.Empty);
                Assert.That(resolved.ServiceData,   Is.Null);
            });

        }

        #endregion

        #region AnUnreachableServiceLeavesTheURLAsItWas()

        /// <summary>
        /// Nothing answered at all.
        ///
        /// An unreachable service is a reason to show the driver the link, never
        /// a reason to fail their verification — the charging data they came for
        /// may well be in the other files they handed over.
        /// </summary>
        [Test]
        public async Task AnUnreachableServiceLeavesTheURLAsItWas()
        {

            // Port 1 on the loopback interface: nothing listens there, and the
            // attempt stays on this machine.
            var url       = new SimpleURL("http://127.0.0.1:1/service");
            var resolved  = await new HTTPURLResolver(Timeout: TimeSpan.FromSeconds(2)).ResolveURL(url);

            Assert.Multiple(() => {
                Assert.That(resolved.URL,           Is.EqualTo("http://127.0.0.1:1/service"));
                Assert.That(resolved.ServiceTypes,  Is.Empty);
                Assert.That(resolved.ServiceData,   Is.Null);
            });

        }

        #endregion


        #region (private, static) DetectText(Text, URLResolver = null)

        /// <summary>
        /// Run a text file through the whole pipeline, as an application would.
        /// </summary>
        /// <param name="Text">The contents of the file.</param>
        /// <param name="URLResolver">An optional resolver for the URLs found in it.</param>
        private static Task<Object> DetectText(String        Text,
                                               IURLResolver? URLResolver = null)

            => new ContentFormatDetector(
                   I18NDictionary.Default(),
                   ChargeTransparencyFormats.All(I18NDictionary.Default()),
                   new PDFAttachmentExtractor(),
                   new QRCodeDecoder(),
                   URLResolver
               ).DetectAndConvertContentFormat([
                   new FileInfo("url.txt", Encoding.UTF8.GetBytes(Text), "text/plain")
               ]);

        #endregion

        #region (class) SpyURLResolver

        /// <summary>
        /// A resolver that answers nothing and remembers everything it was asked.
        /// </summary>
        private class SpyURLResolver : IURLResolver
        {

            private readonly List<String> resolvedURLs = [];

            /// <summary>The URLs this resolver was asked about.</summary>
            public IReadOnlyList<String> ResolvedURLs
                => resolvedURLs;

            public Task<SimpleURL> ResolveURL(SimpleURL          URL,
                                              CancellationToken  CancellationToken = default)
            {
                resolvedURLs.Add(URL.URL);
                return Task.FromResult(URL);
            }

        }

        #endregion

        #region (class) StaticURLResolver

        /// <summary>
        /// A resolver that answers out of a lookup table instead of going online.
        /// </summary>
        /// <param name="URL">The URL it knows about.</param>
        /// <param name="ServiceTypes">What that URL serves.</param>
        /// <param name="ServiceData">What it says about itself.</param>
        private class StaticURLResolver(String               URL,
                                        IEnumerable<String>  ServiceTypes,
                                        JObject              ServiceData) : IURLResolver
        {

            public Task<SimpleURL> ResolveURL(SimpleURL          URL2,
                                              CancellationToken  CancellationToken = default)

                => Task.FromResult(
                       URL2.URL == URL
                           ? new SimpleURL(URL2.URL, URL2.Method, URL2.AcceptType, URL2.Actions, ServiceTypes, ServiceData)
                           : URL2
                   );

        }

        #endregion

        #region (class) LocalHTTPService

        /// <summary>
        /// A minimal HTTP server on the loopback interface, answering every
        /// request the same way and recording what it was asked for.
        /// </summary>
        /// <param name="ContentType">The content type to answer with.</param>
        /// <param name="Body">The body to answer with.</param>
        private sealed class LocalHTTPService : IDisposable
        {

            #region Data

            private readonly HttpListener   listener;
            private readonly List<String>   acceptHeaders = [];

            #endregion

            #region Properties

            /// <summary>The URL this service answers on.</summary>
            public String                 URL              { get; }

            /// <summary>The "Accept" headers this service was asked with.</summary>
            public IReadOnlyList<String>  AcceptHeaders
            {
                get
                {
                    lock (acceptHeaders)
                        return [.. acceptHeaders];
                }
            }

            #endregion

            #region Constructor(s)

            public LocalHTTPService(String  ContentType,
                                    String  Body)
            {

                var port  = FreePort();

                URL       = $"http://127.0.0.1:{port}/service";

                listener  = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Start();

                _ = Task.Run(async () => {

                    var body = Encoding.UTF8.GetBytes(Body);

                    while (listener.IsListening)
                    {

                        HttpListenerContext context;

                        try
                        {
                            context = await listener.GetContextAsync().ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            // The listener was stopped, which is how this ends.
                            return;
                        }

                        lock (acceptHeaders)
                            acceptHeaders.Add(context.Request.Headers["Accept"] ?? "");

                        context.Response.StatusCode      = 200;
                        context.Response.ContentType     = ContentType;
                        context.Response.ContentLength64 = body.Length;

                        await context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);

                        context.Response.Close();

                    }

                });

            }

            #endregion

            #region (private, static) FreePort()

            /// <summary>
            /// A TCP port nobody is using, asked of the operating system.
            /// </summary>
            private static Int32 FreePort()
            {

                var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);

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
                    listener.Stop();
                    listener.Close();
                }
                catch (Exception)
                {
                    // Nothing left to do about a listener that is already gone.
                }
            }

            #endregion

        }

        #endregion

    }

}
