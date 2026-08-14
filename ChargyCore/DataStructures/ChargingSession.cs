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
    /// A single charging process, from plugging in to unplugging, together with
    /// the signed energy meter readings that document it.
    ///
    /// The identifications are what a parser fills in first; the object references
    /// of the same name are resolved afterwards, once the whole charge transparency
    /// record is known.
    /// </summary>
    /// <param name="Id">The identification of the charging session.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Begin">An optional start of the charging session.</param>
    /// <param name="End">An optional end of the charging session.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="ChargingStationOperatorId">An optional identification of the charging station operator.</param>
    /// <param name="ChargingPoolId">An optional identification of the charging pool.</param>
    /// <param name="ChargingStationId">An optional identification of the charging station.</param>
    /// <param name="EVSEId">An optional identification of the EVSE.</param>
    /// <param name="ConnectorId">An optional identification of the connector.</param>
    /// <param name="EnergyMeterId">An optional identification of the energy meter.</param>
    /// <param name="InternalSessionId">An optional internal identification of the backend that produced this record.</param>
    /// <param name="Measurements">The measurements of this charging session.</param>
    /// <param name="PublicKey">An optional public key to verify this charging session with.</param>
    /// <param name="Original">An optional original representation of this charging session, as it was signed.</param>
    /// <param name="Signature">An optional signature over the entire charging session.</param>
    /// <param name="HashValue">An optional hash over the entire charging session.</param>
    public class ChargingSession(String                     Id,
                                 IEnumerable<String>?       Context                    = null,
                                 String?                    Begin                      = null,
                                 String?                    End                        = null,
                                 I18NString?                Description                = null,
                                 String?                    ChargingStationOperatorId  = null,
                                 String?                    ChargingPoolId             = null,
                                 String?                    ChargingStationId          = null,
                                 String?                    EVSEId                     = null,
                                 String?                    ConnectorId                = null,
                                 String?                    EnergyMeterId              = null,
                                 String?                    InternalSessionId          = null,
                                 IEnumerable<Measurement>?  Measurements               = null,
                                 PublicKey?                 PublicKey                  = null,
                                 String?                    Original                   = null,
                                 Signature?                 Signature                  = null,
                                 String?                    HashValue                  = null)
    {

        #region Data

        private readonly List<Measurement>                measurements                = [.. Measurements ?? []];
        private readonly List<ChargingTariff>             chargingTariffs             = [];
        private readonly List<ChargingPeriod>             chargingPeriods             = [];
        private readonly List<Parking>                    parking                     = [];
        private readonly List<LegallyRelevantLogMessage>  legallyRelevantLogMessages  = [];

        #endregion

        #region Properties

        /// <summary>The identification of the charging session.</summary>
        public String                      Id                            { get; }               = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>       JSONLDContext                 { get; }               = Context?.ToArray() ?? [];

        /// <summary>An optional start of the charging session.</summary>
        public String?                     Begin                         { get; }               = Begin;

        /// <summary>An optional end of the charging session.</summary>
        public String?                     End                           { get; }               = End;

        /// <summary>An optional multi-language description.</summary>
        public I18NString?                 Description                   { get; }               = Description;

        /// <summary>An optional identification of the charging station operator.</summary>
        public String?                     ChargingStationOperatorId     { get; internal set; } = ChargingStationOperatorId;

        /// <summary>An optional identification of the charging pool.</summary>
        public String?                     ChargingPoolId                { get; internal set; } = ChargingPoolId;

        /// <summary>An optional identification of the charging station.</summary>
        public String?                     ChargingStationId             { get; internal set; } = ChargingStationId;

        /// <summary>An optional identification of the EVSE.</summary>
        public String?                     EVSEId                        { get; internal set; } = EVSEId;

        /// <summary>An optional identification of the connector.</summary>
        public String?                     ConnectorId                   { get; internal set; } = ConnectorId;

        /// <summary>An optional identification of the energy meter.</summary>
        public String?                     EnergyMeterId                 { get; internal set; } = EnergyMeterId;

        /// <summary>An optional internal identification of the backend that produced this record.</summary>
        public String?                     InternalSessionId             { get; }               = InternalSessionId;

        /// <summary>An optional public key to verify this charging session with.</summary>
        public PublicKey?                  PublicKey                     { get; internal set; } = PublicKey;

        /// <summary>An optional original representation of this charging session, as it was signed.</summary>
        public String?                     Original                      { get; internal set; } = Original;

        /// <summary>An optional signature over the entire charging session.</summary>
        public Signature?                  Signature                     { get; internal set; } = Signature;

        /// <summary>An optional hash over the entire charging session.</summary>
        public String?                     HashValue                     { get; internal set; } = HashValue;

        /// <summary>The measurements of this charging session.</summary>
        public IReadOnlyList<Measurement>  Measurements
            => measurements;

        /// <summary>An optional identification of the applied tariff.</summary>
        public String?                     TariffId                      { get; set; }

        /// <summary>The tariffs that applied to this charging session.</summary>
        public IReadOnlyList<ChargingTariff>             ChargingTariffs
            => chargingTariffs;

        /// <summary>The billing periods of this charging session.</summary>
        public IReadOnlyList<ChargingPeriod>             ChargingPeriods
            => chargingPeriods;

        /// <summary>The parking periods of this charging session.</summary>
        public IReadOnlyList<Parking>                    Parking
            => parking;

        /// <summary>The legally relevant events that happened during this charging session.</summary>
        public IReadOnlyList<LegallyRelevantLogMessage>  LegallyRelevantLogMessages
            => legallyRelevantLogMessages;

        /// <summary>The total costs of this charging session.</summary>
        public ChargingCosts?              TotalCosts                    { get; set; }

        /// <summary>How the charging session was authorized.</summary>
        public Authorization?              AuthorizationStart            { get; set; }

        /// <summary>How the end of the charging session was authorized.</summary>
        public Authorization?              AuthorizationStop             { get; set; }

        /// <summary>The charging product this session was booked under.</summary>
        public ChargingProduct?            Product                       { get; set; }

        /// <summary>Which parts of this charging session influence its price.</summary>
        public ChargingProductRelevance?   ChargingProductRelevance      { get; set; }

        /// <summary>Where this charging session can be looked up and verified.</summary>
        public TransparencyInfos?          TransparencyInfos             { get; set; }

        /// <summary>The result of verifying this charging session.</summary>
        public SessionCryptoResult?        VerificationResult            { get; set; }

        #endregion

        #region Resolved references

        // These are filled in once the whole charge transparency record is known.
        // None of them is serialized — see ToJSON().

        /// <summary>The charge transparency record this charging session belongs to.</summary>
        public ChargeTransparencyRecord?   CTR                           { get; internal set; }

        /// <summary>The operator of the charging station.</summary>
        public ChargingStationOperator?    ChargingStationOperator       { get; internal set; }

        /// <summary>The charging pool.</summary>
        public ChargingPool?               ChargingPool                  { get; internal set; }

        /// <summary>The charging station.</summary>
        public ChargingStation?            ChargingStation               { get; internal set; }

        /// <summary>The EVSE.</summary>
        public EVSE?                       EVSE                          { get; internal set; }

        /// <summary>The connector.</summary>
        public Connector?                  Connector                     { get; internal set; }

        /// <summary>The energy meter.</summary>
        public EnergyMeter?                EnergyMeter                   { get; internal set; }

        #endregion


        #region MeterId

        /// <summary>
        /// The identification of the energy meter of this charging session,
        /// falling back to the meter of its first measurement.
        ///
        /// Not every charge transparency data format names the meter at session
        /// level; several only carry it per measurement.
        /// </summary>
        public String? MeterId

            => EnergyMeterId
                   ?? (measurements.Count > 0
                           ? measurements[0].EnergyMeterId
                           : null);

        #endregion

        #region AddMeasurement              (Measurement)

        /// <summary>
        /// Add a measurement to this charging session.
        /// </summary>
        /// <param name="Measurement">A measurement.</param>
        public ChargingSession AddMeasurement(Measurement Measurement)
        {

            Measurement.ChargingSession = this;

            measurements.Add(Measurement);

            return this;

        }

        #endregion

        #region AddChargingTariff           (ChargingTariff)

        /// <summary>
        /// Add a charging tariff to this charging session.
        /// </summary>
        /// <param name="ChargingTariff">A charging tariff.</param>
        public ChargingSession AddChargingTariff(ChargingTariff ChargingTariff)
        {
            chargingTariffs.Add(ChargingTariff);
            return this;
        }

        #endregion

        #region AddChargingPeriod           (ChargingPeriod)

        /// <summary>
        /// Add a billing period to this charging session.
        /// </summary>
        /// <param name="ChargingPeriod">A billing period.</param>
        public ChargingSession AddChargingPeriod(ChargingPeriod ChargingPeriod)
        {
            chargingPeriods.Add(ChargingPeriod);
            return this;
        }

        #endregion

        #region AddParking                  (Parking)

        /// <summary>
        /// Add a parking period to this charging session.
        /// </summary>
        /// <param name="Parking">A parking period.</param>
        public ChargingSession AddParking(Parking Parking)
        {
            parking.Add(Parking);
            return this;
        }

        #endregion

        #region AddLegallyRelevantLogMessage(LogMessage)

        /// <summary>
        /// Add a legally relevant log message to this charging session.
        /// </summary>
        /// <param name="LogMessage">A legally relevant log message.</param>
        public ChargingSession AddLegallyRelevantLogMessage(LegallyRelevantLogMessage LogMessage)
        {
            legallyRelevantLogMessages.Add(LogMessage);
            return this;
        }

        #endregion


        #region (static) TryParse(JSON, out ChargingSession)

        /// <summary>
        /// Try to parse the given JSON as a charging session.
        /// </summary>
        /// <param name="JSON">A JSON representation of a charging session.</param>
        /// <param name="ChargingSession">The parsed charging session.</param>
        public static Boolean TryParse(JObject JSON, out ChargingSession? ChargingSession)
        {

            ChargingSession = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            PublicKey? publicKey = null;

            if (JSON["publicKey"] is JObject publicKeyJSON)
                chargy.PublicKey.TryParse(publicKeyJSON, out publicKey);

            Signature? signature = null;

            if (JSON["signature"] is JObject signatureJSON)
                chargy.Signature.TryParse(signatureJSON, out signature);

            else if (JSON["signature"]?.Type == JTokenType.String)
                signature = new Signature(JSON["signature"]!.Value<String>());

            var chargingSession = new ChargingSession(
                                      id,
                                      chargy.PublicKey.ParseContext(JSON["@context"]),
                                      JSON["begin"]?.                    Value<String>(),
                                      JSON["end"]?.                      Value<String>(),
                                      JSON["description"] is JObject descriptionJSON
                                          ? I18NString.Parse(descriptionJSON)
                                          : null,
                                      JSON["chargingStationOperatorId"]?.Value<String>(),
                                      JSON["chargingPoolId"]?.           Value<String>(),
                                      JSON["chargingStationId"]?.        Value<String>(),
                                      JSON["EVSEId"]?.                   Value<String>(),
                                      JSON["ConnectorId"]?.              Value<String>(),
                                      JSON["meterId"]?.                  Value<String>(),
                                      JSON["internalSessionId"]?.        Value<String>(),
                                      null,
                                      publicKey,
                                      JSON["original"]?.                 Value<String>(),
                                      signature,
                                      JSON["hashValue"]?.                Value<String>()
                                  );

            chargingSession.TariffId = JSON["tariffId"]?.Value<String>();

            if (JSON["measurements"] is JArray measurementArray)
                foreach (var measurementJSON in measurementArray.OfType<JObject>())
                    if (Measurement.TryParse(measurementJSON, out var measurement))
                        chargingSession.AddMeasurement(measurement!);

            foreach (var tariff in EntityLists.ParseChargingTariffs(JSON["chargingTariffs"]))
                chargingSession.AddChargingTariff(tariff);

            if (JSON["chargingPeriods"] is JArray chargingPeriodArray)
                foreach (var chargingPeriodJSON in chargingPeriodArray.OfType<JObject>())
                    if (ChargingPeriod.TryParse(chargingPeriodJSON, out var chargingPeriod))
                        chargingSession.AddChargingPeriod(chargingPeriod!);

            if (JSON["parking"] is JArray parkingArray)
                foreach (var parkingJSON in parkingArray.OfType<JObject>())
                    if (chargy.Parking.TryParse(parkingJSON, out var parkingPeriod))
                        chargingSession.AddParking(parkingPeriod!);

            if (JSON["legallyRelevantLogMessages"] is JArray logMessageArray)
                foreach (var logMessageJSON in logMessageArray.OfType<JObject>())
                    if (LegallyRelevantLogMessage.TryParse(logMessageJSON, out var logMessage))
                        chargingSession.AddLegallyRelevantLogMessage(logMessage!);

            if (JSON["totalCosts"]               is JObject totalCostsJSON &&
                ChargingCosts.           TryParse(totalCostsJSON,        out var totalCosts))
                chargingSession.TotalCosts               = totalCosts;

            if (JSON["authorizationStart"]       is JObject authorizationStartJSON &&
                Authorization.           TryParse(authorizationStartJSON, out var authorizationStart))
                chargingSession.AuthorizationStart       = authorizationStart;

            if (JSON["authorizationStop"]        is JObject authorizationStopJSON &&
                Authorization.           TryParse(authorizationStopJSON,  out var authorizationStop))
                chargingSession.AuthorizationStop        = authorizationStop;

            if (JSON["product"]                  is JObject productJSON &&
                ChargingProduct.         TryParse(productJSON,            out var product))
                chargingSession.Product                  = product;

            if (JSON["chargingProductRelevance"] is JObject relevanceJSON &&
                ChargingProductRelevance.TryParse(relevanceJSON,          out var relevance))
                chargingSession.ChargingProductRelevance = relevance;

            if (JSON["transparencyInfos"]        is JObject transparencyInfosJSON &&
                TransparencyInfos.       TryParse(transparencyInfosJSON,  out var transparencyInfos))
                chargingSession.TransparencyInfos        = transparencyInfos;

            ChargingSession = chargingSession;

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this charging session.
        ///
        /// Note: None of the resolved references is serialized, only the
        /// identifications. <see cref="CTR"/> points back at the record that
        /// contains this session, so writing it out would not terminate; the
        /// infrastructure references would duplicate entities the record already
        /// carries under its charging station operators.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context",                    JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context",                    new JArray(JSONLDContext)));

            if (Begin                      is not null)
                json.Add(new JProperty("begin",                       Begin));

            if (End                        is not null)
                json.Add(new JProperty("end",                         End));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",                 Description.ToJSON()));

            if (ChargingStationOperatorId  is not null)
                json.Add(new JProperty("chargingStationOperatorId",   ChargingStationOperatorId));

            if (ChargingPoolId             is not null)
                json.Add(new JProperty("chargingPoolId",              ChargingPoolId));

            if (ChargingStationId          is not null)
                json.Add(new JProperty("chargingStationId",           ChargingStationId));

            if (EVSEId                     is not null)
                json.Add(new JProperty("EVSEId",                      EVSEId));

            if (ConnectorId                is not null)
                json.Add(new JProperty("ConnectorId",                 ConnectorId));

            if (EnergyMeterId              is not null)
                json.Add(new JProperty("meterId",                     EnergyMeterId));

            if (InternalSessionId          is not null)
                json.Add(new JProperty("internalSessionId",           InternalSessionId));

            if (TariffId                   is not null)
                json.Add(new JProperty("tariffId",                    TariffId));

            if (PublicKey                  is not null)
                json.Add(new JProperty("publicKey",                   PublicKey.ToJSON()));

            if (Product                    is not null)
                json.Add(new JProperty("product",                     Product.ToJSON()));

            if (ChargingProductRelevance   is not null)
                json.Add(new JProperty("chargingProductRelevance",    ChargingProductRelevance.ToJSON()));

            if (AuthorizationStart         is not null)
                json.Add(new JProperty("authorizationStart",          AuthorizationStart.ToJSON()));

            if (AuthorizationStop          is not null)
                json.Add(new JProperty("authorizationStop",           AuthorizationStop.ToJSON()));

            if (chargingTariffs.Count > 0)
                json.Add(new JProperty("chargingTariffs",             new JArray(chargingTariffs.Select(tariff => tariff.ToJSON()))));

            if (chargingPeriods.Count > 0)
                json.Add(new JProperty("chargingPeriods",             new JArray(chargingPeriods.Select(period => period.ToJSON()))));

            if (TotalCosts                 is not null)
                json.Add(new JProperty("totalCosts",                  TotalCosts.ToJSON()));

            if (parking.Count > 0)
                json.Add(new JProperty("parking",                     new JArray(parking.Select(period => period.ToJSON()))));

            if (TransparencyInfos          is not null)
                json.Add(new JProperty("transparencyInfos",           TransparencyInfos.ToJSON()));

            if (Original                   is not null)
                json.Add(new JProperty("original",                    Original));

            if (Signature                  is not null)
                json.Add(new JProperty("signature",                   Signature.ToJSON()));

            if (HashValue                  is not null)
                json.Add(new JProperty("hashValue",                   HashValue));

            if (measurements.Count > 0)
                json.Add(new JProperty("measurements",                new JArray(measurements.Select(measurement => measurement.ToJSON()))));

            if (legallyRelevantLogMessages.Count > 0)
                json.Add(new JProperty("legallyRelevantLogMessages",  new JArray(legallyRelevantLogMessages.Select(message => message.ToJSON()))));

            if (VerificationResult         is not null)
                json.Add(new JProperty("verificationResult",          VerificationResult.ToJSON()));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this charging session.
        /// </summary>
        public override String ToString()

            => $"{Id}: {measurements.Count} measurement(s)";

        #endregion


    }

}
