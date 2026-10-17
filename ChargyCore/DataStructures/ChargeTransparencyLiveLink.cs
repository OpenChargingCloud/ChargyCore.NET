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

using org.GraphDefined.Vanaheimr.Aegir;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// How a live link delivers its charge transparency data.
    /// </summary>
    public enum TransportType
    {

        /// <summary>Plain HTTPS requests.</summary>
        HTTPS,

        /// <summary>HTTP Server-Sent Events.</summary>
        HTTPSSE,

        /// <summary>A WebSocket connection.</summary>
        WebSocket

    }


    /// <summary>
    /// Extension methods for live link transport types.
    /// </summary>
    public static class TransportTypeExtensions
    {

        #region TryParse(Text, out TransportType)

        /// <summary>
        /// Try to parse the given text as a transport type.
        /// </summary>
        /// <param name="Text">A text representation of a transport type.</param>
        /// <param name="TransportType">The parsed transport type.</param>
        public static Boolean TryParse(String Text, out TransportType TransportType)
        {

            switch (Text.Trim())
            {

                case "https":
                    TransportType = TransportType.HTTPS;
                    return true;

                case "httpSSE":
                    TransportType = TransportType.HTTPSSE;
                    return true;

                case "websocket":
                    TransportType = TransportType.WebSocket;
                    return true;

                default:
                    TransportType = TransportType.HTTPS;
                    return false;

            }

        }

        #endregion

        #region AsText  (this TransportType)

        /// <summary>
        /// The wire representation of the given transport type.
        /// </summary>
        /// <param name="TransportType">A transport type.</param>
        public static String AsText(this TransportType TransportType)

            => TransportType switch {
                   TransportType.HTTPSSE    => "httpSSE",
                   TransportType.WebSocket  => "websocket",
                   _                        => "https"
               };

        #endregion

    }


    /// <summary>
    /// One endpoint of a live link transport, with its load balancing weights.
    /// </summary>
    /// <param name="URL">The URL of the endpoint.</param>
    /// <param name="Priority">An optional priority; lower values are preferred.</param>
    /// <param name="Weight">An optional weight among endpoints of the same priority.</param>
    public class TransportURL(String   URL,
                              Int32?   Priority  = null,
                              Int32?   Weight    = null)
    {

        #region Properties

        /// <summary>The URL of the endpoint.</summary>
        public String  URL         { get; } = URL;

        /// <summary>An optional priority; lower values are preferred.</summary>
        public Int32?  Priority    { get; } = Priority;

        /// <summary>An optional weight among endpoints of the same priority.</summary>
        public Int32?  Weight      { get; } = Weight;

        #endregion


        #region (static) TryParse(JSON, out TransportURL)

        /// <summary>
        /// Try to parse the given JSON as a transport endpoint, accepting both a
        /// bare URL string and an object with priority and weight.
        /// </summary>
        /// <param name="JSON">A JSON representation of a transport endpoint.</param>
        /// <param name="TransportURL">The parsed transport endpoint.</param>
        public static Boolean TryParse(JToken? JSON, out TransportURL? TransportURL)
        {

            TransportURL = null;

            if (JSON is null)
                return false;

            if (JSON.Type == JTokenType.String)
            {

                var url = JSON.Value<String>()!;

                if (String.IsNullOrWhiteSpace(url))
                    return false;

                TransportURL = new TransportURL(url);
                return true;

            }

            if (JSON is JObject json &&
                json["url"]?.Type == JTokenType.String &&
                IsOptionalNumber(json["priority"])     &&
                IsOptionalNumber(json["weight"]))
            {

                TransportURL = new TransportURL(
                                   json["url"]!.Value<String>()!,
                                   json["priority"] is JValue priority ? (Int32) Math.Round(priority.Value<Double>()) : null,
                                   json["weight"]   is JValue weight   ? (Int32) Math.Round(weight.  Value<Double>()) : null
                               );

                return true;

            }

            return false;

        }

        #endregion

        #region (private, static) IsOptionalNumber(JSON)

        /// <summary>
        /// Whether the given property is absent or a number.
        /// </summary>
        /// <param name="JSON">A property value, if there is one.</param>
        private static Boolean IsOptionalNumber(JToken? JSON)

            => JSON is null ||
               JSON.Type is JTokenType.Integer or JTokenType.Float;

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this transport endpoint.
        /// </summary>
        public JToken ToJSON()
        {

            if (!Priority.HasValue && !Weight.HasValue)
                return new JValue(URL);

            var json = new JObject(
                           new JProperty("url", URL)
                       );

            if (Priority.HasValue)
                json.Add(new JProperty("priority",  Priority.Value));

            if (Weight.  HasValue)
                json.Add(new JProperty("weight",    Weight.  Value));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this transport endpoint.
        /// </summary>
        public override String ToString()

            => URL;

        #endregion


    }


    /// <summary>
    /// A time-based one-time password configuration, used to authenticate against
    /// a live link without a long-lived secret travelling in the QR code.
    ///
    /// The shape is the one the TOTP library takes. Only the secret is required;
    /// everything else falls back to what both sides default to. Upstream 0.15.0
    /// renamed "initialSharedSecret" to "sharedSecret" without a fallback, so a
    /// configuration still spelling it the old way is not one.
    /// </summary>
    /// <param name="SharedSecret">The shared secret.</param>
    /// <param name="TimeStep">The optional length of one time step in seconds.</param>
    /// <param name="ValidityTime">The optional time one password stays valid, in seconds.</param>
    /// <param name="TOTPLength">The optional length of a password.</param>
    /// <param name="Alphabet">The optional alphabet a password is made of.</param>
    /// <param name="Timestamp">An optional timestamp the configuration states.</param>
    /// <param name="HashAlgorithm">The optional HMAC hash algorithm, e.g. "SHA-256".</param>
    public class TOTPConfig(String   SharedSecret,
                            Double?  TimeStep       = null,
                            Double?  ValidityTime   = null,
                            Int32?   TOTPLength     = null,
                            String?  Alphabet       = null,
                            String?  Timestamp      = null,
                            String?  HashAlgorithm  = null)
    {

        #region Properties

        /// <summary>The shared secret.</summary>
        public String   SharedSecret     { get; } = SharedSecret;

        /// <summary>The optional length of one time step in seconds.</summary>
        public Double?  TimeStep         { get; } = TimeStep;

        /// <summary>The optional time one password stays valid, in seconds.</summary>
        public Double?  ValidityTime     { get; } = ValidityTime;

        /// <summary>The optional length of a password.</summary>
        public Int32?   TOTPLength       { get; } = TOTPLength;

        /// <summary>The optional alphabet a password is made of.</summary>
        public String?  Alphabet         { get; } = Alphabet;

        /// <summary>An optional timestamp the configuration states.</summary>
        public String?  Timestamp        { get; } = Timestamp;

        /// <summary>The optional HMAC hash algorithm, e.g. "SHA-256".</summary>
        public String?  HashAlgorithm    { get; } = HashAlgorithm;

        #endregion


        #region (static) TryParse(JSON, out TOTPConfig)

        /// <summary>
        /// Try to parse the given JSON as a TOTP configuration.
        /// </summary>
        /// <param name="JSON">A JSON representation of a TOTP configuration.</param>
        /// <param name="TOTPConfig">The parsed TOTP configuration.</param>
        public static Boolean TryParse(JObject JSON, out TOTPConfig? TOTPConfig)
        {

            TOTPConfig = null;

            static Boolean IsOptional(JToken? JSON, params JTokenType[] Types)
                => JSON is null || Types.Contains(JSON.Type);

            if (JSON["sharedSecret"]?.Type != JTokenType.String                           ||
                !IsOptional(JSON["timeStep"],      JTokenType.Integer, JTokenType.Float) ||
                !IsOptional(JSON["validityTime"],  JTokenType.Integer, JTokenType.Float) ||
                !IsOptional(JSON["totpLength"],    JTokenType.Integer, JTokenType.Float) ||
                !IsOptional(JSON["alphabet"],      JTokenType.String)                    ||
                !IsOptional(JSON["timestamp"],     JTokenType.String)                    ||
                !IsOptional(JSON["hashAlgorithm"], JTokenType.String))
            {
                return false;
            }

            TOTPConfig = new TOTPConfig(
                             JSON["sharedSecret"]!.Value<String>()!,
                             JSON["timeStep"]?.     Value<Double>(),
                             JSON["validityTime"]?. Value<Double>(),
                             JSON["totpLength"] is JValue totpLength ? (Int32) Math.Round(totpLength.Value<Double>()) : null,
                             JSON["alphabet"]?.     Value<String>(),
                             JSON["timestamp"]?.    Value<String>(),
                             JSON["hashAlgorithm"]?.Value<String>()
                         );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this TOTP configuration.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("sharedSecret",  SharedSecret)
                       );

            if (TimeStep.     HasValue)
                json.Add(new JProperty("timeStep",       TimeStep.    Value));

            if (ValidityTime. HasValue)
                json.Add(new JProperty("validityTime",   ValidityTime.Value));

            if (TOTPLength.   HasValue)
                json.Add(new JProperty("totpLength",     TOTPLength.  Value));

            if (Alphabet      is not null)
                json.Add(new JProperty("alphabet",       Alphabet));

            if (Timestamp     is not null)
                json.Add(new JProperty("timestamp",      Timestamp));

            if (HashAlgorithm is not null)
                json.Add(new JProperty("hashAlgorithm",  HashAlgorithm));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this TOTP configuration.
        /// </summary>
        public override String ToString()

            => (ValidityTime ?? TimeStep) is Double seconds
                   ? $"TOTP every {seconds.ToString(CultureInfo.InvariantCulture)}s"
                   :  "TOTP";

        #endregion


    }


    /// <summary>
    /// One value of a transport's custom HTTP headers: either the literal string
    /// to send, or a provider that computes the value per request — a one-time
    /// password, say, which would be stale the moment it was written into a
    /// document.
    ///
    /// The format defines the shape, not the providers: what "TOTP" means needs
    /// an external profile. A client that does not know a provider sends no header
    /// for it rather than the description of one.
    /// </summary>
    public sealed class CustomHeaderValue
    {

        #region Properties

        /// <summary>The literal value to send, or null for a provider.</summary>
        public String?   Literal          { get; }

        /// <summary>The name of the provider computing the value, or null for a literal.</summary>
        public String?   ValueProvider    { get; }

        /// <summary>The optional parameters of the provider.</summary>
        public JObject?  Parameters       { get; }

        /// <summary>Whether the value is computed per request rather than stated.</summary>
        public Boolean   IsProvider
            => ValueProvider is not null;

        #endregion

        #region Constructor(s)

        private CustomHeaderValue(String?   Literal,
                                  String?   ValueProvider,
                                  JObject?  Parameters)
        {
            this.Literal        = Literal;
            this.ValueProvider  = ValueProvider;
            this.Parameters     = Parameters;
        }

        /// <summary>
        /// A literal header value.
        /// </summary>
        /// <param name="Value">The value to send.</param>
        public static CustomHeaderValue FromLiteral(String Value)

            => new (Value, null, null);

        /// <summary>
        /// A header value computed per request.
        /// </summary>
        /// <param name="ValueProvider">The name of the provider.</param>
        /// <param name="Parameters">Its optional parameters.</param>
        public static CustomHeaderValue FromProvider(String    ValueProvider,
                                                     JObject?  Parameters = null)

            => new (null, ValueProvider, Parameters);

        #endregion


        #region (static) TryParse(JSON, out CustomHeaderValue)

        /// <summary>
        /// Try to parse the given JSON as a custom header value.
        /// </summary>
        /// <param name="JSON">A JSON representation of a custom header value.</param>
        /// <param name="CustomHeaderValue">The parsed custom header value.</param>
        public static Boolean TryParse(JToken? JSON, out CustomHeaderValue? CustomHeaderValue)
        {

            CustomHeaderValue = JSON switch {

                                    { Type: JTokenType.String }
                                        => FromLiteral(JSON.Value<String>()!),

                                    JObject provider when provider["valueProvider"]?.Type == JTokenType.String &&
                                                         (provider["parameters"] is null || provider["parameters"] is JObject)
                                        => FromProvider(provider["valueProvider"]!.Value<String>()!, provider["parameters"] as JObject),

                                    _   => null

                                };

            return CustomHeaderValue is not null;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this custom header value.
        /// </summary>
        public JToken ToJSON()
        {

            if (ValueProvider is null)
                return new JValue(Literal);

            var json = new JObject(
                           new JProperty("valueProvider",  ValueProvider)
                       );

            if (Parameters is not null)
                json.Add(new JProperty("parameters",  Parameters.DeepClone()));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this custom header value.
        /// </summary>
        public override String ToString()

            => Literal ?? $"<{ValueProvider}>";

        #endregion

    }


    /// <summary>
    /// One way of reaching the live data of a charging station.
    /// </summary>
    /// <param name="Type">How this transport delivers its data.</param>
    /// <param name="URL">An optional single endpoint, for a transport built in code.</param>
    /// <param name="URLs">Optional endpoints, when there is more than one.</param>
    /// <param name="TOTP">An optional TOTP configuration.</param>
    /// <param name="Refresh">How often to ask for the document again; HTTPS only.</param>
    /// <param name="CustomHeaders">Optional HTTP headers to send with every request to this transport.</param>
    public class Transport(TransportType                                    Type,
                           String?                                          URL            = null,
                           IEnumerable<TransportURL>?                       URLs           = null,
                           TOTPConfig?                                      TOTP           = null,
                           TimeSpan?                                        Refresh        = null,
                           IEnumerable<KeyValuePair<String, CustomHeaderValue>>?  CustomHeaders  = null)
    {

        #region Data

        /// <summary>
        /// How often an HTTPS transport is asked again when the document does not
        /// say. A charging session that is still running changes every few
        /// seconds, so "it did not say" means "the usual period", not "never ask
        /// again" — upstream's defaultRefreshSeconds since 0.14.4.
        /// </summary>
        public static readonly TimeSpan DefaultRefresh = TimeSpan.FromSeconds(10);

        #endregion

        #region Properties

        /// <summary>How this transport delivers its data.</summary>
        public TransportType                 Type       { get; } = Type;

        /// <summary>
        /// An optional single endpoint, for a transport built in code.
        ///
        /// The format names endpoints in "urls" and only there — the singular
        /// "url" was the older spelling and upstream 0.15.0 stopped reading it —
        /// so a document never sets this, and <see cref="ToJSON"/> writes it as
        /// the first entry of "urls".
        /// </summary>
        public String?                       URL        { get; } = URL;

        /// <summary>Optional endpoints, when there is more than one.</summary>
        public IReadOnlyList<TransportURL>   URLs       { get; } = URLs?.ToArray() ?? [];

        /// <summary>An optional TOTP configuration.</summary>
        public TOTPConfig?                   TOTP       { get; } = TOTP;

        /// <summary>
        /// Additional HTTP headers to send with every request to this transport,
        /// by header name — an API key or a tenant selector its endpoint expects.
        ///
        /// Every transport can carry them: an HTTPS poll, the opening request of
        /// a server-sent event stream and the handshake of a WebSocket are all
        /// HTTP requests. They belong to the transport that states them and are
        /// sent to its endpoints alone.
        /// </summary>
        public IReadOnlyDictionary<String, CustomHeaderValue>  CustomHeaders    { get; } = CustomHeaders?.ToDictionary() ?? [];

        /// <summary>
        /// How often to ask for the document again.
        ///
        /// A live link points at a charging session that is still running, so the
        /// document goes stale by itself and something has to say how often to
        /// fetch it again. This belongs to HTTPS alone: a WebSocket or a
        /// server-sent event stream delivers a new document when there is one,
        /// and if either ever needs a period of its own it will mean something
        /// else than asking again.
        /// </summary>
        public TimeSpan?                     Refresh    { get; } = Refresh;

        #endregion


        #region (static) TryParse(JSON, out Transport)

        /// <summary>
        /// Try to parse the given JSON as a live link transport.
        /// </summary>
        /// <param name="JSON">A JSON representation of a live link transport.</param>
        /// <param name="Transport">The parsed live link transport.</param>
        public static Boolean TryParse(JObject JSON, out Transport? Transport)
        {

            Transport = null;

            if (!TransportTypeExtensions.TryParse(JSON["type"]?.Value<String>() ?? "", out var type))
                return false;

            var urls = new List<TransportURL>();

            if (JSON["urls"] is JToken urlsJSON)
            {

                if (urlsJSON is not JArray urlArray)
                    return false;

                foreach (var urlJSON in urlArray)
                    if (TransportURL.TryParse(urlJSON, out var transportURL))
                        urls.Add(transportURL!);
                    else
                        return false;

            }

            TOTPConfig? totp = null;

            if (JSON["totp"] is JToken totpJSON &&
               (totpJSON is not JObject totpObject ||
                !TOTPConfig.TryParse(totpObject, out totp)))
            {
                return false;
            }

            #region The headers to send along

            var customHeaders = new Dictionary<String, CustomHeaderValue>();

            if (JSON["customHeaders"] is JToken customHeadersJSON)
            {

                if (customHeadersJSON is not JObject customHeadersObject)
                    return false;

                foreach (var header in customHeadersObject.Properties())
                    if (CustomHeaderValue.TryParse(header.Value, out var value))
                        customHeaders[header.Name] = value!;
                    else
                        return false;

            }

            #endregion

            #region How often to ask again

            TimeSpan? refresh = null;

            // Only HTTPS declares a refresh period, so only there is it
            // validated. On the other two it is an unknown property like any
            // other: neither checked nor a reason to reject the document.
            if (type == TransportType.HTTPS &&
                JSON["refresh"] is JToken refreshJSON)
            {

                if (refreshJSON.Type != JTokenType.Integer &&
                    refreshJSON.Type != JTokenType.Float)
                {
                    return false;
                }

                refresh = TimeSpan.FromSeconds(refreshJSON.Value<Double>());

            }

            #endregion

            // The singular "url" is not read: upstream 0.15.0 made "urls" the one
            // place a transport names its endpoints, and a transport carrying
            // only "url" is a transport with nothing to reach.
            Transport = new Transport(
                            type,
                            null,
                            urls,
                            totp,
                            refresh,
                            customHeaders
                        );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this live link transport.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("type", Type.AsText())
                       );

            if (URL is not null || URLs.Count > 0)
                json.Add(new JProperty("urls",  new JArray(
                                                    (URL is not null ? [ new JValue(URL) ] : Array.Empty<JToken>()).
                                                    Concat(URLs.Select(url => url.ToJSON()))
                                                )));

            if (TOTP is not null)
                json.Add(new JProperty("totp",  TOTP.ToJSON()));

            if (Refresh.HasValue)
                json.Add(new JProperty("refresh",  Refresh.Value.TotalSeconds % 1 == 0
                                                       ? new JValue((Int64) Refresh.Value.TotalSeconds)
                                                       : new JValue(        Refresh.Value.TotalSeconds)));

            if (CustomHeaders.Count > 0)
                json.Add(new JProperty("customHeaders",  new JObject(
                                                             CustomHeaders.Select(header => new JProperty(header.Key, header.Value.ToJSON()))
                                                         )));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this live link transport.
        /// </summary>
        public override String ToString()

            => $"{Type.AsText()}: {URL ?? (URLs.Count > 0 ? URLs[0].URL : "<no URL>")}";

        #endregion


    }


    /// <summary>
    /// A pointer to the live charge transparency data of a charging station,
    /// as encoded in the QR code on the station itself.
    ///
    /// It describes one charging session that is still running, where a charge
    /// transparency record is a collection of finished ones, and it tells an
    /// application where to subscribe while that session lasts. It may carry the
    /// signed meter values read so far as well — see
    /// <see cref="IO.ContentFormatDetector.TryToParseLiveLinkMeterValues"/>,
    /// which turns them into an ordinary charge transparency record — but it
    /// does not thereby become one.
    /// </summary>
    /// <param name="Created">An optional timestamp of when this live link was created.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="ImageURLs">Optional URLs of images or logos.</param>
    /// <param name="GeoLocation">An optional geographical location of the charging station.</param>
    /// <param name="Connector">An optional technical description of the connector.</param>
    /// <param name="LiveTransports">The transports carrying the live data.</param>
    /// <param name="Signatures">Optional signatures over this live link.</param>
    /// <param name="OriginalJSON">The document this live link was read from, when it was read from one.</param>
    public class ChargeTransparencyLiveLink(String?                     Created         = null,
                                            I18NString?                 Description     = null,
                                            IEnumerable<String>?        ImageURLs       = null,
                                            GeoCoordinate?              GeoLocation     = null,
                                            LiveLinkConnector?          Connector       = null,
                                            IEnumerable<Transport>?     LiveTransports  = null,
                                            IEnumerable<Signature>?     Signatures      = null,
                                            JObject?                    OriginalJSON    = null)
    {

        #region Data

        /// <summary>
        /// The JSON-LD context that marks a JSON object as a charge transparency live link.
        /// </summary>
        public const String JSONLDContext = "https://open.charging.cloud/contexts/chargeTransparency/live/link/1.0";

        #endregion

        #region Properties

        /// <summary>An optional timestamp of when this live link was created.</summary>
        public String?                     Created           { get; } = Created;

        /// <summary>An optional multi-language description.</summary>
        public I18NString?                 Description       { get; } = Description;

        /// <summary>Optional URLs of images or logos.</summary>
        public IReadOnlyList<String>       ImageURLs         { get; } = ImageURLs?.     ToArray() ?? [];

        /// <summary>An optional geographical location of the charging station.</summary>
        public GeoCoordinate?              GeoLocation       { get; } = GeoLocation;

        /// <summary>An optional technical description of the connector.</summary>
        public LiveLinkConnector?          Connector         { get; } = Connector;

        /// <summary>The transports carrying the live data.</summary>
        public IReadOnlyList<Transport>    LiveTransports    { get; } = LiveTransports?.ToArray() ?? [];

        /// <summary>Optional signatures over this live link.</summary>
        public IReadOnlyList<Signature>    Signatures        { get; } = Signatures?.    ToArray() ?? [];

        /// <summary>
        /// The document this live link was read from, when it was read from one.
        ///
        /// A live link is an open document: everything this reader does not model
        /// — the charging station, its operator, the energy meter, the signed
        /// meter values — arrives alongside what it does, and the newer fixtures
        /// are signed over the whole of it. A reader that re-assembled the
        /// document from its own model would drop those parts and with them the
        /// bytes any such signature covers, so the original is kept and written
        /// back unchanged.
        ///
        /// This is the object <see cref="TryParse"/> was handed rather than a
        /// copy of it, so that recognising a document does not cost a clone of
        /// it. <see cref="ToJSON"/> clones before returning.
        /// </summary>
        public JObject?                    OriginalJSON      { get; } = OriginalJSON;

        /// <summary>
        /// How the signatures over this whole document came out, when the
        /// document was read through the content format detector.
        ///
        /// Kept here rather than written into the document: the signatures cover
        /// every property but their own, and <see cref="ToJSON"/> hands back the
        /// document as it was signed.
        /// </summary>
        public Crypto.DocumentSignaturesResult?  SignatureVerification    { get; internal set; }

        /// <summary>
        /// Non-fatal findings about this document, e.g. that it is unsigned or
        /// that a signature did not verify. None of them make it unusable.
        /// </summary>
        public IReadOnlyList<Warning>      Warnings
            => warnings;

        #endregion

        #region Data (private)

        private readonly List<Warning> warnings = [];

        #endregion

        #region (internal) AddWarning(Warning)

        /// <summary>
        /// Add a non-fatal finding about this document.
        /// </summary>
        /// <param name="Warning">A warning.</param>
        internal ChargeTransparencyLiveLink AddWarning(Warning Warning)
        {
            warnings.Add(Warning);
            return this;
        }

        #endregion


        #region (static) IsAChargeTransparencyLiveLink(JSON)

        /// <summary>
        /// Whether the given JSON is a charge transparency live link.
        ///
        /// A live link is recognised by its context and by the two properties
        /// every one of them has: when it was created, and where its updates can
        /// be fetched. Nothing optional decides it. A malformed optional field —
        /// a broken transport most of all — must not turn the document into an
        /// unrecognised one that then fails as an "unknown format"; it is dropped
        /// where it is read, and the rest of the document stays usable. That is
        /// upstream 0.15.0, and it reverses what this reader did before, which
        /// refused the whole document over one transport it could not read.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        public static Boolean IsAChargeTransparencyLiveLink(JObject JSON)

            // Deliberately the same code path as reading one: a predicate that
            // says yes where the reader says no would send an application off to
            // fetch data through a document nobody could read.
            => TryParse(JSON, out _);

        #endregion

        #region (static) TryParse(JSON, out ChargeTransparencyLiveLink)

        /// <summary>
        /// Try to parse the given JSON as a charge transparency live link.
        /// </summary>
        /// <param name="JSON">A JSON representation of a charge transparency live link.</param>
        /// <param name="ChargeTransparencyLiveLink">The parsed charge transparency live link.</param>
        public static Boolean TryParse(JObject JSON, out ChargeTransparencyLiveLink? ChargeTransparencyLiveLink)
        {

            ChargeTransparencyLiveLink = null;

            #region The context, alone or in a list naming it among extensions

            var isLiveLink = JSON["@context"] switch {
                                 JValue { Type: JTokenType.String } context  => context.Value<String>() == JSONLDContext,
                                 JArray contexts                              => contexts.All(context => context.Type == JTokenType.String) &&
                                                                                 contexts.Any(context => context.Value<String>() == JSONLDContext),
                                 _                                            => false
                             };

            if (!isLiveLink)
                return false;

            #endregion

            #region When the link was created, which every one of them says

            // Reading a document no longer fills in a missing timestamp: it would
            // record when the document was read, and in a legally relevant
            // document that is not what "created" means.
            String? created;

            switch (JSON["created"]?.Type)
            {

                case JTokenType.String:
                    created = JSON["created"]!.Value<String>();
                    break;

                // A JSON reader that turns timestamps into dates of its own
                // accord — which is the default of Newtonsoft — must not cost the
                // caller their live link. The instant survives; only its spelling
                // becomes the one Chargy writes.
                case JTokenType.Date:
                    // A JSON date arrives as a DateTime or as a DateTimeOffset,
                    // depending on whether the text carried an offset.
                    created = (JSON["created"] is JValue { Value: DateTimeOffset offset }
                                   ? offset
                                   : new DateTimeOffset(JSON["created"]!.Value<DateTime>().ToUniversalTime(), TimeSpan.Zero)).
                              ToUniversalTime().
                              ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
                    break;

                default:
                    return false;

            }

            #endregion

            #region How to reach it, which every one of them says too

            if (JSON["liveTransports"] is not JArray transportArray)
                return false;

            // A transport that does not hold up is dropped, and the others are
            // kept: the document is still a live link with working addresses.
            var liveTransports = new List<Transport>();

            foreach (var transportJSON in transportArray)
                if (transportJSON is JObject transportObject &&
                    Transport.TryParse(transportObject, out var transport))
                {
                    liveTransports.Add(transport!);
                }

            #endregion

            #region Everything optional, read where it holds up and dropped where it does not

            var description = JSON["description"] is JObject descriptionJSON
                                  ? I18NString.Parse(descriptionJSON)
                                  : null;

            var imageURLs   = JSON["imageURLs"] is JArray imageURLArray
                                  ? imageURLArray.Where (imageURL => imageURL.Type == JTokenType.String).
                                                  Select(imageURL => imageURL.Value<String>()!)
                                  : null;

            // Upstream 3a2d3ad moved the position onto the charging station and
            // 0.15.0 dropped it from the top level of the format; a document
            // that still states it there is read, the station is asked otherwise.
            var geoLocation = (JSON["geoLocation"] is JObject geoLocationJSON
                                   ? GeoCoordinate.TryParse(geoLocationJSON)
                                   : null) ??
                              (JSON["chargingStation"]?["geoLocation"] is JObject stationLocation
                                   ? GeoCoordinate.TryParse(stationLocation)
                                   : null);

            // The connector moved below the EVSE below the charging station the
            // same way.
            LiveLinkConnector? connector = null;

            if (JSON["connector"] is JObject connectorJSON)
                LiveLinkConnector.TryParse(connectorJSON, out connector);

            if (connector is null &&
                JSON["chargingStation"]?["EVSE"]?["connector"] is JObject evseConnector)
            {
                LiveLinkConnector.TryParse(evseConnector, out connector);
            }

            var signatures  = new List<Signature>();

            if (JSON["signatures"] is JArray signatureArray)
                foreach (var signatureJSON in signatureArray.OfType<JObject>())
                    if (Signature.TryParse(signatureJSON, out var signature))
                        signatures.Add(signature!);

            #endregion

            ChargeTransparencyLiveLink = new ChargeTransparencyLiveLink(
                                             created,
                                             description,
                                             imageURLs,
                                             geoLocation,
                                             connector,
                                             liveTransports,
                                             signatures,
                                             JSON
                                         );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this charge transparency live link.
        /// </summary>
        public JObject ToJSON()
        {

            #region A link that was read is written back as it arrived

            // Everything this reader does not model would be lost otherwise, and
            // the signatures a live link may carry are taken over the whole
            // document. The one property that may have changed on the way in is
            // the creation timestamp, and only when a JSON reader had turned it
            // into a date token: that is rewritten the way Chargy spells one.
            if (OriginalJSON is not null)
            {

                var document = (JObject) OriginalJSON.DeepClone();

                if (Created is not null)
                    document["created"] = Created;

                return document;

            }

            #endregion

            var json = new JObject(
                           new JProperty("@context", JSONLDContext)
                       );

            if (Created is not null)
                json.Add(new JProperty("created",         Created));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",     Description.ToJSON()));

            if (ImageURLs. Count > 0)
                json.Add(new JProperty("imageURLs",       new JArray(ImageURLs)));

            if (GeoLocation.HasValue)
                json.Add(new JProperty("geoLocation",     GeoLocation.Value.ToJSON()));

            if (Connector is not null)
                json.Add(new JProperty("connector",       Connector.ToJSON()));

            if (LiveTransports.Count > 0)
                json.Add(new JProperty("liveTransports",  new JArray(LiveTransports.Select(transport => transport.ToJSON()))));

            if (Signatures.Count > 0)
                json.Add(new JProperty("signatures",      new JArray(Signatures.Select(signature => signature.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this charge transparency live link.
        /// </summary>
        public override String ToString()

            => $"{LiveTransports.Count} transport(s)";

        #endregion


    }


    /// <summary>
    /// The technical description of the connector a live link belongs to.
    ///
    /// Note: This is deliberately a different type from <see cref="Connector"/>:
    /// a live link describes the connector by its OCPI-style capabilities, not by
    /// the identification and cable data a charge transparency record carries.
    /// </summary>
    /// <param name="Standard">An optional connector standard, e.g. "IEC_62196_T2".</param>
    /// <param name="Format">An optional connector format, e.g. "SOCKET".</param>
    /// <param name="PowerType">An optional power type, e.g. "AC_3_PHASE".</param>
    /// <param name="MaxPower">An optional maximum power.</param>
    public class LiveLinkConnector(String?  Standard   = null,
                                   String?  Format     = null,
                                   String?  PowerType  = null,
                                   String?  MaxPower   = null)
    {

        #region Properties

        /// <summary>An optional connector standard, e.g. "IEC_62196_T2".</summary>
        public String?  Standard     { get; } = Standard;

        /// <summary>An optional connector format, e.g. "SOCKET".</summary>
        public String?  Format       { get; } = Format;

        /// <summary>An optional power type, e.g. "AC_3_PHASE".</summary>
        public String?  PowerType    { get; } = PowerType;

        /// <summary>An optional maximum power.</summary>
        public String?  MaxPower     { get; } = MaxPower;

        #endregion


        #region (static) TryParse(JSON, out LiveLinkConnector)

        /// <summary>
        /// Try to parse the given JSON as a live link connector.
        /// </summary>
        /// <param name="JSON">A JSON representation of a live link connector.</param>
        /// <param name="LiveLinkConnector">The parsed live link connector.</param>
        public static Boolean TryParse(JObject JSON, out LiveLinkConnector? LiveLinkConnector)
        {

            LiveLinkConnector = null;

            // Every field is a string or absent; anything else means the link was
            // not produced by a Chargy compatible station.
            foreach (var name in new[] { "standard", "format", "powerType", "maxPower" })
                if (JSON[name] is JToken token && token.Type != JTokenType.String)
                    return false;

            LiveLinkConnector = new LiveLinkConnector(
                                    JSON["standard"]?. Value<String>(),
                                    JSON["format"]?.   Value<String>(),
                                    JSON["powerType"]?.Value<String>(),
                                    JSON["maxPower"]?. Value<String>()
                                );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this live link connector.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Standard  is not null)
                json.Add(new JProperty("standard",   Standard));

            if (Format    is not null)
                json.Add(new JProperty("format",     Format));

            if (PowerType is not null)
                json.Add(new JProperty("powerType",  PowerType));

            if (MaxPower  is not null)
                json.Add(new JProperty("maxPower",   MaxPower));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this live link connector.
        /// </summary>
        public override String ToString()

            => Standard ?? Format ?? "<unknown connector>";

        #endregion


    }

}
