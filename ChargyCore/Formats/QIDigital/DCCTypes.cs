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

namespace cloud.charging.open.chargy.Formats.QIDigital
{

    /// <summary>
    /// The building blocks of a Digital Calibration Certificate.
    ///
    /// These are a transliteration of the PTB's DCC schema
    /// (https://gitlab.com/ptb/dcc/xsd-dcc), by way of the TypeScript declarations
    /// in ChargyCore.TS. The point of having them in a charge transparency library
    /// is the link they make: a charging station's meter is only trustworthy
    /// because some accredited laboratory calibrated it, and a DCC is that
    /// laboratory's statement, in a form software can check rather than a PDF
    /// somebody has to believe.
    ///
    /// Nothing in ChargyCore.TS reads or writes these — they are declarations for
    /// consumers of the library, and they are equally unexercised here. Anywhere
    /// the schema itself says "type not specified", the value is kept as raw JSON
    /// rather than guessed at.
    /// </summary>
    public static class DCC
    {

        #region (internal, static) JSON helpers

        /// <summary>A string property, or null when it is absent or not a string.</summary>
        internal static String? Text(JObject?  JSON,
                                     String    Key)

            => JSON?[Key]?.Type == JTokenType.String
                   ? JSON[Key]!.Value<String>()
                   : null;

        /// <summary>A boolean property, or null when it is absent or not a boolean.</summary>
        internal static Boolean? Flag(JObject?  JSON,
                                      String    Key)

            => JSON?[Key]?.Type == JTokenType.Boolean
                   ? JSON[Key]!.Value<Boolean>()
                   : null;

        /// <summary>A numeric property, or null when it is absent or not a number.</summary>
        internal static Decimal? Number(JObject?  JSON,
                                        String    Key)

            => JSON?[Key]?.Type == JTokenType.Integer ||
               JSON?[Key]?.Type == JTokenType.Float
                   ? JSON[Key]!.Value<Decimal>()
                   : null;

        /// <summary>An array of strings, or an empty list when it is absent.</summary>
        internal static IEnumerable<String> Texts(JObject?  JSON,
                                                  String    Key)

            => JSON?[Key] is JArray array
                   ? array.Where (element => element.Type == JTokenType.String).
                           Select(element => element.Value<String>()!)
                   : [];

        /// <summary>An array of objects, mapped, or an empty list when it is absent.</summary>
        internal static IEnumerable<T> Array<T>(JObject?           JSON,
                                                String             Key,
                                                Func<JObject, T>   Parse)

            => JSON?[Key] is JArray array
                   ? array.OfType<JObject>().Select(Parse)
                   : [];

        /// <summary>An object property, or null when it is absent or not an object.</summary>
        internal static JObject? Object(JObject?  JSON,
                                        String    Key)

            => JSON?[Key] as JObject;

        /// <summary>A multi-language text, which the DCC writes as a language-keyed object.</summary>
        internal static I18NString? I18N(JObject?  JSON,
                                         String    Key)
        {

            if (JSON?[Key] is not JObject text)
                return null;

            var result = I18NString.Empty;

            foreach (var property in text.Properties())
                if (property.Value.Type == JTokenType.String)
                    result = result.Set(
                                 LanguagesExtensions.TryParse(property.Name) ?? Languages.en,
                                 property.Value.Value<String>()!
                             );

            return result.IsNotNullOrEmpty()
                       ? result
                       : null;

        }

        /// <summary>A timestamp, kept as the text the certificate wrote.</summary>
        internal static String? Timestamp(JObject?  JSON,
                                          String    Key)

            => Text(JSON, Key);

        /// <summary>Add a property when it has a value.</summary>
        internal static void AddIfSet(JObject  JSON,
                                      String   Key,
                                      Object?  Value)
        {

            switch (Value)
            {

                case null:
                    return;

                case String text when text.Length == 0:
                    return;

                case I18NString i18n:
                    if (i18n.IsNotNullOrEmpty())
                        JSON.Add(new JProperty(Key, new JObject(
                            i18n.Select(text => new JProperty(text.Language.ToString(), text.Text))
                        )));
                    return;

                case System.Collections.IEnumerable list and not String:
                    var array = new JArray(list.Cast<Object>().Select(ToToken));
                    if (array.Count > 0)
                        JSON.Add(new JProperty(Key, array));
                    return;

                default:
                    JSON.Add(new JProperty(Key, ToToken(Value)));
                    return;

            }

        }

        /// <summary>One value as JSON, whatever kind of thing it is.</summary>
        private static JToken ToToken(Object Value)

