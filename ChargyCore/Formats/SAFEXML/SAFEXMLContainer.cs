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

using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using org.GraphDefined.Vanaheimr.Aegir;
using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.IO;
using cloud.charging.open.chargy.Formats.Alfen;
using cloud.charging.open.chargy.Formats.EDL40;
using cloud.charging.open.chargy.Formats.OCMF;

#endregion

namespace cloud.charging.open.chargy.Formats.SAFEXML
{

    /// <summary>
    /// The SAFE transparency XML container.
    ///
    /// See https://github.com/SAFE-eV/transparenzsoftware/blob/archive/XML_Format.md
    ///
    /// The container itself signs nothing. It carries signed meter values of some
    /// other format — Alfen, OCMF or EDL40 — and adds what those values cannot
    /// carry: which charging station and which EVSE they came from. The
    /// distinction matters to an EV driver, because the station name in this
    /// container is a claim by whoever wrote the file, while the meter readings
    /// inside it are a claim by the meter.
    ///
    /// The SAFE transparency software v1.0 does not declare its own XML namespace,
    /// so a document may arrive with the namespace, with an empty one, or with
    /// none at all. All three are the same format and all three are accepted.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="Alfen">The Alfen format, for containers carrying Alfen data.</param>
    /// <param name="OCMF">The OCMF format, for containers carrying OCMF data.</param>
    /// <param name="EDL40">The EDL40 format, for containers carrying SML data.</param>
    public partial class SAFEXMLContainer(I18NDictionary  I18N,
                                          AlfenFormat?    Alfen = null,
                                          OCMFFormat?     OCMF  = null,
                                          EDL40Format?    EDL40 = null) : IXMLChargeTransparencyFormat
    {

        #region Data

        private readonly I18NDictionary  i18n   = I18N;
        private readonly AlfenFormat?    alfen  = Alfen;
        private readonly OCMFFormat?     ocmf   = OCMF;
        private readonly EDL40Format?    edl40  = EDL40;

        #endregion

        #region Properties

        /// <summary>The name of the data format.</summary>
        public String Name
            => "SAFE XML";

        #endregion


        #region TryParseXML(Document)

        /// <summary>
        /// Try to read a charge transparency record from a SAFE XML container.
        /// </summary>
        /// <param name="Document">An XML document.</param>
        public Object TryParseXML(XDocument Document)
        {

            try
            {

                if (Document.Root is not XElement root ||
                    root.Name.LocalName != "values")
                    return Invalid("UnknownOrInvalidChargingSessionFormat");

                var containerInfos  = ParseContainerInfos(Document);

                var signedValues    = new List<String>();
                var signedFormat    = "";
                var signedEncoding  = "";
                var publicKeyText   = "";
                var keyEncoding     = "";

                foreach (var value in ElementsByLocalName(Document, "value"))
                {

                    #region Every value has to carry signed data

                    var signedData = ElementsByLocalName(value, "signedData").FirstOrDefault();

                    if (signedData is null)
                        return Invalid("Each value within the given XML container must contain signed data!");

                    #endregion

                    #region ..., and every value has to agree with the others about how

                    var format = AttributeOf(signedData, "format");

                    if (signedFormat.Length == 0 && format.Length > 0)
                        signedFormat = format;
                    else if (format != signedFormat)
                        return Invalid("Invalid mixture of different signed data formats within the given XML container!");

                    var encoding = AttributeOf(signedData, "encoding");

                    if (signedEncoding.Length == 0 && encoding.Length > 0)
                        signedEncoding = encoding;
                    else if (encoding != signedEncoding)
                        return Invalid("Invalid mixture of different signed data encodings within the given XML container!");

                    #endregion

                    var signedValue = signedData.Value.Trim();

                    if (signedValue.Length == 0)
                        return Invalid("The signed data value within the given XML container must not be empty!");

                    #region The public key, which some formats carry themselves and therefore may omit here

                    var publicKey         = ElementsByLocalName(value, "publicKey").FirstOrDefault();
                    var publicKeyEncoding = publicKey is not null ? AttributeOf(publicKey, "encoding") : "";

                    if (keyEncoding.Length == 0 && publicKeyEncoding.Length > 0)
                        keyEncoding = publicKeyEncoding;
                    else if (publicKeyEncoding != keyEncoding)
                        return Invalid("Invalid mixture of different public key encodings within the given XML container!");

                    var publicKeyValue = publicKey is not null
                                             ? WhitespaceRegex().Replace(publicKey.Value.Trim(), "")
                                             : "";

                    if (publicKeyText.Length == 0 && publicKeyValue.Length > 0)
                        publicKeyText = publicKeyValue;
                    else if (publicKeyValue != publicKeyText)
                        return Invalid("Invalid mixture of different public keys within the given XML container!");

                    #endregion

                    #region Decode the signed data into the text its own format expects

                    var decoded = DecodeSignedData(signedValue, signedEncoding);

                    if (decoded is null)
                        return Invalid("Unkown signed data encoding within the given SAFE XML!");

                    signedValues.Add(decoded);

                    #endregion

                }

                if (signedValues.Count == 0)
                    return Invalid("UnknownOrInvalidChargingSessionFormat");

                #region Hand the signed values to the format that produced them

                switch (signedFormat)
                {

                    case "alfen":
                        return alfen is not null
                                   ? alfen.TryParse(signedValues, containerInfos)
                                   : Unsupported();

                    case "ocmf":
                        return ocmf is not null
                                   // The container carries the public key that the
                                   // OCMF documents themselves do not.
                                   ? ocmf.TryParse(signedValues, publicKeyText, keyEncoding, containerInfos)
                                   : Unsupported();

                    case "edl_40_p":
                    case "isa_edl_40_p":
                    case "sml_edl40_p":
                        return edl40 is not null
                                   // An SML message carries no public key of its
                                   // own, so without the container's one nothing
                                   // about it can be checked.
                                   ? edl40.TryParse(signedValues, publicKeyText, containerInfos)
                                   : Unsupported();

                    default:
                        return Invalid("UnknownOrInvalidChargingSessionFormat");

                }

                #endregion

            }
            catch (Exception exception)
            {
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSessionFormat,
                           i18n.GetMultilanguageText($"Exception occured: {exception.Message}"),
                           Exception: exception
                       );
            }

        }

