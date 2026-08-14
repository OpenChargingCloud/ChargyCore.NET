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
    /// The result of verifying a single energy meter measurement, together with
    /// everything that went wrong or looked suspicious while doing so.
    /// </summary>
    /// <param name="Status">The verification result.</param>
    public class CryptoResult(VerificationResult Status)
    {

        #region Data

        private readonly List<Error>    errors    = [];
        private readonly List<Warning>  warnings  = [];

        #endregion

        #region Properties

        /// <summary>
        /// The verification result.
        /// </summary>
        public VerificationResult      Status      { get; set; } = Status;

        /// <summary>
        /// Everything that made the verification fail.
        /// </summary>
        public IReadOnlyList<Error>    Errors
            => errors;

        /// <summary>
        /// Everything that looked suspicious, without invalidating the measurement.
        /// </summary>
        public IReadOnlyList<Warning>  Warnings
            => warnings;

        #endregion


        #region AddError    (Error)

        /// <summary>
        /// Add an error to this verification result.
        /// </summary>
        /// <param name="Error">An error.</param>
        public CryptoResult AddError(Error Error)
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
        public CryptoResult AddWarning(Warning Warning)
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
                           new JProperty("status", Status.AsText())
                       );

            if (errors.  Count > 0)
                json.Add(new JProperty("errors",   new JArray(errors.  Select(error   => error.  ToJSON()))));

            if (warnings.Count > 0)
                json.Add(new JProperty("warnings", new JArray(warnings.Select(warning => warning.ToJSON()))));

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
