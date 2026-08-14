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
    /// One position of a charging bill, e.g. "12.5 kWh cost 3.63 EUR".
    /// </summary>
    /// <param name="Amount">
    /// The billed amount. Note that this can differ from the measured amount:
    /// a tariff with a step size bills in increments, and an EV driver has to be
    /// able to see that this happened.
    /// </param>
    /// <param name="Unit">The unit of the amount, e.g. "kWh" or "min".</param>
    /// <param name="Cost">The resulting cost.</param>
    public class Cost(Decimal  Amount,
                      String   Unit,
                      Decimal  Cost)
    {

        #region Properties

        /// <summary>The billed amount, which may differ from the measured amount.</summary>
        public Decimal  Amount    { get; } = Amount;

        /// <summary>The unit of the amount, e.g. "kWh" or "min".</summary>
        public String   Unit      { get; } = Unit;

        /// <summary>The resulting cost.</summary>
        public Decimal  Value     { get; } = Cost;

        #endregion


        #region (static) TryParse(JSON, out Cost)

        /// <summary>
        /// Try to parse the given JSON as a cost position.
        /// </summary>
        /// <param name="JSON">A JSON representation of a cost position.</param>
        /// <param name="Cost">The parsed cost position.</param>
        public static Boolean TryParse(JObject JSON, out Cost? Cost)
        {

            Cost = null;

            var unit = JSON["unit"]?.Value<String>();

            if (unit is null)
                return false;

            Cost = new Cost(
                       JSON["amount"]?.Value<Decimal>() ?? 0,
                       unit,
                       JSON["cost"]?.  Value<Decimal>() ?? 0
                   );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this cost position.
        /// </summary>
        public JObject ToJSON()

            => new (
                   new JProperty("amount",  Amount),
                   new JProperty("unit",    Unit),
                   new JProperty("cost",    Value)
               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this cost position.
        /// </summary>
        public override String ToString()

            => $"{Amount} {Unit}: {Value}";

        #endregion


    }


    /// <summary>
    /// A cost that does not depend on any amount, e.g. a session fee.
    /// </summary>
    /// <param name="Cost">The cost.</param>
    public class FlatCost(Decimal Cost)
    {

        #region Properties

        /// <summary>The cost.</summary>
        public Decimal  Value    { get; } = Cost;

        #endregion


        #region (static) TryParse(JSON, out FlatCost)

        /// <summary>
        /// Try to parse the given JSON as a flat cost.
        /// </summary>
        /// <param name="JSON">A JSON representation of a flat cost.</param>
        /// <param name="FlatCost">The parsed flat cost.</param>
        public static Boolean TryParse(JObject JSON, out FlatCost? FlatCost)
        {

            FlatCost = null;

            if (JSON["cost"] is null)
                return false;

            FlatCost = new FlatCost(JSON["cost"]!.Value<Decimal>());

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this flat cost.
        /// </summary>
        public JObject ToJSON()

            => new (
                   new JProperty("cost", Value)
               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this flat cost.
        /// </summary>
        public override String ToString()

            => Value.ToString();

        #endregion


    }


    /// <summary>
    /// What a charging session cost, broken down into the positions an EV driver
    /// can check against the signed meter readings.
    /// </summary>
    /// <param name="Total">The total cost.</param>
    /// <param name="Currency">The currency.</param>
    /// <param name="Reservation">An optional cost of reserving the EVSE.</param>
    /// <param name="Energy">An optional cost of the charged energy.</param>
    /// <param name="Time">An optional cost of the charging time.</param>
    /// <param name="Idle">An optional cost of blocking the EVSE after charging.</param>
    /// <param name="Flat">An optional session fee.</param>
    public class ChargingCosts(Decimal    Total,
                               String     Currency,
                               Cost?      Reservation  = null,
                               Cost?      Energy       = null,
                               Cost?      Time         = null,
                               Cost?      Idle         = null,
                               FlatCost?  Flat         = null)
    {

        #region Properties

        /// <summary>The total cost.</summary>
        public Decimal    Total          { get; } = Total;

        /// <summary>The currency.</summary>
        public String     Currency       { get; } = Currency;

        /// <summary>An optional cost of reserving the EVSE.</summary>
        public Cost?      Reservation    { get; } = Reservation;

        /// <summary>An optional cost of the charged energy.</summary>
        public Cost?      Energy         { get; } = Energy;

        /// <summary>An optional cost of the charging time.</summary>
        public Cost?      Time           { get; } = Time;

        /// <summary>An optional cost of blocking the EVSE after charging.</summary>
        public Cost?      Idle           { get; } = Idle;

        /// <summary>An optional session fee.</summary>
        public FlatCost?  Flat           { get; } = Flat;

        #endregion


        #region (static) TryParse(JSON, out ChargingCosts)

        /// <summary>
        /// Try to parse the given JSON as charging costs.
        /// </summary>
        /// <param name="JSON">A JSON representation of charging costs.</param>
        /// <param name="ChargingCosts">The parsed charging costs.</param>
        public static Boolean TryParse(JObject JSON, out ChargingCosts? ChargingCosts)
        {

            ChargingCosts = null;

            var currency = JSON["currency"]?.Value<String>();

            if (currency is null)
                return false;

            static Cost? ParseCost(JToken? JSON)
                => JSON is JObject json && Cost.TryParse(json, out var cost)
                       ? cost
                       : null;

            FlatCost? flat = null;

            if (JSON["flat"] is JObject flatJSON)
                FlatCost.TryParse(flatJSON, out flat);

            ChargingCosts = new ChargingCosts(
                                JSON["total"]?.Value<Decimal>() ?? 0,
                                currency,
                                ParseCost(JSON["reservation"]),
                                ParseCost(JSON["energy"]),
                                ParseCost(JSON["time"]),
                                ParseCost(JSON["idle"]),
                                flat
                            );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of these charging costs.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("total",     Total),
                           new JProperty("currency",  Currency)
                       );

            if (Reservation is not null)
                json.Add(new JProperty("reservation",  Reservation.ToJSON()));

            if (Energy      is not null)
                json.Add(new JProperty("energy",       Energy.     ToJSON()));

            if (Time        is not null)
                json.Add(new JProperty("time",         Time.       ToJSON()));

            if (Idle        is not null)
                json.Add(new JProperty("idle",         Idle.       ToJSON()));

            if (Flat        is not null)
                json.Add(new JProperty("flat",         Flat.       ToJSON()));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of these charging costs.
        /// </summary>
        public override String ToString()

            => $"{Total} {Currency}";

        #endregion


    }


    /// <summary>
    /// One stretch of a charging session during which the same tariff element
    /// applied, e.g. the part before a time-of-day price change.
    /// </summary>
    /// <param name="StartTimestamp">The start of this period.</param>
    /// <param name="ChargingTariffId">The identification of the tariff that applied.</param>
    /// <param name="Costs">The costs of this period.</param>
    /// <param name="StopTimestamp">An optional end of this period.</param>
    /// <param name="EndTimestamp">An optional end of this period, as spelled by some backends.</param>
    /// <param name="ActiveChargingTariffElement">The tariff element that applied.</param>
    public class ChargingPeriod(String                  StartTimestamp,
                                String                  ChargingTariffId,
                                ChargingCosts           Costs,
                                String?                 StopTimestamp                = null,
                                String?                 EndTimestamp                 = null,
                                ChargingTariffElement?  ActiveChargingTariffElement  = null)
    {

        #region Properties

        /// <summary>The start of this period.</summary>
        public String                  StartTimestamp                 { get; } = StartTimestamp;

        /// <summary>The identification of the tariff that applied.</summary>
        public String                  ChargingTariffId               { get; } = ChargingTariffId;

        /// <summary>The costs of this period.</summary>
        public ChargingCosts           Costs                          { get; } = Costs;

        /// <summary>An optional end of this period.</summary>
        public String?                 StopTimestamp                  { get; } = StopTimestamp;

        /// <summary>An optional end of this period, as spelled by some backends.</summary>
        public String?                 EndTimestamp                   { get; } = EndTimestamp;

        /// <summary>The tariff element that applied.</summary>
        public ChargingTariffElement?  ActiveChargingTariffElement    { get; } = ActiveChargingTariffElement;

        #endregion


        #region (static) TryParse(JSON, out ChargingPeriod)

        /// <summary>
        /// Try to parse the given JSON as a charging period.
        /// </summary>
        /// <param name="JSON">A JSON representation of a charging period.</param>
        /// <param name="ChargingPeriod">The parsed charging period.</param>
        public static Boolean TryParse(JObject JSON, out ChargingPeriod? ChargingPeriod)
        {

            ChargingPeriod = null;

            var startTimestamp   = JSON["startTimestamp"]?.  Value<String>();
            var chargingTariffId = JSON["chargingTariffId"]?.Value<String>();

            if (startTimestamp is null || chargingTariffId is null)
                return false;

            if (JSON["costs"] is not JObject costsJSON ||
                !ChargingCosts.TryParse(costsJSON, out var costs))
            {
                return false;
            }

            ChargingTariffElement? activeElement = null;

            if (JSON["activeChargingTariffElement"] is JObject activeElementJSON)
                ChargingTariffElement.TryParse(activeElementJSON, out activeElement);

            ChargingPeriod = new ChargingPeriod(
                                 startTimestamp,
                                 chargingTariffId,
                                 costs!,
                                 JSON["stopTimestamp"]?.Value<String>(),
                                 JSON["endTimestamp"]?. Value<String>(),
                                 activeElement
                             );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this charging period.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("startTimestamp", StartTimestamp)
                       );

            if (StopTimestamp               is not null)
                json.Add(new JProperty("stopTimestamp",                StopTimestamp));

            if (EndTimestamp                is not null)
                json.Add(new JProperty("endTimestamp",                 EndTimestamp));

            json.Add(new JProperty("chargingTariffId",                 ChargingTariffId));

            if (ActiveChargingTariffElement is not null)
                json.Add(new JProperty("activeChargingTariffElement",  ActiveChargingTariffElement.ToJSON()));

            json.Add(new JProperty("costs",                            Costs.ToJSON()));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this charging period.
        /// </summary>
        public override String ToString()

            => $"{StartTimestamp} ({ChargingTariffId}): {Costs}";

        #endregion


    }

}
