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

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.Crypto;
using cloud.charging.open.chargy.Formats.OCMF;

#endregion

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Tests for the signatures over a whole document and over the legally
    /// relevant log messages inside it, ported from upstream's
    /// DocumentSignatures.tests.ts and the live link tests of 0.14.0 and 0.16.1.
    ///
    /// The fixture is signed as a whole by two operator keys, ECDSA and Ed25519,
    /// and its power constraint by two keys of the grid operator. Every case
    /// below changes the document in memory; the fixture itself is shared with
    /// ChargyCore.TS byte for byte.
    /// </summary>
    [TestFixture]
    public class DocumentSignaturesTests : AChargyTests
    {

        #region Data

        private const String SignedLiveLink = "ChargeTransparencyLive/ChargeTransparencyLiveLink_1.json";

        #endregion


        #region EverySignatureOfASignedLiveLinkVerifies()

        /// <summary>
        /// Both operator keys sign the document: one ECDSA over a
        /// SubjectPublicKeyInfo key, one Ed25519 over a key stored raw. The second
        /// only resolves if the raw key is wrapped before its id is computed,
        /// which is where an implementation quietly goes wrong.
        ///
        /// It also proves that the canonicalization here produces the very bytes
        /// upstream signed: one byte of difference and neither would verify.
        /// </summary>
        [Test]
        public void EverySignatureOfASignedLiveLinkVerifies()
        {

            var result = DocumentSignatures.VerifyDocumentSignatures(Document());

            Assert.Multiple(() => {
                Assert.That(result.Status,                                         Is.EqualTo(DocumentSignaturesStatus.AllValid));
                Assert.That(result.ValidCount,                                     Is.EqualTo(2));
                Assert.That(result.Signatures.Select(signature => signature.Algorithm),
                            Is.EqualTo(new[] { "ECDSA-secp256r1-SHA256", "EdDSA-Ed25519" }));
            });

        }

        #endregion

        #region ThePublicKeysOfAllThreePartiesAreFound()

        [Test]
        public void ThePublicKeysOfAllThreePartiesAreFound()
        {

            var publicKeys = DocumentSignatures.CollectDocumentPublicKeys(Document());

            Assert.Multiple(() => {

                Assert.That(publicKeys,                                                         Has.Count.EqualTo(6));
                Assert.That(publicKeys.Select(publicKey => publicKey.Algorithm),                Has.Member("EdDSA-Ed25519"));
                Assert.That(publicKeys.Any(publicKey => publicKey.KeyUsage.Contains("signMeterValues")),  Is.True);

                // The grid operator signs its power constraints rather than the
                // document, so its keys verify a log message, not the document —
                // they belong in the same set all the same.
                Assert.That(publicKeys.Count(publicKey => publicKey.KeyUsage.Contains("signGridPowerConstraints")),  Is.EqualTo(2));

            });

        }

        #endregion

        #region ADocumentWithoutSignaturesIsUnsigned()

        [Test]
        public void ADocumentWithoutSignaturesIsUnsigned()
        {

            var unsigned = Document();
            unsigned.Remove("signatures");

            var empty    = Document();
            empty["signatures"] = new JArray();

            var notAList = Document();
            notAList["signatures"] = "nope";

            Assert.Multiple(() => {
                Assert.That(DocumentSignatures.VerifyDocumentSignatures(unsigned).Status,  Is.EqualTo(DocumentSignaturesStatus.Unsigned));
                Assert.That(DocumentSignatures.VerifyDocumentSignatures(empty).   Status,  Is.EqualTo(DocumentSignaturesStatus.Unsigned));
                Assert.That(DocumentSignatures.VerifyDocumentSignatures(notAList).Status,  Is.EqualTo(DocumentSignaturesStatus.Unsigned));
            });

        }

        #endregion

        #region AChangedDocumentIsNoticed()

        [Test]
        public void AChangedDocumentIsNoticed()
        {

            // Any covered property will do; the description is the one a reader
            // would actually be shown.
            var tampered = Document();
            tampered["description"] = new JObject(new JProperty("en", "Something else"));

            var result   = DocumentSignatures.VerifyDocumentSignatures(tampered);

            Assert.Multiple(() => {
                Assert.That(result.Status,                                       Is.EqualTo(DocumentSignaturesStatus.NoneValid));
                Assert.That(result.Signatures.Select(signature => signature.Status),
                            Is.All.EqualTo(DocumentSignatureStatus.InvalidSignature));
            });

        }

        #endregion

        #region WhatTheSignaturesDoNotCoverIsIgnored()

        [Test]
        public void WhatTheSignaturesDoNotCoverIsIgnored()
        {

            // "signatures" is excluded from the signed data, so reordering the
            // array changes no bytes — both entries still verify.
            var reordered = Document();
            reordered["signatures"] = new JArray(((JArray) reordered["signatures"]!).Reverse());

            Assert.That(DocumentSignatures.VerifyDocumentSignatures(reordered).Status,  Is.EqualTo(DocumentSignaturesStatus.AllValid));

        }

        #endregion

        #region SignaturesThatCannotBeJudgedSaySo()

        [Test]
        public void SignaturesThatCannotBeJudgedSaySo()
        {

            var unknownKey = Document();
            foreach (var signature in unknownKey["signatures"]!.OfType<JObject>())
                signature["keyId"] = String.Concat(Enumerable.Repeat("00", 32));

            var unknownAlgorithm = Document();
            foreach (var signature in unknownAlgorithm["signatures"]!.OfType<JObject>())
                signature["algorithm"] = "ECDSA-brainpoolP256r1-SHA256";

            var malformed = Document();
            malformed["signatures"] = new JArray(42, new JObject());

            Assert.Multiple(() => {

                Assert.That(DocumentSignatures.VerifyDocumentSignatures(unknownKey).Signatures.Select(signature => signature.Status),
                            Is.All.EqualTo(DocumentSignatureStatus.UnknownPublicKey));

                Assert.That(DocumentSignatures.VerifyDocumentSignatures(unknownAlgorithm).Signatures.Select(signature => signature.Status),
                            Is.All.EqualTo(DocumentSignatureStatus.UnsupportedAlgorithm));

                Assert.That(DocumentSignatures.VerifyDocumentSignatures(malformed).Signatures.Select(signature => signature.Status),
                            Is.All.EqualTo(DocumentSignatureStatus.Malformed));

            });

        }

        #endregion

        #region OneBrokenSignatureMixesTheOutcome()

        [Test]
        public void OneBrokenSignatureMixesTheOutcome()
        {

            var document = Document();
            document["signatures"]![0]!["value"] = String.Concat(Enumerable.Repeat("00", 64));

            var result   = DocumentSignatures.VerifyDocumentSignatures(document);

            Assert.Multiple(() => {
                Assert.That(result.Status,      Is.EqualTo(DocumentSignaturesStatus.SomeValid));
                Assert.That(result.ValidCount,  Is.EqualTo(1));
            });

        }

        #endregion


        #region ALogMessageVerifiesAgainstTheKeysOfItsDocument()

        /// <summary>
        /// The grid operator signs its power constraint with both of its keys.
        /// </summary>
        [Test]
        public void ALogMessageVerifiesAgainstTheKeysOfItsDocument()
        {

            var document = Document();
            var result   = DocumentSignatures.VerifyEmbeddedSignatures(LogMessageOf(document), document);

            Assert.Multiple(() => {
                Assert.That(result.Status,      Is.EqualTo(DocumentSignaturesStatus.AllValid));
                Assert.That(result.ValidCount,  Is.EqualTo(2));
            });

        }

        #endregion

        #region AChangedLogMessageIsNoticed()

        /// <summary>
        /// The case the message's own signature exists for: an operator who
        /// re-signs the document around a forged constraint produces a document
        /// that verifies as a whole, and only this signature still says the grid
        /// never asked for it.
        /// </summary>
        [Test]
        public void AChangedLogMessageIsNoticed()
        {

            var document   = Document();
            var logMessage = LogMessageOf(document);

            logMessage["data"] = new JObject(new JProperty("maxPower", "60 kW"));

            var result     = DocumentSignatures.VerifyEmbeddedSignatures(logMessage, document);

            Assert.Multiple(() => {
                Assert.That(result.Status,  Is.EqualTo(DocumentSignaturesStatus.NoneValid));
                Assert.That(result.Signatures.Select(signature => signature.Status),
                            Is.All.EqualTo(DocumentSignatureStatus.InvalidSignature));
            });

        }

        #endregion

        #region ALogMessageCannotBeJudgedWithoutTheKeysOfItsSender()

        /// <summary>
        /// The same message in a document that does not carry the grid operator:
        /// the signature is intact and cannot be judged, which is a weaker
        /// statement than "is wrong".
        /// </summary>
        [Test]
        public void ALogMessageCannotBeJudgedWithoutTheKeysOfItsSender()
        {

            var document   = Document();
            var logMessage = LogMessageOf(document);

            var unsigned   = (JObject) logMessage.DeepClone();
            unsigned.Remove("signatures");

            document.Remove("gridOperator");

            Assert.Multiple(() => {

                Assert.That(DocumentSignatures.VerifyEmbeddedSignatures(logMessage, document).Signatures.Select(signature => signature.Status),
                            Is.All.EqualTo(DocumentSignatureStatus.UnknownPublicKey));

                Assert.That(DocumentSignatures.VerifyEmbeddedSignatures(unsigned, document).Status,
                            Is.EqualTo(DocumentSignaturesStatus.Unsigned));

            });

        }

        #endregion


        #region ReadingALiveLinkVerifiesItsSignatures()

        /// <summary>
        /// The content format detector verifies a live link as it reads it, and a
        /// sound document carries no warnings.
        /// </summary>
        [Test]
        public async Task ReadingALiveLinkVerifiesItsSignatures()
        {

            var liveLink = await Read(Document());

            Assert.Multiple(() => {
                Assert.That(liveLink.SignatureVerification?.Status,  Is.EqualTo(DocumentSignaturesStatus.AllValid));
                Assert.That(liveLink.Warnings,                       Is.Empty);
            });

        }

        #endregion

        #region AChangedLiveLinkIsWarnedAboutOnceAndStillRead()

        /// <summary>
        /// A signature that does not match is a warning, never a reason to refuse
        /// the document: its transports still work, and its meter values carry
        /// signatures of their own. Two signatures failing the same way are one
        /// thing worth saying, not two.
        /// </summary>
        [Test]
        public async Task AChangedLiveLinkIsWarnedAboutOnceAndStillRead()
        {

            var tampered = Document();
            tampered["description"] = new JObject(new JProperty("en", "Something else"));

            var liveLink = await Read(tampered);

            Assert.Multiple(() => {
                Assert.That(liveLink.SignatureVerification?.Status,  Is.EqualTo(DocumentSignaturesStatus.NoneValid));
                Assert.That(liveLink.Warnings,                       Has.Count.EqualTo(1));
                Assert.That(Says(liveLink.Warnings[0], "DocumentSignature_Mismatch"),  Is.True);
                Assert.That(liveLink.Warnings[0].Level,              Is.EqualTo(SeverityLevel.High));
                Assert.That(liveLink.LiveTransports,                 Is.Not.Empty);
            });

        }

        #endregion

        #region AnUnsignedLiveLinkIsSaidToBeUnsigned()

        [Test]
        public async Task AnUnsignedLiveLinkIsSaidToBeUnsigned()
        {

            var unsigned = Document();
            unsigned.Remove("signatures");

            var liveLink = await Read(unsigned);

            Assert.Multiple(() => {
                Assert.That(liveLink.SignatureVerification?.Status,  Is.EqualTo(DocumentSignaturesStatus.Unsigned));
                Assert.That(liveLink.Warnings.Select(warning => Says(warning, "DocumentSignature_Missing")),  Is.EqualTo(new[] { true }));
                Assert.That(liveLink.Warnings[0].Level,              Is.EqualTo(SeverityLevel.Low));
            });

        }

        #endregion

        #region AChangedLogMessageIsWarnedAboutTwice()

        /// <summary>
        /// Changing a message breaks both signatures, the document's and the
        /// message's own, and both are said — they prove different things.
        /// </summary>
        [Test]
        public async Task AChangedLogMessageIsWarnedAboutTwice()
        {

            var document = Document();
            LogMessageOf(document)["data"] = new JObject(new JProperty("maxPower", "60 kW"));

            var liveLink = await Read(document);

            Assert.Multiple(() => {
                Assert.That(liveLink.Warnings.Any(warning => Says(warning, "DocumentSignature_Mismatch")),    Is.True);
                Assert.That(liveLink.Warnings.Any(warning => Says(warning, "LogMessageSignature_Mismatch")),  Is.True);
            });

        }

        #endregion


        #region ACOSEKeySignsADocumentLikeAnyOtherKey(SuiteName, Algorithm)

        /// <summary>
        /// The key id is the hash of the SubjectPublicKeyInfo whatever form the
        /// key is stored in, so a COSE key on a registered curve has one, and can
        /// sign a live link.
        /// </summary>
        [TestCase("ECDSA-P256", "ECDSA-secp256r1-SHA256", "ECDSA-secp256r1")]
        [TestCase("Ed25519",    "EdDSA-Ed25519",          "EdDSA-Ed25519")]
        public void ACOSEKeySignsADocumentLikeAnyOtherKey(String  SuiteName,
                                                          String  Algorithm,
                                                          String  KeyAlgorithm)
        {

            var suite      = SignatureSuites.Get(SuiteName);
            var keyPair    = suite.GenerateKeyPair();
            var publicKey  = keyPair.PublicKey;

            var coseKey    = SuiteName == "Ed25519"
                                 ? new COSEKey(COSEKeyType.OKP, COSECurve.Ed25519, publicKey)
                                 : new COSEKey(COSEKeyType.EC2, COSECurve.P256,    publicKey[1..33], publicKey[33..]);

            var spki       = Convert.FromHexString(SuiteName == "Ed25519"
                                                       ? "302A300506032B6570032100"
                                                       : "3059301306072A8648CE3D020106082A8648CE3D030107034200").
                                     Concat(publicKey).ToArray();

            var document   = new JObject(
                                 new JProperty("@context",                 ChargeTransparencyLiveLink.JSONLDContext),
                                 new JProperty("keyIdGeneration",          new JArray("SubjectPublicKeyInfo", "DER", "SHA-256", "hex")),
                                 new JProperty("chargingStationOperator",  new JObject(
                                     new JProperty("publicKeys", new JArray(new JObject(
                                         new JProperty("keyUsage",   new JArray("signCTRs")),
                                         new JProperty("algorithm",  KeyAlgorithm),
                                         new JProperty("encodings",  new JArray("COSE_Key", "CBOR", "base64url")),
                                         new JProperty("value",      Base64Url.EncodeToString(coseKey.ToByteArray()))
                                     )))
                                 ))
                             );

            var signature  = suite.Sign(CanonicalJSON.ToUTF8Bytes(document), keyPair.PrivateKey);

            document.Add(new JProperty("signatures", new JArray(new JObject(
                new JProperty("keyId",       Convert.ToHexString(SHA256.HashData(spki))),
                new JProperty("algorithm",   Algorithm),
                new JProperty("signedData",  new JObject(
                                                 new JProperty("excludedProperties",  new JArray("signatures")),
                                                 new JProperty("encodings",           new JArray("JSON", "JCS", "UTF-8"))
                                             )),
                new JProperty("encodings",   SuiteName == "Ed25519" ? new JArray("raw", "hex") : new JArray("Ecdsa-Sig-Value", "DER", "hex")),
                new JProperty("value",       Convert.ToHexString(signature))
            ))));

            Assert.That(DocumentSignatures.VerifyDocumentSignatures(document).Status,  Is.EqualTo(DocumentSignaturesStatus.AllValid));

        }

        #endregion


        #region TheTariffTextRecordsTheTariffChanges()

        /// <summary>
        /// A tariff text names one tariff. This session has three tariff periods,
        /// so "TT" carries the tariffs that have metered something so far, in
        /// order and separated by a vertical bar — the extension upstream a02c7a0
        /// documents. The base price appears twice because the session returns to
        /// it: these are periods, not distinct tariffs.
        /// </summary>
        [Test]
        public void TheTariffTextRecordsTheTariffChanges()
        {

            const String basePrice   = "001;EUR;0;35;0;0";
            const String constrained = "001;EUR;0;25;0;0";

            var tariffTexts = Documents().Select(TariffTextOf).ToArray();

            Assert.Multiple(() => {

                Assert.That(tariffTexts.Distinct(),
                            Is.EqualTo(new[] { basePrice,
                                               basePrice + "|" + constrained,
                                               basePrice + "|" + constrained + "|" + basePrice }));

                // Every entry of a history is a Bonn tariff of its own.
                Assert.That(tariffTexts.SelectMany(OCMFBonnTariff.ParseList).Select(tariff => tariff.EnergyFeeCentsPerKWh).Distinct().Order(),
                            Is.EqualTo(new Decimal?[] { 25, 35 }));

            });

        }

        #endregion

        #region TheReadingsAtATariffChangeSaySo()

        /// <summary>
        /// OCMF has a reading reason for a tariff change, "T", and the meter
        /// reads at both ends of the power limit. A "T" reading closes the
        /// interval under the previous tariff, so its history is one entry
        /// shorter than that of the document after it.
        /// </summary>
        [Test]
        public void TheReadingsAtATariffChangeSaySo()
        {

            var documents     = Documents();
            var tariffChanges = documents.Select((document, index) => (document, index)).
                                          Where (entry => ReasonsOf(entry.document).Contains("T")).
                                          ToArray();

            Assert.Multiple(() => {

                Assert.That(tariffChanges,  Has.Length.EqualTo(2));

                foreach (var (document, index) in tariffChanges)
                    Assert.That(OCMFBonnTariff.ParseList(TariffTextOf(documents[index + 1])),
                                Has.Count.EqualTo(OCMFBonnTariff.ParseList(TariffTextOf(document)).Count + 1));

                Assert.That(ReasonsOf(documents[0]),   Is.EqualTo(new[] { "B" }));
                Assert.That(ReasonsOf(documents[^1]),  Is.EqualTo(new[] { "B", "E" }));

            });

        }

        #endregion


        #region (private, static) Helpers

        private static JObject Document()

            => ChargyLib.ParseJSON(ReadTextFixture(SignedLiveLink));

        private static JObject LogMessageOf(JObject Document)

            => (JObject) Document["legallyRelevantLogMessages"]![0]!;

        private static String[] Documents()

            => [ .. Document()["signedMeterValues"]!["values"]!.Select(value => value.Value<String>()!) ];

        /// <summary>
        /// The payload of an OCMF document lies between the first and the last
        /// bar: a tariff text may carry bars of its own.
        /// </summary>
        private static JObject PayloadOf(String OCMF)

            => JObject.Parse(OCMF[(OCMF.IndexOf('|') + 1)..OCMF.LastIndexOf('|')]);

        private static String TariffTextOf(String OCMF)

            => PayloadOf(OCMF)["TT"]!.Value<String>()!;

        private static String[] ReasonsOf(String OCMF)

            => [ .. PayloadOf(OCMF)["RD"]!.Select(reading => reading["TX"]!.Value<String>()!) ];

        private static async Task<ChargeTransparencyLiveLink> Read(JObject Document)
        {

            var result = await Verify([
                             new FileInfo(
                                 "liveLink.json",
                                 Encoding.UTF8.GetBytes(Document.ToString(Newtonsoft.Json.Formatting.None)),
                                 "application/json"
                             )
                         ]);

            Assert.That(result,  Is.InstanceOf<ChargeTransparencyLiveLink>(), VerificationReport.Format(result));

            return (ChargeTransparencyLiveLink) result;

        }

        private static Boolean Says(Warning  Warning,
                                    String   MessageKey)

            => Warning.Message[Languages.en] == I18NDictionary.Default().GetMultilanguageText(MessageKey)[Languages.en];

        #endregion

    }

}
