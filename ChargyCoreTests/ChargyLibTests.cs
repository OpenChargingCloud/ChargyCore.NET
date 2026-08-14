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
    /// Parity tests for <see cref="ChargyLib"/>.
    ///
    /// Every expected value in this fixture was produced by executing the original
    /// chargyLib.ts helpers on Node.js, not by reading the C# implementation. The
    /// byte buffers these helpers assemble are the buffers real energy meters signed,
    /// so a divergence here is not a style difference — it makes genuine measurements
    /// fail to verify.
    /// </summary>
    [TestFixture]
    public class ChargyLibTests
    {

        #region GetInt64Bytes_matches_the_JavaScript_implementation(...)

        // Note the sign extension for values at or above 2^31 and the wrap to zero
        // at 2^32: JavaScript bitwise operators work on signed 32 bit integers, so
        // "getInt64Bytes" never really was a 64 bit conversion.
        [TestCase(         0L, "0000000000000000")]
        [TestCase(         1L, "0000000000000001")]
        [TestCase(       255L, "00000000000000ff")]
        [TestCase(       256L, "0000000000000100")]
        [TestCase(     22675L, "0000000000005893")]
        [TestCase(1554181214L, "000000005ca2ec5e")]
        [TestCase(2147483647L, "000000007fffffff")]
        [TestCase(2147483648L, "ffffffff80000000")]
        [TestCase(4294967296L, "0000000000000000")]
        public void GetInt64Bytes_matches_the_JavaScript_implementation(Int64 Value, String Expected)
        {

            Assert.That(
                ChargyLib.ToHex(ChargyLib.GetInt64Bytes(Value)),
                Is.EqualTo(Expected)
            );

        }

        #endregion

        #region GetInt32Bytes_matches_the_JavaScript_implementation(...)

        [TestCase(         0L, "00000000")]
        [TestCase(         1L, "00000001")]
        [TestCase(       255L, "000000ff")]
        [TestCase(     66051L, "00010203")]
        [TestCase(2147483647L, "7fffffff")]
        [TestCase(2147483648L, "80000000")]
        public void GetInt32Bytes_matches_the_JavaScript_implementation(Int64 Value, String Expected)
        {

            Assert.That(
                ChargyLib.ToHex(ChargyLib.GetInt32Bytes(Value)),
                Is.EqualTo(Expected)
            );

        }

        #endregion

        #region Hex32_matches_the_JavaScript_implementation(...)

        // 4886718345 is 0x123456789: the JavaScript "val &= 0xFFFFFFFF" coerces to
        // a signed 32 bit value first, so the leading digit is lost.
        [TestCase(         0L, "00000000")]
        [TestCase(       255L, "000000FF")]
        [TestCase(4886718345L, "23456789")]
        [TestCase(2147483648L, "80000000")]
        public void Hex32_matches_the_JavaScript_implementation(Int64 Value, String Expected)
        {

            Assert.That(ChargyLib.Hex32(Value),  Is.EqualTo(Expected));

        }

        #endregion

        #region Hex2Bin_matches_the_JavaScript_implementation(...)

        [TestCase("ff",   false, "11111111")]
        [TestCase("01",   false, "00000001")]
        [TestCase("abcd", false, "11001101")]
        [TestCase("0100", true,  "00000001")]
        public void Hex2Bin_matches_the_JavaScript_implementation(String Hex, Boolean Reverse, String Expected)
        {

            Assert.That(ChargyLib.Hex2Bin(Hex, Reverse),  Is.EqualTo(Expected));

        }

        #endregion


        #region ParseOBIS_matches_the_JavaScript_implementation(...)

        [TestCase("0100010800ff", "1-0:1.8.0*255")]
        [TestCase("0100011100ff", "1-0:1.17.0*255")]
        [TestCase("0100980800ff", "1-0:152.8.0*255")]
        public void ParseOBIS_matches_the_JavaScript_implementation(String OBIS, String Expected)
        {

            Assert.That(ChargyLib.ParseOBIS(OBIS),  Is.EqualTo(Expected));

        }

        #endregion

        #region ParseOBIS_rejects_OBIS_numbers_of_the_wrong_length()

        [Test]
        public void ParseOBIS_rejects_OBIS_numbers_of_the_wrong_length()
        {

            Assert.That(
                () => ChargyLib.ParseOBIS("0100010800"),
                Throws.ArgumentException
            );

        }

        #endregion

        #region OBIS2Hex_matches_the_JavaScript_implementation(...)

        [TestCase("1-0:1.8.0*255",   "0100010800ff")]
        [TestCase("1-0:1.17.0*255",  "0100011100ff")]
        [TestCase("1-0:1.8.0*198",   "0100010800c6")]
        [TestCase("1-0:152.8.0*255", "0100980800ff")]
        [TestCase("1.8.0",           "000001080000")]   // A, B and F are optional
        [TestCase("not-an-obis",     "000000000000")]
        public void OBIS2Hex_matches_the_JavaScript_implementation(String OBIS, String Expected)
        {

            Assert.That(ChargyLib.OBIS2Hex(OBIS),  Is.EqualTo(Expected));

        }

        #endregion

        #region OBIS_numbers_round_trip()

        [TestCase("1-0:1.8.0*255")]
        [TestCase("1-0:1.17.0*255")]
        [TestCase("1-0:152.8.0*255")]
        public void OBIS_numbers_round_trip(String OBIS)
        {

            Assert.That(ChargyLib.ParseOBIS(ChargyLib.OBIS2Hex(OBIS)),  Is.EqualTo(OBIS));

        }

        #endregion


        #region SetHex_writes_and_traces_the_given_bytes()

        [Test]
        public void SetHex_writes_and_traces_the_given_bytes()
        {

            Span<Byte> buffer = stackalloc Byte[16];

            var forward       = ChargyLib.SetHex(buffer, "0a01445a470033008506", 0);
            var forwardBuffer = ChargyLib.ToHex(buffer);

            buffer.Clear();

            var reversed       = ChargyLib.SetHex(buffer, "0a01445a470033008506", 0, Reverse: true);
            var reversedBuffer = ChargyLib.ToHex(buffer);

            Assert.Multiple(() => {

                Assert.That(forward,         Is.EqualTo("0a01445a470033008506"));
                Assert.That(forwardBuffer,   Is.EqualTo("0a01445a470033008506000000000000"));

                Assert.That(reversed,        Is.EqualTo("0685003300475a44010a"));
                Assert.That(reversedBuffer,  Is.EqualTo("0685003300475a44010a000000000000"));

            });

        }

        #endregion

        #region SetTimestamp_writes_four_little_endian_bytes_and_traces_eight()

        [Test]
        public void SetTimestamp_writes_four_little_endian_bytes_and_traces_eight()
        {

            Span<Byte> buffer     = stackalloc Byte[16];

            // 2019-04-02T05:00:14Z and 2019-06-26T08:57:44Z, without a local offset,
            // so that this test is independent of the time zone it runs in.
            var timestamp1 = DateTimeOffset.FromUnixTimeSeconds(1554181214);
            var timestamp2 = DateTimeOffset.FromUnixTimeSeconds(1561539464);

            var trace1  = ChargyLib.SetTimestamp(buffer, timestamp1, 0, TimeSpan.Zero);
            var trace2  = ChargyLib.SetTimestamp(buffer, timestamp2, 4, TimeSpan.Zero);
            var written = ChargyLib.ToHex(buffer);

            Assert.Multiple(() => {

                // The trace is eight bytes wide although only four are written:
                // ChargyCore.TS builds it in an eight byte scratch buffer.
                Assert.That(trace1,   Is.EqualTo("5eeca25c00000000"));
                Assert.That(trace2,   Is.EqualTo("8833135d00000000"));

                Assert.That(written,  Is.EqualTo("5eeca25c8833135d0000000000000000"));

            });

        }

        #endregion

        #region SetTimestamp32_traces_only_the_four_written_bytes()

        [Test]
        public void SetTimestamp32_traces_only_the_four_written_bytes()
        {

            Span<Byte> buffer = stackalloc Byte[16];

            var trace = ChargyLib.SetTimestamp32(
                            buffer,
                            DateTimeOffset.FromUnixTimeSeconds(1554181214),
                            0,
                            TimeSpan.Zero
                        );

            Assert.That(trace,  Is.EqualTo("5eeca25c"));

        }

        #endregion

        #region SetTimestamp_adds_the_offset_the_energy_meter_used()

        [Test]
        public void SetTimestamp_adds_the_offset_the_energy_meter_used()
        {

            Span<Byte> buffer = stackalloc Byte[8];

            // 2019-04-02, so Europe/Berlin is on summer time: +2h.
            var withOffset = ChargyLib.SetTimestamp32(buffer, "2019-04-02T05:00:14Z", 0);
            var withoutIt  = ChargyLib.SetTimestamp32(buffer, "2019-04-02T05:00:14Z", 0, AddMeterOffset: false);

            Assert.Multiple(() => {

                // 1554181214 = 0x5CA2EC5E, little endian.
                Assert.That(withoutIt,   Is.EqualTo("5eeca25c"));

                // 1554181214 + 7200 = 1554188414 = 0x5CA3087E, little endian.
                Assert.That(withOffset,  Is.EqualTo("7e08a35c"));

            });

        }

        #endregion

        #region SetTimestamp_does_not_depend_on_the_verifying_machine()

        [Test]
        public void SetTimestamp_does_not_depend_on_the_verifying_machine()
        {

            // This is the bug that made a correctly signed EMH record verify in
            // Germany and fail everywhere else: the offset was taken from the host
            // rather than from the record. Asserting the concrete Europe/Berlin
            // bytes catches a relapse on any machine, not only on a CI runner.
            Span<Byte> buffer = stackalloc Byte[8];

            var summer = ChargyLib.SetTimestamp32(buffer, "2019-04-02T05:00:14Z", 0);   // CEST, +2h
            var winter = ChargyLib.SetTimestamp32(buffer, "2019-02-19T07:47:50Z", 0);   // CET,  +1h

            Assert.Multiple(() => {

                Assert.That(ChargyLib.MeterTimeZone.Id,  Is.EqualTo("Europe/Berlin"));

                // 1554181214 + 7200 = 1554188414 = 0x5CA3087E
                Assert.That(summer,  Is.EqualTo("7e08a35c"));

                // 1550562470 + 3600 = 1550566070 = 0x5C6BC2B6
                Assert.That(winter,  Is.EqualTo("b6c26b5c"));

            });

        }

        #endregion

        #region A_timestamp_with_an_explicit_offset_states_the_meters_own_offset()

        [Test]
        public void A_timestamp_with_an_explicit_offset_states_the_meters_own_offset()
        {

            // chargeIT writes the meter's own local and season offset into the
            // timestamp, so that offset wins over the Europe/Berlin assumption.
            var stated   = ChargyLib.MeterLocalTime("2019-02-19T08:47:50+01:00");
            var assumed  = ChargyLib.MeterLocalTime("2019-02-19T07:47:50Z");
            var elsewhen = ChargyLib.MeterLocalTime("2019-02-19T12:47:50+05:00");

            Assert.Multiple(() => {

                // Same instant either way ...
                Assert.That(stated.  Instant.ToUnixTimeSeconds(),  Is.EqualTo(1550562470));
                Assert.That(assumed. Instant.ToUnixTimeSeconds(),  Is.EqualTo(1550562470));

                // ... but the offset comes from the record when it states one.
                Assert.That(stated.  UTCOffset,  Is.EqualTo(TimeSpan.FromHours(1)));
                Assert.That(assumed. UTCOffset,  Is.EqualTo(TimeSpan.FromHours(1)));   // Europe/Berlin in February
                Assert.That(elsewhen.UTCOffset,  Is.EqualTo(TimeSpan.FromHours(5)));

            });

        }

        #endregion

        #region The_German_offset_is_resolved_including_daylight_saving_time()

        [Test]
        public void The_German_offset_is_resolved_including_daylight_saving_time()
        {

            // 2019-03-31T01:00:00Z is the exact moment Europe/Berlin switches from
            // CET to CEST. A fixed +1h assumption would silently misdate every
            // summer measurement by an hour.
            Assert.Multiple(() => {

                Assert.That(ChargyLib.MeterLocalTime("2019-02-19T07:47:50Z").UTCOffset,  Is.EqualTo(TimeSpan.FromMinutes( 60)));
                Assert.That(ChargyLib.MeterLocalTime("2019-06-26T15:33:20Z").UTCOffset,  Is.EqualTo(TimeSpan.FromMinutes(120)));

                Assert.That(ChargyLib.MeterLocalTime("2019-03-31T00:59:00Z").UTCOffset,  Is.EqualTo(TimeSpan.FromMinutes( 60)));
                Assert.That(ChargyLib.MeterLocalTime("2019-03-31T01:01:00Z").UTCOffset,  Is.EqualTo(TimeSpan.FromMinutes(120)));

            });

        }

        #endregion

        #region Another_meter_time_zone_can_be_requested()

        [Test]
        public void Another_meter_time_zone_can_be_requested()
        {

            var utc     = TimeZoneInfo.FindSystemTimeZoneById("UTC");
            var kolkata = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

            Assert.Multiple(() => {
                Assert.That(ChargyLib.MeterLocalTime("2019-02-19T07:47:50Z", utc).    UTCOffset,  Is.EqualTo(TimeSpan.Zero));
                Assert.That(ChargyLib.MeterLocalTime("2019-02-19T07:47:50Z", kolkata).UTCOffset,  Is.EqualTo(TimeSpan.FromMinutes(330)));
            });

        }

        #endregion

        #region The_64_bit_variant_used_by_GDF_follows_the_same_rules()

        [Test]
        public void The_64_bit_variant_used_by_GDF_follows_the_same_rules()
        {

            Span<Byte> buffer = stackalloc Byte[8];

            var withOffset = ChargyLib.SetTimestamp(buffer, "2019-04-02T05:00:14Z", 0);
            var withoutIt  = ChargyLib.SetTimestamp(buffer, "2019-04-02T05:00:14Z", 0, AddMeterOffset: false);

            Assert.Multiple(() => {
                // Four written bytes, an eight byte trace — as with SetTimestamp32.
                Assert.That(withOffset,  Is.EqualTo("7e08a35c00000000"));
                Assert.That(withoutIt,   Is.EqualTo("5eeca25c00000000"));
            });

        }

        #endregion

        #region A_Z_suffix_says_nothing_about_the_meter()

        [Test]
        public void A_Z_suffix_says_nothing_about_the_meter()
        {

            // "Z" only says the record stores the instant in UTC. Reading it as
            // "the meter ran on UTC" is what produced a zero offset and a wrong
            // signature buffer.
            Assert.Multiple(() => {

                Assert.That(ChargyLib.MeterLocalTime("2019-04-02T05:00:14Z").UTCOffset,
                            Is.EqualTo(TimeSpan.FromHours(2)));

                Assert.That(ChargyLib.MeterLocalTime("2019-04-02T05:00:14").UTCOffset,
                            Is.EqualTo(TimeSpan.FromHours(2)));

                Assert.That(ChargyLib.MeterLocalTime("2019-04-02T05:00:14+00:00").UTCOffset,
                            Is.EqualTo(TimeSpan.Zero));

            });

        }

        #endregion

        #region SetUInt32_writes_big_endian_or_reversed()

        [Test]
        public void SetUInt32_writes_big_endian_or_reversed()
        {

            Span<Byte> buffer = stackalloc Byte[24];

            var forward  = ChargyLib.SetUInt32(buffer, 66051, 0);
            var reversed = ChargyLib.SetUInt32(buffer, 66051, 4, Reverse: true);

            Assert.Multiple(() => {
                Assert.That(forward,   Is.EqualTo("00010203"));
                Assert.That(reversed,  Is.EqualTo("03020100"));
            });

            Assert.That(ChargyLib.ToHex(buffer[..8]),  Is.EqualTo("0001020303020100"));

        }

        #endregion

        #region SetUInt64_writes_big_endian_or_reversed()

        [Test]
        public void SetUInt64_writes_big_endian_or_reversed()
        {

            Span<Byte> buffer = stackalloc Byte[24];

            var forward   = ChargyLib.SetUInt64(buffer, 22675L,      8);
            var reversed  = ChargyLib.SetUInt64(buffer, 22675L,      8, Reverse: true);
            var truncated = ChargyLib.SetUInt64(buffer, 4294967296L, 8, Reverse: true);

            Assert.Multiple(() => {

                Assert.That(forward,    Is.EqualTo("0000000000005893"));
                Assert.That(reversed,   Is.EqualTo("9358000000000000"));

                // Above 2^32 the JavaScript original truncates to nothing at all.
                Assert.That(truncated,  Is.EqualTo("0000000000000000"));

            });

        }

        #endregion

        #region SetUInt64_accepts_decimal_measurement_values()

        [Test]
        public void SetUInt64_accepts_decimal_measurement_values()
        {

            Span<Byte> buffer = stackalloc Byte[8];

            Assert.That(
                ChargyLib.SetUInt64(buffer, 22675.0m, 0, Reverse: true),
                Is.EqualTo("9358000000000000")
            );

        }

        #endregion

        #region SetText_writes_UTF8()

        [Test]
        public void SetText_writes_UTF8()
        {

            Span<Byte> buffer = stackalloc Byte[32];

            Assert.That(
                ChargyLib.SetText(buffer, "DE*GEF*EVSE*CHARGY*1", 0),
                Is.EqualTo("44452a4745462a455653452a4348415247592a31")
            );

        }

        #endregion

        #region SetUInt32WithCode_separates_value_scale_and_OBIS()

        [Test]
        public void SetUInt32WithCode_separates_value_scale_and_OBIS()
        {

            Span<Byte> buffer = stackalloc Byte[16];

            var forward  = ChargyLib.SetUInt32WithCode(buffer, 22675,   0, 30, 0);
            var reversed = ChargyLib.SetUInt32WithCode(buffer, 22675, 255, 30, 0, Reverse: true);

            Assert.Multiple(() => {
                Assert.That(forward,   Is.EqualTo("00005893·00·1e"));
                Assert.That(reversed,  Is.EqualTo("93580000·ff·1e"));
            });

        }

        #endregion

        #region SetTextWithLength_prefixes_a_big_endian_length()

        [Test]
        public void SetTextWithLength_prefixes_a_big_endian_length()
        {

            Span<Byte> buffer = stackalloc Byte[32];

            var withText  = ChargyLib.SetTextWithLength(buffer, "ENERGY_TOTAL", 0);
            var withEmpty = ChargyLib.SetTextWithLength(buffer, "",             0);

            Assert.Multiple(() => {

                Assert.That(withText,   Is.EqualTo("0000000c·454e455247595f544f54414c"));

                // An empty text gets no separator at all.
                Assert.That(withEmpty,  Is.EqualTo("00000000"));

            });

        }

        #endregion


        #region ParseUTC_treats_a_timestamp_without_an_offset_as_UTC()

        [Test]
        public void ParseUTC_treats_a_timestamp_without_an_offset_as_UTC()
        {

            Assert.Multiple(() => {

                Assert.That(ChargyLib.ParseUTC("2019-04-05T14:54:50.000Z").     ToUnixTimeSeconds(),  Is.EqualTo(1554476090));
                Assert.That(ChargyLib.ParseUTC("2019-04-05T14:54:50.000").      ToUnixTimeSeconds(),  Is.EqualTo(1554476090));
                Assert.That(ChargyLib.ParseUTC("2019-04-05T16:54:50.000+02:00").ToUnixTimeSeconds(),  Is.EqualTo(1554476090));

            });

        }

        #endregion

        #region HexToBytes_tolerates_whitespace_and_a_0x_prefix()

        [Test]
        public void HexToBytes_tolerates_whitespace_and_a_0x_prefix()
        {

            Assert.Multiple(() => {

                Assert.That(ChargyLib.ToHex(ChargyLib.HexToBytes("0a 01 44 5a")),  Is.EqualTo("0a01445a"));
                Assert.That(ChargyLib.ToHex(ChargyLib.HexToBytes("0x0A01445A")),   Is.EqualTo("0a01445a"));

                Assert.That(() => ChargyLib.HexToBytes("0a01445"),  Throws.ArgumentException);

            });

        }

        #endregion

        #region OBIS2MeasurementName_maps_the_well_known_OBIS_numbers(...)

        [TestCase("1-0:1.8.0*255",      "ENERGY_TOTAL")]
        [TestCase("1-0:1.8.0*198",      "ENERGY_TOTAL")]
        [TestCase("1-0:1.17.0*255",     "ENERGY_TOTAL")]
        [TestCase("1-0:152.8.0*255",    "ENERGY_TOTAL")]
        [TestCase("01-00:98.08.00.FF",  "ENERGY_TOTAL")]
        [TestCase("1-0:1.7.0*255",      "Total Real Power")]
        [TestCase("1-0:99.99.99*255",   "1-0:99.99.99*255")]
        public void OBIS2MeasurementName_maps_the_well_known_OBIS_numbers(String OBIS, String Expected)
        {

            Assert.That(ChargyLib.OBIS2MeasurementName(OBIS),  Is.EqualTo(Expected));

        }

        #endregion


    }

}
