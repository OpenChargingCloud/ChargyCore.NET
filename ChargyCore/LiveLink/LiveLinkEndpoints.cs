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


        #region LiteralHeaders(Transport)

        /// <summary>
        /// The header names this client never takes from a document: what the
        /// HTTP and WebSocket machinery writes itself, and the one-time password,
        /// which is computed per request.
        /// </summary>
        private static readonly HashSet<String> reservedHeaders = new (StringComparer.OrdinalIgnoreCase) {
            "Host", "Content-Length", "Transfer-Encoding", "Connection", "Upgrade", "Keep-Alive", "TE", "Trailer",
            "Sec-WebSocket-Key", "Sec-WebSocket-Version", "Sec-WebSocket-Accept", "Sec-WebSocket-Extensions", "Sec-WebSocket-Protocol",
            "TOTP"
        };

        /// <summary>
        /// The custom headers of a transport this client sends: those the document
        /// states as a literal value.
        ///
        /// A value a provider computes per request is not sent: the format defines
        /// the shape of a provider, not what any provider means, and a client that
        /// does not know one sends no header for it rather than the description of
        /// one. A header that is not a valid HTTP field — a name that is not a
        /// token, a value with a line break in it — or that the request machinery
        /// owns is not sent either: a document says what it wants sent, it does
        /// not get to write the request.
        /// </summary>
        /// <param name="Transport">A transport of a charge transparency live link.</param>
        public static IEnumerable<KeyValuePair<String, String>> LiteralHeaders(Transport Transport)
        {

            foreach (var (name, value) in Transport.CustomHeaders)
                if (value.Literal is String literal                           &&
                    name.Length > 0                                           &&
                    name.All(character => character < 128 && (Char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character))) &&
                   !literal.Any(character => character is '\r' or '\n' or '\0') &&
                   !reservedHeaders.Contains(name))
                {
                    yield return new KeyValuePair<String, String>(name, literal);
                }

        }

        #endregion

        #region ToHermodTOTPConfig(TOTP)

        /// <summary>
        /// What a live link says about its one-time password, in the shape Hermod
        /// takes it in, or null when there is none — or none this library can
        /// compute.
        ///
        /// Handing this to an HTTP or WebSocket client is all that is needed: the
        /// client generates the current password for every request and sends it
        /// in the "TOTP" header. What the live link leaves out stays at what both
        /// sides default to. The period is its "validityTime" where it states one
        /// and its "timeStep" otherwise, which is the name the format used first.
        ///
        /// A hash algorithm Hermod does not know makes the configuration one
        /// nobody here can compute, rather than one computed with the default
        /// algorithm: a password made with the wrong hash is refused by the
        /// station all the same, only later and with less of a reason.
        /// </summary>
        /// <param name="TOTP">The one-time password configuration of a transport.</param>
        public static HermodTOTPConfig? ToHermodTOTPConfig(TOTPConfig? TOTP)
        {

            if (TOTP is null)
                return null;

            TOTPHashAlgorithm? hashAlgorithm = null;

            if (TOTP.HashAlgorithm is String hashAlgorithmName)
            {

                if (!TOTPHashAlgorithmExtensions.TryParse(hashAlgorithmName, out var parsedHashAlgorithm))
                    return null;

                hashAlgorithm = parsedHashAlgorithm;

            }

            return new HermodTOTPConfig(
                       TOTP.SharedSecret,
                       (TOTP.ValidityTime ?? TOTP.TimeStep) is Double seconds
                           ? TimeSpan.FromSeconds(seconds)
                           : null,
                       TOTP.TOTPLength is Int32 length && length > 0
                           ? (UInt32) length
                           : null,
                       TOTP.Alphabet,
                       HashAlgorithm: hashAlgorithm
                   );

        }

        #endregion

        #region (private, static) Generate(Config, Timestamp)

        /// <summary>
        /// The passwords of a configuration for the given moment.
        /// </summary>
        private static (String Previous, String Current, String Next, TimeSpan RemainingTime, DateTimeOffset EndTime)

            Generate(HermodTOTPConfig  Config,
                     DateTimeOffset?   Timestamp)

            => HermodTOTPGenerator.GenerateTOTPs(
                   Config.SharedSecret,
                   Config.ValidityTime,
                   Config.Length ?? 12,
                   Config.Alphabet,
                   TOTPTimestamp: Timestamp,
                   HashAlgorithm: Config.HashAlgorithm
               );

        #endregion

        #region CurrentTOTPHeader(TOTP, Timestamp = null)

        /// <summary>
        /// The value of the "TOTP" request header for this moment, or null when
        /// the transport is not protected by a one-time password this library can
        /// compute.
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

            if (ToHermodTOTPConfig(TOTP) is not HermodTOTPConfig config)
                return null;

            return new TOTPHTTPHeader(
                       TOTPHTTPHeaderType.RAW,
                       Generate(config, Timestamp).Current
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

            if (ToHermodTOTPConfig(TOTP) is not HermodTOTPConfig config)
                return URL;

            var totps = Generate(config, Timestamp);

            return URL.Replace(TOTPPlaceholder, Uri.EscapeDataString(totps.Current), StringComparison.Ordinal);

        }

        #endregion

    }

}
