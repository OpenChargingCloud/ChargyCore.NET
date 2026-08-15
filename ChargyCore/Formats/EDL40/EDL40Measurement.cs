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

namespace cloud.charging.open.chargy.Formats.EDL40
{

    /// <summary>
    /// A measurement produced by an EDL40 or ISA energy meter.
    /// </summary>
    /// <param name="EnergyMeterId">The identification of the energy meter.</param>
    /// <param name="Name">The name of the measurement, e.g. "ENERGY_TOTAL".</param>
    /// <param name="OBIS">The OBIS code of the measurement.</param>
    /// <param name="Scale">The scale of the measured values.</param>
    /// <param name="ServerId">The identification of the meter as the SML message spells it.</param>
    /// <param name="PublicKey">The public key of the meter, hexadecimal.</param>
    /// <param name="Variant">Which of the two SML layouts the readings came in.</param>
    /// <param name="Curve">The elliptic curve the signatures live on.</param>
    /// <param name="Values">The measured values.</param>
    /// <param name="Context">The JSON-LD context of the measurement.</param>
    /// <param name="Unit">The unit of the measured values.</param>
    /// <param name="UnitEncoded">The unit of the measured values, as a DLMS/COSEM code.</param>
    /// <param name="SignatureInfos">How the values were signed.</param>
    public class EDL40Measurement(String                          EnergyMeterId,
                                  String                          Name,
                                  String                          OBIS,
                                  Int32                           Scale,
                                  String                          ServerId,
                                  String                          PublicKey,
                                  EDL40Variant                    Variant,
                                  ECCurve                         Curve,
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

        /// <summary>The identification of the meter as the SML message spells it.</summary>
        public String        ServerId     { get; } = ServerId;

        /// <summary>The public key of the meter, hexadecimal.</summary>
        public String        PublicKey    { get; } = PublicKey;

        /// <summary>Which of the two SML layouts the readings came in.</summary>
        public EDL40Variant  Variant      { get; } = Variant;

        /// <summary>The elliptic curve the signatures live on.</summary>
        public ECCurve       Curve        { get; } = Curve;

        #endregion

    }


    /// <summary>
    /// One reading of an EDL40 or ISA energy meter, together with the document it
    /// was read out of.
    ///
    /// An ISA document carries two readings — a start and a stop — so two of these
    /// can share one document, and they then share its verdict: the signature
    /// covers the pair, and neither half of it can be true on its own.
    /// </summary>
    /// <param name="Timestamp">When the value was measured, as an ISO 8601 string.</param>
    /// <param name="Value">The measured value, in kWh.</param>
    /// <param name="Document">The document this reading was read out of.</param>
    /// <param name="Signatures">The signatures over this measurement value.</param>
    /// <param name="StatusMeter">The status word of the energy meter.</param>
    /// <param name="PaginationId">The pagination counter of the energy meter.</param>
    public class EDL40MeasurementValue(String                   Timestamp,
                                       Decimal                  Value,
                                       EDL40Document            Document,
                                       IEnumerable<Signature>?  Signatures    = null,
                                       String?                  StatusMeter   = null,
                                       String?                  PaginationId  = null)

        : MeasurementValue(Timestamp,
                           Value,
                           Signatures,
                           StatusMeter:   StatusMeter,
                           PaginationId:  PaginationId)

    {

        #region Properties

        /// <summary>The document this reading was read out of.</summary>
        public EDL40Document Document { get; } = Document;

        #endregion

    }

}
