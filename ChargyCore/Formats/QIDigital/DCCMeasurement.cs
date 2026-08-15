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

    /// <summary>A quantity, whose numeric shape the DCC schema defers to the SI types.</summary>
    public class DCCQuantity(I18NString?           Name          = null,
                             RichContent?          Description   = null,
                             RichContent?          NoQuantity    = null,
                             IEnumerable<String>?  Chars         = null,
                             JToken?               Real          = null,
                             JToken?               Hybrid        = null,
                             JToken?               Complex       = null,
                             JToken?               Constant      = null,
                             JToken?               RealList      = null,
                             String?               Id            = null,
                             IEnumerable<String>?  RefIds        = null,
                             String?               RefType       = null)

        : ADCCElement(Id, RefIds, RefType)

    {

        /// <summary>An optional name.</summary>
        public I18NString?            Name          { get; } = Name;

        /// <summary>An optional description.</summary>
        public RichContent?           Description   { get; } = Description;

        /// <summary>What is reported when there is no quantity to report.</summary>
        public RichContent?           NoQuantity    { get; } = NoQuantity;

        /// <summary>A list of characters.</summary>
        public IReadOnlyList<String>  Chars         { get; } = Chars?.ToArray() ?? [];

        /// <summary>A real value, in the SI schema's own shape.</summary>
        public JToken?                Real          { get; } = Real;

        /// <summary>A hybrid value, in the SI schema's own shape.</summary>
        public JToken?                Hybrid        { get; } = Hybrid;

        /// <summary>A complex value, in the SI schema's own shape.</summary>
        public JToken?                Complex       { get; } = Complex;

        /// <summary>A constant, in the SI schema's own shape.</summary>
        public JToken?                Constant      { get; } = Constant;

        /// <summary>A list of real values, in the SI schema's own shape.</summary>
        public JToken?                RealList      { get; } = RealList;

        /// <summary>Read a quantity out of its JSON.</summary>
        public static DCCQuantity Parse(JObject JSON)

            => new (
                   DCC.I18N  (JSON, "name"),
                   DCC.Object(JSON, "description") is JObject description ? RichContent.Parse(description) : null,
                   DCC.Object(JSON, "noQuantity")  is JObject noQuantity  ? RichContent.Parse(noQuantity)  : null,
                   DCC.Texts (JSON, "charsXMLList"),
                   JSON["real"],
                   JSON["hybrid"],
                   JSON["complex"],
                   JSON["constant"],
                   JSON["realListXMLList"],
                   DCC.Text  (JSON, "id"),
                   DCC.Texts (JSON, "refId"),
                   DCC.Text  (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this quantity.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "name",             Name);
            DCC.AddIfSet(json, "description",      Description);
            DCC.AddIfSet(json, "noQuantity",       NoQuantity);
            DCC.AddIfSet(json, "charsXMLList",     Chars);
            DCC.AddIfSet(json, "real",             Real);
            DCC.AddIfSet(json, "hybrid",           Hybrid);
            DCC.AddIfSet(json, "complex",          Complex);
            DCC.AddIfSet(json, "constant",         Constant);
            DCC.AddIfSet(json, "realListXMLList",  RealList);

            return WithIdentification(json);

        }

    }


    /// <summary>A list of quantities taken at one or more moments.</summary>
    public class DCCList(I18NString?                Name          = null,
                         RichContent?               Description   = null,
                         String?                    DateTime      = null,
                         IEnumerable<String>?       DateTimes     = null,
                         IEnumerable<DCCQuantity>?  Quantities    = null,
                         String?                    Id            = null,
                         IEnumerable<String>?       RefIds        = null,
                         String?                    RefType       = null)

        : ADCCElement(Id, RefIds, RefType)

    {

        /// <summary>An optional name.</summary>
        public I18NString?                 Name          { get; } = Name;

        /// <summary>An optional description.</summary>
        public RichContent?                Description   { get; } = Description;

        /// <summary>An optional moment, kept as the text the certificate wrote.</summary>
        public String?                     DateTime      { get; } = DateTime;

        /// <summary>Several moments.</summary>
        public IReadOnlyList<String>       DateTimes     { get; } = DateTimes?.ToArray()  ?? [];

        /// <summary>The quantities.</summary>
        public IReadOnlyList<DCCQuantity>  Quantities    { get; } = Quantities?.ToArray() ?? [];

        /// <summary>Read a list out of its JSON.</summary>
        public static DCCList Parse(JObject JSON)

            => new (
                   DCC.I18N     (JSON, "name"),
                   DCC.Object   (JSON, "description") is JObject description ? RichContent.Parse(description) : null,
                   DCC.Timestamp(JSON, "dateTime"),
                   DCC.Texts    (JSON, "dateTimeXMLList"),
                   DCC.Array    (JSON, "quantities", DCCQuantity.Parse),
                   DCC.Text     (JSON, "id"),
                   DCC.Texts    (JSON, "refId"),
                   DCC.Text     (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this list.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "name",             Name);
            DCC.AddIfSet(json, "description",      Description);
            DCC.AddIfSet(json, "dateTime",         DateTime);
            DCC.AddIfSet(json, "dateTimeXMLList",  DateTimes);
            DCC.AddIfSet(json, "quantities",       Quantities);

            return WithIdentification(json);

        }

    }


    /// <summary>Everything a result may consist of.</summary>
    public class DCCData(IEnumerable<RichContent>?  Text        = null,
                         IEnumerable<Formula>?      Formulas    = null,
                         IEnumerable<ByteData>?     ByteData    = null,
                         IEnumerable<JToken>?       XML         = null,
                         IEnumerable<DCCQuantity>?  Quantities  = null,
                         IEnumerable<DCCList>?      Lists       = null,
                         String?                    Id          = null,
                         IEnumerable<String>?       RefIds      = null,
                         String?                    RefType     = null)

        : ADCCElement(Id, RefIds, RefType)

    {

        /// <summary>Free-form text.</summary>
        public IReadOnlyList<RichContent>  Text          { get; } = Text?.      ToArray() ?? [];

        /// <summary>Formulas.</summary>
        public IReadOnlyList<Formula>      Formulas      { get; } = Formulas?.  ToArray() ?? [];

        /// <summary>Attached files.</summary>
        public IReadOnlyList<ByteData>     ByteData      { get; } = ByteData?.  ToArray() ?? [];

        /// <summary>Embedded XML, whose shape the schema does not specify.</summary>
        public IReadOnlyList<JToken>       XML           { get; } = XML?.       ToArray() ?? [];

        /// <summary>Quantities.</summary>
        public IReadOnlyList<DCCQuantity>  Quantities    { get; } = Quantities?.ToArray() ?? [];

        /// <summary>Lists of quantities.</summary>
        public IReadOnlyList<DCCList>      Lists         { get; } = Lists?.     ToArray() ?? [];

        /// <summary>Read data out of its JSON.</summary>
        public static DCCData Parse(JObject JSON)

            => new (
                   DCC.Array(JSON, "text",     RichContent.Parse),
                   DCC.Array(JSON, "formula",  Formula.Parse),
                   DCC.Array(JSON, "byteData", QIDigital.ByteData.Parse),
                   JSON["xml"] is JArray xml ? xml.Cast<JToken>() : null,
                   DCC.Array(JSON, "quantity", DCCQuantity.Parse),
                   DCC.Array(JSON, "list",     DCCList.Parse),
                   DCC.Text (JSON, "id"),
                   DCC.Texts(JSON, "refId"),
                   DCC.Text (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this data.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "text",      Text);
            DCC.AddIfSet(json, "formula",   Formulas);
            DCC.AddIfSet(json, "byteData",  ByteData);
            DCC.AddIfSet(json, "xml",       XML);
            DCC.AddIfSet(json, "quantity",  Quantities);
            DCC.AddIfSet(json, "list",      Lists);

            return WithIdentification(json);

        }

    }


    /// <summary>One result of a calibration.</summary>
    public class DCCResult(I18NString?           Name         = null,
                           RichContent?          Description  = null,
                           DCCData?              Data         = null,
                           String?               Id           = null,
                           IEnumerable<String>?  RefIds       = null,
                           String?               RefType      = null)

        : ADCCElement(Id, RefIds, RefType)

    {

        /// <summary>The name of the result.</summary>
        public I18NString?   Name           { get; } = Name;

        /// <summary>An optional description.</summary>
        public RichContent?  Description    { get; } = Description;

        /// <summary>The result itself.</summary>
        public DCCData?      Data           { get; } = Data;

        /// <summary>Read a result out of its JSON.</summary>
        public static DCCResult Parse(JObject JSON)

            => new (
                   DCC.I18N  (JSON, "name"),
                   DCC.Object(JSON, "description") is JObject description ? RichContent.Parse(description) : null,
                   DCC.Object(JSON, "data")        is JObject data        ? DCCData.Parse(data)            : null,
                   DCC.Text  (JSON, "id"),
                   DCC.Texts (JSON, "refId"),
                   DCC.Text  (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this result.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "name",         Name);
            DCC.AddIfSet(json, "description",  Description);
            DCC.AddIfSet(json, "data",         Data);

            return WithIdentification(json);

        }

    }


    /// <summary>A condition that influenced the calibration, and when it applied.</summary>
    public class InfluenceCondition(I18NString?           Name         = null,
                                    RichContent?          Description  = null,
                                    String?               Status       = null,
                                    DCCHash?              Certificate  = null,
                                    DCCData?              Data         = null,
                                    String?               Id           = null,
                                    String?               RefType      = null)

        : ADCCElement(Id, null, RefType)

    {

        /// <summary>The name of the condition.</summary>
        public I18NString?   Name           { get; } = Name;

        /// <summary>An optional description.</summary>
        public RichContent?  Description    { get; } = Description;

        /// <summary>Whether this was before or after an adjustment or a repair.</summary>
        public String?       Status         { get; } = Status;

        /// <summary>A certificate backing the condition.</summary>
        public DCCHash?      Certificate    { get; } = Certificate;

        /// <summary>The condition itself.</summary>
        public DCCData?      Data           { get; } = Data;

        /// <summary>Read a condition out of its JSON.</summary>
        public static InfluenceCondition Parse(JObject JSON)

            => new (
                   DCC.I18N  (JSON, "name"),
                   DCC.Object(JSON, "description") is JObject description ? RichContent.Parse(description) : null,
                   DCC.Text  (JSON, "status"),
                   DCC.Object(JSON, "certificate") is JObject certificate ? DCCHash.Parse(certificate)     : null,
                   DCC.Object(JSON, "data")        is JObject data        ? DCCData.Parse(data)            : null,
                   DCC.Text  (JSON, "id"),
                   DCC.Text  (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this condition.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "name",         Name);
            DCC.AddIfSet(json, "description",  Description);
            DCC.AddIfSet(json, "status",       Status);
            DCC.AddIfSet(json, "certificate",  Certificate);
            DCC.AddIfSet(json, "data",         Data);

            return WithIdentification(json);

        }

    }


    /// <summary>A statement about the calibration and who stands behind it.</summary>
    public class StatementMetaData(I18NString?           Name              = null,
                                   RichContent?          Description       = null,
                                   IEnumerable<String>?  CountryCodes      = null,
                                   String?               Convention        = null,
                                   Boolean?              Traceable         = null,
                                   IEnumerable<String>?  Norms             = null,
                                   IEnumerable<String>?  References        = null,
                                   RichContent?          Declaration       = null,
                                   Boolean?              Valid             = null,
                                   IEnumerable<Boolean>? ValidList         = null,
                                   String?               Date              = null,
                                   String?               Period            = null,
                                   DCCContact?           RespAuthority     = null,
                                   String?               Conformity        = null,
                                   IEnumerable<String>?  ConformityList    = null,
                                   DCCData?              Data              = null,
                                   String?               NonSIDefinition   = null,
                                   String?               NonSIUnit         = null,
                                   Location?             Location          = null,
                                   String?               Id                = null,
                                   IEnumerable<String>?  RefIds            = null,
                                   String?               RefType           = null)

        : ADCCElement(Id, RefIds, RefType)

    {

        /// <summary>The name of the statement.</summary>
        public I18NString?             Name               { get; } = Name;

        /// <summary>An optional description.</summary>
        public RichContent?            Description        { get; } = Description;

        /// <summary>The countries the statement applies in.</summary>
        public IReadOnlyList<String>   CountryCodes       { get; } = CountryCodes?.  ToArray() ?? [];

        /// <summary>The convention the statement follows.</summary>
        public String?                 Convention         { get; } = Convention;

        /// <summary>Whether the statement is traceable.</summary>
        public Boolean?                Traceable          { get; } = Traceable;

        /// <summary>The norms the statement rests on.</summary>
        public IReadOnlyList<String>   Norms              { get; } = Norms?.         ToArray() ?? [];

        /// <summary>References backing the statement.</summary>
        public IReadOnlyList<String>   References         { get; } = References?.    ToArray() ?? [];

        /// <summary>An optional declaration.</summary>
        public RichContent?            Declaration        { get; } = Declaration;

        /// <summary>Whether the statement holds.</summary>
        public Boolean?                Valid              { get; } = Valid;

        /// <summary>Whether each part of the statement holds.</summary>
        public IReadOnlyList<Boolean>  ValidList          { get; } = ValidList?.     ToArray() ?? [];

        /// <summary>When the statement was made.</summary>
        public String?                 Date               { get; } = Date;

        /// <summary>How long it holds, as an ISO 8601 duration.</summary>
        public String?                 Period             { get; } = Period;

        /// <summary>Who stands behind it.</summary>
        public DCCContact?             RespAuthority      { get; } = RespAuthority;

        /// <summary>What it conforms to.</summary>
        public String?                 Conformity         { get; } = Conformity;

        /// <summary>What each part of it conforms to.</summary>
        public IReadOnlyList<String>   ConformityList     { get; } = ConformityList?.ToArray() ?? [];

        /// <summary>Supporting data.</summary>
        public DCCData?                Data               { get; } = Data;

        /// <summary>The definition of a unit outside the SI.</summary>
        public String?                 NonSIDefinition    { get; } = NonSIDefinition;

        /// <summary>A unit outside the SI.</summary>
        public String?                 NonSIUnit          { get; } = NonSIUnit;

        /// <summary>Where the statement applies.</summary>
        public Location?               Location           { get; } = Location;

        /// <summary>Read a statement out of its JSON.</summary>
        public static StatementMetaData Parse(JObject JSON)

            => new (
                   DCC.I18N     (JSON, "name"),
                   DCC.Object   (JSON, "description")   is JObject description   ? RichContent.Parse(description) : null,
                   DCC.Texts    (JSON, "countryCodeISO3166_1"),
                   DCC.Text     (JSON, "convention"),
                   DCC.Flag     (JSON, "traceable"),
                   DCC.Texts    (JSON, "norm"),
                   DCC.Texts    (JSON, "reference"),
                   DCC.Object   (JSON, "declaration")   is JObject declaration   ? RichContent.Parse(declaration) : null,
                   DCC.Flag     (JSON, "valid"),
                   JSON["validXMLList"] is JArray validList
                       ? validList.Where (element => element.Type == JTokenType.Boolean).
                                   Select(element => element.Value<Boolean>())
                       : null,
                   DCC.Timestamp(JSON, "date"),
                   DCC.Text     (JSON, "period"),
                   DCC.Object   (JSON, "respAuthority") is JObject respAuthority ? DCCContact.Parse(respAuthority) : null,
                   DCC.Text     (JSON, "conformity"),
                   DCC.Texts    (JSON, "conformityXMLList"),
                   DCC.Object   (JSON, "data")          is JObject data          ? DCCData.Parse(data)             : null,
                   DCC.Text     (JSON, "nonSIDefinition"),
                   DCC.Text     (JSON, "nonSIUnit"),
                   DCC.Object   (JSON, "location")      is JObject location      ? QIDigital.Location.Parse(location) : null,
                   DCC.Text     (JSON, "id"),
                   DCC.Texts    (JSON, "refId"),
                   DCC.Text     (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this statement.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "name",                  Name);
            DCC.AddIfSet(json, "description",           Description);
            DCC.AddIfSet(json, "countryCodeISO3166_1",  CountryCodes);
            DCC.AddIfSet(json, "convention",            Convention);
            DCC.AddIfSet(json, "traceable",             Traceable);
            DCC.AddIfSet(json, "norm",                  Norms);
            DCC.AddIfSet(json, "reference",             References);
            DCC.AddIfSet(json, "declaration",           Declaration);
            DCC.AddIfSet(json, "valid",                 Valid);
            DCC.AddIfSet(json, "validXMLList",          ValidList);
            DCC.AddIfSet(json, "date",                  Date);
            DCC.AddIfSet(json, "period",                Period);
            DCC.AddIfSet(json, "respAuthority",         RespAuthority);
            DCC.AddIfSet(json, "conformity",            Conformity);
            DCC.AddIfSet(json, "conformityXMLList",     ConformityList);
            DCC.AddIfSet(json, "data",                  Data);
            DCC.AddIfSet(json, "nonSIDefinition",       NonSIDefinition);
            DCC.AddIfSet(json, "nonSIUnit",             NonSIUnit);
            DCC.AddIfSet(json, "location",              Location);

            return WithIdentification(json);

        }

    }


    /// <summary>
    /// One measurement result, with the methods, software and equipment the
    /// laboratory used to arrive at it.
    /// </summary>
    public class MeasurementResult(I18NString?                      Name                 = null,
                                   RichContent?                     Description          = null,
                                   IEnumerable<UsedMethod>?         UsedMethods          = null,
                                   IEnumerable<DCCSoftware>?        UsedSoftware         = null,
                                   IEnumerable<MeasuringEquipment>? MeasuringEquipments  = null,
                                   IEnumerable<InfluenceCondition>? InfluenceConditions  = null,
                                   IEnumerable<DCCResult>?          Results              = null,
                                   IEnumerable<StatementMetaData>?  MeasurementMetaData  = null,
                                   String?                          Id                   = null,
                                   IEnumerable<String>?             RefIds               = null,
                                   String?                          RefType              = null)

        : ADCCElement(Id, RefIds, RefType)

    {

        /// <summary>The name of the result.</summary>
        public I18NString?                       Name                   { get; } = Name;

        /// <summary>An optional description.</summary>
        public RichContent?                      Description            { get; } = Description;

        /// <summary>How the measurement was made.</summary>
        public IReadOnlyList<UsedMethod>         UsedMethods            { get; } = UsedMethods?.        ToArray() ?? [];

        /// <summary>What software was used.</summary>
        public IReadOnlyList<DCCSoftware>        UsedSoftware           { get; } = UsedSoftware?.       ToArray() ?? [];

        /// <summary>What equipment was used.</summary>
        public IReadOnlyList<MeasuringEquipment> MeasuringEquipments    { get; } = MeasuringEquipments?.ToArray() ?? [];

        /// <summary>What influenced the measurement.</summary>
        public IReadOnlyList<InfluenceCondition> InfluenceConditions    { get; } = InfluenceConditions?.ToArray() ?? [];

        /// <summary>The results themselves.</summary>
        public IReadOnlyList<DCCResult>          Results                { get; } = Results?.            ToArray() ?? [];

        /// <summary>Statements about the measurement.</summary>
        public IReadOnlyList<StatementMetaData>  MeasurementMetaData    { get; } = MeasurementMetaData?.ToArray() ?? [];

        /// <summary>Read a measurement result out of its JSON.</summary>
        public static MeasurementResult Parse(JObject JSON)

            => new (
                   DCC.I18N  (JSON, "name"),
                   DCC.Object(JSON, "description") is JObject description ? RichContent.Parse(description) : null,
                   DCC.Array (JSON, "usedMethods",          UsedMethod.Parse),
                   DCC.Array (JSON, "usedSoftware",         DCCSoftware.Parse),
                   DCC.Array (JSON, "measuringEquipments",  MeasuringEquipment.Parse),
                   DCC.Array (JSON, "influenceConditions",  InfluenceCondition.Parse),
                   DCC.Array (JSON, "results",              DCCResult.Parse),
                   DCC.Array (JSON, "measurementMetaData",  StatementMetaData.Parse),
                   DCC.Text  (JSON, "id"),
                   DCC.Texts (JSON, "refId"),
                   DCC.Text  (JSON, "refType")
               );

        /// <summary>Return a JSON representation of this measurement result.</summary>
        public override JObject ToJSON()
        {

            var json = new JObject();

            DCC.AddIfSet(json, "name",                 Name);
            DCC.AddIfSet(json, "description",          Description);
            DCC.AddIfSet(json, "usedMethods",          UsedMethods);
            DCC.AddIfSet(json, "usedSoftware",         UsedSoftware);
            DCC.AddIfSet(json, "measuringEquipments",  MeasuringEquipments);
            DCC.AddIfSet(json, "influenceConditions",  InfluenceConditions);
            DCC.AddIfSet(json, "results",              Results);
            DCC.AddIfSet(json, "measurementMetaData",  MeasurementMetaData);

            return WithIdentification(json);

        }

    }

}
