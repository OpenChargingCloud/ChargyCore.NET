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
using System.Security.Cryptography;
using System.Text;

#endregion

namespace cloud.charging.open.chargy.Formats.OCMF
{

    /// <summary>
    /// The start of a charging session as its original OCMF documents say it:
    /// the one start document, when it was signed, and the tariff texts.
    ///
    /// Finding it is not checking it: <see cref="Signatures"/> says so, and the
    /// caller verifies the start document separately, with
    /// <see cref="OCMFSessionPriceContext.VerifyOCMFPriceStart"/>.
    /// </summary>
    /// <param name="EVSEId">The EVSE the session was charged at.</param>
    /// <param name="MeterSerialNumber">The meter it was measured on.</param>
    /// <param name="ChargingStarted">When the meter signed the start reading.</param>
    /// <param name="StartDocumentSHA256">The SHA-256 of the complete original start document, lowercase hexadecimal.</param>
    /// <param name="TariffTexts">The tariff texts of the start document.</param>
    /// <param name="AllTariffTexts">The tariff texts of every document — compared only once every original signature and association passed.</param>
    public sealed record OCMFPriceContext(String                 EVSEId,
                                          String                 MeterSerialNumber,
                                          DateTimeOffset         ChargingStarted,
                                          String                 StartDocumentSHA256,
                                          IReadOnlyList<String>  TariffTexts,
                                          IReadOnlyList<String>  AllTariffTexts)
    {

        /// <summary>
        /// Discovering the context is not verifying a signature: "not-checked",
        /// always. The original readings are validated separately.
        /// </summary>
        public String Signatures
            => "not-checked";

    }


    /// <summary>
    /// The gross rates a price declares, as exact decimals.
    /// </summary>
    /// <param name="Currency">The currency, e.g. "EUR".</param>
    /// <param name="EnergyPerKWh">The price of a kWh, e.g. "0.35".</param>
    /// <param name="FlatFee">A fee per session.</param>
    /// <param name="TimePerHour">The price of an hour.</param>
    public sealed record GrossPriceRates(String   Currency,
                                         String?  EnergyPerKWh  = null,
                                         String?  FlatFee       = null,
                                         String?  TimePerHour   = null);


    /// <summary>
    /// How the rates of a price compare with the signed tariff texts.
    /// </summary>
    public enum OCMFPriceRateComparisonStatus
    {

        /// <summary>Currency and unconditional rates are the same.</summary>
        RatesMatch,

        /// <summary>The currency or a rate differs.</summary>
        Conflict,

        /// <summary>A tariff or a price this comparison cannot judge.</summary>
        Unsupported,

        /// <summary>No signed tariff text to compare with.</summary>
        Missing

    }


    /// <summary>
    /// How the rates of a price compare with the signed tariff texts, and why.
    /// </summary>
    /// <param name="Status">The verdict.</param>
    /// <param name="Detail">Why it is what it is.</param>
    public sealed record OCMFPriceRateComparison(OCMFPriceRateComparisonStatus  Status,
                                                 String                         Detail);


    /// <summary>
    /// Bind a price to the original start of a charging session: which document
    /// started it and when, what tariff its meter signed, and whether a price
    /// someone declares has the same rates.
    ///
    /// Everything is read anew from the original OCMF documents: neither
    /// validation results nor payloads nor timestamps the session carries are
    /// relied on.
    /// </summary>
    public static class OCMFSessionPriceContext
    {

        #region ReadOCMFSessionPriceContext(Session, I18N = null)

