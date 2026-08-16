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
    /// Who built a charging station or an energy meter.
    ///
    /// Under the German Calibration Law the manufacturer is part of the evidence:
    /// an EV driver has to be able to tell whose device produced a reading, and
    /// whom to contact about it.
    /// </summary>
    /// <param name="Name">The name of the manufacturer.</param>
    /// <param name="URL">
    /// An optional web address of the manufacturer.
    ///
    /// Distinct from a contact's web address, which is where to write to about a
    /// particular device: this is who the manufacturer is. Some formats carry
    /// only one of the two, and which one they carry is what they mean.
    /// </param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="Contact">An optional contact.</param>
    /// <param name="Support">An optional support.</param>
    /// <param name="PrivacyContact">An optional privacy contact.</param>
    /// <param name="GeoLocation">An optional geographical location.</param>
    /// <param name="PublicKeys">Optional public keys of the manufacturer.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    public class Manufacturer(String?                  Name            = null,
                              String?                  URL             = null,
                              I18NString?              Description     = null,
                              Contact?                 Contact         = null,
                              Support?                 Support         = null,
                              PrivacyContact?          PrivacyContact  = null,
                              GeoCoordinate?           GeoLocation     = null,
                              IEnumerable<PublicKey>?  PublicKeys      = null,
                              IEnumerable<String>?     Context         = null)
    {

        #region Properties

        /// <summary>The name of the manufacturer.</summary>
        public String?                   Name              { get; } = Name;

        /// <summary>An optional web address of the manufacturer.</summary>
        public String?                   URL               { get; } = URL;

        /// <summary>An optional multi-language description.</summary>
        public I18NString?               Description       { get; } = Description;

        /// <summary>An optional contact.</summary>
        public Contact?                  Contact           { get; } = Contact;

        /// <summary>An optional support.</summary>
        public Support?                  Support           { get; } = Support;

        /// <summary>An optional privacy contact.</summary>
        public PrivacyContact?           PrivacyContact    { get; } = PrivacyContact;

        /// <summary>An optional geographical location.</summary>
        public GeoCoordinate?            GeoLocation       { get; } = GeoLocation;

        /// <summary>Optional public keys of the manufacturer.</summary>
        public IReadOnlyList<PublicKey>  PublicKeys        { get; } = PublicKeys?.ToArray() ?? [];

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>     JSONLDContext     { get; } = Context?.   ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out Manufacturer)

        /// <summary>
        /// Try to parse the given JSON as a manufacturer.
        /// </summary>
        /// <param name="JSON">A JSON representation of a manufacturer.</param>
        /// <param name="Manufacturer">The parsed manufacturer.</param>
        public static Boolean TryParse(JObject JSON, out Manufacturer? Manufacturer)
        {

            Contact?        contact        = null;
            Support?        support        = null;
            PrivacyContact? privacyContact = null;

            if (JSON["contact"]        is JObject contactJSON)
                chargy.Contact.       TryParse(contactJSON,        out contact);

            if (JSON["support"]        is JObject supportJSON)
                chargy.Support.       TryParse(supportJSON,        out support);

            if (JSON["privacyContact"] is JObject privacyContactJSON)
                chargy.PrivacyContact.TryParse(privacyContactJSON, out privacyContact);

            Manufacturer = new Manufacturer(
                               JSON["name"]?.Value<String>(),
                               JSON["url"]?. Value<String>(),
                               JSON["description"] is JObject descriptionJSON
                                   ? I18NString.Parse(descriptionJSON)
                                   : null,
                               contact,
                               support,
                               privacyContact,
                               JSON["geoLocation"] is JObject geoLocationJSON
                                   ? GeoCoordinate.TryParse(geoLocationJSON)
                                   : null,
                               PublicKeyList.Parse(JSON["publicKeys"]),
                               PublicKey.ParseContext(JSON["@context"])
                           );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this manufacturer.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",        JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",        new JArray(JSONLDContext)));

            if (Name           is not null)
                json.Add(new JProperty("name",            Name));

            if (URL            is not null)
                json.Add(new JProperty("url",             URL));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",     Description.ToJSON()));

            if (Contact        is not null)
                json.Add(new JProperty("contact",         Contact.ToJSON()));

            if (Support        is not null)
                json.Add(new JProperty("support",         Support.ToJSON()));

            if (PrivacyContact is not null)
                json.Add(new JProperty("privacyContact",  PrivacyContact.ToJSON()));

            if (GeoLocation.HasValue)
                json.Add(new JProperty("geoLocation",     GeoLocation.Value.ToJSON()));

            if (PublicKeys.Count > 0)
                json.Add(new JProperty("publicKeys",      new JArray(PublicKeys.Select(publicKey => publicKey.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this manufacturer.
        /// </summary>
        public override String ToString()

            => Name ?? "<unknown manufacturer>";

        #endregion


    }


    /// <summary>
    /// The model of a charging station or an energy meter.
    /// </summary>
    /// <param name="Name">An optional model name.</param>
    /// <param name="URL">An optional URL with more information about the model.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    public class DeviceModel(String?               Name     = null,
                             String?               URL      = null,
                             IEnumerable<String>?  Context  = null)
    {

        #region Properties

        /// <summary>An optional model name.</summary>
        public String?                Name             { get; } = Name;

        /// <summary>An optional URL with more information about the model.</summary>
        public String?                URL              { get; } = URL;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>  JSONLDContext    { get; } = Context?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out DeviceModel)

        /// <summary>
        /// Try to parse the given JSON as a device model.
        /// </summary>
        /// <param name="JSON">A JSON representation of a device model.</param>
        /// <param name="DeviceModel">The parsed device model.</param>
        public static Boolean TryParse(JObject JSON, out DeviceModel? DeviceModel)
        {

            DeviceModel = new DeviceModel(
                              JSON["name"]?.Value<String>(),
                              JSON["url"]?. Value<String>(),
                              PublicKey.ParseContext(JSON["@context"])
                          );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this device model.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",  JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",  new JArray(JSONLDContext)));

            if (Name is not null)
                json.Add(new JProperty("name",      Name));

            if (URL  is not null)
                json.Add(new JProperty("url",       URL));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this device model.
        /// </summary>
        public override String ToString()

            => Name ?? "<unknown model>";

        #endregion


    }


    /// <summary>
    /// The hardware revision of a charging station or an energy meter.
    /// </summary>
    /// <param name="Revision">An optional hardware revision.</param>
    /// <param name="SerialNumber">An optional serial number.</param>
    /// <param name="URL">An optional URL with more information about the hardware.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    public class Hardware(String?               Revision      = null,
                          String?               SerialNumber  = null,
                          String?               URL           = null,
                          IEnumerable<String>?  Context       = null)
    {

        #region Properties

        /// <summary>An optional hardware revision.</summary>
        public String?                Revision         { get; } = Revision;

        /// <summary>An optional serial number.</summary>
        public String?                SerialNumber     { get; } = SerialNumber;

        /// <summary>An optional URL with more information about the hardware.</summary>
        public String?                URL              { get; } = URL;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>  JSONLDContext    { get; } = Context?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out Hardware)

        /// <summary>
        /// Try to parse the given JSON as hardware information.
        /// </summary>
        /// <param name="JSON">A JSON representation of hardware information.</param>
        /// <param name="Hardware">The parsed hardware information.</param>
        public static Boolean TryParse(JObject JSON, out Hardware? Hardware)
        {

            Hardware = new Hardware(
                           JSON["revision"]?.    Value<String>(),
                           JSON["serialNumber"]?.Value<String>(),
                           JSON["url"]?.         Value<String>(),
                           PublicKey.ParseContext(JSON["@context"])
                       );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this hardware information.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",      JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",      new JArray(JSONLDContext)));

            if (Revision     is not null)
                json.Add(new JProperty("revision",      Revision));

            if (SerialNumber is not null)
                json.Add(new JProperty("serialNumber",  SerialNumber));

            if (URL          is not null)
                json.Add(new JProperty("url",           URL));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this hardware information.
        /// </summary>
        public override String ToString()

            => Revision ?? SerialNumber ?? "<unknown hardware>";

        #endregion


    }


    /// <summary>
    /// The firmware of a charging station or an energy meter, including the
    /// checksums that make it identifiable.
    ///
    /// Under the German Calibration Law the legally relevant firmware has to be
    /// identifiable, which is why the checksum matters as much as the version.
    /// </summary>
    /// <param name="Version">An optional firmware version.</param>
    /// <param name="ReleaseDate">An optional release date.</param>
    /// <param name="URL">An optional URL with more information about the firmware.</param>
    /// <param name="Checksum">An optional checksum of the firmware.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="Components">Optional individual firmware components.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    public class Firmware(String?                          Version      = null,
                          String?                          ReleaseDate  = null,
                          String?                          URL          = null,
                          String?                          Checksum     = null,
                          I18NString?                      Description  = null,
                          IEnumerable<FirmwareComponent>?  Components   = null,
                          IEnumerable<String>?             Context      = null)
    {

        #region Properties

        /// <summary>An optional firmware version.</summary>
        public String?                            Version          { get; } = Version;

        /// <summary>An optional release date.</summary>
        public String?                            ReleaseDate      { get; } = ReleaseDate;

        /// <summary>An optional URL with more information about the firmware.</summary>
        public String?                            URL              { get; } = URL;

        /// <summary>An optional checksum of the firmware.</summary>
        public String?                            Checksum         { get; } = Checksum;

        /// <summary>An optional multi-language description.</summary>
        public I18NString?                        Description      { get; } = Description;

        /// <summary>Optional individual firmware components.</summary>
        public IReadOnlyList<FirmwareComponent>   Components       { get; } = Components?.ToArray() ?? [];

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>              JSONLDContext    { get; } = Context?.   ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out Firmware)

        /// <summary>
        /// Try to parse the given JSON as firmware information.
        /// </summary>
        /// <param name="JSON">A JSON representation of firmware information.</param>
        /// <param name="Firmware">The parsed firmware information.</param>
        public static Boolean TryParse(JObject JSON, out Firmware? Firmware)
        {

            var components = new List<FirmwareComponent>();

            if (JSON["components"] is JArray componentArray)
                foreach (var componentJSON in componentArray.OfType<JObject>())
                    if (FirmwareComponent.TryParse(componentJSON, out var component))
                        components.Add(component!);

            Firmware = new Firmware(
                           JSON["version"]?.    Value<String>(),
                           JSON["releaseDate"]?.Value<String>(),
                           JSON["url"]?.        Value<String>(),
                           JSON["checksum"]?.   Value<String>(),
                           JSON["description"] is JObject descriptionJSON
                               ? I18NString.Parse(descriptionJSON)
                               : null,
                           components,
                           PublicKey.ParseContext(JSON["@context"])
                       );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this firmware information.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",     JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",     new JArray(JSONLDContext)));

            if (Version     is not null)
                json.Add(new JProperty("version",      Version));

            if (ReleaseDate is not null)
                json.Add(new JProperty("releaseDate",  ReleaseDate));

            if (URL         is not null)
                json.Add(new JProperty("url",          URL));

            if (Checksum    is not null)
                json.Add(new JProperty("checksum",     Checksum));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",  Description.ToJSON()));

            if (Components.Count > 0)
                json.Add(new JProperty("components",   new JArray(Components.Select(component => component.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this firmware information.
        /// </summary>
        public override String ToString()

            => Version ?? "<unknown firmware>";

        #endregion


    }


    /// <summary>
    /// One individually versioned part of a firmware.
    /// </summary>
    /// <param name="Id">The identification of the firmware component.</param>
    /// <param name="Version">An optional version.</param>
    /// <param name="Checksum">An optional checksum.</param>
    /// <param name="ReleaseDate">An optional release date.</param>
    /// <param name="URL">An optional URL with more information.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    public class FirmwareComponent(String?               Id           = null,
                                   String?               Version      = null,
                                   String?               Checksum     = null,
                                   String?               ReleaseDate  = null,
                                   String?               URL          = null,
                                   I18NString?           Description  = null,
                                   IEnumerable<String>?  Context      = null)
    {

        #region Properties

        /// <summary>The identification of the firmware component.</summary>
        public String?                Id               { get; } = Id;

        /// <summary>An optional version.</summary>
        public String?                Version          { get; } = Version;

        /// <summary>An optional checksum.</summary>
        public String?                Checksum         { get; } = Checksum;

        /// <summary>An optional release date.</summary>
        public String?                ReleaseDate      { get; } = ReleaseDate;

        /// <summary>An optional URL with more information.</summary>
        public String?                URL              { get; } = URL;

        /// <summary>An optional multi-language description.</summary>
        public I18NString?            Description      { get; } = Description;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>  JSONLDContext    { get; } = Context?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out FirmwareComponent)

        /// <summary>
        /// Try to parse the given JSON as a firmware component.
        /// </summary>
        /// <param name="JSON">A JSON representation of a firmware component.</param>
        /// <param name="FirmwareComponent">The parsed firmware component.</param>
        public static Boolean TryParse(JObject JSON, out FirmwareComponent? FirmwareComponent)
        {

            FirmwareComponent = new FirmwareComponent(
                                    JSON["@id"]?.        Value<String>(),
                                    JSON["version"]?.    Value<String>(),
                                    JSON["checksum"]?.   Value<String>(),
                                    JSON["releaseDate"]?.Value<String>(),
                                    JSON["url"]?.        Value<String>(),
                                    JSON["description"] is JObject descriptionJSON
                                        ? I18NString.Parse(descriptionJSON)
                                        : null,
                                    PublicKey.ParseContext(JSON["@context"])
                                );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this firmware component.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Id is not null)
                json.Add(new JProperty("@id",          Id));

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",     JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",     new JArray(JSONLDContext)));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",  Description.ToJSON()));

            if (Version     is not null)
                json.Add(new JProperty("version",      Version));

            if (ReleaseDate is not null)
                json.Add(new JProperty("releaseDate",  ReleaseDate));

            if (Checksum    is not null)
                json.Add(new JProperty("checksum",     Checksum));

            if (URL         is not null)
                json.Add(new JProperty("url",          URL));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this firmware component.
        /// </summary>
        public override String ToString()

            => $"{Id ?? "<unknown component>"}{(Version is not null ? $" v{Version}" : "")}";

        #endregion


    }

}
