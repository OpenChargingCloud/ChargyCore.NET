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

using org.GraphDefined.Vanaheimr.Hermod;

#endregion

namespace cloud.charging.open.chargy.tests.LiveLink
{

    /// <summary>
    /// Tests for the one-time passwords a charging station's live link is reached
    /// through.
    ///
    /// ChargyCore computes none of these itself: the scheme is implemented in
    /// Hermod, which ChargyCore already depends on, and a second implementation
    /// of one algorithm inside one dependency chain is exactly the drift that
    /// locks a driver out of their own charging session. What is tested here is
    /// therefore the dependency — against the fixed answers of the Dynamic
    /// QR-Code reference implementations, which are what a charging station
    /// computes on the other side.
    ///
    /// A one-time password is worth nothing unless both sides derive the same
    /// one, so these vectors are a compatibility contract of the same kind as the
    /// shared golden reports: if Hermod ever changes the algorithm, this says so
    /// here rather than at a charging station.
    ///
    /// The scheme is close to RFC 6238 and not the same — HMAC-SHA256 rather than
    /// SHA-1, the secret used as its own UTF-8 bytes rather than Base32 decoded,
    /// and a run of alphabet characters rather than a truncated number, which
    /// packs far more entropy into a short string than six digits do.
    /// </summary>
    [TestFixture]
    public class TOTPGeneratorTests
    {

        #region Data

        /// <summary>The shared secret of the reference implementation's tests.</summary>
        private const String SharedSecret = "secure!Charging!";

        /// <summary>2024-05-23 00:23:05 UTC — five seconds into a thirty second slot.</summary>
        private static readonly DateTimeOffset Timestamp = new (2024, 5, 23, 0, 23, 5, TimeSpan.Zero);

        #endregion


        #region ThePasswordsAreTheOnesTheReferenceImplementationComputes()

        /// <summary>
        /// The defaults: twelve characters, thirty second slots, digits and
        /// letters in both cases.
        /// </summary>
        [Test]
        public void ThePasswordsAreTheOnesTheReferenceImplementationComputes()
        {

            var totps = TOTPGenerator.GenerateTOTPs(SharedSecret, TOTPTimestamp: Timestamp);

            Assert.Multiple(() => {

                Assert.That(totps.Previous,       Is.EqualTo("MdPU0jCm5tXz"));
                Assert.That(totps.Current,        Is.EqualTo("CN63y502maVh"));
                Assert.That(totps.Next,           Is.EqualTo("dI54vnA25m2h"));

                // Twenty-five seconds left of a thirty second slot entered five
                // seconds ago.
                Assert.That(totps.RemainingTime,  Is.EqualTo(TimeSpan.FromSeconds(25)));

            });

        }

        #endregion

        #region ALongerPasswordExtendsTheShorterOne()

        /// <summary>
        /// Asking for more characters does not produce a different password but a
        /// longer one — the twelve character answer is its prefix.
        ///
        /// Worth pinning: were it not so, a station and a phone that disagreed
        /// about the length would disagree about every character rather than
        /// about the tail, and the disagreement would be far harder to spot.
        /// </summary>
        [Test]
        public void ALongerPasswordExtendsTheShorterOne()
        {

            var totps = TOTPGenerator.GenerateTOTPs(SharedSecret, TOTPLength: 23, TOTPTimestamp: Timestamp);

            Assert.Multiple(() => {

                Assert.That(totps.Current,   Has.Length.EqualTo(23));
                Assert.That(totps.Previous,  Is.EqualTo("MdPU0jCm5tXzkaPrPj61KwI"));
                Assert.That(totps.Current,   Is.EqualTo("CN63y502maVhAsv27Sd7JlE"));
                Assert.That(totps.Next,      Is.EqualTo("dI54vnA25m2hWW3bUcdY13q"));

                Assert.That(totps.Current,   Does.StartWith(TOTPGenerator.GenerateTOTPs(SharedSecret, TOTPTimestamp: Timestamp).Current));

            });

        }

        #endregion

        #region AnAlphabetOfDigitsGivesANumericPassword()

        /// <summary>
        /// The alphabet decides the shape of a password — digits only, for a
        /// station whose display or keypad cannot do better.
        /// </summary>
        [Test]
        public void AnAlphabetOfDigitsGivesANumericPassword()
        {

            var totps = TOTPGenerator.GenerateTOTPs(SharedSecret, Alphabet: "0123456789", TOTPTimestamp: Timestamp);

            Assert.Multiple(() => {
                Assert.That(totps.Previous,  Is.EqualTo("233045043555"));
                Assert.That(totps.Current,   Is.EqualTo("894361286613"));
                Assert.That(totps.Next,      Is.EqualTo("545817627227"));
            });

        }

