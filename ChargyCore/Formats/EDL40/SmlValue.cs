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
    /// One value of an SML (Smart Message Language) message.
    ///
    /// SML is a tag-length-value encoding: every value announces its own type and
    /// length, and lists simply announce how many values follow. That is all the
    /// structure there is — an SML message carries no field names, so what a value
    /// *means* is decided entirely by the OBIS code that sits next to it.
    /// </summary>
    public abstract class SmlValue
    { }


    /// <summary>
    /// An SML octet string: raw bytes, used for identifications, OBIS codes and
    /// signatures.
    /// </summary>
    /// <param name="Bytes">The bytes.</param>
    public sealed class SmlOctetString(Byte[] Bytes) : SmlValue
    {

        /// <summary>The bytes.</summary>
        public Byte[] Bytes { get; } = Bytes;

        /// <summary>Return a text representation of this octet string.</summary>
        public override String ToString()
            => Convert.ToHexStringLower(Bytes);

    }


    /// <summary>
    /// An SML integer.
    ///
    /// SML distinguishes signed from unsigned integers, but only while they are
    /// being read: the sign decides how the bytes become a number, and after that
    /// nothing in the EDL40 format asks which of the two it was. So both arrive
    /// here, with the sign already applied.
    ///
    /// The value is a <see cref="BigInteger"/> because SML puts no upper bound on
    /// the length of an integer, and a meter reading that silently wrapped around
    /// would be a wrong reading rather than a rejected one.
    /// </summary>
    /// <param name="Value">The value.</param>
    public sealed class SmlInteger(BigInteger Value) : SmlValue
    {

        /// <summary>The value.</summary>
        public BigInteger Value { get; } = Value;

        /// <summary>Return a text representation of this integer.</summary>
        public override String ToString()
            => Value.ToString();

    }


    /// <summary>
    /// An SML boolean.
    /// </summary>
    /// <param name="Value">The value.</param>
    public sealed class SmlBoolean(Boolean Value) : SmlValue
    {

        /// <summary>The value.</summary>
        public Boolean Value { get; } = Value;

        /// <summary>Return a text representation of this boolean.</summary>
        public override String ToString()
            => Value.ToString();

    }


    /// <summary>
    /// An SML list.
    ///
    /// An item may be null, which is how SML says "this optional field was left
    /// out" — as opposed to <see cref="SmlEmpty"/>, which says "this field is
    /// present and empty".
    /// </summary>
    /// <param name="Items">The items.</param>
    public sealed class SmlList(IEnumerable<SmlValue?> Items) : SmlValue
    {

        #region Properties

        /// <summary>The items.</summary>
        public IReadOnlyList<SmlValue?>  Items    { get; } = Items.ToArray();

        /// <summary>The number of items.</summary>
        public Int32                     Count
            => Items.Count;

        #endregion

        #region ItemAt(Index)

        /// <summary>
        /// The item at the given position, or null when the list is shorter.
        ///
        /// Reading past the end is normal here rather than exceptional: SML lists
        /// grew over the years, and a meter that predates a field simply sends a
        /// shorter list.
        /// </summary>
        /// <param name="Index">A position within the list.</param>
        public SmlValue? ItemAt(Int32 Index)

            => Index >= 0 && Index < Items.Count
                   ? Items[Index]
                   : null;

        #endregion

        /// <summary>Return a text representation of this list.</summary>
        public override String ToString()
            => $"[{Items.Count} items]";

    }


    /// <summary>
    /// An SML value that is present but carries nothing.
    /// </summary>
    public sealed class SmlEmpty : SmlValue
    {

        /// <summary>The empty SML value.</summary>
        public static SmlEmpty Instance { get; } = new ();

        private SmlEmpty()
        { }

        /// <summary>Return a text representation of this value.</summary>
        public override String ToString()
            => "empty";

    }


    /// <summary>
    /// How an SML value says when it was measured.
    /// </summary>
    /// <param name="Kind">Whether this is a seconds index, a timestamp, or a local timestamp.</param>
    /// <param name="Timestamp">Seconds since the UNIX epoch, or the seconds index.</param>
    /// <param name="LocalOffsetMinutes">The offset of the meter's local time, in minutes.</param>
    /// <param name="SeasonOffsetMinutes">The offset of the meter's summer time, in minutes.</param>
    public class SmlTime(SmlTimeKind  Kind,
                         Int64        Timestamp,
                         Int64        LocalOffsetMinutes   = 0,
                         Int64        SeasonOffsetMinutes  = 0)
    {

        #region Properties

        /// <summary>Whether this is a seconds index, a timestamp, or a local timestamp.</summary>
        public SmlTimeKind  Kind                   { get; } = Kind;

        /// <summary>Seconds since the UNIX epoch, or the seconds index.</summary>
        public Int64        Timestamp              { get; } = Timestamp;

        /// <summary>The offset of the meter's local time, in minutes.</summary>
        public Int64        LocalOffsetMinutes     { get; } = LocalOffsetMinutes;

        /// <summary>The offset of the meter's summer time, in minutes.</summary>
        public Int64        SeasonOffsetMinutes    { get; } = SeasonOffsetMinutes;

        #endregion

        #region LocalEpoch

        /// <summary>
        /// The moment as the meter's own clock reads it.
        ///
        /// This is what goes into the signed buffer: an EDL40 meter signs the time
        /// it displays, not the UTC instant behind it. The two differ by exactly
        /// the German winter or summer time offset, so treating one as the other
        /// invalidates every signature for half the year.
        /// </summary>
        public Int64 LocalEpoch

            => Timestamp + (LocalOffsetMinutes + SeasonOffsetMinutes) * 60;

        #endregion

        #region UTCTimestamp

        /// <summary>
        /// The moment as a point in time.
        /// </summary>
        public DateTimeOffset UTCTimestamp

            => DateTimeOffset.FromUnixTimeSeconds(Timestamp);

        #endregion

    }


    /// <summary>
    /// What an SML time value counts.
    /// </summary>
    public enum SmlTimeKind
    {

        /// <summary>Seconds since the meter was started.</summary>
        SecondsIndex,

        /// <summary>Seconds since the UNIX epoch.</summary>
        Timestamp,

        /// <summary>Seconds since the UNIX epoch, plus the meter's local offsets.</summary>
        LocalTimestamp

    }


    /// <summary>
    /// One entry of an SML value list: a single reading, with the OBIS code that
    /// says what was read.
    /// </summary>
    public class SmlListEntry
    {

        /// <summary>The OBIS code of the reading.</summary>
        public Byte[]?    ObjectName        { get; init; }

        /// <summary>The status word of the meter.</summary>
        public SmlValue?  Status            { get; init; }

        /// <summary>When the value was measured.</summary>
        public SmlTime?   ValueTime         { get; init; }

        /// <summary>The unit of the value, as a DLMS/COSEM code.</summary>
        public Int32?     Unit              { get; init; }

        /// <summary>The scale of the value, as a power of ten.</summary>
        public Int32?     Scaler            { get; init; }

        /// <summary>The value itself.</summary>
        public SmlValue?  Value             { get; init; }

        /// <summary>An optional signature over this single entry.</summary>
        public Byte[]?    ValueSignature    { get; init; }

    }


    /// <summary>
    /// The SML "GetListRes" message: a set of readings from one meter, signed as
    /// a whole.
    /// </summary>
    /// <param name="ServerId">The identification of the meter that produced the readings.</param>
    /// <param name="ListName">An optional OBIS code naming what kind of list this is.</param>
    /// <param name="ValueList">The readings.</param>
    /// <param name="ListSignature">The signature over the readings.</param>
    public class SmlGetListRes(Byte[]                     ServerId,
                               Byte[]?                    ListName,
                               IEnumerable<SmlListEntry>  ValueList,
                               Byte[]                     ListSignature)
    {

        #region Properties

        /// <summary>The identification of the meter that produced the readings.</summary>
        public Byte[]                       ServerId         { get; } = ServerId;

        /// <summary>An optional OBIS code naming what kind of list this is.</summary>
        public Byte[]?                      ListName         { get; } = ListName;

        /// <summary>The readings.</summary>
        public IReadOnlyList<SmlListEntry>  ValueList        { get; } = ValueList.ToArray();

        /// <summary>The signature over the readings.</summary>
        public Byte[]                       ListSignature    { get; } = ListSignature;

        #endregion

        #region FindEntryByOBIS(OBISHex)

        /// <summary>
        /// The reading with the given OBIS code, or null when the meter did not
        /// send one.
        /// </summary>
        /// <param name="OBISHex">An OBIS code, hexadecimal.</param>
        public SmlListEntry? FindEntryByOBIS(String OBISHex)
        {

            var target = OBISHex.ToLowerInvariant();

            foreach (var entry in ValueList)
                if (entry.ObjectName is not null &&
                    Convert.ToHexStringLower(entry.ObjectName) == target)
                {
                    return entry;
                }

            return null;

        }

        #endregion

    }

}
