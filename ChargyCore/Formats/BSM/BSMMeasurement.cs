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

namespace cloud.charging.open.chargy.Formats.BSM
{

    /// <summary>
    /// One signed snapshot of a BAUER Electronic BSM-WS36A energy meter.
    ///
    /// The meter does not sign a single reading. It signs a snapshot of its whole
    /// state — three measured quantities, four counters, three free-text fields
    /// and an event word — as one block, in a fixed order. So every one of these
    /// fields has to be carried through the charge transparency record intact:
    /// none of them can be recomputed, and leaving one out means the block cannot
    /// be reassembled and the signature cannot be checked.
    /// </summary>
    /// <param name="Timestamp">When the snapshot was taken, as an ISO 8601 string.</param>
    /// <param name="Value">The energy delivered since the charging session began.</param>
    /// <param name="Type">Which kind of snapshot this is — start, current, end, turn on, turn off.</param>
    /// <param name="Signatures">The signature over the snapshot.</param>
    /// <param name="ValueDisplayPrefix">An optional display scaling.</param>
    /// <param name="ValueDisplayPrecision">An optional number of decimal places to display.</param>
    public class BSMMeasurementValue(String                   Timestamp,
                                     Decimal                  Value,
                                     Int64                    Type,
                                     IEnumerable<Signature>?  Signatures             = null,
                                     DisplayPrefix?           ValueDisplayPrefix     = null,
                                     UInt16?                  ValueDisplayPrecision  = null)

        : MeasurementValue(Timestamp,
                           Value,
                           Signatures,
                           ValueDisplayPrefix,
                           ValueDisplayPrecision)

    {

        #region Properties — the snapshot, field by field

        /// <summary>Which kind of snapshot this is.</summary>
        public Int64    Type              { get; }      = Type;

        /// <summary>Real energy imported since the last turn-on sequence.</summary>
        public Int64    RCR               { get; init; }

        /// <summary>The scale of <see cref="RCR"/>, as a power of ten.</summary>
        public Int32    RCRScale          { get; init; }

        /// <summary>The unit of <see cref="RCR"/>.</summary>
        public String   RCRUnit           { get; init; } = "";

        /// <summary>Total real energy imported over the meter's lifetime.</summary>
        public Int64    TotWhImp          { get; init; }

        /// <summary>The scale of <see cref="TotWhImp"/>, as a power of ten.</summary>
        public Int32    TotWhImpScale     { get; init; }

        /// <summary>The unit of <see cref="TotWhImp"/>.</summary>
        public String   TotWhImpUnit      { get; init; } = "";

        /// <summary>Total real power at the moment of the snapshot.</summary>
        public Int64    W                 { get; init; }

        /// <summary>The scale of <see cref="W"/>, as a power of ten.</summary>
        public Int32    WScale            { get; init; }

        /// <summary>The unit of <see cref="W"/>.</summary>
        public String   WUnit             { get; init; } = "";

        /// <summary>Meter address 1, which is the meter's own identification.</summary>
        public String   MA1               { get; init; } = "";

        /// <summary>A counter incremented with every snapshot.</summary>
        public Int64    RCnt              { get; init; }

        /// <summary>How many seconds the meter has been in operation.</summary>
        public Int64    OS                { get; init; }

        /// <summary>The meter's local time, in seconds since the UNIX epoch.</summary>
        public Int64    Epoch             { get; init; }

        /// <summary>The offset of that local time to UTC, in minutes.</summary>
        public Int64    TZO               { get; init; }

        /// <summary>How often the time has been set.</summary>
        public Int64    EpochSetCnt       { get; init; }

        /// <summary>The operation seconds at which the time was last set.</summary>
        public Int64    EpochSetOS        { get; init; }

        /// <summary>The state of the digital inputs.</summary>
        public Int64    DI                { get; init; }

        /// <summary>The state of the digital outputs.</summary>
        public Int64    DO                { get; init; }

        /// <summary>User metadata 1, which the charging station fills with the contract identification.</summary>
        public String   Meta1             { get; init; } = "";

        /// <summary>User metadata 2, which the charging station fills with the EVSE identification.</summary>
        public String   Meta2             { get; init; } = "";

        /// <summary>User metadata 3, which the charging station fills with its own software version.</summary>
        public String   Meta3             { get; init; } = "";

        /// <summary>The meter's event flags.</summary>
        public Int64    Evt               { get; init; }

        #endregion

        #region TypeName

        /// <summary>
        /// What kind of snapshot this is, in words.
        /// </summary>
        public String TypeName

            => BSMFormat.SnapshotTypeName(Type);

        #endregion

        #region Events

        /// <summary>
        /// What the meter's event flags report, in words.
        /// </summary>
        public IEnumerable<String> Events

            => BSMFormat.ParseEvents(Evt);

        #endregion

    }

}