        #endregion

        #region ParseContainerInfos(Document)

        /// <summary>
        /// Read what the container says about the charging station the signed
        /// values came from.
        ///
        /// None of this is signed, and the warnings collected here are how an EV
        /// driver gets told that the container described its own installation
        /// inconsistently.
        /// </summary>
        /// <param name="Document">An XML document.</param>
        public ContainerInfos ParseContainerInfos(XDocument Document)
        {

            var containerInfos = new ContainerInfos();

            void AddWarning(String MessageKey)
                => containerInfos.AddWarning(new Warning(i18n.GetMultilanguageText(MessageKey)));

            #region The charging station

            var chargingStationElements = ElementsByLocalName(Document, "chargingStation").ToArray();

            if (chargingStationElements.Length == 0)
                return containerInfos;

            if (chargingStationElements.Length > 1)
                AddWarning("Only one chargingStation element is allowed within the given SAFE XML container!");

            var chargingStationElement  = chargingStationElements[0];
            var chargingStationId       = chargingStationElement.Attribute("id")?.Value.Trim();

            if (chargingStationId is null || chargingStationId.Length == 0)
            {
                AddWarning("The chargingStation identifier within the given SAFE XML container is invalid!");
                return containerInfos;
            }

            #endregion

            #region The EVSE and its connector

            var evses = new List<EVSE>();

            var evseElements = ElementsByLocalName(chargingStationElement, "EVSE").ToArray();

            if (evseElements.Length > 1)
                AddWarning("Only one EVSE element is allowed within the given SAFE XML chargingStation element!");

            if (evseElements.Length == 0)
                AddWarning("The EVSE element within the given SAFE XML chargingStation element is invalid!");

            else
            {

                var evseElement  = evseElements[0];
                var evseId       = evseElement.Attribute("id")?.Value.Trim();

                if (evseId is not null && evseId.Length > 0)
                {

                    var connectors        = new List<Connector>();
                    var connectorElements = ElementsByLocalName(evseElement, "connector").ToArray();

                    if (connectorElements.Length > 1)
                        AddWarning("Only one connector element is allowed within the given SAFE XML EVSE element!");

                    if (connectorElements.Length == 0)
                        AddWarning("The connector element within the given SAFE XML EVSE element is invalid!");

                    else
                    {

                        var connectorElement  = connectorElements[0];
                        var connectorId       = connectorElement.Attribute("id")?.Value.Trim();
                        var connectorType     = DirectChildText(connectorElement, "type");

                        if (connectorId is not null && connectorId.Length > 0 ||
                            connectorType is not null && connectorType.Length > 0)
                        {
                            connectors.Add(new Connector(connectorId, connectorType));
                        }

                    }

                    evses.Add(
                        new EVSE(
                            evseId,
                            Description:  ParseDescription(evseElement),
                            Connectors:   connectors
                        )
                    );

                }

            }

            #endregion

            #region The firmware and the geographical location

            var firmwareElement  = DirectChild(chargingStationElement, "firmware");

            var firmware         = firmwareElement is not null
                                       ? new Firmware(
                                             Version:   DirectChildText(firmwareElement, "version"),
                                             Checksum:  DirectChildText(firmwareElement, "checksum")
                                         )
                                       : null;

            GeoCoordinate? geoLocation = null;

            if (DirectChild(chargingStationElement, "geoLocation") is XElement geoLocationElement &&
                Double.TryParse(DirectChildText(geoLocationElement, "latitude"),  System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var latitude) &&
                Double.TryParse(DirectChildText(geoLocationElement, "longitude"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var longitude))
            {
                geoLocation = GeoCoordinate.Create(Latitude.Parse(latitude), Longitude.Parse(longitude));
            }

            #endregion

            containerInfos.AddChargingStation(
                new ChargingStation(
                    chargingStationId,
                    Description:  ParseDescription(chargingStationElement),
                    Firmware:     firmware,
                    GeoLocation:  geoLocation,
                    EVSEs:        evses
                )
            );

            return containerInfos;

        }

        #endregion


        #region (private) Invalid    (MessageKey)

        /// <summary>
        /// Report that the document is not a valid SAFE XML container.
        /// </summary>
        /// <param name="MessageKey">The i18n key of the reason.</param>
        private SessionCryptoResult Invalid(String MessageKey)

            => new (
                   SessionVerificationResult.InvalidSessionFormat,
                   i18n.GetMultilanguageText(MessageKey)
               );

        #endregion

        #region (private) Unsupported()

        /// <summary>
        /// Report that the container carries a format Chargy was not built with.
        /// </summary>
        private SessionCryptoResult Unsupported()

            => new (
                   SessionVerificationResult.UnknownCTRFormat,
                   i18n.GetMultilanguageText("UnknownOrInvalidChargingSessionFormat")
               );

        #endregion

        #region (private, static) DecodeSignedData(SignedValue, Encoding)

        /// <summary>
        /// Decode the signed data into the text its own format expects.
        ///
        /// A container may carry the very same signed value as plain text, base32,
        /// base64 or hexadecimal — the choice is about how the file travels, not
        /// about what it says.
        /// </summary>
        /// <param name="SignedValue">The encoded signed data.</param>
        /// <param name="Encoding">How it was encoded.</param>
        private static String? DecodeSignedData(String  SignedValue,
                                                String  Encoding)
        {

            try
            {

                switch (Encoding)
                {

                    case "":
                    case "plain":
                        return SignedValue.Trim();

                    case "base32":
                        return System.Text.Encoding.UTF8.GetString(SignedValue.FromBASE32()).Trim();

                    case "base64":
                        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(SignedValue)).Trim();

                    // Some people put whitespace, '-' or ':' into the hexadecimal form.
                    case "hex":
                        return System.Text.Encoding.UTF8.GetString(
                                   Convert.FromHexString(NonHexRegex().Replace(SignedValue, ""))
                               ).Trim();

                    default:
                        return null;

                }

            }
            catch (Exception)
            {
                return null;
            }

        }

