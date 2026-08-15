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
using System.Text.RegularExpressions;
using System.Xml.Linq;

#endregion

namespace cloud.charging.open.chargy.Formats.Mennekes
{

    /// <summary>
    /// Something about a Mennekes charging process does not hold up.
    /// </summary>
    /// <param name="Message">What went wrong.</param>
    public class MennekesValidationException(String Message) : Exception(Message)
    { }


    /// <summary>
    /// One signed reading of a Mennekes EDL40 charging station.
    /// </summary>
    public class MennekesMeasurement
    {

        #region Properties

        /// <summary>When the driver authorized, if this reading states it separately.</summary>
        public String?  TimestampCustomerIdent    { get; init; }

        /// <summary>When the value was measured, in the meter's own local time.</summary>
        public required String  Timestamp         { get; init; }

        /// <summary>The signature over the reading, hexadecimal.</summary>
        public required String  Signature         { get; init; }

        /// <summary>The event counter of the meter.</summary>
        public required Int64   EventCounter      { get; init; }

        /// <summary>The status word of the meter.</summary>
        public required Int64   MeterStatus       { get; init; }

        /// <summary>The reading itself.</summary>
        public required Int64   Value             { get; init; }

        /// <summary>The power of ten the reading is scaled by.</summary>
        public required Int32   Scaler            { get; init; }

        /// <summary>The pagination counter of the meter.</summary>
        public required Int64   Pagination        { get; init; }

        /// <summary>How many seconds the meter has been in operation.</summary>
        public required Int64   SecondIndex       { get; init; }

        #endregion

        #region SignatureRS

        /// <summary>
        /// The signature as its two integers.
        ///
        /// A Mennekes signature is 48 bytes — r and s, 24 each on secp192r1 — or
        /// 50, where the two extra bytes are not part of the signature at all:
        /// they belong inside the signed block, and only the first 48 are checked.
        /// </summary>
        public SignatureRS SignatureRS
        {
            get
            {

                var signature = Signature.Length == 100
                                    ? Signature[..96]
                                    : Signature;

                return new SignatureRS(
                           signature.Length >= 96 ? signature[..48]   : signature,
                           signature.Length >= 96 ? signature[48..96] : "",
                           Value:      signature,
                           Algorithm:  CryptoAlgorithm.ECC.AsText(),
                           Format:     SignatureFormat.RS.AsText()
                       );

            }
        }

        #endregion

        #region (static) Parse(Element)

        /// <summary>
        /// Read one signed reading out of its XML element.
        /// </summary>
        /// <param name="Element">A "MeasurementStart" or "MeasurementEnd" element.</param>
        /// <exception cref="MennekesValidationException">When a field is missing or not a number.</exception>
        public static MennekesMeasurement Parse(XElement Element)

            => new () {
                   TimestampCustomerIdent  = MennekesChargingProcess.OptionalText(Element, "TimestampCustomerIdent"),
                   Timestamp               = MennekesChargingProcess.RequiredText(Element, "Timestamp"),
                   Signature               = ChargyLib.CleanHex(MennekesChargingProcess.RequiredText(Element, "Signature")),
                   EventCounter            = MennekesChargingProcess.RequiredNumber(Element, "EventCounter"),
                   MeterStatus             = MennekesChargingProcess.RequiredNumber(Element, "MeterStatus"),
                   Value                   = MennekesChargingProcess.RequiredNumber(Element, "Value"),
                   Scaler                  = (Int32) MennekesChargingProcess.RequiredNumber(Element, "Scaler"),
                   Pagination              = MennekesChargingProcess.RequiredNumber(Element, "Pagination"),
                   SecondIndex             = MennekesChargingProcess.RequiredNumber(Element, "SecondIndex")
               };

        #endregion

    }


    /// <summary>
    /// One charging process of a Mennekes EDL40 charging station: a start reading,
    /// an end reading, and everything the station knows about both.
    /// </summary>
    public partial class MennekesChargingProcess
    {