            => Value switch {
                   JToken token         => token,
                   IDCCElement element  => element.ToJSON(),
                   _                    => JToken.FromObject(Value)
               };

        #endregion

    }


    /// <summary>
    /// Anything in a Digital Calibration Certificate that can be written back out.
    /// </summary>
    public interface IDCCElement
    {

        /// <summary>Return a JSON representation of this element.</summary>
        JObject ToJSON();

    }


    /// <summary>
    /// The identification a DCC element may carry so that other elements can point
    /// at it.
    /// </summary>
    /// <param name="Id">An optional identification within the certificate.</param>
    /// <param name="RefIds">Optional identifications of elements this one refers to.</param>
    /// <param name="RefType">An optional reference type.</param>
    public abstract class ADCCElement(String?               Id       = null,
                                      IEnumerable<String>?  RefIds   = null,
                                      String?               RefType  = null) : IDCCElement
    {

        /// <summary>An optional identification within the certificate.</summary>
        public String?                Id         { get; } = Id;

        /// <summary>Optional identifications of elements this one refers to.</summary>
        public IReadOnlyList<String>  RefIds     { get; } = RefIds?.ToArray() ?? [];

        /// <summary>An optional reference type.</summary>
        public String?                RefType    { get; } = RefType;

        /// <summary>Return a JSON representation of this element.</summary>
        public abstract JObject ToJSON();

        /// <summary>Add the identification properties every DCC element may carry.</summary>
        protected JObject WithIdentification(JObject JSON)
        {

            DCC.AddIfSet(JSON, "id",       Id);
            DCC.AddIfSet(JSON, "refId",    RefIds);
            DCC.AddIfSet(JSON, "refType",  RefType);

            return JSON;

        }

    }


    /// <summary>A piece of text in one language.</summary>
    public class DCCStringWithLang(String                Value,
                                  String?               Lang     = null,
                                  String?               Id       = null,
                                  IEnumerable<String>?  RefIds   = null,
                                  String?               RefType  = null)

        : ADCCElement(Id, RefIds, RefType)

