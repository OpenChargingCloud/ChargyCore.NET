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

using org.GraphDefined.Vanaheimr.Aegir;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// The operator of the grid a charging station is connected to: a party to a
    /// charging session whenever it asks the station to charge more slowly.
    ///
    /// The power of a session drops then for a reason that is neither the car's
    /// nor the station's, and without the constraint and the key it was signed
    /// with that dip is indistinguishable from a fault. This carries the identity
    /// and the keys to tell the two apart — a charging station operator minus
    /// everything about charging infrastructure, because a grid operator runs no
    /// pools, stations or tariffs. Its keys are listed under the key usage
    /// "signGridPowerConstraints". Upstream 0.16.0.
    /// </summary>
    /// <param name="Id">The identification of the grid operator.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Name">An optional multi-language name.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="Contact">How to reach the grid operator.</param>
    /// <param name="Support">Where to get help.</param>
    /// <param name="Privacy">Whom to ask about personal data.</param>
    /// <param name="GeoLocation">An optional geographical location.</param>
    /// <param name="ImageURLs">Optional URLs of images or logos.</param>
    /// <param name="PublicKeys">Optional public keys of the grid operator.</param>
    public class GridOperator(String                   Id,
                              IEnumerable<String>?     Context      = null,
                              I18NString?              Name         = null,
                              I18NString?              Description  = null,
                              Contact?                 Contact      = null,
                              Support?                 Support      = null,
                              PrivacyContact?          Privacy      = null,
                              GeoCoordinate?           GeoLocation  = null,
                              IEnumerable<String>?     ImageURLs    = null,
                              IEnumerable<PublicKey>?  PublicKeys   = null)
    {

        #region Properties

        /// <summary>The identification of the grid operator.</summary>
        public String                   Id               { get; } = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>    JSONLDContext    { get; } = Context?.   ToArray() ?? [];

        /// <summary>An optional multi-language name.</summary>
        public I18NString?              Name             { get; } = Name;

        /// <summary>An optional multi-language description.</summary>
        public I18NString?              Description      { get; } = Description;

        /// <summary>How to reach the grid operator.</summary>
        public Contact?                 Contact          { get; } = Contact;

        /// <summary>Where to get help.</summary>
        public Support?                 Support          { get; } = Support;

        /// <summary>Whom to ask about personal data.</summary>
        public PrivacyContact?          Privacy          { get; } = Privacy;

        /// <summary>An optional geographical location.</summary>
        public GeoCoordinate?           GeoLocation      { get; } = GeoLocation;

        /// <summary>Optional URLs of images or logos.</summary>
        public IReadOnlyList<String>    ImageURLs        { get; } = ImageURLs?. ToArray() ?? [];

        /// <summary>Optional public keys of the grid operator.</summary>
        public IReadOnlyList<PublicKey> PublicKeys       { get; } = PublicKeys?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out GridOperator)

        /// <summary>
        /// Try to parse the given JSON as a grid operator. Its "@id" is all it
        /// requires.
        /// </summary>
        /// <param name="JSON">A JSON representation of a grid operator.</param>
        /// <param name="GridOperator">The parsed grid operator.</param>
        public static Boolean TryParse(JObject JSON, out GridOperator? GridOperator)
        {

            GridOperator = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            Contact?        contact = null;
            Support?        support = null;
            PrivacyContact? privacy = null;

            if (JSON["contact"] is JObject contactJSON)
                chargy.Contact.       TryParse(contactJSON, out contact);

            if (JSON["support"] is JObject supportJSON)
                chargy.Support.       TryParse(supportJSON, out support);

            if (JSON["privacy"] is JObject privacyJSON)
                chargy.PrivacyContact.TryParse(privacyJSON, out privacy);

            GridOperator = new GridOperator(
                               id,
                               PublicKey.ParseContext(JSON["@context"]),
                               JSON["name"]        is JObject nameJSON        ? I18NString.Parse(nameJSON)        : null,
                               JSON["description"] is JObject descriptionJSON ? I18NString.Parse(descriptionJSON) : null,
                               contact,
                               support,
                               privacy,
                               JSON["geoLocation"] is JObject geoLocationJSON ? GeoCoordinate.TryParse(geoLocationJSON) : null,
                               StringList.Parse   (JSON["imageURLs"]),
                               PublicKeyList.Parse(JSON["publicKeys"])
                           );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this grid operator.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",     JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",     new JArray(JSONLDContext)));

            if (Name.IsNotNullOrEmpty())
                json.Add(new JProperty("name",         Name.ToJSON()));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",  Description.ToJSON()));

            if (Contact is not null)
                json.Add(new JProperty("contact",      Contact.ToJSON()));

            if (Support is not null)
                json.Add(new JProperty("support",      Support.ToJSON()));

            if (Privacy is not null)
                json.Add(new JProperty("privacy",      Privacy.ToJSON()));

            if (GeoLocation.HasValue)
                json.Add(new JProperty("geoLocation",  GeoLocation.Value.ToJSON()));

            if (ImageURLs. Count > 0)
                json.Add(new JProperty("imageURLs",    new JArray(ImageURLs)));

            if (PublicKeys.Count > 0)
                json.Add(new JProperty("publicKeys",   new JArray(PublicKeys.Select(publicKey => publicKey.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this grid operator.
        /// </summary>
        public override String ToString()

            => Id;

        #endregion

    }

}
