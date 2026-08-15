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

    /// <summary>A piece of software, and which version of it was used.</summary>
    public class DCCSoftware(I18NString?   Name         = null,
                             String?       Release      = null,
                             String?       Type         = null,
                             RichContent?  Description  = null,
                             String?       Id           = null,
                             String?       RefType      = null)

        : ADCCElement(Id, null, RefType)

    {

        /// <summary>The name of the software.</summary>
        public I18NString?   Name           { get; } = Name;

        /// <summary>Its release.</summary>
        public String?       Release        { get; } = Release;

        /// <summary>What kind of software it is, e.g. "firmware".</summary>
        public String?       Type           { get; } = Type;

        /// <summary>An optional description.</summary>
        public RichContent?  Description    { get; } = Description;

        /// <summary>Read a software entry out of its JSON.</summary>
        public static DCCSoftware Parse(JObject JSON)

            => new (
                   DCC.I18N  (JSON, "name"),
                   DCC.Text  (JSON, "release"),
                   DCC.Text  (JSON, "type"),
                   DCC.Object(JSON, "description") is JObject description ? RichContent.Parse(description) : null,
                   DCC.Text  (JSON, "id"),
                   DCC.Text  (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this software entry.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "name",         Name);
            DCC.AddIfSet(json, "release",      Release);
            DCC.AddIfSet(json, "type",         Type);
            DCC.AddIfSet(json, "description",  Description);

            return WithIdentification(json);

        }

    }


    /// <summary>What a reference type used in the certificate means.</summary>
    public class RefTypeDefinition(I18NString?   Name         = null,
                                   String?       Namespace    = null,
                                   String?       Link         = null,
                                   RichContent?  Description  = null,
                                   String?       Release      = null,
                                   String?       Value        = null,
                                   String?       Procedure    = null,
                                   String?       Id           = null,
                                   String?       RefType      = null)

        : ADCCElement(Id, null, RefType)

    {

        /// <summary>The name of the reference type.</summary>
        public I18NString?   Name           { get; } = Name;

        /// <summary>Its namespace.</summary>
        public String?       Namespace      { get; } = Namespace;

        /// <summary>Where it is defined.</summary>
        public String?       Link           { get; } = Link;

        /// <summary>An optional description.</summary>
        public RichContent?  Description    { get; } = Description;

        /// <summary>Its release.</summary>
        public String?       Release        { get; } = Release;

        /// <summary>Its value.</summary>
        public String?       Value          { get; } = Value;

        /// <summary>Its procedure.</summary>
        public String?       Procedure      { get; } = Procedure;

        /// <summary>Read a reference type definition out of its JSON.</summary>
        public static RefTypeDefinition Parse(JObject JSON)

            => new (
                   DCC.I18N  (JSON, "name"),
                   DCC.Text  (JSON, "namespace"),
                   DCC.Text  (JSON, "link"),
                   DCC.Object(JSON, "description") is JObject description ? RichContent.Parse(description) : null,
                   DCC.Text  (JSON, "release"),
                   DCC.Text  (JSON, "value"),
                   DCC.Text  (JSON, "procedure"),
                   DCC.Text  (JSON, "id"),
                   DCC.Text  (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this definition.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "name",         Name);
            DCC.AddIfSet(json, "namespace",    Namespace);
            DCC.AddIfSet(json, "link",         Link);
            DCC.AddIfSet(json, "description",  Description);
            DCC.AddIfSet(json, "release",      Release);
            DCC.AddIfSet(json, "value",        Value);
            DCC.AddIfSet(json, "procedure",    Procedure);

            return WithIdentification(json);

        }

    }


    /// <summary>How a measurement was made.</summary>
    public class UsedMethod(I18NString?           Name         = null,
                            RichContent?          Description  = null,
                            IEnumerable<String>?  Norms        = null,
                            IEnumerable<String>?  References   = null,
                            String?               Id           = null,
                            String?               RefType      = null)

        : ADCCElement(Id, null, RefType)

    {

        /// <summary>The name of the method.</summary>
        public I18NString?            Name           { get; } = Name;

        /// <summary>An optional description.</summary>
        public RichContent?           Description    { get; } = Description;

        /// <summary>The norms the method follows.</summary>
        public IReadOnlyList<String>  Norms          { get; } = Norms?.     ToArray() ?? [];

        /// <summary>References backing the method.</summary>
        public IReadOnlyList<String>  References     { get; } = References?.ToArray() ?? [];

        /// <summary>Read a method out of its JSON.</summary>
        public static UsedMethod Parse(JObject JSON)

            => new (
                   DCC.I18N  (JSON, "name"),
                   DCC.Object(JSON, "description") is JObject description ? RichContent.Parse(description) : null,
                   DCC.Texts (JSON, "norm"),
                   DCC.Texts (JSON, "reference"),
                   DCC.Text  (JSON, "id"),
                   DCC.Text  (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this method.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "name",         Name);
            DCC.AddIfSet(json, "description",  Description);
            DCC.AddIfSet(json, "norm",         Norms);
            DCC.AddIfSet(json, "reference",    References);

            return WithIdentification(json);

        }

    }


    /// <summary>How something is identified, and by whom.</summary>
    public class DCCIdentification(String?       Issuer   = null,
                                   String?       Value    = null,
                                   I18NString?   Name     = null,
                                   String?       Id       = null,
                                   String?       RefType  = null)

        : ADCCElement(Id, null, RefType)

    {

        /// <summary>Who issued the identification.</summary>
        public String?      Issuer    { get; } = Issuer;

        /// <summary>The identification itself.</summary>
        public String?      Value     { get; } = Value;

        /// <summary>An optional name.</summary>
        public I18NString?  Name      { get; } = Name;

        /// <summary>Read an identification out of its JSON.</summary>
        public static DCCIdentification Parse(JObject JSON)

            => new (
                   DCC.Text(JSON, "issuer"),
                   DCC.Text(JSON, "value"),
                   DCC.I18N(JSON, "name"),
                   DCC.Text(JSON, "id"),
                   DCC.Text(JSON, "refType")
               );

        /// <summary>Return a JSON representation of this identification.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "issuer",  Issuer);
            DCC.AddIfSet(json, "value",   Value);
            DCC.AddIfSet(json, "name",    Name);

            return WithIdentification(json);

        }

    }


    /// <summary>The class a piece of equipment belongs to.</summary>
    public class EquipmentClass(String?  Reference  = null,
                                String?  ClassID    = null,
                                String?  Id         = null,
                                String?  RefType    = null)

        : ADCCElement(Id, null, RefType)

    {

        /// <summary>Where the classification is defined.</summary>
        public String?  Reference    { get; } = Reference;

        /// <summary>The class.</summary>
        public String?  ClassID      { get; } = ClassID;

        /// <summary>Read an equipment class out of its JSON.</summary>
        public static EquipmentClass Parse(JObject JSON)

            => new (
                   DCC.Text(JSON, "reference"),
                   DCC.Text(JSON, "classID"),
                   DCC.Text(JSON, "id"),
                   DCC.Text(JSON, "refType")
               );

        /// <summary>Return a JSON representation of this class.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "reference",  Reference);
            DCC.AddIfSet(json, "classID",    ClassID);

            return WithIdentification(json);

        }

    }


    /// <summary>A quantity whose numeric shape the DCC schema defers to the SI types.</summary>
    public class PrimitiveQuantity(I18NString?           Name        = null,
                                   RichContent?          Description = null,
                                   RichContent?          NoQuantity  = null,
                                   IEnumerable<String>?  Chars       = null,
                                   JToken?               Real        = null,
                                   JToken?               Hybrid      = null,
                                   JToken?               Complex     = null,
                                   JToken?               Constant    = null,
                                   JToken?               RealList    = null,
                                   String?               Id          = null,
                                   IEnumerable<String>?  RefIds      = null,
                                   String?               RefType     = null)

        : DCCQuantity(Name, Description, NoQuantity, Chars, Real, Hybrid, Complex, Constant, RealList, Id, RefIds, RefType)

    {

        /// <summary>Read a primitive quantity out of its JSON.</summary>
        public new static PrimitiveQuantity Parse(JObject JSON)
        {

            var quantity = DCCQuantity.Parse(JSON);

            return new PrimitiveQuantity(
                       quantity.Name,
                       quantity.Description,
                       quantity.NoQuantity,
                       quantity.Chars,
                       quantity.Real,
                       quantity.Hybrid,
                       quantity.Complex,
                       quantity.Constant,
                       quantity.RealList,
                       quantity.Id,
                       quantity.RefIds,
                       quantity.RefType
                   );

        }

    }


    /// <summary>A piece of equipment the laboratory measured with.</summary>
    public class MeasuringEquipment(I18NString?                       Name                          = null,
                                    IEnumerable<EquipmentClass>?      EquipmentClasses              = null,
                                    RichContent?                      Description                   = null,
                                    DCCHash?                          Certificate                   = null,
                                    DCCContact?                       Manufacturer                  = null,
                                    String?                           Model                         = null,
                                    IEnumerable<DCCIdentification>?   Identifications               = null,
                                    IEnumerable<PrimitiveQuantity>?   MeasuringEquipmentQuantities  = null,
                                    String?                           Id                            = null,
                                    String?                           RefType                       = null)

        : ADCCElement(Id, null, RefType)

    {

        /// <summary>The name of the equipment.</summary>
        public I18NString?                      Name                          { get; } = Name;

        /// <summary>What class of equipment it is.</summary>
        public IReadOnlyList<EquipmentClass>    EquipmentClasses              { get; } = EquipmentClasses?.            ToArray() ?? [];

        /// <summary>An optional description.</summary>
        public RichContent?                     Description                   { get; } = Description;

        /// <summary>The certificate backing it.</summary>
        public DCCHash?                         Certificate                   { get; } = Certificate;

        /// <summary>Who made it.</summary>
        public DCCContact?                      Manufacturer                  { get; } = Manufacturer;

        /// <summary>Its model.</summary>
        public String?                          Model                         { get; } = Model;

        /// <summary>How it is identified.</summary>
        public IReadOnlyList<DCCIdentification> Identifications               { get; } = Identifications?.             ToArray() ?? [];

        /// <summary>What it measures.</summary>
        public IReadOnlyList<PrimitiveQuantity> MeasuringEquipmentQuantities  { get; } = MeasuringEquipmentQuantities?.ToArray() ?? [];

        /// <summary>Read a piece of equipment out of its JSON.</summary>
        public static MeasuringEquipment Parse(JObject JSON)

            => new (
                   DCC.I18N  (JSON, "name"),
                   DCC.Array (JSON, "equipmentClass", EquipmentClass.Parse),
                   DCC.Object(JSON, "description") is JObject description  ? RichContent.Parse(description) : null,
                   DCC.Object(JSON, "certificate") is JObject certificate  ? DCCHash.Parse(certificate)     : null,
                   DCC.Object(JSON, "manufacturer") is JObject manufacturer ? DCCContact.Parse(manufacturer) : null,
                   DCC.Text  (JSON, "model"),
                   DCC.Array (JSON, "identifications",              DCCIdentification.Parse),
                   DCC.Array (JSON, "measuringEquipmentQuantities", PrimitiveQuantity.Parse),
                   DCC.Text  (JSON, "id"),
                   DCC.Text  (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this equipment.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "name",                          Name);
            DCC.AddIfSet(json, "equipmentClass",                EquipmentClasses);
            DCC.AddIfSet(json, "description",                   Description);
            DCC.AddIfSet(json, "certificate",                   Certificate);
            DCC.AddIfSet(json, "manufacturer",                  Manufacturer);
            DCC.AddIfSet(json, "model",                         Model);
            DCC.AddIfSet(json, "identifications",               Identifications);
            DCC.AddIfSet(json, "measuringEquipmentQuantities",  MeasuringEquipmentQuantities);

            return WithIdentification(json);

        }

    }


    /// <summary>The thing that was calibrated.</summary>
    public class CalibratedItem(I18NString?                       Name                = null,
                                IEnumerable<EquipmentClass>?      EquipmentClasses    = null,
                                RichContent?                      Description         = null,
                                IEnumerable<DCCSoftware>?         InstalledSoftwares  = null,
                                DCCContact?                       Manufacturer        = null,
                                String?                           Model               = null,
                                IEnumerable<DCCIdentification>?   Identifications     = null,
                                IEnumerable<PrimitiveQuantity>?   ItemQuantities      = null,
                                String?                           Id                  = null,
                                String?                           RefType             = null)

        : ADCCElement(Id, null, RefType)

    {

        /// <summary>The name of the item.</summary>
        public I18NString?                      Name                  { get; } = Name;

        /// <summary>What class of equipment it is.</summary>
        public IReadOnlyList<EquipmentClass>    EquipmentClasses      { get; } = EquipmentClasses?.  ToArray() ?? [];

        /// <summary>An optional description.</summary>
        public RichContent?                     Description           { get; } = Description;

        /// <summary>What software it runs.</summary>
        public IReadOnlyList<DCCSoftware>       InstalledSoftwares    { get; } = InstalledSoftwares?.ToArray() ?? [];

        /// <summary>Who made it.</summary>
        public DCCContact?                      Manufacturer          { get; } = Manufacturer;

        /// <summary>Its model.</summary>
        public String?                          Model                 { get; } = Model;

        /// <summary>How it is identified.</summary>
        public IReadOnlyList<DCCIdentification> Identifications       { get; } = Identifications?.   ToArray() ?? [];

        /// <summary>What it measures.</summary>
        public IReadOnlyList<PrimitiveQuantity> ItemQuantities        { get; } = ItemQuantities?.    ToArray() ?? [];

        /// <summary>Read a calibrated item out of its JSON.</summary>
        public static CalibratedItem Parse(JObject JSON)

            => new (
                   DCC.I18N  (JSON, "name"),
                   DCC.Array (JSON, "equipmentClass",     EquipmentClass.Parse),
                   DCC.Object(JSON, "description")  is JObject description  ? RichContent.Parse(description) : null,
                   DCC.Array (JSON, "installedSoftwares", DCCSoftware.Parse),
                   DCC.Object(JSON, "manufacturer") is JObject manufacturer ? DCCContact.Parse(manufacturer) : null,
                   DCC.Text  (JSON, "model"),
                   DCC.Array (JSON, "identifications",    DCCIdentification.Parse),
                   DCC.Array (JSON, "itemQuantities",     PrimitiveQuantity.Parse),
                   DCC.Text  (JSON, "id"),
                   DCC.Text  (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this item.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "name",                Name);
            DCC.AddIfSet(json, "equipmentClass",      EquipmentClasses);
            DCC.AddIfSet(json, "description",         Description);
            DCC.AddIfSet(json, "installedSoftwares",  InstalledSoftwares);
            DCC.AddIfSet(json, "manufacturer",        Manufacturer);
            DCC.AddIfSet(json, "model",               Model);
            DCC.AddIfSet(json, "identifications",     Identifications);
            DCC.AddIfSet(json, "itemQuantities",      ItemQuantities);

            return WithIdentification(json);

        }

    }


    /// <summary>Somebody responsible for the calibration, and what they signed.</summary>
    public class ResponsiblePerson(DCCContact?   Person                    = null,
                                   RichContent?  Description               = null,
                                   String?       Role                      = null,
                                   Boolean?      MainSigner                = null,
                                   Boolean?      CryptElectronicSeal       = null,
                                   Boolean?      CryptElectronicSignature  = null,
                                   Boolean?      CryptElectronicTimeStamp  = null,
                                   String?       Id                        = null,
                                   String?       RefType                   = null)

        : ADCCElement(Id, null, RefType)

    {

        /// <summary>Who they are.</summary>
        public DCCContact?   Person                      { get; } = Person;

        /// <summary>An optional description.</summary>
        public RichContent?  Description                 { get; } = Description;

        /// <summary>Their role.</summary>
        public String?       Role                        { get; } = Role;

        /// <summary>Whether they are the main signer.</summary>
        public Boolean?      MainSigner                  { get; } = MainSigner;

        /// <summary>Whether they applied an electronic seal.</summary>
        public Boolean?      CryptElectronicSeal         { get; } = CryptElectronicSeal;

        /// <summary>Whether they applied an electronic signature.</summary>
        public Boolean?      CryptElectronicSignature    { get; } = CryptElectronicSignature;

        /// <summary>Whether they applied an electronic time stamp.</summary>
        public Boolean?      CryptElectronicTimeStamp    { get; } = CryptElectronicTimeStamp;

        /// <summary>Read a responsible person out of their JSON.</summary>
        public static ResponsiblePerson Parse(JObject JSON)

            => new (
                   DCC.Object(JSON, "person")      is JObject person      ? DCCContact.Parse(person)       : null,
                   DCC.Object(JSON, "description") is JObject description ? RichContent.Parse(description) : null,
                   DCC.Text  (JSON, "role"),
                   DCC.Flag  (JSON, "mainSigner"),
                   DCC.Flag  (JSON, "cryptElectronicSeal"),
                   DCC.Flag  (JSON, "cryptElectronicSignature"),
                   DCC.Flag  (JSON, "cryptElectronicTimeStamp"),
                   DCC.Text  (JSON, "id"),
                   DCC.Text  (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this person.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "person",                    Person);
            DCC.AddIfSet(json, "description",               Description);
            DCC.AddIfSet(json, "role",                      Role);
            DCC.AddIfSet(json, "mainSigner",                MainSigner);
            DCC.AddIfSet(json, "cryptElectronicSeal",       CryptElectronicSeal);
            DCC.AddIfSet(json, "cryptElectronicSignature",  CryptElectronicSignature);
            DCC.AddIfSet(json, "cryptElectronicTimeStamp",  CryptElectronicTimeStamp);

            return WithIdentification(json);

        }

    }


    /// <summary>Where the calibration was performed.</summary>
    public class PerformanceLocation(String?               Location  = null,
                                     String?               Id        = null,
                                     IEnumerable<String>?  RefIds    = null,
                                     String?               RefType   = null)

        : ADCCElement(Id, RefIds, RefType)

    {

        /// <summary>Whether at the laboratory, at the customer, or elsewhere.</summary>
        public String? Location { get; } = Location;

        /// <summary>Read a performance location out of its JSON.</summary>
        public static PerformanceLocation Parse(JObject JSON)

            => new (
                   DCC.Text (JSON, "location"),
                   DCC.Text (JSON, "id"),
                   DCC.Texts(JSON, "refId"),
                   DCC.Text (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this location.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "location", Location);

            return WithIdentification(json);

        }

    }


    /// <summary>Which certificate this one replaces, and why.</summary>
    public class ReportAmendedSubstituted(String?  TypeOfChange              = null,
                                          String?  ReplacedUniqueIdentifier  = null,
                                          String?  Id                        = null,
                                          String?  RefType                   = null)

        : ADCCElement(Id, null, RefType)

    {

        /// <summary>Whether the previous certificate was amended or substituted.</summary>
        public String?  TypeOfChange                { get; } = TypeOfChange;

        /// <summary>Which certificate this one replaces.</summary>
        public String?  ReplacedUniqueIdentifier    { get; } = ReplacedUniqueIdentifier;

        /// <summary>Read a replacement note out of its JSON.</summary>
        public static ReportAmendedSubstituted Parse(JObject JSON)

            => new (
                   DCC.Text(JSON, "typeOfChange"),
                   DCC.Text(JSON, "replacedUniqueIdentifier"),
                   DCC.Text(JSON, "id"),
                   DCC.Text(JSON, "refType")
               );

        /// <summary>Return a JSON representation of this note.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "typeOfChange",              TypeOfChange);
            DCC.AddIfSet(json, "replacedUniqueIdentifier",  ReplacedUniqueIdentifier);

            return WithIdentification(json);

        }

    }


    /// <summary>The facts every Digital Calibration Certificate states about itself.</summary>
    public class CoreData(String?                          CountryCode               = null,
                          IEnumerable<String>?             UsedLanguages             = null,
                          IEnumerable<String>?             MandatoryLanguages        = null,
                          String?                          UniqueIdentifier          = null,
                          IEnumerable<DCCIdentification>?  Identifications           = null,
                          String?                          ReceiptDate               = null,
                          String?                          BeginPerformanceDate      = null,
                          String?                          EndPerformanceDate        = null,
                          PerformanceLocation?             PerformanceLocation       = null,
                          String?                          IssueDate                 = null,
                          ReportAmendedSubstituted?        ReportAmendedSubstituted  = null,
                          DCCHash?                         PreviousReport            = null) : IDCCElement
    {

        /// <summary>The ISO 3166 country code of the certificate.</summary>
        public String?                           CountryCode                 { get; } = CountryCode;

        /// <summary>The ISO 639 languages the certificate is written in.</summary>
        public IReadOnlyList<String>             UsedLanguages               { get; } = UsedLanguages?.     ToArray() ?? [];

        /// <summary>The languages it must be read in.</summary>
        public IReadOnlyList<String>             MandatoryLanguages          { get; } = MandatoryLanguages?.ToArray() ?? [];

        /// <summary>What makes this certificate this certificate.</summary>
        public String?                           UniqueIdentifier            { get; } = UniqueIdentifier;

        /// <summary>Further identifications.</summary>
        public IReadOnlyList<DCCIdentification>  Identifications             { get; } = Identifications?.   ToArray() ?? [];

        /// <summary>When the item arrived at the laboratory.</summary>
        public String?                           ReceiptDate                 { get; } = ReceiptDate;

        /// <summary>When the calibration began.</summary>
        public String?                           BeginPerformanceDate        { get; } = BeginPerformanceDate;

        /// <summary>When it ended.</summary>
        public String?                           EndPerformanceDate          { get; } = EndPerformanceDate;

        /// <summary>Where it was performed.</summary>
        public PerformanceLocation?              PerformanceLocation         { get; } = PerformanceLocation;

        /// <summary>When the certificate was issued.</summary>
        public String?                           IssueDate                   { get; } = IssueDate;

        /// <summary>Which certificate this one replaces.</summary>
        public ReportAmendedSubstituted?         ReportAmendedSubstituted    { get; } = ReportAmendedSubstituted;

        /// <summary>The previous report.</summary>
        public DCCHash?                          PreviousReport              { get; } = PreviousReport;

        /// <summary>Read the core data out of its JSON.</summary>
        public static CoreData Parse(JObject JSON)

            => new (
                   DCC.Text     (JSON, "countryCodeISO3166_1"),
                   DCC.Texts    (JSON, "usedLangCodeISO639_1"),
                   DCC.Texts    (JSON, "mandatoryLangCodeISO639_1"),
                   DCC.Text     (JSON, "uniqueIdentifier"),
                   DCC.Array    (JSON, "identifications", DCCIdentification.Parse),
                   DCC.Timestamp(JSON, "receiptDate"),
                   DCC.Timestamp(JSON, "beginPerformanceDate"),
                   DCC.Timestamp(JSON, "endPerformanceDate"),
                   DCC.Object   (JSON, "performanceLocation")      is JObject location    ? QIDigital.PerformanceLocation.Parse(location)     : null,
                   DCC.Timestamp(JSON, "issueDate"),
                   DCC.Object   (JSON, "reportAmendedSubstituted") is JObject replacement ? QIDigital.ReportAmendedSubstituted.Parse(replacement) : null,
                   DCC.Object   (JSON, "previousReport")           is JObject previous    ? DCCHash.Parse(previous)                           : null
               );

        /// <summary>Return a JSON representation of the core data.</summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "countryCodeISO3166_1",       CountryCode);
            DCC.AddIfSet(json, "usedLangCodeISO639_1",       UsedLanguages);
            DCC.AddIfSet(json, "mandatoryLangCodeISO639_1",  MandatoryLanguages);
            DCC.AddIfSet(json, "uniqueIdentifier",           UniqueIdentifier);
            DCC.AddIfSet(json, "identifications",            Identifications);
            DCC.AddIfSet(json, "receiptDate",                ReceiptDate);
            DCC.AddIfSet(json, "beginPerformanceDate",       BeginPerformanceDate);
            DCC.AddIfSet(json, "endPerformanceDate",         EndPerformanceDate);
            DCC.AddIfSet(json, "performanceLocation",        PerformanceLocation);
            DCC.AddIfSet(json, "issueDate",                  IssueDate);
            DCC.AddIfSet(json, "reportAmendedSubstituted",   ReportAmendedSubstituted);
            DCC.AddIfSet(json, "previousReport",             PreviousReport);

            return json;

        }

    }


    /// <summary>The laboratory that performed the calibration.</summary>
    public class CalibrationLaboratory(String?      CalibrationLaboratoryCode  = null,
                                       DCCContact?  Contact                    = null,
                                       Boolean?     CryptElectronicSeal        = null,
                                       Boolean?     CryptElectronicSignature   = null,
                                       Boolean?     CryptElectronicTimeStamp   = null) : IDCCElement
    {

        /// <summary>The laboratory's code.</summary>
        public String?      CalibrationLaboratoryCode    { get; } = CalibrationLaboratoryCode;

        /// <summary>How to reach it.</summary>
        public DCCContact?  Contact                      { get; } = Contact;

        /// <summary>Whether it applied an electronic seal.</summary>
        public Boolean?     CryptElectronicSeal          { get; } = CryptElectronicSeal;

        /// <summary>Whether it applied an electronic signature.</summary>
        public Boolean?     CryptElectronicSignature     { get; } = CryptElectronicSignature;

        /// <summary>Whether it applied an electronic time stamp.</summary>
        public Boolean?     CryptElectronicTimeStamp     { get; } = CryptElectronicTimeStamp;

        /// <summary>Read a laboratory out of its JSON.</summary>
        public static CalibrationLaboratory Parse(JObject JSON)

            => new (
                   DCC.Text  (JSON, "calibrationLaboratoryCode"),
                   DCC.Object(JSON, "contact") is JObject contact ? DCCContact.Parse(contact) : null,
                   DCC.Flag  (JSON, "cryptElectronicSeal"),
                   DCC.Flag  (JSON, "cryptElectronicSignature"),
                   DCC.Flag  (JSON, "cryptElectronicTimeStamp")
               );

        /// <summary>Return a JSON representation of this laboratory.</summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "calibrationLaboratoryCode",  CalibrationLaboratoryCode);
            DCC.AddIfSet(json, "contact",                    Contact);
            DCC.AddIfSet(json, "cryptElectronicSeal",        CryptElectronicSeal);
            DCC.AddIfSet(json, "cryptElectronicSignature",   CryptElectronicSignature);
            DCC.AddIfSet(json, "cryptElectronicTimeStamp",   CryptElectronicTimeStamp);

            return json;

        }

    }


    /// <summary>Everything administrative about a calibration.</summary>
    public class AdministrativeData(IEnumerable<DCCSoftware>?        Software               = null,
                                    IEnumerable<RefTypeDefinition>?  RefTypeDefinitions     = null,
                                    CoreData?                        CoreData               = null,
                                    IEnumerable<CalibratedItem>?     Items                  = null,
                                    CalibrationLaboratory?           CalibrationLaboratory  = null,
                                    IEnumerable<ResponsiblePerson>?  RespPersons            = null,
                                    DCCContact?                      Customer               = null,
                                    IEnumerable<StatementMetaData>?  Statements             = null) : IDCCElement
    {

        /// <summary>The software involved.</summary>
        public IReadOnlyList<DCCSoftware>        Software                 { get; } = Software?.          ToArray() ?? [];

        /// <summary>What the reference types used here mean.</summary>
        public IReadOnlyList<RefTypeDefinition>  RefTypeDefinitions       { get; } = RefTypeDefinitions?.ToArray() ?? [];

        /// <summary>What the certificate states about itself.</summary>
        public CoreData?                         CoreData                 { get; } = CoreData;

        /// <summary>What was calibrated.</summary>
        public IReadOnlyList<CalibratedItem>     Items                    { get; } = Items?.             ToArray() ?? [];

        /// <summary>Who calibrated it.</summary>
        public CalibrationLaboratory?            CalibrationLaboratory    { get; } = CalibrationLaboratory;

        /// <summary>Who is responsible.</summary>
        public IReadOnlyList<ResponsiblePerson>  RespPersons              { get; } = RespPersons?.       ToArray() ?? [];

        /// <summary>Who asked for the calibration.</summary>
        public DCCContact?                       Customer                 { get; } = Customer;

        /// <summary>Statements about the calibration.</summary>
        public IReadOnlyList<StatementMetaData>  Statements               { get; } = Statements?.        ToArray() ?? [];

        /// <summary>Read the administrative data out of its JSON.</summary>
        public static AdministrativeData Parse(JObject JSON)

            => new (
                   DCC.Array (JSON, "software",           DCCSoftware.Parse),
                   DCC.Array (JSON, "refTypeDefinitions", RefTypeDefinition.Parse),
                   DCC.Object(JSON, "coreData")              is JObject coreData   ? QIDigital.CoreData.Parse(coreData)                   : null,
                   DCC.Array (JSON, "items",              CalibratedItem.Parse),
                   DCC.Object(JSON, "calibrationLaboratory") is JObject laboratory ? QIDigital.CalibrationLaboratory.Parse(laboratory)    : null,
                   DCC.Array (JSON, "respPersons",        ResponsiblePerson.Parse),
                   DCC.Object(JSON, "customer")              is JObject customer   ? DCCContact.Parse(customer)                           : null,
                   DCC.Array (JSON, "statements",         StatementMetaData.Parse)
               );

        /// <summary>Return a JSON representation of the administrative data.</summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "software",               Software);
            DCC.AddIfSet(json, "refTypeDefinitions",     RefTypeDefinitions);
            DCC.AddIfSet(json, "coreData",               CoreData);
            DCC.AddIfSet(json, "items",                  Items);
            DCC.AddIfSet(json, "calibrationLaboratory",  CalibrationLaboratory);
            DCC.AddIfSet(json, "respPersons",            RespPersons);
            DCC.AddIfSet(json, "customer",               Customer);
            DCC.AddIfSet(json, "statements",             Statements);

            return json;

        }

    }


    /// <summary>
    /// A Digital Calibration Certificate: an accredited laboratory's statement
    /// that a measuring instrument measures what it claims to.
    ///
    /// This is what stands behind an energy meter's signature. The signature proves
    /// the reading came from that meter; the certificate is why the meter's readings
    /// mean anything in the first place — and having it as data rather than as a PDF
    /// is what lets software follow the chain rather than a person.
    /// </summary>
    /// <param name="Id">The identification of the certificate.</param>
    /// <param name="AdministrativeData">Everything administrative about the calibration.</param>
    /// <param name="MeasurementResults">What was measured.</param>
    /// <param name="SchemaVersion">The version of the DCC schema.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Comments">Optional comments.</param>
    /// <param name="Document">An optional binary copy of the certificate.</param>
    /// <param name="Signatures">Optional digital signatures over the certificate.</param>
    public class DigitalCalibrationCertificate(String                            Id,
                                               AdministrativeData?               AdministrativeData  = null,
                                               IEnumerable<MeasurementResult>?   MeasurementResults  = null,
                                               String?                           SchemaVersion       = null,
                                               IEnumerable<String>?              Context             = null,
                                               IEnumerable<String>?              Comments            = null,
                                               ByteData?                         Document            = null,
                                               IEnumerable<SignatureRS>?         Signatures          = null) : IDCCElement
    {

        #region Properties

        /// <summary>The identification of the certificate.</summary>
        public String                             Id                    { get; } = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>              JSONLDContext         { get; } = Context?.           ToArray() ?? [];

        /// <summary>Everything administrative about the calibration.</summary>
        public AdministrativeData?                AdministrativeData    { get; } = AdministrativeData;

        /// <summary>What was measured.</summary>
        public IReadOnlyList<MeasurementResult>   MeasurementResults    { get; } = MeasurementResults?.ToArray() ?? [];

        /// <summary>The version of the DCC schema.</summary>
        public String?                            SchemaVersion         { get; } = SchemaVersion;

        /// <summary>Optional comments.</summary>
        public IReadOnlyList<String>              Comments              { get; } = Comments?.          ToArray() ?? [];

        /// <summary>An optional binary copy of the certificate.</summary>
        public ByteData?                          Document              { get; } = Document;

        /// <summary>Optional digital signatures over the certificate.</summary>
        public IReadOnlyList<SignatureRS>         Signatures            { get; } = Signatures?.        ToArray() ?? [];

        #endregion

        #region (static) TryParse(JSON, out Certificate)

        /// <summary>
        /// Try to parse the given JSON as a Digital Calibration Certificate.
        /// </summary>
        /// <param name="JSON">A JSON representation of a certificate.</param>
        /// <param name="Certificate">The parsed certificate.</param>
        public static Boolean TryParse(JObject JSON, out DigitalCalibrationCertificate? Certificate)
        {

            Certificate = null;

            var id = DCC.Text(JSON, "@id");

            if (id is null || id.Length == 0)
                return false;

            Certificate = new DigitalCalibrationCertificate(
                              id,
                              DCC.Object(JSON, "administrativeData") is JObject administrativeData
                                  ? QIDigital.AdministrativeData.Parse(administrativeData)
                                  : null,
                              DCC.Array (JSON, "measurementResults", MeasurementResult.Parse),
                              DCC.Text  (JSON, "schemaVersion"),
                              PublicKey.ParseContext(JSON["@context"]),
                              DCC.Texts (JSON, "comments"),
                              DCC.Object(JSON, "document") is JObject document
                                  ? ByteData.Parse(document)
                                  : null,
                              QIDigitalSignatures.Parse(JSON)
                          );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this certificate.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(new JProperty("@id", Id));

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context", JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context", new JArray(JSONLDContext)));

            DCC.AddIfSet(json, "administrativeData",  AdministrativeData);
            DCC.AddIfSet(json, "measurementResults",  MeasurementResults);
            DCC.AddIfSet(json, "schemaVersion",       SchemaVersion);
            DCC.AddIfSet(json, "comments",            Comments);
            DCC.AddIfSet(json, "document",            Document);

            QIDigitalSignatures.AddTo(json, Signatures);

            return json;

        }

        #endregion

        /// <summary>Return a text representation of this certificate.</summary>
        public override String ToString()
            => Id;

    }

}
