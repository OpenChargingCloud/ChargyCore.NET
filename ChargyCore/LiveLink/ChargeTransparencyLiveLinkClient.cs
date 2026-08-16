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

using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

using cloud.charging.open.chargy.IO;

using HermodURL = org.GraphDefined.Vanaheimr.Hermod.HTTP.URL;

#endregion

namespace cloud.charging.open.chargy.LiveLink
{

    /// <summary>
    /// One thing a charging station said while charging was going on.
    /// </summary>
    /// <param name="Timestamp">When this arrived.</param>
    /// <param name="Transport">How it arrived.</param>
    /// <param name="Endpoint">Where it came from.</param>
    /// <param name="Result">
    /// What it turned out to be: a verified charge transparency record, or
    /// whatever else the detector made of it.
    /// </param>
    public class LiveLinkUpdate(DateTimeOffset  Timestamp,
                                TransportType   Transport,
                                String          Endpoint,
                                Object          Result)
    {

        #region Properties

        /// <summary>When this arrived.</summary>
        public DateTimeOffset  Timestamp    { get; } = Timestamp;

        /// <summary>How it arrived.</summary>
        public TransportType   Transport    { get; } = Transport;

        /// <summary>Where it came from.</summary>
        public String          Endpoint     { get; } = Endpoint;

        /// <summary>What it turned out to be.</summary>
        public Object          Result       { get; } = Result;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this update.
        /// </summary>
        public override String ToString()

            => $"{Timestamp:O} {Transport.AsText()} {Endpoint}: {Result.GetType().Name}";

        #endregion

    }