        #region Data

        /// <summary>The XML namespace a Mennekes document declares.</summary>
        public const String XMLNamespace  = "http://www.mennekes.de/Mennekes.EdlVerification.xsd";

        /// <summary>The OBIS code of what a Mennekes EDL40 meter reports.</summary>
        public const String OBIS          = "1-0:1.17.0*255";

        /// <summary>The length of the block a Mennekes EDL40 meter signs.</summary>
        public const Int32  SignedDataLength = 320;

        #endregion

        #region Properties

        /// <summary>The identification of the energy meter.</summary>
        public required String                MeterId                 { get; init; }

        /// <summary>The public key of the energy meter, hexadecimal.</summary>
        public required String                PublicKey               { get; init; }

        /// <summary>The metering point, which is what an EVSE is called here.</summary>
        public          String?               MeteringPoint           { get; init; }

        /// <summary>Where the charging station stands.</summary>
        public          Address?              SiteAddress             { get; init; }

        /// <summary>The token the driver authorized with, hexadecimal.</summary>
        public required String                CustomerIdent           { get; init; }

        /// <summary>When the driver authorized.</summary>
        public required String                TimestampCustomerIdent  { get; init; }

        /// <summary>The reading at the start of the charging process.</summary>
        public required MennekesMeasurement   MeasurementStart        { get; init; }

        /// <summary>The reading at the end of the charging process.</summary>
        public required MennekesMeasurement   MeasurementEnd          { get; init; }

        #endregion


        #region (static) ExtractFrom(Document)

        /// <summary>
        /// Every charging process an XML document holds.
        ///
        /// A document is either a single charging process or a "Billing" wrapper
        /// around several, and it may or may not declare the Mennekes namespace —
        /// so the elements are found by their local name.
        /// </summary>
        /// <param name="Document">An XML document.</param>
        public static IEnumerable<MennekesChargingProcess> ExtractFrom(XDocument Document)
        {

            var root = Document.Root;

            if (root is null)
                return [];

            if (root.Name.LocalName == "ChargingProcess")
                return [ Parse(root) ];

            if (root.Name.LocalName == "Billing")
                return root.Descendants().
                            Where (element => element.Name.LocalName == "ChargingProcess").
                            Select(Parse).
                            ToArray();

            return [];

        }

        #endregion

        #region (static) Parse(Element)

        /// <summary>
        /// Read one charging process out of its XML element.
        /// </summary>
        /// <param name="Element">A "ChargingProcess" element.</param>
        /// <exception cref="MennekesValidationException">When a field is missing or not a number.</exception>
        public static MennekesChargingProcess Parse(XElement Element)
        {

            var siteAddress = Child(Element, "SiteAddress");

            return new MennekesChargingProcess {

                       MeterId                 = ChargyLib.CleanHex(RequiredText(Element, "ServerId")),
                       PublicKey               = ChargyLib.CleanHex(RequiredText(Element, "PublicKey")),
                       MeteringPoint           = OptionalText(Element, "MeteringPoint"),

                       SiteAddress             = siteAddress is not null
                                                     ? new Address(
                                                           Street:      OptionalText(siteAddress, "Street"),
                                                           PostalCode:  OptionalText(siteAddress, "ZipCode") ?? "",
                                                           City:        OptionalText(siteAddress, "Town")    ?? "",
                                                           Country:     "DE",
                                                           Context:     [ "https://open.charging.cloud/contexts/address+json" ]
                                                       )
                                                     : null,

                       CustomerIdent           = ChargyLib.CleanHex(RequiredText(Element, "CustomerIdent")),
                       TimestampCustomerIdent  = RequiredText(Element, "TimestampCustomerIdent"),

                       MeasurementStart        = MennekesMeasurement.Parse(RequiredChild(Element, "MeasurementStart")),
                       MeasurementEnd          = MennekesMeasurement.Parse(RequiredChild(Element, "MeasurementEnd"))

                   };

        }

