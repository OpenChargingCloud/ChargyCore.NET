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

#endregion

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// A URL pointing at charge transparency data, as encoded in the QR codes on
    /// charging stations.
    ///
    /// Scanning such a code does not yield a charge transparency record but a
    /// pointer to one, so Chargy has to recognise it as a distinct kind of input
    /// and offer to resolve it.
    /// </summary>
    /// <param name="URL">The URL.</param>
    /// <param name="Method">An optional HTTP method to use.</param>
    /// <param name="AcceptType">An optional HTTP Accept type.</param>
    /// <param name="Actions">Optional actions the URL offers.</param>
    /// <param name="ServiceTypes">Optional service types the URL offers.</param>
    /// <param name="ServiceData">Optional service specific data.</param>
    public class SimpleURL(String                URL,
                           String?               Method        = null,
                           String?               AcceptType    = null,
                           IEnumerable<String>?  Actions       = null,
                           IEnumerable<String>?  ServiceTypes  = null,
                           JObject?              ServiceData   = null)
    {

        #region Data

        /// <summary>
        /// The JSON-LD context that marks a JSON object as a Chargy URL.
        /// </summary>
        public const String JSONLDContext = "https://open.charging.cloud/contexts/URL";

        #endregion

        #region Properties

        /// <summary>The URL.</summary>
        public String                 URL             { get; } = URL;

        /// <summary>An optional HTTP method to use.</summary>
        public String?                Method          { get; } = Method;

        /// <summary>An optional HTTP Accept type.</summary>
        public String?                AcceptType      { get; } = AcceptType;

        /// <summary>Optional actions the URL offers.</summary>
        public IReadOnlyList<String>  Actions         { get; } = Actions?.     ToArray() ?? [];

        /// <summary>Optional service types the URL offers.</summary>
        public IReadOnlyList<String>  ServiceTypes    { get; } = ServiceTypes?.ToArray() ?? [];

        /// <summary>Optional service specific data.</summary>
        public JObject?               ServiceData     { get; } = ServiceData;

        #endregion


        #region (static) IsValidURL(Text)

        /// <summary>
        /// Whether the given text is an absolute HTTP or HTTPS URL.
        ///
        /// Only those two schemes are accepted: a QR code on a charging station
        /// is scanned by a phone, and a "file:" or "javascript:" URL arriving
        /// from an untrusted printed sticker has no business being followed.
        /// </summary>
        /// <param name="Text">A text representation of a URL.</param>
        public static Boolean IsValidURL(String Text)

            => Uri.TryCreate(Text, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps);

        #endregion

        #region (static) IsASimpleURL(JSON)

        /// <summary>
        /// Whether the given JSON is a Chargy URL.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        public static Boolean IsASimpleURL(JObject JSON)

            => JSON["@context"]?.Value<String>() == JSONLDContext &&
               JSON["url"]?.     Value<String>() is String url &&
               IsValidURL(url);

        #endregion

        #region (static) TryParse(JSON, out SimpleURL)

        /// <summary>
        /// Try to parse the given JSON as a Chargy URL.
        /// </summary>
        /// <param name="JSON">A JSON representation of a Chargy URL.</param>
        /// <param name="SimpleURL">The parsed Chargy URL.</param>
        public static Boolean TryParse(JObject JSON, out SimpleURL? SimpleURL)
        {

            SimpleURL = null;

            if (!IsASimpleURL(JSON))
                return false;

            SimpleURL = new SimpleURL(
                            JSON["url"]!.       Value<String>()!,
                            JSON["method"]?.    Value<String>(),
                            JSON["acceptType"]?.Value<String>(),
                            StringList.Parse(JSON["actions"]),
                            StringList.Parse(JSON["serviceTypes"]),
                            JSON["serviceData"] as JObject
                        );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this Chargy URL.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@context",  JSONLDContext),
                           new JProperty("url",       URL)
                       );

            if (Method       is not null)
                json.Add(new JProperty("method",        Method));

            if (AcceptType   is not null)
                json.Add(new JProperty("acceptType",    AcceptType));

            if (Actions.     Count > 0)
                json.Add(new JProperty("actions",       new JArray(Actions)));

            if (ServiceTypes.Count > 0)
                json.Add(new JProperty("serviceTypes",  new JArray(ServiceTypes)));

            if (ServiceData  is not null)
                json.Add(new JProperty("serviceData",   ServiceData));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this Chargy URL.
        /// </summary>
        public override String ToString()

            => URL;

        #endregion


    }

}