    {

        /// <summary>The text.</summary>
        public String   Value    { get; } = Value;

        /// <summary>The ISO 639 language code of the text.</summary>
        public String?  Lang     { get; } = Lang;

        /// <summary>Read a piece of text out of its JSON.</summary>
        public static DCCStringWithLang Parse(JObject JSON)

            => new (
                   DCC.Text (JSON, "value") ?? "",
                   DCC.Text (JSON, "lang"),
                   DCC.Text (JSON, "id"),
                   DCC.Texts(JSON, "refId"),
                   DCC.Text (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this text.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject(new JProperty("value", Value));

            DCC.AddIfSet(json, "lang", Lang);

            return WithIdentification(json);

        }

    }


    /// <summary>A formula, written for humans and for machines.</summary>
    public class Formula(String?               LaTeX    = null,
                         JToken?               MathML   = null,
                         String?               Id       = null,
                         IEnumerable<String>?  RefIds   = null,
                         String?               RefType  = null)

        : ADCCElement(Id, RefIds, RefType)

    {

        /// <summary>The formula as LaTeX.</summary>
        public String?  LaTeX     { get; } = LaTeX;

        /// <summary>The formula as MathML, whose shape the schema does not specify.</summary>
        public JToken?  MathML    { get; } = MathML;

        /// <summary>Read a formula out of its JSON.</summary>
        public static Formula Parse(JObject JSON)

            => new (
                   DCC.Text (JSON, "latex"),
                   JSON["mathml"],
                   DCC.Text (JSON, "id"),
                   DCC.Texts(JSON, "refId"),
                   DCC.Text (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this formula.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "latex",   LaTeX);
            DCC.AddIfSet(json, "mathml",  MathML);

            return WithIdentification(json);

        }

    }


    /// <summary>A binary file carried inside the certificate.</summary>
    public class ByteData(String                FileName,
                          String                MimeType,
                          String                DataBase64,
                          I18NString?           Name         = null,
                          RichContent?          Description  = null,
                          String?               Id           = null,
                          IEnumerable<String>?  RefIds       = null,
                          String?               RefType      = null)

        : ADCCElement(Id, RefIds, RefType)

    {

        /// <summary>The name of the file.</summary>
        public String        FileName       { get; } = FileName;

        /// <summary>The media type of the file.</summary>
        public String        MimeType       { get; } = MimeType;

        /// <summary>The contents of the file, base64 encoded.</summary>
        public String        DataBase64     { get; } = DataBase64;

        /// <summary>An optional name.</summary>
        public I18NString?   Name           { get; } = Name;

        /// <summary>An optional description.</summary>
        public RichContent?  Description    { get; } = Description;

        /// <summary>Read a file out of its JSON.</summary>
        public static ByteData Parse(JObject JSON)

            => new (
                   DCC.Text (JSON, "fileName")   ?? "",
                   DCC.Text (JSON, "mimeType")   ?? "",
                   DCC.Text (JSON, "dataBase64") ?? "",
                   DCC.I18N (JSON, "name"),
                   DCC.Object(JSON, "description") is JObject description ? RichContent.Parse(description) : null,
                   DCC.Text (JSON, "id"),
                   DCC.Texts(JSON, "refId"),
                   DCC.Text (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this file.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("fileName",    FileName),
                           new JProperty("mimeType",    MimeType),
                           new JProperty("dataBase64",  DataBase64)
                       );

            DCC.AddIfSet(json, "name",         Name);
            DCC.AddIfSet(json, "description",  Description);

            return WithIdentification(json);

        }

    }


    /// <summary>Free-form content: text, formulas and files together.</summary>
    public class RichContent(I18NString?                     Name     = null,
                             IEnumerable<DCCStringWithLang>? Content  = null,
                             IEnumerable<ByteData>?          Files    = null,
                             IEnumerable<Formula>?           Formulas = null,
                             String?                         Id       = null,
                             IEnumerable<String>?            RefIds   = null,
                             String?                         RefType  = null)

        : ADCCElement(Id, RefIds, RefType)

    {

        /// <summary>An optional name.</summary>
        public I18NString?                        Name        { get; } = Name;

        /// <summary>The text, in as many languages as the certificate offers.</summary>
        public IReadOnlyList<DCCStringWithLang>   Content     { get; } = Content?.ToArray()  ?? [];

        /// <summary>Attached files.</summary>
        public IReadOnlyList<ByteData>            Files       { get; } = Files?.ToArray()    ?? [];

        /// <summary>Attached formulas.</summary>
        public IReadOnlyList<Formula>             Formulas    { get; } = Formulas?.ToArray() ?? [];

        /// <summary>Read free-form content out of its JSON.</summary>
        public static RichContent Parse(JObject JSON)

            => new (
                   DCC.I18N (JSON, "name"),
                   DCC.Array(JSON, "content", DCCStringWithLang.Parse),
                   DCC.Array(JSON, "file",    ByteData.Parse),
                   DCC.Array(JSON, "formula", Formula.Parse),
                   DCC.Text (JSON, "id"),
                   DCC.Texts(JSON, "refId"),
                   DCC.Text (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this content.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "name",     Name);
            DCC.AddIfSet(json, "content",  Content);
            DCC.AddIfSet(json, "file",     Files);
            DCC.AddIfSet(json, "formula",  Formulas);

            return WithIdentification(json);

        }

    }


    /// <summary>Where something is.</summary>
    public class Location(String?                City                 = null,
                          String?                CountryCode          = null,
                          String?                PostCode             = null,
                          String?                PostOfficeBox        = null,
                          String?                State                = null,
                          String?                Street               = null,
                          String?                StreetNo             = null,
                          RichContent?           Further              = null,
                          PositionCoordinates?   PositionCoordinates  = null) : IDCCElement
    {

        /// <summary>The city.</summary>
        public String?               City                   { get; } = City;

        /// <summary>The ISO 3166 country code.</summary>
        public String?               CountryCode            { get; } = CountryCode;

        /// <summary>The postal code.</summary>
        public String?               PostCode               { get; } = PostCode;

        /// <summary>The post office box.</summary>
        public String?               PostOfficeBox          { get; } = PostOfficeBox;

        /// <summary>The state.</summary>
        public String?               State                  { get; } = State;

        /// <summary>The street.</summary>
        public String?               Street                 { get; } = Street;

        /// <summary>The house number.</summary>
        public String?               StreetNo               { get; } = StreetNo;

        /// <summary>Anything else worth saying about the place.</summary>
        public RichContent?          Further                { get; } = Further;

        /// <summary>Where the place is, exactly.</summary>
        public PositionCoordinates?  PositionCoordinates    { get; } = PositionCoordinates;

        /// <summary>Read a location out of its JSON.</summary>
        public static Location Parse(JObject JSON)

            => new (
                   DCC.Text  (JSON, "city"),
                   DCC.Text  (JSON, "countryCode"),
                   DCC.Text  (JSON, "postCode"),
                   DCC.Text  (JSON, "postOfficeBox"),
                   DCC.Text  (JSON, "state"),
                   DCC.Text  (JSON, "street"),
                   DCC.Text  (JSON, "streetNo"),
                   DCC.Object(JSON, "further")             is JObject further     ? RichContent.Parse(further)                 : null,
                   DCC.Object(JSON, "positionCoordinates") is JObject coordinates ? QIDigital.PositionCoordinates.Parse(coordinates) : null
               );

        /// <summary>Return a JSON representation of this location.</summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "city",                 City);
            DCC.AddIfSet(json, "countryCode",          CountryCode);
            DCC.AddIfSet(json, "postCode",             PostCode);
            DCC.AddIfSet(json, "postOfficeBox",        PostOfficeBox);
            DCC.AddIfSet(json, "state",                State);
            DCC.AddIfSet(json, "street",               Street);
            DCC.AddIfSet(json, "streetNo",             StreetNo);
            DCC.AddIfSet(json, "further",              Further);
            DCC.AddIfSet(json, "positionCoordinates",  PositionCoordinates);

            return json;

        }

    }


