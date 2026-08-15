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

namespace cloud.charging.open.chargy.Formats.Mennekes
{

    /// <summary>
    /// A measurement produced by a Mennekes EDL40 charging station.
    ///
    /// The charging process the readings came from travels with the measurement,
    /// because verifying one of them needs the whole of it: the meter's own
    /// identification, the token the driver authorized with and the moment they
    /// did are all part of what the meter signed, and none of them lives on the
    /// reading itself.
    /// </summary>
    /// <param name="EnergyMeterId">The identification of the energy meter.</param>
    /// <param name="Name">The name of the measurement, e.g. "ENERGY_TOTAL".</param>
    /// <param name="OBIS">The OBIS code of the measurement.</param>
    /// <param name="Scale">The scale of the measured values.</param>
    /// <param name="ChargingProcess">The charging process the readings came from.</param>
    /// <param name="Values">The measured values.</param>
    /// <param name="Context">The JSON-LD context of the measurement.</param>
    /// <param name="Unit">The unit of the measured values.</param>
    /// <param name="UnitEncoded">The unit of the measured values, as a DLMS/COSEM code.</param>
    /// <param name="SignatureInfos">How the values were signed.</param>
    public class MennekesChargyMeasurement(String                          EnergyMeterId,
                                           String                          Name,
                                           String                          OBIS,
                                           Int32                           Scale,
                                           MennekesChargingProcess         ChargingProcess,
                                           IEnumerable<MeasurementValue>?  Values          = null,
                                           IEnumerable<String>?            Context         = null,
                                           String?                         Unit            = null,
                                           UInt16?                         UnitEncoded     = null,
                                           SignatureInfos?                 SignatureInfos  = null)

        : Measurement(EnergyMeterId,
                      Name,
                      OBIS,
                      Scale,
                      Values,
                      Context,
                      Unit:            Unit,
                      UnitEncoded:     UnitEncoded,
                      SignatureInfos:  SignatureInfos)

    {

        #region Properties

        /// <summary>The charging process the readings came from.</summary>
        public MennekesChargingProcess  ChargingProcess    { get; } = ChargingProcess;

        #endregion

    }


    /// <summary>
    /// One signed reading of a Mennekes EDL40 charging station.
    /// </summary>
    /// <param name="Timestamp">When the value was measured, as an ISO 8601 string.</param>
    /// <param name="Value">The measured value.</param>
    /// <param name="Reading">The reading as the XML document spelled it.</param>
    public class MennekesMeasurementValue(String               Timestamp,
                                          Decimal              Value,
                                          MennekesMeasurement  Reading)

        : MeasurementValue(Timestamp,
                           Value,
                           [ Reading.SignatureRS ],
                           StatusMeter:   Reading.MeterStatus.ToString(),
                           SecondsIndex:  Reading.SecondIndex,
                           PaginationId:  Reading.Pagination.  ToString(),
                           LogBookIndex:  Reading.EventCounter.ToString())

    {

        #region Properties

        /// <summary>
        /// The reading as the XML document spelled it.
        ///
        /// Kept whole rather than spread across the base class, because the signed
        /// block is rebuilt from it: a field that had been converted on the way in
        /// and back out on the way to the buffer is a field that can differ.
        /// </summary>
        public MennekesMeasurement  Reading    { get; } = Reading;

        #endregion

    }

}
