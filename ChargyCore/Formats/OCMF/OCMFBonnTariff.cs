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

using System.Globalization;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.chargy.Formats.OCMF
{

    /// <summary>
    /// Why a tariff text could not be read as a Bonn tariff.
    /// </summary>
    /// <param name="TariffText">The tariff text that could not be read.</param>
    /// <param name="Message">What is wrong with it.</param>
    public class OCMFBonnTariffParseException(String  TariffText,
                                              String  Message) : Exception(Message)
    {

        /// <summary>The tariff text that could not be read.</summary>
        public String TariffText { get; } = TariffText;

    }


    /// <summary>
    /// The tariff text extension agreed at the Bonner Eichrechtstage.
    ///
    /// OCMF signs a tariff as free text in "TT", which leaves an EV driver with a
    /// price they cannot check: a receipt saying "001;EUR;100;59;10;120" states
    /// what was charged only to somebody who already knows what the fields mean.
    /// Three profiles were agreed to give that string a meaning, and this turns it
    /// into an ordinary charging tariff — the same shape a roaming platform would
    /// have delivered, except that this one arrived inside the meter's signature.
    ///
    /// The profiles differ in what may be charged besides the energy:
    ///
    /// <list type="bullet">
    /// <item>001 — start fee, energy price, and a blocking fee from a stated minute on</item>
    /// <item>002 — start fee, energy price, and a blocking fee once charging has ended</item>
    /// <item>003 — start fee and a price for the time spent charging</item>
    /// </list>
    ///
    /// A text that is not one of the three is not an error in the charging session:
    /// "TT" was free text before this extension existed and still is. Use
    /// <see cref="TryParse(String, out OCMFBonnTariff?)"/> where an unreadable
    /// tariff text simply means there is nothing to interpret.
    /// </summary>
    public partial class OCMFBonnTariff
    {

        #region Properties

        /// <summary>The tariff text this was read from.</summary>
        public String    Raw                               { get; private init; } = "";

        /// <summary>The profile: "001", "002" or "003".</summary>
        public String    Code                              { get; private init; } = "";

        /// <summary>The currency, which the extension fixes to "EUR".</summary>
        public String    Currency                          { get; private init; } = "EUR";

        /// <summary>What is charged for starting the charging session, in cents.</summary>
        public Decimal   StartFeeCents                     { get; private init; }

        /// <summary>What is charged per kWh, in cents. Profiles 001 and 002.</summary>
        public Decimal?  EnergyFeeCentsPerKWh              { get; private init; }

        /// <summary>What is charged per minute of blocking the charging point, in cents. Profiles 001 and 002.</summary>
        public Decimal?  BlockingFeeCentsPerMinute         { get; private init; }

        /// <summary>The minute from which the blocking fee applies. Profile 001.</summary>
        public Decimal?  BlockingFeeStartMinute            { get; private init; }

        /// <summary>Whether the blocking fee applies from the end of charging. Profile 002.</summary>
        public Boolean   BlockingFeeStartsAfterCharging    { get; private init; }

        /// <summary>What is charged per minute of charging, in cents. Profile 003.</summary>
        public Decimal?  TimeFeeCentsPerMinute             { get; private init; }

        #endregion


        #region (static) Parse   (TariffText)

        /// <summary>
        /// Read a Bonn tariff text.
        /// </summary>
        /// <param name="TariffText">An OCMF "TT" value.</param>
        /// <exception cref="OCMFBonnTariffParseException">When the text is not a Bonn tariff.</exception>
        public static OCMFBonnTariff Parse(String TariffText)
        {

            var fields = TariffText.Split(';');
            var code   = fields[0];

            if (fields.Length < 2 || fields[1] != "EUR")
                throw new OCMFBonnTariffParseException(TariffText, "currency must be EUR");

            switch (code)
            {

                case "001":
                    if (fields.Length != 6)
                        throw new OCMFBonnTariffParseException(TariffText, "profile 001 must contain six fields");
                    return new OCMFBonnTariff {
                               Raw                        = TariffText,
                               Code                       = code,
                               StartFeeCents              = ParseCents(fields[2], TariffText, "W"),
                               EnergyFeeCentsPerKWh       = ParseCents(fields[3], TariffText, "X"),
                               BlockingFeeCentsPerMinute  = ParseCents(fields[4], TariffText, "Y"),
                               BlockingFeeStartMinute     = ParseCents(fields[5], TariffText, "Z")
                           };

                case "002":
                    if (fields.Length != 5)
                        throw new OCMFBonnTariffParseException(TariffText, "profile 002 must contain five fields");
                    return new OCMFBonnTariff {
                               Raw                             = TariffText,
                               Code                            = code,
                               StartFeeCents                   = ParseCents(fields[2], TariffText, "W"),
                               EnergyFeeCentsPerKWh            = ParseCents(fields[3], TariffText, "X"),
                               BlockingFeeCentsPerMinute       = ParseCents(fields[4], TariffText, "Y"),
                               BlockingFeeStartsAfterCharging  = true
                           };

                case "003":
                    if (fields.Length != 4)
                        throw new OCMFBonnTariffParseException(TariffText, "profile 003 must contain four fields");
                    return new OCMFBonnTariff {
                               Raw                    = TariffText,
                               Code                   = code,
                               StartFeeCents          = ParseCents(fields[2], TariffText, "W"),
                               TimeFeeCentsPerMinute  = ParseCents(fields[3], TariffText, "X")
                           };

                default:
                    throw new OCMFBonnTariffParseException(TariffText, "unknown Bonn tariff profile");

            }

        }

        #endregion

        #region (static) TryParse(TariffText, out BonnTariff)

        /// <summary>
        /// Try to read a Bonn tariff text.
        /// </summary>
        /// <param name="TariffText">An OCMF "TT" value.</param>
        /// <param name="BonnTariff">The tariff, when the text is one.</param>
        public static Boolean TryParse(String TariffText, out OCMFBonnTariff? BonnTariff)
        {

            try
            {
                BonnTariff = Parse(TariffText);
                return true;
            }
            catch (OCMFBonnTariffParseException)
            {
                BonnTariff = null;
                return false;
            }

        }

        #endregion

        #region (private, static) ParseCents(Value, TariffText, FieldName)

        /// <summary>
        /// Read one amount in cents.
        ///
        /// Only a non-negative decimal without a leading zero is accepted. That is
        /// stricter than a number parser has to be, and deliberately so: this is a
        /// price an EV driver will be billed by, and "-5", "1e3" or "007" are more
        /// likely a broken generator than an intended tariff.
        /// </summary>
        /// <param name="Value">The field, as written.</param>
        /// <param name="TariffText">The whole tariff text, for the error.</param>
        /// <param name="FieldName">The name the extension gives this field.</param>
        private static Decimal ParseCents(String  Value,
                                          String  TariffText,
                                          String  FieldName)
        {

            if (!CentsRegex().IsMatch(Value))
                throw new OCMFBonnTariffParseException(TariffText, $"{FieldName} must be a non-negative decimal number");

            if (!Decimal.TryParse(Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var cents))
                throw new OCMFBonnTariffParseException(TariffText, $"{FieldName} is outside the supported numeric range");

            return cents;

        }

        #endregion


        #region ToChargingTariff()

        /// <summary>
        /// Turn this tariff into an ordinary charging tariff.
        ///
        /// The prices become euros, because that is what a receipt is written in,
        /// and the per-minute fees become prices per hour with a sixty second step
        /// — which is how OCPI states a time price, and it is OCPI that everything
        /// downstream of Chargy speaks.
        /// </summary>
        public ChargingTariff ToChargingTariff()
        {

            var baseComponents  = new List<PriceComponent> {
                                      new ("FLAT", EurosFromCents(StartFeeCents), 1)
                                  };

            var elements        = new List<ChargingTariffElement>();

            switch (Code)
            {

                case "001":
                    baseComponents.Add(new PriceComponent("ENERGY", EurosFromCents(EnergyFeeCentsPerKWh ?? 0), 1));
                    elements.Add(new ChargingTariffElement(baseComponents));
                    elements.Add(
                        new ChargingTariffElement(
                            [ new PriceComponent("PARKING_TIME", EurosPerHourFromCentsPerMinute(BlockingFeeCentsPerMinute ?? 0), 60) ],
                            // OCPI counts a minimum duration in seconds, and the
                            // profile states it in minutes.
                            new TariffRestriction(MinDuration: ToSeconds(BlockingFeeStartMinute ?? 0))
                        )
                    );
                    break;

                case "002":
                    baseComponents.Add(new PriceComponent("ENERGY", EurosFromCents(EnergyFeeCentsPerKWh ?? 0), 1));
                    elements.Add(new ChargingTariffElement(baseComponents));
                    // No restriction: this blocking fee starts when charging ends,
                    // which is a moment no OCPI restriction can express.
                    elements.Add(
                        new ChargingTariffElement(
                            [ new PriceComponent("PARKING_TIME", EurosPerHourFromCentsPerMinute(BlockingFeeCentsPerMinute ?? 0), 60) ]
                        )
                    );
                    break;

                case "003":
                    baseComponents.Add(new PriceComponent("TIME", EurosPerHourFromCentsPerMinute(TimeFeeCentsPerMinute ?? 0), 60));
                    elements.Add(new ChargingTariffElement(baseComponents));
                    break;

            }

            return new ChargingTariff(
                       Raw,
                       Currency:  Currency,
                       Elements:  elements
                   );

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this tariff.
        ///
        /// Only the fields of its own profile are written, because a profile 003
        /// tariff has no energy price at all and writing one as null would suggest
        /// the meter left it out.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("raw",            Raw),
                           new JProperty("code",           Code),
                           new JProperty("currency",       Currency),
                           new JProperty("startFeeCents",  StartFeeCents)
                       );

            if (EnergyFeeCentsPerKWh.     HasValue)
                json.Add(new JProperty("energyFeeCentsPerKWh",            EnergyFeeCentsPerKWh.     Value));

            if (BlockingFeeCentsPerMinute.HasValue)
                json.Add(new JProperty("blockingFeeCentsPerMinute",       BlockingFeeCentsPerMinute.Value));

            if (BlockingFeeStartMinute.   HasValue)
                json.Add(new JProperty("blockingFeeStartMinute",          BlockingFeeStartMinute.   Value));

            if (BlockingFeeStartsAfterCharging)
                json.Add(new JProperty("blockingFeeStartsAfterCharging",  true));

            if (TimeFeeCentsPerMinute.    HasValue)
                json.Add(new JProperty("timeFeeCentsPerMinute",           TimeFeeCentsPerMinute.    Value));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this tariff.
        /// </summary>
        public override String ToString()

            => Raw;

        #endregion


        #region (private, static) Price conversions

        /// <summary>Cents to euros.</summary>
        private static Decimal EurosFromCents(Decimal Cents)

            => Cents / 100;

        /// <summary>Cents per minute to euros per hour.</summary>
        private static Decimal EurosPerHourFromCentsPerMinute(Decimal Cents)

            => Cents / 100 * 60;

        /// <summary>
        /// Minutes to whole seconds.
        ///
        /// Rounded rather than truncated: every profile in use states whole
        /// minutes, and where one does not, the nearest second is closer to what
        /// was written than the second below it.
        /// </summary>
        private static Int64 ToSeconds(Decimal Minutes)

            => (Int64) Decimal.Round(Minutes * 60, MidpointRounding.AwayFromZero);

        #endregion

        #region (private) Regular expressions

        [GeneratedRegex(@"^(?:0|[1-9][0-9]*)(?:\.[0-9]+)?$")]
        private static partial Regex CentsRegex();

        #endregion


    }

}