        /// <summary>
        /// Find the one original start of a charging session.
        /// </summary>
        /// <param name="Session">A charging session read from OCMF documents.</param>
        /// <param name="I18N">The dictionary the documents are read with.</param>
        /// <exception cref="ArgumentException">When the documents are not one session of this EVSE and meter, or do not have exactly one start.</exception>
        public static OCMFPriceContext ReadOCMFSessionPriceContext(ChargingSession   Session,
                                                                   I18NDictionary?   I18N = null)
        {

            var material = MeterKeyVerification.ReadOCMFSessionMaterial(Session);

            if (new OCMFFormat(I18N ?? I18NDictionary.Default()).TryParse(material.Documents, (String?) null, null, null) is not ChargeTransparencyRecord record ||
                record.ChargingSessions.Count != 1)
            {
                throw new ArgumentException("Price binding needs one original OCMF session", nameof(Session));
            }

            var starts       = new Dictionary<String, (String Raw, DateTimeOffset At)>(StringComparer.Ordinal);
            var tariffs      = new List<String>();
            var allTariffs   = new List<String>();

            foreach (var reading in MeterKeyVerification.ReadingsOf(record.ChargingSessions[0]))
            {

                var document = reading.OCMF?.Document;

                if (document is null || document.Raw.Length == 0                                ||
                    document.Payload["CT"]?.ToString().ToUpperInvariant() != "EVSEID"            ||
                    document.Payload["CI"]?.ToString().Trim()             != Session.EVSEId      ||
                    document.Payload["MS"]?.ToString()                    != material.MeterSerialNumber)
                {
                    throw new ArgumentException("Price context differs from original signed EVSE/meter identity", nameof(Session));
                }

                var tariffText = document.Payload["TT"]?.Type == Newtonsoft.Json.Linq.JTokenType.String
                                     ? document.Payload["TT"]!.ToString()
                                     : null;

                if (tariffText is not null && !allTariffs.Contains(tariffText))
                    allTariffs.Add(tariffText);

                if (reading.OCMF?.Transaction == "B")
                {

                    if (tariffText is not null && !tariffs.Contains(tariffText))
                        tariffs.Add(tariffText);

                    DateTimeOffset at;

                    try
                    {
                        at = reading.Value.ParsedTimestamp;
                    }
                    catch (FormatException)
                    {
                        throw new ArgumentException("Invalid original start time", nameof(Session));
                    }

                    starts[document.Raw + "\n" + at.UtcTicks] = (document.Raw, at);

                }

            }

            if (starts.Count != 1 || Session.EVSEId is null)
                throw new ArgumentException("Price binding needs one unambiguous original start document/time", nameof(Session));

            var start = starts.Values.First();

            return new OCMFPriceContext(
                       Session.EVSEId,
                       material.MeterSerialNumber,
                       start.At,
                       SHA256Of(start.Raw),
                       tariffs,
                       allTariffs
                   );

        }

        #endregion

        #region CompareOCMFPriceRates      (Context, Rates)

