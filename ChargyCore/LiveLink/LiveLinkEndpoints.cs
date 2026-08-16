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

using org.GraphDefined.Vanaheimr.Hermod;

// Hermod's TOTP configuration and Chargy's live link TOTP configuration share
// the name, and deliberately: they describe the same thing from two sides.
using HermodTOTPConfig     = org.GraphDefined.Vanaheimr.Hermod.TOTPConfig;
using HermodTOTPGenerator  = org.GraphDefined.Vanaheimr.Hermod.TOTPGenerator;

#endregion

namespace cloud.charging.open.chargy.LiveLink
{

    /// <summary>
    /// Which of a transport's addresses to try, in which order, and how the
    /// one-time password a transport is protected by reaches the charging station.
    ///
    /// Note that nothing here computes a one-time password. Hermod already
    /// implements the scheme — the same one as the Dynamic QR-Code reference
    /// implementations, built for the EU Alternative Fuels Infrastructure
    /// Regulation and adopted into OCPP v2.1 — and already knows how to put the
    /// current password into a request. A second implementation of one algorithm
    /// inside one dependency chain is exactly the drift that locks drivers out
    /// of their own charging session, so this only translates what a live link
    /// states into what Hermod expects.
    /// </summary>
    public static class LiveLinkEndpoints
    {

        #region Data

        /// <summary>
        /// The placeholder a live link URL may carry for the current one-time
        /// password, as the Dynamic QR-Code reference implementations write it.
        /// </summary>
        public const String TOTPPlaceholder = "{totp}";

        #endregion


        #region InPreferenceOrder(Transport, Random = null)

        /// <summary>
        /// The addresses of a transport, in the order a client should try them.
        ///
        /// The rule is the one DNS uses for service records, and the fields are
        /// named after it: the lower priority number goes first, and addresses
        /// sharing a priority are drawn against each other in proportion to their
        /// weight. That gives an operator both things they need out of one list —
        /// a fallback that is only used when the preferred hosts are unreachable,
        /// and a split of the load across the hosts at the same level.
        ///
        /// Drawing rather than sorting matters: a client that always tried the
        /// heaviest endpoint first would send every driver to one host and leave
        /// the weights meaning nothing.
        /// </summary>
        /// <param name="Transport">A transport of a charge transparency live link.</param>
        /// <param name="Random">The source of randomness for the weighted draw; a fresh one when not given.</param>
        public static IReadOnlyList<String> InPreferenceOrder(Transport  Transport,
                                                              Random?    Random = null)
        {

            var random     = Random ?? new Random();
            var endpoints  = new List<String>();

            // A transport that names one URL outright has named its endpoint, and
            // that is where to go first. The list is the elaborate form of the
            // same statement, so it follows.
            if (Transport.URL is String url && url.Length > 0)
                endpoints.Add(url);

            foreach (var group in Transport.URLs.GroupBy (transportURL => transportURL.Priority ?? Int32.MaxValue).
                                                 OrderBy (group        => group.Key))
            {

                var remaining = group.ToList();

                while (remaining.Count > 0)
                {

                    var total = remaining.Sum(transportURL => (Int64) (transportURL.Weight ?? 0));

                    // Everything unweighted: no preference was expressed, so
                    // spread the load evenly rather than inventing one.
                    var chosen = total <= 0
                                     ? remaining[random.Next(remaining.Count)]
                                     : Draw(remaining, random.NextInt64(total + 1));

                    endpoints.Add(chosen.URL);
                    remaining.Remove(chosen);

                }

            }

            return endpoints;

        }

        #endregion

        #region (private, static) Draw(Endpoints, Target)

        /// <summary>
        /// The endpoint the running sum of weights reaches at the drawn number.
        /// </summary>
        /// <param name="Endpoints">The endpoints still to be ordered.</param>
        /// <param name="Target">A number drawn between zero and the total weight.</param>
        private static TransportURL Draw(IReadOnlyList<TransportURL>  Endpoints,
                                         Int64                        Target)
        {

            var running = 0L;

            foreach (var endpoint in Endpoints)
            {

                running += endpoint.Weight ?? 0;

                if (running >= Target)
                    return endpoint;

            }

            return Endpoints[^1];

        }

