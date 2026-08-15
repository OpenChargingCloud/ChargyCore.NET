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

using System.Numerics;

#endregion

namespace cloud.charging.open.chargy.Formats.EDL40
{

    /// <summary>
    /// Something about an EDL40 document does not hold up.
    /// </summary>
    /// <param name="Code">A stable, language-neutral code for the reason.</param>
    /// <param name="Message">What went wrong.</param>
    public class EDL40ValidationException(String  Code,
                                          String  Message) : Exception(Message)
    {

        /// <summary>A stable, language-neutral code for the reason.</summary>
        public String Code { get; } = Code;

    }


    /// <summary>
    /// Reads SML (Smart Message Language) as an EDL40 meter writes it.
    ///
    /// The bytes arrive wrapped in a transport frame, escaped so that the frame
    /// markers can also occur inside the payload, and encoded as text — because
    /// the whole message has to survive a trip through an XML file. This unwraps
    /// all three layers and hands back the one message that matters: the list of
    /// readings the meter signed.
    /// </summary>
    public static class SmlReader
    {

        #region Data

        /// <summary>The escape sequence that opens an SML transport frame.</summary>
        private static ReadOnlySpan<Byte> StartEscape
            => [ 0x1b, 0x1b, 0x1b, 0x1b, 0x01, 0x01, 0x01, 0x01 ];

        /// <summary>The escape sequence itself, which the payload doubles to quote.</summary>
        private static ReadOnlySpan<Byte> Escape
            => [ 0x1b, 0x1b, 0x1b, 0x1b ];

        /// <summary>The base32 alphabet of RFC 4648.</summary>
        private const String Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        /// <summary>
        /// How deeply SML lists may nest.
        ///
        /// A real GetListRes nests about seven levels. The limit exists because
        /// the reader is recursive and the input is a file somebody sent us: a
        /// few kilobytes of nothing but "list of one" would otherwise exhaust the
        /// stack, and in .NET that ends the process rather than raising something
        /// catchable. ChargyCore.TS has no such limit because a JavaScript engine
        /// turns the same input into an ordinary exception.
        /// </summary>
        private const Int32 MaxListDepth = 64;

        #endregion


        #region ParseGetListRes(Data)

        /// <summary>
        /// Read the signed list of readings out of an encoded SML message.
        /// </summary>
        /// <param name="Data">An SML message, base32, base64 or hexadecimal.</param>
        /// <exception cref="EDL40ValidationException">When the data holds no SML list of readings.</exception>
        public static SmlGetListRes ParseGetListRes(String Data)
        {

            // Which encoding was used is not declared anywhere, so every encoding
            // the text could plausibly be gets decoded and parsed. A wrong guess
            // does not produce a wrong reading, it produces no readings at all —
            // the transport frame and the TLV structure have to come out intact.
            foreach (var encoding in GuessEncodings(Data))
            {
                try
                {

                    var result = FindGetListRes(
                                     DecodeMessages(
                                         StripTransport(
                                             Decode(encoding, Data)
                                         )
                                     )
                                 );

                    if (result is not null)
                        return result;

                }
                catch (Exception)
                {
                    // Try the next plausible encoding.
                }
            }

            throw new EDL40ValidationException("SML_NO_GETLISTRES", "No verifiable SML data found");

        }

        #endregion

        #region StripTransport (Raw)

        /// <summary>
        /// Take the SML transport frame off, and unquote the payload.
        ///
        /// Everything before the opening escape sequence is preamble, everything
        /// from the closing one is checksum, and inside the payload a doubled
        /// escape sequence stands for a literal one.
        /// </summary>
        /// <param name="Raw">The raw bytes of an SML message.</param>
        public static Byte[] StripTransport(Byte[] Raw)
        {

            var start = IndexOfSequence(Raw, StartEscape);

            if (start < 0)
                return Raw;

            var index   = start + StartEscape.Length;
            var output  = new List<Byte>(Raw.Length);

            while (index < Raw.Length)
            {

                if (MatchesSequence(Raw, index, Escape))
                {

                    // 0x1a closes the frame.
                    if (index + 4 < Raw.Length && Raw[index + 4] == 0x1a)
                        break;

                    if (MatchesSequence(Raw, index + 4, Escape))
                    {
                        output.AddRange([ 0x1b, 0x1b, 0x1b, 0x1b ]);
                        index += 8;
                        continue;
                    }

                }

                output.Add(Raw[index]);
                index++;

            }

            return [.. output];

        }