        #endregion

        #region BuildSignedData(Measurement)

        /// <summary>
        /// Rebuild the 320 bytes a Mennekes EDL40 meter signed for one reading.
        ///
        /// Little endian throughout for the numbers, big endian for the event
        /// counter, and the OBIS code written out as the six bytes the meter uses
        /// rather than derived from the measurement — because those six bytes are
        /// what it signed, whatever the record says the measurement is called.
        ///
        /// The tail past byte 173 stays zero. It is part of the signed block all
        /// the same, which is why the buffer is 320 bytes rather than 173.
        /// </summary>
        /// <param name="Measurement">A signed reading of this charging process.</param>
        /// <exception cref="MennekesValidationException">When the signature or the token has an impossible length.</exception>
        public Byte[] BuildSignedData(MennekesMeasurement Measurement)
        {

            var signatureBytes = Convert.FromHexString(ChargyLib.CleanHex(Measurement.Signature));

            if (signatureBytes.Length != 48 && signatureBytes.Length != 50)
                throw new MennekesValidationException("Mennekes signatures must contain 48 or 50 bytes!");

            var customerIdent = Convert.FromHexString(ChargyLib.CleanHex(CustomerIdent));

            if (customerIdent.Length > 128)
                throw new MennekesValidationException("Mennekes CustomerIdent must not exceed 128 bytes!");

            var signedData = new Byte[SignedDataLength];
            var buffer     = signedData.AsSpan();

            Write(buffer, Convert.FromHexString(MeterId),                          0, 10);   //   0.. 10  the meter
            Write(buffer, LocalEpochBytes(Measurement.Timestamp),                 10,  4);   //  10.. 14  when it was read, meter local time
            buffer[14] = (Byte) (Measurement.MeterStatus & 0xFF);                            //  14.. 15
            Write(buffer, LittleEndian(Measurement.SecondIndex, 4),               15,  4);   //  15.. 19
            Write(buffer, LittleEndian(Measurement.Pagination,  4),               19,  4);   //  19.. 23
            Write(buffer, [ 0x01, 0x00, 0x01, 0x11, 0x00, 0xFF ],                 23,  6);   //  23.. 29  the OBIS code, as the meter writes it
            buffer[29] = 30;                                                                 //  29.. 30  1e => 30 => Wh
            buffer[30] = (Byte) (Measurement.Scaler & 0xFF);                                 //  30.. 31
            Write(buffer, LittleEndian(Measurement.Value, 8),                     31,  8);   //  31.. 39  the reading itself

            // A 50 byte signature carries its own two bytes for this field. A 48
            // byte one does not, and the event counter goes here instead.
            Write(buffer,
                  signatureBytes.Length > 48
                      ? signatureBytes[48..50]
                      : BigEndian(Measurement.EventCounter, 2),                   39,  2);   //  39.. 41

            Write(buffer, customerIdent,                                          41, customerIdent.Length);   //  41..169  the token the driver authorized with
            Write(buffer, LocalEpochBytes(Measurement.TimestampCustomerIdent
                                              ?? TimestampCustomerIdent),        169,  4);   // 169..173  when they authorized

            return signedData;

        }

        #endregion

        #region (static) LocalEpochSeconds(Timestamp)

