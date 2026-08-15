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

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.IO;

#endregion

namespace cloud.charging.open.chargy.Formats.EDL40
{

    /// <summary>
    /// The EDL40 and ISA-EDL40 formats: signed meter readings as SML messages.
    ///
    /// SML is what a German electricity meter speaks natively, so unlike the other
    /// charge transparency formats this one was not invented for charging — it is
    /// the meter's own language, with a signed block bolted on. That shows: the
    /// readings arrive wrapped in a transport frame, addressed by OBIS code, and
    /// carrying the meter's local time rather than UTC.
    ///
    /// A document never travels on its own. It always arrives inside a SAFE XML
    /// container, which is also where its public key comes from — an SML message
    /// carries no key, so without the container nothing about it can be checked.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    public class EDL40Format(I18NDictionary I18N) : IChargeTransparencyFormat
    {

        #region Data

        /// <summary>The JSON-LD context of an EDL40 charging session.</summary>
        public const String SessionContext      = "https://open.charging.cloud/contexts/SessionSignatureFormats/EDL40+json";

        /// <summary>The JSON-LD context of an EDL40 measurement.</summary>
        public const String MeasurementContext  = "https://open.charging.cloud/contexts/EnergyMeterSignatureFormats/EDL40+json";

        /// <summary>The OBIS code of the total energy an EDL40 meter reports.</summary>
        public const String OBIS                = "1-0:1.8.0*255";

        private readonly I18NDictionary i18n = I18N;

        #endregion

        #region Properties

        /// <summary>The name of the data format.</summary>
        public String Name
            => "EDL40";

        #endregion


        #region TryParse(SignedValues, PublicKeyHEX, ContainerInfos = null)

        /// <summary>
        /// Try to read a charge transparency record from EDL40 or ISA documents.
        /// </summary>
        /// <param name="SignedValues">The SML messages the container carried.</param>
        /// <param name="PublicKeyHEX">The public key of the meter, which the container carries.</param>
        /// <param name="ContainerInfos">What the surrounding container knew, if anything.</param>
        public Object TryParse(IEnumerable<String>  SignedValues,
                               String               PublicKeyHEX,
                               ContainerInfos?      ContainerInfos = null)
        {

            try
            {

                var signedValues = SignedValues.ToArray();

                if (signedValues.Length == 0)
                    return Invalid("The given EDL40 data could not be parsed!");

                var parsed = signedValues.Select(signedValue => (
                                                     Raw:            signedValue,
                                                     SignatureData:  AEDL40SignatureData.Parse(signedValue)
                                                 )).
                                          ToArray();

                #region A charging session is one meter in one layout, not a mixture

                if (parsed.Select(value => value.SignatureData.Variant).Distinct().Count() > 1)
                    return Invalid("Invalid mixture of different signed data formats within the given XML container!");

                #endregion

                var documents = parsed.Select(value => EDL40Document.Verify(
                                                          value.SignatureData,
                                                          PublicKeyHEX,
                                                          value.Raw
                                                      )).
                                       ToArray();

                return BuildChargeTransparencyRecord(
                           [.. parsed.Select(value => value.SignatureData)],
                           documents,
                           PublicKeyHEX,
                           ContainerInfos
                       );

            }
            catch (Exception exception)
            {
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSessionFormat,
                           i18n.GetMultilanguageText(exception.Message),
                           Exception: exception
                       );
            }

        }

        #endregion


        #region (private) BuildChargeTransparencyRecord(SignatureData, Documents, PublicKeyHEX, ContainerInfos)

