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

#endregion

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// One measured quantity of an energy meter over the course of a charging
    /// session, e.g. the total imported energy, together with all of its signed
    /// readings.
    /// </summary>
    /// <param name="EnergyMeterId">The identification of the energy meter.</param>
    /// <param name="Name">
    /// The name of the measured quantity, e.g. "ENERGY_TOTAL".
    ///
    /// Absent for the meters that report several quantities under one signature —
    /// a BSM snapshot signs an energy reading, a total and a power at once, and
    /// naming the group after any one of them would misdescribe the other two.
    /// Those name themselves per <see cref="Phenomenon"/> instead.
    /// </param>
    /// <param name="OBIS">The OBIS number of the measured quantity, e.g. "1-0:1.8.0*255"; absent for the same reason as the name.</param>
    /// <param name="Scale">The power of ten the measured values are scaled by.</param>
    /// <param name="Values">The signed readings.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Unit">An optional unit, e.g. "kWh".</param>
    /// <param name="UnitEncoded">An optional numeric unit code, as used by the SML based formats.</param>
    /// <param name="ValueType">An optional value type.</param>
    /// <param name="VerifyChain">Whether the readings form a hash chain that has to be verified as a whole.</param>
    /// <param name="SignatureInfos">Optional information about how the readings are signed.</param>
    /// <param name="Phenomena">The individual quantities, for the meters that sign several at once.</param>
    public class Measurement(String                          EnergyMeterId,
                             String?                         Name,
                             String?                         OBIS,
                             Int32                           Scale,
                             IEnumerable<MeasurementValue>?  Values          = null,
                             IEnumerable<String>?            Context         = null,
                             String?                         Unit            = null,
                             UInt16?                         UnitEncoded     = null,
                             String?                         ValueType       = null,
                             Boolean?                        VerifyChain     = null,
                             SignatureInfos?                 SignatureInfos  = null,
                             IEnumerable<Phenomenon>?        Phenomena       = null)
    {

        #region Data

        private readonly List<MeasurementValue> values = [.. Values ?? []];

        #endregion

        #region Properties

        /// <summary>The identification of the energy meter.</summary>
        public String                           EnergyMeterId         { get; }      = EnergyMeterId;

        /// <summary>The name of the measured quantity, e.g. "ENERGY_TOTAL".</summary>
        public String?                          Name                  { get; }      = Name;

        /// <summary>The OBIS number of the measured quantity, e.g. "1-0:1.8.0*255".</summary>
        public String?                          OBIS                  { get; }      = OBIS;

        /// <summary>The individual quantities, for the meters that sign several at once.</summary>
        public IReadOnlyList<Phenomenon>        Phenomena             { get; }      = Phenomena?.ToArray() ?? [];

        /// <summary>The power of ten the measured values are scaled by.</summary>
        public Int32                            Scale                 { get; }      = Scale;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>            Context               { get; }      = Context?.ToArray() ?? [];

        /// <summary>An optional unit, e.g. "kWh".</summary>
        public String?                          Unit                  { get; }      = Unit;

        /// <summary>An optional numeric unit code, as used by the SML based formats.</summary>
        public UInt16?                          UnitEncoded           { get; }      = UnitEncoded;

        /// <summary>An optional value type.</summary>
        public String?                          ValueType             { get; }      = ValueType;

        /// <summary>Whether the readings form a hash chain that has to be verified as a whole.</summary>
        public Boolean?                         VerifyChain           { get; }      = VerifyChain;

        /// <summary>Optional information about how the readings are signed.</summary>
        public SignatureInfos?                  SignatureInfos        { get; }      = SignatureInfos;

        /// <summary>The signed readings.</summary>
        public IReadOnlyList<MeasurementValue>  Values
            => values;

        /// <summary>
        /// The charging session this measurement belongs to. Set while a charge
        /// transparency record is being assembled.
        /// </summary>
        public ChargingSession?                 ChargingSession       { get; internal set; }

        /// <summary>The result of verifying this measurement.</summary>
        public CryptoResult?                    VerificationResult    { get; set; }

        #endregion


        #region AddValue(Value)

        /// <summary>
        /// Add a signed reading to this measurement, linking it to its predecessor
        /// so that the hash chained data formats can verify the chain.
        /// </summary>
        /// <param name="Value">A signed reading.</param>
        public Measurement AddValue(MeasurementValue Value)
        {

            Value.Measurement   = this;
            Value.PreviousValue = values.Count > 0
                                      ? values[^1]
                                      : null;

            values.Add(Value);

            return this;

        }

        #endregion


        #region (static) TryParse(JSON, out Measurement)

        /// <summary>
        /// Try to parse the given JSON as a measurement.
        /// </summary>
        /// <param name="JSON">A JSON representation of a measurement.</param>
        /// <param name="Measurement">The parsed measurement.</param>
        public static Boolean TryParse(JObject JSON, out Measurement? Measurement)
        {

            Measurement = null;

            var energyMeterId = JSON["energyMeterId"]?.Value<String>();
            var name          = JSON["name"]?.         Value<String>();
            var obis          = JSON["obis"]?.         Value<String>();

            if (energyMeterId is null)
                return false;

            SignatureInfos? signatureInfos = null;

            if (JSON["signatureInfos"] is JObject signatureInfosJSON)
                chargy.SignatureInfos.TryParse(signatureInfosJSON, out signatureInfos);

            var measurement = new Measurement(
                                  energyMeterId,
                                  name,
                                  obis,
                                  JSON["scale"]?.Value<Int32>() ?? 0,
                                  null,
                                  PublicKey.ParseContext(JSON["@context"]),
                                  JSON["unit"]?.        Value<String>(),
                                  JSON["unitEncoded"]?. Value<UInt16>(),
                                  JSON["valueType"]?.   Value<String>(),
                                  JSON["verifyChain"]?. Value<Boolean>(),
                                  signatureInfos,
                                  JSON["phenomena"] is JArray phenomenaArray
                                      ? phenomenaArray.OfType<JObject>().
                                                       Select(Phenomenon.Parse)
                                      : null
                              );

            if (JSON["values"] is JArray valueArray)
                foreach (var valueJSON in valueArray.OfType<JObject>())
                    if (MeasurementValue.TryParse(valueJSON, out var value))
                        measurement.AddValue(value!);

            Measurement = measurement;

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this measurement.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Context.Count == 1)
                json.Add(new JProperty("@context",        Context[0]));

            else if (Context.Count > 1)
                json.Add(new JProperty("@context",        new JArray(Context)));

            json.Add(new JProperty("energyMeterId",       EnergyMeterId));

            if (Name           is not null)
                json.Add(new JProperty("name",            Name));

            if (OBIS           is not null)
                json.Add(new JProperty("obis",            OBIS));

            if (Phenomena.Count > 0)
                json.Add(new JProperty("phenomena",       new JArray(Phenomena.Select(phenomenon => phenomenon.ToJSON()))));

            if (Unit           is not null)
                json.Add(new JProperty("unit",            Unit));

            if (UnitEncoded.HasValue)
                json.Add(new JProperty("unitEncoded",     UnitEncoded.Value));

            if (ValueType      is not null)
                json.Add(new JProperty("valueType",       ValueType));

            json.Add(new JProperty("scale",               Scale));

            if (VerifyChain.HasValue)
                json.Add(new JProperty("verifyChain",     VerifyChain.Value));

            if (SignatureInfos is not null)
                json.Add(new JProperty("signatureInfos",  SignatureInfos.ToJSON()));

            json.Add(new JProperty("values",              new JArray(values.Select(value => value.ToJSON()))));

            if (VerificationResult is not null)
                json.Add(new JProperty("verificationResult", VerificationResult.ToJSON()));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this measurement.
        /// </summary>
        public override String ToString()

            => $"{Name} ({OBIS}): {values.Count} value(s)";

        #endregion


    }

}
