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
    public class OCMFMeasurementValue(String        Timestamp,
                                      Decimal       Value,
                                      OCMFDocument  Document)

        : MeasurementValue(Timestamp,
                           Value)

    {

        #region Properties

        /// <summary>The document this reading was read out of.</summary>
        public OCMFDocument Document { get; } = Document;

        #endregion

    }

}