        /// <summary>
        /// Compare the gross rates a price declares with the signed tariff texts
        /// of the start document, as exact decimal cents.
        ///
        /// This is rate consistency only: taxes, steps, rounding, quantities,
        /// conditional parking fees and the final invoice are not checked, and no
        /// amount is ever compared as a floating point number.
        /// </summary>
        /// <param name="Context">The original start of the charging session.</param>
        /// <param name="Rates">The rates a price declares, when it declares any this comparison understands.</param>
        public static OCMFPriceRateComparison CompareOCMFPriceRates(OCMFPriceContext   Context,
                                                                    GrossPriceRates?   Rates)
        {

            if (Context.TariffTexts.Count == 0)
                return new (OCMFPriceRateComparisonStatus.Missing,     "No signed OCMF tariff text");

            if (Rates is null)
                return new (OCMFPriceRateComparisonStatus.Unsupported, "No supported declared gross-rate projection");

            try
            {

                foreach (var text in Context.TariffTexts)
                {
                    foreach (var part in text.Split(OCMFBonnTariff.Separator))
                    {

                        var profile  = OCMFBonnTariff.Parse(part);
                        var fields   = part.Split(';');

                        if (profile.Currency != Rates.Currency)
                            return new (OCMFPriceRateComparisonStatus.Conflict, "Signed currency differs from the selected price");

                        if (Exact(fields[2]) / 100m != Exact(Rates.FlatFee ?? "0"))
                            return new (OCMFPriceRateComparisonStatus.Conflict, "Signed start fee differs from the selected price");

                        if (profile.Code is "001" or "002")
                        {

                            if (Exact(fields[4]) != 0m)
                                return new (OCMFPriceRateComparisonStatus.Unsupported, "Conditional parking/blocking fees need their own applicability profile");

                            if (Exact(fields[3]) / 100m != Exact(Rates.EnergyPerKWh ?? "0") ||
                                Exact(Rates.TimePerHour ?? "0") != 0m)
                            {
                                return new (OCMFPriceRateComparisonStatus.Conflict, "Signed energy/time rate differs from the selected price");
                            }

                        }

                        else if (Exact(fields[3]) * 60m / 100m != Exact(Rates.TimePerHour ?? "0") ||
                                 Exact(Rates.EnergyPerKWh ?? "0") != 0m)
                        {
                            return new (OCMFPriceRateComparisonStatus.Conflict, "Signed time/energy rate differs from the selected price");
                        }

                    }
                }

                return new (OCMFPriceRateComparisonStatus.RatesMatch,
                            "Declared gross currency and unconditional rates match the original start tariff texts; billing quantities, rounding and customer consent are separate");

            }
            catch (Exception e) when (e is OCMFBonnTariffParseException or FormatException or OverflowException or IndexOutOfRangeException)
            {
                return new (OCMFPriceRateComparisonStatus.Unsupported, e.Message);
            }

        }

        #endregion

        #region VerifyOCMFPriceStart       (Session, Context, Binding, Now = null, I18N = null, CancellationToken = default)

        /// <summary>
        /// Verify exactly the original start document with a key the caller
        /// vouches for at the time it was signed — whether or not later readings
        /// exist yet.
        /// </summary>
        /// <param name="Session">A charging session read from OCMF documents.</param>
        /// <param name="Context">Its original start.</param>
        /// <param name="Binding">A historical binding covering the start time.</param>
        /// <param name="Now">The clock the binding's validity is checked against.</param>
        /// <param name="I18N">The dictionary the document is read with.</param>
        /// <param name="CancellationToken">Cancels the check; its results are then discarded.</param>
        /// <exception cref="ArgumentException">When the session has not that start, or the binding does not cover its time.</exception>
        /// <exception cref="OperationCanceledException">When the check was cancelled.</exception>
        public static BoundOCMFResult VerifyOCMFPriceStart(ChargingSession        Session,
                                                           OCMFPriceContext       Context,
                                                           MeterKeyBinding        Binding,
                                                           Func<DateTimeOffset>?  Now                = null,
                                                           I18NDictionary?        I18N               = null,
                                                           CancellationToken      CancellationToken  = default)
        {

            var start = MeterKeyVerification.ReadingsOf(Session).
                                             Where(reading => reading.OCMF?.Document.Raw is { Length: > 0 } raw &&
                                                              SHA256Of(raw) == Context.StartDocumentSHA256).
                                             ToArray();

            if (start.Length == 0 ||
                Context.EVSEId != Session.EVSEId ||
                Binding.Evidence.MeasurementTimes?.Contains(Context.ChargingStarted) != true)
            {
                throw new ArgumentException("Price assignment needs the exact original start and authorized historical coverage", nameof(Context));
            }

            return MeterKeyVerification.Verify(
                       Session.EVSEId,
                       start,
                       Binding,
                       Now ?? (() => DateTimeOffset.UtcNow),
                       I18N ?? I18NDictionary.Default(),
                       CancellationToken
                   );

        }

        #endregion


        #region (private) Exact(Text) / SHA256Of(Text)

        /// <summary>
        /// A decimal exactly as written: "0.350000000000000000001" is not 0.35.
        /// </summary>
        private static Decimal Exact(String Text)

            => Decimal.Parse(Text, NumberStyles.Float, CultureInfo.InvariantCulture);

        private static String SHA256Of(String Text)

            => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Text)));

        #endregion

    }

}