        #endregion

        #region ReadTLV        (Buffer, Position)

        /// <summary>
        /// Read one tag-length-value item.
        ///
        /// The high nibble of the first byte is the type, the low nibble the
        /// length — and for a list the "length" counts items rather than bytes,
        /// while for everything else it counts the header along with the data.
        /// That asymmetry is the format, not a mistake.
        /// </summary>
        /// <param name="Buffer">The payload of an SML message.</param>
        /// <param name="Position">Where to start reading.</param>
        /// <returns>The value, which is null when the field was left out, and where the next item starts.</returns>
        /// <exception cref="EDL40ValidationException">When the data is not valid SML.</exception>
        public static (SmlValue? Value, Int32 Next) ReadTLV(Byte[]  Buffer,
                                                            Int32   Position)

            => ReadTLV(Buffer, Position, 0);

        private static (SmlValue? Value, Int32 Next) ReadTLV(Byte[]  Buffer,
                                                             Int32   Position,
                                                             Int32   Depth)
        {

            if (Position >= Buffer.Length)
                throw new EDL40ValidationException("SML_INCOMPLETE", $"Unexpected end of SML data at {Position}");

            var tl = Buffer[Position];

            if (tl == 0x00)
                return (SmlEmpty.Instance, Position + 1);

            if (tl == 0x01)
                return (null, Position + 1);

            var type         = (tl >> 4) & 0x07;
            var length       = tl & 0x0f;
            var headerBytes  = 1;

            #region A length that did not fit into one nibble continues in the following bytes

            if ((tl & 0x80) != 0)
            {

                var p = Position + 1;

                while (p < Buffer.Length && (Buffer[p] & 0x80) != 0)
                {
                    length = (length << 4) | (Buffer[p] & 0x0f);
                    p++;
                    headerBytes++;
                }

                if (p >= Buffer.Length)
                    throw new EDL40ValidationException("SML_INCOMPLETE", "Truncated multi-byte length");

                length = (length << 4) | (Buffer[p] & 0x0f);
                headerBytes++;

            }

            #endregion

            #region A list, whose length counts its items

            if (type == 0x07)
            {

                if (Depth >= MaxListDepth)
                    throw new EDL40ValidationException("SML_TOO_DEEP", $"SML lists nested deeper than {MaxListDepth} levels");

                var position  = Position + headerBytes;
                var items     = new List<SmlValue?>(length);

                for (var i = 0; i < length; i++)
                {
                    var (value, next) = ReadTLV(Buffer, position, Depth + 1);
                    items.Add(value);
                    position = next;
                }

                return (new SmlList(items), position);

            }

            #endregion

            #region ..., and everything else, whose length counts the header along with the data

            var dataLength = length - headerBytes;

            if (dataLength < 0 ||
                Position + headerBytes + dataLength > Buffer.Length)
            {
                throw new EDL40ValidationException("SML_TLV_INVALID", $"Invalid TLV length at {Position}");
            }

            var data      = Buffer[(Position + headerBytes)..(Position + headerBytes + dataLength)];
            var nextItem  = Position + headerBytes + dataLength;

            return type switch {

                       0x00  => (new SmlOctetString(data),                                    nextItem),
                       0x04  => (new SmlBoolean    (data.Length > 0 && data[0] != 0),         nextItem),
                       0x05  => (new SmlInteger    (ToSignedInteger  (data)),                 nextItem),
                       0x06  => (new SmlInteger    (ToUnsignedInteger(data)),                 nextItem),

                       _     => throw new EDL40ValidationException("SML_TLV_INVALID", $"Unknown SML type 0x{type:x}")

                   };

            #endregion

        }

