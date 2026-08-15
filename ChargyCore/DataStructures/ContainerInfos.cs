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

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// What a container knows that the signed data inside it does not.
    ///
    /// A signed meter value says which meter produced it and what it read, and
    /// nothing else — deliberately, because everything the meter signs is
    /// everything the meter can vouch for. Where the charging station stands, what
    /// its EVSE is called, which tariff applied: none of that is signed, and none
    /// of it can be. It comes from the container the signed data travelled in, and
    /// Chargy keeps the distinction, because an EV driver deserves to know which
    /// half of what they are shown is cryptographically proven.
    /// </summary>
    public class ContainerInfos
    {

        #region Data

        private readonly List<ChargingStation>  chargingStations  = [];
        private readonly List<Warning>          warnings          = [];

        #endregion

        #region Properties

        /// <summary>The charging stations the container described.</summary>
        public IReadOnlyList<ChargingStation>  ChargingStations
            => chargingStations;

        /// <summary>Everything about the container that looked wrong but was not fatal.</summary>
        public IReadOnlyList<Warning>          Warnings
            => warnings;

        /// <summary>The identification of the charging session, when the container named one.</summary>
        public String?                         ChargingSessionId    { get; set; }

        /// <summary>The charging tariffs the container described.</summary>
        public IReadOnlyList<ChargingTariff>?  ChargingTariffs      { get; set; }

        /// <summary>The energy meter the container described.</summary>
        public EnergyMeter?                    EnergyMeter          { get; set; }

        #endregion


        #region AddChargingStation(ChargingStation)

        /// <summary>
        /// Add a charging station the container described.
        /// </summary>
        /// <param name="ChargingStation">A charging station.</param>
        public ContainerInfos AddChargingStation(ChargingStation ChargingStation)
        {
            chargingStations.Add(ChargingStation);
            return this;
        }

        #endregion

        #region AddWarning        (Warning)

        /// <summary>
        /// Record something about the container that looked wrong but was not fatal.
        /// </summary>
        /// <param name="Warning">A warning.</param>
        public ContainerInfos AddWarning(Warning Warning)
        {
            warnings.Add(Warning);
            return this;
        }

        #endregion


        #region FirstChargingStationId / FirstEVSEId

        /// <summary>
        /// The identification of the first charging station the container described.
        /// </summary>
        public String? FirstChargingStationId

            => chargingStations.Count > 0
                   ? chargingStations[0].Id
                   : null;

        /// <summary>
        /// The identification of the first EVSE the container described.
        /// </summary>
        public String? FirstEVSEId

            => chargingStations.Count > 0 &&
               chargingStations[0].EVSEs.Count > 0
                   ? chargingStations[0].EVSEs[0].Id
                   : null;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of these container infos.
        /// </summary>
        public override String ToString()

            => $"{chargingStations.Count} charging station(s), {warnings.Count} warning(s)";

        #endregion


    }

}
