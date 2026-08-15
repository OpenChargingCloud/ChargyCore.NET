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
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.IO;

#endregion

namespace cloud.charging.open.chargy.Formats.Alfen
{

    /// <summary>
    /// The Alfen charging station format.
    ///
    /// A signed meter value looks like this, and an EV driver may well be holding
    /// it on a printed receipt:
    ///
    ///     AP;0;3;AJ2J7LYMGCIWT4AHUJPPFIIVB3FGRV2JQ2HVZG2I;BIHEIWSH…====;S27J5BHL…===;
    ///
    /// Six fields: the format marker, whether this is a start, stop or
    /// intermediate reading, the blob version, the meter's public key, an 82 byte
    /// data set, and the signature over it. Everything after the version is base32,
    /// because the whole thing has to survive being printed on paper and typed
    /// back in by hand.
    ///
    /// The 82 bytes are little endian throughout and contain no separators: the
    /// layout *is* the specification. Which is also why every field of it is
    /// reproduced byte for byte when the signature is checked — see
    /// <see cref="AlfenCrypt01"/>.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    public class AlfenFormat(I18NDictionary I18N) : ITextChargeTransparencyFormat
    {

        #region Data

        /// <summary>The JSON-LD context of an Alfen charging session.</summary>
        public const String SessionContext      = "https://open.charging.cloud/contexts/SessionSignatureFormats/AlfenCrypt01+json";

        /// <summary>The JSON-LD context of an Alfen measurement.</summary>
        public const String MeasurementContext  = "https://open.charging.cloud/contexts/EnergyMeterSignatureFormats/AlfenCrypt03+json";

        /// <summary>The signature format of an Alfen energy meter.</summary>
        public const String MeterSignatureFormat = "https://open.charging.cloud/contexts/EnergyMeterSignatureFormats/AlfenCrypt01";

        private readonly I18NDictionary i18n = I18N;

        #endregion

        #region Properties

        /// <summary>The name of the data format.</summary>
        public String Name
            => "Alfen";

        #endregion


        #region CanParse    (Text)

        /// <summary>
        /// Whether the given text is an Alfen signed meter value.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        public Boolean CanParse(String Text)

            => Text.StartsWith("AP;", StringComparison.Ordinal);

        #endregion

        #region TryParseText(Text, PublicKeyHEX = null)

        /// <summary>
        /// Try to read one or more Alfen signed meter values.
        /// </summary>
        /// <param name="Text">One signed meter value per line.</param>
        /// <param name="PublicKeyHEX">Unused: an Alfen meter value carries its own public key.</param>
        public Object TryParseText(String   Text,
                                   String?  PublicKeyHEX = null)

            => TryParse(
                   Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                   null
               );

        #endregion

        #region TryParse    (SignedValues, ContainerInfos)

        /// <summary>
        /// Try to read a charge transparency record from Alfen signed meter values.
        /// </summary>
        /// <param name="SignedValues">The signed meter values.</param>
        /// <param name="ContainerInfos">What the surrounding container knew, if anything.</param>
        public Object TryParse(IEnumerable<String>  SignedValues,
                               ContainerInfos?      ContainerInfos)
        {

            try
            {

                var signedValues  = SignedValues.ToArray();
                var dataSets      = new List<AlfenDataSet>();
                var common        = new AlfenCommonValues();

                var previousTimestamp = "";

                foreach (var signedValue in signedValues)
                {

                    #region The six fields of a signed meter value

                    var elements = signedValue.Split(';');

                    if (elements.Length != 6 && elements.Length != 7)
                        return Invalid("Invalid number of signed meter values!");

                    var formatId          = elements[0];
                    var type              = elements[1];
                    var blobVersion       = elements[2];
                    var publicKeyValue    = elements[3];

                    var publicKey         = TryFromBase32(publicKeyValue);
                    var dataSet           = TryFromBase32(elements[4]);
                    var signature         = TryFromBase32(elements[5]);

                    #endregion

                    #region The public key has to be the same throughout

                    if (common.PublicKey.Length == 0)
                        common.PublicKey = publicKeyValue;

                    else if (publicKeyValue != common.PublicKey)
                        return Invalid("Inconsistent public keys!");

                    #endregion

                    #region Everything has to have exactly the length the format prescribes

                    if (formatId          != "AP" ||
                        type.Length       != 1    ||
                        blobVersion       != "3"  ||
                        publicKey?.Length != 25   ||
                        dataSet?.Length   != 82   ||
                        signature?.Length != 48)
                    {
                        return Invalid("Invalid charging session format!");
                    }

                    #endregion

                    // A 25 byte public key is a secp192r1 key; the length check above
                    // is what establishes that.
                    common.PublicKeyFormat = "secp192r1";

                    var parsed = ParseDataSet(dataSet, signature);

                    #region Every field that describes the installation has to be the same throughout

                    if (Differs(ref common.AdapterId,          parsed.AdapterId))          return Invalid("Inconsistent Alfen adapter identification!");
                    if (Differs(ref common.AdapterFWVersion,   parsed.AdapterFWVersion))   return Invalid("Inconsistent Alfen adapter firmware version!");
                    if (Differs(ref common.AdapterFWChecksum,  parsed.AdapterFWChecksum))  return Invalid("Inconsistent Alfen adapter firmware checksum!");
                    if (Differs(ref common.MeterId,            parsed.MeterId))            return Invalid("Inconsistent meter identification!");
                    if (Differs(ref common.ObisId,             parsed.ObisId))             return Invalid("Inconsistent OBIS code!");
                    if (Differs(ref common.Scalar,             parsed.Scalar))             return Invalid("Inconsistent measurement scalar!");
                    if (Differs(ref common.UID,                parsed.UID))                return Invalid("Inconsistent user identification!");

                    if (common.UnitEncoded == 0)
                        common.UnitEncoded = parsed.UnitEncoded;
                    else if (parsed.UnitEncoded != common.UnitEncoded)
                        return Invalid("Inconsistent unit (encoded) value!");

                    if (common.InternalSessionId == 0)
                        common.InternalSessionId = parsed.InternalSessionId;
                    else if (parsed.InternalSessionId != common.InternalSessionId)
                        return Invalid("Inconsistent internal charging session identification!");

                    #endregion

                    #region ..., and the readings have to move forward in time

                    if (previousTimestamp.Length > 0 &&
                        String.CompareOrdinal(previousTimestamp, parsed.Timestamp) > 0)
                    {
                        return new SessionCryptoResult(
                                   SessionVerificationResult.InconsistentTimestamps,
                                   i18n.GetMultilanguageText("Inconsistent timestamps!")
                               );
                    }

                    previousTimestamp = parsed.Timestamp;

                    #endregion

                    dataSets.Add(parsed);

                }

                if (dataSets.Count == 0)
                    return Invalid("UnknownOrInvalidChargingSessionFormat");

                return BuildChargeTransparencyRecord(common, dataSets, ContainerInfos);

            }
            catch (Exception exception)
            {
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSessionFormat,
                           i18n.GetMultilanguageText($"Exception occured: {exception.Message}"),
                           Exception: exception
                       );
            }

        }

