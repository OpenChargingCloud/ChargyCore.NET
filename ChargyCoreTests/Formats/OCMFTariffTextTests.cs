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

using cloud.charging.open.chargy.Formats.OCMF;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the tariff text extension agreed at the Bonner Eichrechtstage.
    ///
    /// What is being checked is a translation: a semicolon-separated string that
    /// a meter signed, turned into the prices an EV driver was actually billed.
    /// Every assertion here is therefore about money, and a wrong step size or a
    /// misplaced factor of sixty is a wrong invoice rather than a formatting
    /// detail.
    /// </summary>
    [TestFixture]
    public class OCMFTariffTextTests : AChargyTests
    {

        #region Profile001_ChargesEnergyAndBlockingFromAStatedMinute()

        /// <summary>
        /// Profile 001: one euro to start, 59 cents per kWh, and ten cents a
        /// minute once the car has been standing there for two hours.
        /// </summary>
        [Test]
        public void Profile001_ChargesEnergyAndBlockingFromAStatedMinute()
        {

            Assert.That(OCMFBonnTariff.TryParse("001;EUR;100;59;10;120", out var tariff), Is.True);
            Assert.That(tariff, Is.Not.Null);

            var chargingTariff = tariff!.ToChargingTariff();

            Assert.Multiple(() => {

                Assert.That(tariff.Raw,                              Is.EqualTo("001;EUR;100;59;10;120"));
                Assert.That(tariff.Code,                             Is.EqualTo("001"));
                Assert.That(tariff.Currency,                         Is.EqualTo("EUR"));
                Assert.That(tariff.StartFeeCents,                    Is.EqualTo(100));
                Assert.That(tariff.EnergyFeeCentsPerKWh,             Is.EqualTo(59));
                Assert.That(tariff.BlockingFeeCentsPerMinute,        Is.EqualTo(10));
                Assert.That(tariff.BlockingFeeStartMinute,           Is.EqualTo(120));
                Assert.That(tariff.BlockingFeeStartsAfterCharging,   Is.False);

                Assert.That(chargingTariff.Id,                       Is.EqualTo("001;EUR;100;59;10;120"));
                Assert.That(chargingTariff.Currency,                 Is.EqualTo("EUR"));
                Assert.That(chargingTariff.Elements,                 Has.Count.EqualTo(2));

                AssertPrices(
                    chargingTariff.Elements[0],
                    ("FLAT",    1,    1),
                    ("ENERGY",  0.59m, 1)
                );
                Assert.That(chargingTariff.Elements[0].Restrictions,  Is.Null);

                // Ten cents a minute is six euros an hour, and the two hours the
                // profile grants become 7200 seconds, which is how OCPI counts.
                AssertPrices(
                    chargingTariff.Elements[1],
                    ("PARKING_TIME", 6, 60)
                );
                Assert.That(chargingTariff.Elements[1].Restrictions?.MinDuration,  Is.EqualTo(7200));

            });

        }

        #endregion

        #region Profile002_ChargesBlockingOnceChargingHasEnded()

        /// <summary>
        /// Profile 002 prices the same three things as 001, but the blocking fee
        /// begins when charging ends rather than at a stated minute — a moment no
        /// OCPI restriction can express, so the element carries none.
        /// </summary>
        [Test]
        public void Profile002_ChargesBlockingOnceChargingHasEnded()
        {

            Assert.That(OCMFBonnTariff.TryParse("002;EUR;50;40;8", out var tariff), Is.True);
            Assert.That(tariff, Is.Not.Null);

            var chargingTariff = tariff!.ToChargingTariff();

            Assert.Multiple(() => {

                Assert.That(tariff.Code,                             Is.EqualTo("002"));
                Assert.That(tariff.StartFeeCents,                    Is.EqualTo(50));
                Assert.That(tariff.EnergyFeeCentsPerKWh,             Is.EqualTo(40));
                Assert.That(tariff.BlockingFeeCentsPerMinute,        Is.EqualTo(8));
                Assert.That(tariff.BlockingFeeStartMinute,           Is.Null);
                Assert.That(tariff.BlockingFeeStartsAfterCharging,   Is.True);

                Assert.That(chargingTariff.Elements,                 Has.Count.EqualTo(2));

                AssertPrices(
                    chargingTariff.Elements[0],
                    ("FLAT",    0.5m,  1),
                    ("ENERGY",  0.4m,  1)
                );

                AssertPrices(
                    chargingTariff.Elements[1],
                    ("PARKING_TIME", 4.8m, 60)
                );
                Assert.That(chargingTariff.Elements[1].Restrictions,  Is.Null);

            });

        }

        #endregion

        #region Profile003_ChargesForTheTimeSpentCharging()

        /// <summary>
        /// Profile 003 bills the time rather than the energy, which is what a
        /// charging point does where the energy is not what is scarce.
        /// </summary>
        [Test]
        public void Profile003_ChargesForTheTimeSpentCharging()
        {

            Assert.That(OCMFBonnTariff.TryParse("003;EUR;25;6", out var tariff), Is.True);
            Assert.That(tariff, Is.Not.Null);

            var chargingTariff = tariff!.ToChargingTariff();

            Assert.Multiple(() => {

                Assert.That(tariff.Code,                       Is.EqualTo("003"));
                Assert.That(tariff.StartFeeCents,              Is.EqualTo(25));
                Assert.That(tariff.TimeFeeCentsPerMinute,      Is.EqualTo(6));
                Assert.That(tariff.EnergyFeeCentsPerKWh,       Is.Null);

                // One element only: there is nothing here that starts later.
                Assert.That(chargingTariff.Elements,           Has.Count.EqualTo(1));

                AssertPrices(
                    chargingTariff.Elements[0],
                    ("FLAT",  0.25m,  1),
                    ("TIME",  3.6m,  60)
                );

            });

        }

        #endregion

        #region AMalformedTariffTextIsNoTariff()

        /// <summary>
        /// What is rejected, and why each of them has to be.
        ///
        /// "TT" was free text before this extension existed and still is, so an
        /// unreadable tariff text is not an error in the charging session — it
        /// only means there is nothing to price. What must never happen is that
        /// one of these is read as a tariff anyway and an EV driver is shown a
        /// number nobody signed.
        /// </summary>
        [Test]
        public void AMalformedTariffTextIsNoTariff()

            => Assert.Multiple(() => {

                   // A currency the extension does not define.
                   Assert.That(OCMFBonnTariff.TryParse("001;USD;10;20;30;40",           out _),  Is.False);

                   // Profile 001 with a field missing: the last number could be the
                   // blocking fee or the minute it starts at, and guessing which
                   // would put an invented price on a receipt.
                   Assert.That(OCMFBonnTariff.TryParse("001;EUR;10;20;30",              out _),  Is.False);

                   // A profile nobody has defined.
                   Assert.That(OCMFBonnTariff.TryParse("004;EUR;10;20",                 out _),  Is.False);

                   // A negative price.
                   Assert.That(OCMFBonnTariff.TryParse("003;EUR;-1;20",                 out _),  Is.False);

                   // ..., and free-form text, which is what most tariff texts are.
                   Assert.That(OCMFBonnTariff.TryParse("ordinary free-form tariff text", out _),  Is.False);

                   Assert.That(
                       Assert.Throws<OCMFBonnTariffParseException>(() => OCMFBonnTariff.Parse("001;USD;10;20;30;40"))?.TariffText,
                       Is.EqualTo("001;USD;10;20;30;40")
                   );

               });

        #endregion


        #region (private, static) AssertPrices(Element, Expected)

        /// <summary>
        /// Assert that a tariff element prices exactly the given things.
        /// </summary>
        /// <param name="Element">A tariff element.</param>
        /// <param name="Expected">What it should price.</param>
        private static void AssertPrices(ChargingTariffElement                         Element,
                                         params (String Type, Decimal Price, Int64 StepSize)[] Expected)
        {

            Assert.That(Element.PriceComponents, Has.Count.EqualTo(Expected.Length));

            for (var i = 0; i < Expected.Length; i++)
            {
                Assert.That(Element.PriceComponents[i].Type,      Is.EqualTo(Expected[i].Type));
                Assert.That(Element.PriceComponents[i].Price,     Is.EqualTo(Expected[i].Price));
                Assert.That(Element.PriceComponents[i].StepSize,  Is.EqualTo(Expected[i].StepSize));
            }

        }

        #endregion

    }

}
