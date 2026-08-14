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
    /// An error that occurred while parsing or verifying a charge transparency record.
    /// </summary>
    /// <param name="Message">A multi-language description of the error.</param>
    /// <param name="Level">How severe the error is.</param>
    /// <param name="Code">
    /// A stable, language-neutral identifier the GUI can branch on. This is the
    /// i18n key of <paramref name="Message"/>, so that a caller can react to a
    /// specific error without having to match translated text.
    /// </param>
    /// <param name="Details">
    /// Optional raw technical detail, e.g. an exception message. Deliberately not
    /// localized, because it is meant for bug reports rather than for end users.
    /// </param>
    public class Error(I18NString     Message,
                       SeverityLevel  Level    = SeverityLevel.High,
                       String?        Code     = null,
                       String?        Details  = null)
    {

        #region Properties

        /// <summary>
        /// A multi-language description of the error.
        /// </summary>
        public I18NString     Message    { get; } = Message;

        /// <summary>
        /// How severe the error is.
        /// </summary>
        public SeverityLevel  Level      { get; } = Level;

        /// <summary>
        /// A stable, language-neutral identifier the GUI can branch on.
        /// </summary>
        public String?        Code       { get; } = Code;

        /// <summary>
        /// Optional raw technical detail, e.g. an exception message.
        /// </summary>
        public String?        Details    { get; } = Details;

        #endregion


        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this error.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("level",    Level.AsText()),
                           new JProperty("message",  Message.ToJSON())
                       );

            if (Code    is not null)
                json.Add(new JProperty("code",     Code));

            if (Details is not null)
                json.Add(new JProperty("details",  Details));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this error.
        /// </summary>
        public override String ToString()

            => $"{Level.AsText()}: {Message.FirstText()}{(Details is not null ? $" ({Details})" : "")}";

        #endregion


    }

}
