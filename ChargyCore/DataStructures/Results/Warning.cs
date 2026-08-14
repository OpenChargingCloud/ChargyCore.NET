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

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// A warning about a charge transparency record that does not invalidate it,
    /// e.g. an implausibly large amount of energy for a single charging session.
    /// </summary>
    /// <param name="Message">A multi-language description of the warning.</param>
    /// <param name="Level">How severe the warning is.</param>
    public class Warning(I18NString    Message,
                         SeverityLevel Level = SeverityLevel.Low)
    {

        #region Properties

        /// <summary>
        /// A multi-language description of the warning.
        /// </summary>
        public I18NString     Message    { get; } = Message;

        /// <summary>
        /// How severe the warning is.
        /// </summary>
        public SeverityLevel  Level      { get; } = Level;

        #endregion


        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this warning.
        /// </summary>
        public JObject ToJSON()

            => new (
                   new JProperty("level",    Level.AsText()),
                   new JProperty("message",  Message.ToJSON())
               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this warning.
        /// </summary>
        public override String ToString()

            => $"{Level.AsText()}: {Message.FirstText()}";

        #endregion


    }

}
