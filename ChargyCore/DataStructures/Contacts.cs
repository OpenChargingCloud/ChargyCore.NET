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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// How to reach a charging station operator or a manufacturer.
    /// </summary>
    /// <param name="EMail">An optional e-mail address.</param>
    /// <param name="Web">An optional web site.</param>
    /// <param name="LogoURL">An optional URL of a logo.</param>
    /// <param name="Address">An optional postal address.</param>
    /// <param name="PublicKeys">Optional public keys of this contact.</param>
    public class Contact(String?                  EMail       = null,
                         String?                  Web         = null,
                         String?                  LogoURL     = null,
                         Address?                 Address     = null,
                         IEnumerable<PublicKey>?  PublicKeys  = null)
    {

        #region Properties

        /// <summary>An optional e-mail address.</summary>
        public String?                   EMail         { get; } = EMail;

        /// <summary>An optional web site.</summary>
        public String?                   Web           { get; } = Web;

        /// <summary>An optional URL of a logo.</summary>
        public String?                   LogoURL       { get; } = LogoURL;

        /// <summary>An optional postal address.</summary>
        public Address?                  Address       { get; } = Address;

        /// <summary>Optional public keys of this contact.</summary>
        public IReadOnlyList<PublicKey>  PublicKeys    { get; } = PublicKeys?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out Contact)

        /// <summary>
        /// Try to parse the given JSON as a contact.
        /// </summary>
        /// <param name="JSON">A JSON representation of a contact.</param>
        /// <param name="Contact">The parsed contact.</param>
        public static Boolean TryParse(JObject JSON, out Contact? Contact)
        {

            Address? address = null;

            if (JSON["address"] is JObject addressJSON)
                chargy.Address.TryParse(addressJSON, out address);

            Contact = new Contact(
                          JSON["email"]?.  Value<String>(),
                          JSON["web"]?.    Value<String>(),
                          JSON["logoUrl"]?.Value<String>(),
                          address,
                          PublicKeyList.Parse(JSON["publicKeys"])
                      );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this contact.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (EMail   is not null)
                json.Add(new JProperty("email",       EMail));

            if (Web     is not null)
                json.Add(new JProperty("web",         Web));

            if (LogoURL is not null)
                json.Add(new JProperty("logoUrl",     LogoURL));

            if (Address is not null)
                json.Add(new JProperty("address",     Address.ToJSON()));

            if (PublicKeys.Count > 0)
                json.Add(new JProperty("publicKeys",  new JArray(PublicKeys.Select(publicKey => publicKey.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this contact.
        /// </summary>
        public override String ToString()

            => EMail ?? Web ?? "<no contact>";

        #endregion


    }


    /// <summary>
    /// Where an EV driver can get help with a charging session, including the
    /// mediation services that settle a dispute about a charging bill.
    /// </summary>
    /// <param name="EMail">The e-mail address of the support.</param>
    /// <param name="Hotline">An optional telephone number.</param>
    /// <param name="Web">An optional web site.</param>
    /// <param name="MediationServices">Optional mediation services.</param>
    /// <param name="PublicKeys">Optional public keys of the support.</param>
    public class Support(String                            EMail,
                         String?                           Hotline            = null,
                         String?                           Web                = null,
                         IEnumerable<MediationService>?    MediationServices  = null,
                         IEnumerable<PublicKey>?           PublicKeys         = null)
    {

        #region Properties

        /// <summary>The e-mail address of the support.</summary>
        public String                            EMail                { get; } = EMail;

        /// <summary>An optional telephone number.</summary>
        public String?                           Hotline              { get; } = Hotline;

        /// <summary>An optional web site.</summary>
        public String?                           Web                  { get; } = Web;

        /// <summary>Optional mediation services.</summary>
        public IReadOnlyList<MediationService>   MediationServices    { get; } = MediationServices?.ToArray() ?? [];

        /// <summary>Optional public keys of the support.</summary>
        public IReadOnlyList<PublicKey>          PublicKeys           { get; } = PublicKeys?.       ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out Support)

        /// <summary>
        /// Try to parse the given JSON as support information.
        /// </summary>
        /// <param name="JSON">A JSON representation of support information.</param>
        /// <param name="Support">The parsed support information.</param>
        public static Boolean TryParse(JObject JSON, out Support? Support)
        {

            Support = null;

            var email = JSON["email"]?.Value<String>();

            if (email is null)
                return false;

            var mediationServices = new List<MediationService>();

            if (JSON["mediationServices"] is JArray mediationServiceArray)
                foreach (var mediationServiceJSON in mediationServiceArray.OfType<JObject>())
                    if (MediationService.TryParse(mediationServiceJSON, out var mediationService))
                        mediationServices.Add(mediationService!);

            Support = new Support(
                          email,
                          JSON["hotline"]?.Value<String>(),
                          JSON["web"]?.    Value<String>(),
                          mediationServices,
                          PublicKeyList.Parse(JSON["publicKeys"])
                      );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this support information.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Hotline is not null)
                json.Add(new JProperty("hotline",            Hotline));

            json.Add(new JProperty("email",                  EMail));

            if (Web     is not null)
                json.Add(new JProperty("web",                Web));

            if (MediationServices.Count > 0)
                json.Add(new JProperty("mediationServices",  new JArray(MediationServices.Select(service   => service.  ToJSON()))));

            if (PublicKeys.       Count > 0)
                json.Add(new JProperty("publicKeys",         new JArray(PublicKeys.       Select(publicKey => publicKey.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this support information.
        /// </summary>
        public override String ToString()

            => EMail;

        #endregion


    }


    /// <summary>
    /// Whom to ask about the personal data within a charge transparency record.
    /// </summary>
    /// <param name="Contact">The name of the data protection officer.</param>
    /// <param name="EMail">The e-mail address of the data protection officer.</param>
    /// <param name="Web">The web site of the data protection officer.</param>
    /// <param name="PublicKeys">Optional public keys of the data protection officer.</param>
    public class PrivacyContact(String                   Contact,
                                String                   EMail,
                                String                   Web,
                                IEnumerable<PublicKey>?  PublicKeys = null)
    {

        #region Properties

        /// <summary>The name of the data protection officer.</summary>
        public String                    Contact       { get; } = Contact;

        /// <summary>The e-mail address of the data protection officer.</summary>
        public String                    EMail         { get; } = EMail;

        /// <summary>The web site of the data protection officer.</summary>
        public String                    Web           { get; } = Web;

        /// <summary>Optional public keys of the data protection officer.</summary>
        public IReadOnlyList<PublicKey>  PublicKeys    { get; } = PublicKeys?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out PrivacyContact)

        /// <summary>
        /// Try to parse the given JSON as a privacy contact.
        /// </summary>
        /// <param name="JSON">A JSON representation of a privacy contact.</param>
        /// <param name="PrivacyContact">The parsed privacy contact.</param>
        public static Boolean TryParse(JObject JSON, out PrivacyContact? PrivacyContact)
        {

            PrivacyContact = null;

            var contact = JSON["contact"]?.Value<String>();
            var email   = JSON["email"]?.  Value<String>();
            var web     = JSON["web"]?.    Value<String>();

            if (contact is null || email is null || web is null)
                return false;

            PrivacyContact = new PrivacyContact(
                                 contact,
                                 email,
                                 web,
                                 PublicKeyList.Parse(JSON["publicKeys"])
                             );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this privacy contact.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("contact",  Contact),
                           new JProperty("email",    EMail),
                           new JProperty("web",      Web)
                       );

            if (PublicKeys.Count > 0)
                json.Add(new JProperty("publicKeys",  new JArray(PublicKeys.Select(publicKey => publicKey.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this privacy contact.
        /// </summary>
        public override String ToString()

            => $"{Contact} <{EMail}>";

        #endregion


    }


    /// <summary>
    /// An independent body an EV driver can turn to when a charging bill is
    /// disputed, e.g. a conciliation board.
    /// </summary>
    /// <param name="Id">The identification of the mediation service.</param>
    /// <param name="Description">A multi-language description.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="PublicKeys">Optional public keys of the mediation service.</param>
    public class MediationService(String                   Id,
                                  I18NString               Description,
                                  IEnumerable<String>?     Context     = null,
                                  IEnumerable<PublicKey>?  PublicKeys  = null)
    {

        #region Properties

        /// <summary>The identification of the mediation service.</summary>
        public String                    Id             { get; } = Id;

        /// <summary>A multi-language description.</summary>
        public I18NString                Description    { get; } = Description;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>     Context        { get; } = Context?.ToArray() ?? [];

        /// <summary>Optional public keys of the mediation service.</summary>
        public IReadOnlyList<PublicKey>  PublicKeys     { get; } = PublicKeys?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out MediationService)

        /// <summary>
        /// Try to parse the given JSON as a mediation service.
        /// </summary>
        /// <param name="JSON">A JSON representation of a mediation service.</param>
        /// <param name="MediationService">The parsed mediation service.</param>
        public static Boolean TryParse(JObject JSON, out MediationService? MediationService)
        {

            MediationService = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            MediationService = new MediationService(
                                   id,
                                   JSON["description"] is JObject descriptionJSON
                                       ? I18NString.Parse(descriptionJSON) ?? I18NString.Empty
                                       : I18NString.Empty,
                                   PublicKey.ParseContext(JSON["@context"]),
                                   PublicKeyList.Parse(JSON["publicKeys"])
                               );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this mediation service.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (Context.Count == 1)
                json.Add(new JProperty("@context",    Context[0]));

            else if (Context.Count > 1)
                json.Add(new JProperty("@context",    new JArray(Context)));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description", Description.ToJSON()));

            if (PublicKeys.Count > 0)
                json.Add(new JProperty("publicKeys",  new JArray(PublicKeys.Select(publicKey => publicKey.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this mediation service.
        /// </summary>
        public override String ToString()

            => Id;

        #endregion


    }


    /// <summary>
    /// Helpers for the public key lists that nearly every entity of a charge
    /// transparency record carries.
    /// </summary>
    internal static class PublicKeyList
    {

        #region Parse(JSON)

        /// <summary>
        /// The public keys of the given JSON array, silently skipping malformed
        /// entries: a single unreadable key must not discard an entire charging
        /// station operator.
        /// </summary>
        /// <param name="JSON">A JSON array of public keys.</param>
        internal static List<PublicKey> Parse(JToken? JSON)
        {

            var publicKeys = new List<PublicKey>();

            if (JSON is JArray publicKeyArray)
                foreach (var publicKeyJSON in publicKeyArray.OfType<JObject>())
                    if (PublicKey.TryParse(publicKeyJSON, out var publicKey))
                        publicKeys.Add(publicKey!);

            return publicKeys;

        }

        #endregion

    }

}
