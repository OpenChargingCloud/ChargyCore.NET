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

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// Low-level helpers shared by all charge transparency data format parsers:
    /// hexadecimal conversion, OBIS numbers, timestamps and the writers that
    /// assemble the byte buffers over which energy meters compute their signatures.
    ///
    /// This is the C# port of "chargyLib.ts". The buffer writers deliberately
    /// reproduce the JavaScript semantics of the original bit for bit, including
    /// its quirks — see <see cref="GetInt64Bytes"/> — because those bytes are what
    /// real energy meters signed, and a "cleaner" implementation would simply fail
    /// to verify genuine charge transparency records.
    /// </summary>
    public static partial class ChargyLib
    {

        #region (private) JavaScript number semantics

        /// <summary>
        /// The ECMAScript ToInt32 conversion.
        ///
        /// JavaScript bitwise operators coerce their operands to signed 32 bit
        /// integers, which is why the byte writers below lose everything above
        /// 2^31 even though they emit eight bytes.
        /// </summary>
        private static Int32 ToInt32(Int64 Value)

            => unchecked((Int32) Value);

        #endregion


        #region GetInt8Bytes    (Value)

        /// <summary>
        /// The lowest byte of the given value.
        /// </summary>
        public static Byte[] GetInt8Bytes(Int64 Value)

            => [ (Byte) (ToInt32(Value) & 0xFF) ];

        #endregion

        #region GetInt16Bytes   (Value)

        /// <summary>
        /// The lowest two bytes of the given value, most significant byte first.
        /// </summary>
        public static Byte[] GetInt16Bytes(Int64 Value)
        {

            var value = ToInt32(Value);

            return [
                       (Byte) ((value >> 8) & 0xFF),
                       (Byte) ( value       & 0xFF)
                   ];

        }

        #endregion

        #region GetInt32Bytes   (Value)

        /// <summary>
        /// The four bytes of the given value, most significant byte first.
        /// </summary>
        public static Byte[] GetInt32Bytes(Int64 Value)
        {

            var value  = ToInt32(Value);
            var bytes  = new Byte[4];

            for (var i = 3; i >= 0; i--)
            {
                bytes[i]  = (Byte) (value & 0xFF);
                value   >>= 8;
            }

            return bytes;

        }

        #endregion

        #region GetInt64Bytes   (Value)

        /// <summary>
        /// The eight bytes of the given value, most significant byte first.
        ///
        /// Note: This mirrors "getInt64Bytes()" of ChargyCore.TS, which shifts
        /// through JavaScript bitwise operators and therefore works on a signed
        /// 32 bit value. The upper four bytes are consequently always 0x00 for
        /// non-negative values and 0xFF for negative ones, no matter how large
        /// the input was.
        ///
        /// This is not an oversight in the port: energy meter readings are well
        /// below 2^31, and the bytes produced here are exactly the bytes the
        /// meters signed. Emitting a mathematically correct 64 bit encoding
        /// instead would break the verification of genuine measurements.
        /// </summary>
        public static Byte[] GetInt64Bytes(Int64 Value)
        {

            var value  = ToInt32(Value);
            var bytes  = new Byte[8];

            for (var i = 7; i >= 0; i--)
            {
                bytes[i]  = (Byte) (value & 0xFF);
                value   >>= 8;
            }

            return bytes;

        }

        #endregion


        #region ToHex           (Bytes)

        /// <summary>
        /// The lower case hexadecimal representation of the given bytes.
        /// </summary>
        public static String ToHex(ReadOnlySpan<Byte> Bytes)

            => Convert.ToHexStringLower(Bytes);

        #endregion

        #region ParseHexString  (Hex)

        /// <summary>
        /// Parse a hexadecimal string into its bytes.
        ///
        /// A trailing half byte is dropped, exactly like "parseHexString()" of
        /// ChargyCore.TS does, because some vendors pad their hex strings.
        /// </summary>
        public static Byte[] ParseHexString(String Hex)
        {

            var length  = Hex.Length / 2;
            var bytes   = new Byte[length];

            for (var i = 0; i < length; i++)
                bytes[i] = Byte.Parse(
                               Hex.AsSpan(2 * i, 2),
                               NumberStyles.HexNumber,
                               CultureInfo.InvariantCulture
                           );

            return bytes;

        }

        #endregion

        #region CleanHex        (Hex)

        /// <summary>
        /// Remove all whitespace and an optional "0x" prefix from a hexadecimal string.
        /// </summary>
        public static String CleanHex(String Hex)
        {

            var cleaned = WhitespaceRegex().Replace(Hex, "");

            return cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                       ? cleaned[2..]
                       : cleaned;

        }

        #endregion

        #region HexToBytes      (Hex)

        /// <summary>
        /// Parse a hexadecimal string into its bytes, tolerating whitespace and
        /// a "0x" prefix, and requiring an even number of hexadecimal digits.
        /// </summary>
        /// <exception cref="ArgumentException">When the string has an odd length or contains non-hexadecimal characters.</exception>
        public static Byte[] HexToBytes(String Hex)
        {

            var cleaned = CleanHex(Hex);

            if (cleaned.Length % 2 != 0)
                throw new ArgumentException(
                          $"The hexadecimal string '{Hex}' has an odd number of characters!",
                          nameof(Hex)
                      );

            return Convert.FromHexString(cleaned);

        }

        #endregion

        #region Hex32           (Value)

        /// <summary>
        /// The given value as an eight character upper case hexadecimal string.
        /// </summary>
        public static String Hex32(Int64 Value)

            => (unchecked((UInt32) ToInt32(Value))).ToString("X8", CultureInfo.InvariantCulture);

        #endregion

        #region Hex2Bin         (Hex, Reverse = false)

        /// <summary>
        /// The lowest eight bits of the given hexadecimal string as a binary string.
        /// </summary>
        /// <param name="Hex">A hexadecimal string.</param>
        /// <param name="Reverse">Whether to reverse the order of the hexadecimal byte pairs first.</param>
        public static String Hex2Bin(String Hex, Boolean Reverse = false)
        {

            var hex = Hex;

            if (Reverse)
            {

                var pairs = new List<String>();

                for (var i = 0; i < hex.Length; i += 2)
                    pairs.Add(hex.Substring(i, Math.Min(2, hex.Length - i)));

                pairs.Reverse();
                hex = String.Concat(pairs);

            }

            var value = Convert.ToUInt64(hex, 16);

            return Convert.ToString((Int64) (value & 0xFF), 2).PadLeft(8, '0');

        }

        #endregion


        #region ParseOBIS               (OBIS)

        /// <summary>
        /// Parse a 12 character hexadecimal OBIS number into its human readable
        /// "A-B:C.D.E*F" representation, e.g. "0100010800ff" => "1-0:1.8.0*255".
        ///
        /// DIN EN 62056-61:2002, see https://wiki.volkszaehler.org/software/obis
        /// </summary>
        /// <exception cref="ArgumentException">When the given string is not 12 characters long.</exception>
        public static String ParseOBIS(String OBIS)
        {

            if (OBIS.Length != 12)
                throw new ArgumentException($"Invalid OBIS number '{OBIS}'!", nameof(OBIS));

            static Byte Part(String obis, Int32 offset)
                => Byte.Parse(obis.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            var media       = Part(OBIS,  0);   // A =>  1: Energy
            var channel     = Part(OBIS,  2);   // B =>  0: No channels available
            var indicator   = Part(OBIS,  4);   // C =>  1: Active energy import P+, kWh
            var mode        = Part(OBIS,  6);   // D => 17: Signed instantaneous value
            var quantities  = Part(OBIS,  8);   // E =>  0: Total
            var storage     = Part(OBIS, 10);   // F

            return $"{media}-{channel}:{indicator}.{mode}.{quantities}*{storage}";

        }

        #endregion

        #region OBIS2Hex                (OBIS)

        /// <summary>
        /// The 12 character hexadecimal representation of a human readable OBIS
        /// number, e.g. "1-0:1.8.0*255" => "0100010800ff".
        ///
        /// Returns "000000000000" when the given string is not an OBIS number.
        /// </summary>
        public static String OBIS2Hex(String OBIS)
        {

            var match = OBISRegex().Match(OBIS);

            if (!match.Success)
                return "000000000000";

            static String Part(Group group)
                => Byte.Parse(
                       group.Success ? group.Value : "0",
                       CultureInfo.InvariantCulture
                   ).ToString("x2", CultureInfo.InvariantCulture);

            return Part(match.Groups[ 2]) +   // optional  A
                   Part(match.Groups[ 4]) +   // optional  B
                   Part(match.Groups[ 6]) +   // mandatory C
                   Part(match.Groups[ 7]) +   // mandatory D
                   Part(match.Groups[ 9]) +   // optional  E
                   Part(match.Groups[11]);    // optional  F

        }

        #endregion

        #region OBIS2MeasurementName    (OBIS)

        /// <summary>
        /// The measurement name of well-known OBIS numbers, or the OBIS number itself.
        /// </summary>
        public static String OBIS2MeasurementName(String OBIS)

            => OBIS switch {

                   "1-0:1.7.0*255"      => "Total Real Power",

                   "1-0:1.8.0*198"      => "ENERGY_TOTAL",
                   "1-0:1.8.0*255"      => "ENERGY_TOTAL",
                   "1-0:1.17.0*255"     => "ENERGY_TOTAL",

                   // DZG GSH01, "Total Transaction Import Device Energy"
                   "1-0:152.8.0*255"    => "ENERGY_TOTAL",
                   "01-00:98.08.00.FF"  => "ENERGY_TOTAL",

                   _                    => OBIS

               };

        #endregion

        #region MeasurementName2Human   (MeasurementName)

        /// <summary>
        /// The human readable form of a measurement name.
        /// </summary>
        public static String MeasurementName2Human(String MeasurementName)

            => MeasurementName switch {
                   "ENERGY_TOTAL"  => "Bezogene Energiemenge",
                   _               => MeasurementName
               };

        #endregion


        #region ParseUTC                (Timestamp)

        /// <summary>
        /// Parse an ISO 8601 timestamp. A timestamp without an explicit UTC offset
        /// is interpreted as UTC, matching "moment.utc()" of ChargyCore.TS.
        /// </summary>
        /// <exception cref="FormatException">When the timestamp cannot be parsed.</exception>
        public static DateTimeOffset ParseUTC(String Timestamp)

            => DateTimeOffset.Parse(
                   Timestamp,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
               );

        #endregion

        #region ParseUnixTimestamp      (UnixTime)

        /// <summary>
        /// The given number of seconds since the UNIX epoch as a timestamp.
        /// </summary>
        public static DateTimeOffset ParseUnixTimestamp(Int64 UnixTime)

            => DateTimeOffset.FromUnixTimeSeconds(UnixTime);

        #endregion


        #region SetHex              (CryptoBuffer, Hex,   Offset, Reverse = false)

        /// <summary>
        /// Write the given hexadecimal string into the signature buffer.
        /// </summary>
        /// <param name="CryptoBuffer">The buffer over which the signature is computed.</param>
        /// <param name="Hex">The value as a hexadecimal string.</param>
        /// <param name="Offset">The position within the buffer.</param>
        /// <param name="Reverse">Whether to reverse the byte order.</param>
        /// <returns>The written bytes as a hexadecimal string, for the verification trace.</returns>
        public static String SetHex(Span<Byte>  CryptoBuffer,
                                    String      Hex,
                                    Int32       Offset,
                                    Boolean     Reverse = false)
        {

            var bytes = ParseHexString(Hex);

            if (Reverse)
                Array.Reverse(bytes);

            bytes.CopyTo(CryptoBuffer[Offset..]);

            return ToHex(bytes);

        }

        #endregion

        #region SetTimestamp        (CryptoBuffer, Timestamp, Offset, AddLocalOffset = true, TimeZone = null)

        /// <summary>
        /// Write the lowest four bytes of a UNIX timestamp into the signature buffer
        /// in little endian byte order.
        ///
        /// Note: Despite its name this writes four bytes, not eight — the returned
        /// trace string is padded to eight bytes because ChargyCore.TS builds it in an
        /// eight byte scratch buffer of which only the first four are ever written.
        /// </summary>
        /// <param name="CryptoBuffer">The buffer over which the signature is computed.</param>
        /// <param name="Timestamp">The timestamp of the measurement.</param>
        /// <param name="Offset">The position within the buffer.</param>
        /// <param name="AddLocalOffset">
        /// Whether to add the UTC offset of <paramref name="TimeZone"/> to the UNIX
        /// timestamp. The EMH and GDF energy meters sign their local wall clock time,
        /// so their measurements only verify when this is enabled.
        /// </param>
        /// <param name="TimeZone">
        /// The time zone the energy meter used. Defaults to the time zone of this
        /// machine, which is what ChargyCore.TS uses — meaning verification there
        /// silently depends on where the verifying computer stands. Pass the time
        /// zone of the charging station to get a reproducible result.
        /// </param>
        /// <returns>The written bytes as a hexadecimal string, for the verification trace.</returns>
        public static String SetTimestamp(Span<Byte>      CryptoBuffer,
                                          DateTimeOffset  Timestamp,
                                          Int32           Offset,
                                          Boolean         AddLocalOffset  = true,
                                          TimeZoneInfo?   TimeZone        = null)
        {

            Span<Byte> trace = stackalloc Byte[8];

            WriteUnixTimestamp32(
                CryptoBuffer,
                trace,
                Timestamp,
                Offset,
                AddLocalOffset,
                TimeZone
            );

            return ToHex(trace);

        }

        #endregion

        #region SetTimestamp32      (CryptoBuffer, Timestamp, Offset, AddLocalOffset = true, TimeZone = null)

        /// <summary>
        /// Write the lowest four bytes of a UNIX timestamp into the signature buffer
        /// in little endian byte order.
        ///
        /// Identical to <see cref="SetTimestamp"/>, but the returned trace string
        /// covers only the four bytes actually written.
        /// </summary>
        /// <param name="CryptoBuffer">The buffer over which the signature is computed.</param>
        /// <param name="Timestamp">The timestamp of the measurement.</param>
        /// <param name="Offset">The position within the buffer.</param>
        /// <param name="AddLocalOffset">Whether to add the UTC offset of <paramref name="TimeZone"/>.</param>
        /// <param name="TimeZone">The time zone the energy meter used; defaults to the time zone of this machine.</param>
        /// <returns>The written bytes as a hexadecimal string, for the verification trace.</returns>
        public static String SetTimestamp32(Span<Byte>      CryptoBuffer,
                                            DateTimeOffset  Timestamp,
                                            Int32           Offset,
                                            Boolean         AddLocalOffset  = true,
                                            TimeZoneInfo?   TimeZone        = null)
        {

            Span<Byte> trace = stackalloc Byte[4];

            WriteUnixTimestamp32(
                CryptoBuffer,
                trace,
                Timestamp,
                Offset,
                AddLocalOffset,
                TimeZone
            );

            return ToHex(trace);

        }

        #endregion

        #region (private) WriteUnixTimestamp32(...)

        private static void WriteUnixTimestamp32(Span<Byte>      CryptoBuffer,
                                                 Span<Byte>      Trace,
                                                 DateTimeOffset  Timestamp,
                                                 Int32           Offset,
                                                 Boolean         AddLocalOffset,
                                                 TimeZoneInfo?   TimeZone)
        {

            var unixTime = Timestamp.ToUnixTimeSeconds();

            if (AddLocalOffset)
            {

                var timeZone = TimeZone ?? TimeZoneInfo.Local;

                unixTime += (Int64) timeZone.GetUtcOffset(Timestamp).TotalSeconds;

            }

            var bytes = GetInt64Bytes(unixTime);

            // The four least significant bytes, least significant byte first.
            for (var i = 0; i < 4; i++)
            {
                CryptoBuffer[Offset + i]  = bytes[7 - i];
                Trace       [        i]   = bytes[7 - i];
            }

        }

        #endregion

        #region SetInt8             (CryptoBuffer, Value, Offset)

        /// <summary>
        /// Write a single byte into the signature buffer.
        /// </summary>
        /// <returns>The written byte as a hexadecimal string, for the verification trace.</returns>
        public static String SetInt8(Span<Byte>  CryptoBuffer,
                                     Int64       Value,
                                     Int32       Offset)
        {

            var value = (Byte) (ToInt32(Value) & 0xFF);

            CryptoBuffer[Offset] = value;

            return value.ToString("x2", CultureInfo.InvariantCulture);

        }

        #endregion

        #region SetUInt32           (CryptoBuffer, Value, Offset, Reverse = false)

        /// <summary>
        /// Write a 32 bit value into the signature buffer, most significant byte
        /// first, or least significant byte first when reversed.
        /// </summary>
        /// <returns>The written bytes as a hexadecimal string, for the verification trace.</returns>
        public static String SetUInt32(Span<Byte>  CryptoBuffer,
                                       Int64       Value,
                                       Int32       Offset,
                                       Boolean     Reverse = false)
        {

            var bytes = GetInt32Bytes(Value);

            if (Reverse)
                Array.Reverse(bytes);

            bytes.CopyTo(CryptoBuffer[Offset..]);

            return ToHex(bytes);

        }

        #endregion

        #region SetUInt64           (CryptoBuffer, Value, Offset, Reverse = false)

        /// <summary>
        /// Write a 64 bit value into the signature buffer, most significant byte
        /// first, or least significant byte first when reversed.
        ///
        /// See <see cref="GetInt64Bytes"/> for why values above 2^31 are truncated.
        /// </summary>
        /// <returns>The written bytes as a hexadecimal string, for the verification trace.</returns>
        public static String SetUInt64(Span<Byte>  CryptoBuffer,
                                       Int64       Value,
                                       Int32       Offset,
                                       Boolean     Reverse = false)
        {

            var bytes = GetInt64Bytes(Value);

            if (Reverse)
                Array.Reverse(bytes);

            bytes.CopyTo(CryptoBuffer[Offset..]);

            return ToHex(bytes);

        }

        #endregion

        #region SetUInt64           (CryptoBuffer, Value, Offset, Reverse = false)

        /// <summary>
        /// Write a decimal measurement value into the signature buffer as a 64 bit value.
        /// </summary>
        /// <returns>The written bytes as a hexadecimal string, for the verification trace.</returns>
        public static String SetUInt64(Span<Byte>  CryptoBuffer,
                                       Decimal     Value,
                                       Int32       Offset,
                                       Boolean     Reverse = false)

            => SetUInt64(
                   CryptoBuffer,
                   (Int64) Value,
                   Offset,
                   Reverse
               );

        #endregion

        #region SetText             (CryptoBuffer, Text,  Offset)

        /// <summary>
        /// Write the UTF-8 encoded text into the signature buffer.
        /// </summary>
        /// <returns>The written bytes as a hexadecimal string, for the verification trace.</returns>
        public static String SetText(Span<Byte>  CryptoBuffer,
                                     String      Text,
                                     Int32       Offset)
        {

            var bytes = Encoding.UTF8.GetBytes(Text);

            bytes.CopyTo(CryptoBuffer[Offset..]);

            return ToHex(bytes);

        }

        #endregion

        #region SetUInt32WithCode   (CryptoBuffer, Value, Scale, OBIS, Offset, Reverse = false)

        /// <summary>
        /// Write a 32 bit measurement value followed by its scale factor and its
        /// OBIS code into the signature buffer.
        /// </summary>
        /// <returns>
        /// The written bytes as a hexadecimal string, with the value, the scale factor
        /// and the OBIS code separated by middle dots for the verification trace.
        /// </returns>
        public static String SetUInt32WithCode(Span<Byte>  CryptoBuffer,
                                               Int64       Value,
                                               Int64       Scale,
                                               Int64       OBIS,
                                               Int32       Offset,
                                               Boolean     Reverse = false)
        {

            var valueBytes = GetInt32Bytes(Value);

            if (Reverse)
                Array.Reverse(valueBytes);

            Span<Byte> trace = stackalloc Byte[valueBytes.Length + 2];

            valueBytes.CopyTo(CryptoBuffer[Offset..]);
            valueBytes.CopyTo(trace);

            var scaleByte  = (Byte) (ToInt32(Scale) & 0xFF);
            var obisByte   = (Byte) (ToInt32(OBIS)  & 0xFF);

            CryptoBuffer[Offset + valueBytes.Length]      = scaleByte;
            trace       [         valueBytes.Length]      = scaleByte;
            CryptoBuffer[Offset + valueBytes.Length + 1]  = obisByte;
            trace       [         valueBytes.Length + 1]  = obisByte;

            var hex = ToHex(trace);

            return $"{hex[..8]}·{hex[8..10]}·{hex[10..12]}";

        }

        #endregion

        #region SetTextWithLength   (CryptoBuffer, Text,  Offset)

        /// <summary>
        /// Write the length of the UTF-8 encoded text as a big endian 32 bit value,
        /// followed by the text itself, into the signature buffer.
        /// </summary>
        /// <returns>
        /// The written bytes as a hexadecimal string, with the length separated from
        /// the text by a middle dot for the verification trace.
        /// </returns>
        public static String SetTextWithLength(Span<Byte>  CryptoBuffer,
                                               String      Text,
                                               Int32       Offset)
        {

            var bytes = Encoding.UTF8.GetBytes(Text);

            Span<Byte> trace = stackalloc Byte[4 + bytes.Length];

            BinaryPrimitives.WriteInt32BigEndian(CryptoBuffer[Offset..], bytes.Length);
            BinaryPrimitives.WriteInt32BigEndian(trace,                  bytes.Length);

            bytes.CopyTo(CryptoBuffer[(Offset + 4)..]);
            bytes.CopyTo(trace[4..]);

            var hex = ToHex(trace);

            return bytes.Length > 0
                       ? $"{hex[..8]}·{hex[8..]}"
                       : hex;

        }

        #endregion


        #region IsNullOrEmpty       (Value)

        /// <summary>
        /// Whether the given string is null or empty.
        /// </summary>
        public static Boolean IsNullOrEmpty(String? Value)

            => String.IsNullOrEmpty(Value);

        #endregion

        #region WhenNullOrEmpty     (Value, Replacement)

        /// <summary>
        /// The given string, or the replacement when it is null or empty.
        /// </summary>
        public static String WhenNullOrEmpty(String? Value, String Replacement)

            => String.IsNullOrEmpty(Value)
                   ? Replacement
                   : Value;

        #endregion


        #region (private) Regular expressions

        /// <summary>
        /// An OBIS number of the form "A-B:C.D.E*F", where only C and D are mandatory.
        /// </summary>
        [GeneratedRegex(@"((\d+)\-)?((\d+):)?((\d+)\.)(\d+)(\.(\d+))?(\*(\d+))?")]
        private static partial Regex OBISRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();

        #endregion


    }

}
