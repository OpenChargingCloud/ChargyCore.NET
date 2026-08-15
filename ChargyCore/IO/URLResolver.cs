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

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

// Hermod's URL and Chargy's SimpleURL parameter share the name "URL".
using HermodURL = org.GraphDefined.Vanaheimr.Hermod.HTTP.URL;

#endregion

namespace cloud.charging.open.chargy.IO
{

    /// <summary>
    /// Asks a URL what it has to offer.
    ///
    /// A QR code on a charging station often holds nothing but a link. Following
    /// it turns a bare address into something an application can act on — but it
    /// also tells the operator that this particular EV driver is looking at this
    /// particular charging session, which is precisely the kind of observation
    /// transparency software should not force on anyone. Resolution is therefore
    /// off unless the application asks for it.
    /// </summary>
    public interface IURLResolver
    {

        /// <summary>
        /// Find out what the given URL serves.
        /// </summary>
        /// <param name="URL">A URL.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        Task<SimpleURL> ResolveURL(SimpleURL          URL,
                                   CancellationToken  CancellationToken = default);

    }


    /// <summary>
    /// Asks a URL what it has to offer, over HTTP.
    /// </summary>
    /// <param name="DNSClient">
    /// The DNS client used to look the host up. Sharing one across an application
    /// is worthwhile, because it caches.
    /// </param>
    /// <param name="RemoteCertificateValidator">
    /// How to judge the TLS certificate of the service. The default asks the
    /// operating system, and a certificate the operating system rejects is not
    /// accepted here either — a link that promises charging data is exactly the
    /// kind of link worth impersonating.
    /// </param>
    /// <param name="Timeout">How long to wait for an answer.</param>
    public class HTTPURLResolver(IDNSClient?                                                DNSClient                   = null,
                                 RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator  = null,
                                 TimeSpan?                                                  Timeout                     = null) : IURLResolver
    {

        #region Properties

        /// <summary>The DNS client used to look the host up.</summary>
        public IDNSClient?                                                DNSClient                     { get; } = DNSClient;

        /// <summary>How to judge the TLS certificate of the service.</summary>
        public RemoteTLSServerCertificateValidationHandler<IHTTPClient>   RemoteCertificateValidator    { get; } = RemoteCertificateValidator
                                                                                                                      ?? TLSValidationExtensions.AskTheOS;

        /// <summary>How long to wait for an answer.</summary>
        public TimeSpan                                                   Timeout                       { get; } = Timeout ?? TimeSpan.FromSeconds(10);

        #endregion


        #region ResolveURL(URL, CancellationToken = default)

        /// <summary>
        /// Find out what the given URL serves.
        ///
        /// Anything short of a well-formed answer leaves the URL as it was: an
        /// unreachable service is a reason to show the EV driver the link, not a
        /// reason to fail their verification.
        /// </summary>
        /// <param name="URL">A URL.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public async Task<SimpleURL> ResolveURL(SimpleURL          URL,
                                                CancellationToken  CancellationToken = default)
        {

            try
            {

                if (HermodURL.TryParse(URL.URL) is not HermodURL remoteURL)
                    return URL;

                var chargy    = HTTPContentType.TryParse(ContentTypes.Chargy, out var contentType)
                                    ? contentType
                                    : null;

                using var httpClient  = new HTTPSClient(
                                            remoteURL,
                                            RemoteCertificateValidator,
                                            ConnectTimeout:  Timeout,
                                            ReceiveTimeout:  Timeout,
                                            DNSClient:       DNSClient
                                        );

                var response = await httpClient.GET(
                                         remoteURL.Path,
                                         Accept:             chargy is not null
                                                                 ? AcceptTypes.FromHTTPContentTypes(chargy)
                                                                 : null,
                                         RequestTimeout:     Timeout,
                                         CancellationToken:  CancellationToken
                                     ).ConfigureAwait(false);

                if (!response.HTTPStatusCode.IsSuccessful)
                    return URL;

                JObject? serviceData = null;

                try
                {
                    if (response.HTTPBodyAsUTF8String is String body && body.Length > 0)
                        serviceData = ChargyLib.ParseJSON(body);
                }
                catch (Exception)
                {
                    // A service may answer with something other than JSON, which
                    // is a perfectly good answer — it simply carries no service data.
                }

                return new SimpleURL(
                           URL.URL,
                           URL.Method,
                           URL.AcceptType,
                           URL.Actions,
                           chargy is not null && response.ContentType == chargy
                               ? [ "chargy" ]
                               : URL.ServiceTypes,
                           serviceData ?? URL.ServiceData
                       );

            }
            catch (Exception)
            {
                return URL;
            }

        }

        #endregion


    }

}
