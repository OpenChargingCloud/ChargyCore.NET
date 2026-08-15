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
    /// How relevant a piece of information is for understanding a charging bill.
    /// </summary>
    public enum InformationRelevance
    {

        /// <summary>Not known.</summary>
        Unknown,

        /// <summary>Not relevant and not shown.</summary>
        Ignored,

        /// <summary>Shown, but not part of the billing.</summary>
        Informative,

        /// <summary>Part of the billing.</summary>
        Important

    }


    /// <summary>
    /// Extension methods for information relevances.
    /// </summary>
    public static class InformationRelevanceExtensions
    {

        #region TryParse(Text, out InformationRelevance)

        /// <summary>
        /// Try to parse the given text as an information relevance.
        /// </summary>
        /// <param name="Text">A text representation of an information relevance.</param>
        /// <param name="InformationRelevance">The parsed information relevance.</param>
        public static Boolean TryParse(String Text, out InformationRelevance InformationRelevance)

            => Enum.TryParse(Text.Trim(), out InformationRelevance);

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text as an information relevance.
        /// </summary>
        /// <param name="Text">A text representation of an information relevance.</param>
        public static InformationRelevance? TryParse(String Text)

            => TryParse(Text, out var relevance)
                   ? relevance
                   : null;

        #endregion

        #region AsText  (this InformationRelevance)

        /// <summary>
        /// The wire representation of the given information relevance.
        /// </summary>
        /// <param name="InformationRelevance">An information relevance.</param>
        public static String AsText(this InformationRelevance InformationRelevance)

            => InformationRelevance.ToString();

        #endregion

    }


    /// <summary>
    /// Which parts of a charging session actually influence its price.
    ///
    /// This is what lets a GUI tell an EV driver "the parking time was recorded
    /// but not billed" instead of leaving them to guess.
    /// </summary>
    /// <param name="Time">How relevant the charging time is.</param>
    /// <param name="Energy">How relevant the charged energy is.</param>
    /// <param name="Parking">How relevant the parking time is.</param>
    /// <param name="SessionFee">How relevant a session fee is.</param>
    public class ChargingProductRelevance(InformationRelevance?  Time        = null,
                                          InformationRelevance?  Energy      = null,
                                          InformationRelevance?  Parking     = null,
                                          InformationRelevance?  SessionFee  = null)
    {

        #region Properties

        /// <summary>How relevant the charging time is.</summary>
        public InformationRelevance?  Time          { get; } = Time;

        /// <summary>How relevant the charged energy is.</summary>
        public InformationRelevance?  Energy        { get; } = Energy;

        /// <summary>How relevant the parking time is.</summary>
        public InformationRelevance?  Parking       { get; } = Parking;

        /// <summary>How relevant a session fee is.</summary>
        public InformationRelevance?  SessionFee    { get; } = SessionFee;

        #endregion


        #region (static) TryParse(JSON, out ChargingProductRelevance)

        /// <summary>
        /// Try to parse the given JSON as a charging product relevance.
        /// </summary>
        /// <param name="JSON">A JSON representation of a charging product relevance.</param>
        /// <param name="ChargingProductRelevance">The parsed charging product relevance.</param>
        public static Boolean TryParse(JObject JSON, out ChargingProductRelevance? ChargingProductRelevance)
        {

            static InformationRelevance? Parse(JToken? JSON)
                => JSON?.Value<String>() is String text
                       ? InformationRelevanceExtensions.TryParse(text)
                       : null;

            ChargingProductRelevance = new ChargingProductRelevance(
                                           Parse(JSON["time"]),
                                           Parse(JSON["energy"]),
                                           Parse(JSON["parking"]),
                                           Parse(JSON["sessionFee"])
                                       );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this charging product relevance.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Time.      HasValue)
                json.Add(new JProperty("time",        Time.      Value.AsText()));

            if (Energy.    HasValue)
                json.Add(new JProperty("energy",      Energy.    Value.AsText()));

            if (Parking.   HasValue)
                json.Add(new JProperty("parking",     Parking.   Value.AsText()));

            if (SessionFee.HasValue)
                json.Add(new JProperty("sessionFee",  SessionFee.Value.AsText()));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this charging product relevance.
        /// </summary>
        public override String ToString()

            => String.Join(
                   ", ",
                   new[] {
                       Time.      HasValue ? $"time: {Time.            Value.AsText()}"       : null,
                       Energy.    HasValue ? $"energy: {Energy.        Value.AsText()}"       : null,
                       Parking.   HasValue ? $"parking: {Parking.      Value.AsText()}"       : null,
                       SessionFee.HasValue ? $"sessionFee: {SessionFee.Value.AsText()}"       : null
                   }.
                   Where(part => part is not null)
               );

        #endregion


    }


    /// <summary>
    /// The charging product a charging session was booked under.
    /// </summary>
    /// <param name="Id">The identification of the charging product.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    public class ChargingProduct(String                Id,
                                 IEnumerable<String>?  Context = null)
    {

        #region Properties

        /// <summary>The identification of the charging product.</summary>
        public String                 Id               { get; } = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>  JSONLDContext    { get; } = Context?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out ChargingProduct)

        /// <summary>
        /// Try to parse the given JSON as a charging product.
        /// </summary>
        /// <param name="JSON">A JSON representation of a charging product.</param>
        /// <param name="ChargingProduct">The parsed charging product.</param>
        public static Boolean TryParse(JObject JSON, out ChargingProduct? ChargingProduct)
        {

            ChargingProduct = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            ChargingProduct = new ChargingProduct(
                                  id,
                                  PublicKey.ParseContext(JSON["@context"])
                              );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this charging product.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",  JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",  new JArray(JSONLDContext)));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this charging product.
        /// </summary>
        public override String ToString()

            => Id;

        #endregion


    }


    /// <summary>
    /// How a charging session was authorized: who started it, and through which
    /// e-mobility provider.
    /// </summary>
    /// <param name="Id">The identification of the authorization, e.g. an RFID token.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Type">An optional authorization type, e.g. "RFID".</param>
    /// <param name="Timestamp">An optional timestamp of the authorization.</param>
    /// <param name="ChargingStationOperator">An optional charging station operator.</param>
    /// <param name="RoamingNetwork">An optional roaming network.</param>
    /// <param name="EMobilityProvider">An optional e-mobility provider.</param>
    /// <param name="IdentificationStatus">Whether the meter considered the driver's identification to be present and complete.</param>
    /// <param name="IdentificationLevel">How the identification was assured, e.g. verified by a trusted party.</param>
    /// <param name="IdentificationFlags">
    /// How the driver identified themselves, e.g. "RFID_PLAIN" or "OCPP_AUTH".
    ///
    /// Empty rather than absent when the meter reported none, because that is what
    /// the OCMF specification asks for — "no flags" is a statement, "no field" is
    /// a meter that did not answer, and both arrive here as an empty list only
    /// because the specification says so.
    /// </param>
    public class Authorization(String                Id,
                               IEnumerable<String>?  Context                  = null,
                               String?               Type                     = null,
                               String?               Timestamp                = null,
                               String?               ChargingStationOperator  = null,
                               String?               RoamingNetwork           = null,
                               String?               EMobilityProvider        = null,
                               Boolean?              IdentificationStatus     = null,
                               String?               IdentificationLevel      = null,
                               IEnumerable<String>?  IdentificationFlags      = null)
    {

        #region Properties

        /// <summary>The identification of the authorization, e.g. an RFID token.</summary>
        public String                 Id                         { get; } = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>  JSONLDContext              { get; } = Context?.ToArray() ?? [];

        /// <summary>An optional authorization type, e.g. "RFID".</summary>
        public String?                Type                       { get; } = Type;

        /// <summary>An optional timestamp of the authorization.</summary>
        public String?                Timestamp                  { get; } = Timestamp;

        /// <summary>An optional charging station operator.</summary>
        public String?                ChargingStationOperator    { get; } = ChargingStationOperator;

        /// <summary>An optional roaming network.</summary>
        public String?                RoamingNetwork             { get; } = RoamingNetwork;

        /// <summary>An optional e-mobility provider.</summary>
        public String?                EMobilityProvider          { get; } = EMobilityProvider;

        /// <summary>Whether the meter considered the driver's identification to be present and complete.</summary>
        public Boolean?               IdentificationStatus       { get; } = IdentificationStatus;

        /// <summary>How the identification was assured.</summary>
        public String?                IdentificationLevel        { get; } = IdentificationLevel;

        /// <summary>How the driver identified themselves.</summary>
        public IReadOnlyList<String>  IdentificationFlags        { get; } = IdentificationFlags?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out Authorization)

        /// <summary>
        /// Try to parse the given JSON as an authorization.
        /// </summary>
        /// <param name="JSON">A JSON representation of an authorization.</param>
        /// <param name="Authorization">The parsed authorization.</param>
        public static Boolean TryParse(JObject JSON, out Authorization? Authorization)
        {

            Authorization = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            Authorization = new Authorization(
                                id,
                                PublicKey.ParseContext(JSON["@context"]),
                                JSON["type"]?.                   Value<String>(),
                                JSON["timestamp"]?.              Value<String>(),
                                JSON["chargingStationOperator"]?.Value<String>(),
                                JSON["roamingNetwork"]?.         Value<String>(),
                                JSON["eMobilityProvider"]?.      Value<String>(),
                                JSON["identificationStatus"]?.   Value<Boolean>(),
                                JSON["identificationLevel"]?.    Value<String>(),
                                JSON["identificationFlags"] is JArray flags
                                    ? flags.Select(flag => flag.Value<String>()).
                                            Where (flag => flag is not null).
                                            Cast<String>()
                                    : null
                            );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this authorization.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",                JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",                new JArray(JSONLDContext)));

            if (Type                    is not null)
                json.Add(new JProperty("type",                    Type));

            if (Timestamp               is not null)
                json.Add(new JProperty("timestamp",               Timestamp));

            if (ChargingStationOperator is not null)
                json.Add(new JProperty("chargingStationOperator", ChargingStationOperator));

            if (RoamingNetwork          is not null)
                json.Add(new JProperty("roamingNetwork",          RoamingNetwork));

            if (EMobilityProvider       is not null)
                json.Add(new JProperty("eMobilityProvider",       EMobilityProvider));

            if (IdentificationStatus.HasValue)
                json.Add(new JProperty("identificationStatus",    IdentificationStatus.Value));

            if (IdentificationLevel     is not null)
                json.Add(new JProperty("identificationLevel",     IdentificationLevel));

            if (IdentificationFlags.Count > 0)
                json.Add(new JProperty("identificationFlags",     new JArray(IdentificationFlags)));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this authorization.
        /// </summary>
        public override String ToString()

            => Id;

        #endregion


    }


    /// <summary>
    /// A stretch of time during which the vehicle occupied the parking space,
    /// including whether it stayed beyond the end of charging.
    /// </summary>
    /// <param name="Id">The identification of the parking period.</param>
    /// <param name="Begin">The start of the parking period.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="End">An optional end of the parking period.</param>
    /// <param name="Overstay">Whether the vehicle stayed beyond the end of charging.</param>
    public class Parking(String                Id,
                         String                Begin,
                         IEnumerable<String>?  Context   = null,
                         String?               End       = null,
                         Boolean?              Overstay  = null)
    {

        #region Properties

        /// <summary>The identification of the parking period.</summary>
        public String                 Id               { get; } = Id;

        /// <summary>The start of the parking period.</summary>
        public String                 Begin            { get; } = Begin;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>  JSONLDContext    { get; } = Context?.ToArray() ?? [];

        /// <summary>An optional end of the parking period.</summary>
        public String?                End              { get; } = End;

        /// <summary>Whether the vehicle stayed beyond the end of charging.</summary>
        public Boolean?               Overstay         { get; } = Overstay;

        #endregion


        #region (static) TryParse(JSON, out Parking)

        /// <summary>
        /// Try to parse the given JSON as a parking period.
        /// </summary>
        /// <param name="JSON">A JSON representation of a parking period.</param>
        /// <param name="Parking">The parsed parking period.</param>
        public static Boolean TryParse(JObject JSON, out Parking? Parking)
        {

            Parking = null;

            var id    = JSON["@id"]?.  Value<String>();
            var begin = JSON["begin"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id) || begin is null)
                return false;

            Parking = new Parking(
                          id,
                          begin,
                          PublicKey.ParseContext(JSON["@context"]),
                          JSON["end"]?.     Value<String>(),
                          JSON["overstay"]?.Value<Boolean>()
                      );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this parking period.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",  JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",  new JArray(JSONLDContext)));

            json.Add(new JProperty("begin",         Begin));

            if (End is not null)
                json.Add(new JProperty("end",       End));

            if (Overstay.HasValue)
                json.Add(new JProperty("overstay",  Overstay.Value));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this parking period.
        /// </summary>
        public override String ToString()

            => $"{Begin}{(End is not null ? $" - {End}" : "")}{(Overstay == true ? " (overstay)" : "")}";

        #endregion


    }


    /// <summary>
    /// A legally relevant event that happened during a charging session, e.g. a
    /// clock adjustment or a firmware update.
    ///
    /// Under the German Calibration Law these events are part of the evidence:
    /// an EV driver has to be able to see that the meter's clock was corrected
    /// mid-session, because it changes how the timestamps are to be read.
    /// </summary>
    /// <param name="Timestamp">When the event happened.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Code">An optional machine-readable event code.</param>
    /// <param name="Data">Optional structured event data.</param>
    /// <param name="Text">An optional multi-language description.</param>
    /// <param name="Signatures">Optional signatures over this log message.</param>
    public class LegallyRelevantLogMessage(String                   Timestamp,
                                           IEnumerable<String>?     Context     = null,
                                           String?                  Code        = null,
                                           JObject?                 Data        = null,
                                           I18NString?              Text        = null,
                                           IEnumerable<Signature>?  Signatures  = null)
    {

        #region Properties

        /// <summary>When the event happened.</summary>
        public String                    Timestamp        { get; } = Timestamp;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>     JSONLDContext    { get; } = Context?.   ToArray() ?? [];

        /// <summary>An optional machine-readable event code.</summary>
        public String?                   Code             { get; } = Code;

        /// <summary>Optional structured event data.</summary>
        public JObject?                  Data             { get; } = Data;

        /// <summary>An optional multi-language description.</summary>
        public I18NString?               Text             { get; } = Text;

        /// <summary>Optional signatures over this log message.</summary>
        public IReadOnlyList<Signature>  Signatures       { get; } = Signatures?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out LegallyRelevantLogMessage)

        /// <summary>
        /// Try to parse the given JSON as a legally relevant log message.
        /// </summary>
        /// <param name="JSON">A JSON representation of a legally relevant log message.</param>
        /// <param name="LegallyRelevantLogMessage">The parsed legally relevant log message.</param>
        public static Boolean TryParse(JObject JSON, out LegallyRelevantLogMessage? LegallyRelevantLogMessage)
        {

            LegallyRelevantLogMessage = null;

            var timestamp = JSON["timestamp"]?.Value<String>();

            if (timestamp is null)
                return false;

            var signatures = new List<Signature>();

            if (JSON["signatures"] is JArray signatureArray)
                foreach (var signatureJSON in signatureArray.OfType<JObject>())
                    if (Signature.TryParse(signatureJSON, out var signature))
                        signatures.Add(signature!);

            LegallyRelevantLogMessage = new LegallyRelevantLogMessage(
                                            timestamp,
                                            PublicKey.ParseContext(JSON["@context"]),
                                            JSON["code"]?.Value<String>(),
                                            JSON["data"] as JObject,
                                            JSON["text"] is JObject textJSON
                                                ? I18NString.Parse(textJSON)
                                                : null,
                                            signatures
                                        );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this legally relevant log message.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",    JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",    new JArray(JSONLDContext)));

            json.Add(new JProperty("timestamp",       Timestamp));

            if (Code is not null)
                json.Add(new JProperty("code",        Code));

            if (Data is not null)
                json.Add(new JProperty("data",        Data));

            if (Text.IsNotNullOrEmpty())
                json.Add(new JProperty("text",        Text.ToJSON()));

            if (Signatures.Count > 0)
                json.Add(new JProperty("signatures",  new JArray(Signatures.Select(signature => signature.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this legally relevant log message.
        /// </summary>
        public override String ToString()

            => $"{Timestamp}: {Code ?? Text?.FirstText() ?? "<no code>"}";

        #endregion


    }

}
