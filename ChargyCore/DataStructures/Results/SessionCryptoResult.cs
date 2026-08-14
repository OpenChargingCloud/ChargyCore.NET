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
    /// The result of verifying an entire charging session.
    /// </summary>
    /// <param name="Status">The verification result.</param>
    /// <param name="Message">An optional multi-language description of the result.</param>
    /// <param name="Certainty">
    /// How sure we are that this result is correct, between 0.0 and 1.0.
    ///
    /// JSON charge transparency records do not always carry an unambiguous format
    /// identifier, so several parsers can be candidates for the same file. The
    /// certainty lets the best matching parser win instead of the first one.
    /// </param>
    /// <param name="Exception">An optional exception that caused this result.</param>
    public class SessionCryptoResult(SessionVerificationResult  Status,
                                     I18NString?                Message    = null,
                                     Double                     Certainty  = 0,
                                     Exception?                 Exception  = null)
    {

        #region Data

        private readonly List<Error>    errors    = [];
        private readonly List<Warning>  warnings  = [];

        #endregion

        #region Properties

        /// <summary>
        /// The verification result.
        /// </summary>
        public SessionVerificationResult  Status      { get; set; } = Status;

        /// <summary>
        /// An optional multi-language description of the result.
        /// </summary>
        public I18NString?                Message     { get; set; } = Message;

        /// <summary>
        /// How sure we are that this result is correct, between 0.0 and 1.0.
        /// </summary>
        public Double                     Certainty   { get; set; } = Certainty;

        /// <summary>
        /// An optional exception that caused this result.
        /// </summary>
        public Exception?                 Exception   { get; set; } = Exception;

        /// <summary>
        /// Everything that made the verification fail.
        /// </summary>
        public IReadOnlyList<Error>       Errors
            => errors;

        /// <summary>
        /// Everything that looked suspicious, without invalidating the charging session.
        /// </summary>
        public IReadOnlyList<Warning>     Warnings
            => warnings;

        #endregion


        #region AddError    (Error)

        /// <summary>
        /// Add an error to this verification result.
        /// </summary>
        /// <param name="Error">An error.</param>
        public SessionCryptoResult AddError(Error Error)
        {
            errors.Add(Error);
            return this;
        }

        #endregion

        #region AddWarning  (Warning)

        /// <summary>
        /// Add a warning to this verification result.
        /// </summary>
        /// <param name="Warning">A warning.</param>
        public SessionCryptoResult AddWarning(Warning Warning)
        {
            warnings.Add(Warning);
            return this;
        }

        #endregion


        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this verification result.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("status",     Status.AsText()),
                           new JProperty("certainty",  Certainty)
                       );

            if (Message is not null)
                json.Add(new JProperty("message",   Message.ToJSON()));

            if (errors.  Count > 0)
                json.Add(new JProperty("errors",    new JArray(errors.  Select(error   => error.  ToJSON()))));

            if (warnings.Count > 0)
                json.Add(new JProperty("warnings",  new JArray(warnings.Select(warning => warning.ToJSON()))));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this verification result.
        /// </summary>
        public override String ToString()

            => Status.AsText();

        #endregion


    }

}