        #endregion

        #region (private, static) XML helpers

        /// <summary>
        /// Every descendant with the given local name, whatever its namespace.
        ///
        /// The namespace cannot be relied upon: the SAFE transparency software
        /// v1.0 does not declare its own.
        /// </summary>
        private static IEnumerable<XElement> ElementsByLocalName(XContainer  Parent,
                                                                 String      LocalName)

            => Parent.Descendants().Where(element => element.Name.LocalName == LocalName);

        /// <summary>The direct child with the given local name.</summary>
        private static XElement? DirectChild(XElement  Parent,
                                             String    LocalName)

            => Parent.Elements().FirstOrDefault(element => element.Name.LocalName == LocalName);

        /// <summary>The trimmed text of the direct child with the given local name.</summary>
        private static String? DirectChildText(XElement  Parent,
                                               String    LocalName)
        {

            var text = DirectChild(Parent, LocalName)?.Value.Trim();

            return text is not null && text.Length > 0
                       ? text
                       : null;

        }

        /// <summary>The trimmed, lower cased value of an attribute.</summary>
        private static String AttributeOf(XElement  Element,
                                          String    AttributeName)

            => Element.Attribute(AttributeName)?.Value.Trim().ToLowerInvariant() ?? "";

        /// <summary>
        /// The "description" children of an element, as a multi-language text.
        /// </summary>
        private static I18NString? ParseDescription(XElement Parent)
        {

            var description = I18NString.Empty;

            foreach (var element in Parent.Elements().Where(element => element.Name.LocalName == "description"))
            {

                var text = element.Value.Trim();

                if (text.Length == 0)
                    continue;

                description = description.Set(
                                  LanguagesExtensions.TryParse(element.Attribute("language")?.Value.Trim() ?? "en") ?? Languages.en,
                                  text
                              );

            }

            return description.IsNotNullOrEmpty()
                       ? description
                       : null;

        }

        #endregion

        #region (private) Regular expressions

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();

        [GeneratedRegex("[^a-fA-F0-9]")]
        private static partial Regex NonHexRegex();

        #endregion


    }

}
