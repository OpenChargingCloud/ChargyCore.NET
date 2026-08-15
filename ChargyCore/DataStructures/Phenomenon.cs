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

#endregion

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// One physical quantity within a measurement that reports several at once.
    ///
    /// A BSM snapshot signs the energy delivered since the charging session began,
    /// the meter's lifetime total and the momentary power — three different
    /// quantities under one signature. They cannot be split into three
    /// measurements, because the signature covers them together; and the group
    /// cannot be named after any one of them without misdescribing the others. So
    /// each names itself here, and <see cref="Value"/> says which field of the
    /// reading it refers to.
    /// </summary>
    /// <param name="Name">The name of the quantity, e.g. "Total Real Power".</param>
    /// <param name="Value">Which field of a reading holds it, e.g. "TotWhImp".</param>
    /// <param name="OBIS">The OBIS number of the quantity.</param>
    /// <param name="Unit">The unit of the quantity, e.g. "Wh".</param>
    /// <param name="UnitEncoded">The unit as a DLMS/COSEM code.</param>
    /// <param name="ValueType">How the value is encoded, e.g. "UnsignedInteger32".</param>
    /// <param name="Scale">The power of ten the values are scaled by.</param>
    /// <param name="DisplayPrefix">An optional display scaling.</param>
    /// <param name="DisplayPrecision">An optional number of decimal places to display.</param>
    public class Phenomenon(String?         Name,
                            String?         Value,
                            String?         OBIS              = null,
                            String?         Unit              = null,
                            UInt16?         UnitEncoded       = null,
                            String?         ValueType         = null,
                            Int32?          Scale             = null,
                            DisplayPrefix?  DisplayPrefix     = null,
                            UInt16?         DisplayPrecision  = null)
    {

        #region Properties

        /// <summary>The name of the quantity.</summary>
        public String?         Name                { get; } = Name;

        /// <summary>Which field of a reading holds it.</summary>
        public String?         Value               { get; } = Value;

        /// <summary>The OBIS number of the quantity.</summary>
        public String?         OBIS                { get; } = OBIS;

        /// <summary>The unit of the quantity.</summary>
        public String?         Unit                { get; } = Unit;

        /// <summary>The unit as a DLMS/COSEM code.</summary>
        public UInt16?         UnitEncoded         { get; } = UnitEncoded;

        /// <summary>How the value is encoded.</summary>
        public String?         ValueType           { get; } = ValueType;

        /// <summary>The power of ten the values are scaled by.</summary>
        public Int32?          Scale               { get; } = Scale;

        /// <summary>An optional display scaling.</summary>
        public DisplayPrefix?  DisplayPrefix       { get; } = DisplayPrefix;

        /// <summary>An optional number of decimal places to display.</summary>
        public UInt16?         DisplayPrecision    { get; } = DisplayPrecision;

        #endregion


        #region (static) Parse(JSON)

        /// <summary>
        /// Parse the given JSON as a phenomenon.
        /// </summary>
        /// <param name="JSON">A JSON representation of a phenomenon.</param>
        public static Phenomenon Parse(JObject JSON)

            => new (
                   JSON["name"]?.             Value<String>(),
                   JSON["value"]?.            Value<String>(),
                   JSON["obis"]?.             Value<String>(),
                   JSON["unit"]?.             Value<String>(),
                   JSON["unitEncoded"]?.      Value<UInt16>(),
                   JSON["valueType"]?.        Value<String>(),
                   JSON["scale"]?.            Value<Int32>(),
                   JSON["formatPrefix"]?.Type == JTokenType.Integer &&
                   Enum.IsDefined((DisplayPrefix) JSON["formatPrefix"]!.Value<Int32>())
                       ? (DisplayPrefix) JSON["formatPrefix"]!.Value<Int32>()
                       : null,
                   JSON["formatPrecision"]?.  Value<UInt16>()
               );

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this phenomenon.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Name          is not null)
                json.Add(new JProperty("name",             Name));

            if (OBIS          is not null)
                json.Add(new JProperty("obis",             OBIS));

            if (Unit          is not null)
                json.Add(new JProperty("unit",             Unit));

            if (UnitEncoded.HasValue)
                json.Add(new JProperty("unitEncoded",      UnitEncoded.Value));

            if (ValueType     is not null)
                json.Add(new JProperty("valueType",        ValueType));

            if (Value         is not null)
                json.Add(new JProperty("value",            Value));

            if (Scale.HasValue)
                json.Add(new JProperty("scale",            Scale.Value));

            if (DisplayPrefix.HasValue)
                json.Add(new JProperty("formatPrefix",     (Int32) DisplayPrefix.Value));

            if (DisplayPrecision.HasValue)
                json.Add(new JProperty("formatPrecision",  DisplayPrecision.Value));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this phenomenon.
        /// </summary>
        public override String ToString()

            => $"{Name} ({OBIS})";

        #endregion

    }

}
