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
using System.Numerics;

#endregion

namespace cloud.charging.open.chargy.Formats.EDL40
{

    /// <summary>
    /// The readings of one signed SML message, together with the exact 320 bytes
    /// the meter put its signature on.
    ///
    /// Those 320 bytes are not in the message. The meter signs a fixed layout it
    /// assembles from its own values, and the message carries the values — so
    /// verifying a reading means rebuilding that layout field by field and hoping
    /// to arrive at the same bytes. Every offset below is therefore load-bearing:
    /// a field written one byte off does not produce a slightly wrong answer, it
    /// produces "invalid signature" on a perfectly honest charging session.
    /// </summary>
    public abstract class AEDL40SignatureData
    {

        #region Data

        /// <summary>The length of the block an EDL40 meter signs.</summary>
        public const Int32 SignedDataLength = 320;

        /// <summary>The unit every EDL40 meter reading has to be in: watt hours.</summary>
        protected const Int32 RequiredUnit = 30;

        #endregion

        #region OBIS codes

        /// <summary>The contract identification, i.e. what the driver authorized with.</summary>
        protected const String OBIS_ContractId       = "8182815401ff";

        /// <summary>The signed meter reading.</summary>
        protected const String OBIS_SignedValue      = "0100011100ff";

        /// <summary>The signed meter reading, as the older meters name it.</summary>
        protected const String OBIS_SignedValue2     = "0100010800ff";

        /// <summary>The pagination counter of an EDL40 meter.</summary>
        protected const String OBIS_EDLPagination    = "8180817101ff";

        /// <summary>The seconds index of an EDL40 meter.</summary>
        protected const String OBIS_EDLSecondsIndex  = "810060080001";

        /// <summary>The version of the signature format.</summary>
        protected const String OBIS_SignatureVersion = "00af737672ff";

        /// <summary>The meter reading at the start of the charging session.</summary>
        protected const String OBIS_StartEC          = "010001080080";

        /// <summary>The meter reading at the end of the charging session.</summary>
        protected const String OBIS_ActualEC         = "0100010800ff";

        /// <summary>The pagination counter of an ISA meter.</summary>
        protected const String OBIS_ISAPagination    = "8180c7f040ff";

        /// <summary>The "ESTH" block of an ISA meter.</summary>
        protected const String OBIS_ESTH             = "8180816101ff";

        #endregion

        #region Properties

        /// <summary>Which of the two SML layouts this is.</summary>
        public abstract EDL40Variant  Variant          { get; }

        /// <summary>The 320 bytes the meter signed.</summary>
        public required Byte[]        SignedData       { get; init; }

        /// <summary>The signature the SML message carries.</summary>
        public required Byte[]        ListSignature    { get; init; }

        /// <summary>The identification of the meter.</summary>
        public required Byte[]        ServerId         { get; init; }

        /// <summary>The contract identification, padded to 128 bytes.</summary>
        public required Byte[]        ContractId       { get; init; }

        /// <summary>The pagination counter of the meter.</summary>
        public required Int64         Pagination       { get; init; }

        #endregion


        #region (static) Parse(Data)

        /// <summary>
        /// Read an EDL40 or ISA document out of an encoded SML message.
        ///
        /// The ISA layout is tried first because it is the stricter of the two:
        /// it insists on five specific OBIS codes, so a message that satisfies it
        /// really is an ISA message, while the EDL40 layout would accept it and
        /// read the wrong reading out of it.
        /// </summary>
        /// <param name="Data">An SML message, base32, base64 or hexadecimal.</param>
        /// <exception cref="EDL40ValidationException">When the data is not an EDL40 document.</exception>
        public static AEDL40SignatureData Parse(String Data)
        {

            var getListRes = SmlReader.ParseGetListRes(Data);

            try
            {
                return ISAEDL40SignatureData.Build(getListRes);
            }
            catch (Exception)
            {
                return EDL40PSignatureData.Build(getListRes);
            }

        }

        #endregion

        #region (static) CanParse(Data)