        #endregion


        #region (private) ParseDataSet(DataSet, Signature)

        /// <summary>
        /// Read the 82 bytes an Alfen adapter signs.
        ///
        /// Little endian throughout, no separators, no length prefixes: the offsets
        /// below are the format. They are written out one per line, with the byte
        /// range in the comment, because this is the one place where an off-by-one
        /// silently turns a valid receipt into an invalid one.
        /// </summary>
        /// <param name="DataSet">The 82 byte data set.</param>
        /// <param name="Signature">The 48 byte signature over it.</param>
        private static AlfenDataSet ParseDataSet(Byte[]  DataSet,
                                                 Byte[]  Signature)
        {

            var data = DataSet.AsSpan();

            return new AlfenDataSet {

                       AdapterId          = Convert.ToHexStringLower(data[ 0..10]),                    //  0..10  0a 54 65 73 74 44 65 76 00 09
                       AdapterFWVersion   = Encoding.ASCII.GetString(data[10..14]),                    // 10..14  ASCII "v014"
                       AdapterFWChecksum  = Convert.ToHexStringLower(data[14..16]),                    // 14..16  b9 79
                       MeterId            = Convert.ToHexStringLower(data[16..26]),                    // 16..26  0a 01 44 5a 47 00 33 00 25 02
                       MeterStatus        = ReverseHex             (data[26..28]),                     // 26..28
                       AdapterStatus      = ReverseHex             (data[28..30]),                     // 28..30
                       SecondIndex        = BinaryPrimitives.ReadInt32LittleEndian(data[30..34]),      // 30..34  seconds since the adapter was started
                       Timestamp          = ChargyLib.UnixTimestampToISO8601(
                                                BinaryPrimitives.ReadInt32LittleEndian(data[34..38])), // 34..38
                       ObisId             = Convert.ToHexStringLower(data[38..44]),                    // 38..44  01 00 01 08 00 ff
                       UnitEncoded        = data[44],                                                  // 44..45  1e => 30 => Wh
                       Scalar             = Convert.ToHexStringLower(data[45..46]),                    // 45..46
                       Value              = BinaryPrimitives.ReadInt64LittleEndian(data[46..54]),      // 46..54  the meter reading itself
                       UID                = TrimAtNul(Encoding.ASCII.GetString(data[54..74])),         // 54..74  the token the driver authorized with
                       InternalSessionId  = BinaryPrimitives.ReadInt32LittleEndian(data[74..78]),      // 74..78
                       Paging             = BinaryPrimitives.ReadInt32LittleEndian(data[78..82]),      // 78..82
                       Signature          = Convert.ToHexStringLower(Signature)

                   };

        }