        #endregion

        #region AnotherValidityTimeIsAnotherSlot()

        /// <summary>
        /// A different time step puts the same moment into a different slot and
        /// therefore produces an entirely different password. A live link states
        /// its time step for exactly this reason.
        /// </summary>
        [Test]
        public void AnotherValidityTimeIsAnotherSlot()
        {

            var totps = TOTPGenerator.GenerateTOTPs(SharedSecret, TimeSpan.FromMinutes(1), TOTPTimestamp: Timestamp);

            Assert.Multiple(() => {
                Assert.That(totps.Previous,       Is.EqualTo("nTdkiuG6yUyg"));
                Assert.That(totps.Current,        Is.EqualTo("XJZr0L1DGKn0"));
                Assert.That(totps.Next,           Is.EqualTo("ft0ONZ62MdMj"));
                Assert.That(totps.RemainingTime,  Is.EqualTo(TimeSpan.FromSeconds(55)));
            });

        }

        #endregion

        #region ThePasswordChangesOnlyWhenTheSlotDoes()

        /// <summary>
        /// Every moment within one slot yields the same password, and the moment
        /// after it yields the next.
        ///
        /// This is what makes the scheme usable at all: a driver's phone and a
        /// charging station never agree on the time to the second, and the three
        /// passwords handed out at once are what covers that drift — a station
        /// accepting the previous, the current and the next password tolerates a
        /// clock that is a whole slot out in either direction.
        /// </summary>
        [Test]
        public void ThePasswordChangesOnlyWhenTheSlotDoes()
        {

            var atFive       = TOTPGenerator.GenerateTOTPs(SharedSecret, TOTPTimestamp: Timestamp);
            var atTwentyNine = TOTPGenerator.GenerateTOTPs(SharedSecret, TOTPTimestamp: Timestamp.AddSeconds(24));
            var atThirty     = TOTPGenerator.GenerateTOTPs(SharedSecret, TOTPTimestamp: Timestamp.AddSeconds(25));

            Assert.Multiple(() => {

                Assert.That(atTwentyNine.Current,       Is.EqualTo(atFive.Current));
                Assert.That(atTwentyNine.RemainingTime, Is.EqualTo(TimeSpan.FromSeconds(1)));

                // One second later the slot has turned over, and what was the
                // next password is now the current one.
                Assert.That(atThirty.Current,           Is.EqualTo(atFive.Next));
                Assert.That(atThirty.Previous,          Is.EqualTo(atFive.Current));
                Assert.That(atThirty.RemainingTime,     Is.EqualTo(TimeSpan.FromSeconds(30)));

            });

        }

        #endregion

        #region ASecretTooShortToProtectAnythingIsRefused()

        /// <summary>
        /// What is refused, and why refusing is the right answer.
        ///
        /// The passwords are public by design — they are printed on a display for
        /// anyone standing there to read — so the whole security of the scheme is
        /// the secret behind them. A short one is not weak protection but none:
        /// somebody who reads a few passwords off a station can search a short
        /// secret and then generate every future password for it.
        /// </summary>
        [Test]
        public void ASecretTooShortToProtectAnythingIsRefused()

            => Assert.Multiple(() => {

                   Assert.That(() => TOTPGenerator.GenerateTOTPs("short"),                          Throws.ArgumentException);
                   Assert.That(() => TOTPGenerator.GenerateTOTPs(""),                               Throws.Exception);
                   Assert.That(() => TOTPGenerator.GenerateTOTPs("secure Charging secret"),         Throws.ArgumentException, "whitespace in the secret");

                   Assert.That(() => TOTPGenerator.GenerateTOTPs(SharedSecret, TOTPLength: 3),      Throws.ArgumentException);
                   Assert.That(() => TOTPGenerator.GenerateTOTPs(SharedSecret, Alphabet: "012"),    Throws.ArgumentException, "too few characters");
                   Assert.That(() => TOTPGenerator.GenerateTOTPs(SharedSecret, Alphabet: "01123"),  Throws.ArgumentException, "a character twice");
                   Assert.That(() => TOTPGenerator.GenerateTOTPs(SharedSecret, Alphabet: "01 23"),  Throws.ArgumentException, "whitespace in the alphabet");

               });

        #endregion

    }

}
