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

using System.Net.Http.Headers;

using Newtonsoft.Json.Linq;

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
    /// <param name="HTTPClient">
    /// An optional HTTP client to use. Sharing one across resolutions is the
    /// caller's business, because a client owns connections.
    /// </param>
    /// <param name="Timeout">How long to wait for an answer.</param>
    public class HTTPURLResolver(HttpClient?  HTTPClient  = null,
                                 TimeSpan?    Timeout     = null) : IURLResolver
    {

        #region Data

        private readonly HttpClient  httpClient  = HTTPClient ?? new HttpClient();

        #endregion

        #region Properties

        /// <summary>How long to wait for an answer.</summary>
        public TimeSpan Timeout    { get; } = Timeout ?? TimeSpan.FromSeconds(10);

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

                using var timeout  = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                timeout.CancelAfter(Timeout);

                using var request  = new HttpRequestMessage(HttpMethod.Get, URL.URL);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ContentTypes.Chargy));

                using var response = await httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return URL;

                var contentType  = ContentTypes.Normalize(response.Content.Headers.ContentType?.ToString());
                var body         = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);

                JObject? serviceData = null;

                try
                {
                    serviceData = JObject.Parse(body);
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
                           contentType == ContentTypes.Chargy
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
