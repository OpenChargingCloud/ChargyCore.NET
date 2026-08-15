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

using System.Xml.Linq;

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.chargy.IO
{

    /// <summary>
    /// One of the charge transparency data formats.
    /// </summary>
    public interface IChargeTransparencyFormat
    {

        /// <summary>The name of the data format, e.g. "OCMF" or "Alfen".</summary>
        String Name { get; }

    }


    /// <summary>
    /// A charge transparency data format written as XML.
    /// </summary>
    public interface IXMLChargeTransparencyFormat : IChargeTransparencyFormat
    {

        /// <summary>
        /// Try to read a charge transparency record from an XML document.
        /// </summary>
        /// <param name="Document">An XML document.</param>
        /// <returns>
        /// A <see cref="ChargeTransparencyRecord"/>, or a
        /// <see cref="SessionCryptoResult"/> saying why it is not one.
        /// </returns>
        Object TryParseXML(XDocument Document);

    }


    /// <summary>
    /// A charge transparency data format written as text, e.g. OCMF or the Alfen
    /// format an EV driver reads off a charging station's display.
    /// </summary>
    public interface ITextChargeTransparencyFormat : IChargeTransparencyFormat
    {

        /// <summary>
        /// Whether this format recognises the given text as its own.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        Boolean CanParse(String Text);

        /// <summary>
        /// Try to read a charge transparency record from text.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        /// <param name="PublicKeyHEX">
        /// An optional public key that arrived alongside the data, because these
        /// formats routinely carry no key of their own.
        /// </param>
        Object TryParseText(String   Text,
                            String?  PublicKeyHEX = null);

    }


    /// <summary>
    /// A charge transparency data format written as JSON.
    /// </summary>
    public interface IJSONChargeTransparencyFormat : IChargeTransparencyFormat
    {

        /// <summary>
        /// Try to read a charge transparency record from a JSON object.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        Object TryParseJSON(JObject JSON);

    }


    /// <summary>
    /// The charge transparency data formats Chargy can read.
    ///
    /// The formats sit in named slots rather than in a list, because the order in
    /// which they are tried is part of the behaviour, not an implementation
    /// detail: several formats would happily accept another's data and produce a
    /// plausible but wrong reading of a charging session. Leaving a slot empty
    /// disables that format, which is how an application can restrict itself to
    /// the formats it is willing to vouch for.
    /// </summary>
    public class ChargeTransparencyFormats
    {

        #region XML formats

        /// <summary>The Mennekes XML format.</summary>
        public IXMLChargeTransparencyFormat?   Mennekes       { get; init; }

        /// <summary>The SAFE transparency XML format.</summary>
        public IXMLChargeTransparencyFormat?   SAFEXML        { get; init; }

        /// <summary>The generic XML container format.</summary>
        public IXMLChargeTransparencyFormat?   XMLContainer   { get; init; }

        #endregion

        #region Text formats

        /// <summary>The Open Charge Metering Format.</summary>
        public ITextChargeTransparencyFormat?  OCMF           { get; init; }

        /// <summary>The Porsche Charging Data Format.</summary>
        public ITextChargeTransparencyFormat?  PCDF           { get; init; }

        /// <summary>The Alfen format.</summary>
        public ITextChargeTransparencyFormat?  Alfen          { get; init; }

        #endregion

        #region Container-only formats

        /// <summary>
        /// The EDL40 and ISA-EDL40 formats.
        ///
        /// These have no slot among the text formats because they are never
        /// detected on their own: an SML message carries no public key, so it can
        /// only be read inside a container that supplies one.
        /// </summary>
        public Formats.EDL40.EDL40Format?      EDL40          { get; init; }

        #endregion

        #region JSON formats

        /// <summary>The PTB container format.</summary>
        public IJSONChargeTransparencyFormat?  PTB            { get; init; }

        /// <summary>The chargeIT mobility container format.</summary>
        public IJSONChargeTransparencyFormat?  ChargeIT       { get; init; }

        /// <summary>
        /// The BAUER Electronic BSM-WS36A meter value format.
        ///
        /// Like EDL40 this has no detector of its own: BSM snapshots only ever
        /// arrive inside a chargeIT container, which is what supplies the place
        /// they were taken at.
        /// </summary>
        public Formats.BSM.BSMFormat?          BSM            { get; init; }

        /// <summary>The ChargePoint format.</summary>
        public IJSONChargeTransparencyFormat?  ChargePoint    { get; init; }

        /// <summary>The Open Charge Point Interface format.</summary>
        public IJSONChargeTransparencyFormat?  OCPI           { get; init; }

        #endregion


        #region Data

        /// <summary>
        /// No formats at all.
        ///
        /// Everything around the formats — unpacking containers, reading QR codes,
        /// recognising public key files, resolving URLs — works without a single
        /// format being registered, and is tested that way.
        /// </summary>
        public static ChargeTransparencyFormats None { get; } = new ();

        #endregion

        #region (static) All()

        /// <summary>
        /// Every charge transparency data format this library implements.
        ///
        /// This is what an application wants unless it has a reason to be
        /// pickier. Formats are added here as they are ported.
        /// </summary>
        public static ChargeTransparencyFormats All(I18NDictionary? I18N = null)
        {

            var i18n   = I18N ?? I18NDictionary.Default();
            var alfen  = new Formats.Alfen.AlfenFormat(i18n);
            var ocmf   = new Formats.OCMF.OCMFFormat(i18n);
            var edl40  = new Formats.EDL40.EDL40Format(i18n);
            var bsm    = new Formats.BSM.  BSMFormat  (i18n);

            return new () {
                       Alfen     = alfen,
                       OCMF      = ocmf,
                       EDL40     = edl40,
                       BSM       = bsm,
                       // The containers carry someone else's signed data, so they
                       // have to know the formats they may be carrying.
                       SAFEXML   = new Formats.SAFEXML. SAFEXMLContainer(i18n, alfen, ocmf, edl40),
                       ChargeIT  = new Formats.ChargeIT.ChargeITContainer(i18n, alfen, bsm)
                   };

        }

        #endregion


    }

}