        #endregion

        #region DecodeMessages (Payload)

        /// <summary>
        /// Read every SML message out of an unwrapped payload.
        ///
        /// Meters pad between messages with NUL bytes, and a message that does not
        /// parse ends the reading rather than the whole file: the messages before
        /// it are still perfectly good.
        /// </summary>
        /// <param name="Payload">The unwrapped payload of an SML transport frame.</param>
        public static IEnumerable<SmlList> DecodeMessages(Byte[] Payload)
        {

            var messages  = new List<SmlList>();
            var position  = 0;

            while (position < Payload.Length)
            {

                if (Payload[position] == 0x00)
                {
                    position++;
                    continue;
                }

                var (value, next) = ReadTLV(Payload, position);

                if (next <= position)
                    break;

                if (value is SmlList list)
                    messages.Add(list);

                position = next;

            }

            return messages;

        }

        #endregion

        #region FindGetListRes (Messages)

        /// <summary>
        /// The first "GetListRes" among the given SML messages, or null when there
        /// is none.
        /// </summary>
        /// <param name="Messages">Some SML messages.</param>
        public static SmlGetListRes? FindGetListRes(IEnumerable<SmlList> Messages)
        {

            foreach (var message in Messages)
            {

                if (message.Count < 4)
                    continue;

                if (message.ItemAt(3) is not SmlList messageBody ||
                    messageBody.Count < 2)
                {
                    continue;
                }

                // 0x0701 is the message type of a GetListRes.
                if (messageBody.ItemAt(0) is not SmlInteger tag ||
                    tag.Value != 0x0701                        ||
                    messageBody.ItemAt(1) is not SmlValue body)
                {
                    continue;
                }

                var result = ParseGetListResBody(body);

                if (result is not null)
                    return result;

            }

            return null;

        }

        #endregion

        #region ParseSmlTime   (Value)

        /// <summary>
        /// Read an SML time value, or null when the value is not one.
        /// </summary>
        /// <param name="Value">An SML value.</param>
        public static SmlTime? ParseSmlTime(SmlValue? Value)
        {

            if (Value is not SmlList list ||
                list.Count < 2)
            {
                return null;
            }

            var tag   = AsInt64(list.ItemAt(0));
            var body  = list.ItemAt(1);

            if (tag == 1)
                return new SmlTime(SmlTimeKind.SecondsIndex, AsInt64(body));

            if (tag == 2)
                return new SmlTime(SmlTimeKind.Timestamp,    AsInt64(body));

            if (tag == 3 &&
                body is SmlList localTime &&
                localTime.Count >= 3)
            {
                return new SmlTime(
                           SmlTimeKind.LocalTimestamp,
                           AsInt64(localTime.ItemAt(0)),
                           AsInt64(localTime.ItemAt(1)),
                           AsInt64(localTime.ItemAt(2))
                       );
            }

            return null;

        }

        #endregion


        #region (private, static) ParseGetListResBody(Body)

        /// <summary>
        /// Read the body of a "GetListRes" message, or null when it is not one.
        /// </summary>
        /// <param name="Body">The body of an SML message.</param>
        private static SmlGetListRes? ParseGetListResBody(SmlValue Body)
        {

            if (Body is not SmlList body ||
                body.Count < 6)
            {
                return null;
            }

            var serverId       = AsOctetString(body.ItemAt(1));
            var listName       = AsOctetString(body.ItemAt(2));
            var listSignature  = AsOctetString(body.ItemAt(5));

            if (serverId      is null ||
                listSignature is null ||
                body.ItemAt(4) is not SmlList valueListNode)
            {
                return null;
            }

            var valueList = new List<SmlListEntry>();

            foreach (var entry in valueListNode.Items)
                if (ParseListEntry(entry) is SmlListEntry parsed)
                    valueList.Add(parsed);

            return new SmlGetListRes(
                       serverId,
                       listName,
                       valueList,
                       listSignature
                   );

        }

