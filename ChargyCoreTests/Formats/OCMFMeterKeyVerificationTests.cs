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
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NUnit.Framework;

using cloud.charging.open.chargy.Formats.OCMF;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// The port of "MeterKeyVerification.tests.ts" of ChargyCore.TS: OCMF
    /// readings checked with a meter key an authorized caller vouches for, now
    /// and at the times of the readings, and a price bound to the original start.
    ///
    /// The fixtures in "POISession" are shared byte-for-byte with ChargyCore.TS,
    /// which copied them from ChargyWebApp 97da24d. Every key in them is a test key.
    /// </summary>
    [TestFixture]
    public class OCMFMeterKeyVerificationTests : AChargyTests
    {

        #region Data

        private const String SessionFixture     = "POISession/completed-session-with-evse-proof.chargy";
        private const String HistoricalFixture  = "POISession/historical-session.chargy";
        private const String PriceFixture       = "POISession/price-session.chargy";

        private static readonly String           text              = ReadTextFixture(SessionFixture);
        private static readonly String           publicKey         = ChargyLib.ParseJSON(text)["publicKey"]!.Value<String>()!;
        private static readonly JObject          metadata          = ChargyLib.ParseJSON(ReadTextFixture("POISession/provenance.json"));

        private static readonly DateTimeOffset   at                = Date(metadata["generatedAt"]!);

        private static readonly MeterKeyBinding  binding           = new (
                                                                         EVSEId:             metadata["evseId"]!.Value<String>()!,
                                                                         ChargingStationId:  "DE*POI*S1",
                                                                         MeterId:            "DE*POI*M1",
                                                                         MeterSerialNumber:  metadata["meterSerial"]!.Value<String>()!,
                                                                         Key:                new MeterKey(
                                                                                                 Id:           metadata["meterKeyId"]!.Value<String>()!,
                                                                                                 IdScheme:     "sha256-spki",
                                                                                                 CanonicalId:  "provided-by-authorized-caller",
                                                                                                 Value:        publicKey,
                                                                                                 Encodings:    [ "SubjectPublicKeyInfo", "DER", "base64" ],
                                                                                                 Algorithm:    "ECDSA-secp256r1"
                                                                                             ),
                                                                         Evidence:           new MeterKeyEvidence(
                                                                                                 VerifiedAt:                at,
                                                                                                 ValidUntil:                Date(metadata["attestationsValidUntil"]!),
                                                                                                 Association:               MeterKeyAssociation.CurrentSnapshot,
                                                                                                 RoamingNetworkId:          "CHARGY-POI-DEMO",
                                                                                                 RoamingNetworkOperatorId:  "CHARGY-DEMO-RNO",
                                                                                                 CommitId:                  "demo",
                                                                                                 Revision:                  1,
                                                                                                 TreeRoot:                  "demo",
                                                                                                 KeyStatementDigest:        "demo"
                                                                                             )
                                                                     );

        #endregion


        #region Explicit authorized meter-key handoff to Core

        [Test]
        public async Task RechecksOriginalSignaturesWithSuppliedProvenance_IncludingTheFirstReading()
        {

            var original  = await Session();
            var result    = MeterKeyVerification.VerifyOCMFSessionWithMeterKey(original, binding, () => at);

            Assert.Multiple(() => {
                Assert.That(result.Status,                 Is.EqualTo(MeterKeyVerificationStatus.Verified), result.Detail);
                Assert.That(result.SignaturesValid,        Is.True);
                Assert.That(result.SignatureCount,         Is.EqualTo(2));
                Assert.That(result.ReadingPhase,           Is.EqualTo(ReadingPhase.Complete));
                Assert.That(result.MeasurementCount,       Is.EqualTo(2));
                Assert.That(result.HistoricalAssociation,  Is.EqualTo(HistoricalAssociation.NotProven));
                // ChargyCore.TS checks that the first reading of the record read
                // anew carries its crypto method, so that its details can be shown;
                // here a reading carries its own result instead.
                Assert.That(result.Record?.ChargingSessions[0].Measurements[0].Values[0].Result?.Status,  Is.EqualTo(VerificationResult.ValidSignature));
                Assert.That(MeterKeyVerification.ReadOCMFSessionMaterial(original).MeterSerialNumber,       Is.EqualTo(binding.MeterSerialNumber));
            });

        }

        [Test]
        public async Task NeverNeedsAKeyFromTheContainerToVerifyTheOriginalMeasurements()
        {

            var original = await Session();

            foreach (var document in DocumentsOf(original))
            {
                document.PublicKey          = null;
                document.PublicKeyEncoding  = null;
            }

            var result = MeterKeyVerification.VerifyOCMFSessionWithMeterKey(original, binding, () => at);

            Assert.That(result.Status, Is.EqualTo(MeterKeyVerificationStatus.Verified), result.Detail);

        }

        [Test]
        public async Task DetectsAConflictingContainerKey_ButStillReportsTheActualSignatureCheckWithTheAuthorizedKey()
        {

            var foreign = Regex.Replace(ReadTextFixture("ChargeTransparencyLive/OCMF-Test-01/publicKey_cpo_signCTRs.pem"), "-----[^-]+-----|\\s", "");
            var result  = MeterKeyVerification.VerifyOCMFSessionWithMeterKey(await Session(ReplaceFirst(text, publicKey, foreign)), binding, () => at);

            Assert.Multiple(() => {
                Assert.That(result.Status,           Is.EqualTo(MeterKeyVerificationStatus.KeyConflict), result.Detail);
                Assert.That(result.SignaturesValid,  Is.True);
            });

        }

        [Test]
        public async Task ComparesEquivalentSupportedEncodings_AndReportsCompressedEncodingsAsUnsupported_NotAsForeignKeys()
        {

            var original   = await Session();
            var spki       = Convert.FromBase64String(publicKey);
            var documents  = DocumentsOf(original);

            foreach (var document in documents)
            {
                document.PublicKey          = Convert.ToHexStringLower(spki);
                document.PublicKeyEncoding  = "hex";
            }

            Assert.That(MeterKeyVerification.VerifyOCMFSessionWithMeterKey(original, binding, () => at).Status,
                        Is.EqualTo(MeterKeyVerificationStatus.Verified));

            var compressed = "3039301306072a8648ce3d020106082a8648ce3d030107032200" +
                             (spki[^1] % 2 == 0 ? "02" : "03") +
                             Convert.ToHexStringLower(spki[27..59]);

            foreach (var document in documents)
                document.PublicKey = compressed;

            Assert.That(MeterKeyVerification.VerifyOCMFSessionWithMeterKey(original, binding, () => at).Status,
                        Is.EqualTo(MeterKeyVerificationStatus.Unsupported));

        }

        [Test]
        public async Task ATamperedReadingCannotInheritCachedValidValidationResults()
        {

            var result = MeterKeyVerification.VerifyOCMFSessionWithMeterKey(await Session(ReplaceFirst(text, "1011", "1012")), binding, () => at);

            Assert.Multiple(() => {
                Assert.That(result.Status,           Is.EqualTo(MeterKeyVerificationStatus.InvalidSignature), result.Detail);
                Assert.That(result.SignaturesValid,  Is.False);
            });

        }

        [Test]
        public async Task BindsBothTheSignedIdentityAndTheDisplayedMeasurementData()
        {

            var original  = await Session();
            var foreign   = MeterKeyVerification.VerifyOCMFSessionWithMeterKey(original, binding with { EVSEId = "FOREIGN" }, () => at);

            Assert.That(foreign.Status, Is.EqualTo(MeterKeyVerificationStatus.IdentityConflict));

            var first     = true;
            var mismatch  = MeterKeyVerification.VerifyOCMFSessionWithMeterKey(
                                Rebuilt(original, value => {
                                    var changed = first ? Copy(value, Value: 99) : value;
                                    first = false;
                                    return changed;
                                }),
                                binding,
                                () => at
                            );

            Assert.That(mismatch.Status, Is.EqualTo(MeterKeyVerificationStatus.RecordConflict));

        }

        [Test]
        public async Task ExpiredObservation_UnsupportedAlgorithms_AndCancellationNeverProduceATrustedSuccess()
        {

            Assert.That(MeterKeyVerification.VerifyOCMFSessionWithMeterKey(await Session(), binding, () => at.AddHours(25)).Status,
                        Is.EqualTo(MeterKeyVerificationStatus.EvidenceExpired));

            Assert.That(MeterKeyVerification.VerifyOCMFSessionWithMeterKey(await Session(), binding with { Key = binding.Key with { Algorithm = "Ed25519" } }, () => at).Status,
                        Is.EqualTo(MeterKeyVerificationStatus.Unsupported));

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            var session = await Session();

            Assert.That(() => MeterKeyVerification.VerifyOCMFSessionWithMeterKey(session, binding, () => at, CancellationToken: cancelled.Token),
                        Throws.InstanceOf<OperationCanceledException>());

        }

        #endregion

        #region Historical original-document handoff

        private static readonly String             archiveText  = ReadTextFixture(HistoricalFixture);

        private static readonly MeterKeyBinding[]  historical   = [ .. new[] { "2026-06-01T10:00:00Z", "2026-06-01T10:30:00Z" }.
                                                                       Select(time => binding with {
                                                                                          Evidence = binding.Evidence with {
                                                                                                         Association       = MeterKeyAssociation.HistoricalAttestation,
                                                                                                         MeasurementTimes  = [ Date(time) ]
                                                                                                     }
                                                                                      }) ];

        [Test]
        public async Task ChecksExpiredHistoricalDocumentsWithTodayAuthorizedBindings_AtExactOriginalReadingTimes()
        {

            var result = MeterKeyVerification.VerifyHistoricalOCMFSession(await Session(archiveText), historical, () => at);

            Assert.Multiple(() => {
                Assert.That(result.Status,          Is.EqualTo(HistoricalOCMFStatus.Verified));
                Assert.That(result.SignatureCount,  Is.EqualTo(2));
                Assert.That(result.Checks.Select(check => check.HistoricalAssociation),
                            Is.All.EqualTo(HistoricalAssociation.RNOAttestedAtReadingTimes));
            });

        }

        [Test]
        public async Task CoverageCannotBeSuppliedByAnUncheckedTimestampOrDuplicateCompetingBindings()
        {

            var session = await Session(archiveText);

            Assert.That(() => MeterKeyVerification.VerifyHistoricalOCMFSession(session, historical[..1], () => at),
                        Throws.ArgumentException.With.Message.Contains("exactly one"));

            Assert.That(() => MeterKeyVerification.VerifyHistoricalOCMFSession(session, [ .. historical, historical[0] ], () => at),
                        Throws.ArgumentException.With.Message.Contains("exactly one"));

        }

        /// <summary>
        /// Not in ChargyCore.TS, where the historical check selects bindings that
        /// cover every reading before it gets this far: a historical binding handed
        /// over directly covers the reading times it names, and no others.
        /// </summary>
        [Test]
        public async Task AHistoricalBindingHandedOverDirectlyCoversOnlyTheReadingTimesItNames()
        {

            var result = MeterKeyVerification.VerifyOCMFSessionWithMeterKey(await Session(archiveText), historical[0], () => at);

            Assert.Multiple(() => {
                Assert.That(result.Status,                 Is.EqualTo(MeterKeyVerificationStatus.IdentityConflict), result.Detail);
                Assert.That(result.HistoricalAssociation,  Is.EqualTo(HistoricalAssociation.NotProven));
            });

        }

        [Test]
        public async Task AnInvalidOriginalSignatureNeverProducesASuccessfulHistoricalAssociationResult()
        {

            var result = MeterKeyVerification.VerifyHistoricalOCMFSession(await Session(ReplaceFirst(archiveText, "1011", "1012")), historical, () => at);

            Assert.Multiple(() => {
                Assert.That(result.SignaturesValid,  Is.False);
                Assert.That(result.Checks.Select(check => check.HistoricalAssociation),
                            Has.Some.EqualTo(HistoricalAssociation.NotProven));
            });

        }

        #endregion

        #region Original session price context and exact gross rates

        private static readonly String priceText = ReadTextFixture(PriceFixture);

        private static MeterKeyBinding CoveringTheStart(OCMFPriceContext Context)

            => binding with {
                   Evidence = binding.Evidence with {
                                  Association       = MeterKeyAssociation.HistoricalAttestation,
                                  MeasurementTimes  = [ Context.ChargingStarted ]
                              }
               };

        [Test]
        public async Task DiscoversAUniqueOriginalStart_AndBindsItsUnchangedCompleteDocumentBytes()
        {

            var original  = await Session(priceText);
            var context   = OCMFSessionPriceContext.ReadOCMFSessionPriceContext(original);
            var begin     = ChargyLib.ParseJSON(priceText)["ocmfBegin"]!.Value<String>()!;

            Assert.Multiple(() => {
                Assert.That(context.StartDocumentSHA256,  Is.EqualTo(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(begin)))));
                Assert.That(context.ChargingStarted,      Is.EqualTo(Date("2026-06-01T10:00:00Z")));
                Assert.That(context.Signatures,           Is.EqualTo("not-checked"));
            });

            var result = OCMFSessionPriceContext.VerifyOCMFPriceStart(original, context, CoveringTheStart(context), () => at);

            Assert.Multiple(() => {
                Assert.That(result.Status,            Is.EqualTo(MeterKeyVerificationStatus.Verified), result.Detail);
                Assert.That(result.MeasurementCount,  Is.EqualTo(1));
            });

        }

        [Test]
        public async Task NeverTrustsCachedPayloadsOrDisplayedTimestampsWhenDiscoveringOriginalPriceContext()
        {

            var first    = true;
            var changed  = Rebuilt(await Session(priceText), value => {

                               if (!first)
                                   return value;

                               first = false;
                               value.Document.Payload["TT"] = "001;EUR;0;99;0;0";

                               return Copy(value, Timestamp: "2030-01-01T00:00:00Z");

                           });

            var context  = OCMFSessionPriceContext.ReadOCMFSessionPriceContext(changed);

            Assert.Multiple(() => {
                Assert.That(context.ChargingStarted,  Is.EqualTo(Date("2026-06-01T10:00:00Z")));
                Assert.That(context.TariffTexts,      Is.EqualTo(new[] { "001;EUR;0;35;0;0" }));
            });

        }

        [Test]
        public async Task CanVerifyTheFirstStartBeforeAnEndReadingExists()
        {

            var original  = Rebuilt(await Session(priceText), value => value.Transaction == "B" ? value : null);
            var context   = OCMFSessionPriceContext.ReadOCMFSessionPriceContext(original);

            Assert.That(OCMFSessionPriceContext.VerifyOCMFPriceStart(original, context, CoveringTheStart(context), () => at).Status,
                        Is.EqualTo(MeterKeyVerificationStatus.Verified));

        }

        [Test]
        public async Task ASecondCompetingStartNeverAcquiresAnImplicitPriceSubject()
        {

            var raw          = ChargyLib.ParseJSON(priceText);
            raw["ocmfEnd"]   = ReplaceFirst(raw["ocmfBegin"]!.Value<String>()!, "1000", "1001");

            var session      = await Session(raw.ToString(Formatting.None));

            Assert.That(() => OCMFSessionPriceContext.ReadOCMFSessionPriceContext(session),
                        Throws.ArgumentException);

        }

        [Test]
        public async Task ComparesExactRawCentsWithoutRounding_AndDistinguishesUnsupportedConditions()
        {

            var context = OCMFSessionPriceContext.ReadOCMFSessionPriceContext(await Session(priceText));

            Assert.Multiple(() => {
                Assert.That(OCMFSessionPriceContext.CompareOCMFPriceRates(context, new GrossPriceRates("EUR", EnergyPerKWh: "0.3500")).Status,                    Is.EqualTo(OCMFPriceRateComparisonStatus.RatesMatch));
                Assert.That(OCMFSessionPriceContext.CompareOCMFPriceRates(context, new GrossPriceRates("EUR", EnergyPerKWh: "0.350000000000000000001")).Status,   Is.EqualTo(OCMFPriceRateComparisonStatus.Conflict));
                Assert.That(OCMFSessionPriceContext.CompareOCMFPriceRates(context, new GrossPriceRates("USD", EnergyPerKWh: "0.35")).Status,                      Is.EqualTo(OCMFPriceRateComparisonStatus.Conflict));
                Assert.That(OCMFSessionPriceContext.CompareOCMFPriceRates(context with { TariffTexts = [ "001;EUR;0;35;5;10" ] },
                                                                          new GrossPriceRates("EUR", EnergyPerKWh: "0.35")).Status,                              Is.EqualTo(OCMFPriceRateComparisonStatus.Unsupported));
            });

        }

        #endregion


        #region (private) Session / DocumentsOf / Rebuilt / Copy / ReplaceFirst / Date

        /// <summary>
        /// Run a container through the whole Chargy pipeline, as ChargyCore.TS's
        /// "verifyChargeData" does, and take its one charging session.
        /// </summary>
        private static async Task<ChargingSession> Session(String? Input = null)
        {

            var result = await Verify([ new FileInfo(SessionFixture, Encoding.UTF8.GetBytes(Input ?? text), MIMETypeOf(SessionFixture)) ]);

            if (result is not ChargeTransparencyRecord record || record.ChargingSessions.Count == 0)
                throw new InvalidOperationException("Missing fixture session: " + result);

            return record.ChargingSessions[0];

        }

        private static OCMFDocument[] DocumentsOf(ChargingSession Session)

            => [ .. Session.Measurements.SelectMany(measurement => measurement.Values).OfType<OCMFMeasurementValue>().Select(value => value.Document).Distinct() ];

        /// <summary>
        /// The same charging session with its readings mapped, or left out where
        /// the map says null — what ChargyCore.TS does by changing a session in
        /// place, which the readings of this port do not allow.
        /// </summary>
        private static ChargingSession Rebuilt(ChargingSession                                     Session,
                                               Func<OCMFMeasurementValue, OCMFMeasurementValue?>  Map)
        {

            // Each reading is mapped exactly once, and one the map leaves as it is
            // is copied all the same: a measurement takes its readings over, and
            // the original session is to stay as it was.
            OCMFMeasurementValue? MapOnce(OCMFMeasurementValue Value)
                => Map(Value) is OCMFMeasurementValue mapped
                       ? ReferenceEquals(mapped, Value) ? Copy(Value) : mapped
                       : null;

            var measurements = Session.Measurements.Select(measurement => new Measurement(
                                                                              measurement.EnergyMeterId,
                                                                              measurement.Name,
                                                                              measurement.OBIS,
                                                                              measurement.Scale,
                                                                              [ .. measurement.Values.OfType<OCMFMeasurementValue>().Select(MapOnce).OfType<OCMFMeasurementValue>() ],
                                                                              measurement.Context,
                                                                              measurement.Unit,
                                                                              measurement.UnitEncoded,
                                                                              measurement.CurrentType,
                                                                              measurement.ValueType,
                                                                              measurement.VerifyChain,
                                                                              measurement.SignatureInfos,
                                                                              measurement.Phenomena
                                                                          )).
                                                    ToArray();

            return new ChargingSession(
                       Session.Id,
                       EVSEId:         Session.EVSEId,
                       EnergyMeterId:  Session.EnergyMeterId,
                       Measurements:   measurements
                   );

        }

        /// <summary>
        /// A copy of a reading, with its timestamp or value changed, carrying the
        /// same document and the same result.
        /// </summary>
        private static OCMFMeasurementValue Copy(OCMFMeasurementValue  Original,
                                                 String?               Timestamp  = null,
                                                 Decimal?              Value      = null)

            => new (Timestamp ?? Original.Timestamp,
                    Value     ?? Original.Value,
                    Original.Document,
                    Original.TimeSync,
                    Original.Transaction,
                    Original.TransactionType,
                    Original.Pagination,
                    Original.ErrorIndex,
                    Original.ErrorFlags,
                    Original.CumulatedLoss,
                    Original.StatusMeter) {
                   Result = Original.Result
               };

        /// <summary>
        /// Replace the first occurrence only, as JavaScript's String.replace does —
        /// C#'s replaces every one.
        /// </summary>
        private static String ReplaceFirst(String Text, String Old, String New)
        {
            var index = Text.IndexOf(Old, StringComparison.Ordinal);
            return index < 0 ? Text : String.Concat(Text.AsSpan(0, index), New, Text.AsSpan(index + Old.Length));
        }

        private static DateTimeOffset Date(JToken Value)

            => Date(Value.Value<String>()!);

        private static DateTimeOffset Date(String Value)

            => DateTimeOffset.Parse(Value, CultureInfo.InvariantCulture);

        #endregion

    }

}
