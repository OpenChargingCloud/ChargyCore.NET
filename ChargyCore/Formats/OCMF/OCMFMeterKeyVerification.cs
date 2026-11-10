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

using System.Text.RegularExpressions;

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.Formats.OCMF
{

    /// <summary>
    /// What a meter key binding says about the time its key was the meter's.
    /// </summary>
    public enum MeterKeyAssociation
    {

        /// <summary>The key is the meter's now, as observed; nothing is said about earlier readings.</summary>
        CurrentSnapshot,

        /// <summary>The key was the meter's at the reading times the binding names.</summary>
        HistoricalAttestation

    }


    /// <summary>
    /// How the original readings of a charging session came out against a key
    /// the caller vouches for.
    /// </summary>
    public enum MeterKeyVerificationStatus
    {

        /// <summary>Every original reading verified with the authorized key.</summary>
        Verified,

        /// <summary>The key the container brought along is another one; the readings were checked with the authorized key anyway.</summary>
        KeyConflict,

        /// <summary>The signed meter or EVSE is not the one the binding is about.</summary>
        IdentityConflict,

        /// <summary>What the session displays differs from what the original documents say.</summary>
        RecordConflict,

        /// <summary>An original reading does not verify with the authorized key.</summary>
        InvalidSignature,

        /// <summary>The input or the key is of a kind this check does not handle.</summary>
        Unsupported,

        /// <summary>The binding was not current when the check began or ended.</summary>
        EvidenceExpired

    }


    /// <summary>
    /// Whether a result says the key was the meter's when the readings were taken.
    /// </summary>
    public enum OCMFHistoricalAssociation
    {

        /// <summary>Nothing is said about the time of the readings.</summary>
        NotProven,

        /// <summary>The roaming network operator attested the key for exactly the reading times, and the signatures verified with it.</summary>
        RNOAttestedAtReadingTimes

    }


    /// <summary>
    /// Which part of a charging session the original readings cover.
    /// </summary>
    public enum OCMFReadingPhase
    {

        /// <summary>A single start reading, before an end reading exists.</summary>
        StartOnly,

        /// <summary>A start and an end reading.</summary>
        Complete,

        /// <summary>Anything else.</summary>
        Partial

    }


    /// <summary>
    /// The measurement key of a meter, as an authorized caller hands it over.
    /// </summary>
    /// <param name="Id">The key's identifier.</param>
    /// <param name="IdScheme">How the identifier was made, e.g. "sha256-spki".</param>
    /// <param name="CanonicalId">The identifier the caller compares keys by.</param>
    /// <param name="Value">The key.</param>
    /// <param name="Encodings">How the key is encoded, innermost first, e.g. SubjectPublicKeyInfo, DER, base64.</param>
    /// <param name="Algorithm">The key type, e.g. "ECDSA-secp256r1".</param>
    public sealed record AuthorizedMeterKey(String                 Id,
                                            String                 IdScheme,
                                            String                 CanonicalId,
                                            String                 Value,
                                            IReadOnlyList<String>  Encodings,
                                            String                 Algorithm);


    /// <summary>
    /// Where a meter key binding comes from, and how long it may be relied on.
    /// </summary>
    /// <param name="VerifiedAt">When the caller verified its trust evidence.</param>
    /// <param name="ValidUntil">Until when that verification may be relied on.</param>
    /// <param name="Association">What the binding says about the time the key was the meter's.</param>
    /// <param name="RoamingNetworkId">The roaming network that vouches for the key.</param>
    /// <param name="RoamingNetworkOperatorId">The operator of that roaming network.</param>
    /// <param name="CommitId">The commit of the network's key statements the key was found in.</param>
    /// <param name="Revision">The revision of that commit.</param>
    /// <param name="TreeRoot">The root of the tree the commit belongs to.</param>
    /// <param name="KeyStatementDigest">The digest of the key statement.</param>
    /// <param name="MeasurementTimes">For a historical attestation: the reading times it covers.</param>
    public sealed record MeterKeyBindingEvidence(DateTimeOffset                  VerifiedAt,
                                                 DateTimeOffset                  ValidUntil,
                                                 MeterKeyAssociation             Association,
                                                 String                          RoamingNetworkId,
                                                 String                          RoamingNetworkOperatorId,
                                                 String                          CommitId,
                                                 UInt64                          Revision,
                                                 String                          TreeRoot,
                                                 String                          KeyStatementDigest,
                                                 IReadOnlyList<DateTimeOffset>?  MeasurementTimes   = null);


    /// <summary>
    /// A meter key an authorized caller vouches for, bound to the EVSE and the
    /// meter it measures with.
    ///
    /// The caller verifies its own trust evidence — a roaming network's key
    /// statements, say — before it builds one. Nothing in a charge transparency
    /// file can make one: importing JSON that carries these fields never grants
    /// any trust.
    /// </summary>
    /// <param name="EVSEId">The EVSE the key measures for.</param>
    /// <param name="ChargingStationId">The charging station of that EVSE.</param>
    /// <param name="MeterId">The meter's identifier in the roaming network.</param>
    /// <param name="MeterSerialNumber">The serial number the meter signs its readings with, OCMF "MS".</param>
    /// <param name="Key">The key.</param>
    /// <param name="Evidence">Where the binding comes from, and how long it may be relied on.</param>
    public sealed record MeterKeyBinding(String                   EVSEId,
                                         String                   ChargingStationId,
                                         String                   MeterId,
                                         String                   MeterSerialNumber,
                                         AuthorizedMeterKey       Key,
                                         MeterKeyBindingEvidence  Evidence);


    /// <summary>
    /// A key the container of a charge transparency record brought along with
    /// its readings.
    /// </summary>
    /// <param name="Value">The key.</param>
    /// <param name="Encodings">How the key is encoded, innermost first.</param>
    public sealed record OCMFContainerKey(String                 Value,
                                          IReadOnlyList<String>  Encodings);


    /// <summary>
    /// The original OCMF documents of a charging session, and the keys its
    /// container brought along.
    /// </summary>
    /// <param name="Documents">The original documents, each once, in the order they were read.</param>
    /// <param name="MeterSerialNumber">The one meter the readings were taken on.</param>
    /// <param name="ContainerKeys">The keys the container brought along, if any.</param>
    public sealed record OCMFSessionMaterial(IReadOnlyList<String>            Documents,
                                             String                           MeterSerialNumber,
                                             IReadOnlyList<OCMFContainerKey>  ContainerKeys);


    /// <summary>
    /// How the original readings of a charging session came out against a key
    /// the caller vouches for.
    /// </summary>
    public sealed class BoundOCMFResult
    {

        #region Properties

        /// <summary>The verdict.</summary>
        public MeterKeyVerificationStatus      Status                   { get; }

        /// <summary>Whether every original reading verified with the authorized key.</summary>
        public Boolean                         SignaturesValid          { get; }

        /// <summary>How many original documents were checked.</summary>
        public Int32                           SignatureCount           { get; }

        /// <summary>How many readings they held.</summary>
        public Int32                           MeasurementCount         { get; }

        /// <summary>What the binding says about the time its key was the meter's.</summary>
        public MeterKeyAssociation             Association
            => Binding.Evidence.Association;

        /// <summary>The reading times the binding covers, for a historical attestation.</summary>
        public IReadOnlyList<DateTimeOffset>?  MeasurementTimes
            => Binding.Evidence.MeasurementTimes;

        /// <summary>
        /// Whether the result says the key was the meter's when the readings were
        /// taken: only a historical attestation whose readings all verified does.
        /// </summary>
        public OCMFHistoricalAssociation       HistoricalAssociation    { get; }

        /// <summary>Which part of the charging session the readings cover.</summary>
        public OCMFReadingPhase                ReadingPhase             { get; }

        /// <summary>The binding the readings were checked against.</summary>
        public MeterKeyBinding                 Binding                  { get; }

        /// <summary>Why the verdict is what it is.</summary>
        public String                          Detail                   { get; }

        /// <summary>The record read anew from the original documents with the authorized key, once it was.</summary>
        public ChargeTransparencyRecord?       Record                   { get; }

        #endregion

        #region Constructor(s)

        internal BoundOCMFResult(MeterKeyVerificationStatus  Status,
                                 String                      Detail,
                                 MeterKeyBinding             Binding,
                                 OCMFReadingPhase            ReadingPhase,
                                 Int32                       SignatureCount    = 0,
                                 Int32                       MeasurementCount  = 0,
                                 ChargeTransparencyRecord?   Record            = null,
                                 Boolean                     SignaturesValid   = false)
        {

            this.Status                 = Status;
            this.Detail                 = Detail;
            this.Binding                = Binding;
            this.ReadingPhase           = ReadingPhase;
            this.SignatureCount         = SignatureCount;
            this.MeasurementCount       = MeasurementCount;
            this.Record                 = Record;
            this.SignaturesValid        = SignaturesValid;

            this.HistoricalAssociation  = Binding.Evidence.Association == MeterKeyAssociation.HistoricalAttestation &&
                                          SignaturesValid &&
                                          (Status == MeterKeyVerificationStatus.Verified || Status == MeterKeyVerificationStatus.KeyConflict)

                                              ? OCMFHistoricalAssociation.RNOAttestedAtReadingTimes
                                              : OCMFHistoricalAssociation.NotProven;

        }

        #endregion

        #region (override) ToString()

        /// <inheritdoc/>
        public override String ToString()

            => $"{Status}: {Detail}";

        #endregion

    }


    /// <summary>
    /// Whether every original document of a charging session verified with its
    /// historical key.
    /// </summary>
    public enum HistoricalOCMFStatus
    {

        /// <summary>Every original document verified.</summary>
        Verified,

        /// <summary>At least one did not.</summary>
        Failed

    }


    /// <summary>
    /// How the original documents of a charging session came out, each against
    /// the key that was the meter's when it was signed.
    /// </summary>
    /// <param name="Checks">One check per original document.</param>
    public sealed class HistoricalOCMFResult(IEnumerable<BoundOCMFResult> Checks)
    {

        /// <summary>The time basis: the timestamps the meter signed, not the time of the check.</summary>
        public const String TimeBasis = "signed-measurement-timestamps";

        /// <summary>One check per original document.</summary>
        public IReadOnlyList<BoundOCMFResult>  Checks              { get; } = [ .. Checks ];

        /// <summary>Whether every original document verified.</summary>
        public HistoricalOCMFStatus            Status
            => this.Checks.Count > 0 && this.Checks.All(check => check.Status == MeterKeyVerificationStatus.Verified)
                   ? HistoricalOCMFStatus.Verified
                   : HistoricalOCMFStatus.Failed;

        /// <summary>Whether every original reading verified with its key.</summary>
        public Boolean                         SignaturesValid
            => this.Checks.Count > 0 && this.Checks.All(check => check.SignaturesValid);

        /// <summary>How many original documents were checked.</summary>
        public Int32                           SignatureCount
            => this.Checks.Sum(check => check.SignatureCount);

        /// <summary>How many readings they held.</summary>
        public Int32                           MeasurementCount
            => this.Checks.Sum(check => check.MeasurementCount);

    }


    /// <summary>
    /// Verify the original OCMF readings of a charging session with a meter key
    /// an authorized caller vouches for — one a roaming network attests for the
    /// EVSE, rather than the one the charge transparency file brought along.
    ///
    /// ChargyCore has no dependency on such a network: the caller verifies its
    /// own trust evidence and hands over a <see cref="MeterKeyBinding"/>. What is
    /// checked here is the readings: every original document is read anew with
    /// exactly that key, its signed meter and EVSE are compared with the binding,
    /// and what the session displays with what the documents say. Validation
    /// results the session already carries are not looked at, and nothing signed
    /// is changed.
    ///
    /// The pilot handles P-256 keys, as a canonical uncompressed
    /// SubjectPublicKeyInfo.
    /// </summary>
    public static class MeterKeyVerification
    {

        #region Data

        /// <summary>
        /// A canonical uncompressed P-256 SubjectPublicKeyInfo, hexadecimal. Other
        /// encodings of the same key are a limitation of the pilot, never evidence
        /// of a different key.
        /// </summary>
        private static readonly Regex pilotSPKI = new ("^3059301306072a8648ce3d020106082a8648ce3d03010703420004[0-9a-f]{128}$",
                                                       RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>The key type the pilot handles.</summary>
        public const String PilotAlgorithm = "ECDSA-secp256r1";

        #endregion


        #region ReadOCMFSessionMaterial      (Session)

        /// <summary>
        /// The original OCMF documents of a charging session, and the keys its
        /// container brought along — read from the documents themselves, never
        /// from validation results the session carries.
        /// </summary>
        /// <param name="Session">A charging session read from OCMF documents.</param>
        /// <exception cref="ArgumentException">When a reading has no original document, a container key is not explicitly encoded, or the readings are not of exactly one meter.</exception>
        public static OCMFSessionMaterial ReadOCMFSessionMaterial(ChargingSession Session)

            => ReadOCMFSessionMaterial(ReadingsOf(Session));

        #endregion

        #region VerifyOCMFSessionWithMeterKey(Session, Binding, Now = null, I18N = null, CancellationToken = default)

        /// <summary>
        /// Read every original OCMF document of a charging session anew with the
        /// key the caller vouches for, and say how its readings came out.
        /// </summary>
        /// <param name="Session">A charging session read from OCMF documents.</param>
        /// <param name="Binding">The key, bound to the EVSE and the meter.</param>
        /// <param name="Now">The clock the binding's validity is checked against.</param>
        /// <param name="I18N">The dictionary the documents are read with.</param>
        /// <param name="CancellationToken">Cancels the check; its results are then discarded.</param>
        /// <exception cref="OperationCanceledException">When the check was cancelled.</exception>
        public static BoundOCMFResult VerifyOCMFSessionWithMeterKey(ChargingSession         Session,
                                                                    MeterKeyBinding         Binding,
                                                                    Func<DateTimeOffset>?   Now                = null,
                                                                    I18NDictionary?         I18N               = null,
                                                                    CancellationToken       CancellationToken  = default)

            => Verify(
                   Session.EVSEId,
                   ReadingsOf(Session),
                   Binding,
                   Now ?? (() => DateTimeOffset.UtcNow),
                   I18N ?? I18NDictionary.Default(),
                   CancellationToken
               );

        #endregion

        #region VerifyHistoricalOCMFSession  (Session, Bindings, Now = null, I18N = null, CancellationToken = default)

        /// <summary>
        /// Verify every original OCMF document of a charging session once, with
        /// the one key the caller vouches for at the times of its readings.
        ///
        /// Different documents may have been signed with different keys — a meter
        /// whose key was replaced between start and end — but one document never
        /// silently switches keys between its readings, and every reading needs
        /// exactly one binding that covers its time.
        /// </summary>
        /// <param name="Session">A charging session read from OCMF documents.</param>
        /// <param name="Bindings">Historical bindings, each covering the reading times it names.</param>
        /// <param name="Now">The clock the bindings' validity is checked against.</param>
        /// <param name="I18N">The dictionary the documents are read with.</param>
        /// <param name="CancellationToken">Cancels the check; its results are then discarded.</param>
        /// <exception cref="ArgumentException">When a reading is covered by no binding or by more than one, or a document's readings by different keys.</exception>
        /// <exception cref="OperationCanceledException">When the check was cancelled.</exception>
        public static HistoricalOCMFResult VerifyHistoricalOCMFSession(ChargingSession               Session,
                                                                       IEnumerable<MeterKeyBinding>  Bindings,
                                                                       Func<DateTimeOffset>?         Now                = null,
                                                                       I18NDictionary?               I18N               = null,
                                                                       CancellationToken             CancellationToken  = default)
        {

            var readings  = ReadingsOf(Session);
            var material  = ReadOCMFSessionMaterial(readings);
            var bindings  = Bindings.ToArray();
            var checks    = new List<BoundOCMFResult>();

            foreach (var raw in material.Documents)
            {

                CancellationToken.ThrowIfCancellationRequested();

                var ofDocument  = readings.Where(reading => reading.OCMF?.Document.Raw == raw).ToArray();
                var times       = ofDocument.Select(reading => reading.Value.ParsedTimestamp).ToArray();

                var selected    = times.Select(time => {

                                      var candidates = bindings.Where(binding => binding.Evidence.Association == MeterKeyAssociation.HistoricalAttestation &&
                                                                                 binding.Evidence.MeasurementTimes?.Contains(time) == true).
                                                                ToArray();

                                      if (candidates.Length != 1)
                                          throw new ArgumentException("Each original reading needs exactly one explicitly authorized historical binding", nameof(Bindings));

                                      return candidates[0];

                                  }).ToArray();

                var binding     = selected.FirstOrDefault();

                if (binding is null || selected.Any(other => other.Key.CanonicalId   != binding.Key.CanonicalId ||
                                                             other.EVSEId            != binding.EVSEId            ||
                                                             other.MeterSerialNumber != binding.MeterSerialNumber))
                {
                    throw new ArgumentException("One original signed document cannot silently switch meter keys between readings", nameof(Bindings));
                }

                var combined    = binding with {
                                      Evidence = binding.Evidence with {
                                                     MeasurementTimes  = times,
                                                     VerifiedAt        = selected.Max(item => item.Evidence.VerifiedAt),
                                                     ValidUntil        = selected.Min(item => item.Evidence.ValidUntil)
                                                 }
                                  };

                checks.Add(
                    Verify(
                        Session.EVSEId,
                        ofDocument,
                        combined,
                        Now ?? (() => DateTimeOffset.UtcNow),
                        I18N ?? I18NDictionary.Default(),
                        CancellationToken
                    )
                );

            }

            return new HistoricalOCMFResult(checks);

        }

        #endregion


        #region (internal) Reading / ReadingsOf(Session)

        /// <summary>
        /// One reading of a charging session, with the measurement it belongs to.
        /// </summary>
        internal readonly record struct Reading(Measurement       Measurement,
                                                MeasurementValue  Value)
        {

            /// <summary>The reading as an OCMF one, when it is.</summary>
            public OCMFMeasurementValue? OCMF
                => Value as OCMFMeasurementValue;

        }

        internal static Reading[] ReadingsOf(ChargingSession Session)

            => [ .. Session.Measurements.SelectMany(measurement => measurement.Values.Select(value => new Reading(measurement, value))) ];

        #endregion

        #region (internal) ReadOCMFSessionMaterial(Readings)

        internal static OCMFSessionMaterial ReadOCMFSessionMaterial(IEnumerable<Reading> Readings)
        {

            var documents      = new List<String>();
            var meterIds       = new HashSet<String>(StringComparer.Ordinal);
            var containerKeys  = new List<OCMFContainerKey>();

            foreach (var reading in Readings)
            {

                meterIds.Add(reading.Measurement.EnergyMeterId);

                var document = reading.OCMF?.Document;

                if (document is null || document.Raw.Length == 0)
                    throw new ArgumentException("Original OCMF documents are required for proven-key verification");

                if (!documents.Contains(document.Raw))
                    documents.Add(document.Raw);

                if (document.PublicKey is not null)
                {

                    if (document.PublicKeyEncoding is not ("hex" or "base64"))
                        throw new ArgumentException("An explicit hex/base64 SPKI container key is required");

                    containerKeys.Add(new OCMFContainerKey(document.PublicKey, [ "SubjectPublicKeyInfo", "DER", document.PublicKeyEncoding ]));

                }

            }

            if (documents.Count == 0 || meterIds.Count != 1)
                throw new ArgumentException("Exactly one meter and original signed OCMF data are required");

            return new OCMFSessionMaterial(documents, meterIds.First(), containerKeys);

        }

        #endregion

        #region (internal) Verify(EVSEId, Readings, Binding, Now, I18N, CancellationToken)

        /// <summary>
        /// What every check of readings against an authorized key comes down to,
        /// for all readings of a session or only those of one document.
        /// </summary>
        internal static BoundOCMFResult Verify(String?                EVSEId,
                                               IReadOnlyList<Reading>  Readings,
                                               MeterKeyBinding        Binding,
                                               Func<DateTimeOffset>   Now,
                                               I18NDictionary         I18N,
                                               CancellationToken      CancellationToken)
        {

            var readingPhase = OCMFReadingPhase.Partial;

            BoundOCMFResult Finish(MeterKeyVerificationStatus  Status,
                                   String                      Detail,
                                   Int32                       SignatureCount    = 0,
                                   Int32                       MeasurementCount  = 0,
                                   ChargeTransparencyRecord?   Record            = null,
                                   Boolean                     SignaturesValid   = false)

                => new (Status, Detail, Binding, readingPhase, SignatureCount, MeasurementCount, Record, SignaturesValid);

            CancellationToken.ThrowIfCancellationRequested();

            #region The binding: current, and about this EVSE

            var current = Now();

            if (Binding.Evidence.VerifiedAt > current ||
                Binding.Evidence.ValidUntil < current ||
                !Enum.IsDefined(Binding.Evidence.Association))
            {
                return Finish(MeterKeyVerificationStatus.EvidenceExpired,
                              "The supplied observation is not current; reconstruct authorization and verify its proof again");
            }

            if (EVSEId != Binding.EVSEId || Binding.MeterSerialNumber.Length == 0)
                return Finish(MeterKeyVerificationStatus.IdentityConflict,
                              "The requested session EVSE differs from the authorized key binding");

            if (Binding.Key.Algorithm != PilotAlgorithm)
                return Finish(MeterKeyVerificationStatus.Unsupported,
                              "The bound-key OCMF pilot supports P-256 only");

            #endregion

            #region The original documents, and what the session displays of them

            OCMFSessionMaterial material;
            DisplayedReading[]  expected;

            try
            {
                material  = ReadOCMFSessionMaterial(Readings);
                expected  = Displayed(Readings);
            }
            catch (Exception e) when (e is ArgumentException or FormatException)
            {
                return Finish(MeterKeyVerificationStatus.Unsupported, e.Message);
            }

            if (material.MeterSerialNumber != Binding.MeterSerialNumber)
                return Finish(MeterKeyVerificationStatus.IdentityConflict,
                              "The session meter differs from the explicitly associated EVSE meter");

            #endregion

            #region The keys: the authorized one, and those the container brought along

            var key = LiveLinkPublicKey.Read(Binding.Key.Value, Binding.Key.Encodings, Binding.Key.Algorithm);

            if (key.KeyHEX is null)
                return Finish(MeterKeyVerificationStatus.Unsupported,
                              key.ReasonKey ?? "The authorized public key cannot be decoded");

            if (!pilotSPKI.IsMatch(key.KeyHEX))
                return Finish(MeterKeyVerificationStatus.Unsupported,
                              "The P-256 pilot requires canonical uncompressed SPKI");

            // A container key that differs is reported, never silently replaced,
            // and the readings are still checked with the authorized key: whether
            // they verify with it is the other half of what the caller needs.
            var conflict = false;

            foreach (var supplied in material.ContainerKeys)
            {

                var decoded = LiveLinkPublicKey.Read(supplied.Value, supplied.Encodings, Binding.Key.Algorithm);

                if (decoded.KeyHEX is null)
                    return Finish(MeterKeyVerificationStatus.Unsupported,
                                  decoded.ReasonKey ?? "The supplied container key cannot be decoded");

                if (!pilotSPKI.IsMatch(decoded.KeyHEX))
                    return Finish(MeterKeyVerificationStatus.Unsupported,
                                  "The supplied key needs a supported uncompressed P-256 encoding before identity comparison");

                if (!String.Equals(decoded.KeyHEX, key.KeyHEX, StringComparison.OrdinalIgnoreCase))
                    conflict = true;

            }

            #endregion

            #region The documents, read anew with the authorized key

            var parsed = new OCMFFormat(I18N).TryParse(material.Documents, key.KeyHEX, "hex", null);

            CancellationToken.ThrowIfCancellationRequested();

            if (parsed is not ChargeTransparencyRecord record)
                return Finish(MeterKeyVerificationStatus.Unsupported,
                              "The original OCMF documents could not be parsed with the authorized key");

            if (record.ChargingSessions.Count != 1)
                return Finish(MeterKeyVerificationStatus.IdentityConflict,
                              "The original readings do not describe exactly one charging session");

            var parsedReadings   = ReadingsOf(record.ChargingSessions[0]);
            var parsedDocuments  = new HashSet<String>(StringComparer.Ordinal);
            var transactions     = new HashSet<String>(StringComparer.Ordinal);
            var count            = 0;
            var valid            = true;

            foreach (var reading in parsedReadings)
            {

                var document = reading.OCMF?.Document;

                if (document is null || document.Raw.Length == 0)
                    return Finish(MeterKeyVerificationStatus.Unsupported,
                                  "Missing original parsed OCMF document");

                if (document.Payload["MS"]?.ToString()                         != Binding.MeterSerialNumber ||
                    document.Payload["CT"]?.ToString().ToUpperInvariant()      != "EVSEID"                  ||
                    document.Payload["CI"]?.ToString().Trim()                  != Binding.EVSEId)
                {
                    return Finish(MeterKeyVerificationStatus.IdentityConflict,
                                  "The signed OCMF meter/EVSE identification does not match the proven association");
                }

                if (document.Signature["SA"] is { } signatureAlgorithm && signatureAlgorithm.ToString() != "ECDSA-secp256r1-SHA256")
                    return Finish(MeterKeyVerificationStatus.Unsupported,
                                  "The signature algorithm does not match the authorized P-256 key");

                if (Binding.Evidence.Association == MeterKeyAssociation.HistoricalAttestation &&
                    Binding.Evidence.MeasurementTimes?.Contains(reading.Value.ParsedTimestamp) != true)
                {
                    return Finish(MeterKeyVerificationStatus.IdentityConflict,
                                  "The original reading timestamp is outside the explicitly proven historical coverage");
                }

                parsedDocuments.Add(document.Raw);
                valid = valid && reading.Value.Result?.Status == VerificationResult.ValidSignature;

                if (reading.OCMF?.Transaction is String transaction)
                    transactions.Add(transaction);

                count++;

            }

            readingPhase = transactions.Contains("B") && transactions.Contains("E") ? OCMFReadingPhase.Complete
                         : count == 1 && transactions.Contains("B")                 ? OCMFReadingPhase.StartOnly
                         :                                                            OCMFReadingPhase.Partial;

            if (count == 0 || parsedDocuments.Count != material.Documents.Count)
                return Finish(MeterKeyVerificationStatus.Unsupported,
                              "Not every original document yielded readings");

            if (!Displayed(parsedReadings).SequenceEqual(expected))
                return Finish(MeterKeyVerificationStatus.RecordConflict,
                              "Displayed measurement fields differ from the original signed documents");

            #endregion

            #region Still current?

            var checkedAt = Now();

            if (checkedAt < Binding.Evidence.VerifiedAt || checkedAt > Binding.Evidence.ValidUntil)
                return Finish(MeterKeyVerificationStatus.EvidenceExpired,
                              "The observation expired while the readings were being checked");

            #endregion

            return conflict

                       ? Finish(MeterKeyVerificationStatus.KeyConflict,
                                "The container key conflicts with the authorized EVSE key; no silent replacement",
                                parsedDocuments.Count, count, record, valid)

                       : valid

                             ? Finish(MeterKeyVerificationStatus.Verified,
                                      "Original reading signatures verified with the explicitly associated key; see the separate historical association result",
                                      parsedDocuments.Count, count, record, valid)

                             : Finish(MeterKeyVerificationStatus.InvalidSignature,
                                      "An original reading signature does not match the authorized key",
                                      parsedDocuments.Count, count, record, valid);

        }

        #endregion

        #region (private) DisplayedReading / Displayed(Readings)

        /// <summary>
        /// What a session shows of a reading: compared field by field, so that a
        /// value written with more decimal places is still the same value.
        /// </summary>
        private readonly record struct DisplayedReading(String          Meter,
                                                        DateTimeOffset  Timestamp,
                                                        Decimal         Value,
                                                        String?         Unit,
                                                        Int32           Scale,
                                                        String?         OBIS);

        private static DisplayedReading[] Displayed(IEnumerable<Reading> Readings)

            => [ .. Readings.Select(reading => new DisplayedReading(
                                                   reading.Measurement.EnergyMeterId,
                                                   reading.Value.ParsedTimestamp,
                                                   reading.Value.Value,
                                                   reading.Measurement.Unit,
                                                   reading.Measurement.Scale,
                                                   reading.Measurement.OBIS
                                               )) ];

        #endregion

    }

}
