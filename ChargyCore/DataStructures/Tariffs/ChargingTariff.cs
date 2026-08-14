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
    /// A day of the week, as used by the tariff restrictions of OCPI.
    /// </summary>
    public enum DayOfWeek
    {

        /// <summary>Sunday.</summary>
        Sunday    = 0,

        /// <summary>Monday.</summary>
        Monday    = 1,

        /// <summary>Tuesday.</summary>
        Tuesday   = 2,

        /// <summary>Wednesday.</summary>
        Wednesday = 3,

        /// <summary>Thursday.</summary>
        Thursday  = 4,

        /// <summary>Friday.</summary>
        Friday    = 5,

        /// <summary>Saturday.</summary>
        Saturday  = 6

    }


    /// <summary>
    /// A value added tax applied to a charging tariff.
    /// </summary>
    /// <param name="Id">The identification of the tax.</param>
    /// <param name="Percentage">The tax rate in percent.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    public class Taxes(String                Id,
                       Decimal               Percentage,
                       I18NString?           Description  = null,
                       IEnumerable<String>?  Context      = null)
    {

        #region Properties

        /// <summary>The identification of the tax.</summary>
        public String                 Id               { get; } = Id;

        /// <summary>The tax rate in percent.</summary>
        public Decimal                Percentage       { get; } = Percentage;

        /// <summary>An optional multi-language description.</summary>
        public I18NString?            Description      { get; } = Description;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>  JSONLDContext    { get; } = Context?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out Taxes)

        /// <summary>
        /// Try to parse the given JSON as a tax.
        /// </summary>
        /// <param name="JSON">A JSON representation of a tax.</param>
        /// <param name="Taxes">The parsed tax.</param>
        public static Boolean TryParse(JObject JSON, out Taxes? Taxes)
        {

            Taxes = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            Taxes = new Taxes(
                        id,
                        JSON["percentage"]?.Value<Decimal>() ?? 0,
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
        /// Return a JSON representation of this tax.
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

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",  Description.ToJSON()));

            json.Add(new JProperty("percentage",       Percentage));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this tax.
        /// </summary>
        public override String ToString()

            => $"{Id}: {Percentage}%";

        #endregion


    }


    /// <summary>
    /// One price of a tariff element, e.g. "0.29 EUR per kWh in steps of 1 Wh".
    /// </summary>
    /// <param name="Type">What is being charged for, e.g. "ENERGY", "TIME" or "FLAT".</param>
    /// <param name="Price">The price per <paramref name="StepSize"/>.</param>
    /// <param name="StepSize">The billing increment.</param>
    public class PriceComponent(String   Type,
                                Decimal  Price,
                                Int64    StepSize)
    {

        #region Properties

        /// <summary>What is being charged for, e.g. "ENERGY", "TIME" or "FLAT".</summary>
        public String   Type        { get; } = Type;

        /// <summary>The price per <see cref="StepSize"/>.</summary>
        public Decimal  Price       { get; } = Price;

        /// <summary>The billing increment.</summary>
        public Int64    StepSize    { get; } = StepSize;

        #endregion


        #region (static) TryParse(JSON, out PriceComponent)

        /// <summary>
        /// Try to parse the given JSON as a price component.
        /// </summary>
        /// <param name="JSON">A JSON representation of a price component.</param>
        /// <param name="PriceComponent">The parsed price component.</param>
        public static Boolean TryParse(JObject JSON, out PriceComponent? PriceComponent)
        {

            PriceComponent = null;

            var type = JSON["type"]?.Value<String>();

            if (type is null)
                return false;

            PriceComponent = new PriceComponent(
                                 type,
                                 JSON["price"]?.    Value<Decimal>() ?? 0,
                                 JSON["step_size"]?.Value<Int64>()   ?? 0
                             );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this price component.
        /// </summary>
        public JObject ToJSON()

            => new (
                   new JProperty("type",       Type),
                   new JProperty("price",      Price),
                   new JProperty("step_size",  StepSize)
               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this price component.
        /// </summary>
        public override String ToString()

            => $"{Price} per {StepSize} {Type}";

        #endregion


    }


    /// <summary>
    /// When a tariff element applies, e.g. "only on weekdays between 8 and 18
    /// o'clock, and only above 10 kWh".
    /// </summary>
    /// <param name="StartTime">An optional start time of day.</param>
    /// <param name="EndTime">An optional end time of day.</param>
    /// <param name="StartDate">An optional start date.</param>
    /// <param name="EndDate">An optional end date.</param>
    /// <param name="MinkWh">An optional minimum amount of energy.</param>
    /// <param name="MaxkWh">An optional maximum amount of energy.</param>
    /// <param name="MinPower">An optional minimum charging power.</param>
    /// <param name="MaxPower">An optional maximum charging power.</param>
    /// <param name="MinDuration">An optional minimum duration in seconds.</param>
    /// <param name="MaxDuration">An optional maximum duration in seconds.</param>
    /// <param name="DayOfWeek">Optional days of the week this element applies on.</param>
    public class TariffRestriction(String?                   StartTime    = null,
                                   String?                   EndTime      = null,
                                   String?                   StartDate    = null,
                                   String?                   EndDate      = null,
                                   Decimal?                  MinkWh       = null,
                                   Decimal?                  MaxkWh       = null,
                                   Decimal?                  MinPower     = null,
                                   Decimal?                  MaxPower     = null,
                                   Int64?                    MinDuration  = null,
                                   Int64?                    MaxDuration  = null,
                                   IEnumerable<DayOfWeek>?   DayOfWeek    = null)
    {

        #region Properties

        /// <summary>An optional start time of day.</summary>
        public String?                    StartTime      { get; } = StartTime;

        /// <summary>An optional end time of day.</summary>
        public String?                    EndTime        { get; } = EndTime;

        /// <summary>An optional start date.</summary>
        public String?                    StartDate      { get; } = StartDate;

        /// <summary>An optional end date.</summary>
        public String?                    EndDate        { get; } = EndDate;

        /// <summary>An optional minimum amount of energy.</summary>
        public Decimal?                   MinkWh         { get; } = MinkWh;

        /// <summary>An optional maximum amount of energy.</summary>
        public Decimal?                   MaxkWh         { get; } = MaxkWh;

        /// <summary>An optional minimum charging power.</summary>
        public Decimal?                   MinPower       { get; } = MinPower;

        /// <summary>An optional maximum charging power.</summary>
        public Decimal?                   MaxPower       { get; } = MaxPower;

        /// <summary>An optional minimum duration in seconds.</summary>
        public Int64?                     MinDuration    { get; } = MinDuration;

        /// <summary>An optional maximum duration in seconds.</summary>
        public Int64?                     MaxDuration    { get; } = MaxDuration;

        /// <summary>Optional days of the week this element applies on.</summary>
        public IReadOnlyList<DayOfWeek>   DayOfWeek      { get; } = DayOfWeek?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out TariffRestriction)

        /// <summary>
        /// Try to parse the given JSON as a tariff restriction.
        /// </summary>
        /// <param name="JSON">A JSON representation of a tariff restriction.</param>
        /// <param name="TariffRestriction">The parsed tariff restriction.</param>
        public static Boolean TryParse(JObject JSON, out TariffRestriction? TariffRestriction)
        {

            var daysOfWeek = new List<DayOfWeek>();

            if (JSON["day_of_week"] is JArray dayArray)
                foreach (var day in dayArray)
                {

                    if (day.Type == JTokenType.Integer &&
                        Enum.IsDefined((DayOfWeek) day.Value<Int32>()))
                    {
                        daysOfWeek.Add((DayOfWeek) day.Value<Int32>());
                    }

                    else if (day.Type == JTokenType.String &&
                             Enum.TryParse<DayOfWeek>(day.Value<String>(), true, out var parsedDay))
                    {
                        daysOfWeek.Add(parsedDay);
                    }

                }

            TariffRestriction = new TariffRestriction(
                                    JSON["start_time"]?.  Value<String>(),
                                    JSON["end_time"]?.    Value<String>(),
                                    JSON["start_date"]?.  Value<String>(),
                                    JSON["end_date"]?.    Value<String>(),
                                    JSON["min_kwh"]?.     Value<Decimal>(),
                                    JSON["max_kwh"]?.     Value<Decimal>(),
                                    JSON["min_power"]?.   Value<Decimal>(),
                                    JSON["max_power"]?.   Value<Decimal>(),
                                    JSON["min_duration"]?.Value<Int64>(),
                                    JSON["max_duration"]?.Value<Int64>(),
                                    daysOfWeek
                                );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this tariff restriction.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (StartTime is not null)
                json.Add(new JProperty("start_time",    StartTime));

            if (EndTime   is not null)
                json.Add(new JProperty("end_time",      EndTime));

            if (StartDate is not null)
                json.Add(new JProperty("start_date",    StartDate));

            if (EndDate   is not null)
                json.Add(new JProperty("end_date",      EndDate));

            if (MinkWh.     HasValue)
                json.Add(new JProperty("min_kwh",       MinkWh.     Value));

            if (MaxkWh.     HasValue)
                json.Add(new JProperty("max_kwh",       MaxkWh.     Value));

            if (MinPower.   HasValue)
                json.Add(new JProperty("min_power",     MinPower.   Value));

            if (MaxPower.   HasValue)
                json.Add(new JProperty("max_power",     MaxPower.   Value));

            if (MinDuration.HasValue)
                json.Add(new JProperty("min_duration",  MinDuration.Value));

            if (MaxDuration.HasValue)
                json.Add(new JProperty("max_duration",  MaxDuration.Value));

            if (DayOfWeek.Count > 0)
                json.Add(new JProperty("day_of_week",   new JArray(DayOfWeek.Select(day => (Int32) day))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this tariff restriction.
        /// </summary>
        public override String ToString()

            => String.Join(
                   ", ",
                   new[] {
                       StartTime is not null || EndTime is not null ? $"{StartTime ?? ""}-{EndTime ?? ""}" : null,
                       DayOfWeek.Count > 0                          ? String.Join("/", DayOfWeek)          : null
                   }.
                   Where(part => part is not null)
               );

        #endregion


    }


    /// <summary>
    /// One block of a tariff: a set of prices and the conditions under which they
    /// apply.
    /// </summary>
    /// <param name="PriceComponents">The prices of this tariff element.</param>
    /// <param name="Restrictions">Optional conditions under which this element applies.</param>
    public class ChargingTariffElement(IEnumerable<PriceComponent>  PriceComponents,
                                       TariffRestriction?           Restrictions = null)
    {

        #region Properties

        /// <summary>The prices of this tariff element.</summary>
        public IReadOnlyList<PriceComponent>  PriceComponents    { get; } = PriceComponents.ToArray();

        /// <summary>Optional conditions under which this element applies.</summary>
        public TariffRestriction?             Restrictions       { get; } = Restrictions;

        #endregion


        #region (static) TryParse(JSON, out ChargingTariffElement)

        /// <summary>
        /// Try to parse the given JSON as a tariff element.
        /// </summary>
        /// <param name="JSON">A JSON representation of a tariff element.</param>
        /// <param name="ChargingTariffElement">The parsed tariff element.</param>
        public static Boolean TryParse(JObject JSON, out ChargingTariffElement? ChargingTariffElement)
        {

            ChargingTariffElement = null;

            if (JSON["price_components"] is not JArray priceComponentArray)
                return false;

            var priceComponents = new List<PriceComponent>();

            foreach (var priceComponentJSON in priceComponentArray.OfType<JObject>())
                if (PriceComponent.TryParse(priceComponentJSON, out var priceComponent))
                    priceComponents.Add(priceComponent!);

            TariffRestriction? restrictions = null;

            if (JSON["restrictions"] is JObject restrictionsJSON)
                TariffRestriction.TryParse(restrictionsJSON, out restrictions);

            ChargingTariffElement = new ChargingTariffElement(
                                        priceComponents,
                                        restrictions
                                    );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this tariff element.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("price_components", new JArray(PriceComponents.Select(component => component.ToJSON())))
                       );

            if (Restrictions is not null)
                json.Add(new JProperty("restrictions", Restrictions.ToJSON()));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this tariff element.
        /// </summary>
        public override String ToString()

            => $"{PriceComponents.Count} price component(s)";

        #endregion


    }


    /// <summary>
    /// A piece of text in one particular language, as used by the alternative
    /// tariff texts of OCPI.
    /// </summary>
    /// <param name="Language">The language of the text.</param>
    /// <param name="Text">The text.</param>
    public class DisplayText(String  Language,
                             String  Text)
    {

        #region Properties

        /// <summary>The language of the text.</summary>
        public String  Language    { get; } = Language;

        /// <summary>The text.</summary>
        public String  Text        { get; } = Text;

        #endregion


        #region (static) TryParse(JSON, out DisplayText)

        /// <summary>
        /// Try to parse the given JSON as a display text.
        /// </summary>
        /// <param name="JSON">A JSON representation of a display text.</param>
        /// <param name="DisplayText">The parsed display text.</param>
        public static Boolean TryParse(JObject JSON, out DisplayText? DisplayText)
        {

            DisplayText = null;

            var language = JSON["language"]?.Value<String>();
            var text     = JSON["text"]?.    Value<String>();

            if (language is null || text is null)
                return false;

            DisplayText = new DisplayText(language, text);

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this display text.
        /// </summary>
        public JObject ToJSON()

            => new (
                   new JProperty("language",  Language),
                   new JProperty("text",      Text)
               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this display text.
        /// </summary>
        public override String ToString()

            => $"[{Language}] {Text}";

        #endregion


    }


    /// <summary>
    /// What an EV driver is charged for a charging session, modelled after
    /// OCPI v2.1.1 with the Chargy extensions.
    /// </summary>
    /// <param name="Id">The identification of the tariff.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="CountryCode">An optional country code of the operator.</param>
    /// <param name="PartyId">An optional party identification of the operator.</param>
    /// <param name="ShortName">An optional multi-language short name.</param>
    /// <param name="Summary">An optional multi-language summary.</param>
    /// <param name="TariffAltURL">An optional URL with the full tariff.</param>
    /// <param name="Currency">An optional currency.</param>
    /// <param name="Taxes">Optional taxes.</param>
    /// <param name="Elements">The tariff elements.</param>
    /// <param name="NotBefore">An optional start of the validity period.</param>
    /// <param name="NotAfter">An optional end of the validity period.</param>
    /// <param name="Created">An optional creation timestamp.</param>
    /// <param name="LastUpdated">An optional last update timestamp.</param>
    /// <param name="Signatures">Optional signatures over this tariff.</param>
    public class ChargingTariff(String                              Id,
                                IEnumerable<String>?                Context       = null,
                                String?                             CountryCode   = null,
                                String?                             PartyId       = null,
                                I18NString?                         ShortName     = null,
                                I18NString?                         Summary       = null,
                                String?                             TariffAltURL  = null,
                                String?                             Currency      = null,
                                IEnumerable<Taxes>?                 Taxes         = null,
                                IEnumerable<ChargingTariffElement>? Elements      = null,
                                String?                             NotBefore     = null,
                                String?                             NotAfter      = null,
                                String?                             Created       = null,
                                String?                             LastUpdated   = null,
                                IEnumerable<SignatureRS>?           Signatures    = null)
    {

        #region Properties

        /// <summary>The identification of the tariff.</summary>
        public String                                  Id              { get; } = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>                   JSONLDContext   { get; } = Context?.   ToArray() ?? [];

        /// <summary>An optional country code of the operator.</summary>
        public String?                                 CountryCode     { get; } = CountryCode;

        /// <summary>An optional party identification of the operator.</summary>
        public String?                                 PartyId         { get; } = PartyId;

        /// <summary>An optional multi-language short name.</summary>
        public I18NString?                             ShortName       { get; } = ShortName;

        /// <summary>An optional multi-language summary.</summary>
        public I18NString?                             Summary         { get; } = Summary;

        /// <summary>An optional URL with the full tariff.</summary>
        public String?                                 TariffAltURL    { get; } = TariffAltURL;

        /// <summary>An optional currency.</summary>
        public String?                                 Currency        { get; } = Currency;

        /// <summary>Optional taxes.</summary>
        public IReadOnlyList<Taxes>                    Taxes           { get; } = Taxes?.     ToArray() ?? [];

        /// <summary>The tariff elements.</summary>
        public IReadOnlyList<ChargingTariffElement>    Elements        { get; } = Elements?.  ToArray() ?? [];

        /// <summary>An optional start of the validity period.</summary>
        public String?                                 NotBefore       { get; } = NotBefore;

        /// <summary>An optional end of the validity period.</summary>
        public String?                                 NotAfter        { get; } = NotAfter;

        /// <summary>An optional creation timestamp.</summary>
        public String?                                 Created         { get; } = Created;

        /// <summary>An optional last update timestamp.</summary>
        public String?                                 LastUpdated     { get; } = LastUpdated;

        /// <summary>Optional signatures over this tariff.</summary>
        public IReadOnlyList<SignatureRS>              Signatures      { get; } = Signatures?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out ChargingTariff)

        /// <summary>
        /// Try to parse the given JSON as a charging tariff.
        /// </summary>
        /// <param name="JSON">A JSON representation of a charging tariff.</param>
        /// <param name="ChargingTariff">The parsed charging tariff.</param>
        public static Boolean TryParse(JObject JSON, out ChargingTariff? ChargingTariff)
        {

            ChargingTariff = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            ChargingTariff = new ChargingTariff(
                                 id,
                                 PublicKey.ParseContext(JSON["@context"]),
                                 JSON["country_code"]?.  Value<String>(),
                                 JSON["party_id"]?.      Value<String>(),
                                 JSON["shortName"] is JObject shortNameJSON
                                     ? I18NString.Parse(shortNameJSON)
                                     : null,
                                 JSON["summary"]   is JObject summaryJSON
                                     ? I18NString.Parse(summaryJSON)
                                     : null,
                                 JSON["tariff_alt_url"]?.Value<String>(),
                                 JSON["currency"]?.      Value<String>(),
                                 TariffLists.ParseTaxes   (JSON["taxes"]),
                                 TariffLists.ParseElements(JSON["elements"]),
                                 JSON["not_before"]?.    Value<String>(),
                                 JSON["not_after"]?.     Value<String>(),
                                 JSON["created"]?.       Value<String>(),
                                 JSON["last_updated"]?.  Value<String>(),
                                 TariffLists.ParseSignatures(JSON["signatures"])
                             );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this charging tariff.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",        JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",        new JArray(JSONLDContext)));

            if (CountryCode  is not null)
                json.Add(new JProperty("country_code",    CountryCode));

            if (PartyId      is not null)
                json.Add(new JProperty("party_id",        PartyId));

            if (ShortName.IsNotNullOrEmpty())
                json.Add(new JProperty("shortName",       ShortName.ToJSON()));

            if (Summary.  IsNotNullOrEmpty())
                json.Add(new JProperty("summary",         Summary.  ToJSON()));

            if (TariffAltURL is not null)
                json.Add(new JProperty("tariff_alt_url",  TariffAltURL));

            if (Currency     is not null)
                json.Add(new JProperty("currency",        Currency));

            if (Taxes.     Count > 0)
                json.Add(new JProperty("taxes",           new JArray(Taxes.     Select(tax       => tax.      ToJSON()))));

            if (Elements.  Count > 0)
                json.Add(new JProperty("elements",        new JArray(Elements.  Select(element   => element.  ToJSON()))));

            if (NotBefore    is not null)
                json.Add(new JProperty("not_before",      NotBefore));

            if (NotAfter     is not null)
                json.Add(new JProperty("not_after",       NotAfter));

            if (Created      is not null)
                json.Add(new JProperty("created",         Created));

            if (LastUpdated  is not null)
                json.Add(new JProperty("last_updated",    LastUpdated));

            if (Signatures.Count > 0)
                json.Add(new JProperty("signatures",      new JArray(Signatures.Select(signature => signature.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this charging tariff.
        /// </summary>
        public override String ToString()

            => $"{Id}: {Elements.Count} element(s)";

        #endregion


    }


    /// <summary>
    /// What an EV driver is charged for occupying a parking space, e.g. after the
    /// battery is full.
    /// </summary>
    /// <param name="Id">The identification of the tariff.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="CountryCode">An optional country code of the operator.</param>
    /// <param name="PartyId">An optional party identification of the operator.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="TariffAltText">Optional alternative tariff texts.</param>
    /// <param name="TariffAltURL">An optional URL with the full tariff.</param>
    /// <param name="Currency">An optional currency.</param>
    /// <param name="Taxes">Optional taxes.</param>
    /// <param name="Elements">The tariff elements.</param>
    /// <param name="NotBefore">An optional start of the validity period.</param>
    /// <param name="NotAfter">An optional end of the validity period.</param>
    /// <param name="Created">An optional creation timestamp.</param>
    /// <param name="LastUpdated">An optional last update timestamp.</param>
    /// <param name="Signatures">Optional signatures over this tariff.</param>
    public class ParkingTariff(String                              Id,
                               IEnumerable<String>?                Context        = null,
                               String?                             CountryCode    = null,
                               String?                             PartyId        = null,
                               I18NString?                         Description    = null,
                               IEnumerable<DisplayText>?           TariffAltText  = null,
                               String?                             TariffAltURL   = null,
                               String?                             Currency       = null,
                               IEnumerable<Taxes>?                 Taxes          = null,
                               IEnumerable<ChargingTariffElement>? Elements       = null,
                               String?                             NotBefore      = null,
                               String?                             NotAfter       = null,
                               String?                             Created        = null,
                               String?                             LastUpdated    = null,
                               IEnumerable<SignatureRS>?           Signatures     = null)
    {

        #region Properties

        /// <summary>The identification of the tariff.</summary>
        public String                                  Id               { get; } = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>                   JSONLDContext    { get; } = Context?.      ToArray() ?? [];

        /// <summary>An optional country code of the operator.</summary>
        public String?                                 CountryCode      { get; } = CountryCode;

        /// <summary>An optional party identification of the operator.</summary>
        public String?                                 PartyId          { get; } = PartyId;

        /// <summary>An optional multi-language description.</summary>
        public I18NString?                             Description      { get; } = Description;

        /// <summary>Optional alternative tariff texts.</summary>
        public IReadOnlyList<DisplayText>              TariffAltText    { get; } = TariffAltText?.ToArray() ?? [];

        /// <summary>An optional URL with the full tariff.</summary>
        public String?                                 TariffAltURL     { get; } = TariffAltURL;

        /// <summary>An optional currency.</summary>
        public String?                                 Currency         { get; } = Currency;

        /// <summary>Optional taxes.</summary>
        public IReadOnlyList<Taxes>                    Taxes            { get; } = Taxes?.        ToArray() ?? [];

        /// <summary>The tariff elements.</summary>
        public IReadOnlyList<ChargingTariffElement>    Elements         { get; } = Elements?.     ToArray() ?? [];

        /// <summary>An optional start of the validity period.</summary>
        public String?                                 NotBefore        { get; } = NotBefore;

        /// <summary>An optional end of the validity period.</summary>
        public String?                                 NotAfter         { get; } = NotAfter;

        /// <summary>An optional creation timestamp.</summary>
        public String?                                 Created          { get; } = Created;

        /// <summary>An optional last update timestamp.</summary>
        public String?                                 LastUpdated      { get; } = LastUpdated;

        /// <summary>Optional signatures over this tariff.</summary>
        public IReadOnlyList<SignatureRS>              Signatures       { get; } = Signatures?.   ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out ParkingTariff)

        /// <summary>
        /// Try to parse the given JSON as a parking tariff.
        /// </summary>
        /// <param name="JSON">A JSON representation of a parking tariff.</param>
        /// <param name="ParkingTariff">The parsed parking tariff.</param>
        public static Boolean TryParse(JObject JSON, out ParkingTariff? ParkingTariff)
        {

            ParkingTariff = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            var tariffAltText = new List<DisplayText>();

            if (JSON["tariff_alt_text"] is JArray textArray)
                foreach (var textJSON in textArray.OfType<JObject>())
                    if (DisplayText.TryParse(textJSON, out var displayText))
                        tariffAltText.Add(displayText!);

            ParkingTariff = new ParkingTariff(
                                id,
                                PublicKey.ParseContext(JSON["@context"]),
                                JSON["country_code"]?.  Value<String>(),
                                JSON["party_id"]?.      Value<String>(),
                                JSON["description"] is JObject descriptionJSON
                                    ? I18NString.Parse(descriptionJSON)
                                    : null,
                                tariffAltText,
                                JSON["tariff_alt_url"]?.Value<String>(),
                                JSON["currency"]?.      Value<String>(),
                                TariffLists.ParseTaxes     (JSON["taxes"]),
                                TariffLists.ParseElements  (JSON["elements"]),
                                JSON["not_before"]?.    Value<String>(),
                                JSON["not_after"]?.     Value<String>(),
                                JSON["created"]?.       Value<String>(),
                                JSON["last_updated"]?.  Value<String>(),
                                TariffLists.ParseSignatures(JSON["signatures"])
                            );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this parking tariff.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",         JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",         new JArray(JSONLDContext)));

            if (CountryCode  is not null)
                json.Add(new JProperty("country_code",     CountryCode));

            if (PartyId      is not null)
                json.Add(new JProperty("party_id",         PartyId));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",      Description.ToJSON()));

            if (TariffAltText.Count > 0)
                json.Add(new JProperty("tariff_alt_text",  new JArray(TariffAltText.Select(text      => text.     ToJSON()))));

            if (TariffAltURL is not null)
                json.Add(new JProperty("tariff_alt_url",   TariffAltURL));

            if (Currency     is not null)
                json.Add(new JProperty("currency",         Currency));

            if (Taxes.     Count > 0)
                json.Add(new JProperty("taxes",            new JArray(Taxes.     Select(tax       => tax.      ToJSON()))));

            if (Elements.  Count > 0)
                json.Add(new JProperty("elements",         new JArray(Elements.  Select(element   => element.  ToJSON()))));

            if (NotBefore    is not null)
                json.Add(new JProperty("not_before",       NotBefore));

            if (NotAfter     is not null)
                json.Add(new JProperty("not_after",        NotAfter));

            if (Created      is not null)
                json.Add(new JProperty("created",          Created));

            if (LastUpdated  is not null)
                json.Add(new JProperty("last_updated",     LastUpdated));

            if (Signatures.Count > 0)
                json.Add(new JProperty("signatures",       new JArray(Signatures.Select(signature => signature.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this parking tariff.
        /// </summary>
        public override String ToString()

            => $"{Id}: {Elements.Count} element(s)";

        #endregion


    }


    /// <summary>
    /// Helpers for the repeating list shapes of the tariff data structures.
    /// </summary>
    internal static class TariffLists
    {

        #region ParseTaxes     (JSON)

        internal static List<Taxes> ParseTaxes(JToken? JSON)
        {

            var taxes = new List<Taxes>();

            if (JSON is JArray array)
                foreach (var taxJSON in array.OfType<JObject>())
                    if (chargy.Taxes.TryParse(taxJSON, out var tax))
                        taxes.Add(tax!);

            return taxes;

        }

        #endregion

        #region ParseElements  (JSON)

        internal static List<ChargingTariffElement> ParseElements(JToken? JSON)
        {

            var elements = new List<ChargingTariffElement>();

            if (JSON is JArray array)
                foreach (var elementJSON in array.OfType<JObject>())
                    if (ChargingTariffElement.TryParse(elementJSON, out var element))
                        elements.Add(element!);

            return elements;

        }

        #endregion

        #region ParseSignatures(JSON)

        internal static List<SignatureRS> ParseSignatures(JToken? JSON)
        {

            var signatures = new List<SignatureRS>();

            if (JSON is JArray array)
                foreach (var signatureJSON in array.OfType<JObject>())
                    if (SignatureRS.TryParse(signatureJSON, out var signature))
                        signatures.Add(signature!);

            return signatures;

        }

        #endregion

    }

}
