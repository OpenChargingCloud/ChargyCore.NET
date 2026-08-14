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

using System.Globalization;

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// How a measured value is compared against the threshold of a validation rule.
    /// </summary>
    public enum ValidationRuleOperator
    {

        /// <summary>Greater than the threshold.</summary>
        GreaterThan,

        /// <summary>Greater than or equal to the threshold.</summary>
        GreaterThanOrEqual,

        /// <summary>Less than the threshold.</summary>
        LessThan,

        /// <summary>Less than or equal to the threshold.</summary>
        LessThanOrEqual,

        /// <summary>Equal to the threshold.</summary>
        Equal

    }


    /// <summary>
    /// Extension methods for validation rule operators.
    /// </summary>
    public static class ValidationRuleOperatorExtensions
    {

        #region TryParse(Text, out ValidationRuleOperator)

        /// <summary>
        /// Try to parse the given text as a validation rule operator.
        /// </summary>
        /// <param name="Text">A text representation of a validation rule operator.</param>
        /// <param name="ValidationRuleOperator">The parsed validation rule operator.</param>
        public static Boolean TryParse(String Text, out ValidationRuleOperator ValidationRuleOperator)
        {

            switch (Text.Trim())
            {

                case ">":
                    ValidationRuleOperator = ValidationRuleOperator.GreaterThan;
                    return true;

                case ">=":
                    ValidationRuleOperator = ValidationRuleOperator.GreaterThanOrEqual;
                    return true;

                case "<":
                    ValidationRuleOperator = ValidationRuleOperator.LessThan;
                    return true;

                case "<=":
                    ValidationRuleOperator = ValidationRuleOperator.LessThanOrEqual;
                    return true;

                // ChargyCore.TS accepts both spellings.
                case "=":
                case "==":
                    ValidationRuleOperator = ValidationRuleOperator.Equal;
                    return true;

                default:
                    ValidationRuleOperator = ValidationRuleOperator.GreaterThan;
                    return false;

            }

        }

        #endregion

        #region AsText  (this ValidationRuleOperator)

        /// <summary>
        /// The wire representation of the given validation rule operator.
        /// </summary>
        /// <param name="ValidationRuleOperator">A validation rule operator.</param>
        public static String AsText(this ValidationRuleOperator ValidationRuleOperator)

            => ValidationRuleOperator switch {
                   ValidationRuleOperator.GreaterThanOrEqual  => ">=",
                   ValidationRuleOperator.LessThan            => "<",
                   ValidationRuleOperator.LessThanOrEqual     => "<=",
                   ValidationRuleOperator.Equal               => "=",
                   _                                          => ">"
               };

        #endregion

        #region Matches (this ValidationRuleOperator, Value, Threshold)

        /// <summary>
        /// Whether the given value satisfies this operator against the threshold.
        /// </summary>
        /// <param name="ValidationRuleOperator">A validation rule operator.</param>
        /// <param name="Value">The measured value.</param>
        /// <param name="Threshold">The threshold of the validation rule.</param>
        public static Boolean Matches(this ValidationRuleOperator  ValidationRuleOperator,
                                      Decimal                      Value,
                                      Decimal                      Threshold)

            => ValidationRuleOperator switch {
                   ValidationRuleOperator.GreaterThan         => Value >  Threshold,
                   ValidationRuleOperator.GreaterThanOrEqual  => Value >= Threshold,
                   ValidationRuleOperator.LessThan            => Value <  Threshold,
                   ValidationRuleOperator.LessThanOrEqual     => Value <= Threshold,
                   _                                          => Value == Threshold
               };

        #endregion

    }


    /// <summary>
    /// A plausibility rule for the total energy of a charging session, e.g.
    /// "warn when more than 500 kWh were charged in a single session".
    /// </summary>
    /// <param name="Operator">How the measured energy is compared against the threshold.</param>
    /// <param name="Threshold">The threshold.</param>
    /// <param name="Unit">The unit of the threshold, e.g. "kWh".</param>
    /// <param name="Level">How severe a violation of this rule is.</param>
    public class EnergyValidationRule(ValidationRuleOperator  Operator,
                                      Decimal                 Threshold,
                                      String                  Unit,
                                      SeverityLevel           Level)
    {

        #region Properties

        /// <summary>How the measured energy is compared against the threshold.</summary>
        public ValidationRuleOperator  Operator    { get; } = Operator;

        /// <summary>The threshold.</summary>
        public Decimal                 Threshold   { get; } = Threshold;

        /// <summary>The unit of the threshold, e.g. "kWh".</summary>
        public String                  Unit        { get; } = Unit;

        /// <summary>How severe a violation of this rule is.</summary>
        public SeverityLevel           Level       { get; } = Level;

        /// <summary>
        /// The threshold converted to kWh, or null when <see cref="Unit"/> is not
        /// an energy unit this rule engine understands.
        /// </summary>
        public Decimal?                ThresholdInKWh    { get; } = EnergyInKWh(Threshold, Unit);

        #endregion


        #region IsViolatedBy(TotalEnergyKWh)

        /// <summary>
        /// Whether the given amount of energy violates this rule.
        ///
        /// A rule with an unrecognised unit never fires, mirroring ChargyCore.TS:
        /// a plausibility rule nobody can evaluate must not turn into a warning
        /// on every single charging session.
        /// </summary>
        /// <param name="TotalEnergyKWh">The total energy of a charging session, in kWh.</param>
        public Boolean IsViolatedBy(Decimal TotalEnergyKWh)

            => ThresholdInKWh.HasValue &&
               Operator.Matches(TotalEnergyKWh, ThresholdInKWh.Value);

        #endregion

        #region (static) EnergyInKWh(Value, Unit)

        /// <summary>
        /// The given amount of energy in kWh, or null when the unit is unknown.
        /// </summary>
        /// <param name="Value">An amount of energy.</param>
        /// <param name="Unit">Its unit: "Wh", "kWh" or "MWh".</param>
        public static Decimal? EnergyInKWh(Decimal Value, String Unit)

            => Unit.Trim().ToLowerInvariant() switch {
                   "wh"   => Value / 1000,
                   "kwh"  => Value,
                   "mwh"  => Value * 1000,
                   _      => null
               };

        #endregion

        #region TryParse    (JSON, out EnergyValidationRule)

        /// <summary>
        /// Try to parse the given JSON as an energy validation rule, e.g.
        /// { "rule": [ "&gt;", "500", "kWh" ], "level": "low" }.
        /// </summary>
        /// <param name="JSON">A JSON representation of an energy validation rule.</param>
        /// <param name="EnergyValidationRule">The parsed energy validation rule.</param>
        public static Boolean TryParse(JObject JSON, out EnergyValidationRule? EnergyValidationRule)
        {

            EnergyValidationRule = null;

            if (JSON["rule"] is not JArray rule || rule.Count < 3)
                return false;

            if (!ValidationRuleOperatorExtensions.TryParse(rule[0].Value<String>() ?? "", out var ruleOperator))
                return false;

            if (!Decimal.TryParse(rule[1].Value<String>(),
                                  NumberStyles.Number,
                                  CultureInfo.InvariantCulture,
                                  out var threshold))
                return false;

            var unit = rule[2].Value<String>();

            if (String.IsNullOrWhiteSpace(unit))
                return false;

            EnergyValidationRule = new EnergyValidationRule(
                                       ruleOperator,
                                       threshold,
                                       unit,
                                       SeverityLevelExtensions.Parse(JSON["level"]?.Value<String>() ?? "low")
                                   );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this energy validation rule.
        /// </summary>
        public JObject ToJSON()

            => new (
                   new JProperty("rule",   new JArray(
                                               Operator.AsText(),
                                               Threshold.ToString(CultureInfo.InvariantCulture),
                                               Unit
                                           )),
                   new JProperty("level",  Level.AsText())
               );

        #endregion

    }


    /// <summary>
    /// The plausibility rules Chargy applies to a charge transparency record on
    /// top of verifying its cryptographic signatures.
    ///
    /// A violation never invalidates a record — the signatures are what decide
    /// that. It only raises a warning, because "technically valid but implausible"
    /// is exactly the case an EV driver needs to be told about.
    /// </summary>
    /// <param name="TotalEnergy">An optional plausibility rule for the total energy of a charging session.</param>
    public class ValidationRules(EnergyValidationRule? TotalEnergy = null)
    {

        #region Properties

        /// <summary>
        /// An optional plausibility rule for the total energy of a charging session.
        /// </summary>
        public EnergyValidationRule?  TotalEnergy    { get; } = TotalEnergy;

        #endregion


        #region (static) Parse    (JSON)

        /// <summary>
        /// Parse the given JSON as validation rules, e.g. the contents of
        /// "validationRules.json". Unparsable rules are ignored rather than
        /// rejected, so that an unknown future rule does not stop a verification.
        /// </summary>
        /// <param name="JSON">A JSON representation of validation rules.</param>
        public static ValidationRules Parse(JObject JSON)
        {

            EnergyValidationRule? totalEnergy = null;

            if (JSON["chargingSession"]?["totalEnergy"] is JObject totalEnergyJSON)
                EnergyValidationRule.TryParse(totalEnergyJSON, out totalEnergy);

            return new ValidationRules(totalEnergy);

        }

        #endregion

        #region (static) Default

        /// <summary>
        /// The validation rules embedded into this assembly.
        /// </summary>
        public static ValidationRules Default

            => Parse(ChargyResources.GetDefaultValidationRulesJSON());

        #endregion

        #region (static) None

        /// <summary>
        /// No validation rules at all.
        /// </summary>
        public static ValidationRules None

            => new ();

        #endregion


        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of these validation rules.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (TotalEnergy is not null)
                json.Add(new JProperty("chargingSession",
                             new JObject(
                                 new JProperty("totalEnergy", TotalEnergy.ToJSON())
                             )));

            return json;

        }

        #endregion


    }

}
