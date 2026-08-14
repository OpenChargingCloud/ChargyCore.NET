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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Tests for the verification result types.
    ///
    /// The enum wire strings below were extracted from the enum declarations in
    /// chargyInterfaces.ts, not from the C# implementation. They appear verbatim
    /// in the golden verification reports, so a rename on either side breaks the
    /// parity contract between both implementations — which is exactly what
    /// <see cref="The_charging_session_verification_results_match_ChargyCoreTS"/>
    /// and its sibling are here to catch.
    /// </summary>
    [TestFixture]
    public class ResultTypeTests
    {

        #region Data

        private static readonly String[] expectedSessionVerificationResults = [
            "Unvalidated",
            "UnknownCTRFormat",
            "NoChargeTransparencyRecordsFound",
            "UnknownSessionFormat",
            "InvalidSessionFormat",
            "AtLeastTwoMeasurementsRequired",
            "InconsistentTimestamps",
            "MissingStartValue",
            "InvalidStartValue",
            "InvalidIntermediateValue",
            "MissingStopValue",
            "InvalidStopValue",
            "EnergyMeterNotFound",
            "InvalidMeasurement",
            "InplausibleMeasurement",
            "PublicKeyNotFound",
            "UnknownPublicKeyFormat",
            "InvalidPublicKey",
            "UnknownSignatureFormat",
            "InvalidSignature",
            "ValidSignature"
        ];

        private static readonly String[] expectedVerificationResults = [
            "Unvalidated",
            "NoOperation",
            "UnknownCTRFormat",
            "EnergyMeterNotFound",
            "InvalidMeasurement",
            "InvalidStartValue",
            "StartValue",
            "ValidStartValue",
            "InvalidIntermediateValue",
            "IntermediateValue",
            "ValidIntermediateValue",
            "InvalidStopValue",
            "StopValue",
            "ValidStopValue",
            "PublicKeyNotFound",
            "UnknownPublicKeyFormat",
            "InvalidPublicKey",
            "UnknownSignatureFormat",
            "InvalidSignature",
            "ValidSignature",
            "ValidationError"
        ];

        #endregion


        #region The_charging_session_verification_results_match_ChargyCoreTS()

        [Test]
        public void The_charging_session_verification_results_match_ChargyCoreTS()
        {

            var actual = Enum.GetValues<SessionVerificationResult>().
                              Select(result => result.AsText()).
                              ToArray();

            Assert.That(actual,  Is.EquivalentTo(expectedSessionVerificationResults));

        }

        #endregion

        #region The_measurement_verification_results_match_ChargyCoreTS()

        [Test]
        public void The_measurement_verification_results_match_ChargyCoreTS()
        {

            var actual = Enum.GetValues<VerificationResult>().
                              Select(result => result.AsText()).
                              ToArray();

            Assert.That(actual,  Is.EquivalentTo(expectedVerificationResults));

        }

        #endregion

        #region Every_verification_result_round_trips_through_its_wire_string()

        [Test]
        public void Every_verification_result_round_trips_through_its_wire_string()
        {

            Assert.Multiple(() => {

                foreach (var result in Enum.GetValues<SessionVerificationResult>())
                    Assert.That(SessionVerificationResultExtensions.Parse(result.AsText()),
                                Is.EqualTo(result));

                foreach (var result in Enum.GetValues<VerificationResult>())
                    Assert.That(VerificationResultExtensions.Parse(result.AsText()),
                                Is.EqualTo(result));

            });

        }

        #endregion

        #region Unknown_verification_results_do_not_parse()

        [Test]
        public void Unknown_verification_results_do_not_parse()
        {

            Assert.Multiple(() => {

                // Case matters: the wire format is exact, and quietly accepting a
                // mis-cased status would hide a malformed charge transparency record.
                Assert.That(SessionVerificationResultExtensions.TryParse("validsignature"),  Is.Null);
                Assert.That(SessionVerificationResultExtensions.TryParse("Nonsense"),        Is.Null);

                // ... but the forgiving Parse() falls back to "not verified yet"
                // rather than throwing in the middle of parsing a record.
                Assert.That(SessionVerificationResultExtensions.Parse("Nonsense"),
                            Is.EqualTo(SessionVerificationResult.Unvalidated));

            });

        }

        #endregion


        #region Severity_levels_use_lower_case_wire_strings()

        [Test]
        public void Severity_levels_use_lower_case_wire_strings()
        {

            Assert.Multiple(() => {

                Assert.That(SeverityLevel.Low.   AsText(),  Is.EqualTo("low"));
                Assert.That(SeverityLevel.Medium.AsText(),  Is.EqualTo("medium"));
                Assert.That(SeverityLevel.High.  AsText(),  Is.EqualTo("high"));

                Assert.That(SeverityLevelExtensions.Parse("low"),     Is.EqualTo(SeverityLevel.Low));
                Assert.That(SeverityLevelExtensions.Parse("MEDIUM"),  Is.EqualTo(SeverityLevel.Medium));
                Assert.That(SeverityLevelExtensions.Parse(" high "),  Is.EqualTo(SeverityLevel.High));

                Assert.That(SeverityLevelExtensions.TryParse("nonsense"),  Is.Null);

            });

        }

        #endregion


        #region A_warning_defaults_to_the_lowest_severity()

        [Test]
        public void A_warning_defaults_to_the_lowest_severity()
        {

            var warning = new Warning(I18NString.Create("Too much energy!"));

            Assert.Multiple(() => {

                Assert.That(warning.Level,                            Is.EqualTo(SeverityLevel.Low));
                Assert.That(warning.ToString(),                       Is.EqualTo("low: Too much energy!"));
                Assert.That((String?) warning.ToJSON()["level"],      Is.EqualTo("low"));

            });

        }

        #endregion

        #region An_error_defaults_to_the_highest_severity_and_carries_its_i18n_key()

        [Test]
        public void An_error_defaults_to_the_highest_severity_and_carries_its_i18n_key()
        {

            var error = new Error(
                            I18NString.Create("Invalid signature!"),
                            Code:     "InvalidSignature",
                            Details:  "r is not on the curve"
                        );

            var json = error.ToJSON();

            Assert.Multiple(() => {

                Assert.That(error.Level,                  Is.EqualTo(SeverityLevel.High));
                Assert.That((String?) json["level"],      Is.EqualTo("high"));
                Assert.That((String?) json["code"],       Is.EqualTo("InvalidSignature"));
                Assert.That((String?) json["details"],    Is.EqualTo("r is not on the curve"));

            });

        }

        #endregion

        #region An_error_without_a_code_or_details_omits_them()

        [Test]
        public void An_error_without_a_code_or_details_omits_them()
        {

            var json = new Error(I18NString.Create("General Error!")).ToJSON();

            Assert.Multiple(() => {
                Assert.That(json["code"],     Is.Null);
                Assert.That(json["details"],  Is.Null);
            });

        }

        #endregion


        #region A_crypto_result_collects_errors_and_warnings()

        [Test]
        public void A_crypto_result_collects_errors_and_warnings()
        {

            var result = new CryptoResult(VerificationResult.InvalidSignature).
                             AddError  (new Error  (I18NString.Create("Invalid signature!"))).
                             AddWarning(new Warning(I18NString.Create("Implausible value!"), SeverityLevel.Medium));

            var json = result.ToJSON();

            Assert.Multiple(() => {

                Assert.That(result.Errors,             Has.Count.EqualTo(1));
                Assert.That(result.Warnings,           Has.Count.EqualTo(1));
                Assert.That((String?) json["status"],  Is.EqualTo("InvalidSignature"));
                Assert.That(json["errors"],            Is.Not.Null);
                Assert.That(json["warnings"],          Is.Not.Null);

            });

        }

        #endregion

        #region A_clean_crypto_result_omits_empty_error_and_warning_lists()

        [Test]
        public void A_clean_crypto_result_omits_empty_error_and_warning_lists()
        {

            var json = new CryptoResult(VerificationResult.ValidSignature).ToJSON();

            Assert.Multiple(() => {
                Assert.That((String?) json["status"],  Is.EqualTo("ValidSignature"));
                Assert.That(json["errors"],            Is.Null);
                Assert.That(json["warnings"],          Is.Null);
            });

        }

        #endregion

        #region A_session_crypto_result_carries_its_certainty()

        [Test]
        public void A_session_crypto_result_carries_its_certainty()
        {

            var result = new SessionCryptoResult(
                             SessionVerificationResult.NoChargeTransparencyRecordsFound,
                             I18NString.Create("No charge transparency records found!"),
                             Certainty: 0
                         );

            var json = result.ToJSON();

            Assert.Multiple(() => {

                Assert.That((String?) json["status"],     Is.EqualTo("NoChargeTransparencyRecordsFound"));
                Assert.That((Double?) json["certainty"],  Is.EqualTo(0));
                Assert.That(json["message"],              Is.Not.Null);

            });

        }

        #endregion


    }

}
