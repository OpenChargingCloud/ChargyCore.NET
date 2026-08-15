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

using System.Xml.Linq;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.IO;

#endregion

namespace cloud.charging.open.chargy.Formats.Mennekes
{

    /// <summary>
    /// The Mennekes EDL40 XML format.
    ///
    /// A Mennekes document describes whole charging processes rather than a stream
    /// of readings: each one carries exactly two signed readings, the start and the
    /// end, plus the token the driver authorized with. So unlike the meter formats
    /// there is nothing to reassemble into sessions — the document already says
    /// which readings belong together, and the signatures are what decides whether
    /// to believe it.
    ///
    /// One document may hold several charging processes, wrapped in a "Billing"
    /// element. Each becomes its own charging session at its own charging station,
    /// because a single invoice covering several stations is an invoice, not a
    /// charging session.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    public class MennekesFormat(I18NDictionary I18N) : IXMLChargeTransparencyFormat
    {

        #region Data

        /// <summary>The JSON-LD context of a Mennekes charging session.</summary>
        public const String SessionContext      = "https://open.charging.cloud/contexts/SessionSignatureFormats/MennekesCrypt01+json";

        /// <summary>The JSON-LD context of a Mennekes measurement.</summary>
        public const String MeasurementContext  = "https://open.charging.cloud/contexts/EnergyMeterSignatureFormats/MennekesCrypt01+json";

        /// <summary>The signature format of a Mennekes energy meter.</summary>
        public const String MeterSignatureFormat = "https://open.charging.cloud/contexts/EnergyMeterSignatureFormats/MennekesCrypt01";

        private readonly I18NDictionary i18n = I18N;

        #endregion

        #region Properties

        /// <summary>The name of the data format.</summary>
        public String Name
            => "Mennekes";

        #endregion


        #region TryParseXML(Document)

        /// <summary>
        /// Try to read a charge transparency record from a Mennekes EDL40 document.
        /// </summary>
        /// <param name="Document">An XML document.</param>
        public Object TryParseXML(XDocument Document)
        {

            try
            {

                var chargingProcesses = MennekesChargingProcess.ExtractFrom(Document).ToArray();

                if (chargingProcesses.Length == 0)
                    return new SessionCryptoResult(
                               SessionVerificationResult.InvalidSessionFormat,
                               i18n.GetMultilanguageText("UnknownOrInvalidChargingSessionFormat")
                           );

                return BuildChargeTransparencyRecord(chargingProcesses);

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


        #region (private, static) BuildChargeTransparencyRecord(ChargingProcesses)

        /// <summary>
        /// Turn the charging processes into a charge transparency record.
        /// </summary>
        /// <param name="ChargingProcesses">The charging processes the document held.</param>
        private static ChargeTransparencyRecord BuildChargeTransparencyRecord(MennekesChargingProcess[] ChargingProcesses)
        {

            var chargingSessions  = new List<ChargingSession>();
            var chargingStations  = new List<ChargingStation>();

            for (var index = 0; index < ChargingProcesses.Length; index++)
            {
                chargingSessions.Add(BuildChargingSession(ChargingProcesses[index], index));
                chargingStations.Add(BuildChargingStation(ChargingProcesses[index], index));
            }

            var record = new ChargeTransparencyRecord(
                             chargingSessions[0].Id,
                             [ "https://open.charging.cloud/contexts/CTR+json" ],
                             chargingSessions[0]. Begin,
                             chargingSessions[^1].End,
                             I18NString.Create(Languages.de, "Alle Ladevorgaenge").
                                              Set(Languages.en, "All charging sessions"),
                             Certainty:  1,
                             Status:     SessionVerificationResult.Unvalidated
                         );

            record.AddChargingPool(
                new ChargingPool(
                    "DE*GEF*POOL*MENNEKES*1",
                    Description:       I18NString.Create(Languages.en, "Mennekes EDL40 charging pool"),
                    ChargingStations:  chargingStations
                )
            );

            foreach (var chargingSession in chargingSessions)
                record.AddChargingSession(chargingSession);

            record.AddContract(new Contract(ChargingProcesses[0].CustomerIdent));

            return record;

        }

        #endregion

        #region (private, static) BuildChargingStation(ChargingProcess, Index)

        /// <summary>
        /// The charging station one charging process came from.
        /// </summary>
        /// <param name="ChargingProcess">A charging process.</param>
        /// <param name="Index">Which charging process of the document this is.</param>
        private static ChargingStation BuildChargingStation(MennekesChargingProcess  ChargingProcess,
                                                            Int32                    Index)

            => new (
                   $"DE*GEF*STATION*MENNEKES*{Index + 1}",
                   Description:   I18NString.Create(Languages.en, "Mennekes EDL40 charging station"),
                   Manufacturer:  new Manufacturer("MENNEKES"),
                   Address:       ChargingProcess.SiteAddress,
                   EVSEs:         [
                                      new EVSE(
                                          EVSEIdOf(ChargingProcess, Index),
                                          EnergyMeters: [
                                                            new EnergyMeter(
                                                                ChargingProcess.MeterId,
                                                                Manufacturer:     new Manufacturer("MENNEKES"),
                                                                SignatureFormat:  MeterSignatureFormat,
                                                                PublicKeys:       [
                                                                                      new PublicKey(
                                                                                          ChargingProcess.PublicKey,
                                                                                          new OIDInfo(ECCurve.secp192r1.AsText()),
                                                                                          Format:    "XY",
                                                                                          Encoding:  DataEncoding.Hex.AsText()
                                                                                      )
                                                                                  ]
                                                            )
                                                        ]
                                      )
                                  ]
               );

        #endregion

        #region (private, static) BuildChargingSession(ChargingProcess, Index)

        /// <summary>
        /// The charging session one charging process describes.
        /// </summary>
        /// <param name="ChargingProcess">A charging process.</param>
        /// <param name="Index">Which charging process of the document this is.</param>
        private static ChargingSession BuildChargingSession(MennekesChargingProcess  ChargingProcess,
                                                            Int32                    Index)
        {

            var start              = ChargingProcess.MeasurementStart;
            var end                = ChargingProcess.MeasurementEnd;

            // The meter plus the pages it wrote: what makes two sessions of the
            // same meter tellable apart, and a page removed between them visible.
            var chargingSessionId  = $"{ChargingProcess.MeterId}-{start.Pagination}-{end.Pagination}";

            var measurement = new MennekesChargyMeasurement(
                                  ChargingProcess.MeterId,
                                  ChargyLib.OBIS2MeasurementName(MennekesChargingProcess.OBIS),
                                  MennekesChargingProcess.OBIS,
                                  start.Scaler,
                                  ChargingProcess,
                                  Values:          [
                                                       new MennekesMeasurementValue(start.Timestamp, start.Value, start),
                                                       new MennekesMeasurementValue(end.  Timestamp, end.  Value, end)
                                                   ],
                                  Context:         [ MeasurementContext ],
                                  Unit:            "Wh",
                                  UnitEncoded:     30,
                                  SignatureInfos:  new SignatureInfos(
                                                       Hash:            CryptoHashAlgorithm.SHA256,
                                                       Algorithm:       CryptoAlgorithm.ECC,
                                                       Curve:           ECCurve.secp192r1,
                                                       Format:          SignatureFormat.RS,
                                                       HashTruncation:  24,
                                                       Encoding:        DataEncoding.Hex
                                                   )
                              );

            return new ChargingSession(
                       chargingSessionId,
                       Context:            [ SessionContext ],
                       Begin:              start.Timestamp,
                       End:                end.  Timestamp,
                       EVSEId:             EVSEIdOf(ChargingProcess, Index),
                       EnergyMeterId:      ChargingProcess.MeterId,
                       InternalSessionId:  chargingSessionId,
                       Measurements:       [ measurement ]
                   ) {
                       AuthorizationStart = new Authorization(
                                                ChargingProcess.CustomerIdent,
                                                Timestamp: ChargingProcess.TimestampCustomerIdent
                                            )
                   };

        }

        #endregion

        #region (private, static) EVSEIdOf(ChargingProcess, Index)

        /// <summary>
        /// The identification of the EVSE a charging process came from.
        ///
        /// Mennekes calls it a metering point, and a document that names none
        /// falls back to a placeholder that is honestly generic rather than to an
        /// invented identification.
        /// </summary>
        /// <param name="ChargingProcess">A charging process.</param>
        /// <param name="Index">Which charging process of the document this is.</param>
        private static String EVSEIdOf(MennekesChargingProcess  ChargingProcess,
                                       Int32                    Index)

            => ChargingProcess.MeteringPoint
                   ?? $"DE*GEF*EVSE*MENNEKES*{Index + 1}";

        #endregion


    }

}