        #endregion

        #region (private, static) ParseListEntry     (Value)

        /// <summary>
        /// Read one entry of an SML value list, or null when it is not one.
        /// </summary>
        /// <param name="Value">An SML value.</param>
        private static SmlListEntry? ParseListEntry(SmlValue? Value)
        {

            if (Value is not SmlList entry)
                return null;

            return new SmlListEntry {
                       ObjectName      = AsOctetString(entry.ItemAt(0)),
                       Status          = entry.ItemAt(1),
                       ValueTime       = ParseSmlTime (entry.ItemAt(2)),
                       Unit            = AsInt32      (entry.ItemAt(3)),
                       Scaler          = AsInt32      (entry.ItemAt(4)),
                       Value           = entry.ItemAt(5),
                       ValueSignature  = AsOctetString(entry.ItemAt(6))
                   };

        }

        #endregion

        #region (private, static) GuessEncodings     (Data)

        /// <summary>
        /// Every encoding the given text could be.
        ///
        /// The three overlap — "2AF7" is at once valid base32, valid hexadecimal
        /// and, at the right length, valid base64 — which is why this yields
        /// candidates rather than an answer, and the SML structure decides.
        /// </summary>
        /// <param name="Data">An encoded SML message.</param>
        private static IEnumerable<String> GuessEncodings(String? Data)
        {

            var matches = new List<String>();

            if (Data is null || Data.Trim().Length == 0)
                return matches;

            if (TryDecode("base32", Data)) matches.Add("base32");
            if (TryDecode("base64", Data)) matches.Add("base64");
            if (TryDecode("hex",    Data)) matches.Add("hex");

            return matches;

        }