        #endregion


        #region ToHermodTOTPConfig(TOTP)

        /// <summary>
        /// What a live link says about its one-time password, in the shape Hermod
        /// takes it in.
        ///
        /// Handing this to an HTTP or WebSocket client is all that is needed: the
        /// client generates the current password for every request and sends it
        /// in the "TOTP" header. The live link states only the secret and the
        /// time step, so the length and the alphabet stay at what both sides
        /// default to.
        ///
        /// The field is called "initialSharedSecret", which suggests a secret
        /// that is meant to change over time. Nothing in the live link format
        /// says what it would change into or when, so it is used as the shared
        /// secret — and named here, rather than silently, because the day that
        /// evolution is specified this is the line that has to change.
        /// </summary>
        /// <param name="TOTP">The one-time password configuration of a transport.</param>
        public static HermodTOTPConfig? ToHermodTOTPConfig(TOTPConfig? TOTP)

            => TOTP is null
                   ? null
                   : new HermodTOTPConfig(
                         TOTP.InitialSharedSecret,
                         TimeSpan.FromSeconds(TOTP.TimeStep)
                     );

        #endregion

        #region CurrentTOTPHeader(TOTP, Timestamp = null)

        /// <summary>
        /// The value of the "TOTP" request header for this moment, or null when
        /// the transport is not protected by a one-time password.
        ///
        /// Hermod's own clients build this themselves from a
        /// <see cref="HermodTOTPConfig"/>; this exists for the transports that do
        /// not go through one. The format is Hermod's — a type digit, a space and
        /// the password — and it is produced by Hermod's own type here, so that a
        /// charging station reading it never has to care which of the two sent
        /// the request.
        /// </summary>
        /// <param name="TOTP">The one-time password configuration of a transport.</param>
        /// <param name="Timestamp">The moment to compute the password for; now, when not given.</param>
        public static String? CurrentTOTPHeader(TOTPConfig?      TOTP,
                                                DateTimeOffset?  Timestamp = null)
        {

            if (TOTP is null)
                return null;

            var totps = HermodTOTPGenerator.GenerateTOTPs(
                            TOTP.InitialSharedSecret,
                            TimeSpan.FromSeconds(TOTP.TimeStep),
                            TOTPTimestamp: Timestamp
                        );

            return new TOTPHTTPHeader(
                       TOTPHTTPHeaderType.RAW,
                       totps.Current
                   ).ToString();

        }

        #endregion

        #region ResolveTOTPPlaceholder(URL, TOTP, Timestamp = null)

        /// <summary>
        /// Put the current one-time password into an address that has a place for
        /// it, and leave every other address alone.
        ///
        /// A URL carrying <see cref="TOTPPlaceholder"/> is the convention of the
        /// Dynamic QR-Code reference implementations, where the password has to
        /// travel inside the address because the address is all a QR code can
        /// carry. A live link is not a QR code — a request has headers — so an
        /// address without the placeholder is left as it is and the password
        /// travels in the "TOTP" header instead, which is where Hermod puts it.
        /// </summary>
        /// <param name="URL">The address of an endpoint.</param>
        /// <param name="TOTP">The one-time password configuration, when the transport has one.</param>
        /// <param name="Timestamp">The moment to compute the password for; now, when not given.</param>
        public static String ResolveTOTPPlaceholder(String           URL,
                                                    TOTPConfig?      TOTP,
                                                    DateTimeOffset?  Timestamp = null)
        {

            if (TOTP is null ||
                !URL.Contains(TOTPPlaceholder, StringComparison.Ordinal))
            {
                return URL;
            }

            var totps = HermodTOTPGenerator.GenerateTOTPs(
                            TOTP.InitialSharedSecret,
                            TimeSpan.FromSeconds(TOTP.TimeStep),
                            TOTPTimestamp: Timestamp
                        );

            return URL.Replace(TOTPPlaceholder, Uri.EscapeDataString(totps.Current), StringComparison.Ordinal);

        }

        #endregion

    }

}
