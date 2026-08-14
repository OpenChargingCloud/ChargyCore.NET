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

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Tests for the charge transparency record plausibility rules.
    /// </summary>
    [TestFixture]
    public class ValidationRulesTests : AChargyTests
    {

        #region The_embedded_default_rule_warns_above_500_kWh()

        [Test]
        public void The_embedded_default_rule_warns_above_500_kWh()
        {

            var rule = ValidationRules.Default.TotalEnergy;

            Assert.That(rule,  Is.Not.Null);

            Assert.Multiple(() => {

                Assert.That(rule!.Operator,   Is.EqualTo(ValidationRuleOperator.GreaterThan));
                Assert.That(rule.Threshold,  Is.EqualTo(500m));
                Assert.That(rule.Unit,       Is.EqualTo("kWh"));
                Assert.That(rule.Level,      Is.EqualTo(SeverityLevel.Low));

                Assert.That(rule.IsViolatedBy(500.0m),  Is.False);
                Assert.That(rule.IsViolatedBy(500.1m),  Is.True);

            });

        }

        #endregion

        #region The_5MWh_test_fixture_parses()

        [Test]
        public void The_5MWh_test_fixture_parses()
        {

            // The fixture ChargyCore.TS uses to check that a caller can relax the
            // default plausibility threshold.
            var rules = ValidationRules.Parse(
                            JObject.Parse(ReadTextFixture("validationRules/validationRules_5MWh.json"))
                        );

            Assert.That(rules.TotalEnergy,  Is.Not.Null);

            Assert.Multiple(() => {

                Assert.That(rules.TotalEnergy!.Threshold,           Is.EqualTo(5m));
                Assert.That(rules.TotalEnergy.Unit,                Is.EqualTo("MWh"));

                // Thresholds are normalized to kWh before being compared.
                Assert.That(rules.TotalEnergy.ThresholdInKWh,      Is.EqualTo(5000m));
                Assert.That(rules.TotalEnergy.IsViolatedBy(4999m),  Is.False);
                Assert.That(rules.TotalEnergy.IsViolatedBy(5001m),  Is.True);

            });

        }

        #endregion

        #region All_operators_are_understood(...)

        [TestCase(">",  400, false)]
        [TestCase(">",  600, true)]
        [TestCase(">=", 500, true)]
        [TestCase("<",  400, true)]
        [TestCase("<",  600, false)]
        [TestCase("<=", 500, true)]
        [TestCase("=",  500, true)]
        [TestCase("==", 500, true)]   // ChargyCore.TS accepts both spellings
        [TestCase("=",  501, false)]
        public void All_operators_are_understood(String Operator, Int32 Energy, Boolean ExpectedViolation)
        {

            var json = new JObject(
                           new JProperty("rule",   new JArray(Operator, "500", "kWh")),
                           new JProperty("level",  "low")
                       );

            Assert.That(EnergyValidationRule.TryParse(json, out var rule),  Is.True);
            Assert.That(rule!.IsViolatedBy(Energy),  Is.EqualTo(ExpectedViolation));

        }

        #endregion

        #region A_malformed_rule_is_rejected(...)

        [Test]
        public void A_malformed_rule_is_rejected()
        {

            Assert.Multiple(() => {

                // Not an array
                Assert.That(EnergyValidationRule.TryParse(
                                new JObject(new JProperty("rule", "nonsense")), out _),  Is.False);

                // Too few elements
                Assert.That(EnergyValidationRule.TryParse(
                                new JObject(new JProperty("rule", new JArray(">", "500"))), out _),  Is.False);

                // Unknown operator
                Assert.That(EnergyValidationRule.TryParse(
                                new JObject(new JProperty("rule", new JArray("~", "500", "kWh"))), out _),  Is.False);

                // Threshold is not a number
                Assert.That(EnergyValidationRule.TryParse(
                                new JObject(new JProperty("rule", new JArray(">", "lots", "kWh"))), out _),  Is.False);

            });

        }

        #endregion

        #region Energy_units_are_normalized_to_kWh(...)

        [TestCase("Wh",     500, 0.5)]
        [TestCase("kWh",    500, 500)]
        [TestCase("MWh",      5, 5000)]
        [TestCase(" mwh ",    5, 5000)]   // trimmed and case-insensitive
        public void Energy_units_are_normalized_to_kWh(String Unit, Int32 Value, Decimal ExpectedKWh)
        {

            Assert.That(EnergyValidationRule.EnergyInKWh(Value, Unit),  Is.EqualTo(ExpectedKWh));

        }

        #endregion

        #region A_rule_with_an_unknown_unit_never_fires()

        [Test]
        public void A_rule_with_an_unknown_unit_never_fires()
        {

            // A plausibility rule nobody can evaluate must not warn on every
            // single charging session.
            var json = new JObject(
                           new JProperty("rule",   new JArray(">", "500", "Joule")),
                           new JProperty("level",  "low")
                       );

            Assert.That(EnergyValidationRule.TryParse(json, out var rule),  Is.True);

            Assert.Multiple(() => {
                Assert.That(rule!.ThresholdInKWh,          Is.Null);
                Assert.That(rule.IsViolatedBy(1000000m),   Is.False);
            });

        }

        #endregion

        #region An_unknown_rule_is_ignored_rather_than_rejected()

        [Test]
        public void An_unknown_rule_is_ignored_rather_than_rejected()
        {

            // An unknown future rule must not stop a verification.
            var rules = ValidationRules.Parse(
                            JObject.Parse("""{ "chargingSession": { "someFutureRule": { "rule": [">", "1", "x"] } } }""")
                        );

            Assert.That(rules.TotalEnergy,  Is.Null);

        }

        #endregion

        #region Validation_rules_round_trip_through_JSON()

        [Test]
        public void Validation_rules_round_trip_through_JSON()
        {

            var original  = ValidationRules.Default;
            var roundTrip = ValidationRules.Parse(original.ToJSON());

            Assert.That(roundTrip.TotalEnergy,  Is.Not.Null);

            Assert.Multiple(() => {
                Assert.That(roundTrip.TotalEnergy!.Operator,   Is.EqualTo(original.TotalEnergy!.Operator));
                Assert.That(roundTrip.TotalEnergy.Threshold,  Is.EqualTo(original.TotalEnergy.Threshold));
                Assert.That(roundTrip.TotalEnergy.Unit,       Is.EqualTo(original.TotalEnergy.Unit));
                Assert.That(roundTrip.TotalEnergy.Level,      Is.EqualTo(original.TotalEnergy.Level));
            });

        }

        #endregion


    }

}
