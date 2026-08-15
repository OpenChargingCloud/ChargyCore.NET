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

using Newtonsoft.Json;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy.cli
{

    /// <summary>
    /// Turns whatever the detector returned into something a person can read.
    ///
    /// Note that everything printed here is already decided — this only says it
    /// out loud. The one judgement it makes is which exit code the whole run
    /// deserves, and even that follows the verdicts the library reached.
    /// </summary>
    /// <param name="I18N">The dictionary the messages are localised through.</param>
    /// <param name="Out">Where to write.</param>
    public class Report(I18NDictionary  I18N,
                        TextWriter      Out)
    {

        #region Data

        private readonly I18NDictionary  i18n  = I18N;
        private readonly TextWriter      output   = Out;

        #endregion


        #region Print(Result, AsJSON = false)

        /// <summary>
        /// Say what was found, and return the exit code it deserves.
        /// </summary>
        /// <param name="Result">Whatever the detector returned.</param>
        /// <param name="AsJSON">Whether to print the record itself rather than a report.</param>
        public Int32 Print(Object   Result,
                           Boolean  AsJSON = false)
        {

            switch (Result)
            {

                case ChargeTransparencyRecord record:
                    if (AsJSON)
                    {
                        output.WriteLine(record.ToJSON().ToString(Formatting.Indented));
                        return ExitCodeOf(record);
                    }
                    return PrintRecord(record);

                case ChargeTransparencyLiveLink liveLink:
                    return PrintLiveLink(liveLink, AsJSON);

                case SimpleURL url:
                    return PrintURL(url, AsJSON);

                case PublicKeyLookup publicKeys:
                    return PrintPublicKeys(publicKeys);

                case SessionCryptoResult failure:
                    output.WriteLine(i18n.GetLocalizedText(failure.Message) ?? failure.Status.AsText());
                    if (failure.Exception is not null)
                        output.WriteLine($"  {failure.Exception.Message}");
                    return Program.ExitUnreadable;

                default:
                    output.WriteLine("The files could not be read as charge transparency data.");
                    return Program.ExitUnreadable;

            }

        }

        #endregion


        #region (private) PrintRecord    (Record)

        /// <summary>
        /// Say what a charge transparency record contains and whether it holds up.
        /// </summary>
        /// <param name="Record">A charge transparency record.</param>
        private Int32 PrintRecord(ChargeTransparencyRecord Record)
        {

            output.WriteLine($"Charge transparency record {Record.Id}");

            if (Record.Begin is not null || Record.End is not null)
                output.WriteLine($"  {Record.Begin ?? "?"} .. {Record.End ?? "?"}");

            output.WriteLine($"  {Record.ChargingSessions.Count} charging session(s)");
            output.WriteLine();

            var sessionNumber = 0;

            foreach (var session in Record.ChargingSessions)
            {

                sessionNumber++;

                var verdict = session.VerificationResult?.Status;

                output.WriteLine($"Charging session {sessionNumber}: {Verdict(verdict)}");
                output.WriteLine($"  identification:   {session.Id}");

                if (session.Begin is not null || session.End is not null)
                    output.WriteLine($"  measured:         {session.Begin ?? "?"} .. {session.End ?? "?"}");

                if (session.EVSEId            is String evseId)
                    output.WriteLine($"  EVSE:             {evseId}");

                if (session.ChargingStationId is String chargingStationId)
                    output.WriteLine($"  charging station: {chargingStationId}");

                if (session.EnergyMeterId     is String energyMeterId)
                    output.WriteLine($"  energy meter:     {energyMeterId}");

                if (session.AuthorizationStart?.Id is String authorization && authorization != "?")
                    output.WriteLine($"  authorized by:    {authorization}");

                #region What the meter measured, and how much of it is proven

                foreach (var measurement in session.Measurements)
                {

                    var values  = measurement.Values;
                    var proven  = values.Count(value => value.Result?.Status == VerificationResult.ValidSignature);

                    output.WriteLine(
                        $"  {measurement.Name ?? measurement.OBIS ?? "measurement"}: " +
                        $"{values.Count} reading(s), {proven} with a valid signature"
                    );

                    // The energy is the difference between the first and the last
                    // reading, and it is only worth stating when both of them are
                    // proven — a signed start and an unsigned end is not a
                    // measured amount of energy, it is a claim about one.
                    if (values.Count >= 2 && proven == values.Count)
                        output.WriteLine(
                            $"    {values[^1].Value - values[0].Value}" +
                            $"{(measurement.Unit is not null ? " " + measurement.Unit : "")} " +
                            $"({values[0].Value} .. {values[^1].Value})"
                        );

                    #region Whatever went wrong, said once per reason rather than once per reading

                    foreach (var reason in values.SelectMany  (value  => value.Result?.Errors ?? []).
                                                  GroupBy     (error  => error.Code ?? i18n.GetLocalizedText(error.Message) ?? "?").
                                                  OrderBy     (group  => group.Key))
                    {

                        var first = reason.First();

                        output.WriteLine(
                            $"    {i18n.GetLocalizedText(first.Message) ?? reason.Key}" +
                            $"{(reason.Count() > 1 ? $" ({reason.Count()} readings)" : "")}"
                        );

                        if (first.Details is String details)
                            output.WriteLine($"      {details}");

                    }

                    #endregion

                }

                #endregion

                if (session.VerificationResult?.Message is I18NString message &&
                    i18n.GetLocalizedText(message) is String messageText &&
                    messageText.Length > 0)
                {
                    output.WriteLine($"  {messageText}");
                }

                output.WriteLine();

            }

            PrintProblems(Record);

            return ExitCodeOf(Record);

        }

        #endregion

        #region (private) PrintProblems  (Record)

        /// <summary>
        /// Say what was wrong with the record as a whole, and what could not be
        /// read at all.
        /// </summary>
        /// <param name="Record">A charge transparency record.</param>
        private void PrintProblems(ChargeTransparencyRecord Record)
        {

            foreach (var warning in Record.Warnings)
                output.WriteLine($"Warning: {i18n.GetLocalizedText(warning.Message)}");

            foreach (var error in Record.Errors)
                output.WriteLine($"Error: {i18n.GetLocalizedText(error.Message)}");

            // Files that were handed over and made no sense are named rather than
            // dropped: somebody who passed six files and got five verified needs
            // to know which one is missing.
            foreach (var invalid in Record.InvalidDataSets)
                output.WriteLine($"Not charge transparency data: {invalid.FileInfo.Name}");

            if (Record.Warnings.Count > 0 || Record.Errors.Count > 0 || Record.InvalidDataSets.Count > 0)
                output.WriteLine();

        }

        #endregion

        #region (private) PrintLiveLink  (LiveLink, AsJSON)

        /// <summary>
        /// Say where the live data of a charging station can be reached.
        /// </summary>
        /// <param name="LiveLink">A charge transparency live link.</param>
        /// <param name="AsJSON">Whether to print the link itself.</param>
        private Int32 PrintLiveLink(ChargeTransparencyLiveLink  LiveLink,
                                    Boolean                     AsJSON)
        {

            if (AsJSON)
            {
                output.WriteLine(LiveLink.ToJSON().ToString(Formatting.Indented));
                return Program.ExitVerified;
            }

            output.WriteLine("A charge transparency live link.");
            output.WriteLine();
            output.WriteLine("This is not charging data but a pointer to some — nothing here has");
            output.WriteLine("been verified, and following any of these addresses tells the operator");
            output.WriteLine("that somebody is looking at this charging station.");
            output.WriteLine();

            if (i18n.GetLocalizedText(LiveLink.Description) is String description)
                output.WriteLine($"  {description}");

            if (LiveLink.Timestamp is String timestamp)
                output.WriteLine($"  as of:      {timestamp}");

            if (LiveLink.GeoLocation is not null)
                output.WriteLine($"  located at: {LiveLink.GeoLocation}");

            foreach (var transport in LiveLink.Transports)
            {

                output.WriteLine($"  {transport.Type.AsText()}:");

                foreach (var url in transport.URL is String single ? [ single ] : transport.URLs.Select(url => url.URL))
                    output.WriteLine($"    {url}");

                if (transport.TOTP is not null)
                    output.WriteLine($"    (behind a one-time password, changing every {transport.TOTP.TimeStep} seconds)");

            }

            return Program.ExitVerified;

        }

        #endregion

        #region (private) PrintURL       (URL, AsJSON)

        /// <summary>
        /// Say where the charging data can be found.
        /// </summary>
        /// <param name="URL">A Chargy URL.</param>
        /// <param name="AsJSON">Whether to print the URL object itself.</param>
        private Int32 PrintURL(SimpleURL  URL,
                               Boolean    AsJSON)
        {

            if (AsJSON)
            {
                output.WriteLine(URL.ToJSON().ToString(Formatting.Indented));
                return Program.ExitVerified;
            }

            output.WriteLine("A link to charge transparency data, and no data itself:");
            output.WriteLine();
            output.WriteLine($"  {URL.URL}");

            if (URL.ServiceTypes.Count > 0)
                output.WriteLine($"  serves: {String.Join(", ", URL.ServiceTypes)}");

            output.WriteLine();
            output.WriteLine("Nothing was fetched. Pass '--resolve-urls' to ask this address what it");
            output.WriteLine("offers — which also tells its operator that you are asking.");

            return Program.ExitVerified;

        }

        #endregion

        #region (private) PrintPublicKeys(PublicKeys)

        /// <summary>
        /// Say which public keys were handed over.
        /// </summary>
        /// <param name="PublicKeys">The public keys.</param>
        private Int32 PrintPublicKeys(PublicKeyLookup PublicKeys)
        {

            output.WriteLine($"{PublicKeys.PublicKeys.Count} public key(s) and no charging data.");
            output.WriteLine();

            foreach (var publicKey in PublicKeys.PublicKeys)
                output.WriteLine($"  {publicKey.Algorithm?.Name ?? "unknown algorithm"}: {publicKey.Value}");

            output.WriteLine();
            output.WriteLine("A key on its own proves nothing. Pass it together with the charging");
            output.WriteLine("data it belongs to.");

            return Program.ExitVerified;

        }

        #endregion


        #region (private, static) ExitCodeOf(Record)

        /// <summary>
        /// What the whole run deserves.
        ///
        /// One session that does not verify is enough: a record is an account of
        /// a charging process, and an account with an unproven part is not a
        /// proven account.
        /// </summary>
        /// <param name="Record">A charge transparency record.</param>
        private static Int32 ExitCodeOf(ChargeTransparencyRecord Record)

            => Record.ChargingSessions.Count > 0 &&
               Record.ChargingSessions.All(session => session.VerificationResult?.Status == SessionVerificationResult.ValidSignature)
                   ? Program.ExitVerified
                   : Program.ExitNotVerified;

        #endregion

        #region (private, static) Verdict   (Status)

        /// <summary>
        /// What a verification result means, in words.
        /// </summary>
        /// <param name="Status">The verdict on a charging session.</param>
        private static String Verdict(SessionVerificationResult? Status)

            => Status switch {
                   SessionVerificationResult.ValidSignature      => "verified",
                   SessionVerificationResult.InvalidSignature    => "NOT verified — the signature does not match",
                   SessionVerificationResult.PublicKeyNotFound   => "not checked — no public key was handed over",
                   SessionVerificationResult.InvalidPublicKey    => "not checked — the public key could not be used",
                   null                                          => "not checked",
                   _                                             => $"not verified — {Status.Value.AsText()}"
               };

        #endregion

    }

}