    /// <summary>A measured quantity with its uncertainty.</summary>
    public class RealQuantity(Decimal   Value,
                              Decimal?  Uncertainty = null) : IDCCElement
    {

        /// <summary>The value.</summary>
        public Decimal   Value          { get; } = Value;

        /// <summary>How uncertain it is.</summary>
        public Decimal?  Uncertainty    { get; } = Uncertainty;

        /// <summary>Read a quantity out of its JSON.</summary>
        public static RealQuantity Parse(JObject JSON)

            => new (
                   DCC.Number(JSON, "value") ?? 0,
                   DCC.Number(JSON, "uncertainty")
               );

        /// <summary>Return a JSON representation of this quantity.</summary>
        public JObject ToJSON()
        {

            var json = new JObject(new JProperty("value", Value));

            DCC.AddIfSet(json, "uncertainty", Uncertainty);

            return json;

        }

    }


    /// <summary>A position in a named coordinate system.</summary>
    public class PositionCoordinates(String                PositionCoordinateSystem,
                                     RealQuantity          Coordinate1,
                                     RealQuantity          Coordinate2,
                                     RealQuantity?         Coordinate3  = null,
                                     String?               Reference    = null,
                                     RichContent?          Declaration  = null,
                                     String?               Id           = null,
                                     IEnumerable<String>?  RefIds       = null,
                                     String?               RefType      = null)

        : ADCCElement(Id, RefIds, RefType)

    {

        /// <summary>Which coordinate system the numbers are in.</summary>
        public String         PositionCoordinateSystem    { get; } = PositionCoordinateSystem;

        /// <summary>The first coordinate.</summary>
        public RealQuantity   Coordinate1                 { get; } = Coordinate1;

        /// <summary>The second coordinate.</summary>
        public RealQuantity   Coordinate2                 { get; } = Coordinate2;

        /// <summary>An optional third coordinate.</summary>
        public RealQuantity?  Coordinate3                 { get; } = Coordinate3;

        /// <summary>An optional reference.</summary>
        public String?        Reference                   { get; } = Reference;

        /// <summary>An optional declaration.</summary>
        public RichContent?   Declaration                 { get; } = Declaration;

        /// <summary>Read a position out of its JSON.</summary>
        public static PositionCoordinates Parse(JObject JSON)

            => new (
                   DCC.Text  (JSON, "positionCoordinateSystem") ?? "",
                   DCC.Object(JSON, "positionCoordinate1") is JObject first  ? RealQuantity.Parse(first)  : new RealQuantity(0),
                   DCC.Object(JSON, "positionCoordinate2") is JObject second ? RealQuantity.Parse(second) : new RealQuantity(0),
                   DCC.Object(JSON, "positionCoordinate3") is JObject third  ? RealQuantity.Parse(third)  : null,
                   DCC.Text  (JSON, "reference"),
                   DCC.Object(JSON, "declaration") is JObject declaration ? RichContent.Parse(declaration) : null,
                   DCC.Text  (JSON, "id"),
                   DCC.Texts (JSON, "refId"),
                   DCC.Text  (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this position.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("positionCoordinateSystem", PositionCoordinateSystem),
                           new JProperty("positionCoordinate1",      Coordinate1.ToJSON()),
                           new JProperty("positionCoordinate2",      Coordinate2.ToJSON())
                       );

            DCC.AddIfSet(json, "positionCoordinate3",  Coordinate3);
            DCC.AddIfSet(json, "reference",            Reference);
            DCC.AddIfSet(json, "declaration",          Declaration);

            return WithIdentification(json);

        }

    }


    /// <summary>Somebody to contact, whose location the schema leaves optional.</summary>
    public class DCCContact(I18NString?           Name             = null,
                            String?               EMail            = null,
                            String?               Phone            = null,
                            String?               Fax              = null,
                            Location?             Location         = null,
                            ByteData?             DescriptionData  = null,
                            String?               Id               = null,
                            IEnumerable<String>?  RefIds           = null,
                            String?               RefType          = null)

