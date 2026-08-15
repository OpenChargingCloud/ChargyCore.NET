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

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.IO;

#endregion

namespace cloud.charging.open.chargy.Formats.PCDF
{

    /// <summary>
    /// A measurement produced by a Porsche DC meter.
    /// </summary>
    /// <param name="Timestamp">When the value was measured, as an ISO 8601 string.</param>
    /// <param name="Value">The measured value.</param>
    /// <param name="Document">The document this reading was read out of.</param>
    /// <param name="StatusMeter">The status of the meter, "G" when the closing reading is there.</param>
    public class PCDFMeasurementValue(String        Timestamp,
                                      Decimal       Value,
                                      PCDFDocument  Document,
                                      String?       StatusMeter = null)

        : MeasurementValue(Timestamp,
                           Value,
                           [ Document.Signature ],
                           StatusMeter: StatusMeter)

    {

        /// <summary>The document this reading was read out of.</summary>
        public PCDFDocument Document { get; } = Document;

    }


    /// <summary>
    /// The Porsche Charging Data Format.
    ///
    /// A whole charging session on one line: fourteen parenthesised fields, the
    /// last of which signs the thirteen before it. That makes it the format an EV
    /// driver is most likely to be able to read with their own eyes — and the one
    /// where nothing at all can be checked without the closing signature, since
    /// there is no second reading to compare against.
    ///
    /// The document carries its own public key, which sounds circular and is not:
    /// a key inside a signed document proves nothing on its own, and the operator
    /// still has to publish the key that a driver can compare it against. What it
    /// does give is a self-contained document, which is why a key handed over
    /// separately is checked against the one inside rather than used instead of it.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    public class PCDFFormat(I18NDictionary I18N) : ITextChargeTransparencyFormat
    {

        #region Data

        /// <summary>The JSON-LD context of a PCDF charging session.</summary>
        public const String SessionContext       = "https://open.charging.cloud/contexts/SessionSignatureFormats/PCDF+json";

        /// <summary>The signature format of a Porsche DC meter.</summary>
        public const String MeterSignatureFormat = "https://open.charging.cloud/contexts/EnergyMeterSignatureFormats/PCDF+json";

        private readonly I18NDictionary i18n = I18N;

        #endregion

        #region Properties

        /// <summary>The name of the data format.</summary>
        public String Name
            => "PCDF";

        #endregion


        #region CanParse    (Text)

        /// <summary>
        /// Whether the given text is a PCDF document.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        public Boolean CanParse(String Text)

            => PCDFDocument.IsPCDFText(Text);

        #endregion

        #region TryParseText(Text, PublicKeyHEX = null)

        /// <summary>
        /// Try to read a charge transparency record from a PCDF document.
        /// </summary>
        /// <param name="Text">The contents of a file.</param>
        /// <param name="PublicKeyHEX">
        /// An optional public key that arrived alongside the document. A PCDF
        /// document carries its own, so this is used to *contradict* it rather
        /// than to supply what is missing.
        /// </param>
        public Object TryParseText(String   Text,
                                   String?  PublicKeyHEX = null)
        {

            try
            {

                var document = PCDFDocument.Read(Text);

                #region A separately filed key that disagrees is a reason to stop, not to choose

                if (PublicKeyHEX is not null &&
                    PCDFDocument.NormalizePublicKeyHEX(PublicKeyHEX) != document.PublicKeyHEX)
                {
                    return new SessionCryptoResult(
                               SessionVerificationResult.InvalidPublicKey,
                               i18n.GetMultilanguageText("Wrong Public Key"),
                               Certainty: 1
                           );
                }

                #endregion

                return BuildChargeTransparencyRecord(document);

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


        #region (private, static) BuildChargeTransparencyRecord(Document)

        /// <summary>
        /// Turn a verified PCDF document into a charge transparency record.
        /// </summary>
        /// <param name="Document">A verified PCDF document.</param>
        private static ChargeTransparencyRecord BuildChargeTransparencyRecord(PCDFDocument Document)
        {

            // A PCDF document says nothing about where it was produced, so the
            // pool, the station and the EVSE are Chargy's own placeholders — named
            // honestly as such rather than invented.
            const String poolId     = "DE*GEF*POOL*CHARGY*1";
            const String stationId  = "DE*GEF*STATION*CHARGY*1";
            const String evseId     = "DE*GEF*EVSE*CHARGY*1";

            var meterId        = Document.HardwareSerial;

            var transactionId  = Document.Session.TransactionId.Length > 0
                                     ? Document.Session.TransactionId
                                     : Document.ChargingSessionCounter.ToString();

            #region The energy meter and its public key

            var energyMeter = new EnergyMeter(
                                  meterId,
                                  Manufacturer:     new Manufacturer("Porsche"),
                                  Model:            new DeviceModel(
                                                        Document.DCMeterType == 0
                                                            ? "PES DCMeter EU"
                                                            : "Unknown DC Meter"
                                                    ),
                                  Firmware:         new Firmware(Checksum: Document.SoftwareChecksum),
                                  SignatureFormat:  MeterSignatureFormat,
                                  PublicKeys:       [
                                                        new PublicKey(
                                                            Document.PublicKeyHEX,
                                                            new OIDInfo(ECCurve.secp256r1.AsText()),
                                                            Format:    "XY",
                                                            Encoding:  DataEncoding.Hex.AsText()
                                                        )
                                                    ]
                              );

            #endregion

            #region The measurement, which holds the single closing reading

            var measurement = new Measurement(
                                  meterId,
                                  "ENERGY_TOTAL",
                                  PCDFDocument.Prefix,
                                  -3,
                                  Values:          [
                                                       new PCDFMeasurementValue(
                                                           Document.StopTime,
                                                           Document.ReadingValue,
                                                           Document,
                                                           // "G" for good, "E" when the meter
                                                           // never got to write its last reading.
                                                           StatusMeter: Document.StopPresent ? "G" : "E"
                                                       ) {
                                                           Result = new CryptoResult(Document.ValidationStatus)
                                                       }
                                                   ],
                                  Unit:            Document.ReadingUnit,
                                  SignatureInfos:  new SignatureInfos(
                                                       Hash:       CryptoHashAlgorithm.SHA256,
                                                       Algorithm:  CryptoAlgorithm.ECC,
                                                       Curve:      ECCurve.secp256r1,
                                                       Format:     SignatureFormat.DER,
                                                       Encoding:   DataEncoding.Hex
                                                   )
                              );

            #endregion

            var chargingSession = new ChargingSession(
                                      transactionId,
                                      Context:            [ SessionContext ],
                                      Begin:              Document.StartTime,
                                      End:                Document.StopTime,
                                      EVSEId:             evseId,
                                      EnergyMeterId:      meterId,
                                      InternalSessionId:  Document.ChargingSessionCounter.ToString(),
                                      Measurements:       [ measurement ]
                                  ) {
                                      AuthorizationStart = new Authorization(
                                                               Document.Session.IdTag,
                                                               Type: Document.Session.IdTagType
                                                           )
                                  };

            var record = new ChargeTransparencyRecord(
                             $"PCDF:{transactionId}",
                             [ "https://open.charging.cloud/contexts/CTR+json" ],
                             Document.StartTime,
                             Document.StopTime,
                             I18NString.Create(Languages.de, "Porsche Charging Data Format Ladevorgang").
                                              Set(Languages.en, "Porsche Charging Data Format charging session"),
                             Certainty:  1
                         );

            record.AddChargingPool(
                new ChargingPool(
                    poolId,
                    Description:       I18NString.Create(Languages.en, "GraphDefined CHARGY Virtual Charging Pool 1"),
                    ChargingStations:  [
                                           new ChargingStation(
                                               stationId,
                                               Description:  I18NString.Create(Languages.en, "GraphDefined CHARGY Virtual Charging Station 1"),
                                               EVSEs:        [
                                                                 new EVSE(
                                                                     evseId,
                                                                     Description:   I18NString.Create(Languages.en, "GraphDefined CHARGY Virtual EVSE 1"),
                                                                     EnergyMeters:  [ energyMeter ]
                                                                 )
                                                             ]
                                           )
                                       ]
                )
            );

            record.AddChargingSession(chargingSession);

            record.AddPublicKey(
                new PublicKey(
                    Document.PublicKeyHEX,
                    new OIDInfo(ECCurve.secp256r1.AsText()),
                    Context:    [ "https://open.charging.cloud/contexts/publicKey+json" ],
                    Subject:    meterId,
                    Encoding:   DataEncoding.Hex.AsText(),
                    Certainty:  1
                )
            );

            record.AddContract(
                new Contract(
                    Document.Session.IdTag,
                    Type: Document.Session.IdTagType
                )
            );

            return record;

        }

        #endregion


    }

}
