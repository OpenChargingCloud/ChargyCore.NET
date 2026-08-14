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

        private readonly List<Measurement> measurements = [.. Measurements ?? []];

        #endregion

        #region Properties

        /// <summary>The identification of the charging session.</summary>
        public String                      Id                           { get; } = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>       Context                      { get; } = Context?.ToArray() ?? [];

        /// <summary>An optional start of the charging session.</summary>
        public String?                     Begin                        { get; } = Begin;

        /// <summary>An optional end of the charging session.</summary>
        public String?                     End                          { get; } = End;

        /// <summary>An optional multi-language description.</summary>
        public I18NString?                 Description                  { get; } = Description;

        /// <summary>An optional identification of the charging station operator.</summary>
        public String?                     ChargingStationOperatorId    { get; } = ChargingStationOperatorId;

        /// <summary>An optional identification of the charging pool.</summary>
        public String?                     ChargingPoolId               { get; } = ChargingPoolId;

        /// <summary>An optional identification of the charging station.</summary>
        public String?                     ChargingStationId            { get; } = ChargingStationId;

        /// <summary>An optional identification of the EVSE.</summary>
        public String?                     EVSEId                       { get; } = EVSEId;

        /// <summary>An optional identification of the connector.</summary>
        public String?                     ConnectorId                  { get; } = ConnectorId;

        /// <summary>An optional identification of the energy meter.</summary>
        public String?                     EnergyMeterId                { get; } = EnergyMeterId;

        /// <summary>An optional internal identification of the backend that produced this record.</summary>
        public String?                     InternalSessionId            { get; } = InternalSessionId;

        /// <summary>An optional public key to verify this charging session with.</summary>
        public PublicKey?                  PublicKey                    { get; } = PublicKey;

        /// <summary>An optional original representation of this charging session, as it was signed.</summary>
        public String?                     Original                     { get; } = Original;

        /// <summary>An optional signature over the entire charging session.</summary>
        public Signature?                  Signature                    { get; } = Signature;

        /// <summary>An optional hash over the entire charging session.</summary>
        public String?                     HashValue                    { get; } = HashValue;

        /// <summary>The measurements of this charging session.</summary>
        public IReadOnlyList<Measurement>  Measurements
            => measurements;

        /// <summary>The result of verifying this charging session.</summary>
        public SessionCryptoResult?        VerificationResult           { get; set; }

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

        #region AddMeasurement(Measurement)

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

            if (JSON["measurements"] is JArray measurementArray)
                foreach (var measurementJSON in measurementArray.OfType<JObject>())
                    if (Measurement.TryParse(measurementJSON, out var measurement))
                        chargingSession.AddMeasurement(measurement!);

            ChargingSession = chargingSession;

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this charging session.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (Context.Count == 1)
                json.Add(new JProperty("@context",                   Context[0]));

            else if (Context.Count > 1)
                json.Add(new JProperty("@context",                   new JArray(Context)));

            if (Begin                      is not null)
                json.Add(new JProperty("begin",                      Begin));

            if (End                        is not null)
                json.Add(new JProperty("end",                        End));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",                Description.ToJSON()));

            if (ChargingStationOperatorId  is not null)
                json.Add(new JProperty("chargingStationOperatorId",  ChargingStationOperatorId));

            if (ChargingPoolId             is not null)
                json.Add(new JProperty("chargingPoolId",             ChargingPoolId));

            if (ChargingStationId          is not null)
                json.Add(new JProperty("chargingStationId",          ChargingStationId));

            if (EVSEId                     is not null)
                json.Add(new JProperty("EVSEId",                     EVSEId));

            if (ConnectorId                is not null)
                json.Add(new JProperty("ConnectorId",                ConnectorId));

            if (EnergyMeterId              is not null)
                json.Add(new JProperty("meterId",                    EnergyMeterId));

            if (InternalSessionId          is not null)
                json.Add(new JProperty("internalSessionId",          InternalSessionId));

            if (PublicKey                  is not null)
                json.Add(new JProperty("publicKey",                  PublicKey.ToJSON()));

            if (Original                   is not null)
                json.Add(new JProperty("original",                   Original));

            if (Signature                  is not null)
                json.Add(new JProperty("signature",                  Signature.ToJSON()));

            if (HashValue                  is not null)
                json.Add(new JProperty("hashValue",                  HashValue));

            if (measurements.Count > 0)
                json.Add(new JProperty("measurements",               new JArray(measurements.Select(measurement => measurement.ToJSON()))));

            if (VerificationResult         is not null)
                json.Add(new JProperty("verificationResult",         VerificationResult.ToJSON()));

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
