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

namespace cloud.charging.open.chargy.Formats.Alfen
{

    /// <summary>
    /// A measurement produced by an Alfen charging station.
    ///
    /// Between the energy meter and the charging station sits a signing adapter —
    /// the meter itself does not sign. The adapter's identity, firmware version
    /// and firmware checksum are therefore part of what gets signed: they say
    /// which piece of hardware vouched for the reading, and a changed firmware
    /// checksum is a changed witness.
    /// </summary>
    /// <param name="EnergyMeterId">The identification of the energy meter.</param>
    /// <param name="Name">The name of the measurement, e.g. "ENERGY_TOTAL".</param>
    /// <param name="OBIS">The OBIS code of the measurement.</param>
    /// <param name="Scale">The scale of the measured values.</param>
    /// <param name="AdapterId">The identification of the signing adapter.</param>
    /// <param name="AdapterFWVersion">The firmware version of the signing adapter.</param>
    /// <param name="AdapterFWChecksum">The firmware checksum of the signing adapter.</param>
    /// <param name="Values">The measured values.</param>
    /// <param name="Context">The JSON-LD context of the measurement.</param>
    /// <param name="UnitEncoded">The unit of the measured values, as a DLMS/COSEM code.</param>
    /// <param name="SignatureInfos">How the values were signed.</param>
    public class AlfenMeasurement(String                          EnergyMeterId,
                                  String                          Name,
                                  String                          OBIS,
                                  Int32                           Scale,
                                  String                          AdapterId,
                                  String                          AdapterFWVersion,
                                  String                          AdapterFWChecksum,
                                  IEnumerable<MeasurementValue>?  Values          = null,
                                  IEnumerable<String>?            Context         = null,
                                  UInt16?                         UnitEncoded     = null,
                                  SignatureInfos?                 SignatureInfos  = null)

        : Measurement(EnergyMeterId,
                      Name,
                      OBIS,
                      Scale,
                      Values,
                      Context,
                      UnitEncoded:     UnitEncoded,
                      SignatureInfos:  SignatureInfos)

    {

        #region Properties

        /// <summary>The identification of the signing adapter.</summary>
        public String  AdapterId            { get; } = AdapterId;

        /// <summary>The firmware version of the signing adapter.</summary>
        public String  AdapterFWVersion     { get; } = AdapterFWVersion;

        /// <summary>The firmware checksum of the signing adapter.</summary>
        public String  AdapterFWChecksum    { get; } = AdapterFWChecksum;

        #endregion

    }

}