        : ADCCElement(Id, RefIds, RefType)

    {

        /// <summary>The name.</summary>
        public I18NString?  Name               { get; } = Name;

        /// <summary>An optional e-mail address.</summary>
        public String?      EMail              { get; } = EMail;

        /// <summary>An optional telephone number.</summary>
        public String?      Phone              { get; } = Phone;

        /// <summary>An optional fax number.</summary>
        public String?      Fax                { get; } = Fax;

        /// <summary>Where they are.</summary>
        public Location?    Location           { get; } = Location;

        /// <summary>An optional attached description.</summary>
        public ByteData?    DescriptionData    { get; } = DescriptionData;

        /// <summary>Read a contact out of its JSON.</summary>
        public static DCCContact Parse(JObject JSON)

            => new (
                   DCC.I18N  (JSON, "name"),
                   DCC.Text  (JSON, "eMail"),
                   DCC.Text  (JSON, "phone"),
                   DCC.Text  (JSON, "fax"),
                   DCC.Object(JSON, "location")        is JObject location    ? QIDigital.Location.Parse(location) : null,
                   DCC.Object(JSON, "descriptionData") is JObject description ? ByteData.Parse(description)        : null,
                   DCC.Text  (JSON, "id"),
                   DCC.Texts (JSON, "refId"),
                   DCC.Text  (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this contact.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "name",             Name);
            DCC.AddIfSet(json, "eMail",            EMail);
            DCC.AddIfSet(json, "phone",            Phone);
            DCC.AddIfSet(json, "fax",              Fax);
            DCC.AddIfSet(json, "location",         Location);
            DCC.AddIfSet(json, "descriptionData",  DescriptionData);

            return WithIdentification(json);

        }

    }


    /// <summary>A reference to another document, secured by a hash of it.</summary>
    public class DCCHash(I18NString?           Referral         = null,
                         String?               ReferralID       = null,
                         String?               Procedure        = null,
                         String?               Value            = null,
                         RichContent?          Description      = null,
                         Boolean?              InValidityRange  = null,
                         Boolean?              Traceable        = null,
                         DCCHash?              LinkedReport     = null,
                         String?               Id               = null,
                         String?               RefType          = null)

        : ADCCElement(Id, null, RefType)

    {

        /// <summary>What is being referred to.</summary>
        public I18NString?   Referral           { get; } = Referral;

        /// <summary>Its identification.</summary>
        public String?       ReferralID         { get; } = ReferralID;

        /// <summary>Which hash procedure secures the reference.</summary>
        public String?       Procedure          { get; } = Procedure;

        /// <summary>The hash itself.</summary>
        public String?       Value              { get; } = Value;

        /// <summary>An optional description.</summary>
        public RichContent?  Description        { get; } = Description;

        /// <summary>Whether the referred document is still within its validity.</summary>
        public Boolean?      InValidityRange    { get; } = InValidityRange;

        /// <summary>Whether the referred document is traceable.</summary>
        public Boolean?      Traceable          { get; } = Traceable;

        /// <summary>A further report the referred one links to.</summary>
        public DCCHash?      LinkedReport       { get; } = LinkedReport;

        /// <summary>Read a reference out of its JSON.</summary>
        public static DCCHash Parse(JObject JSON)

            => new (
                   DCC.I18N  (JSON, "referral"),
                   DCC.Text  (JSON, "referralID"),
                   DCC.Text  (JSON, "procedure"),
                   DCC.Text  (JSON, "value"),
                   DCC.Object(JSON, "description")  is JObject description  ? RichContent.Parse(description) : null,
                   DCC.Flag  (JSON, "inValidityRange"),
                   DCC.Flag  (JSON, "traceable"),
                   DCC.Object(JSON, "linkedReport") is JObject linkedReport ? Parse(linkedReport)            : null,
                   DCC.Text  (JSON, "id"),
                   DCC.Text  (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this reference.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "referral",         Referral);
            DCC.AddIfSet(json, "referralID",       ReferralID);
            DCC.AddIfSet(json, "procedure",        Procedure);
            DCC.AddIfSet(json, "value",            Value);
            DCC.AddIfSet(json, "description",      Description);
            DCC.AddIfSet(json, "inValidityRange",  InValidityRange);
            DCC.AddIfSet(json, "traceable",        Traceable);
            DCC.AddIfSet(json, "linkedReport",     LinkedReport);

            return WithIdentification(json);

        }

    }

}
