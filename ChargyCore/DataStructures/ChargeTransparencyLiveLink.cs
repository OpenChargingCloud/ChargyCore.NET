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
                json["url"]?.Value<String>() is String objectURL)
            {

                TransportURL = new TransportURL(
                                   objectURL,
                                   json["priority"]?.Value<Int32>(),
                                   json["weight"]?.  Value<Int32>()
                               );

                return true;

            }

            return false;

        }

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
    /// </summary>
    /// <param name="InitialSharedSecret">The shared secret.</param>
    /// <param name="TimeStep">The length of one time step in seconds.</param>
    public class TOTPConfig(String  InitialSharedSecret,
                            Int32   TimeStep)
    {

        #region Properties

        /// <summary>The shared secret.</summary>
        public String  InitialSharedSecret    { get; } = InitialSharedSecret;

        /// <summary>The length of one time step in seconds.</summary>
        public Int32   TimeStep               { get; } = TimeStep;

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

            var secret   = JSON["initialSharedSecret"]?.Value<String>();
            var timeStep = JSON["timeStep"];

            if (secret is null || timeStep is null || timeStep.Type != JTokenType.Integer)
                return false;

            TOTPConfig = new TOTPConfig(secret, timeStep.Value<Int32>());

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this TOTP configuration.
        /// </summary>
        public JObject ToJSON()

            => new (
                   new JProperty("initialSharedSecret",  InitialSharedSecret),
                   new JProperty("timeStep",             TimeStep)
               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this TOTP configuration.
        /// </summary>
        public override String ToString()

            => $"TOTP every {TimeStep}s";

        #endregion


    }


    /// <summary>
    /// One way of reaching the live data of a charging station.
    /// </summary>
    /// <param name="Type">How this transport delivers its data.</param>
    /// <param name="URL">An optional single URL.</param>
    /// <param name="URLs">Optional endpoints, when there is more than one.</param>
    /// <param name="TOTP">An optional TOTP configuration.</param>
    /// <param name="Refresh">How often to ask for the document again; HTTPS only.</param>
    public class Transport(TransportType               Type,
                           String?                     URL      = null,
                           IEnumerable<TransportURL>?  URLs     = null,
                           TOTPConfig?                 TOTP     = null,
                           TimeSpan?                   Refresh  = null)
    {

        #region Properties

        /// <summary>How this transport delivers its data.</summary>
        public TransportType                 Type       { get; } = Type;

        /// <summary>An optional single URL.</summary>
        public String?                       URL        { get; } = URL;

        /// <summary>Optional endpoints, when there is more than one.</summary>
        public IReadOnlyList<TransportURL>   URLs       { get; } = URLs?.ToArray() ?? [];

        /// <summary>An optional TOTP configuration.</summary>
        public TOTPConfig?                   TOTP       { get; } = TOTP;

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

            if (JSON["urls"] is JArray urlArray)
                foreach (var urlJSON in urlArray)
                    if (TransportURL.TryParse(urlJSON, out var transportURL))
                        urls.Add(transportURL!);
                    else
                        return false;

            TOTPConfig? totp = null;

            if (JSON["totp"] is JObject totpJSON &&
               !TOTPConfig.TryParse(totpJSON, out totp))
            {
                return false;
            }

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

            Transport = new Transport(
                            type,
                            JSON["url"]?.Value<String>(),
                            urls,
                            totp,
                            refresh
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

            if (URL  is not null)
                json.Add(new JProperty("url",   URL));

            if (URLs.Count > 0)
                json.Add(new JProperty("urls",  new JArray(URLs.Select(url => url.ToJSON()))));

            if (TOTP is not null)
                json.Add(new JProperty("totp",  TOTP.ToJSON()));

            if (Refresh.HasValue)
                json.Add(new JProperty("refresh",  Refresh.Value.TotalSeconds % 1 == 0
                                                       ? new JValue((Int64) Refresh.Value.TotalSeconds)
                                                       : new JValue(        Refresh.Value.TotalSeconds)));

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

        #endregion


        #region (static) IsAChargeTransparencyLiveLink(JSON)

        /// <summary>
        /// Whether the given JSON is a charge transparency live link.
        ///
        /// The whole document is checked, not just its context. A live link is
        /// not evidence to be judged but a list of addresses somebody is being
        /// invited to contact, and a field that is not what it claims to be
        /// makes the document unusable rather than merely incomplete.
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

            if (JSON["@context"]?.Value<String>() != JSONLDContext)
                return false;

            #region When the link was created, if it says

            String? created = null;

            if (JSON["created"] is JToken timestampJSON)
                switch (timestampJSON.Type)
                {

                    case JTokenType.Null:
                        break;

                    case JTokenType.String:
                        created = timestampJSON.Value<String>();
                        break;

                    // A JSON reader that turns timestamps into dates of its own
                    // accord — which is Newtonsoft's default — must not cost the
                    // caller their live link. The instant survives; only its
                    // spelling becomes the one Chargy writes. Nothing here is
                    // signed, so nothing depends on the original characters.
                    case JTokenType.Date:
                        // A JSON date arrives as a DateTime or as a DateTimeOffset,
                        // depending on whether the text carried an offset.
                        created = (timestampJSON is JValue { Value: DateTimeOffset offset }
                                       ? offset
                                       : new DateTimeOffset(timestampJSON.Value<DateTime>().ToUniversalTime(), TimeSpan.Zero)).
                                  ToUniversalTime().
                                  ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
                        break;

                    default:
                        return false;

                }

            #endregion

            #region The fields that are simply what they are, or the document is not one

            if (JSON["description"] is JToken descriptionJSON &&
                descriptionJSON     is not JObject)
            {
                return false;
            }

            if (JSON["imageURLs"]   is JToken imageURLsJSON &&
               (imageURLsJSON       is not JArray imageURLArray ||
                imageURLArray.Any(imageURL => imageURL.Type != JTokenType.String)))
            {
                return false;
            }

            #endregion

            #region Where the charging station stands

            GeoCoordinate? geoLocation = null;

            if (JSON["geoLocation"] is JToken geoLocationJSON)
            {

                if (geoLocationJSON is not JObject geoLocationObject)
                    return false;

                geoLocation = GeoCoordinate.TryParse(geoLocationObject);

                if (!geoLocation.HasValue)
                    return false;

            }

            // Upstream 3a2d3ad moved the position onto the charging station,
            // where a charge transparency record has always kept it, so a live
            // link written since then states it there and nowhere else.
            //
            // Read leniently, unlike the property above: that one is what the
            // type guard validates, so a malformed value there means the
            // document is not a live link. Nobody validates the station block,
            // and refusing a usable list of addresses over a bad coordinate
            // inside it would be stricter than upstream for no gain.
            geoLocation ??= JSON["chargingStation"]?["geoLocation"] is JObject stationLocation
                                ? GeoCoordinate.TryParse(stationLocation)
                                : null;

            #endregion

            #region How to reach it

            var liveTransports = new List<Transport>();

            if (JSON["liveTransports"] is JToken transportsJSON)
            {

                if (transportsJSON is not JArray transportArray)
                    return false;

                foreach (var transportJSON in transportArray)
                    // An unreadable transport means the link cannot be trusted
                    // to describe how to reach the station at all. Keeping the
                    // readable ones and dropping the rest would leave an
                    // application believing it had been told everything.
                    if (transportJSON is not JObject transportObject ||
                        !Transport.TryParse(transportObject, out var transport))
                    {
                        return false;
                    }
                    else
                        liveTransports.Add(transport!);

            }

            LiveLinkConnector? connector = null;

            if (JSON["connector"] is JToken connectorJSON &&
               (connectorJSON     is not JObject connectorObject ||
                !LiveLinkConnector.TryParse(connectorObject, out connector)))
            {
                return false;
            }

            // The connector moved below the EVSE below the charging station with
            // the same commit, and is read there as leniently as the position.
            if (connector is null &&
                JSON["chargingStation"]?["EVSE"]?["connector"] is JObject evseConnector)
            {
                LiveLinkConnector.TryParse(evseConnector, out connector);
            }

            #endregion

            #region ..., and who says so

            var signatures = new List<Signature>();

            if (JSON["signatures"] is JToken signaturesJSON)
            {

                if (signaturesJSON is not JArray signatureArray)
                    return false;

                foreach (var signatureJSON in signatureArray.OfType<JObject>())
                    if (Signature.TryParse(signatureJSON, out var signature))
                        signatures.Add(signature!);

            }

            #endregion

            ChargeTransparencyLiveLink = new ChargeTransparencyLiveLink(
                                             created,
                                             JSON["description"] is JObject description
                                                 ? I18NString.Parse(description)
                                                 : null,
                                             StringList.Parse(JSON["imageURLs"]),
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
            // the creation timestamp: a link that carried none is stamped when it
            // is read, and a date token is rewritten the way Chargy spells one.
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