        /// <summary>
        /// Turn the verified documents into a charge transparency record.
        /// </summary>
        /// <param name="SignatureData">The parsed documents.</param>
        /// <param name="Documents">The same documents, with their verification results.</param>
        /// <param name="PublicKeyHEX">The public key of the meter.</param>
        /// <param name="ContainerInfos">What the surrounding container knew, if anything.</param>
        private static ChargeTransparencyRecord BuildChargeTransparencyRecord(AEDL40SignatureData[]  SignatureData,
                                                                              EDL40Document[]        Documents,
                                                                              String                 PublicKeyHEX,
                                                                              ContainerInfos?        ContainerInfos)
        {

            var first              = SignatureData[0];
            var last               = SignatureData[^1];

            var values             = ToMeasurementValues(SignatureData, Documents);
            var firstValue         = values[0];
            var lastValue          = values[^1];

            var publicKey          = ChargyLib.CleanHex(PublicKeyHEX);
            var serverId           = Convert.ToHexStringLower(first.ServerId);
            var energyMeterId      = serverId;
            var curve              = Documents.Length > 0
                                         ? Documents[0].Curve
                                         : ECCurve.secp192r1;

            // The identification is the meter plus the range of pages it wrote:
            // that is what makes two sessions of the same meter tellable apart,
            // and what makes a page removed from the middle noticeable.
            var chargingSessionId  = $"{serverId}-{first.Pagination}-{last.Pagination}";

            // An SML message says nothing about where it was produced, so without
            // a container these fall back to Chargy's own placeholders.
            var evseId             = ContainerInfos?.FirstEVSEId            ?? "DE*GEF*EVSE*EDL40*1";
            var chargingStationId  = ContainerInfos?.FirstChargingStationId ?? "DE*GEF*STATION*EDL40*1";

            #region The energy meter and its public key

            var energyMeter = new EnergyMeter(
                                  energyMeterId,
                                  Manufacturer:     new Manufacturer(
                                                        first.Variant == EDL40Variant.ISA_EDL_40_P
                                                            ? "ISA"
                                                            : "EDL40"
                                                    ),
                                  SignatureFormat:  MeasurementContext,
                                  PublicKeys:       [
                                                        new PublicKey(
                                                            publicKey,
                                                            new OIDInfo(curve.AsText()),
                                                            Format:    "XY",
                                                            Encoding:  DataEncoding.Hex.AsText()
                                                        )
                                                    ]
                              );

            #endregion

            #region The measurement and its values

            var measurement = new EDL40Measurement(
                                  energyMeterId,
                                  ChargyLib.OBIS2MeasurementName(OBIS),
                                  OBIS,
                                  // The readings are handed on in kWh, so the scale
                                  // that turned watt hours into them is recorded here.
                                  -3,
                                  serverId,
                                  publicKey,
                                  first.Variant,
                                  curve,
                                  Values:          values,
                                  Context:         [ MeasurementContext ],
                                  Unit:            "kWh",
                                  UnitEncoded:     30,
                                  SignatureInfos:  new SignatureInfos(
                                                       Hash:            CryptoHashAlgorithm.SHA256,
                                                       Algorithm:       CryptoAlgorithm.ECC,
                                                       Curve:           curve,
                                                       Format:          SignatureFormat.RS,
                                                       HashTruncation:  curve == ECCurve.secp192r1 ? (UInt16) 24 : (UInt16) 32,
                                                       Encoding:        DataEncoding.Hex
                                                   )
                              );

            #endregion

            #region The charging station, which only the container can describe

            var containerStation  = ContainerInfos?.ChargingStations.FirstOrDefault();
            var containerEVSE     = containerStation?.EVSEs.FirstOrDefault();

            var chargingStation   = new ChargingStation(
                                        chargingStationId,
                                        Description:  containerStation?.Description
                                                          ?? (containerStation is null
                                                                  ? I18NString.Create(Languages.en, "EDL40 charging station")
                                                                  : null),
                                        Firmware:     containerStation?.Firmware,
                                        GeoLocation:  containerStation?.GeoLocation,
                                        EVSEs:        [
                                                          new EVSE(
                                                              evseId,
                                                              Description:   containerEVSE?.Description,
                                                              EnergyMeters:  [ energyMeter ],
                                                              Connectors:    containerEVSE?.Connectors
                                                          )
                                                      ]
                                    );

            #endregion

            var chargingSession = new ChargingSession(
                                      chargingSessionId,
                                      Context:            [ SessionContext ],
                                      Begin:              firstValue.Timestamp,
                                      End:                lastValue.Timestamp,
                                      EVSEId:             evseId,
                                      EnergyMeterId:      energyMeterId,
                                      InternalSessionId:  chargingSessionId,
                                      Measurements:       [ measurement ]
                                  ) {
                                      AuthorizationStart = Documents.Length > 0 && Documents[0].ContractId.Length > 0
                                                               ? new Authorization(Documents[0].ContractId)
                                                               : null
                                  };

            var record = new ChargeTransparencyRecord(
                             chargingSessionId,
                             [ "https://open.charging.cloud/contexts/CTR+json" ],
                             chargingSession.Begin,
                             chargingSession.End,
                             I18NString.Create(Languages.de, "EDL40/ISA-EDL40 Ladevorgang").
                                              Set(Languages.en, "EDL40/ISA-EDL40 charging session"),
                             Certainty:  1,
                             Status:     SessionVerificationResult.Unvalidated
                         );

            record.AddChargingStation(chargingStation);
            record.AddChargingSession(chargingSession);

            record.AddPublicKey(
                new PublicKey(
                    publicKey,
                    new OIDInfo(curve.AsText()),
                    Context:    [ "https://open.charging.cloud/contexts/publicKey+json" ],
                    Subject:    energyMeterId,
                    Format:     "XY",
                    Encoding:   DataEncoding.Hex.AsText(),
                    Certainty:  1
                )
            );

            // Everything the container found questionable travels with the record,
            // so that an EV driver is told about it rather than only the software.
            foreach (var warning in ContainerInfos?.Warnings ?? [])
                record.AddWarning(warning);

            return record;

        }

        #endregion

        #region (private, static) ToMeasurementValues(SignatureData, Documents)

