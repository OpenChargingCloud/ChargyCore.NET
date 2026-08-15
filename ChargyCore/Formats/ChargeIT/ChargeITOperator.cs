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

using org.GraphDefined.Vanaheimr.Aegir;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy.Formats.ChargeIT
{

    /// <summary>
    /// The charging station operator a chargeIT container implies.
    ///
    /// A chargeIT file names neither its operator nor how to reach them — it only
    /// says which EVSE the readings came from. Everything else is filled in from
    /// what is known about chargeIT mobility itself, which is why it is written
    /// out here rather than read from the file.
    ///
    /// That distinction matters to an EV driver: the meter readings are a claim by
    /// the meter, the address is a claim by whoever wrote the file, and the
    /// support hotline below is a claim by this software. Only the first of the
    /// three is signed.
    /// </summary>
    public static class ChargeITOperator
    {

        #region Data

        /// <summary>The identification Chargy gives the chargeIT mobility operator.</summary>
        public const String Id = "chargeITmobilityCSO";

        #endregion

        #region Build(ChargingStation, ChargingTariffs = null)

        /// <summary>
        /// The chargeIT mobility charging station operator, around the given
        /// charging station.
        /// </summary>
        /// <param name="ChargingStation">The charging station the readings came from.</param>
        /// <param name="ChargingTariffs">The charging tariffs the container declared, if any.</param>
        public static ChargingStationOperator Build(ChargingStation                ChargingStation,
                                                    IEnumerable<ChargingTariff>?   ChargingTariffs = null)

            => new (
                   Id,

                   Contact:      new Contact(
                                     EMail:       "info@chargeit-mobility.com",
                                     Web:         "https://www.chargeit-mobility.com",
                                     LogoURL:     "http://www.chargeit-mobility.com/fileadmin/BELECTRIC_Drive/templates/pics/chargeit_logo_408x70.png",
                                     PublicKeys:  [
                                                      new PublicKey(
                                                          "042313b9e469612b4ca06981bfdecb226e234632b01d84b6a814f63a114b7762c34ddce2e6853395b7a0f87275f63ffe3c",
                                                          new OIDInfo("secp192r1"),
                                                          Format:    "DER",
                                                          Encoding:  "hex"
                                                      ),
                                                      new PublicKey(
                                                          "04a8ff0d82107922522e004a167cc658f0eef408c5020f98e7a2615be326e61852666877335f4f8d9a0a756c26f0c9fb3f401431416abb5317cc0f5d714d3026fe",
                                                          new OIDInfo("secp256k1"),
                                                          Format:    "DER",
                                                          Encoding:  "hex"
                                                      )
                                                  ]
                                 ),

                   Support:      new Support(
                                     EMail:    "service@chargeit-mobility.com",
                                     Hotline:  "+49 9321 / 2680 - 700",
                                     Web:      "https://cso.chargeit.charging.cloud/issues"
                                 ),

                   Privacy:      new PrivacyContact(
                                     Contact:  "Dr. iur. Christian Borchers, datenschutz süd GmbH",
                                     EMail:    "datenschutz@chargeit-mobility.com",
                                     Web:      "http://www.chargeit-mobility.com/de/datenschutz/"
                                 ),

                   Description:  I18NString.Create(Languages.de, "chargeIT mobility GmbH - Charging Station Operator Services"),

                   ChargingStations:  [ ChargingStation ],
                   ChargingTariffs:   ChargingTariffs
               );

        #endregion

        #region ChargingStationIdOf(EVSEId)

        /// <summary>
        /// The identification of the charging station an EVSE belongs to.
        ///
        /// A chargeIT container names only the EVSE, and an EVSE identification
        /// ends in the connector: everything before the last separator is the
        /// station.
        /// </summary>
        /// <param name="EVSEId">The identification of an EVSE.</param>
        public static String ChargingStationIdOf(String EVSEId)
        {

            var separator = EVSEId.LastIndexOf('*');

            return separator > 0
                       ? EVSEId[..separator]
                       : EVSEId;

        }

        #endregion

        #region BuildChargingStation(EVSEId, Address, GeoLocation, Firmware = null, EnergyMeters = null)

        /// <summary>
        /// The charging station a chargeIT container describes.
        /// </summary>
        /// <param name="EVSEId">The identification of the EVSE the readings came from.</param>
        /// <param name="Address">Where the charging station stands.</param>
        /// <param name="GeoLocation">Where the charging station stands, exactly.</param>
        /// <param name="Firmware">The software the charging station runs, if the container said.</param>
        /// <param name="EnergyMeters">The energy meters installed in the EVSE.</param>
        public static ChargingStation BuildChargingStation(String                    EVSEId,
                                                           Address?                  Address,
                                                           GeoCoordinate?            GeoLocation,
                                                           Firmware?                 Firmware      = null,
                                                           IEnumerable<EnergyMeter>? EnergyMeters  = null)

            => new (
                   ChargingStationIdOf(EVSEId),
                   Firmware:     Firmware,
                   Address:      Address,
                   GeoLocation:  GeoLocation,
                   EVSEs:        [
                                     new EVSE(
                                         EVSEId,
                                         EnergyMeters: EnergyMeters
                                     )
                                 ]
               );

        #endregion

    }

}
