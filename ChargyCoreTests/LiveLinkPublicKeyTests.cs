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

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.IO;
using cloud.charging.open.chargy.Crypto;
using cloud.charging.open.chargy.tests.Formats;

#endregion

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Tests for the public keys of a charge transparency live link that are not
    /// a hexadecimal SubjectPublicKeyInfo: COSE keys, base64, bare points, and
    /// encodings nobody reads.
    ///
    /// The fixtures are shared with ChargyCore.TS byte for byte, so no new file
    /// is added for any of this. The live link fixture is read and changed in
    /// memory, and where a curve has no fixture at all, its meter values are
    /// signed here.
    /// </summary>
    [TestFixture]
    public class LiveLinkPublicKeyTests : AChargyTests
    {

        #region Data

        /// <summary>
        /// The last document of the OCMF-Test-01 series: the meter signs the start
        /// and the end value, the operator the readings in between, all on
        /// secp256r1.
        /// </summary>
        private const String CompletedSession = "ChargeTransparencyLive/ChargeTransparencyLiveLink_1.json";

        #endregion


        #region AP256KeyAsACOSEKeyVerifiesWhatItsSubjectPublicKeyInfoTwinVerifies()

        /// <summary>
        /// P-256 is registered, so a COSE key on it says everything a
        /// SubjectPublicKeyInfo says, and the same readings verify with it.
        /// </summary>
        [Test]
        public void AP256KeyAsACOSEKeyVerifiesWhatItsSubjectPublicKeyInfoTwinVerifies()
        {

            var asSubjectPublicKeyInfo  = MeterValues(LiveLink());

            var liveLink                = LiveLink();

            foreach (var entry in MeterValueKeys(liveLink))
            {
                entry["value"]      = Base64Url.EncodeToString(COSEKey.From(ECKey(entry["value"]!.Value<String>()!)).ToByteArray());
                entry["encodings"]  = new JArray("COSE_Key", "CBOR", "base64url");
            }

            var asCOSEKey               = MeterValues(liveLink);

            Assert.Multiple(() => {

                Assert.That(StatusesOf(asSubjectPublicKeyInfo),  Is.All.EqualTo(VerificationResult.ValidSignature));
                Assert.That(StatusesOf(asCOSEKey),               Is.EqualTo(StatusesOf(asSubjectPublicKeyInfo)), VerificationReport.Format(asCOSEKey));

                Assert.That(asCOSEKey.ChargingSessions[0].VerificationResult?.Status,  Is.EqualTo(SessionVerificationResult.ValidSignature));
                Assert.That(asCOSEKey.Errors,                                          Is.Empty);

            });

        }

        #endregion

        #region RegisteredCurvesVerifyAsCOSEKeys(CurveName, SA)

        /// <summary>
        /// secp256k1 and the brainpool curves are in the registry as well, so a
        /// meter on one of them can publish its key as a COSE key.
        /// </summary>
        [TestCase("secp256k1",        "ECDSA-secp256k1-SHA256")]
        [TestCase("brainpoolP256r1",  "ECDSA-brainpool256r1-SHA256")]
        [TestCase("brainpoolP384r1",  "ECDSA-brainpool384r1-SHA384")]
        public void RegisteredCurvesVerifyAsCOSEKeys(String  CurveName,
                                                     String  SA)
        {

            var keyPair   = OCMFTestSigner.NewKeyPair(CurveName);
            var coseKey   = COSEKey.From((ECPublicKeyParameters) keyPair.Public).ToByteArray();

            var record    = MeterValues(
                                SignedWith(
                                    keyPair,
                                    SA,
                                    MeterKey(
                                        $"ECDSA-{CurveName}",
                                        [ "COSE_Key", "CBOR", "base64url" ],
                                        Base64Url.EncodeToString(coseKey)
                                    )
                                )
                            );

            Assert.Multiple(() => {
                Assert.That(StatusesOf(record),                                     Is.All.EqualTo(VerificationResult.ValidSignature), VerificationReport.Format(record));
                // Every reading of the session, each signed anew.
                Assert.That(StatusesOf(record),                                     Has.Length.EqualTo(StatusesOf(MeterValues(LiveLink())).Length));
                Assert.That(record.ChargingSessions[0].VerificationResult?.Status,  Is.EqualTo(SessionVerificationResult.ValidSignature));
            });

        }

        #endregion

        #region ACurveACOSEKeyCannotNameIsSaidSo(Curve)

        /// <summary>
        /// A COSE key that names no curve, an unregistered one, or one from the
        /// private-use range cannot be read by anybody but its author. The
        /// readings only that key could have verified say exactly that — not that
        /// their signature does not match, which is what the operator's key alone
        /// would otherwise have concluded about the meter's three readings.
        /// </summary>
        [TestCase(-65537)]
        [TestCase(9)]
        [TestCase(null)]
        public void ACurveACOSEKeyCannotNameIsSaidSo(Int32? Curve)
        {

            var liveLink  = LiveLink();
            var meterKey  = ECKey(MeterKeyOf(liveLink)["value"]!.Value<String>()!).Q.Normalize();

            var coseKey   = new COSEKey(
                                COSEKeyType.EC2,
                                Curve.HasValue ? new COSECurve(Curve.Value) : null,
                                meterKey.AffineXCoord.GetEncoded(),
                                meterKey.AffineYCoord.GetEncoded()
                            ).ToByteArray();

            MeterKeyOf(liveLink)["value"]      = Base64Url.EncodeToString(coseKey);
            MeterKeyOf(liveLink)["encodings"]  = new JArray("COSE_Key", "CBOR", "base64url");

            var record    = MeterValues(liveLink);
            var results   = ResultsOf(record);

            var (total, byOperator) = ReadingsSignedByTheOperator();
            Assert.That(total - byOperator,  Is.GreaterThan(0));

            Assert.Multiple(() => {

                // The operator's readings are untouched by the meter's key.
                Assert.That(results.Count(result => result.Status == VerificationResult.ValidSignature),          Is.EqualTo(byOperator));

                Assert.That(results.Count(result => result.Status == VerificationResult.UnknownPublicKeyFormat),  Is.EqualTo(total - byOperator),
                            VerificationReport.Format(record));

                Assert.That(results.Where (result => result.Status == VerificationResult.UnknownPublicKeyFormat).
                                    Select(result => result.Errors.Select(error => error.Code)),
                            Is.All.Contains(LiveLinkPublicKey.COSECurveNotExpressible));

                Assert.That(results.Select(result => result.Status),  Has.None.EqualTo(VerificationResult.InvalidSignature));

                Assert.That(record.Errors.Select(error => error.Code),  Is.EqualTo(new[] { LiveLinkPublicKey.COSECurveNotExpressible }));

                Assert.That(record.ChargingSessions[0].VerificationResult?.Status,  Is.EqualTo(SessionVerificationResult.UnknownPublicKeyFormat));

            });

        }

        #endregion

        #region TheGapIsInTheRegistry(CurveName)

        /// <summary>
        /// Why the curves of the EMH, GDF, Alfen and legacy ChargePoint meters
        /// can never be COSE keys: the IANA "COSE Elliptic Curves" registry has
        /// no identifier for them, so no implementation can write one.
        /// </summary>
        [TestCase("secp192r1")]
        [TestCase("secp192k1")]
        [TestCase("secp224k1")]
        public void TheGapIsInTheRegistry(String CurveName)
        {

            var publicKey = (ECPublicKeyParameters) OCMFTestSigner.NewKeyPair(CurveName).Public;

            Assert.That(() => COSEKey.From(publicKey),  Throws.InstanceOf<COSEException>());

        }

        #endregion

        #region ASecp192r1MeterKeyStillVerifies(Encodings)

        /// <summary>
        /// The way those curves do travel: a SubjectPublicKeyInfo, which names the
        /// curve by its object identifier, or the bare point next to an algorithm
        /// that names the curve.
        /// </summary>
        [TestCase("SubjectPublicKeyInfo")]
        [TestCase("ECPoint")]
        public void ASecp192r1MeterKeyStillVerifies(String Structure)
        {

            var keyPair    = OCMFTestSigner.NewKeyPair("secp192r1");

            var meterKey   = Structure == "ECPoint"
                                 ? MeterKey("ECDSA-secp192r1", [ "ECPoint", "hex" ],
                                            Convert.ToHexString(((ECPublicKeyParameters) keyPair.Public).Q.GetEncoded(false)))
                                 : MeterKey("ECDSA-secp192r1", [ "SubjectPublicKeyInfo", "DER", "hex" ],
                                            OCMFTestSigner.PublicKeyHEX(keyPair));

            var record     = MeterValues(SignedWith(keyPair, "ECDSA-secp192r1-SHA256", meterKey));

            Assert.Multiple(() => {
                Assert.That(StatusesOf(record),                                     Is.All.EqualTo(VerificationResult.ValidSignature), VerificationReport.Format(record));
                // Every reading of the session, each signed anew.
                Assert.That(StatusesOf(record),                                     Has.Length.EqualTo(StatusesOf(MeterValues(LiveLink())).Length));
                Assert.That(record.ChargingSessions[0].VerificationResult?.Status,  Is.EqualTo(SessionVerificationResult.ValidSignature));
            });

        }

        #endregion

        #region ASubjectPublicKeyInfoMayTravelInAnotherText(Encodings)

        /// <summary>
        /// hex is a convention of the fixtures, not a rule of the format: the
        /// same SubjectPublicKeyInfo in base64, base64url or PEM is the same key.
        /// </summary>
        [TestCase("base64")]
        [TestCase("base64url")]
        [TestCase("PEM")]
        public void ASubjectPublicKeyInfoMayTravelInAnotherText(String Text)
        {

            var liveLink = LiveLink();

            foreach (var entry in MeterValueKeys(liveLink))
            {

                var der = Convert.FromHexString(entry["value"]!.Value<String>()!);

                entry["value"]      = Text switch {
                                          "base64url"  => Base64Url.EncodeToString(der),
                                          "PEM"        => $"-----BEGIN PUBLIC KEY-----\n{Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks)}\n-----END PUBLIC KEY-----\n",
                                          _            => Convert.ToBase64String(der)
                                      };

                entry["encodings"]  = Text == "PEM"
                                          ? new JArray("SubjectPublicKeyInfo", "DER", "base64", "PEM")
                                          : new JArray("SubjectPublicKeyInfo", "DER", Text);

            }

            var record = MeterValues(liveLink);

            Assert.That(StatusesOf(record),  Is.All.EqualTo(VerificationResult.ValidSignature), VerificationReport.Format(record));

        }

        #endregion

        #region AKeyInAnEncodingNobodyReadsIsReportedNotDropped()

        /// <summary>
        /// A key in an encoding this library does not read used to be left out
        /// without a word, and the meter's readings then failed against the
        /// operator's key as if their signatures did not match. They are now
        /// reported as what they are: readings whose key could not be read.
        /// </summary>
        [Test]
        public void AKeyInAnEncodingNobodyReadsIsReportedNotDropped()
        {

            var liveLink = LiveLink();

            MeterKeyOf(liveLink)["encodings"] = new JArray("SubjectPublicKeyInfo", "DER", "base85");

            var record   = MeterValues(liveLink);
            var results  = ResultsOf(record);

            var (total, byOperator) = ReadingsSignedByTheOperator();
            Assert.That(total - byOperator,  Is.GreaterThan(0));

            Assert.Multiple(() => {

                Assert.That(results.Count(result => result.Status == VerificationResult.ValidSignature),          Is.EqualTo(byOperator));
                Assert.That(results.Count(result => result.Status == VerificationResult.UnknownPublicKeyFormat),  Is.EqualTo(total - byOperator),
                            VerificationReport.Format(record));

                Assert.That(results.Select(result => result.Status),
                            Has.None.EqualTo(VerificationResult.InvalidSignature).And.None.EqualTo(VerificationResult.PublicKeyNotFound));

                Assert.That(record.Errors.Select(error => error.Code),  Is.EqualTo(new[] { LiveLinkPublicKey.UnsupportedEncoding }));
                Assert.That(record.Errors[0].Details,                   Does.Contain("base85"));

            });

        }

        #endregion

        #region AnUnreadableKeyIsReportedEvenWhereItDidNotMatter()

        /// <summary>
        /// A key nobody can read is worth saying even when every reading verified
        /// without it: the document lists it, and whoever wrote the document
        /// should hear that it is unusable before a reading depends on it.
        /// </summary>
        [Test]
        public void AnUnreadableKeyIsReportedEvenWhereItDidNotMatter()
        {

            var liveLink = LiveLink();

            ((JArray) MeterKeysOf(liveLink)).Add(
                new JObject(
                    new JProperty("keyUsage",   new JArray("signMeterValues")),
                    new JProperty("algorithm",  "ECDSA-secp256r1"),
                    new JProperty("encodings",  new JArray("JWK")),
                    new JProperty("value",      "{\"kty\":\"EC\"}")
                )
            );

            var record = MeterValues(liveLink);

            Assert.Multiple(() => {
                Assert.That(StatusesOf(record),                         Is.All.EqualTo(VerificationResult.ValidSignature));
                Assert.That(record.Errors.Select(error => error.Code),  Is.EqualTo(new[] { LiveLinkPublicKey.UnsupportedEncoding }));
            });

        }

        #endregion


        #region EveryRegisteredCOSECurveBecomesTheSubjectPublicKeyInfoOfThatCurve()

        /// <summary>
        /// The rule behind the COSE key support, stated over the whole registry
        /// rather than for the curves a test happened to pick: every registered
        /// elliptic curve becomes a SubjectPublicKeyInfo naming that very curve
        /// by its object identifier, holding that very point.
        /// </summary>
        [Test]
        public void EveryRegisteredCOSECurveBecomesTheSubjectPublicKeyInfoOfThatCurve()
        {

            var curves = COSECurve.All.Where(curve => curve.KeyType == COSEKeyType.EC2).ToArray();

            Assert.That(curves, Has.Length.GreaterThanOrEqualTo(8));

            Assert.Multiple(() => {

                foreach (var curve in curves)
                {

                    var generator  = new ECKeyPairGenerator();
                    generator.Init(new ECKeyGenerationParameters(curve.DomainParameters!, new SecureRandom()));

                    var publicKey  = (ECPublicKeyParameters) generator.GenerateKeyPair().Public;

                    var read       = LiveLinkPublicKey.Read(
                                         Base64Url.EncodeToString(COSEKey.From(publicKey).ToByteArray()),
                                         [ "COSE_Key", "CBOR", "base64url" ]
                                     );

                    Assert.That(read.IsUsable,  Is.True, $"{curve.Name}: {read}");

                    var parsed     = PublicKeyParser.TryParseDER(Convert.FromHexString(read.KeyHEX ?? ""));

                    Assert.That(parsed?.Value,         Is.EqualTo(publicKey.Q.GetEncoded(false)),  curve.Name);
                    Assert.That(ECNamedCurveTable.GetByName(parsed?.CurveName ?? "")?.Curve,
                                Is.EqualTo(curve.DomainParameters!.Curve),                          curve.Name);

                }

            });

        }

        #endregion

        #region AnEdDSACOSEKeyBecomesItsRawKey()

        /// <summary>
        /// An EdDSA key is passed on as its raw octets, which is how the
        /// verifiers take it and how [ "raw", "hex" ] already carries it.
        /// </summary>
        [Test]
        public void AnEdDSACOSEKeyBecomesItsRawKey()
        {

            var generator  = new Ed25519KeyPairGenerator();
            generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));

            var publicKey  = (Ed25519PublicKeyParameters) generator.GenerateKeyPair().Public;

            var read       = LiveLinkPublicKey.Read(
                                 Base64Url.EncodeToString(COSEKey.From(publicKey).ToByteArray()),
                                 [ "COSE_Key", "CBOR", "base64url" ]
                             );

            Assert.That(read.KeyHEX,  Is.EqualTo(Convert.ToHexString(publicKey.GetEncoded())));

        }

        #endregion

        #region AnEdDSASubjectPublicKeyInfoBecomesItsRawKey()

        /// <summary>
        /// The same key written as a SubjectPublicKeyInfo is unwrapped: handed on
        /// as it is, the EdDSA verifier would see 44 bytes where it expects 32,
        /// and say the signature does not match.
        /// </summary>
        [Test]
        public void AnEdDSASubjectPublicKeyInfoBecomesItsRawKey()
        {

            var generator  = new Ed25519KeyPairGenerator();
            generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));

            var publicKey  = (Ed25519PublicKeyParameters) generator.GenerateKeyPair().Public;

            var read       = LiveLinkPublicKey.Read(
                                 Convert.ToHexString(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey).GetDerEncoded()),
                                 [ "SubjectPublicKeyInfo", "DER", "hex" ]
                             );

            Assert.That(read.KeyHEX,  Is.EqualTo(Convert.ToHexString(publicKey.GetEncoded())));

        }

        #endregion

        #region AHexadecimalKeyIsPassedOnUntouched()

        /// <summary>
        /// What the fixtures carry has to reach the verifiers byte for byte as
        /// before, letter case included, because the key ends up in the reports.
        /// </summary>
        [Test]
        public void AHexadecimalKeyIsPassedOnUntouched()
        {

            var value = MeterKeyOf(LiveLink())["value"]!.Value<String>()!;

            Assert.Multiple(() => {
                Assert.That(LiveLinkPublicKey.Read(value, [ "SubjectPublicKeyInfo", "DER", "hex" ]).KeyHEX,  Is.EqualTo(value));
                Assert.That(LiveLinkPublicKey.Read(value, null).KeyHEX,                                      Is.EqualTo(value));
                Assert.That(LiveLinkPublicKey.Read(value.ToLowerInvariant(), [ "raw", "hex" ]).KeyHEX,       Is.EqualTo(value.ToLowerInvariant()));
            });

        }

        #endregion

        #region AValueThatDoesNotDecodeIsAnInvalidKey()

        /// <summary>
        /// A value that is not what its pipeline says is a broken key, which is a
        /// different statement from a pipeline nobody reads.
        /// </summary>
        [Test]
        public void AValueThatDoesNotDecodeIsAnInvalidKey()
        {

            var notHex   = LiveLinkPublicKey.Read("not hexadecimal",  [ "SubjectPublicKeyInfo", "DER", "hex" ]);
            var notCOSE  = LiveLinkPublicKey.Read("AAEC",             [ "COSE_Key", "CBOR", "base64url" ]);

            Assert.Multiple(() => {

                Assert.That(notHex.Status,      Is.EqualTo(VerificationResult.InvalidPublicKey));
                Assert.That(notHex.ReasonKey,   Is.EqualTo(LiveLinkPublicKey.DecodingFailed));

                Assert.That(notCOSE.Status,     Is.EqualTo(VerificationResult.InvalidPublicKey));
                Assert.That(notCOSE.ReasonKey,  Is.EqualTo(LiveLinkPublicKey.DecodingFailed));

            });

        }

        #endregion


        #region (private, static) LiveLink()

        /// <summary>
        /// The completed session, read afresh, so that each test can change it.
        /// </summary>
        private static JObject LiveLink()

            => ChargyLib.ParseJSON(ReadTextFixture(CompletedSession));

        #endregion

        #region (private, static) ReadingsSignedByTheOperator()

        /// <summary>
        /// How many readings the session has, and how many the operator's key
        /// verifies on its own — the rest are the meter's. Counted rather than
        /// named, because the series is regenerated whenever its generator learns
        /// something new.
        /// </summary>
        private static (Int32 Total, Int32 ByOperator) ReadingsSignedByTheOperator()
        {

            var liveLink = LiveLink();

            liveLink["chargingStation"]!["EVSE"]!["energyMeter"]!["publicKeys"] = new JArray();

            var results  = ResultsOf(MeterValues(liveLink));

            return (results.Length, results.Count(result => result.Status == VerificationResult.ValidSignature));

        }

        #endregion

        #region (private, static) MeterKeysOf / MeterKeyOf / MeterValueKeys(LiveLink)

        private static JToken MeterKeysOf(JObject LiveLink)

            => LiveLink["chargingStation"]!["EVSE"]!["energyMeter"]!["publicKeys"]!;

        private static JObject MeterKeyOf(JObject LiveLink)

            => (JObject) MeterKeysOf(LiveLink)[0]!;

        /// <summary>
        /// Every key of the document that signs meter values: the meter's, and
        /// the operator's one for the readings in between.
        /// </summary>
        private static IEnumerable<JObject> MeterValueKeys(JObject LiveLink)

            => LiveLink["chargingStationOperator"]!["publicKeys"]!.
                   OfType<JObject>().
                   Where (key => key["keyUsage"]!.Any(usage => usage.Value<String>() == "signEnergyMeterValues")).
                   Append(MeterKeyOf(LiveLink)).
                   ToArray();

        #endregion

        #region (private, static) MeterKey(Algorithm, Encodings, Value)

        private static JObject MeterKey(String    Algorithm,
                                        String[]  Encodings,
                                        String    Value)

            => new (
                   new JProperty("keyUsage",   new JArray("signMeterValues")),
                   new JProperty("algorithm",  Algorithm),
                   new JProperty("encodings",  new JArray(Encodings)),
                   new JProperty("value",      Value)
               );

        #endregion

        #region (private, static) SignedWith(KeyPair, SA, MeterKey)

        /// <summary>
        /// The completed session with every reading signed anew by the given key
        /// on the given algorithm, and that key as the only one for meter values.
        ///
        /// The payloads stay as they are: they say nothing about the key or the
        /// curve, so a valid OCMF session on any curve is one signature away.
        /// </summary>
        private static JObject SignedWith(AsymmetricCipherKeyPair  KeyPair,
                                          String                   SA,
                                          JObject                  MeterKey)
        {

            var liveLink  = LiveLink();
            var hashName  = SA[(SA.LastIndexOf('-') + 1)..];

            var values    = liveLink["signedMeterValues"]!["values"]!.
                                Select(value => value.Value<String>()!).
                                Select(document => {

                                    // Between the first and the last bar: a tariff text may
                                    // carry bars of its own.
                                    var payload = document[(document.IndexOf('|') + 1)..document.LastIndexOf('|')];

                                    return $"OCMF|{payload}|{{\"SA\":\"{SA}\",\"SE\":\"hex\",\"SM\":\"application/x-der\",\"SD\":\"{OCMFTestSigner.Sign(KeyPair, payload, hashName)}\"}}";

                                });

            liveLink["signedMeterValues"]!["values"]                       = new JArray(values);
            liveLink["chargingStationOperator"]!["publicKeys"]             = new JArray();
            liveLink["chargingStation"]!["EVSE"]!["energyMeter"]!["publicKeys"] = new JArray(MeterKey);

            return liveLink;

        }

        #endregion

        #region (private, static) MeterValues(LiveLink)

        private static ChargeTransparencyRecord MeterValues(JObject LiveLink)
        {

            Assert.That(ChargeTransparencyLiveLink.TryParse(LiveLink, out var liveLink),  Is.True);

            var record = new ContentFormatDetector(
                             I18NDictionary.Default(),
                             ChargeTransparencyFormats.All(I18NDictionary.Default())
                         ).TryToParseLiveLinkMeterValues(liveLink!);

            Assert.That(record,  Is.Not.Null);

            return record!;

        }

        #endregion

        #region (private, static) ResultsOf / StatusesOf(Record)

        private static CryptoResult[] ResultsOf(ChargeTransparencyRecord Record)

            => [ .. Record.ChargingSessions.
                        SelectMany(session     => session.Measurements).
                        SelectMany(measurement => measurement.Values).
                        Select    (value       => value.Result!) ];

        private static VerificationResult[] StatusesOf(ChargeTransparencyRecord Record)

            => [ .. ResultsOf(Record).Select(result => result.Status) ];

        #endregion

        #region (private, static) ECKey(SubjectPublicKeyInfoHEX)

        private static ECPublicKeyParameters ECKey(String SubjectPublicKeyInfoHEX)

            => (ECPublicKeyParameters) PublicKeyFactory.CreateKey(Convert.FromHexString(SubjectPublicKeyInfoHEX));

        #endregion

    }

}