        /// <summary>Whether the given text decodes with the given encoding at all.</summary>
        private static Boolean TryDecode(String  Encoding,
                                         String  Data)
        {

            try
            {
                Decode(Encoding, Data);
                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        #endregion

        #region (private, static) Decode             (Encoding, Data)

        /// <summary>
        /// Decode the given text with the given encoding.
        /// </summary>
        /// <param name="Encoding">One of "base32", "base64" or "hex".</param>
        /// <param name="Data">An encoded SML message.</param>
        private static Byte[] Decode(String  Encoding,
                                     String  Data)

            => Encoding switch {
                   "base32"  => DecodeBase32(Data),
                   "base64"  => DecodeBase64(Data),
                   _         => ChargyLib.HexToBytes(Data)
               };

        #endregion

        #region (private, static) DecodeBase64       (Data)

        /// <summary>
        /// Decode base64, and reject anything that merely looks like it.
        /// </summary>
        /// <param name="Data">A base64 encoded text.</param>
        private static Byte[] DecodeBase64(String Data)
        {

            var clean    = new String([.. Data.Where(character => !Char.IsWhiteSpace(character))]);
            var payload  = clean.TrimEnd('=');

            if (clean.Length % 4 != 0 ||
                clean.Length - payload.Length > 2)
            {
                throw new FormatException("Invalid base64 data");
            }

            foreach (var character in payload)
                if (!Char.IsAsciiLetterOrDigit(character) &&
                    character != '+' && character != '/')
                {
                    throw new FormatException("Invalid base64 data");
                }

            return Convert.FromBase64String(clean);

        }

        #endregion

        #region (private, static) DecodeBase32       (Data)

        /// <summary>
        /// Decode base32 as RFC 4648, discarding the bits of a trailing partial
        /// group.
        ///
        /// Written out rather than taken from Illias because the exact tolerances
        /// matter here: this decoder decides which encodings are *tried*, so one
        /// that accepted less would silently stop reading meter values that the
        /// reference implementation reads.
        /// </summary>
        /// <param name="Data">A base32 encoded text.</param>
        private static Byte[] DecodeBase32(String Data)
        {

            var clean = new String([.. Data.Where(character => !Char.IsWhiteSpace(character))]).
                            TrimEnd('=').
                            ToUpperInvariant();

            if (clean.Length == 0)
                return [];

            var output  = new List<Byte>(clean.Length * 5 / 8);
            var bits    = 0;
            var value   = 0;

            foreach (var character in clean)
            {

                var index = Base32Alphabet.IndexOf(character);

                if (index < 0)
                    throw new FormatException("Invalid base32 data");

                value  = (value << 5) | index;
                bits  += 5;

                if (bits >= 8)
                {
                    bits -= 8;
                    output.Add((Byte) ((value >> bits) & 0xff));
                }

            }

            return [.. output];

        }

        #endregion


        #region (internal, static) SML value helpers

        /// <summary>The bytes of an SML octet string, or null when the value is not one.</summary>
        internal static Byte[]? AsOctetString(SmlValue? Value)

            => (Value as SmlOctetString)?.Bytes;

        /// <summary>
        /// The value of an SML integer, or null when the value is not one — or is
        /// too large to be the unit or the scale of a meter reading.
        /// </summary>
        internal static Int32? AsInt32(SmlValue? Value)

            => Value is SmlInteger integer &&
               integer.Value >= Int32.MinValue &&
               integer.Value <= Int32.MaxValue
                   ? (Int32) integer.Value
                   : null;

        /// <summary>The value of an SML integer, or zero when the value is not one.</summary>
        internal static Int64 AsInt64(SmlValue? Value)

            => Value is SmlInteger integer &&
               integer.Value >= Int64.MinValue &&
               integer.Value <= Int64.MaxValue
                   ? (Int64) integer.Value
                   : 0;

        /// <summary>
        /// The first integer somewhere inside an SML value, searching a list from
        /// its last item backwards.
        ///
        /// Meters wrap these counters differently — some send the number itself,
        /// others a list whose last item holds it — and the direction is what the
        /// reference implementation does, so it stays.
        /// </summary>
        /// <param name="Value">An SML value.</param>
        internal static BigInteger? FindInteger(SmlValue? Value)
        {

            if (Value is null)
                return null;

            if (Value is SmlInteger integer)
                return integer.Value;

            if (Value is SmlList list)
                for (var i = list.Count - 1; i >= 0; i--)
                    if (FindInteger(list.ItemAt(i)) is BigInteger found)
                        return found;

            return null;

        }

        #endregion

        #region (private, static) Byte helpers

        /// <summary>The given bytes as an unsigned big endian integer.</summary>
        private static BigInteger ToUnsignedInteger(ReadOnlySpan<Byte> Bytes)

            => new (Bytes, isUnsigned: true, isBigEndian: true);

        /// <summary>The given bytes as a signed big endian integer.</summary>
        private static BigInteger ToSignedInteger(ReadOnlySpan<Byte> Bytes)

            => Bytes.Length == 0
                   ? BigInteger.Zero
                   : new BigInteger(Bytes, isUnsigned: false, isBigEndian: true);

        /// <summary>Where the given sequence starts within the given bytes, or -1.</summary>
        private static Int32 IndexOfSequence(ReadOnlySpan<Byte>  Haystack,
                                             ReadOnlySpan<Byte>  Needle)

            => Haystack.IndexOf(Needle);

        /// <summary>Whether the given sequence sits at the given position.</summary>
        private static Boolean MatchesSequence(ReadOnlySpan<Byte>  Buffer,
                                               Int32               Position,
                                               ReadOnlySpan<Byte>  Sequence)

            => Position >= 0 &&
               Position + Sequence.Length <= Buffer.Length &&
               Buffer.Slice(Position, Sequence.Length).SequenceEqual(Sequence);

        #endregion


    }

}
