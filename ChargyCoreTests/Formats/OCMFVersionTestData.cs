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
using System.Text;

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// A complete, signed OCMF document for one version of the format, together
    /// with what reading it has to produce.
    ///
    /// Every OCMF version renamed or added something, and the fixtures in this
    /// repository cover the versions real meters happen to use — which is not all
    /// of them, and says nothing about the ones nobody sent us a file for. This
    /// generates one document per version, filled with values that differ in every
    /// field, so that a field read out of the wrong place shows up as the wrong
    /// value rather than as a coincidence.
    ///
    /// The values are drawn from a seeded generator: deterministic, so that a
    /// failing test fails the same way twice, and arbitrary, so that no assertion
    /// can quietly be satisfied by a hard-coded constant on both sides. The
    /// generator is the one ChargyCore.TS uses, down to the bit, so the two
    /// implementations are fed the same documents. The signature is not: it is
    /// made here with a key derived from the same stream, because neither
    /// implementation signs deterministically and neither needs to.
    /// </summary>
    internal class OCMFVersionTestData
    {

        #region Data

        /// <summary>The OCMF versions this port claims to read.</summary>
        public static readonly String[] SupportedVersions = [ "0.1", "1.0", "1.1", "1.2", "1.3", "1.4" ];

        #endregion

        #region Properties

        /// <summary>The OCMF version this document declares.</summary>
        public String    Version                { get; private init; } = "";

        /// <summary>Whether the payload carries an "FV" field at all.</summary>
        public Boolean   IncludesFormatVersion  { get; private init; }

        /// <summary>The whole document: "OCMF|{payload}|{signature}".</summary>
        public String    Document               { get; private init; } = "";

        /// <summary>The public key, as a base64 encoded SubjectPublicKeyInfo.</summary>
        public String    PublicKeyBase64        { get; private init; } = "";

        /// <summary>The payload, parsed.</summary>
        public JObject   Payload                { get; private init; } = [];

        /// <summary>What reading this document has to produce.</summary>
        public OCMFVersionExpectations Expected { get; private init; } = new ();

        #endregion


        #region (static) Create(Version, IncludesFormatVersion, ForcedChargePointType = null)

        /// <summary>
        /// Build the test data for one OCMF version.
        /// </summary>
        /// <param name="Version">An OCMF version.</param>
        /// <param name="IncludesFormatVersion">Whether the payload should carry an "FV" field.</param>
        /// <param name="ForcedChargePointType">Which way the charge point should be identified, when the test cares.</param>
        public static OCMFVersionTestData Create(String   Version,
                                                 Boolean  IncludesFormatVersion,
                                                 String?  ForcedChargePointType = null)
        {

            // The values are stable per version: only whether "FV" is written and
            // the resulting signature differ between the two variants of a version.
            var random    = new SeededRandom($"ChargyCore OCMF {Version}");
            var isLegacy  = Version == "0.1";

            #region The gateway and the meter

            var gatewayInformation  = (isLegacy ? "LegacyVendor-" : "Gateway-") + random.Hex(6);
            var gatewaySerial       = isLegacy ? null : "GS" + random.Hex(12);
            var gatewayVersion      = $"{random.Integer(1, 9)}.{random.Integer(0, 99)}";
            var meterVendor         = "MeterVendor-" + random.Hex(4);
            var meterModel          = "Model-"       + random.Hex(6);
            var meterSerial         = "MS"           + random.Hex(14);
            var meterFirmware       = $"{random.Integer(1, 9)}.{random.Integer(0, 99)}";
            var identificationType  = "ISO14443";
            var identificationData  = random.Hex(14);
            var pagination          = random.Integer(1, 999999);

            #endregion

            #region How the charge point is identified

            var useEVSEId          = ForcedChargePointType is not null
                                         ? ForcedChargePointType == "EVSEID"
                                         : !isLegacy && random.Integer(0, 1) == 0;

            var chargePointType    = isLegacy
                                         ? null
                                         : ForcedChargePointType ?? (useEVSEId ? "EVSEID" : "CBIDC");

            var chargingStationId  = chargePointType == "CBIDC"  ? $"CP-{random.Hex(10)}"                        : null;
            var evseId             = chargePointType == "EVSEID" ? $"DE*RND*E{random.Integer(1000000, 9999999)}" : null;
            var connectorId        = chargePointType == "CBIDC"  ? random.Integer(1, 8).ToString(CultureInfo.InvariantCulture) : null;

            var chargePointId      = chargePointType == "EVSEID"
                                         ? evseId
                                         : chargePointType == "CBIDC"
                                               ? $"{chargingStationId} {connectorId}"
                                               : null;

            #endregion

            #region The readings

            var beginValue          = random.Decimal(100, 5000, 3);
            var endValue            = Round(beginValue + random.Decimal(1, 100, 3), 3);

            var beginTimestampDate  = new DateTime(
                                          (Int32) random.Integer(2020, 2025),
                                          (Int32) random.Integer(0, 11) + 1,
                                          (Int32) random.Integer(1, 20),
                                          (Int32) random.Integer(0, 20),
                                          (Int32) random.Integer(0, 40),
                                          0,
                                          DateTimeKind.Utc
                                      );

            var endTimestampDate    = beginTimestampDate.AddMinutes(random.Integer(5, 120));

            var obis                = isLegacy
                                          ? "1-b:1.8.e"
                                          : Version == "1.4"
                                                ? "01-00:01.08.00*FF"
                                                : "1-b:1.8.0";

            #endregion

            #region What each version added

            var tariffProfile             = IsAtLeast(Version, "1.1")
                                                ? new[] { "001", "002", "003" }[random.Integer(0, 2)]
                                                : null;

            var tariffText                = tariffProfile switch {
                                                "001"  => $"001;EUR;{random.Integer(0, 500)};{random.Integer(1, 100)};{random.Integer(1, 50)};{random.Integer(1, 240)}",
                                                "002"  => $"002;EUR;{random.Integer(0, 500)};{random.Integer(1, 100)};{random.Integer(1, 50)}",
                                                "003"  => $"003;EUR;{random.Integer(0, 500)};{random.Integer(1, 100)}",
                                                _      => null
                                            };

            var lossCompensationName      = IsAtLeast(Version, "1.2") ? "Cable-" + random.Hex(4)  : null;
            var lossCompensationId        = IsAtLeast(Version, "1.2") ? random.Integer(1, 999)    : (Int64?) null;
            var lossCompensationOhms      = IsAtLeast(Version, "1.2") ? random.Decimal(1, 20, 3)  : (Decimal?) null;

            var controllerFirmwareVersion = IsAtLeast(Version, "1.3")
                                                ? $"{random.Integer(1, 9)}.{random.Integer(0, 999)}"
                                                : null;

            var cumulatedLoss             = IsAtLeast(Version, "1.2") ? random.Decimal(1, 10, 3)  : (Decimal?) null;
            var errorIndex                = isLegacy                  ? random.Integer(1, 9999)   : (Int64?) null;

            #endregion

            #region The payload, written out in the order the version writes it

            var fields = new List<String>();

            if (IncludesFormatVersion)
                fields.Add(Property("FV", Version));

            fields.Add(Property(isLegacy ? "VI" : "GI", gatewayInformation));
            fields.Add(Property(isLegacy ? "VV" : "GV", gatewayVersion));

            if (gatewaySerial is not null)
                fields.Add(Property("GS", gatewaySerial));

            fields.Add(Property("PG",  $"T{pagination}"));
            fields.Add(Property("MV",  meterVendor));
            fields.Add(Property("MM",  meterModel));
            fields.Add(Property("MS",  meterSerial));
            fields.Add(Property("MF",  meterFirmware));

            // The identification status: a word in 0.1, a boolean from 1.0 on.
            fields.Add(isLegacy
                           ? Property("IS", "VERIFIED")
                           : "\"IS\":true");

            if (!isLegacy)
                fields.Add(Property("IL", "TRUSTED"));

            fields.Add("\"IF\":[\"RFID_PLAIN\",\"OCPP_AUTH_TLS\"]");
            fields.Add(Property("IT",  identificationType));
            fields.Add(Property("ID",  identificationData));

            if (chargePointType is not null)
                fields.Add(Property("CT", chargePointType));

            if (chargePointId   is not null)
                fields.Add(Property("CI", chargePointId));

            if (tariffText      is not null)
                fields.Add(Property("TT", tariffText));

            if (lossCompensationOhms.HasValue)
                fields.Add($"\"LC\":{{{Property("LN", lossCompensationName!)},\"LI\":{lossCompensationId},\"LR\":{Number(lossCompensationOhms.Value)},{Property("LU", "mOhm")}}}");

            if (controllerFirmwareVersion is not null)
                fields.Add(Property("CF", controllerFirmwareVersion));

            fields.Add(
                "\"RD\":[" +
                Reading(beginTimestampDate, "B", beginValue, obis, isLegacy, errorIndex, cumulatedLoss.HasValue ? 0 : null) + "," +
                Reading(endTimestampDate,   "E", endValue,   obis, isLegacy, errorIndex, cumulatedLoss) +
                "]"
            );

            var rawPayload = "{" + String.Join(",", fields) + "}";

            #endregion

            #region ..., signed with a key derived from the same stream

            var curve       = ECNamedCurveTable.GetByName("secp256r1")!;
            var domain      = new ECNamedDomainParameters(SecObjectIdentifiers.SecP256r1, curve);

            var scalar      = random.Bytes(32);

            // Below 2^255 puts the scalar safely under the group order, and a set
            // low bit keeps it from being zero.
            scalar[0]      &= 0x7F;
            scalar[31]     |= 0x01;

            var privateKey  = new ECPrivateKeyParameters(new BigInteger(1, scalar), domain);
            var publicKey   = new ECPublicKeyParameters(domain.G.Multiply(privateKey.D).Normalize(), domain);

            var payload     = Encoding.UTF8.GetBytes(rawPayload);
            var signer      = SignerUtilities.GetSigner("SHA-256withECDSA");

            signer.Init(true, privateKey);
            signer.BlockUpdate(payload, 0, payload.Length);

            var signature   = "{\"SA\":\"ECDSA-secp256r1-SHA256\",\"SE\":\"hex\",\"SM\":\"application/x-der\",\"SD\":\"" +
                              Convert.ToHexString(signer.GenerateSignature()) +
                              "\"}";

            #endregion

            return new OCMFVersionTestData {

                       Version                = Version,
                       IncludesFormatVersion  = IncludesFormatVersion,
                       Document               = $"OCMF|{rawPayload}|{signature}",
                       Payload                = ChargyLib.ParseJSON(rawPayload),
                       PublicKeyBase64        = Convert.ToBase64String(
                                                    SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey).GetDerEncoded()
                                                ),

                       Expected = new OCMFVersionExpectations {
                                      GatewayInformation             = gatewayInformation,
                                      GatewaySerial                  = gatewaySerial,
                                      GatewayVersion                 = gatewayVersion,
                                      MeterVendor                    = meterVendor,
                                      MeterModel                     = meterModel,
                                      MeterSerial                    = meterSerial,
                                      MeterFirmware                  = meterFirmware,
                                      IdentificationStatus           = isLegacy ? null : true,
                                      IdentificationStatusText       = isLegacy ? "VERIFIED" : null,
                                      IdentificationLevel            = isLegacy ? null : "TRUSTED",
                                      IdentificationType             = identificationType,
                                      IdentificationData             = identificationData,
                                      TariffText                     = tariffText,
                                      TariffProfile                  = tariffProfile,
                                      ControllerFirmwareVersion      = controllerFirmwareVersion,
                                      LossCompensationName           = lossCompensationName,
                                      LossCompensationId             = lossCompensationId,
                                      LossCompensationOhms           = lossCompensationOhms,
                                      ChargePointIdentificationType  = chargePointType,
                                      ChargePointIdentification      = chargePointId,
                                      ChargingStationId              = chargingStationId,
                                      EVSEId                         = evseId,
                                      ConnectorId                    = connectorId,
                                      Pagination                     = (UInt64) pagination,
                                      OBIS                           = obis,
                                      Unit                           = "kWh",
                                      BeginTimestamp                 = ISO8601(beginTimestampDate),
                                      EndTimestamp                   = ISO8601(endTimestampDate),
                                      BeginValue                     = beginValue,
                                      EndValue                       = endValue,
                                      CumulatedLoss                  = cumulatedLoss,
                                      ErrorIndex                     = errorIndex
                                  }

                   };

        }

        #endregion


        #region (private, static) Reading (Timestamp, Transaction, Value, OBIS, IsLegacy, ErrorIndex, CumulatedLoss)

        /// <summary>
        /// One reading, written out in the order OCMF writes it.
        /// </summary>
        private static String Reading(DateTime  Timestamp,
                                      String    Transaction,
                                      Decimal   Value,
                                      String    OBIS,
                                      Boolean   IsLegacy,
                                      Int64?    ErrorIndex,
                                      Decimal?  CumulatedLoss)
        {

            var fields = new List<String> {
                             Property("TM",  Timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss',000+0000 S'", CultureInfo.InvariantCulture)),
                             Property("TX",  Transaction),
                             $"\"RV\":{Number(Value)}",
                             Property("RI",  OBIS),
                             Property("RU",  "kWh"),
                             Property("RT",  "AC"),
                             Property("ST",  "G")
                         };

            // OCMF 0.1 counted errors; from 1.0 on they became flags.
            fields.Add(IsLegacy
                           ? $"\"EI\":{ErrorIndex}"
                           : Property("EF", ""));

            if (CumulatedLoss.HasValue)
                fields.Add($"\"CL\":{Number(CumulatedLoss.Value)}");

            return "{" + String.Join(",", fields) + "}";

        }

        #endregion

        #region (private, static) Property(Name, Value)

        /// <summary>One JSON string property.</summary>
        private static String Property(String  Name,
                                       String  Value)

            => $"\"{Name}\":\"{Value}\"";

        #endregion

        #region (private, static) Number  (Value)

        /// <summary>
        /// A number, written the way JavaScript writes it — without the trailing
        /// zeros a decimal would otherwise carry.
        /// </summary>
        private static String Number(Decimal Value)
        {

            var text = Value.ToString(CultureInfo.InvariantCulture);

            return text.Contains('.')
                       ? text.TrimEnd('0').TrimEnd('.')
                       : text;

        }

        #endregion

        #region (private, static) Round   (Value, Digits)

        /// <summary>Round to the given number of decimal places.</summary>
        private static Decimal Round(Decimal  Value,
                                     Int32    Digits)

            => Decimal.Round(Value, Digits, MidpointRounding.AwayFromZero);

        #endregion

        #region (private, static) ISO8601 (Timestamp)

        /// <summary>The timestamp as the OCMF reader will have written it.</summary>
        private static String ISO8601(DateTime Timestamp)

            => Timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss'.000+00:00'", CultureInfo.InvariantCulture);

        #endregion

        #region (private, static) IsAtLeast(Version, Minimum)

        /// <summary>Whether the given version is the given one or a later one.</summary>
        private static Boolean IsAtLeast(String  Version,
                                         String  Minimum)

            => Version != "0.1" &&
               Array.IndexOf(SupportedVersions, Version) >= Array.IndexOf(SupportedVersions, Minimum);

        #endregion


        #region (class) SeededRandom

        /// <summary>
        /// The pseudo random generator ChargyCore.TS seeds its version test data
        /// with: FNV-1a over the seed text, then xorshift32.
        ///
        /// Reproduced bit for bit rather than replaced by a better generator,
        /// because the point of it is that both implementations read the same
        /// documents. Nothing here is cryptographic, and nothing here should be
        /// used as if it were.
        /// </summary>
        /// <param name="Seed">The seed text.</param>
        private class SeededRandom(String Seed)
        {

            private UInt32 state = HashSeed(Seed) is UInt32 hash && hash != 0 ? hash : 0x6D2B79F5;

            /// <summary>The next 32 bits.</summary>
            public UInt32 NextUInt32()
            {
                unchecked
                {
                    var value = state;
                    value ^= value << 13;
                    value ^= value >> 17;
                    value ^= value << 5;
                    state  = value;
                    return state;
                }
            }

            /// <summary>A whole number between the two bounds, both included.</summary>
            public Int64 Integer(Int64  Minimum,
                                 Int64  Maximum)

                => Minimum + NextUInt32() % (Maximum - Minimum + 1);

            /// <summary>A number with the given number of decimal places.</summary>
            public Decimal Decimal(Int64  Minimum,
                                   Int64  Maximum,
                                   Int32  Digits)
            {

                var factor = (Int64) Math.Pow(10, Digits);

                return (Decimal) Integer(Minimum * factor, Maximum * factor) / factor;

            }

            /// <summary>An upper case hexadecimal string of the given length.</summary>
            public String Hex(Int32 Length)
            {

                var result = new StringBuilder();

                while (result.Length < Length)
                    result.Append(NextUInt32().ToString("x8", CultureInfo.InvariantCulture));

                return result.ToString(0, Length).ToUpperInvariant();

            }

            /// <summary>The given number of bytes.</summary>
            public Byte[] Bytes(Int32 Length)
            {

                var result = new Byte[Length];

                for (var i = 0; i < Length; i++)
                    result[i] = (Byte) (NextUInt32() & 0xFF);

                return result;

            }

            /// <summary>FNV-1a over the seed text.</summary>
            private static UInt32 HashSeed(String Seed)
            {
                unchecked
                {

                    var hash = 0x811C9DC5u;

                    foreach (var character in Seed)
                    {
                        hash ^= character;
                        hash *= 0x01000193u;
                    }

                    return hash;

                }
            }

        }

        #endregion

    }


    /// <summary>
    /// What the reading of one generated OCMF document has to produce.
    /// </summary>
    internal class OCMFVersionExpectations
    {

        #region Properties

        /// <summary>What the signing gateway calls itself.</summary>
        public String?   GatewayInformation             { get; init; }

        /// <summary>The serial number of the gateway.</summary>
        public String?   GatewaySerial                  { get; init; }

        /// <summary>The software version of the gateway.</summary>
        public String?   GatewayVersion                 { get; init; }

        /// <summary>The manufacturer of the energy meter.</summary>
        public String?   MeterVendor                    { get; init; }

        /// <summary>The model of the energy meter.</summary>
        public String?   MeterModel                     { get; init; }

        /// <summary>The serial number of the energy meter.</summary>
        public String?   MeterSerial                    { get; init; }

        /// <summary>The firmware version of the energy meter.</summary>
        public String?   MeterFirmware                  { get; init; }

        /// <summary>Whether the driver's identification was present and complete.</summary>
        public Boolean?  IdentificationStatus           { get; init; }

        /// <summary>The meter's own word for that, where it wrote one.</summary>
        public String?   IdentificationStatusText       { get; init; }

        /// <summary>How the identification was assured.</summary>
        public String?   IdentificationLevel            { get; init; }

        /// <summary>How the driver identified themselves.</summary>
        public String?   IdentificationType             { get; init; }

        /// <summary>What they identified themselves with.</summary>
        public String?   IdentificationData             { get; init; }

        /// <summary>The tariff, as free text.</summary>
        public String?   TariffText                     { get; init; }

        /// <summary>Which Bonn profile that text follows.</summary>
        public String?   TariffProfile                  { get; init; }

        /// <summary>The firmware version of the charging controller.</summary>
        public String?   ControllerFirmwareVersion      { get; init; }

        /// <summary>The name of the cable loss compensation.</summary>
        public String?   LossCompensationName           { get; init; }

        /// <summary>The identification of the cable loss compensation.</summary>
        public Int64?    LossCompensationId             { get; init; }

        /// <summary>The resistance the meter compensates for.</summary>
        public Decimal?  LossCompensationOhms           { get; init; }

        /// <summary>How the charge point identification is to be read.</summary>
        public String?   ChargePointIdentificationType  { get; init; }

        /// <summary>The identification of the charge point.</summary>
        public String?   ChargePointIdentification      { get; init; }

        /// <summary>The identification of the charging station, where the document names one.</summary>
        public String?   ChargingStationId              { get; init; }

        /// <summary>The identification of the EVSE, where the document names one.</summary>
        public String?   EVSEId                         { get; init; }

        /// <summary>The identification of the connector, where the document names one.</summary>
        public String?   ConnectorId                    { get; init; }

        /// <summary>The pagination counter.</summary>
        public UInt64    Pagination                     { get; init; }

        /// <summary>The OBIS code of the readings.</summary>
        public String?   OBIS                           { get; init; }

        /// <summary>The unit of the readings.</summary>
        public String?   Unit                           { get; init; }

        /// <summary>When the first reading was taken.</summary>
        public String?   BeginTimestamp                 { get; init; }

        /// <summary>When the last reading was taken.</summary>
        public String?   EndTimestamp                   { get; init; }

        /// <summary>The first reading.</summary>
        public Decimal   BeginValue                     { get; init; }

        /// <summary>The last reading.</summary>
        public Decimal   EndValue                       { get; init; }

        /// <summary>The energy compensated for the cable by the end.</summary>
        public Decimal?  CumulatedLoss                  { get; init; }

        /// <summary>The error index, as OCMF 0.1 wrote it.</summary>
        public Int64?    ErrorIndex                     { get; init; }

        #endregion

    }

}
