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

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// A Charge Transparency Record: one or more charging sessions together with
    /// the metadata an EV driver needs to make sense of them.
    ///
    /// This is the consumer-facing data model of Chargy. It is what every
    /// supported charge transparency data format is converted into, so that the
    /// applications never have to know which meter vendor produced the data.
    /// </summary>
    /// <param name="Id">The identification of the charge transparency record.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Begin">An optional start of the covered time span.</param>
    /// <param name="End">An optional end of the covered time span.</param>
    /// <param name="Description">An optional multi-language description.</param>
    /// <param name="ChargingSessions">The charging sessions.</param>
    /// <param name="PublicKeys">Optional public keys to verify the charging sessions with.</param>
    /// <param name="Certainty">
    /// How sure we are that this record was parsed by the right parser, between
    /// 0.0 and 1.0. JSON charge transparency records do not always carry an
    /// unambiguous format identifier, so several parsers can be candidates for the
    /// same file and the best match wins.
    /// </param>
    /// <param name="Status">An optional overall verification status.</param>
    public class ChargeTransparencyRecord(String                        Id,
                                          IEnumerable<String>?          Context           = null,
                                          String?                       Begin             = null,
                                          String?                       End               = null,
                                          I18NString?                   Description       = null,
                                          IEnumerable<ChargingSession>? ChargingSessions  = null,
                                          IEnumerable<PublicKey>?       PublicKeys        = null,
                                          Double                        Certainty         = 0,
                                          SessionVerificationResult?    Status            = null)
    {

        #region Data

        private readonly List<ChargingSession>          chargingSessions          = [.. ChargingSessions ?? []];
        private readonly List<PublicKey>                publicKeys                = [.. PublicKeys       ?? []];
        private readonly List<Error>                    errors                    = [];
        private readonly List<Warning>                  warnings                  = [];
        private readonly List<ChargingStationOperator>  chargingStationOperators  = [];
        private readonly List<ChargingPool>             chargingPools             = [];
        private readonly List<ChargingStation>          chargingStations          = [];
        private readonly List<ChargingTariff>           chargingTariffs           = [];
        private readonly List<EMobilityProvider>        eMobilityProviders        = [];
        private readonly List<MediationService>         mediationServices         = [];
        private readonly List<Contract>                 contracts                 = [];
        private readonly List<ExtendedFileInfo>         invalidDataSets           = [];

        #endregion

        #region Properties

        /// <summary>The identification of the charge transparency record.</summary>
        public String                           Id                    { get; }      = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>            Context               { get; }      = Context?.ToArray() ?? [];

        /// <summary>An optional start of the covered time span.</summary>
        public String?                          Begin                 { get; }      = Begin;

        /// <summary>An optional end of the covered time span.</summary>
        public String?                          End                   { get; }      = End;

        /// <summary>An optional multi-language description.</summary>
        public I18NString?                      Description           { get; }      = Description;

        /// <summary>How sure we are that this record was parsed by the right parser.</summary>
        public Double                           Certainty             { get; set; } = Certainty;

        /// <summary>An optional overall verification status.</summary>
        public SessionVerificationResult?       Status                { get; set; } = Status;

        /// <summary>The charging sessions.</summary>
        public IReadOnlyList<ChargingSession>   ChargingSessions
            => chargingSessions;

        /// <summary>Optional public keys to verify the charging sessions with.</summary>
        public IReadOnlyList<PublicKey>         PublicKeys
            => publicKeys;

        /// <summary>Everything that made the verification of this record fail.</summary>
        public IReadOnlyList<Error>             Errors
            => errors;

        /// <summary>Everything that looked suspicious about this record.</summary>
        public IReadOnlyList<Warning>           Warnings
            => warnings;

        /// <summary>The charging station operators of this record.</summary>
        public IReadOnlyList<ChargingStationOperator>  ChargingStationOperators
            => chargingStationOperators;

        /// <summary>Charging pools that are not attached to an operator.</summary>
        public IReadOnlyList<ChargingPool>             ChargingPools
            => chargingPools;

        /// <summary>Charging stations that are not attached to a pool or an operator.</summary>
        public IReadOnlyList<ChargingStation>          ChargingStations
            => chargingStations;

        /// <summary>Charging tariffs that apply to the whole record.</summary>
        public IReadOnlyList<ChargingTariff>           ChargingTariffs
            => chargingTariffs;

        /// <summary>The e-mobility providers of this record.</summary>
        public IReadOnlyList<EMobilityProvider>        EMobilityProviders
            => eMobilityProviders;

        /// <summary>The mediation services an EV driver can turn to.</summary>
        public IReadOnlyList<MediationService>         MediationServices
            => mediationServices;

        /// <summary>The charging contracts this record was produced under.</summary>
        public IReadOnlyList<Contract>                 Contracts
            => contracts;

        /// <summary>
        /// The input files that could not be turned into charging sessions.
        ///
        /// These are kept rather than dropped, so that an application can tell an
        /// EV driver which of the files they provided were not understood, instead
        /// of silently verifying only some of them.
        /// </summary>
        public IReadOnlyList<ExtendedFileInfo>         InvalidDataSets
            => invalidDataSets;

        /// <summary>The result of verifying this record as a whole.</summary>
        public SessionCryptoResult?             VerificationResult    { get; set; }

        #endregion


        #region AddChargingSession(ChargingSession)

        /// <summary>
        /// Add a charging session to this charge transparency record.
        /// </summary>
        /// <param name="ChargingSession">A charging session.</param>
        public ChargeTransparencyRecord AddChargingSession(ChargingSession ChargingSession)
        {
            chargingSessions.Add(ChargingSession);
            return this;
        }

        #endregion

        #region AddPublicKey       (PublicKey)

        /// <summary>
        /// Add a public key to this charge transparency record.
        /// </summary>
        /// <param name="PublicKey">A public key.</param>
        public ChargeTransparencyRecord AddPublicKey(PublicKey PublicKey)
        {
            publicKeys.Add(PublicKey);
            return this;
        }

        #endregion

        #region Add...             (entities)

        /// <summary>
        /// Add a charging station operator to this charge transparency record.
        /// </summary>
        /// <param name="ChargingStationOperator">A charging station operator.</param>
        public ChargeTransparencyRecord AddChargingStationOperator(ChargingStationOperator ChargingStationOperator)
        {
            chargingStationOperators.Add(ChargingStationOperator);
            return this;
        }

        /// <summary>
        /// Add a charging pool to this charge transparency record.
        /// </summary>
        /// <param name="ChargingPool">A charging pool.</param>
        public ChargeTransparencyRecord AddChargingPool(ChargingPool ChargingPool)
        {
            chargingPools.Add(ChargingPool);
            return this;
        }

        /// <summary>
        /// Add a charging station to this charge transparency record.
        /// </summary>
        /// <param name="ChargingStation">A charging station.</param>
        public ChargeTransparencyRecord AddChargingStation(ChargingStation ChargingStation)
        {
            chargingStations.Add(ChargingStation);
            return this;
        }

        /// <summary>
        /// Add a charging tariff to this charge transparency record.
        /// </summary>
        /// <param name="ChargingTariff">A charging tariff.</param>
        public ChargeTransparencyRecord AddChargingTariff(ChargingTariff ChargingTariff)
        {
            chargingTariffs.Add(ChargingTariff);
            return this;
        }

        /// <summary>
        /// Add an e-mobility provider to this charge transparency record.
        /// </summary>
        /// <param name="EMobilityProvider">An e-mobility provider.</param>
        public ChargeTransparencyRecord AddEMobilityProvider(EMobilityProvider EMobilityProvider)
        {
            eMobilityProviders.Add(EMobilityProvider);
            return this;
        }

        /// <summary>
        /// Add a mediation service to this charge transparency record.
        /// </summary>
        /// <param name="MediationService">A mediation service.</param>
        public ChargeTransparencyRecord AddMediationService(MediationService MediationService)
        {
            mediationServices.Add(MediationService);
            return this;
        }

        /// <summary>
        /// Add a contract to this charge transparency record.
        /// </summary>
        /// <param name="Contract">A contract.</param>
        public ChargeTransparencyRecord AddContract(Contract Contract)
        {
            contracts.Add(Contract);
            return this;
        }

        /// <summary>
        /// Add an input file that could not be turned into charging sessions.
        /// </summary>
        /// <param name="InvalidDataSet">An input file that was not understood.</param>
        public ChargeTransparencyRecord AddInvalidDataSet(ExtendedFileInfo InvalidDataSet)
        {
            invalidDataSets.Add(InvalidDataSet);
            return this;
        }

        #endregion

        #region AddError           (Error)

        /// <summary>
        /// Add an error to this charge transparency record.
        /// </summary>
        /// <param name="Error">An error.</param>
        public ChargeTransparencyRecord AddError(Error Error)
        {
            errors.Add(Error);
            return this;
        }

        #endregion

        #region AddWarning         (Warning)

        /// <summary>
        /// Add a warning to this charge transparency record.
        /// </summary>
        /// <param name="Warning">A warning.</param>
        public ChargeTransparencyRecord AddWarning(Warning Warning)
        {
            warnings.Add(Warning);
            return this;
        }

        #endregion


        #region (static) IsAChargeTransparencyRecord(JSON)

        /// <summary>
        /// Whether the given JSON looks like a charge transparency record.
        ///
        /// Deliberately as lenient as ChargyCore.TS: a record is recognised by
        /// having a beginning and charging sessions, because the container formats
        /// in the wild disagree about everything else.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        public static Boolean IsAChargeTransparencyRecord(JObject JSON)

            => JSON["begin"]            is not null &&
               JSON["chargingSessions"] is not null;

        #endregion

        #region (static) TryParse(JSON, out ChargeTransparencyRecord)

        /// <summary>
        /// Try to parse the given JSON as a charge transparency record.
        /// </summary>
        /// <param name="JSON">A JSON representation of a charge transparency record.</param>
        /// <param name="ChargeTransparencyRecord">The parsed charge transparency record.</param>
        public static Boolean TryParse(JObject JSON, out ChargeTransparencyRecord? ChargeTransparencyRecord)
        {

            ChargeTransparencyRecord = null;

            var id = JSON["@id"]?.Value<String>();

            if (String.IsNullOrWhiteSpace(id))
                return false;

            var record = new chargy.ChargeTransparencyRecord(
                             id,
                             PublicKey.ParseContext(JSON["@context"]),
                             JSON["begin"]?.Value<String>(),
                             JSON["end"]?.  Value<String>(),
                             JSON["description"] is JObject descriptionJSON
                                 ? I18NString.Parse(descriptionJSON)
                                 : null,
                             null,
                             null,
                             JSON["certainty"]?.Value<Double>() ?? 0,
                             SessionVerificationResultExtensions.TryParse(JSON["status"]?.Value<String>() ?? "")
                         );

            if (JSON["chargingSessions"] is JArray sessionArray)
                foreach (var sessionJSON in sessionArray.OfType<JObject>())
                    if (ChargingSession.TryParse(sessionJSON, out var chargingSession))
                        record.AddChargingSession(chargingSession!);

            if (JSON["publicKeys"] is JArray publicKeyArray)
                foreach (var publicKeyJSON in publicKeyArray.OfType<JObject>())
                    if (PublicKey.TryParse(publicKeyJSON, out var publicKey))
                        record.AddPublicKey(publicKey!);

            if (JSON["chargingStationOperators"] is JArray operatorArray)
                foreach (var operatorJSON in operatorArray.OfType<JObject>())
                    if (ChargingStationOperator.TryParse(operatorJSON, out var chargingStationOperator))
                        record.AddChargingStationOperator(chargingStationOperator!);

            if (JSON["chargingPools"]    is JArray poolArray)
                foreach (var poolJSON in poolArray.OfType<JObject>())
                    if (ChargingPool.   TryParse(poolJSON,    out var chargingPool))
                        record.AddChargingPool(chargingPool!);

            if (JSON["chargingStations"] is JArray stationArray)
                foreach (var stationJSON in stationArray.OfType<JObject>())
                    if (ChargingStation.TryParse(stationJSON, out var chargingStation))
                        record.AddChargingStation(chargingStation!);

            foreach (var tariff in EntityLists.ParseChargingTariffs(JSON["chargingTariffs"]))
                record.AddChargingTariff(tariff);

            if (JSON["eMobilityProviders"] is JArray providerArray)
                foreach (var providerJSON in providerArray.OfType<JObject>())
                    if (EMobilityProvider.TryParse(providerJSON, out var eMobilityProvider))
                        record.AddEMobilityProvider(eMobilityProvider!);

            if (JSON["mediationServices"]  is JArray mediationServiceArray)
                foreach (var mediationServiceJSON in mediationServiceArray.OfType<JObject>())
                    if (MediationService. TryParse(mediationServiceJSON, out var mediationService))
                        record.AddMediationService(mediationService!);

            if (JSON["contracts"]          is JArray contractArray)
                foreach (var contractJSON in contractArray.OfType<JObject>())
                    if (Contract.         TryParse(contractJSON, out var contract))
                        record.AddContract(contract!);

            ChargeTransparencyRecord = record;

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this charge transparency record.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("@id", Id)
                       );

            if (Context.Count == 1)
                json.Add(new JProperty("@context",            Context[0]));

            else if (Context.Count > 1)
                json.Add(new JProperty("@context",            new JArray(Context)));

            if (Begin       is not null)
                json.Add(new JProperty("begin",               Begin));

            if (End         is not null)
                json.Add(new JProperty("end",                 End));

            if (Description.IsNotNullOrEmpty())
                json.Add(new JProperty("description",         Description.ToJSON()));

            if (contracts.               Count > 0)
                json.Add(new JProperty("contracts",                 new JArray(contracts.               Select(contract         => contract.        ToJSON()))));

            if (chargingStationOperators.Count > 0)
                json.Add(new JProperty("chargingStationOperators",  new JArray(chargingStationOperators.Select(csOperator       => csOperator.      ToJSON()))));

            if (chargingPools.           Count > 0)
                json.Add(new JProperty("chargingPools",             new JArray(chargingPools.           Select(pool             => pool.            ToJSON()))));

            if (chargingStations.        Count > 0)
                json.Add(new JProperty("chargingStations",          new JArray(chargingStations.        Select(station          => station.         ToJSON()))));

            if (chargingTariffs.         Count > 0)
                json.Add(new JProperty("chargingTariffs",           new JArray(chargingTariffs.         Select(tariff           => tariff.          ToJSON()))));

            if (eMobilityProviders.      Count > 0)
                json.Add(new JProperty("eMobilityProviders",        new JArray(eMobilityProviders.      Select(provider         => provider.        ToJSON()))));

            if (mediationServices.       Count > 0)
                json.Add(new JProperty("mediationServices",         new JArray(mediationServices.       Select(mediationService => mediationService.ToJSON()))));

            if (publicKeys.Count > 0)
                json.Add(new JProperty("publicKeys",          new JArray(publicKeys.      Select(publicKey => publicKey.ToJSON()))));

            json.Add(new JProperty("chargingSessions",        new JArray(chargingSessions.Select(session   => session.  ToJSON()))));

            if (errors.  Count > 0)
                json.Add(new JProperty("errors",              new JArray(errors.  Select(error   => error.  ToJSON()))));

            if (warnings.Count > 0)
                json.Add(new JProperty("warnings",            new JArray(warnings.Select(warning => warning.ToJSON()))));

            if (Status.HasValue)
                json.Add(new JProperty("status",              Status.Value.AsText()));

            if (VerificationResult is not null)
                json.Add(new JProperty("verificationResult",  VerificationResult.ToJSON()));

            json.Add(new JProperty("certainty",               Certainty));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this charge transparency record.
        /// </summary>
        public override String ToString()

            => $"{Id}: {chargingSessions.Count} charging session(s)";

        #endregion


    }

}