        #endregion

        #region (private) BuildChargeTransparencyRecord(Common, DataSets, ContainerInfos)

        /// <summary>
        /// Turn the signed data sets into a charge transparency record.
        /// </summary>
        /// <param name="Common">The values shared by every data set.</param>
        /// <param name="DataSets">The signed data sets.</param>
        /// <param name="ContainerInfos">What the surrounding container knew, if anything.</param>
        private static ChargeTransparencyRecord BuildChargeTransparencyRecord(AlfenCommonValues     Common,
                                                                              List<AlfenDataSet>    DataSets,
                                                                              ContainerInfos?       ContainerInfos)
        {

            var firstDataSet       = DataSets[0];
            var lastDataSet        = DataSets[^1];

            // A signed Alfen value says nothing about where it was produced, so
            // without a container these fall back to Chargy's own placeholders —
            // and an EV driver is shown a name that is honestly generic rather
            // than an invented one.
            var evseId             = ContainerInfos?.FirstEVSEId            ?? "DE*GEF*EVSE*CHARGY*1";
            var chargingStationId  = ContainerInfos?.FirstChargingStationId ?? "DE*GEF*STATION*CHARGY*1";
            var chargingSessionId  = ContainerInfos?.ChargingSessionId      ?? Common.InternalSessionId.ToString();

            #region The energy meter and its public key

            var energyMeter = new EnergyMeter(
                                  Common.MeterId,
                                  Firmware:         new Firmware(
                                                        Version:   Common.AdapterFWVersion,
                                                        Checksum:  Common.AdapterFWChecksum
                                                    ),
                                  SignatureFormat:  MeterSignatureFormat,
                                  PublicKeys:       [
                                                        new PublicKey(
                                                            Common.PublicKey,
                                                            new OIDInfo(Common.PublicKeyFormat),
                                                            Format:    "DER",
                                                            Encoding:  "base32"
                                                        )
                                                    ]
                              );

            #endregion

            #region The measurement and its values

            var measurement = new AlfenMeasurement(
                                  Common.MeterId,
                                  ChargyLib.OBIS2MeasurementName(ChargyLib.ParseOBIS(Common.ObisId)),
                                  ChargyLib.ParseOBIS(Common.ObisId),
                                  ParseScalar(Common.Scalar),
                                  Common.AdapterId,
                                  Common.AdapterFWVersion,
                                  Common.AdapterFWChecksum,
                                  Values:          DataSets.Select(dataSet => new MeasurementValue(
                                                                                  dataSet.Timestamp,
                                                                                  dataSet.Value,
                                                                                  Signatures:     [
                                                                                                      new SignatureRS(
                                                                                                          dataSet.Signature[..48],
                                                                                                          dataSet.Signature[48..]
                                                                                                      )
                                                                                                  ],
                                                                                  StatusMeter:    dataSet.MeterStatus,
                                                                                  StatusAdapter:  dataSet.AdapterStatus,
                                                                                  SecondsIndex:   dataSet.SecondIndex,
                                                                                  PaginationId:   dataSet.Paging.ToString()
                                                                              )),
                                  Context:         [ MeasurementContext ],
                                  UnitEncoded:     Common.UnitEncoded,
                                  SignatureInfos:  new SignatureInfos(
                                                       Hash:       CryptoHashAlgorithm.SHA256,
                                                       Algorithm:  CryptoAlgorithm.ECC,
                                                       Curve:      ECCurve.secp192r1,
                                                       Format:     SignatureFormat.RS,
                                                       Encoding:   DataEncoding.Base32
                                                   )
                              );

            #endregion

            var chargingSession = new ChargingSession(
                                      chargingSessionId,
                                      Context:            [ SessionContext ],
                                      Begin:              firstDataSet.Timestamp,
                                      End:                lastDataSet.Timestamp,
                                      EVSEId:             evseId,
                                      EnergyMeterId:      Common.MeterId,
                                      InternalSessionId:  Common.InternalSessionId.ToString(),
                                      Measurements:       [ measurement ]
                                  ) {
                                      AuthorizationStart = new Authorization(Common.UID)
                                  };

            var chargingStation = new ChargingStation(
                                      chargingStationId,
                                      EVSEs: [
                                          new EVSE(
                                              evseId,
                                              EnergyMeters: [ energyMeter ]
                                          )
                                      ]
                                  );

            var record = new ChargeTransparencyRecord(
                             chargingSessionId,
                             [ "https://open.charging.cloud/contexts/CTR+json" ],
                             firstDataSet.Timestamp,
                             lastDataSet.Timestamp,
                             I18NString.Create(Languages.de, "Alle Ladevorgänge").
                                              Set(Languages.en, "All charging sessions"),
                             Certainty:  1,
                             Status:     SessionVerificationResult.Unvalidated
                         );

            record.AddChargingStation(chargingStation);
            record.AddChargingSession(chargingSession);

            // Everything the container found questionable travels with the record,
            // so that an EV driver is told about it rather than only the software.
            foreach (var warning in ContainerInfos?.Warnings ?? [])
                record.AddWarning(warning);

            return record;

        }