    /// <summary>
    /// Watches a charging session while it is happening.
    ///
    /// A charge transparency file is written after the fact; a live link is the
    /// same evidence while the car is still plugged in. That is worth having —
    /// an EV driver who can see the meter's own signed readings rise has been
    /// told something no display on the station can tell them — and it is the
    /// first thing in this library that opens a network connection on their
    /// behalf, so nothing here happens unless an application asks for it.
    ///
    /// Every update goes through the same pipeline as a file. What arrives over a
    /// WebSocket is not more trustworthy for having arrived quickly: it is signed
    /// or it is not, and the signature is checked the same way either way.
    ///
    /// This has no counterpart in ChargyCore.TS, which declares the transports and
    /// implements none of them.
    /// </summary>
    /// <param name="Detector">The pipeline every update is verified through.</param>
    /// <param name="PollingInterval">How often to ask again, for the transport that has to poll.</param>
    /// <param name="Timeout">How long to wait for a connection or an answer.</param>
    /// <param name="DNSClient">The DNS client used to look the hosts up.</param>
    /// <param name="RemoteCertificateValidator">How to judge the TLS certificate of a station's service.</param>
    /// <param name="Random">The source of randomness for the weighted endpoint draw.</param>
    public class ChargeTransparencyLiveLinkClient(ContentFormatDetector                                      Detector,
                                                  TimeSpan?                                                  PollingInterval             = null,
                                                  TimeSpan?                                                  Timeout                     = null,
                                                  IDNSClient?                                                DNSClient                   = null,
                                                  RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator  = null,
                                                  Random?                                                    Random                      = null)
    {

        #region Data

        private readonly ContentFormatDetector  detector  = Detector;
        private readonly Random                 random    = Random ?? new Random();

        #endregion

        #region Properties

        /// <summary>How often to ask again, for the transport that has to poll.</summary>
        public TimeSpan  PollingInterval    { get; } = PollingInterval ?? TimeSpan.FromSeconds(10);

        /// <summary>How long to wait for a connection or an answer.</summary>
        public TimeSpan  Timeout            { get; } = Timeout         ?? TimeSpan.FromSeconds(30);

        /// <summary>How to judge the TLS certificate of a station's service.</summary>
        public RemoteTLSServerCertificateValidationHandler<IHTTPClient> RemoteCertificateValidator { get; } =
            RemoteCertificateValidator ?? TLSValidationExtensions.AskTheOS;

        /// <summary>The DNS client used to look the hosts up.</summary>
        public IDNSClient?  DNSClient       { get; } = DNSClient;

        #endregion


        #region Connect(LiveLink, Transports = null, CancellationToken = default)

        /// <summary>
        /// Follow a live link and report everything the charging station sends.
        ///
        /// The transports are tried in the order the live link states them, and
        /// within a transport the endpoints in the order priority and weight put
        /// them. An endpoint that cannot be reached is passed over rather than
        /// reported: that is what a list of several was for.
        /// </summary>
        /// <param name="LiveLink">A charge transparency live link.</param>
        /// <param name="Transports">Which transports to consider, most preferred first; all of them, in the order stated, when not given.</param>
        /// <param name="CancellationToken">A token to stop watching.</param>
        public async IAsyncEnumerable<LiveLinkUpdate> Connect(ChargeTransparencyLiveLink                  LiveLink,
                                                              IEnumerable<TransportType>?                 Transports         = null,
                                                              [EnumeratorCancellation] CancellationToken  CancellationToken  = default)
        {

            var transports = Transports is null
                                 ? LiveLink.Transports
                                 : [.. Transports.Select   (type      => LiveLink.Transports.FirstOrDefault(transport => transport.Type == type)).
                                                  OfType<Transport>()];

            foreach (var transport in transports)
                foreach (var endpoint in LiveLinkEndpoints.InPreferenceOrder(transport, random))
                {

                    var updates = transport.Type switch {
                                      TransportType.HTTPS      => Poll     (transport, endpoint, CancellationToken),
                                      TransportType.HTTPSSE    => Stream   (transport, endpoint, CancellationToken),
                                      TransportType.WebSocket  => Listen   (transport, endpoint, CancellationToken),
                                      _                        => null
                                  };

                    if (updates is null)
                        continue;

                    var connected = false;

                    await foreach (var update in updates.ConfigureAwait(false))
                    {
                        connected = true;
                        yield return update;
                    }

                    // Something answered and then the conversation ended — either
                    // because the caller stopped listening, or because the
                    // charging session did. Neither is a reason to fall through
                    // to the next endpoint and start again.
                    if (connected || CancellationToken.IsCancellationRequested)
                        yield break;

                }

        }

        #endregion


        #region (private) Poll  (Transport, Endpoint, CancellationToken)

        /// <summary>
        /// Ask an endpoint again and again.
        ///
        /// The plain HTTPS transport has no way to be told that something
        /// happened, so it asks. That is the least good of the three and the most
        /// likely to work: it is an ordinary request, and it survives the proxies
        /// and firewalls that a long-lived connection does not.
        /// </summary>
        private async IAsyncEnumerable<LiveLinkUpdate> Poll(Transport                                   Transport,
                                                            String                                      Endpoint,
                                                            [EnumeratorCancellation] CancellationToken  CancellationToken)
        {

            var answered = false;

            while (!CancellationToken.IsCancellationRequested)
            {

                var response = await Request(Transport, Endpoint, null, CancellationToken).ConfigureAwait(false);

                var usable   = response is not null            &&
                               response.HTTPStatusCode.IsSuccessful &&
                               response.HTTPBody is Byte[] body &&
                               body.Length > 0;

                if (usable)
                {
                    answered = true;
                    yield return await Verify(Transport, Endpoint, response!.HTTPBody!, response.ContentType?.ToString(), CancellationToken).ConfigureAwait(false);
                }

                // An address that has never answered usefully is not this
                // station's address, and asking it again forever would leave the
                // other addresses in the list untried. Once it has answered
                // once, a failure is a moment with nothing new rather than a
                // wrong address, and polling carries on.
                else if (!answered)
                    yield break;

                try
                {
                    await Task.Delay(PollingInterval, CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }

            }

        }

        #endregion

        #region (private) Stream(Transport, Endpoint, CancellationToken)

        /// <summary>
        /// Listen to an endpoint that sends events as they happen.
        ///
        /// This is the one transport that does not go through Hermod, and not by
        /// preference. Hermod's HTTP client does not yet hand back a live body
        /// for an event stream: a chunked response is consumed before the call
        /// returns, so a stream that never ends never returns, and a
        /// close-delimited one comes back with its socket already disposed.
        /// Either way a charging session could not be watched, so this uses the
        /// runtime's own client, which streams both correctly. Reported to
        /// Hermod; when it can do this, the three transports become one stack
        /// again.
        ///
        /// The one-time password is put into the request here rather than by the
        /// client, in exactly the header and format Hermod would have used.
        /// </summary>
        private async IAsyncEnumerable<LiveLinkUpdate> Stream(Transport                                   Transport,
                                                              String                                      Endpoint,
                                                              [EnumeratorCancellation] CancellationToken  CancellationToken)
        {

            using var httpClient = new HttpClient(
                                       new HttpClientHandler {
                                           ServerCertificateCustomValidationCallback = null
                                       }
                                   ) {
                                       // The stream is the answer, and it lasts as
                                       // long as the charging session does. Only
                                       // establishing it is given a deadline.
                                       Timeout = System.Threading.Timeout.InfiniteTimeSpan
                                   };

            using var request = new HttpRequestMessage(
                                    HttpMethod.Get,
                                    LiveLinkEndpoints.ResolveTOTPPlaceholder(Endpoint, Transport.TOTP)
                                );

            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

            if (LiveLinkEndpoints.CurrentTOTPHeader(Transport.TOTP) is String totp)
                request.Headers.TryAddWithoutValidation("TOTP", totp);

            HttpResponseMessage? response = null;

            try
            {

                using var connecting = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                connecting.CancelAfter(Timeout);

                response = await httpClient.SendAsync(
                                     request,
                                     HttpCompletionOption.ResponseHeadersRead,
                                     connecting.Token
                                 ).ConfigureAwait(false);

            }
            catch (Exception)
            {
                // Unreachable: the caller moves on to the next endpoint.
            }

            if (response is null || !response.IsSuccessStatusCode)
            {
                response?.Dispose();
                yield break;
            }

            try
            {

                var stream = await response.Content.ReadAsStreamAsync(CancellationToken).ConfigureAwait(false);

                await foreach (var serverSentEvent in ServerSentEvents.Read(stream, CancellationToken).ConfigureAwait(false))
                {

                    if (serverSentEvent.Data.Length == 0)
                        continue;

                    yield return await Verify(
                                           Transport,
                                           Endpoint,
                                           Encoding.UTF8.GetBytes(serverSentEvent.Data),
                                           null,
                                           CancellationToken
                                       ).ConfigureAwait(false);

                }

            }
            finally
            {
                response.Dispose();
            }

        }

        #endregion

        #region (private) Listen(Transport, Endpoint, CancellationToken)

        /// <summary>
        /// Listen to an endpoint over a WebSocket.
        ///
        /// The messages arrive on Hermod's own event handlers, so they are handed
        /// into a channel and read out of it here: an async stream is what a
        /// caller can stop, and a callback is not.
        /// </summary>
        private async IAsyncEnumerable<LiveLinkUpdate> Listen(Transport                                   Transport,
                                                              String                                      Endpoint,
                                                              [EnumeratorCancellation] CancellationToken  CancellationToken)
        {

            var address = LiveLinkEndpoints.ResolveTOTPPlaceholder(Endpoint, Transport.TOTP);

            if (HermodURL.TryParse(address) is not HermodURL remoteURL)
                yield break;

            // Bounded, and the oldest is dropped when it overflows: a client that
            // cannot keep up with a charging station should fall behind on the
            // readings it has already seen rather than grow until the machine
            // gives out.
            var messages = Channel.CreateBounded<Byte[]>(
                               new BoundedChannelOptions(64) {
                                   FullMode = BoundedChannelFullMode.DropOldest
                               }
                           );

            var client = new WebSocketClient(
                             remoteURL,
                             RequestTimeout:  Timeout,
                             ConnectTimeout:  Timeout,
                             TOTPConfig:      LiveLinkEndpoints.ToHermodTOTPConfig(Transport.TOTP),
                             DNSClient:       DNSClient
                         );

            client.OnTextMessageReceived   += (timestamp, webSocketClient, connection, frame, eventTrackingId, textMessage, cancellationToken) => {
                messages.Writer.TryWrite(Encoding.UTF8.GetBytes(textMessage));
                return Task.CompletedTask;
            };

            client.OnBinaryMessageReceived += (timestamp, webSocketClient, connection, frame, eventTrackingId, binaryMessage, cancellationToken) => {
                messages.Writer.TryWrite(binaryMessage);
                return Task.CompletedTask;
            };

            client.OnCloseMessageReceived  += (timestamp, webSocketClient, connection, frame, eventTrackingId, statusCode, reason, cancellationToken) => {
                messages.Writer.TryComplete();
                return Task.CompletedTask;
            };

            try
            {

                try
                {
                    await client.Connect(CancellationToken: CancellationToken).
                                 WaitAsync(Timeout, CancellationToken).
                                 ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Unreachable: the caller moves on to the next endpoint.
                    yield break;
                }

                while (!CancellationToken.IsCancellationRequested)
                {

                    Byte[]? message;

                    try
                    {
                        message = await messages.Reader.ReadAsync(CancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // Cancelled, or the station closed the conversation.
                        yield break;
                    }

                    yield return await Verify(Transport, Endpoint, message, null, CancellationToken).ConfigureAwait(false);

                }

            }
            finally
            {

                try
                {
                    await client.Close().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Nothing left to do about a connection that is already gone.
                }

            }

        }

        #endregion


        #region (private) Request(Transport, Endpoint, Accept, CancellationToken)

        /// <summary>
        /// Ask an endpoint once.
        ///
        /// The one-time password is not put into the address unless the address
        /// has a place for it: Hermod sends the current password in the "TOTP"
        /// header, where it does not end up in every server log along the way.
        /// </summary>
        private async Task<HTTPResponse?> Request(Transport            Transport,
                                                  String               Endpoint,
                                                  HTTPContentType?     Accept,
                                                  CancellationToken    CancellationToken)
        {

            try
            {

                var address = LiveLinkEndpoints.ResolveTOTPPlaceholder(Endpoint, Transport.TOTP);

                if (HermodURL.TryParse(address) is not HermodURL remoteURL)
                    return null;

                var httpClient = new HTTPSClient(
                                     remoteURL,
                                     RemoteCertificateValidator,
                                     ConnectTimeout:  Timeout,
                                     ReceiveTimeout:  Timeout,
                                     TOTPConfig:      LiveLinkEndpoints.ToHermodTOTPConfig(Transport.TOTP),
                                     DNSClient:       DNSClient
                                 );

                return await httpClient.GET(
                                 remoteURL.Path,
                                 QueryString:         remoteURL.QueryString,
                                 Accept:              Accept is not null
                                                          ? AcceptTypes.FromHTTPContentTypes(Accept)
                                                          : null,
                                 RequestTimeout:      Timeout,
                                 CancellationToken:   CancellationToken
                             ).ConfigureAwait(false);

            }
            catch (Exception)
            {
                // Unreachable, or something that is not an HTTP service. Either
                // way this address is not the one, and the caller has others.
                return null;
            }

        }

        #endregion

        #region (private) Verify (Transport, Endpoint, Data, ContentType, CancellationToken)

        /// <summary>
        /// Make sense of what a charging station sent, exactly as if it had been
        /// handed over as a file.
        ///
        /// Arriving over a live connection makes data neither more nor less
        /// trustworthy — it is signed or it is not — so it goes through the same
        /// pipeline, and the same public keys, as everything else.
        /// </summary>
        private async Task<LiveLinkUpdate> Verify(Transport          Transport,
                                                  String             Endpoint,
                                                  Byte[]             Data,
                                                  String?            ContentType,
                                                  CancellationToken  CancellationToken)

            => new (
                   org.GraphDefined.Vanaheimr.Illias.Timestamp.Now,
                   Transport.Type,
                   Endpoint,
                   await detector.DetectAndConvertContentFormat(
                             [ new FileInfo("live", Data, ContentType) ],
                             CancellationToken
                         ).ConfigureAwait(false)
               );

        #endregion

    }

}
