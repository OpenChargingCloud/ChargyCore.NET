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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy.Formats.OCMF
{

    /// <summary>
    /// What a meter states about the charging cable it compensates for.
    ///
    /// A cable has a resistance, and the energy lost in it is measured by nobody:
    /// the meter sits at the wall, the car is at the other end. A meter that
    /// compensates for that loss has to say so, and say by how much, because the
    /// number it then reports is no longer only what it measured.
    /// </summary>
    /// <param name="Resistance">The resistance the meter compensates for, OCMF "LR".</param>
    /// <param name="Unit">The unit of the resistance, OCMF "LU".</param>
    /// <param name="Name">An optional name of the compensation, OCMF "LN".</param>
    /// <param name="Identification">An optional identification of the compensation, OCMF "LI".</param>
    public class OCMFLossCompensation(Decimal   Resistance,
                                      String    Unit,
                                      String?   Name            = null,
                                      Decimal?  Identification  = null)
    {

        #region Properties

        /// <summary>The resistance the meter compensates for, OCMF "LR".</summary>
        public Decimal   Resistance        { get; } = Resistance;

        /// <summary>The unit of the resistance, OCMF "LU".</summary>
        public String    Unit              { get; } = Unit;

        /// <summary>An optional name of the compensation, OCMF "LN".</summary>
        public String?   Name              { get; } = Name;

        /// <summary>An optional identification of the compensation, OCMF "LI".</summary>
        public Decimal?  Identification    { get; } = Identification;

        #endregion


        #region (static) TryParse(JSON, out LossCompensation)

        /// <summary>
        /// Try to read the loss compensation of an OCMF payload.
        ///
        /// Both the resistance and its unit have to be there. A resistance without
        /// a unit is not a resistance, and a compensation that cannot be stated is
        /// one an EV driver cannot check.
        /// </summary>
        /// <param name="JSON">The value of an OCMF "LC" field.</param>
        /// <param name="LossCompensation">The parsed loss compensation.</param>
        public static Boolean TryParse(JObject? JSON, out OCMFLossCompensation? LossCompensation)
        {

            LossCompensation = null;

            if (JSON is null)
                return false;

            var resistance = JSON["LR"];
            var unit       = JSON["LU"];

            if (resistance is null ||
                resistance.Type is not (JTokenType.Integer or JTokenType.Float) ||
                unit       is null ||
                unit.      Type is not JTokenType.String ||
                unit.Value<String>() is not String unitName ||
                unitName.Length == 0)
            {
                return false;
            }

            LossCompensation = new OCMFLossCompensation(
                                   resistance.Value<Decimal>(),
                                   unitName,
                                   JSON["LN"]?.Type == JTokenType.String
                                       ? JSON["LN"]!.Value<String>()
                                       : null,
                                   JSON["LI"]?.Type is JTokenType.Integer or JTokenType.Float
                                       ? JSON["LI"]!.Value<Decimal>()
                                       : null
                               );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this loss compensation, in the field
        /// names the meter signed it under.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Name           is not null)
                json.Add(new JProperty("LN",  Name));

            if (Identification.HasValue)
                json.Add(new JProperty("LI",  Identification.Value));

            json.Add(new JProperty("LR",      Resistance));
            json.Add(new JProperty("LU",      Unit));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this loss compensation.
        /// </summary>
        public override String ToString()

            => $"{Resistance} {Unit}{(Name is not null ? $" ({Name})" : "")}";

        #endregion


    }


    /// <summary>
    /// Everything an OCMF payload states that a charge transparency record has no
    /// field of its own for.
    ///
    /// A charge transparency record is deliberately format-agnostic: it says which
    /// meter measured what, when, and whether the signature holds. OCMF says more
    /// than that — which gateway relayed the readings, what the meter's firmware
    /// version is, which tariff text was signed alongside them — and none of it is
    /// noise. It is what an EV driver would have to be shown to check the receipt
    /// against the standard rather than against Chargy's summary of it.
    /// </summary>
    public class OCMFInfo
    {

        #region Properties

        /// <summary>The OCMF version the document declares, OCMF "FV".</summary>
        public String?           FormatVersion                    { get; init; }

        /// <summary>What the signing gateway calls itself, OCMF "GI" or "VI".</summary>
        public String?           GatewayInformation               { get; init; }

        /// <summary>The serial number of the gateway, OCMF "GS".</summary>
        public String?           GatewaySerial                    { get; init; }

        /// <summary>The software version of the gateway, OCMF "GV" or "VV".</summary>
        public String?           GatewayVersion                   { get; init; }

        /// <summary>The manufacturer of the energy meter, OCMF "MV".</summary>
        public String?           MeterVendor                      { get; init; }

        /// <summary>The model of the energy meter, OCMF "MM".</summary>
        public String?           MeterModel                       { get; init; }

        /// <summary>The serial number of the energy meter, OCMF "MS".</summary>
        public String?           MeterSerial                      { get; init; }

        /// <summary>The firmware version of the energy meter, OCMF "MF".</summary>
        public String?           MeterFirmware                    { get; init; }

        /// <summary>The tariff, as free text, OCMF "TT".</summary>
        public String?           TariffText                       { get; init; }

        /// <summary>What the tariff text means, when it is a Bonn tariff.</summary>
        public OCMFBonnTariff?   TariffTextInterpretation         { get; init; }

        /// <summary>The firmware version of the charging controller, OCMF "CF".</summary>
        public String?           ControllerFirmwareVersion        { get; init; }

        /// <summary>What the meter compensates for the charging cable, OCMF "LC".</summary>
        public OCMFLossCompensation?  LossCompensation            { get; init; }

        /// <summary>How the charge point identification is to be read, OCMF "CT".</summary>
        public String?           ChargePointIdentificationType    { get; init; }

        /// <summary>The identification of the charge point, OCMF "CI".</summary>
        public String?           ChargePointIdentification        { get; init; }

        #endregion


        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this OCMF information.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            void Add(String Name, String? Value)
            {
                if (Value is not null)
                    json.Add(new JProperty(Name, Value));
            }

            Add("formatVersion",                  FormatVersion);
            Add("gatewayInformation",             GatewayInformation);
            Add("gatewaySerial",                  GatewaySerial);
            Add("gatewayVersion",                 GatewayVersion);
            Add("meterVendor",                    MeterVendor);
            Add("meterModel",                     MeterModel);
            Add("meterSerial",                    MeterSerial);
            Add("meterFirmware",                  MeterFirmware);
            Add("tariffText",                     TariffText);

            if (TariffTextInterpretation is not null)
                json.Add(new JProperty("tariffTextInterpretation",  TariffTextInterpretation.ToJSON()));

            Add("controllerFirmwareVersion",      ControllerFirmwareVersion);

            if (LossCompensation         is not null)
                json.Add(new JProperty("lossCompensation",          LossCompensation.        ToJSON()));

            Add("chargePointIdentificationType",  ChargePointIdentificationType);
            Add("chargePointIdentification",      ChargePointIdentification);

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this OCMF information.
        /// </summary>
        public override String ToString()

            => $"OCMF {FormatVersion ?? "?"}: {MeterVendor} {MeterModel} {MeterSerial}";

        #endregion


    }


    /// <summary>
    /// A charge transparency record that came out of OCMF documents, keeping what
    /// OCMF said beyond what every format says.
    /// </summary>
    /// <param name="Id">The identification of the charge transparency record.</param>
    /// <param name="OCMF">What the OCMF payload stated.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Begin">An optional start of the covered time span.</param>
    /// <param name="End">An optional end of the covered time span.</param>
    /// <param name="Certainty">How sure we are that this record was parsed by the right parser.</param>
    /// <param name="Status">An optional overall verification status.</param>
    public class OCMFChargeTransparencyRecord(String                      Id,
                                              OCMFInfo                    OCMF,
                                              IEnumerable<String>?        Context    = null,
                                              String?                     Begin      = null,
                                              String?                     End        = null,
                                              Double                      Certainty  = 0,
                                              SessionVerificationResult?  Status     = null)

        : ChargeTransparencyRecord(Id,
                                   Context,
                                   Begin,
                                   End,
                                   Certainty:  Certainty,
                                   Status:     Status)

    {

        #region Properties

        /// <summary>What the OCMF payload stated.</summary>
        public OCMFInfo OCMF { get; } = OCMF;

        #endregion

    }

}
