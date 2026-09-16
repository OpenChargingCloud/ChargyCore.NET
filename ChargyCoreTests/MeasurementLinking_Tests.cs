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

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// A reading knows which measurement it belongs to, however the measurement
    /// was built.
    /// </summary>
    /// <remarks>
    /// There are two ways to put readings into a measurement - hand them to the
    /// constructor, or add them one at a time - and they used to produce
    /// different objects. The constructor copied them into the list and left
    /// every one of them without a measurement, so nothing that verifies a
    /// reading could be used on it: ACrypt's implementations reach for
    /// <c>MeasurementValue.Measurement</c> first and give up when it is null.
    /// AlfenCrypt01 answers "Not an Alfen measurement!" - for a record written
    /// by an Alfen charging station as readily as for one written by anybody
    /// else.
    ///
    /// It was invisible from inside because the record processor sets the same
    /// links again in a pass of its own, so everything that went through the
    /// whole pipeline worked. What did not work was calling a format directly,
    /// which is what somebody integrating this library does first.
    /// </remarks>
    [TestFixture]
    public class MeasurementLinking_Tests
    {

        #region ReadingsGivenToTheConstructor_KnowTheirMeasurement()

        [Test]
        public void ReadingsGivenToTheConstructor_KnowTheirMeasurement()
        {

            var first        = new MeasurementValue("2026-09-16T10:00:00Z", 1);
            var second       = new MeasurementValue("2026-09-16T10:01:00Z", 2);

            var measurement  = new Measurement(
                                   "meter-1",
                                   "ENERGY_TOTAL",
                                   "1-0:1.8.0*255",
                                   0,
                                   Values: [ first, second ]
                               );

            Assert.Multiple(() => {

                Assert.That(first.Measurement,   Is.SameAs(measurement));
                Assert.That(second.Measurement,  Is.SameAs(measurement));

                // And chained to each other, which is what the hash chained
                // formats need in order to notice a reading taken out of the
                // middle.
                Assert.That(first.PreviousValue,   Is.Null);
                Assert.That(second.PreviousValue,  Is.SameAs(first));

                Assert.That(measurement.Values.Count, Is.EqualTo(2));

            });

        }

        #endregion

        #region TheTwoWaysOfBuildingOne_AgreeExactly()

        /// <summary>
        /// Handing the readings to the constructor and adding them afterwards
        /// have to end at the same object.
        /// </summary>
        [Test]
        public void TheTwoWaysOfBuildingOne_AgreeExactly()
        {

            var built  = new Measurement("meter-1", "ENERGY_TOTAL", "1-0:1.8.0*255", 0,
                                         Values: [ new MeasurementValue("2026-09-16T10:00:00Z", 1),
                                                   new MeasurementValue("2026-09-16T10:01:00Z", 2) ]);

            var added  = new Measurement("meter-1", "ENERGY_TOTAL", "1-0:1.8.0*255", 0);

            added.AddValue(new MeasurementValue("2026-09-16T10:00:00Z", 1));
            added.AddValue(new MeasurementValue("2026-09-16T10:01:00Z", 2));

            Assert.Multiple(() => {

                Assert.That(built.Values.Count, Is.EqualTo(added.Values.Count));

                for (var i = 0; i < built.Values.Count; i++)
                {
                    Assert.That(built.Values[i].Measurement,   Is.SameAs(built),  $"value {i}");
                    Assert.That(added.Values[i].Measurement,   Is.SameAs(added),  $"value {i}");

                    Assert.That(built.Values[i].PreviousValue is null,
                                Is.EqualTo(added.Values[i].PreviousValue is null),
                                $"value {i}");
                }

            });

        }

        #endregion

        #region MeasurementsGivenToASession_KnowTheirSession()

        /// <summary>
        /// The same defect one level up, and the one that actually produced a
        /// wrong answer rather than a refusal to answer.
        /// </summary>
        /// <remarks>
        /// ACrypt reads the charging session for fields that a signature
        /// covers. AlfenCrypt01 takes the user identification and the internal
        /// session number from there; with no session it used an empty
        /// identification and a zero, rebuilt a buffer eight bytes different
        /// from the one the meter had signed, and reported a perfectly good
        /// record as an invalid signature - which is to say, as a charging
        /// station that is lying.
        /// </remarks>
        [Test]
        public void MeasurementsGivenToASession_KnowTheirSession()
        {

            var measurement  = new Measurement("meter-1", "ENERGY_TOTAL", "1-0:1.8.0*255", 0,
                                               Values: [ new MeasurementValue("2026-09-16T10:00:00Z", 1) ]);

            var session      = new ChargingSession(
                                   "session-1",
                                   InternalSessionId:  "7",
                                   Measurements:       [ measurement ]
                               );

            Assert.Multiple(() => {

                Assert.That(measurement.ChargingSession, Is.SameAs(session));

                // The whole way down: a reading can reach the session it was
                // taken during, which is what a verifier walks.
                Assert.That(measurement.Values[0].Measurement?.ChargingSession?.InternalSessionId,
                            Is.EqualTo("7"));

            });

        }

        #endregion

        #region AnEmptyMeasurement_IsEmpty()

        [Test]
        public void AnEmptyMeasurement_IsEmpty()
        {

            var measurement = new Measurement("meter-1", null, null, 0);

            Assert.That(measurement.Values, Is.Empty);

        }

        #endregion

    }

}
