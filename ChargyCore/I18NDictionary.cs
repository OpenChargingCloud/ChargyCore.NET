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

using System.Collections.Frozen;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// The translations of all messages Chargy can emit, keyed by a stable,
    /// language-neutral message key.
    ///
    /// The keys double as the English fallback text: an unknown key is returned
    /// unchanged rather than rendering as a missing-translation marker, which is
    /// what makes it safe to introduce a new message before translating it.
    /// </summary>
    public class I18NDictionary
    {

        #region Data

        private readonly FrozenDictionary<String, I18NString>  entries;

        private          Languages[]                           uiLanguages;

        #endregion

        #region Properties

        /// <summary>
        /// The user interface languages, in order of preference.
        /// </summary>
        public IReadOnlyList<Languages>  UILanguages
            => uiLanguages;

        /// <summary>
        /// The number of known message keys.
        /// </summary>
        public Int32                     Count
            => entries.Count;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new i18n dictionary.
        /// </summary>
        /// <param name="Entries">The translations, keyed by message key.</param>
        /// <param name="UILanguages">The user interface languages, in order of preference.</param>
        public I18NDictionary(IReadOnlyDictionary<String, I18NString>  Entries,
                              IEnumerable<Languages>?                  UILanguages = null)
        {

            this.entries      = Entries.ToFrozenDictionary();
            this.uiLanguages  = NormalizeUILanguages(UILanguages);

        }

        #endregion


        #region (static) Parse    (JSON, UILanguages = null)

        /// <summary>
        /// Parse the given JSON as an i18n dictionary, e.g. the contents of "i18n.json".
        /// </summary>
        /// <param name="JSON">A JSON object mapping message keys onto their translations.</param>
        /// <param name="UILanguages">The user interface languages, in order of preference.</param>
        public static I18NDictionary Parse(JObject                  JSON,
                                           IEnumerable<Languages>?  UILanguages = null)
        {

            var entries = new Dictionary<String, I18NString>();

            foreach (var property in JSON.Properties())
            {

                if (property.Value is not JObject translations)
                    continue;

                var i18nString = I18NString.Empty;

                foreach (var translation in translations.Properties())
                {

                    if (translation.Value.Type == JTokenType.String &&
                        Enum.TryParse<Languages>(translation.Name, out var language))
                    {
                        i18nString.Set(language, translation.Value.Value<String>() ?? "");
                    }

                }

                entries[property.Name] = i18nString;

            }

            return new I18NDictionary(entries, UILanguages);

        }

        #endregion

        #region (static) Default  (UILanguages = null)

        /// <summary>
        /// The i18n dictionary embedded into this assembly.
        /// </summary>
        /// <param name="UILanguages">The user interface languages, in order of preference.</param>
        public static I18NDictionary Default(IEnumerable<Languages>? UILanguages = null)

            => Parse(
                   ChargyResources.GetI18NJSON(),
                   UILanguages
               );

        #endregion


        #region SetUILanguages    (UILanguages)

        /// <summary>
        /// Set the user interface languages, in order of preference.
        /// Duplicates are dropped; an empty list falls back to English.
        /// </summary>
        /// <param name="UILanguages">The user interface languages, in order of preference.</param>
        public void SetUILanguages(IEnumerable<Languages>? UILanguages)
        {
            uiLanguages = NormalizeUILanguages(UILanguages);
        }

        #endregion

        #region (private) NormalizeUILanguages(UILanguages)

        private static Languages[] NormalizeUILanguages(IEnumerable<Languages>? UILanguages)
        {

            var languages = (UILanguages ?? []).
                                Where   (language => language != Languages.unknown).
                                Distinct().
                                ToArray();

            return languages.Length > 0
                       ? languages
                       : [ Languages.en ];

        }

        #endregion


        #region GetLocalizedMessage               (Key)

        /// <summary>
        /// The translation of the given message key in the best available user
        /// interface language, or the key itself when it is unknown.
        /// </summary>
        /// <param name="Key">A message key.</param>
        public String GetLocalizedMessage(String Key)

            => entries.TryGetValue(Key, out var translations)
                   ? FindBestText(translations) ?? Key
                   : Key;

        #endregion

        #region GetMultilanguageText              (Key)

        /// <summary>
        /// The translations of the given message key, completed so that every user
        /// interface language has a text.
        /// </summary>
        /// <param name="Key">A message key.</param>
        public I18NString GetMultilanguageText(String Key)

            => CompleteText(
                   entries.TryGetValue(Key, out var translations)
                       ? translations
                       : null,
                   Key
               );

        #endregion

        #region GetLocalizedMessageWithParameter  (Key, Parameter)

        /// <summary>
        /// The translation of the given message key in the best available user
        /// interface language, with the first "%p" replaced by the given parameter.
        /// </summary>
        /// <param name="Key">A message key.</param>
        /// <param name="Parameter">A value to insert into the message.</param>
        public String GetLocalizedMessageWithParameter(String Key, Object Parameter)
        {

            var parameter = AsText(Parameter);

            return entries.TryGetValue(Key, out var translations) &&
                   FindBestText(translations) is String localized

                       ? ReplaceFirst(localized, parameter)
                       : ReplaceFirst(Key,       parameter);

        }

        #endregion

        #region GetMultilanguageTextWithParameter (Key, Parameter)

        /// <summary>
        /// The translations of the given message key, each with the first "%p"
        /// replaced by the given parameter, completed so that every user interface
        /// language has a text.
        /// </summary>
        /// <param name="Key">A message key.</param>
        /// <param name="Parameter">A value to insert into the message.</param>
        public I18NString GetMultilanguageTextWithParameter(String Key, Object Parameter)
        {

            var parameter        = AsText(Parameter);
            var parameterized    = I18NString.Empty;

            if (entries.TryGetValue(Key, out var translations))
                foreach (var translation in translations)
                    parameterized.Set(
                        translation.Language,
                        ReplaceFirst(translation.Text, parameter)
                    );

            return CompleteText(
                       parameterized,
                       ReplaceFirst(Key, parameter)
                   );

        }

        #endregion

        #region GetLocalizedText                  (Text)

        /// <summary>
        /// The given multi-language text in the best available user interface
        /// language, or null when there is no text at all.
        /// </summary>
        /// <param name="Text">A multi-language text.</param>
        public String? GetLocalizedText(I18NString? Text)

            => Text is null
                   ? null
                   : FindBestText(Text);

        #endregion


        #region (private) FindBestText  (Text)

        /// <summary>
        /// The given text in the first available user interface language, falling
        /// back to English and then to whatever language happens to be present.
        /// </summary>
        private String? FindBestText(I18NString Text)
        {

            foreach (var language in uiLanguages)
                if (Text.Has(language))
                    return Text[language];

            if (Text.Has(Languages.en))
                return Text[Languages.en];

            foreach (var translation in Text)
                return translation.Text;

            return null;

        }

        #endregion

        #region (private) CompleteText  (Text, FallbackText)

        /// <summary>
        /// A copy of the given text in which every user interface language has a
        /// text, using the best available translation as the fallback.
        /// </summary>
        private I18NString CompleteText(I18NString? Text, String FallbackText)
        {

            var result    = Text?.Clone() ?? I18NString.Empty;
            var fallback  = FindBestText(result) ?? FallbackText;

            foreach (var language in uiLanguages)
                if (!result.Has(language))
                    result.Set(language, fallback);

            return result;

        }

        #endregion

        #region (private) ReplaceFirst  (Text, Parameter)

        /// <summary>
        /// Replace the first "%p" of the given text with the given parameter.
        ///
        /// Note: Only the first occurrence, because that is what JavaScript's
        /// String.replace() with a string pattern does — and several messages of
        /// i18n.json rely on it.
        /// </summary>
        private static String ReplaceFirst(String Text, String Parameter)
        {

            var index = Text.IndexOf("%p", StringComparison.Ordinal);

            return index < 0
                       ? Text
                       : String.Concat(Text.AsSpan(0, index), Parameter, Text.AsSpan(index + 2));

        }

        #endregion

        #region (private) AsText        (Parameter)

        /// <summary>
        /// A parameter as text, formatted invariantly so that a message reads the
        /// same no matter which culture the verifying process happens to run under.
        /// </summary>
        private static String AsText(Object Parameter)

            => Parameter is IFormattable formattable
                   ? formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture)
                   : Parameter.ToString() ?? "";

        #endregion


    }

}