        /// <summary>
        /// Whether the given text is an EDL40 document.
        /// </summary>
        /// <param name="Data">The contents of a file.</param>
        public static Boolean CanParse(String Data)
        {

            try
            {
                Parse(Data);
                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        #endregion


        #region (protected, static) Writing the signed block

        /// <summary>
        /// Write the local time of a reading, as four little endian bytes.
        /// </summary>
        /// <param name="Buffer">The block being assembled.</param>
        /// <param name="Entry">A reading.</param>
        /// <param name="Offset">Where to write.</param>
        /// <exception cref="EDL40ValidationException">When the reading carries no time.</exception>
        protected static void WriteTime(Span<Byte>    Buffer,
                                        SmlListEntry  Entry,
                                        Int32         Offset)
        {

            if (Entry.ValueTime is null)
                throw new EDL40ValidationException("MISSING_FIELD", "EDL40/ISA: missing valTime");

            WriteUInt32(Buffer, (UInt32) Entry.ValueTime.LocalEpoch, Offset);

        }

        /// <summary>
        /// Write an unsigned 32 bit value, least significant byte first.
        /// </summary>
        protected static void WriteUInt32(Span<Byte>  Buffer,
                                          UInt32      Value,
                                          Int32       Offset)

            => BinaryPrimitives.WriteUInt32LittleEndian(Buffer[Offset..(Offset + 4)], Value);

        /// <summary>
        /// Write a meter reading as eight bytes, least significant byte first.
        ///
        /// A reading wider than 64 bits is truncated rather than rejected, because
        /// that is what the meter's own eight byte field does with it.
        /// </summary>
        protected static void WriteInt64(Span<Byte>  Buffer,
                                         BigInteger  Value,
                                         Int32       Offset)

            => BinaryPrimitives.WriteUInt64LittleEndian(
                   Buffer[Offset..(Offset + 8)],
                   (UInt64) (Value & UInt64.MaxValue)
               );

        /// <summary>
        /// Copy at most the given number of bytes, leaving the rest of the field
        /// as it was.
        /// </summary>
        protected static void WriteBytes(Span<Byte>  Buffer,
                                         Byte[]?     Bytes,
                                         Int32       Offset,
                                         Int32       MaxLength)
        {

            if (Bytes is null || Bytes.Length == 0)
                return;

            var length = Math.Min(Bytes.Length, MaxLength);

            Bytes.AsSpan(0, length).CopyTo(Buffer[Offset..]);

        }

        /// <summary>
        /// The last two bytes of the SML signature, which the signed block repeats.
        /// </summary>
        protected static Byte[] LastTwoBytes(Byte[] Bytes)

            => Bytes.Length >= 2
                   ? Bytes[^2..]
                   : Bytes;

        #endregion

        #region (protected, static) Reading the SML values

        /// <summary>
        /// The value of a reading, whether the meter sent it as a number or as
        /// raw bytes.
        /// </summary>
        /// <param name="Entry">A reading.</param>
        protected static BigInteger ValueOf(SmlListEntry Entry)

            => Entry.Value switch {
                   SmlInteger      integer  => integer.Value,
                   SmlOctetString  octets   => octets.Bytes.Length > 0
                                                   ? new BigInteger(octets.Bytes, isUnsigned: false, isBigEndian: true)
                                                   : BigInteger.Zero,
                   _                        => BigInteger.Zero
               };

        /// <summary>
        /// The contract identification, padded to the 128 bytes the signed block
        /// reserves for it.
        /// </summary>
        /// <param name="Entry">The contract identification reading, if there is one.</param>
        protected static Byte[] ContractIdOf(SmlListEntry? Entry)
        {

            var contractId = new Byte[128];

            if (Entry?.Value is SmlOctetString octets)
                octets.Bytes.AsSpan(0, Math.Min(octets.Bytes.Length, 128)).CopyTo(contractId);

            return contractId;

        }

        /// <summary>
        /// The status word of a reading as eight big endian bytes.
        /// </summary>
        /// <param name="Entry">A reading.</param>
        protected static Byte[] Status8Of(SmlListEntry Entry)
        {

            var status = new Byte[8];

            if (Entry.Status is SmlInteger integer)
                BinaryPrimitives.WriteUInt64BigEndian(status, (UInt64) (integer.Value & UInt64.MaxValue));

            return status;

        }

        /// <summary>
        /// The reading with the given OBIS code, or a failure naming what is missing.
        /// </summary>
        /// <param name="GetListRes">A signed list of readings.</param>
        /// <param name="OBIS">An OBIS code, hexadecimal.</param>
        /// <param name="Label">What the reading is called, for the error message.</param>
        /// <exception cref="EDL40ValidationException">When the meter did not send it.</exception>
        protected static SmlListEntry RequireEntry(SmlGetListRes  GetListRes,
                                                   String         OBIS,
                                                   String         Label)

            => GetListRes.FindEntryByOBIS(OBIS)
                   ?? throw new EDL40ValidationException("MISSING_FIELD", $"ISA: missing {Label} entry (OBIS {OBIS})");

        /// <summary>
        /// A counter of the meter — a pagination, a seconds index, a version —
        /// which the meter may have wrapped in a list.
        /// </summary>
        /// <param name="Value">An SML value.</param>
        protected static Int64 CounterOf(SmlValue? Value)

            => SmlReader.FindInteger(Value) is BigInteger counter &&
               counter >= Int64.MinValue &&
               counter <= Int64.MaxValue
                   ? (Int64) counter
                   : 0;

        #endregion

    }


    /// <summary>
    /// Which of the two SML layouts a document uses.
    /// </summary>
    public enum EDL40Variant
    {

        /// <summary>The EDL40 layout: one signed meter reading per message.</summary>
        EDL_40_P,

        /// <summary>The ISA layout: a start and a stop reading in one message.</summary>
        ISA_EDL_40_P

    }


    /// <summary>
    /// Extension methods for the EDL40 layouts.
    /// </summary>
    public static class EDL40VariantExtensions
    {

        /// <summary>
        /// The wire representation of the given layout.
        /// </summary>
        /// <param name="Variant">An EDL40 layout.</param>
        public static String AsText(this EDL40Variant Variant)

            => Variant.ToString();

    }


    /// <summary>
    /// An EDL40 document: one signed meter reading.
    ///
    /// A charging session therefore needs at least two of these — one from the
    /// start and one from the end — and what an EV driver was billed for is the
    /// difference between them.
    /// </summary>
    public class EDL40PSignatureData : AEDL40SignatureData
    {

        #region Properties

        /// <summary>Which of the two SML layouts this is.</summary>
        public override EDL40Variant  Variant    => EDL40Variant.EDL_40_P;

        /// <summary>The version of the signature format.</summary>
        public required Int64         Version           { get; init; }

        /// <summary>
        /// Whether the meter is an eMobility charging controller, which encodes
        /// its status word differently from a plain meter.
        /// </summary>
        public required Boolean       IsEMOC            { get; init; }

        /// <summary>The unit of the reading, as a DLMS/COSEM code.</summary>
        public required Int32         Unit              { get; init; }

        /// <summary>The scale of the reading, as a power of ten.</summary>
        public required Int32         Scaler            { get; init; }

        /// <summary>The meter reading itself.</summary>
        public required BigInteger    MeterValue        { get; init; }

        /// <summary>The OBIS code of the reading.</summary>
        public required Byte[]        ObisId            { get; init; }

        /// <summary>The status word of the meter.</summary>
        public required Int32         Status            { get; init; }

        /// <summary>When the meter took the reading.</summary>
        public required DateTimeOffset MeterTimestamp   { get; init; }

        #endregion


        #region (static) Build(GetListRes)

        /// <summary>
        /// Rebuild the 320 bytes an EDL40 meter signed.
        /// </summary>
        /// <param name="GetListRes">A signed list of readings.</param>
        /// <exception cref="EDL40ValidationException">When the readings do not make up an EDL40 document.</exception>
        public static EDL40PSignatureData Build(SmlGetListRes GetListRes)
        {

            var listSignature     = GetListRes.ListSignature;

            // A 66 byte signature is what an eMobility charging controller sends.
            var isEMOC            = listSignature.Length == 66;

            var signedValueEntry  = GetListRes.FindEntryByOBIS(OBIS_SignedValue)
                                        ?? GetListRes.FindEntryByOBIS(OBIS_SignedValue2)
                                        ?? throw new EDL40ValidationException("MISSING_FIELD", "EDL40: missing signed value entry");

            var contractEntry     = GetListRes.FindEntryByOBIS(OBIS_ContractId);
            var paginationEntry   = GetListRes.FindEntryByOBIS(OBIS_EDLPagination);
            var secondsIndexEntry = GetListRes.FindEntryByOBIS(OBIS_EDLSecondsIndex);
            var versionEntry      = GetListRes.FindEntryByOBIS(OBIS_SignatureVersion);

            var unit              = signedValueEntry.Unit ?? 0;

            if (unit != RequiredUnit)
                throw new EDL40ValidationException("INVALID_UNIT", "EDL40: unit must be 30 (Wh)");

            var scaler            = signedValueEntry.Scaler ?? 0;
            var meterValue        = ValueOf(signedValueEntry);
            var obisId            = signedValueEntry.ObjectName ?? new Byte[6];
            var contractId        = ContractIdOf(contractEntry);

            #region The status word, which an eMobility charging controller scatters over 32 bits

            var status = 0;

            if (signedValueEntry.Status is SmlInteger statusValue)
            {

                var status32 = (UInt32) (statusValue.Value & UInt32.MaxValue);

                status = isEMOC
                             ? TransformEMOCStatus(status32)
                             : (Int32) (status32 & 0xff);

            }

            #endregion

            var pagination        = CounterOf(paginationEntry?.  Value);
            var secondsIndex      = CounterOf(secondsIndexEntry?.Value);
            var version           = CounterOf(versionEntry?.     Value);

            #region The 320 bytes, in the order the meter assembles them

            var signedData = new Byte[SignedDataLength];
            var buffer     = signedData.AsSpan();

            WriteBytes (buffer, GetListRes.ServerId,                     0,  10);   //   0.. 10  the meter
            WriteTime  (buffer, signedValueEntry,                       10);        //  10.. 14  when it was read, meter local time
            buffer[14] = (Byte) status;                                             //  14.. 15
            WriteUInt32(buffer, (UInt32) secondsIndex,                  15);        //  15.. 19
            WriteUInt32(buffer, (UInt32) pagination,                    19);        //  19.. 23
            WriteBytes (buffer, obisId,                                 23,   6);   //  23.. 29  what was read
            buffer[29] = (Byte) unit;                                               //  29.. 30  1e => 30 => Wh
            buffer[30] = (Byte) scaler;                                             //  30.. 31
            WriteInt64 (buffer, meterValue,                             31);        //  31.. 39  the reading itself
            WriteBytes (buffer, LastTwoBytes(listSignature),            39,   2);   //  39.. 41
            WriteBytes (buffer, contractId,                             41, 128);   //  41..169  the token the driver authorized with

            if (contractEntry is not null)
                WriteTime(buffer, contractEntry,                       169);        // 169..173

            #endregion

            return new EDL40PSignatureData {
                       SignedData      = signedData,
                       ListSignature   = listSignature,
                       ServerId        = GetListRes.ServerId,
                       ContractId      = contractId,
                       Pagination      = pagination,
                       Version         = version,
                       IsEMOC          = isEMOC,
                       Unit            = unit,
                       Scaler          = scaler,
                       MeterValue      = meterValue,
                       ObisId          = obisId,
                       Status          = status,
                       MeterTimestamp  = signedValueEntry.ValueTime?.UTCTimestamp
                                             ?? DateTimeOffset.UnixEpoch
                   };

        }

        #endregion

        #region (static) TransformEMOCStatus(Status)

        /// <summary>
        /// Collect the eight status bits an eMobility charging controller spreads
        /// across its 32 bit status word.
        ///
        /// The mapping is not a compression of the word — it picks six specific
        /// bits and leaves bits 1 and 2 of the result clear, because a charging
        /// controller has no equivalent of them.
        /// </summary>
        /// <param name="Status">The 32 bit status word of the controller.</param>
        public static Int32 TransformEMOCStatus(UInt32 Status)
        {

            var result = 0;

            void Map(Int32 TargetBit, Int32 SourceBit)
            {
                if ((Status & (1u << SourceBit)) != 0)
                    result |= 1 << TargetBit;
            }

            Map(0, 17);
            Map(3, 31);
            Map(4, 16);
            Map(5, 11);
            Map(6,  9);
            Map(7,  8);

            return result & 0xff;

        }

        #endregion

    }


    /// <summary>
    /// An ISA document: a start and a stop reading in one signed message.
    ///
    /// A single ISA message is therefore already a whole charging session, which
    /// is why it also names itself — as a start, an intermediate update, or the
    /// final reading of the session.
    /// </summary>
    public class ISAEDL40SignatureData : AEDL40SignatureData
    {

        #region Properties

        /// <summary>Which of the two SML layouts this is.</summary>
        public override EDL40Variant   Variant    => EDL40Variant.ISA_EDL_40_P;

        /// <summary>The signature over the readings, without its two trailing bytes.</summary>
        public required Byte[]         DataSignature      { get; init; }

        /// <summary>The OBIS code naming what kind of list this is.</summary>
        public required Byte[]?        ListName           { get; init; }

        /// <summary>The unit of the readings, as a DLMS/COSEM code.</summary>
        public required Int32          Unit               { get; init; }

        /// <summary>The meter reading at the end of the charging session.</summary>
        public required BigInteger     ActualECValue      { get; init; }

        /// <summary>The scale of the reading at the end of the charging session.</summary>
        public required Int32          ActualECScaler     { get; init; }

        /// <summary>The OBIS code of the reading at the end of the charging session.</summary>
        public required Byte[]         ActualECObis       { get; init; }

        /// <summary>The status word at the end of the charging session.</summary>
        public required Byte[]         ActualECStatus     { get; init; }

        /// <summary>When the reading at the end of the charging session was taken.</summary>
        public required DateTimeOffset ActualECTimestamp  { get; init; }

        /// <summary>The meter reading at the start of the charging session.</summary>
        public required BigInteger     StartECValue       { get; init; }

        /// <summary>The scale of the reading at the start of the charging session.</summary>
        public required Int32          StartECScaler      { get; init; }

        /// <summary>The OBIS code of the reading at the start of the charging session.</summary>
        public required Byte[]         StartECObis        { get; init; }

        /// <summary>The status word at the start of the charging session.</summary>
        public required Byte[]         StartECStatus      { get; init; }

        /// <summary>When the reading at the start of the charging session was taken.</summary>
        public required DateTimeOffset StartECTimestamp   { get; init; }

        #endregion

        #region ListNameContext

        /// <summary>
        /// Whether this message is the start, an update, or the end of a charging
        /// session.
        /// </summary>
        public String ListNameContext

            => ContextOf(ListName);

        #endregion


        #region (static) Build(GetListRes)

        /// <summary>
        /// Rebuild the 320 bytes an ISA meter signed.
        /// </summary>
        /// <param name="GetListRes">A signed list of readings.</param>
        /// <exception cref="EDL40ValidationException">When the readings do not make up an ISA document.</exception>
        public static ISAEDL40SignatureData Build(SmlGetListRes GetListRes)
        {

            var contractEntry    = RequireEntry(GetListRes, OBIS_ContractId,    "contract-id");
            var startEntry       = RequireEntry(GetListRes, OBIS_StartEC,       "start-ec");
            var actualEntry      = RequireEntry(GetListRes, OBIS_ActualEC,      "actual-ec");
            var paginationEntry  = RequireEntry(GetListRes, OBIS_ISAPagination, "pagination");
            var esthEntry        = RequireEntry(GetListRes, OBIS_ESTH,          "esth");

            var actualUnit       = actualEntry.Unit ?? 0;
            var startUnit        = startEntry. Unit ?? 0;

            if (actualUnit != RequiredUnit ||
                startUnit  != RequiredUnit)
            {
                throw new EDL40ValidationException("INVALID_UNIT", "ISA: unit must be 30 (Wh)");
            }

            if (paginationEntry.Value is not SmlInteger paginationValue)
                throw new EDL40ValidationException("MISSING_FIELD", "ISA: pagination is not an unsigned integer");

            var contractId       = ContractIdOf(contractEntry);
            var esth             = SmlReader.AsOctetString(esthEntry.Value) ?? new Byte[20];
            var actualStatus     = Status8Of(actualEntry);
            var startStatus      = Status8Of(startEntry);
            var actualValue      = ValueOf  (actualEntry);
            var startValue       = ValueOf  (startEntry);
            var actualSignature  = actualEntry.ValueSignature ?? new Byte[66];
            var listName         = GetListRes.ListName ?? new Byte[6];
            var listSignature    = GetListRes.ListSignature;
            var pagination       = CounterOf(paginationValue);

            // The last two bytes of the SML signature are not part of the signature
            // itself: they are repeated inside the signed block instead.
            var dataSignature    = listSignature.Length >= 2
                                       ? listSignature[..^2]
                                       : listSignature;

            #region The 320 bytes, in the order the meter assembles them

            var signedData = new Byte[SignedDataLength];
            var buffer     = signedData.AsSpan();

            WriteBytes (buffer, GetListRes.ServerId,                     0,  10);   //   0.. 10  the meter
            WriteTime  (buffer, actualEntry,                            10);        //  10.. 14  the end of the session
            buffer[14] = actualStatus[7];                                           //  14.. 15
            WriteBytes (buffer, actualEntry.ObjectName,                 15,   6);   //  15.. 21
            buffer[21] = (Byte)  actualUnit;                                        //  21.. 22
            buffer[22] = (Byte) (actualEntry.Scaler ?? 0);                          //  22.. 23
            WriteInt64 (buffer, actualValue,                            23);        //  23.. 31  the reading at the end
            WriteBytes (buffer, LastTwoBytes(listSignature),            31,   2);   //  31.. 33
            WriteBytes (buffer, actualSignature,                        33,  66);   //  33.. 99
            WriteBytes (buffer, contractId,                             99, 128);   //  99..227  the token the driver authorized with
            WriteTime  (buffer, startEntry,                            227);        // 227..231  the start of the session
            WriteBytes (buffer, esth,                                  231,  20);   // 231..251
            buffer[251] = startStatus[7];                                           // 251..252
            WriteBytes (buffer, startEntry.ObjectName,                 252,   6);   // 252..258
            buffer[258] = (Byte)  startUnit;                                        // 258..259
            buffer[259] = (Byte) (startEntry.Scaler ?? 0);                          // 259..260
            WriteInt64 (buffer, startValue,                            260);        // 260..268  the reading at the start
            WriteBytes (buffer, listName,                              268,   6);   // 268..274  start, update or stop
            WriteUInt32(buffer, (UInt32) pagination,                   274);        // 274..278

            #endregion

            return new ISAEDL40SignatureData {
                       SignedData         = signedData,
                       ListSignature      = listSignature,
                       DataSignature      = dataSignature,
                       ServerId           = GetListRes.ServerId,
                       ListName           = GetListRes.ListName,
                       ContractId         = contractId,
                       Pagination         = pagination,
                       Unit               = actualUnit,
                       ActualECValue      = actualValue,
                       ActualECScaler     = actualEntry.Scaler ?? 0,
                       ActualECObis       = actualEntry.ObjectName ?? new Byte[6],
                       ActualECStatus     = actualStatus,
                       ActualECTimestamp  = actualEntry.ValueTime?.UTCTimestamp ?? DateTimeOffset.UnixEpoch,
                       StartECValue       = startValue,
                       StartECScaler      = startEntry.Scaler ?? 0,
                       StartECObis        = startEntry.ObjectName ?? new Byte[6],
                       StartECStatus      = startStatus,
                       StartECTimestamp   = startEntry.ValueTime?.UTCTimestamp ?? DateTimeOffset.UnixEpoch
                   };

        }

        #endregion

        #region (static) ContextOf(ListName)

        /// <summary>
        /// Whether a list name says this is the start, an update, or the end of a
        /// charging session.
        ///
        /// An unknown name reads as the start, which is the reference
        /// implementation's choice: a message that does not say is treated as an
        /// opening reading rather than as a closing one, so it can never on its
        /// own be taken to conclude a session.
        /// </summary>
        /// <param name="ListName">The OBIS code naming a list.</param>
        public static String ContextOf(Byte[]? ListName)

            => (ListName is not null ? Convert.ToHexStringLower(ListName) : "") switch {
                   "8180816201ff"  => "UPDATE",
                   "8180816202ff"  => "STOP",
                   _               => "START"
               };

        #endregion

    }

}
