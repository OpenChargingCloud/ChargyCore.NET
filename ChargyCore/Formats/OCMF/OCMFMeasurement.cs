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

namespace cloud.charging.open.chargy.Formats.OCMF
{

    /// <summary>
    /// What an OCMF pagination counter counts.
    ///
    /// The counter is prefixed with a letter, and the letter decides what the
    /// document is: "T" counts the charging sessions a meter has signed, "F" the
    /// fiscal readings it has taken on its own. The two are separate sequences,
    /// so a gap in one is not a gap in the other.
    /// </summary>
    public enum OCMFTransactionType
    {

        /// <summary>Neither — which makes the document unreadable.</summary>
        Undefined,

        /// <summary>A charging session, OCMF "T".</summary>
        Transaction,

        /// <summary>A fiscal reading, OCMF "F".</summary>
        Fiscal

    }


    /// <summary>
    /// One reading of an OCMF document.
    ///
    /// The document travels with the reading because an OCMF signature covers the
    /// whole document rather than the individual reading: what vouches for this
    /// number is a signature over a text that also holds a dozen other numbers,
    /// and a user interface asked to show why a reading is trustworthy has to be
    /// able to point at that text.
    ///
    /// Several readings of one document therefore share it, and share its verdict.
    /// </summary>
    /// <param name="Timestamp">When the value was measured, as an ISO 8601 string.</param>
    /// <param name="Value">The measured value.</param>
    /// <param name="Document">The document this reading was read out of.</param>
    /// <param name="TimeSync">How the meter's clock was synchronised, OCMF "TM" second part.</param>
    /// <param name="Transaction">Where in the charging session this reading was taken, OCMF "TX".</param>
    /// <param name="TransactionType">What the pagination counter counts.</param>
    /// <param name="Pagination">The pagination counter of the document, OCMF "PG" without its prefix.</param>
    /// <param name="ErrorIndex">An optional error index, OCMF "EI" — OCMF 0.1 only.</param>
    /// <param name="ErrorFlags">Optional error flags, OCMF "EF" — OCMF 1.x onwards.</param>
    /// <param name="CumulatedLoss">
    /// The energy the meter has compensated for the charging cable so far, OCMF "CL".
    ///
    /// Absent when the meter wrote a zero: a compensation of nothing is what every
    /// reading before the first loss carries, and showing "0 kWh compensated"
    /// alongside a reading suggests a compensation took place.
    /// </param>
    /// <param name="StatusMeter">The status word of the energy meter, OCMF "ST".</param>
    public class OCMFMeasurementValue(String                Timestamp,
                                      Decimal               Value,
                                      OCMFDocument          Document,
                                      String?               TimeSync         = null,
                                      String?               Transaction      = null,
                                      OCMFTransactionType   TransactionType  = OCMFTransactionType.Undefined,
                                      UInt64                Pagination       = 0,
                                      Decimal?              ErrorIndex       = null,
                                      String?               ErrorFlags       = null,
                                      Decimal?              CumulatedLoss    = null,
                                      String?               StatusMeter      = null)

        : MeasurementValue(Timestamp,
                           Value,
                           StatusMeter: StatusMeter)

    {

        #region Properties

        /// <summary>The document this reading was read out of.</summary>
        public OCMFDocument         Document           { get; } = Document;

        /// <summary>How the meter's clock was synchronised.</summary>
        public String?              TimeSync           { get; } = TimeSync;

        /// <summary>Where in the charging session this reading was taken.</summary>
        public String?              Transaction        { get; } = Transaction;

        /// <summary>What the pagination counter counts.</summary>
        public OCMFTransactionType  TransactionType    { get; } = TransactionType;

        /// <summary>The pagination counter of the document.</summary>
        public UInt64               Pagination         { get; } = Pagination;

        /// <summary>An optional error index.</summary>
        public Decimal?             ErrorIndex         { get; } = ErrorIndex;

        /// <summary>Optional error flags.</summary>
        public String?              ErrorFlags         { get; } = ErrorFlags;

        /// <summary>The energy compensated for the charging cable so far.</summary>
        public Decimal?             CumulatedLoss      { get; } = CumulatedLoss;

        #endregion

    }

}
