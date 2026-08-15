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

namespace cloud.charging.open.chargy.Formats.ChargeIT
{

    /// <summary>
    /// Counts how much of a chargeIT container turned out to be there.
    ///
    /// The older chargeIT format declares no context, so nothing in the file says
    /// what it is. Recognising it means checking whether it has the right shape,
    /// and the share of those checks that passed becomes the record's certainty.
    /// That number is what lets Chargy distinguish "a damaged chargeIT file" from
    /// "not a chargeIT file", which are very different things to tell somebody
    /// holding a charging receipt.
    ///
    /// A missing container counts once as an error and again as however many
    /// checks it took with it: not finding the address is one mistake, but it also
    /// means the street, the postal code and the town could not be looked at, and
    /// the certainty has to reflect that they were not confirmed either.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="BaseChecks">How many checks the container itself is put through.</param>
    public class ChargeITFormatChecks(I18NDictionary  I18N,
                                      Int32           BaseChecks)
    {

        #region Data

        /// <summary>How many checks a single signed meter value is put through.</summary>
        public const Int32 ChecksPerMeterValue = 39;

        private readonly List<Error>    errors           = [];
        private readonly List<Warning>  warnings         = [];
        private          Int32          totalChecks      = BaseChecks + 2 * ChecksPerMeterValue;
        private          Int32          unreachedChecks;

        #endregion

        #region Properties

        /// <summary>Whether anything was found missing.</summary>
        public Boolean  HasErrors
            => errors.Count > 0;

        /// <summary>The share of the format checks that passed.</summary>
        public Double   Certainty
            => (totalChecks - errors.Count - unreachedChecks) / (Double) totalChecks;

        #endregion


        #region Missing       (MessageKey, UnreachedChecks = 0)

        /// <summary>
        /// Record that something the format prescribes is not there.
        /// </summary>
        /// <param name="MessageKey">The i18n key of what is missing.</param>
        /// <param name="UnreachedChecks">How many further checks this made impossible.</param>
        public void Missing(String  MessageKey,
                            Int32   UnreachedChecks = 0)
        {
            errors.Add(new Error(I18N.GetMultilanguageText(MessageKey)));
            unreachedChecks += UnreachedChecks;
        }

        #endregion

        #region AddMeterValues(Count)

        /// <summary>
        /// Account for the signed meter values beyond the two a session needs at
        /// the very least.
        /// </summary>
        /// <param name="Count">How many signed meter values the container carries.</param>
        public void AddMeterValues(Int32 Count)
        {

            if (Count > 2)
                totalChecks += ChecksPerMeterValue * (Count - 2);

        }

        #endregion

        #region Failed        (Exception = null)

        /// <summary>
        /// Report that this is not a chargeIT container, and how close it came.
        /// </summary>
        /// <param name="Exception">An optional exception that ended the reading.</param>
        public SessionCryptoResult Failed(Exception? Exception = null)
        {

            var result = new SessionCryptoResult(
                             SessionVerificationResult.InvalidSessionFormat,
                             Exception is not null
                                 ? I18N.GetMultilanguageText($"Exception occured: {Exception.Message}")
                                 : null,
                             Certainty:  Certainty,
                             Exception:  Exception
                         );

            foreach (var error   in errors)    result.AddError  (error);
            foreach (var warning in warnings)  result.AddWarning(warning);

            return result;

        }

        #endregion

    }

}
