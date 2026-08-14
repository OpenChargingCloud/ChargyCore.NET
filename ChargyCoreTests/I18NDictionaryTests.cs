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

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Tests for the i18n dictionary and its language fallback behaviour,
    /// ported from the multilanguage handling of chargy.ts.
    /// </summary>
    [TestFixture]
    public class I18NDictionaryTests
    {

        #region The_embedded_dictionary_holds_every_message_of_ChargyCoreTS()

        [Test]
        public void The_embedded_dictionary_holds_every_message_of_ChargyCoreTS()
        {

            var i18n = I18NDictionary.Default();

            Assert.That(i18n.Count,  Is.EqualTo(286),
                        "i18n.json of ChargyCore.TS holds 286 message keys.");

        }

        #endregion

        #region An_unknown_key_is_returned_unchanged()

        [Test]
        public void An_unknown_key_is_returned_unchanged()
        {

            var i18n = I18NDictionary.Default();

            // This is what makes it safe to introduce a message before translating it.
            Assert.That(i18n.GetLocalizedMessage("SomeBrandNewMessage"),
                        Is.EqualTo("SomeBrandNewMessage"));

        }

        #endregion

        #region The_preferred_user_interface_language_wins()

        [Test]
        public void The_preferred_user_interface_language_wins()
        {

            var german  = I18NDictionary.Default([ Languages.de, Languages.en ]);
            var english = I18NDictionary.Default([ Languages.en ]);

            Assert.Multiple(() => {
                Assert.That(german. GetLocalizedMessage("GeneralError"),  Is.EqualTo("Allgemeiner Fehler!"));
                Assert.That(english.GetLocalizedMessage("GeneralError"),  Is.EqualTo("General Error!"));
            });

        }

        #endregion

        #region An_untranslated_language_falls_back_to_English()

        [Test]
        public void An_untranslated_language_falls_back_to_English()
        {

            // i18n.json only carries German and English.
            var i18n = I18NDictionary.Default([ Languages.fr ]);

            Assert.That(i18n.GetLocalizedMessage("GeneralError"),  Is.EqualTo("General Error!"));

        }

        #endregion

        #region No_user_interface_language_defaults_to_English()

        [Test]
        public void No_user_interface_language_defaults_to_English()
        {

            var i18n = I18NDictionary.Default([]);

            Assert.Multiple(() => {
                Assert.That(i18n.UILanguages,                             Is.EqualTo(new[] { Languages.en }));
                Assert.That(i18n.GetLocalizedMessage("GeneralError"),     Is.EqualTo("General Error!"));
            });

        }

        #endregion

        #region The_user_interface_languages_are_deduplicated()

        [Test]
        public void The_user_interface_languages_are_deduplicated()
        {

            var i18n = I18NDictionary.Default();

            i18n.SetUILanguages([ Languages.de, Languages.en, Languages.de ]);

            Assert.That(i18n.UILanguages,  Is.EqualTo(new[] { Languages.de, Languages.en }));

        }

        #endregion


        #region A_parameter_replaces_the_first_placeholder()

        [Test]
        public void A_parameter_replaces_the_first_placeholder()
        {

            var i18n = I18NDictionary.Default([ Languages.en ]);

            Assert.That(
                i18n.GetLocalizedMessageWithParameter("MissingOrInvalidSignedMeterValueP", 3),
                Is.EqualTo("Missing or invalid 3. signed meter value!")
            );

        }

        #endregion

        #region Only_the_first_placeholder_is_replaced()

        [Test]
        public void Only_the_first_placeholder_is_replaced()
        {

            // JavaScript's String.replace() with a string pattern replaces only the
            // first occurrence, and messages of i18n.json rely on that. C#'s
            // String.Replace() would replace all of them.
            var i18n = I18NDictionary.Parse(
                           new JObject(
                               new JProperty("TwoPlaceholders",
                                   new JObject(new JProperty("en", "%p and %p")))
                           ),
                           [ Languages.en ]
                       );

            Assert.That(
                i18n.GetLocalizedMessageWithParameter("TwoPlaceholders", 7),
                Is.EqualTo("7 and %p")
            );

        }

        #endregion

        #region A_parameter_is_formatted_culture_invariantly()

        [Test]
        public void A_parameter_is_formatted_culture_invariantly()
        {

            // A German culture would render this as "1,5" and change the message
            // depending on where the verifying process happens to run.
            var i18n = I18NDictionary.Parse(
                           new JObject(
                               new JProperty("Energy",
                                   new JObject(new JProperty("en", "%p kWh")))
                           ),
                           [ Languages.en ]
                       );

            Assert.That(
                i18n.GetLocalizedMessageWithParameter("Energy", 1.5m),
                Is.EqualTo("1.5 kWh")
            );

        }

        #endregion

        #region An_unknown_parameterized_key_still_replaces_its_placeholder()

        [Test]
        public void An_unknown_parameterized_key_still_replaces_its_placeholder()
        {

            var i18n = I18NDictionary.Default();

            Assert.That(
                i18n.GetLocalizedMessageWithParameter("Unknown %p message", 2),
                Is.EqualTo("Unknown 2 message")
            );

        }

        #endregion


        #region A_multilanguage_text_covers_every_user_interface_language()

        [Test]
        public void A_multilanguage_text_covers_every_user_interface_language()
        {

            var i18n = I18NDictionary.Default([ Languages.de, Languages.en ]);
            var text = i18n.GetMultilanguageText("GeneralError");

            Assert.Multiple(() => {
                Assert.That(text[Languages.de],  Is.EqualTo("Allgemeiner Fehler!"));
                Assert.That(text[Languages.en],  Is.EqualTo("General Error!"));
            });

        }

        #endregion

        #region An_unknown_multilanguage_key_falls_back_to_the_key_itself()

        [Test]
        public void An_unknown_multilanguage_key_falls_back_to_the_key_itself()
        {

            var i18n = I18NDictionary.Default([ Languages.de, Languages.en ]);
            var text = i18n.GetMultilanguageText("SomeBrandNewMessage");

            Assert.Multiple(() => {
                Assert.That(text[Languages.de],  Is.EqualTo("SomeBrandNewMessage"));
                Assert.That(text[Languages.en],  Is.EqualTo("SomeBrandNewMessage"));
            });

        }

        #endregion

        #region A_partially_translated_message_is_completed_from_the_best_match()

        [Test]
        public void A_partially_translated_message_is_completed_from_the_best_match()
        {

            var i18n = I18NDictionary.Parse(
                           new JObject(
                               new JProperty("OnlyGerman",
                                   new JObject(new JProperty("de", "Nur Deutsch")))
                           ),
                           [ Languages.de, Languages.en ]
                       );

            var text = i18n.GetMultilanguageText("OnlyGerman");

            Assert.Multiple(() => {
                Assert.That(text[Languages.de],  Is.EqualTo("Nur Deutsch"));
                // The missing English text is filled from the best available
                // translation, not from the key.
                Assert.That(text[Languages.en],  Is.EqualTo("Nur Deutsch"));
            });

        }

        #endregion

        #region A_parameterized_multilanguage_text_replaces_the_placeholder_in_every_language()

        [Test]
        public void A_parameterized_multilanguage_text_replaces_the_placeholder_in_every_language()
        {

            var i18n = I18NDictionary.Default([ Languages.de, Languages.en ]);
            var text = i18n.GetMultilanguageTextWithParameter("MissingOrInvalidSignedMeterValueP", 3);

            Assert.Multiple(() => {
                Assert.That(text[Languages.de],  Is.EqualTo("Fehlender oder ungültiger 3. signierter Messwert!"));
                Assert.That(text[Languages.en],  Is.EqualTo("Missing or invalid 3. signed meter value!"));
            });

        }

        #endregion

        #region GetLocalizedText_returns_null_for_no_text()

        [Test]
        public void GetLocalizedText_returns_null_for_no_text()
        {

            var i18n = I18NDictionary.Default([ Languages.de ]);

            Assert.Multiple(() => {

                Assert.That(i18n.GetLocalizedText(null),  Is.Null);

                Assert.That(i18n.GetLocalizedText(I18NString.Create(Languages.de, "Hallo")),
                            Is.EqualTo("Hallo"));

                // Not available in German, so English wins.
                Assert.That(i18n.GetLocalizedText(I18NString.Create(Languages.en, "Hello")),
                            Is.EqualTo("Hello"));

            });

        }

        #endregion


    }

}
