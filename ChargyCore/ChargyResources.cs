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

using System.Reflection;

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// Access to the data files embedded into this assembly.
    ///
    /// Both files are shared verbatim with ChargyCore.TS, where they are shipped
    /// as side-car files next to the npm package. Embedding them here means a
    /// consumer of ChargyCore.NET gets a working default configuration without
    /// having to deploy additional files.
    /// </summary>
    public static class ChargyResources
    {

        #region Data

        private const  String    i18nResourceName             = "cloud.charging.open.chargy.Resources.i18n.json";
        private const  String    validationRulesResourceName  = "cloud.charging.open.chargy.Resources.validationRules.json";

        private static readonly  Assembly  assembly           = typeof(ChargyResources).Assembly;

        #endregion


        #region ReadEmbeddedText  (ResourceName)

        /// <summary>
        /// Read an embedded UTF-8 text resource of this assembly.
        /// </summary>
        /// <param name="ResourceName">The fully qualified name of the embedded resource.</param>
        /// <exception cref="InvalidOperationException">When the resource does not exist.</exception>
        public static String ReadEmbeddedText(String ResourceName)
        {

            using var stream = assembly.GetManifestResourceStream(ResourceName)
                                   ?? throw new InvalidOperationException(
                                          $"The embedded resource '{ResourceName}' could not be found within assembly '{assembly.GetName().Name}'!"
                                      );

            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);

            return reader.ReadToEnd();

        }

        #endregion

        #region GetI18NJSON()

        /// <summary>
        /// The embedded i18n dictionary, mapping message keys onto their
        /// translations, e.g. { "GeneralError": { "de": "...", "en": "..." } }.
        /// </summary>
        public static JObject GetI18NJSON()

            => JObject.Parse(ReadEmbeddedText(i18nResourceName));

        #endregion

        #region GetDefaultValidationRulesJSON()

        /// <summary>
        /// The embedded default validation rules, e.g. the plausibility threshold
        /// for the total energy of a charging session.
        /// </summary>
        public static JObject GetDefaultValidationRulesJSON()

            => JObject.Parse(ReadEmbeddedText(validationRulesResourceName));

        #endregion


    }

}