        /// <summary>
        /// Turn the documents into meter readings, in the order they were taken.
        ///
        /// An ISA document yields two readings, an EDL40 document one — which is
        /// why a single ISA message is already a whole charging session while a
        /// single EDL40 message never is.
        /// </summary>
        /// <param name="SignatureData">The parsed documents.</param>
        /// <param name="Documents">The same documents, with their verification results.</param>
        private static EDL40MeasurementValue[] ToMeasurementValues(AEDL40SignatureData[]  SignatureData,
                                                                   EDL40Document[]        Documents)
        {

            var values = new List<EDL40MeasurementValue>();

            for (var index = 0; index < SignatureData.Length; index++)
            {

                var signatureData  = SignatureData[index];
                var document       = Documents[index];

                if (signatureData is ISAEDL40SignatureData isa)
                {

                    values.Add(ToValue(
                                   isa.StartECTimestamp,
                                   isa.StartECValue,
                                   isa.StartECScaler,
                                   Convert.ToHexStringLower(isa.StartECStatus),
                                   isa.Pagination,
                                   document
                               ));

                    values.Add(ToValue(
                                   isa.ActualECTimestamp,
                                   isa.ActualECValue,
                                   isa.ActualECScaler,
                                   Convert.ToHexStringLower(isa.ActualECStatus),
                                   isa.Pagination,
                                   document
                               ));

                }

                else if (signatureData is EDL40PSignatureData edl40)
                {
                    values.Add(ToValue(
                                   edl40.MeterTimestamp,
                                   edl40.MeterValue,
                                   edl40.Scaler,
                                   edl40.Status.ToString("x2"),
                                   edl40.Pagination,
                                   document
                               ));
                }

            }

            if (values.Count == 0)
                throw new EDL40ValidationException("MISSING_FIELD", "Missing EDL40 measurement value");

            return [.. values.OrderBy(value => value.Timestamp, StringComparer.Ordinal)];

        }

        #endregion

        #region (private, static) ToValue(Timestamp, ValueWh, Scaler, StatusMeter, Pagination, Document)

        /// <summary>
        /// One meter reading.
        /// </summary>
        /// <param name="Timestamp">When the value was measured.</param>
        /// <param name="ValueWh">The reading, in the meter's own scaled watt hours.</param>
        /// <param name="Scaler">The scale of the reading, as a power of ten.</param>
        /// <param name="StatusMeter">The status word of the meter, hexadecimal.</param>
        /// <param name="Pagination">The pagination counter of the meter.</param>
        /// <param name="Document">The document this reading was read out of.</param>
        private static EDL40MeasurementValue ToValue(DateTimeOffset  Timestamp,
                                                     BigInteger      ValueWh,
                                                     Int32           Scaler,
                                                     String          StatusMeter,
                                                     Int64           Pagination,
                                                     EDL40Document   Document)

            => new (
                   ChargyLib.ToISO8601(Timestamp),
                   ScaledWhToKWh(ValueWh, Scaler),
                   Document,
                   Signatures:    [ Document.Signature ],
                   StatusMeter:   StatusMeter,
                   PaginationId:  Pagination.ToString()
               ) {
                   // Every reading of a document inherits that document's verdict:
                   // the signature covers the whole block, so a reading inside it
                   // cannot be more or less valid than the block it came in.
                   Result = new CryptoResult(Document.ValidationStatus)
               };

        #endregion

        #region (private, static) ScaledWhToKWh(ValueWh, Scaler)

        /// <summary>
        /// A meter reading in kWh.
        ///
        /// The meter reports an integer and, separately, the power of ten it has
        /// to be multiplied by — so the reading only becomes a quantity of energy
        /// once both are applied. Done in decimal rather than in binary floating
        /// point, because this number ends up on an invoice.
        /// </summary>
        /// <param name="ValueWh">The reading, in the meter's own scaled watt hours.</param>
        /// <param name="Scaler">The scale of the reading, as a power of ten.</param>
        private static Decimal ScaledWhToKWh(BigInteger  ValueWh,
                                             Int32       Scaler)
        {

            var value = (Decimal) ValueWh;

            return Scaler >= 0
                       ? value * Pow10( Scaler) / 1000
                       : value / Pow10(-Scaler) / 1000;

        }

        /// <summary>The given power of ten.</summary>
        private static Decimal Pow10(Int32 Exponent)
        {

            var result = 1m;

            for (var i = 0; i < Exponent; i++)
                result *= 10;

            return result;

        }

        #endregion

        #region (private) Invalid(MessageKey)

        /// <summary>
        /// Report that the data is not a valid EDL40 charging session.
        /// </summary>
        /// <param name="MessageKey">The i18n key of the reason.</param>
        private SessionCryptoResult Invalid(String MessageKey)

            => new (
                   SessionVerificationResult.InvalidSessionFormat,
                   i18n.GetMultilanguageText(MessageKey)
               );

        #endregion


    }

}
