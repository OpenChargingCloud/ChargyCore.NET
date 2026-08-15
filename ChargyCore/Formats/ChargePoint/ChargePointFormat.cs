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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Aegir;
using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.IO;

#endregion

namespace cloud.charging.open.chargy.Formats.ChargePoint
{

    /// <summary>
    /// The ChargePoint format.
    ///
    /// Unlike the meter formats, ChargePoint does not sign individual readings. It
    /// signs the whole session record as one file — the bytes of "secrrct", and its
    /// signature alongside in "secrrct.sign" — so what is verified here is the
    /// document, not the numbers inside it. That is why the start and stop readings
    /// carry no signatures of their own and are reported as start and stop values
    /// rather than as valid or invalid ones: they are as good as the document, and
    /// no better.
    ///
    /// Two shapes of that document exist. The older one is an invoice, with tariffs
    /// and parking periods and the meter readings tucked into "additional_info".
    /// The newer one is a plain session record. Both are recognised by the fields
    /// they carry, because neither declares what it is.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    public class ChargePointFormat(I18NDictionary I18N) : IJSONChargeTransparencyFormat
    {

        #region Data

        /// <summary>The JSON-LD context of a ChargePoint charging session.</summary>
        public const String SessionContext      = "https://open.charging.cloud/contexts/SessionSignatureFormats/ChargePointCrypt01+json";

        /// <summary>The JSON-LD context of a ChargePoint measurement.</summary>
        public const String MeasurementContext  = "https://open.charging.cloud/contexts/EnergyMeterSignatureFormats/ChargePointCrypt01+json";

        private readonly I18NDictionary i18n = I18N;

        #endregion

        #region Properties

        /// <summary>The name of the data format.</summary>
        public String Name
            => "ChargePoint";

        #endregion


        #region TryParseJSON(JSON)

        /// <summary>
        /// Try to read a charge transparency record from a ChargePoint document.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        public Object TryParseJSON(JObject JSON)
        {

            try
            {

                // Neither shape names itself, so each is recognised by the fields
                // it carries. The invoice is tried first because its session record
                // hides inside "additional_info" and would otherwise be missed.
                if (JSON["company_name"]    is not null &&
                    JSON["display_unit"]    is not null &&
                    JSON["minMaxAdj"]       is not null &&
                    JSON["subtotal"]        is not null &&
                    JSON["totalAmount"]     is not null &&
                    JSON["additional_info"] is JObject additionalInfo)
                {
                    return ParseInvoice(JSON, additionalInfo);
                }

                if (JSON["outlet"]             is not null &&
                    JSON["session_id"]         is not null &&
                    JSON["station_mac"]        is not null &&
                    JSON["driver_info"]        is not null &&
                    JSON["meter_serial"]       is not null &&
                    JSON["meter_startreading"] is not null &&
                    JSON["meter_endreading"]   is not null &&
                    JSON["total_energy"]       is not null &&
                    JSON["energy_units"]       is not null &&
                    JSON["start_time"]         is not null &&
                    JSON["end_time"]           is not null)
                {
                    return ParseSessionRecord(JSON);
                }

                return Invalid("UnknownOrInvalidChargingSessionFormat");

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


        #region (private) ParseSessionRecord(JSON)

        /// <summary>
        /// Read the plain session record — the shape a charging station ships as
        /// "secrrct".
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        private ChargeTransparencyRecord ParseSessionRecord(JObject JSON)
        {

            var stationMAC   = Text  (JSON, "station_mac")  ?? "";
            var outlet       = Number(JSON, "outlet");
            var sessionId    = Number(JSON, "session_id");
            var driverInfo   = Text  (JSON, "driver_info")  ?? "";
            var meterSerial  = Text  (JSON, "meter_serial") ?? "";
            var energyUnits  = Text  (JSON, "energy_units") ?? "";
            var startTime    = Number(JSON, "start_time");
            var endTime      = Number(JSON, "end_time");
            var evseId       = Text  (JSON, "EVSEId") ?? $"{stationMAC}-{FormatNumber(outlet)}";

            var begin        = ISO8601(startTime);
            var end          = ISO8601(endTime);

            return BuildRecord(
                       JSON,
                       StationMAC:    stationMAC,
                       EVSEId:        evseId,
                       Outlet:        outlet,
                       SessionId:     sessionId,
                       DriverInfo:    driverInfo,
                       MeterSerial:   meterSerial,
                       EnergyUnits:   energyUnits,
                       Begin:         begin,
                       End:           end,
                       ValueBegin:    begin,
                       ValueEnd:      end,
                       StartReading:  Number(JSON, "meter_startreading"),
                       EndReading:    Number(JSON, "meter_endreading"),
                       Currency:      null,
                       Parking:       null
                   );

        }

        #endregion

        #region (private) ParseInvoice      (JSON, AdditionalInfo)

        /// <summary>
        /// Read the older shape, which is an invoice with the session record inside
        /// it.
        ///
        /// The charging session's span comes from the invoice's line items rather
        /// than from a field of its own: whichever of the energy and parking items
        /// started earliest opens the session.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        /// <param name="AdditionalInfo">The "additional_info" object, which holds the session record.</param>
        private ChargeTransparencyRecord ParseInvoice(JObject  JSON,
                                                      JObject  AdditionalInfo)
        {

            #region When did the charging, and the parking, actually happen?

            var (chargingStart, chargingEnd)  = SpanOf(JSON["energy"]  as JArray);
            var (parkingStart,  parkingEnd)   = SpanOf(JSON["parking"] as JArray);

            // Sometimes an invoice has parking but no energy at all.
            chargingStart ??= parkingStart;
            chargingEnd   ??= parkingEnd;

            var sessionStart = parkingStart.HasValue && (!chargingStart.HasValue || parkingStart < chargingStart)
                                   ? parkingStart
                                   : chargingStart;

            var sessionEnd   = parkingEnd.HasValue && (!chargingEnd.HasValue || parkingEnd < chargingEnd)
                                   ? parkingEnd
                                   : chargingEnd;

            #endregion

            var stationMAC   = Text  (AdditionalInfo, "station_mac")   ?? "";
            var outlet       = Number(AdditionalInfo, "outlet");
            var evseId       = Text  (JSON, "EVSEId") ?? $"{stationMAC}-{FormatNumber(outlet)}";
            var currency     = Text  (AdditionalInfo, "currency_code") ?? "";

            return BuildRecord(
                       JSON,
                       StationMAC:    stationMAC,
                       EVSEId:        evseId,
                       Outlet:        outlet,
                       SessionId:     Number(AdditionalInfo, "session_id"),
                       DriverInfo:    Text  (AdditionalInfo, "driver_info")  ?? "",
                       MeterSerial:   Text  (AdditionalInfo, "meter_serial") ?? "",
                       EnergyUnits:   Text  (AdditionalInfo, "energy_units") ?? "",
                       Begin:         ISO8601(sessionStart),
                       End:           ISO8601(sessionEnd),
                       ValueBegin:    ISO8601(chargingStart),
                       ValueEnd:      ISO8601(chargingEnd),
                       StartReading:  Number(AdditionalInfo, "meter_startreading"),
                       EndReading:    Number(AdditionalInfo, "meter_endreading"),
                       OperatorId:    Text  (JSON, "company_name"),
                       Currency:      currency,
                       Parking:       JSON["parking"] as JArray
                   );

        }

        #endregion

        #region (private) BuildRecord(...)

        /// <summary>
        /// Turn either shape into a charge transparency record.
        /// </summary>
        private ChargeTransparencyRecord BuildRecord(JObject   JSON,
                                                     String    StationMAC,
                                                     String    EVSEId,
                                                     Decimal?  Outlet,
                                                     Decimal?  SessionId,
                                                     String    DriverInfo,
                                                     String    MeterSerial,
                                                     String    EnergyUnits,
                                                     String    Begin,
                                                     String    End,
                                                     String    ValueBegin,
                                                     String    ValueEnd,
                                                     Decimal?  StartReading,
                                                     Decimal?  EndReading,
                                                     String?   Currency,
                                                     JArray?   Parking,
                                                     String?   OperatorId = null)
        {

            var chargingSessionId = $"{StationMAC}-{FormatNumber(Outlet)}-{FormatNumber(SessionId)}";

            #region The measurement, whose readings ChargePoint does not sign individually

            var measurement = new Measurement(
                                  MeterSerial,
                                  "Bezogene Energiemenge",
                                  "1-0:1.8.0",
                                  0,
                                  Values:   [
                                                new MeasurementValue(ValueBegin, StartReading ?? 0),
                                                new MeasurementValue(ValueEnd,   EndReading   ?? 0)
                                            ],
                                  Context:  [ MeasurementContext ],
                                  Unit:     EnergyUnits
                              );

            #endregion

            var chargingSession = new ChargingSession(
                                      chargingSessionId,
                                      Context:    [ SessionContext ],
                                      Begin:      Begin,
                                      End:        End,
                                      EVSEId:     EVSEId,
                                      // The document is what was signed, verbatim,
                                      // so it travels along base64 encoded: parsing
                                      // and re-serialising it would destroy the very
                                      // bytes the signature covers.
                                      Original:   Text(JSON, "original"),
                                      Signature:  SignatureOf(JSON["signature"]),
                                      Measurements: [ measurement ]
                                  ) {
                                      AuthorizationStart        = new Authorization(DriverInfo, Type: "userId"),
                                      ChargingProductRelevance  = new ChargingProductRelevance(
                                                                      Time:        InformationRelevance.Informative,
                                                                      Energy:      InformationRelevance.Important,
                                                                      Parking:     InformationRelevance.Informative,
                                                                      SessionFee:  InformationRelevance.Informative
                                                                  )
                                  };

            #region The parking periods an invoice lists, minus its own subtotal line

            foreach (var parkingItem in Parking?.OfType<JObject>() ?? [])
                if (Text(parkingItem, "seq_num") != "SUBTOTAL")
                    chargingSession.AddParking(
                        new Parking(
                            "-",
                            ISO8601(Number(parkingItem, "start_time_utc")),
                            End:       ISO8601(Number(parkingItem, "end_time_utc")),
                            Overstay:  Number(parkingItem, "overstay") == 1
                        )
                    );

            #endregion

            var record = new ChargeTransparencyRecord(
                             $"chargepoint-{chargingSessionId}",
                             [ "https://open.charging.cloud/contexts/CTR+json" ],
                             Begin,
                             End,
                             Certainty:  1,
                             Status:     SessionVerificationResult.Unvalidated
                         );

            record.AddChargingStationOperator(
                BuildChargingStationOperator(
                    JSON,
                    OperatorId ?? "chargepoint",
                    StationMAC,
                    EVSEId,
                    MeterSerial,
                    Currency
                )
            );

            record.AddChargingSession(chargingSession);
            record.AddContract(new Contract(DriverInfo, Type: "userId"));

            return record;

        }

        #endregion

        #region (private, static) BuildChargingStationOperator(...)

        /// <summary>
        /// The ChargePoint charging station operator.
        ///
        /// None of this is in the file. It is what is known about ChargePoint
        /// itself, and it is written out here rather than read — which is the
        /// distinction an EV driver needs: the meter readings are a claim by the
        /// charging station, the support hotline is a claim by this software.
        /// </summary>
        private static ChargingStationOperator BuildChargingStationOperator(JObject  JSON,
                                                                            String   OperatorId,
                                                                            String   StationMAC,
                                                                            String   EVSEId,
                                                                            String   MeterSerial,
                                                                            String?  Currency)
        {

            var taxes = Currency is not null
                            ? new[] { new Taxes("MwSt", Percentage: 19.0M) }
                            : null;

            return new ChargingStationOperator(

                       OperatorId,

                       Contact:      new Contact(
                                         EMail:    "sales@chargepoint.com",
                                         Web:      "https://www.chargepoint.com",
                                         LogoURL:  "https://www.chargepoint.com/themes/chargepoint/logo.svg"
                                     ),

                       Support:      new Support(
                                         EMail:    "support.eu@chargepoint.com",
                                         Hotline:  "+49(69) 95307383",
                                         Web:      "https://chargepoint.charging.cloud/issues"
                                     ),

                       Privacy:      new PrivacyContact(
                                         Contact:  "chargepoint, Attn: Data Protection Officer, ChargePoint Network (Netherlands) B.V., Hoogoorddreef 56E, 1101BE Amsterdam",
                                         EMail:    "privacy.eu@chargepoint.com ",
                                         Web:      "https://de.chargepoint.com/privacy_policy"
                                     ),

                       Description:  I18NString.Create(Languages.de, "chargepoint - Charging Station Operator Services"),

                       ChargingStations: [
                           new ChargingStation(
                               StationMAC,
                               // Neither the geographical location nor the address
                               // is validated yet; they are taken as the file
                               // states them.
                               Address:      AddressOf    (JSON["address"]     as JObject),
                               GeoLocation:  GeoLocationOf(JSON["geoLocation"] as JObject),
                               EVSEs:        [
                                                 new EVSE(
                                                     EVSEId,
                                                     EnergyMeters: [
                                                                       new EnergyMeter(
                                                                           MeterSerial,
                                                                           Manufacturer:  new Manufacturer(
                                                                                              "Carlo Gavazzi",
                                                                                              Contact: new Contact(Web: "https://www.gavazziautomation.com")
                                                                                          ),
                                                                           Model:         new DeviceModel(
                                                                                              "EM340-DIN.AV2.3.X.S1.X",
                                                                                              "https://www.gavazziautomation.com/fileadmin/images/PIM/DATASHEET/ENG/EM340_DS_ENG.pdf"
                                                                                          )
                                                                       )
                                                                   ]
                                                 )
                                             ]
                           )
                       ],

                       ChargingTariffs: Currency is not null
                                            ? [ new ChargingTariff("default", Currency: Currency, Taxes: taxes) ]
                                            : null,

                       ParkingTariffs:  Currency is not null
                                            ? [ new ParkingTariff ("default", Currency: Currency, Taxes: taxes) ]
                                            : null

                   );

        }

        #endregion


        #region (private) Invalid(MessageKey)

        /// <summary>
        /// Report that the data is not a valid ChargePoint charging session.
        /// </summary>
        /// <param name="MessageKey">The i18n key of the reason.</param>
        private SessionCryptoResult Invalid(String MessageKey)

            => new (
                   SessionVerificationResult.InvalidSessionFormat,
                   i18n.GetMultilanguageText(MessageKey)
               );

        #endregion

        #region (private, static) Helpers

        /// <summary>
        /// The earliest start and the latest start's end among a list of invoice
        /// line items.
        /// </summary>
        private static (Decimal? Start, Decimal? End) SpanOf(JArray? Items)
        {

            Decimal? start = null;
            Decimal? end   = null;

            foreach (var item in Items?.OfType<JObject>() ?? [])
            {

                var itemStart = Number(item, "start_time_utc");
                var itemEnd   = Number(item, "end_time_utc");

                if (!itemStart.HasValue)
                    continue;

                if (!start.HasValue || itemStart < start)
                    start = itemStart;

                // Note: the end follows the *latest starting* item, not the latest
                // end. That is what the reference implementation does, and with
                // line items that do not overlap it comes to the same thing.
                if (!end.HasValue || itemStart > end)
                    end = itemEnd;

            }

            return (start, end);

        }

        /// <summary>
        /// The signature a ChargePoint document carries, which is DER encoded
        /// hexadecimal, or already taken apart into r and s.
        /// </summary>
        private static Signature? SignatureOf(JToken? JSON)
        {

            if (JSON?.Type == JTokenType.String)
                return new Signature(JSON.Value<String>());

            if (JSON is JObject signature &&
                Text(signature, "r") is String r &&
                Text(signature, "s") is String s)
            {
                return new SignatureRS(r, s);
            }

            return null;

        }

        /// <summary>The address a document states, if any.</summary>
        private static Address? AddressOf(JObject? JSON)

            => JSON is not null
                   ? new Address(
                         Street:      Text(JSON, "street"),
                         PostalCode:  Text(JSON, "postalCode"),
                         City:        Text(JSON, "city"),
                         Country:     Text(JSON, "country")
                     )
                   : null;

        /// <summary>The geographical location a document states, if any.</summary>
        private static GeoCoordinate? GeoLocationOf(JObject? JSON)
        {

            var latitude   = Number(JSON, "lat");
            var longitude  = Number(JSON, "lng") ?? Number(JSON, "lon");

            return latitude.HasValue && longitude.HasValue
                       ? GeoCoordinate.Create(
                             Latitude. Parse((Double) latitude.Value),
                             Longitude.Parse((Double) longitude.Value)
                         )
                       : null;

        }

        /// <summary>
        /// A UNIX timestamp as an ISO 8601 string in UTC, without milliseconds —
        /// which is how "moment().utc().format()" writes it.
        /// </summary>
        private static String ISO8601(Decimal? Seconds)

            => Seconds.HasValue
                   ? DateTimeOffset.FromUnixTimeSeconds((Int64) Seconds.Value).
                                    UtcDateTime.
                                    ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)
                   : "";

        /// <summary>A number as it appears in an identification, without a decimal point.</summary>
        private static String FormatNumber(Decimal? Value)

            => Value.HasValue
                   ? ((Int64) Value.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)
                   : "";

        /// <summary>A string property, or null when it is absent or not a string.</summary>
        private static String? Text(JObject?  JSON,
                                    String    Key)

            => JSON?[Key]?.Type == JTokenType.String
                   ? JSON[Key]!.Value<String>()
                   : null;

        /// <summary>A numeric property, or null when it is absent or not a number.</summary>
        private static Decimal? Number(JObject?  JSON,
                                       String    Key)

            => JSON?[Key]?.Type == JTokenType.Integer ||
               JSON?[Key]?.Type == JTokenType.Float
                   ? JSON[Key]!.Value<Decimal>()
                   : null;

        #endregion


    }

}
