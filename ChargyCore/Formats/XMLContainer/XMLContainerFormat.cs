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

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.IO;

#endregion

namespace cloud.charging.open.chargy.Formats.XMLContainer
{

    /// <summary>
    /// The generic XML container format.
    ///
    /// A plainer relative of the SAFE container: a list of signed meter values,
    /// each with its own public key, signature, signature method and encoding, and
    /// nothing at all about the charging station. It exists because several vendors
    /// ship this shape, and Chargy has to be able to say something better about it
    /// than "unreadable file".
    ///
    /// What it can say is limited, and deliberately so. The container is read and
    /// checked for internal consistency — one public key throughout, one signature
    /// method, one encoding — and then reported as a format whose signed values
    /// cannot be turned into a charging session. **ChargyCore.TS stops at the same
    /// point**: the conversion is left as a ToDo there, and inventing one here
    /// would mean this port claiming to verify something the reference
    /// implementation does not.
    ///
    /// The consistency checks are not wasted for that. A container that mixes two
    /// public keys is a file assembled from several charging sessions, and saying
    /// so is more useful than saying nothing.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    public partial class XMLContainerFormat(I18NDictionary I18N) : IXMLChargeTransparencyFormat
    {

        #region Data

        private readonly I18NDictionary i18n = I18N;

        #endregion

        #region Properties

        /// <summary>The name of the data format.</summary>
        public String Name
            => "XML container";

        #endregion


        #region TryParseXML(Document)

        /// <summary>
        /// Try to read a charge transparency record from a generic XML container.
        /// </summary>
        /// <param name="Document">An XML document.</param>
        public Object TryParseXML(XDocument Document)
        {

            try
            {

                var publicKey             = "";
                var signatureMethod       = "";
                var encodingMethod        = "";
                var meterValueSignatures  = new List<String>();
                var encodedMeterValues    = new List<String>();

                var valueLists = ElementsByLocalName(Document, "signedMeterValues").ToArray();

                if (valueLists.Length == 1)
                    foreach (var value in ElementsByLocalName(valueLists[0], "signedMeterValue"))
                    {

                        #region The public key, which this container makes optional

                        var publicKeyElement = ElementsByLocalName(value, "publicKey").FirstOrDefault();

                        if (publicKeyElement is not null)
                        {

                            var decoded = Decode(publicKeyElement);

                            if (decoded is null)
                                return Invalid("Unkown public key encoding within the given XML container!");

                            if (decoded.Length == 0)
                                return Invalid("The public key within the given XML container must not be empty!");

                            if (publicKey.Length == 0)
                                publicKey = decoded;

                            else if (decoded != publicKey)
                                return Invalid("Invalid mixture of different public keys within the given XML container!");

                        }

                        #endregion

                        #region The signature over the meter value

                        var signatureElement = ElementsByLocalName(value, "meterValueSignature").FirstOrDefault();

                        if (signatureElement is not null)
                        {

                            var decoded = Decode(signatureElement);

                            if (decoded is null)
                                return Invalid("Unkown meter value signature encoding within the given XML container!");

                            if (decoded.Length == 0)
                                return Invalid("The meter value signature within the given XML container must not be empty!");

                            meterValueSignatures.Add(decoded);

                        }

                        #endregion

                        #region ..., and every value has to agree with the others about how it was signed

                        var currentSignatureMethod = TextOf(value, "signatureMethod");

                        if (signatureMethod.Length == 0)
                            signatureMethod = currentSignatureMethod;

                        else if (currentSignatureMethod != signatureMethod)
                            return Invalid("Invalid mixture of different signature methods within the given XML container!");

                        var currentEncodingMethod = TextOf(value, "encodingMethod");

                        if (encodingMethod.Length == 0)
                            encodingMethod = currentEncodingMethod;

                        else if (currentEncodingMethod != encodingMethod)
                            return Invalid("Invalid mixture of different signed data formats within the given XML container!");

                        #endregion

                        #region The signed meter value itself, without which there is nothing to verify

                        var encodedValue = ElementsByLocalName(value, "encodedMeterValue").FirstOrDefault();

                        if (encodedValue is null)
                            return Invalid("The signed data tag within the given XML container must not be empty!");

                        var encodedText = Decode(encodedValue);

                        if (encodedText is null)
                            return Invalid("Unkown signed data encoding within the given XML container!");

                        if (encodedText.Length == 0)
                            return Invalid("The signed data value within the given XML container must not be empty!");

                        encodedMeterValues.Add(encodedText);

                        #endregion

                    }

                // Everything above holds together, and still nothing can be made of
                // it: which format the signed values are in is not something this
                // container says, and ChargyCore.TS does not work it out either.
                return Invalid("UnknownOrInvalidChargingSessionFormat");

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


        #region (private) Invalid   (MessageKey)

        /// <summary>
        /// Report that the document is not a usable XML container.
        /// </summary>
        /// <param name="MessageKey">The i18n key of the reason.</param>
        private SessionCryptoResult Invalid(String MessageKey)

            => new (
                   SessionVerificationResult.InvalidSessionFormat,
                   i18n.GetMultilanguageText(MessageKey)
               );

        #endregion

        #region (private, static) Decode    (Element)

        /// <summary>
        /// The contents of an element, decoded the way its "encoding" attribute
        /// says — or null when it names an encoding this container does not have.
        /// </summary>
        /// <param name="Element">An element carrying encoded text.</param>
        private static String? Decode(XElement Element)
        {

            var encoding = Element.Attribute("encoding")?.Value.Trim().ToLowerInvariant() ?? "";
            var text     = Element.Value.Trim();

            try
            {

                return encoding switch {

                           ""       or
                           "plain"  => text,

                           "base32" => Encoding.UTF8.GetString(text.FromBASE32()).Trim(),
                           "base64" => Encoding.UTF8.GetString(Convert.FromBase64String(text)).Trim(),

                           // Some people put whitespace, '-' or ':' into the
                           // hexadecimal form.
                           "hex"    => Encoding.UTF8.GetString(
                                           Convert.FromHexString(NonHexRegex().Replace(text, ""))
                                       ).Trim(),

                           _        => null

                       };

            }
            catch (Exception)
            {
                return null;
            }

        }

        #endregion

        #region (private, static) XML helpers

        /// <summary>Every descendant with the given local name, whatever its namespace.</summary>
        private static IEnumerable<XElement> ElementsByLocalName(XContainer  Parent,
                                                                 String      LocalName)

            => Parent.Descendants().Where(element => element.Name.LocalName == LocalName);

        /// <summary>The trimmed, lower cased text of the first descendant with the given local name.</summary>
        private static String TextOf(XElement  Parent,
                                     String    LocalName)

            => ElementsByLocalName(Parent, LocalName).FirstOrDefault()?.Value.Trim().ToLowerInvariant() ?? "";

        #endregion

        #region (private) Regular expressions

        [GeneratedRegex("[^a-fA-F0-9]")]
        private static partial Regex NonHexRegex();

        #endregion


    }

}
