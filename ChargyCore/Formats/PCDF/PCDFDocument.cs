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
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.Formats.PCDF
{

    /// <summary>
    /// A PCDF document is not shaped the way it claims to be.
    /// </summary>
    /// <param name="Code">A stable, language-neutral code for the reason.</param>
    /// <param name="Message">What went wrong.</param>
    /// <param name="Field">The field the reason is about, if any.</param>
    public class PCDFParseException(String   Code,
                                    String   Message,
                                    String?  Field = null) : Exception(Message)
    {

        /// <summary>A stable, language-neutral code for the reason.</summary>
        public String   Code     { get; } = Code;

        /// <summary>The field the reason is about, if any.</summary>
        public String?  Field    { get; } = Field;

    }


    /// <summary>
    /// A PCDF document is shaped correctly but says something impossible.
    /// </summary>
    /// <param name="Errors">Everything found wrong, collected rather than reported one at a time.</param>
    public class PCDFValidationException(IEnumerable<String> Errors)

        : Exception(String.Join("; ", Errors))

    {

        /// <summary>Everything found wrong.</summary>
        public IReadOnlyList<String> Errors { get; } = Errors.ToArray();

    }


    /// <summary>
    /// The parts of a PCDF document, before anything has been made of them.
    /// </summary>
    /// <param name="Raw">The document, unwrapped and with everything before the prefix removed.</param>
    /// <param name="Fields">The fourteen fields, by name.</param>
    /// <param name="SignedPayload">Everything up to the signature — which is exactly what was signed.</param>
    public class PCDFRawDocument(String                            Raw,
                                 IReadOnlyDictionary<String, String>  Fields,
                                 String                            SignedPayload)
    {

        /// <summary>The document, unwrapped and with everything before the prefix removed.</summary>
        public String                               Raw              { get; } = Raw;

        /// <summary>The fourteen fields, by name.</summary>
        public IReadOnlyDictionary<String, String>  Fields           { get; } = Fields;

        /// <summary>Everything up to the signature.</summary>
        public String                               SignedPayload    { get; } = SignedPayload;

    }


    /// <summary>
    /// Who authorized a PCDF charging session, and under which transaction.
    /// </summary>
    /// <param name="IdTag">The token the driver authorized with.</param>
    /// <param name="IdTagType">What kind of token it is, "1" through "5".</param>
    /// <param name="TransactionId">The identification of the transaction.</param>
    public class PCDFSessionInfo(String  IdTag,
                                 String  IdTagType,
                                 String  TransactionId)
    {

        /// <summary>The token the driver authorized with.</summary>
        public String  IdTag            { get; } = IdTag;

        /// <summary>What kind of token it is.</summary>
        public String  IdTagType        { get; } = IdTagType;

        /// <summary>The identification of the transaction.</summary>
        public String  TransactionId    { get; } = TransactionId;

    }


    /// <summary>
    /// One PCDF document — the Porsche Charging Data Format — read, checked and
    /// with its signature verified.
    ///
    /// A PCDF document is a single line of parenthesised fields in a fixed order,
    /// and what the meter signed is that line up to but excluding the signature —
    /// its text, not a reassembled buffer. Which makes verification unusually
    /// direct: hash the payload, check the DER signature against the key the
    /// document carries. There is nothing to get subtly wrong in the layout,
    /// because the layout *is* the document.
    ///
    /// What can go wrong instead is everything the fields claim: a stop time
    /// before the start, a duration with 74 minutes in it, a document saying the
    /// meter reported a billing error. Those are checked here, and all of them are
    /// collected rather than reported one at a time — somebody holding an
    /// unbillable receipt is better served by the whole list than by its first
    /// entry.
    /// </summary>
    public partial class PCDFDocument
    {

        #region Data

        /// <summary>The OBIS-like prefix every PCDF document opens with.</summary>
        public const String Prefix = "128.8.0";

        /// <summary>
        /// The fields, in the order they have to appear.
        ///
        /// The order is not a convention: the signature covers the payload as
        /// text, so a document whose fields were reordered is a different document
        /// and would not verify anyway.
        /// </summary>
        public static readonly IReadOnlyList<String> FieldOrder = [
            "ST",   // start time
            "CT",   // stop time
            "CD",   // charging duration
            "TV",   // time valid
            "BV",   // billing valid
            "CSC",  // charging session counter
            "SP",   // stop present
            "RV",   // the meter reading
            "SI",   // who authorized, and the transaction
            "CS",   // the software checksum
            "HW",   // the hardware serial number
            "DT",   // the kind of DC meter
            "PK",   // the public key
            "SG"    // the signature
        ];

        /// <summary>
        /// The SubjectPublicKeyInfo header a DER encoded P-256 key starts with.
        ///
        /// A PCDF document may carry either the bare point or the whole DER
        /// structure, and the two describe the same key.
        /// </summary>
        private const String SPKIP256Prefix = "3059301306072a8648ce3d020106082a8648ce3d030107034200";

        #endregion

        #region Properties

        /// <summary>The document as it arrived, unwrapped.</summary>
        public required String                               Raw                       { get; init; }

        /// <summary>The fourteen fields, by name.</summary>
        public required IReadOnlyDictionary<String, String>  Fields                    { get; init; }

        /// <summary>Exactly what the meter signed.</summary>
        public required String                               SignedPayload             { get; init; }

        /// <summary>The hash of the signed payload, hexadecimal.</summary>
        public required String                               HashValue                 { get; init; }

        /// <summary>When the charging session started.</summary>
        public required String                               StartTime                 { get; init; }

        /// <summary>When it stopped.</summary>
        public required String                               StopTime                  { get; init; }

        /// <summary>How long it lasted, in seconds.</summary>
        public required Int64                                DurationSeconds           { get; init; }

        /// <summary>Whether the meter considered its clock to be right.</summary>
        public required Boolean                              TimeValid                 { get; init; }

        /// <summary>Whether the meter considered the session billable.</summary>
        public required Boolean                              BillingValid              { get; init; }

        /// <summary>How many charging sessions the meter has counted.</summary>
        public required Int64                                ChargingSessionCounter    { get; init; }

        /// <summary>Whether the closing reading is present.</summary>
        public required Boolean                              StopPresent               { get; init; }

        /// <summary>The meter reading.</summary>
        public required Decimal                              ReadingValue              { get; init; }

        /// <summary>The unit of the reading.</summary>
        public required String                               ReadingUnit               { get; init; }

        /// <summary>Who authorized, and under which transaction.</summary>
        public required PCDFSessionInfo                      Session                   { get; init; }

        /// <summary>The checksum of the meter's software.</summary>
        public required String                               SoftwareChecksum          { get; init; }

        /// <summary>The serial number of the meter.</summary>
        public required String                               HardwareSerial            { get; init; }

        /// <summary>Which kind of DC meter produced the document.</summary>
        public required Int64                                DCMeterType               { get; init; }

        /// <summary>The public key of the meter, as a bare point, hexadecimal.</summary>
        public required String                               PublicKeyHEX              { get; init; }

        /// <summary>The signature, DER encoded, hexadecimal.</summary>
        public required String                               SignatureHEX              { get; init; }

        /// <summary>The signature, as its two integers.</summary>
        public required SignatureRS                          Signature                 { get; init; }

        /// <summary>What checking the signature concluded.</summary>
        public required VerificationResult                   ValidationStatus          { get; init; }

        /// <summary>The hash algorithm, which is SHA-256.</summary>
        public String                                        HashAlgorithm
            => "SHA256";

        #endregion


        #region (static) IsPCDFText     (Text)

        /// <summary>
        /// Whether the given text is a PCDF document.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        public static Boolean IsPCDFText(String? Text)

            => Text is not null &&
               StripControlCharacters(Unquote(Text)).StartsWith(Prefix, StringComparison.Ordinal);

        #endregion

        #region (static) Unquote        (Text)

        /// <summary>
        /// Take a PCDF document out of the quotation marks somebody wrapped it in.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        public static String Unquote(String Text)
        {

            var trimmed = Text.Trim();

            return trimmed.Length >= 2 &&
                   trimmed.StartsWith('"') &&
                   trimmed.EndsWith('"')
                       ? trimmed[1..^1]
                       : trimmed;

        }

        #endregion

        #region (static) StripControlCharacters(Text)

        /// <summary>
        /// Take off the STX and ETX bytes a serial line wraps a document in.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        public static String StripControlCharacters(String Text)
        {

            var cleaned = Text.Trim();

            if (cleaned.Length > 0 && cleaned[0]  == '')
                cleaned = cleaned[1..];

            if (cleaned.Length > 0 && cleaned[^1] == '')
                cleaned = cleaned[..^1];

            return cleaned.Trim();

        }

        #endregion

        #region (static) Parse          (Text)

        /// <summary>
        /// Take a PCDF document apart into its fields.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        /// <exception cref="PCDFParseException">When the text is not a PCDF document.</exception>
        public static PCDFRawDocument Parse(String Text)
        {

            var cleaned      = StripControlCharacters(Unquote(Text));
            var prefixIndex  = cleaned.IndexOf(Prefix, StringComparison.Ordinal);

            if (prefixIndex < 0)
                throw new PCDFParseException("MISSING_PREFIX", "Charging data is not valid");

            cleaned = cleaned[prefixIndex..];

            var signatureIndex = cleaned.IndexOf("(SG:", StringComparison.Ordinal);

            if (signatureIndex < 0)
                throw new PCDFParseException("MISSING_SIGNATURE", "No signature present in data tuple", "SG");

            var match = DocumentRegex().Match(cleaned);

            #region A document that does not match is worth explaining, not just rejecting

            if (!match.Success)
            {

                var found = FieldRegex().Matches(cleaned).
                                         Select(fieldMatch => fieldMatch.Groups[1].Value).
                                         Where (FieldOrder.Contains).
                                         ToHashSet(StringComparer.Ordinal);

                var missing = FieldOrder.Where(field => !found.Contains(field)).ToArray();

                if (missing.Length > 0)
                    throw new PCDFParseException(
                              "MISSING_FIELDS",
                              $"Missing fields in the data tuple: {String.Join(", ", missing)}",
                              missing[0]
                          );

                throw new PCDFParseException("INVALID_FIELD_ORDER", "Charging data is not valid");

            }

            #endregion

            var fields = new Dictionary<String, String>(StringComparer.Ordinal);

            for (var i = 0; i < FieldOrder.Count; i++)
            {

                var value = match.Groups[i + 1].Value;

                if (value.Length == 0)
                    throw new PCDFParseException("MISSING_FIELDS", "Missing fields in the data tuple", FieldOrder[i]);

                fields[FieldOrder[i]] = value;

            }

            return new PCDFRawDocument(
                       cleaned,
                       fields,
                       cleaned[..signatureIndex]
                   );

        }

        #endregion

        #region (static) Read           (Text)

        /// <summary>
        /// Read a PCDF document, check what it claims, and verify its signature.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        /// <exception cref="PCDFParseException">When the text is not a PCDF document.</exception>
        /// <exception cref="PCDFValidationException">When the document says something impossible.</exception>
        public static PCDFDocument Read(String Text)
        {

            var parsed = Parse(Text);

            return Validate(parsed);

        }

        #endregion

        #region (static) Validate       (RawDocument)

        /// <summary>
        /// Check what a parsed document claims, and verify its signature.
        ///
        /// Everything wrong is collected before anything is thrown, because these
        /// faults come in groups — a meter that lost its clock usually reports
        /// several impossible things at once, and the first of them is rarely the
        /// informative one.
        /// </summary>
        /// <param name="RawDocument">A parsed PCDF document.</param>
        /// <exception cref="PCDFValidationException">When the document says something impossible.</exception>
        public static PCDFDocument Validate(PCDFRawDocument RawDocument)
        {

            var fields  = RawDocument.Fields;
            var errors  = new List<String>();

            var startTime               = ParseTimestamp(fields["ST"], "ST", errors);
            var stopTime                = ParseTimestamp(fields["CT"], "CT", errors);
            var durationSeconds         = ParseDuration (fields["CD"],       errors);
            var timeValid               = ParseFlag     (fields["TV"], "TV", errors);
            var billingValid            = ParseFlag     (fields["BV"], "BV", errors);
            var chargingSessionCounter  = ParseInteger  (fields["CSC"],"CSC",errors);
            var stopPresent             = ParseFlag     (fields["SP"], "SP", errors);
            var reading                 = ParseReading  (fields["RV"],       errors);
            var session                 = ParseSession  (fields["SI"],       errors);
            var dcMeterType             = ParseInteger  (fields["DT"], "DT", errors);

            if (startTime.HasValue && stopTime.HasValue && stopTime < startTime)
                errors.Add("Corrupt time information: CT must be greater than or equal to ST");

            // These two are the meter's own verdict on its work, and neither is a
            // formatting problem: the meter is saying the session cannot be billed.
            if (billingValid == false)
                errors.Add("Billing not possible. DCMeter error");

            if (stopPresent != true)
                errors.Add("Charge session does not include the last data");

            if (!ChecksumRegex().IsMatch(fields["CS"]))
                errors.Add("CS must be exactly 8 hex characters");

            if (fields["HW"].Length != 11)
                errors.Add("HW must be exactly 11 characters");

            String? publicKeyHEX = null;

            try
            {
                publicKeyHEX = NormalizePublicKeyHEX(fields["PK"]);
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
            }

            SignatureRS? signature = null;

            try
            {
                signature = ParseSignature(fields["SG"]);
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
            }

            if (errors.Count > 0                ||
                !startTime.HasValue             ||
                !stopTime.HasValue              ||
                !durationSeconds.HasValue       ||
                !timeValid.HasValue             ||
                !billingValid.HasValue          ||
                !chargingSessionCounter.HasValue||
                !stopPresent.HasValue           ||
                reading      is null            ||
                session      is null            ||
                !dcMeterType.HasValue           ||
                publicKeyHEX is null            ||
                signature    is null)
            {
                throw new PCDFValidationException(
                          errors.Count > 0
                              ? errors
                              : [ "Session information is invalid" ]
                      );
            }

            #region The signature covers the payload as text, so it is hashed as text

            var hash    = Convert.ToHexStringLower(
                              SHA256.HashData(Encoding.UTF8.GetBytes(RawDocument.SignedPayload))
                          );

            var status  = VerifySignature(hash, publicKeyHEX, signature);

            #endregion

            return new PCDFDocument {

                       Raw                     = RawDocument.Raw,
                       Fields                  = fields,
                       SignedPayload           = RawDocument.SignedPayload,
                       HashValue               = hash,

                       StartTime               = ToISO8601(startTime.Value),
                       StopTime                = ToISO8601(stopTime.Value),
                       DurationSeconds         = durationSeconds.Value,
                       TimeValid               = timeValid.Value,
                       BillingValid            = billingValid.Value,
                       ChargingSessionCounter  = chargingSessionCounter.Value,
                       StopPresent             = stopPresent.Value,
                       ReadingValue            = reading.Value.Value,
                       ReadingUnit             = reading.Value.Unit,
                       Session                 = session,
                       SoftwareChecksum        = fields["CS"].ToLowerInvariant(),
                       HardwareSerial          = fields["HW"],
                       DCMeterType             = dcMeterType.Value,

                       PublicKeyHEX            = publicKeyHEX,
                       SignatureHEX            = fields["SG"],
                       Signature               = signature,
                       ValidationStatus        = status

                   };

        }

        #endregion


        #region (static) NormalizePublicKeyHEX(PublicKeyHEX)

        /// <summary>
        /// The bare elliptic curve point of a PCDF public key.
        ///
        /// A document carries either the point itself or the whole DER structure
        /// around it. Both describe the same key, so the wrapper is taken off
        /// rather than treated as a different kind of key.
        /// </summary>
        /// <param name="PublicKeyHEX">A public key, hexadecimal.</param>
        /// <exception cref="Exception">When it is not a public key of this format.</exception>
        public static String NormalizePublicKeyHEX(String PublicKeyHEX)
        {

            var normalized  = WhitespaceRegex().Replace(PublicKeyHEX, "").ToLowerInvariant();

            var rawPoint    = normalized.StartsWith(SPKIP256Prefix, StringComparison.Ordinal)
                                  ? normalized[SPKIP256Prefix.Length..]
                                  : normalized;

            if (!LowerHexRegex().IsMatch(rawPoint))
                throw new Exception("Invalid public key encoding");

            if (rawPoint.Length != 130)
                throw new Exception("Invalid public key length");

            // 0x04 says the point is given by both of its coordinates. A compressed
            // point would be half as long and is not what this format uses.
            if (!rawPoint.StartsWith("04", StringComparison.Ordinal))
                throw new Exception("Invalid public key format");

            return rawPoint;

        }

        #endregion

        #region (static) ParseSignature (SignatureHEX)

        /// <summary>
        /// Take a DER encoded ECDSA signature apart into its two integers.
        /// </summary>
        /// <param name="SignatureHEX">A DER encoded signature, hexadecimal.</param>
        /// <exception cref="Exception">When it is not one.</exception>
        public static SignatureRS ParseSignature(String SignatureHEX)
        {

            var normalized = WhitespaceRegex().Replace(SignatureHEX, "").ToLowerInvariant();

            if (normalized.Length == 0 ||
                normalized.Length % 2 != 0 ||
                !LowerHexRegex().IsMatch(normalized))
            {
                throw new Exception("Invalid signature encoding");
            }

            var decoded = ECCurveVerifier.TryDecodeDERSignature(Convert.FromHexString(normalized))
                              ?? throw new Exception("Invalid signature");

            return new SignatureRS(
                       decoded.R,
                       decoded.S,
                       Value:      normalized,
                       Algorithm:  CryptoAlgorithm.ECC.AsText(),
                       Format:     SignatureFormat.RS.AsText()
                   );

        }

        #endregion

        #region (static) VerifySignature(HashValue, PublicKeyHEX, Signature)

        /// <summary>
        /// Check a PCDF signature over the hashed payload.
        /// </summary>
        /// <param name="HashValue">The SHA-256 hash of the signed payload, hexadecimal.</param>
        /// <param name="PublicKeyHEX">The public key of the meter, hexadecimal.</param>
        /// <param name="Signature">The signature, as its two integers.</param>
        public static VerificationResult VerifySignature(String       HashValue,
                                                         String       PublicKeyHEX,
                                                         SignatureRS  Signature)
        {

            try
            {

                var verificationKey = ECCurveVerifier.secp256r1.ParsePublicKey(PublicKeyHEX);

                if (verificationKey is null)
                    return VerificationResult.InvalidSignature;

                return verificationKey.Verify(HashValue, Signature.R, Signature.S)
                           ? VerificationResult.ValidSignature
                           : VerificationResult.InvalidSignature;

            }
            catch (Exception)
            {
                return VerificationResult.InvalidSignature;
            }

        }

        #endregion


        #region (private, static) Field readers

        /// <summary>
        /// A PCDF timestamp: two digits each of year, month, day, hour, minute and
        /// second, in UTC, with the year counted from 2000.
        /// </summary>
        private static DateTimeOffset? ParseTimestamp(String        Value,
                                                      String        Field,
                                                      List<String>  Errors)
        {

            void Corrupt()
                => Errors.Add($"Corrupt time information: {Field}");

            if (!TimestampRegex().IsMatch(Value))
            {
                Corrupt();
                return null;
            }

            var year    = Int32.Parse(Value[ 0.. 2], CultureInfo.InvariantCulture);
            var month   = Int32.Parse(Value[ 2.. 4], CultureInfo.InvariantCulture);
            var day     = Int32.Parse(Value[ 4.. 6], CultureInfo.InvariantCulture);
            var hour    = Int32.Parse(Value[ 6.. 8], CultureInfo.InvariantCulture);
            var minute  = Int32.Parse(Value[ 8..10], CultureInfo.InvariantCulture);
            var second  = Int32.Parse(Value[10..12], CultureInfo.InvariantCulture);

            // The format predates no meter that could have written 2018, so a year
            // below 19 is a clock that was never set rather than a vintage record.
            if (year   < 19 ||
                month  <  1 || month > 12 ||
                day    <  1 || day   > 31 ||
                hour   > 23 ||
                minute > 59 ||
                second > 59)
            {
                Corrupt();
                return null;
            }

            // The 31st of February passes the ranges above and is still not a day.
            try
            {
                return new DateTimeOffset(2000 + year, month, day, hour, minute, second, TimeSpan.Zero);
            }
            catch (ArgumentOutOfRangeException)
            {
                Corrupt();
                return null;
            }

        }

        /// <summary>A charging duration: two digits each of hours, minutes and seconds.</summary>
        private static Int64? ParseDuration(String        Value,
                                            List<String>  Errors)
        {

            if (!DurationRegex().IsMatch(Value))
            {
                Errors.Add("Charging duration is invalid");
                return null;
            }

            var hours    = Int64.Parse(Value[0..2], CultureInfo.InvariantCulture);
            var minutes  = Int64.Parse(Value[2..4], CultureInfo.InvariantCulture);
            var seconds  = Int64.Parse(Value[4..6], CultureInfo.InvariantCulture);

            if (minutes > 59 || seconds > 59)
            {
                Errors.Add("Charging duration is invalid");
                return null;
            }

            return hours * 3600 + minutes * 60 + seconds;

        }

        /// <summary>A flag, which is "0" or "1" and nothing else.</summary>
        private static Boolean? ParseFlag(String        Value,
                                          String        Field,
                                          List<String>  Errors)
        {

            if (Value == "0") return false;
            if (Value == "1") return true;

            Errors.Add($"{Field} must be 0 or 1");

            return null;

        }

        /// <summary>An unsigned integer.</summary>
        private static Int64? ParseInteger(String        Value,
                                           String        Field,
                                           List<String>  Errors)
        {

            if (!DigitsRegex().IsMatch(Value) ||
                !Int64.TryParse(Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                Errors.Add($"{Field} must be an integer");
                return null;
            }

            return number;

        }

        /// <summary>
        /// The meter reading, which is written with exactly four digits before and
        /// three after the decimal point.
        /// </summary>
        private static (Decimal Value, String Unit)? ParseReading(String        Value,
                                                                  List<String>  Errors)
        {

            var match = ReadingRegex().Match(Value);

            if (!match.Success)
            {
                Errors.Add("Session information is invalid");
                return null;
            }

            return (
                       Decimal.Parse(
                           $"{match.Groups[1].Value}.{match.Groups[2].Value}",
                           NumberStyles.Number,
                           CultureInfo.InvariantCulture
                       ),
                       "kWh"
                   );

        }

        /// <summary>Who authorized, what kind of token they used, and the transaction.</summary>
        private static PCDFSessionInfo? ParseSession(String        Value,
                                                     List<String>  Errors)
        {

            var parts = Value.Split('*');

            if (parts.Length != 3)
            {
                Errors.Add("Session information is invalid");
                return null;
            }

            var idTag          = parts[0];
            var idTagType      = parts[1];
            var transactionId  = parts[2];

            if (idTag.Length < 1 || idTag.Length > 36 ||
                idTagType.Length != 1 || !IdTagTypeRegex().IsMatch(idTagType) ||
                transactionId.Length < 1 || transactionId.Length > 36 ||
                Value.Length < 5 || Value.Length > 75)
            {
                Errors.Add("Session information is invalid");
                return null;
            }

            return new PCDFSessionInfo(idTag, idTagType, transactionId);

        }

        /// <summary>
        /// A timestamp in the form JavaScript's Date.toISOString() writes it, which
        /// is what the charge transparency records carry.
        /// </summary>
        private static String ToISO8601(DateTimeOffset Timestamp)

            => ChargyLib.ToISO8601(Timestamp);

        #endregion

        #region (private) Regular expressions

        [GeneratedRegex(@"^128\.8\.0\(ST:([^)]*)\)\(CT:([^)]*)\)\(CD:([^)]*)\)\(TV:([^)]*)\)\(BV:([^)]*)\)\(CSC:([^)]*)\)\(SP:([^)]*)\)\(RV:([^)]*)\)\(SI:([^)]*)\)\(CS:([^)]*)\)\(HW:([^)]*)\)\(DT:([^)]*)\)\(PK:([^)]*)\)\(SG:([^)]*)\)$")]
        private static partial Regex DocumentRegex();

        [GeneratedRegex(@"\(([A-Z]{2,3}):([^)]*)\)")]
        private static partial Regex FieldRegex();

        [GeneratedRegex(@"^\d{12}$")]
        private static partial Regex TimestampRegex();

        [GeneratedRegex(@"^\d{6}$")]
        private static partial Regex DurationRegex();

        [GeneratedRegex(@"^\d+$")]
        private static partial Regex DigitsRegex();

        [GeneratedRegex(@"^(\d{4})\.(\d{3})\*kWh$")]
        private static partial Regex ReadingRegex();

        [GeneratedRegex(@"^[1-5]$")]
        private static partial Regex IdTagTypeRegex();

        [GeneratedRegex(@"^[0-9a-fA-F]{8}$")]
        private static partial Regex ChecksumRegex();

        [GeneratedRegex(@"^[0-9a-f]+$")]
        private static partial Regex LowerHexRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();

        #endregion

    }

}