        /// <summary>
        /// A timestamp as the meter's own clock read it, in seconds.
        ///
        /// The meter signs the time it displays, not the UTC instant behind it, so
        /// the stated offset is added rather than applied. A timestamp without an
        /// offset is already local by that reading and is taken as it is.
        /// </summary>
        /// <param name="Timestamp">An ISO 8601 timestamp.</param>
        /// <exception cref="MennekesValidationException">When the timestamp cannot be parsed.</exception>
        public static Int64 LocalEpochSeconds(String Timestamp)
        {

            if (!DateTimeOffset.TryParse(Timestamp.Trim(),
                                         CultureInfo.InvariantCulture,
                                         DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                         out var instant))
            {
                throw new MennekesValidationException($"Invalid Mennekes timestamp: {Timestamp}");
            }

            var offset = UTCOffsetRegex().Match(Timestamp.Trim());

            if (!offset.Success)
                return instant.ToUnixTimeSeconds();

            var offsetSeconds = (offset.Groups[1].Value == "+" ? 1 : -1) *
                                (Int64.Parse(offset.Groups[2].Value, CultureInfo.InvariantCulture) * 3600 +
                                 Int64.Parse(offset.Groups[3].Value, CultureInfo.InvariantCulture) * 60);

            return instant.ToUnixTimeSeconds() + offsetSeconds;

        }

        #endregion


        #region (internal, static) XML helpers

        /// <summary>The direct child with the given local name.</summary>
        internal static XElement? Child(XElement  Parent,
                                        String    LocalName)

            => Parent.Elements().FirstOrDefault(element => element.Name.LocalName == LocalName);

        /// <summary>The direct child with the given local name, or a failure naming it.</summary>
        internal static XElement RequiredChild(XElement  Parent,
                                               String    LocalName)

            => Child(Parent, LocalName)
                   ?? throw new MennekesValidationException($"Missing Mennekes XML element: {LocalName}");

        /// <summary>The trimmed text of the direct child with the given local name.</summary>
        internal static String? OptionalText(XElement  Parent,
                                             String    LocalName)
        {

            var text = Child(Parent, LocalName)?.Value.Trim();

            return text is not null && text.Length > 0
                       ? text
                       : null;

        }

        /// <summary>The trimmed text of the direct child with the given local name, or a failure naming it.</summary>
        internal static String RequiredText(XElement  Parent,
                                            String    LocalName)

            => OptionalText(Parent, LocalName)
                   ?? throw new MennekesValidationException($"Missing Mennekes XML text: {LocalName}");

        /// <summary>The number in the direct child with the given local name, or a failure naming it.</summary>
        internal static Int64 RequiredNumber(XElement  Parent,
                                             String    LocalName)

            => Int64.TryParse(RequiredText(Parent, LocalName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                   ? value
                   : throw new MennekesValidationException($"Invalid Mennekes numeric XML text: {LocalName}");

        #endregion

        #region (private, static) Byte helpers

        /// <summary>Copy a field into the signed block, or fail rather than leave it half written.</summary>
        private static void Write(Span<Byte>          Buffer,
                                  ReadOnlySpan<Byte>  Source,
                                  Int32               Offset,
                                  Int32               Length)
        {

            if (Source.Length < Length)
                throw new MennekesValidationException("Not enough bytes for a Mennekes signature field!");

            Source[..Length].CopyTo(Buffer[Offset..]);

        }

        /// <summary>The given value, least significant byte first.</summary>
        private static Byte[] LittleEndian(Int64  Value,
                                           Int32  Length)
        {

            var bytes = BigEndian(Value, Length);

            Array.Reverse(bytes);

            return bytes;

        }

        /// <summary>The given value, most significant byte first, wrapping a negative value.</summary>
        private static Byte[] BigEndian(Int64  Value,
                                        Int32  Length)
        {

            var bytes      = new Byte[Length];
            var remaining  = unchecked((UInt64) Value);

            for (var index = Length - 1; index >= 0; index--)
            {
                bytes[index]  = (Byte) (remaining & 0xFF);
                remaining   >>= 8;
            }

            return bytes;

        }

        /// <summary>The meter's own clock, as four little endian bytes.</summary>
        private static Byte[] LocalEpochBytes(String Timestamp)

            => LittleEndian(LocalEpochSeconds(Timestamp), 4);

        #endregion

        #region (private) Regular expressions

        [GeneratedRegex(@"([+-])(\d{2}):(\d{2})$")]
        private static partial Regex UTCOffsetRegex();

        #endregion

    }

}