        #endregion


        #region (private) Invalid              (MessageKey)

        /// <summary>
        /// Report that the data is not a valid Alfen charging session.
        /// </summary>
        /// <param name="MessageKey">The i18n key of the reason.</param>
        private SessionCryptoResult Invalid(String MessageKey)

            => new (
                   SessionVerificationResult.InvalidSessionFormat,
                   i18n.GetMultilanguageText(MessageKey)
               );

        #endregion

        #region (private, static) Differs      (Common, Value)

        /// <summary>
        /// Remember the first value seen, and report whether a later one differs.
        /// </summary>
        /// <param name="Common">The value seen so far.</param>
        /// <param name="Value">The value of the current data set.</param>
        private static Boolean Differs(ref String  Common,
                                       String      Value)
        {

            if (Common.Length == 0)
            {
                Common = Value;
                return false;
            }

            return Value != Common;

        }

        #endregion

        #region (private, static) TryFromBase32(Text)

        /// <summary>
        /// Decode base32 as RFC 4648, or return null when the text is not base32.
        /// </summary>
        /// <param name="Text">A base32 encoded text.</param>
        private static Byte[]? TryFromBase32(String Text)
        {

            try
            {
                return Text.FromBASE32();
            }
            catch (Exception)
            {
                return null;
            }

        }

        #endregion

        #region (private, static) ReverseHex   (Bytes)

        /// <summary>
        /// The hexadecimal form of the given bytes, least significant byte first.
        ///
        /// The status words are little endian in the data set but are read as
        /// numbers, so their bytes are reversed before they become text.
        /// </summary>
        /// <param name="Bytes">Some bytes.</param>
        private static String ReverseHex(ReadOnlySpan<Byte> Bytes)
        {

            Span<Byte> reversed = stackalloc Byte[Bytes.Length];

            Bytes.CopyTo(reversed);
            reversed.Reverse();

            return Convert.ToHexStringLower(reversed);

        }

        #endregion

        #region (private, static) TrimAtNul    (Text)

        /// <summary>
        /// Cut a fixed-width text field at its first NUL byte.
        /// </summary>
        /// <param name="Text">A fixed-width text field.</param>
        private static String TrimAtNul(String Text)
        {

            var nul = Text.IndexOf('\0');

            return nul >= 0
                       ? Text[..nul]
                       : Text;

        }

        #endregion

        #region (private, static) ParseScalar  (Scalar)

        /// <summary>
        /// The scale of the measured values, from its hexadecimal form.
        /// </summary>
        /// <param name="Scalar">The scale, hexadecimal.</param>
        private static Int32 ParseScalar(String Scalar)

            => Int32.TryParse(Scalar, System.Globalization.NumberStyles.HexNumber,
                              System.Globalization.CultureInfo.InvariantCulture, out var scale)
                   ? (SByte) scale
                   : 0;

        #endregion


    }


    /// <summary>
    /// The values every signed meter value of one charging session shares.
    ///
    /// They are checked rather than assumed: a set of readings that disagree about
    /// which meter produced them is not a charging session, it is a collection of
    /// unrelated receipts.
    /// </summary>
    internal class AlfenCommonValues
    {
        public String  PublicKey          = "";
        public String  PublicKeyFormat    = "";
        public String  AdapterId          = "";
        public String  AdapterFWVersion   = "";
        public String  AdapterFWChecksum  = "";
        public String  MeterId            = "";
        public String  ObisId             = "";
        public UInt16  UnitEncoded;
        public String  Scalar             = "";
        public String  UID                = "";
        public Int32   InternalSessionId;
    }


    /// <summary>
    /// One signed Alfen meter reading.
    /// </summary>
    internal class AlfenDataSet
    {
        public String   AdapterId          = "";
        public String   AdapterFWVersion   = "";
        public String   AdapterFWChecksum  = "";
        public String   MeterId            = "";
        public String   MeterStatus        = "";
        public String   AdapterStatus      = "";
        public Int32    SecondIndex;
        public String   Timestamp          = "";
        public String   ObisId             = "";
        public UInt16   UnitEncoded;
        public String   Scalar             = "";
        public Decimal  Value;
        public String   UID                = "";
        public Int32    InternalSessionId;
        public Int32    Paging;
        public String   Signature          = "";
    }

}
