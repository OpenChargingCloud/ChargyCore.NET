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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NUnit.Framework;

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.Crypto;
using cloud.charging.open.chargy.LiveLink;

#endregion

namespace cloud.charging.open.chargy.tests.LiveLink
{

    /// <summary>
    /// The port of "LiveCPOAuthority.tests.ts" of ChargyCore.TS: a live session
    /// document is the operator's own only by a key the caller obtained
    /// independently, and a key change needs a document signed with the old key
    /// and the new one.
    ///
    /// The keys are Ed25519 keys whose 32 private bytes all equal one seed, as in
    /// ChargyCore.TS; the one of seed 161 is the operator key the "POILive"
    /// documents are signed with.
    /// </summary>
    [TestFixture]
    public class LiveCPOAuthorityTests : AChargyTests
    {

        #region Data

        private sealed record TestKey(Ed25519PrivateKeyParameters  PrivateKey,
                                      DocumentPublicKey            PublicKey,
                                      String                       Id);

        private static TestKey Key(Byte Seed)
        {

            var privateKey  = new Ed25519PrivateKeyParameters(Enumerable.Repeat(Seed, 32).ToArray(), 0);
            var spki        = Convert.FromHexString("302A300506032B6570032100").Concat(privateKey.GeneratePublicKey().GetEncoded()).ToArray();

            return new TestKey(
                       privateKey,
                       new DocumentPublicKey("EdDSA-Ed25519", [ "SubjectPublicKeyInfo", "DER", "hex" ], Convert.ToHexString(spki), [ "signCTRs" ]),
                       Convert.ToHexString(SHA256.HashData(spki))
                   );

        }

        private static readonly TestKey           old        = Key(161);
        private static readonly TestKey           next       = Key(162);

        private static readonly LiveCPOAuthority  authority  = new (
                                                                   EVSEId:             "DE*POI*E1*1",
                                                                   ChargingStationId:  "DE*POI*S1",
                                                                   OperatorId:         "DE*POI",
                                                                   Keys:               [ old.PublicKey ]
                                                               );

        private static DateTimeOffset Now
            => LiveSessionContinuityTests.Now;

        private static String Fixture(String Name)
            => LiveSessionContinuityTests.File(Name);

        #endregion


        [Test]
        public void VerifiesOriginalFixtureSignaturesWithIndependentlyProvidedCPOKeys()
        {

            Assert.That(LiveSessionContinuity.VerifyLiveSessionAuthority(Fixture("00-waiting"), authority, null, Now).EVSEId,
                        Is.EqualTo(authority.EVSEId));

        }

        [Test]
        public void AMathematicallyValidForeignSelfSignatureAndAMismatchedOwnerCannotGrantAuthority()
        {

            var forged = Document(Fixture("00-waiting"), [ Key(199) ]);

            Assert.That(LiveSessionContinuity.ReadLiveSessionDocument(forged, Now).Id, Is.Not.Empty);

            Assert.That(() => LiveSessionContinuity.VerifyLiveSessionAuthority(forged, authority, null, Now),
                        Throws.InstanceOf<LiveSessionException>().With.Message.Contains("authorized"));

            Assert.That(() => LiveSessionContinuity.VerifyLiveSessionAuthority(Fixture("00-waiting"), authority with { OperatorId = "FOREIGN" }, null, Now),
                        Throws.InstanceOf<LiveSessionException>());

            Assert.That(() => LiveSessionContinuity.VerifyLiveSessionAuthority(Fixture("00-waiting"),
                                                                              authority with { Keys = [ new DocumentPublicKey(old.PublicKey.Algorithm, old.PublicKey.Encodings, old.PublicKey.Value, [ "priceStatements" ]) ] },
                                                                              null,
                                                                              Now),
                        Throws.InstanceOf<LiveSessionException>());

        }

        [Test]
        public void AnAuthorizedCoSignedBridgePermitsTheNewKeyAloneInTheFollowingOriginal()
        {

            var original  = Fixture("01-start");
            var cross     = Document(original, [ old, next ]);
            var after     = Document(cross,    [ next ]);
            var both      = authority with { Keys = [ old.PublicKey, next.PublicKey ] };

            Assert.Multiple(() => {

                Assert.That(LiveSessionContinuity.VerifyLiveSessionAuthority(cross, both, original, Now).Readings,  Has.Count.EqualTo(1));
                Assert.That(LiveSessionContinuity.VerifyLiveSessionAuthority(after, both, cross,    Now).Readings,  Has.Count.EqualTo(1));

                Assert.That(() => LiveSessionContinuity.VerifyLiveSessionAuthority(Document(original, [ next ]), both, original, Now),
                            Throws.InstanceOf<LiveSessionException>().With.Message.Contains("co-signature"));

                Assert.That(() => LiveSessionContinuity.VerifyLiveSessionAuthority(cross, authority, original, Now),
                            Throws.InstanceOf<LiveSessionException>().With.Message.Contains("authorized"));

            });

        }


        #region (private) Document(Previous, Signers)

        /// <summary>
        /// The next live session document after the given one, listing and
        /// signed by the given operator keys: a second later, naming its
        /// predecessor, over the complete canonical document.
        /// </summary>
        private static String Document(String                  Previous,
                                       IEnumerable<TestKey>     Signers)
        {

            var signers  = Signers.ToArray();
            var before   = ChargyLib.ParseJSON(Previous);
            var json     = (JObject) before.DeepClone();

            json.Remove("signatures");

            json["lastUpdated"] = DateTimeOffset.Parse(before["lastUpdated"]!.Value<String>()!, CultureInfo.InvariantCulture).
                                                 AddSeconds(1).
                                                 UtcDateTime.
                                                 ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

            json["updates"]     = LiveSessionContinuity.ReadLiveSessionDocument(Previous, Now).Reference.ToUpperInvariant();

            json["chargingStationOperator"]!["publicKeys"] = new JArray(
                                                                 signers.Select(signer => new JObject(
                                                                                              new JProperty("algorithm",  signer.PublicKey.Algorithm),
                                                                                              new JProperty("encodings",  new JArray(signer.PublicKey.Encodings)),
                                                                                              new JProperty("value",      signer.PublicKey.Value),
                                                                                              new JProperty("keyUsage",   new JArray(signer.PublicKey.KeyUsage))
                                                                                          ))
                                                             );

            var signedBytes = CanonicalJSON.ToUTF8Bytes(json);

            json["signatures"] = new JArray(
                                     signers.Select(signer => {

                                         var ed25519 = new Ed25519Signer();
                                         ed25519.Init(true, signer.PrivateKey);
                                         ed25519.BlockUpdate(signedBytes, 0, signedBytes.Length);

                                         return new JObject(
                                                    new JProperty("keyId",       signer.Id),
                                                    new JProperty("algorithm",   "EdDSA-Ed25519"),
                                                    new JProperty("signedData",  new JObject(
                                                                                     new JProperty("excludedProperties",  new JArray("signatures")),
                                                                                     new JProperty("encodings",           new JArray("JSON", "JCS", "UTF-8"))
                                                                                 )),
                                                    new JProperty("encodings",   new JArray("hex")),
                                                    new JProperty("value",       Convert.ToHexString(ed25519.GenerateSignature()))
                                                );

                                     })
                                 );

            return json.ToString(Formatting.None);

        }

        #endregion

    }

}
