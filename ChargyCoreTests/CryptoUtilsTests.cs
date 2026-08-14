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

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Tests for signing and verifying JSON messages, ported from
    /// CryptoUtils.test.ts.
    /// </summary>
    [TestFixture]
    public class CryptoUtilsTests
    {

        #region A_signed_message_verifies(...)

        [TestCase("ECDSA-P256")]
        [TestCase("ECDSA-P384")]
        [TestCase("ECDSA-P521")]
        [TestCase("ECDSA-secp256k1")]
        [TestCase("Ed25519")]
        [TestCase("Ed448")]
        [TestCase("ML-DSA-44")]
        [TestCase("ML-DSA-65")]
        [TestCase("ML-DSA-87")]
        public void A_signed_message_verifies(String Algorithm)
        {

            var message = JObject.Parse("""{ "sessionId": "DE*TEST*E1", "energy": 12.5 }""");
            var keyPair = SignatureSuites.GenerateKeyPair(Algorithm);

            Assert.That(CryptoUtils.SignMessage(message, keyPair),  Is.True);
            Assert.That(CryptoUtils.VerifyMessage(message),         Is.True);

            var signature = (JObject) message["signatures"]![0]!;

            Assert.Multiple(() => {

                Assert.That((String?) signature["algorithm"],          Is.EqualTo(Algorithm));

                // The two spellings of the same bytes must agree, because a
                // consumer may read either of them.
                Assert.That(Convert.ToHexStringLower(Convert.FromBase64String((String) signature["publicKey"]!)),
                            Is.EqualTo((String?) signature["publicKeyHEX"]));

                Assert.That(Convert.ToHexStringLower(Convert.FromBase64String((String) signature["signature"]!)),
                            Is.EqualTo((String?) signature["signatureHEX"]));

            });

        }

        #endregion

        #region The_signatures_are_not_part_of_what_is_signed()

        [Test]
        public void The_signatures_are_not_part_of_what_is_signed()
        {

            // If they were, a second signature could never be added to a message.
            var message = JObject.Parse("""{ "sessionId": "DE*TEST*E1" }""");

            var first   = SignatureSuites.GenerateKeyPair("ECDSA-P256");
            var second  = SignatureSuites.GenerateKeyPair("Ed25519");

            Assert.That(CryptoUtils.SignMessage(message, first),   Is.True);
            Assert.That(CryptoUtils.SignMessage(message, second),  Is.True);

            var results = CryptoUtils.VerifyMessageResults(message);

            Assert.Multiple(() => {
                Assert.That(((JArray) message["signatures"]!).Count,  Is.EqualTo(2));
                Assert.That(results.IsValid,                          Is.True);
                Assert.That(results.Signatures,                       Has.Count.EqualTo(2));
            });

        }

        #endregion

        #region A_signature_survives_reserialization_with_a_different_key_order()

        [Test]
        public void A_signature_survives_reserialization_with_a_different_key_order()
        {

            // This is the whole point of canonicalizing before signing: a charge
            // transparency record passes through backends that re-serialize it.
            var message = JObject.Parse("""{ "b": 2, "a": 1, "nested": { "y": 2, "x": 1 } }""");
            var keyPair = SignatureSuites.GenerateKeyPair("ECDSA-P256");

            Assert.That(CryptoUtils.SignMessage(message, keyPair),  Is.True);

            var reordered = new JObject(
                                new JProperty("nested",      new JObject(new JProperty("x", 1), new JProperty("y", 2))),
                                new JProperty("a",           1),
                                new JProperty("b",           2),
                                new JProperty("signatures",  message["signatures"]!.DeepClone())
                            );

            Assert.That(CryptoUtils.VerifyMessage(reordered),  Is.True);

        }

        #endregion

        #region A_changed_message_does_not_verify()

        [Test]
        public void A_changed_message_does_not_verify()
        {

            var message = JObject.Parse("""{ "sessionId": "DE*TEST*E1", "energy": 12.5 }""");
            var keyPair = SignatureSuites.GenerateKeyPair("ECDSA-P256");

            CryptoUtils.SignMessage(message, keyPair);

            message["energy"] = 999;

            var results = CryptoUtils.VerifyMessageResults(message);

            Assert.Multiple(() => {
                Assert.That(results.IsValid,             Is.False);
                Assert.That(results.Status,              Is.EqualTo(JSONSignatureVerificationStatus.False));
                Assert.That(results.Signatures[0].Status, Is.EqualTo(JSONSignatureVerificationStatus.False));
            });

        }

        #endregion


        #region Signing_with_no_usable_key_pair_reports_failure()

        [Test]
        public void Signing_with_no_usable_key_pair_reports_failure()
        {

            // ChargyCore.TS 46eec22: signing with only unusable key pairs used to
            // report success, so a caller could mistake an unsigned message for a
            // signed one.
            var message    = JObject.Parse("""{ "a": 1 }""");
            var invalidKey = new SignatureKeyPair("ECDSA-P256", [ 0x00 ], []);

            Assert.Multiple(() => {

                Assert.That(CryptoUtils.SignMessage(message, invalidKey),  Is.False);
                Assert.That(message["signatures"],                         Is.Null);

                // No key pair at all is not "nothing to do", it is a failure.
                Assert.That(CryptoUtils.SignMessage(message),                            Is.False);
                Assert.That(CryptoUtils.SignMessage(message, (SignatureKeyPair?) null),  Is.False);

            });

        }

        #endregion

        #region An_unknown_algorithm_is_skipped_rather_than_signed_with()

        [Test]
        public void An_unknown_algorithm_is_skipped_rather_than_signed_with()
        {

            var message = JObject.Parse("""{ "a": 1 }""");
            var unknown = new SignatureKeyPair("ECDSA-P224", [ 0x01 ], []);
            var usable  = SignatureSuites.GenerateKeyPair("ECDSA-P256");

            Assert.Multiple(() => {

                Assert.That(CryptoUtils.SignMessage(message, unknown),  Is.False);

                // ... but a usable key pair alongside it still signs.
                Assert.That(CryptoUtils.SignMessage(message, unknown, usable),  Is.True);
                Assert.That(((JArray) message["signatures"]!).Count,            Is.EqualTo(1));

            });

        }

        #endregion


        #region A_message_without_signatures_reports_why()

        [Test]
        public void A_message_without_signatures_reports_why()
        {

            Assert.Multiple(() => {

                Assert.That(CryptoUtils.VerifyMessageResults(JObject.Parse("""{ "a": 1 }""")).Status,
                            Is.EqualTo(JSONSignatureVerificationStatus.MissingSignatures));

                Assert.That(CryptoUtils.VerifyMessageResults(JObject.Parse("""{ "signatures": [] }""")).Status,
                            Is.EqualTo(JSONSignatureVerificationStatus.MissingSignatures));

                Assert.That(CryptoUtils.VerifyMessageResults(null).Status,
                            Is.EqualTo(JSONSignatureVerificationStatus.InvalidJSON));

                Assert.That(CryptoUtils.VerifyMessageResults(JObject.Parse("""{ "signatures": "nope" }""")).Status,
                            Is.EqualTo(JSONSignatureVerificationStatus.InvalidSignaturesArray));

            });

        }

        #endregion

        #region An_incomplete_signature_object_reports_why()

        [Test]
        public void An_incomplete_signature_object_reports_why()
        {

            var message = JObject.Parse("""
                { "a": 1, "signatures": [ { "publicKey": "AA==", "publicKeyHEX": "00" } ] }
                """);

            Assert.That(CryptoUtils.VerifyMessageResults(message).Signatures[0].Status,
                        Is.EqualTo(JSONSignatureVerificationStatus.InvalidSignatureStructure));

        }

        #endregion

        #region Disagreeing_base64_and_hexadecimal_spellings_are_rejected()

        [Test]
        public void Disagreeing_base64_and_hexadecimal_spellings_are_rejected()
        {

            // A record where the two disagree has been edited by something that
            // understood only one of them, and neither can then be trusted.
            var message = JObject.Parse("""{ "sessionId": "DE*TEST*E1" }""");
            var keyPair = SignatureSuites.GenerateKeyPair("ECDSA-P256");

            CryptoUtils.SignMessage(message, keyPair);

            var signature = (JObject) message["signatures"]![0]!;
            signature["publicKeyHEX"] = "00112233";

            Assert.That(CryptoUtils.VerifyMessageResults(message).Signatures[0].Status,
                        Is.EqualTo(JSONSignatureVerificationStatus.InvalidSignatureEncoding));

        }

        #endregion

        #region A_public_key_of_the_wrong_curve_reports_why()

        [Test]
        public void A_public_key_of_the_wrong_curve_reports_why()
        {

            var message  = JObject.Parse("""{ "sessionId": "DE*TEST*E1" }""");
            var keyPair  = SignatureSuites.GenerateKeyPair("ECDSA-P256");

            CryptoUtils.SignMessage(message, keyPair);

            // Replace the key with a P-384 point, which is not on P-256.
            var other     = SignatureSuites.GenerateKeyPair("ECDSA-P384");
            var signature = (JObject) message["signatures"]![0]!;

            signature["publicKey"]    = Convert.ToBase64String  (other.PublicKey);
            signature["publicKeyHEX"] = Convert.ToHexStringLower(other.PublicKey);

            Assert.That(CryptoUtils.VerifyMessageResults(message).Signatures[0].Status,
                        Is.EqualTo(JSONSignatureVerificationStatus.InvalidPublicKey));

        }

        #endregion

        #region An_unsupported_algorithm_in_a_signature_reports_why()

        [Test]
        public void An_unsupported_algorithm_in_a_signature_reports_why()
        {

            var message = JObject.Parse("""{ "sessionId": "DE*TEST*E1" }""");
            var keyPair = SignatureSuites.GenerateKeyPair("ECDSA-P256");

            CryptoUtils.SignMessage(message, keyPair);

            ((JObject) message["signatures"]![0]!)["algorithm"] = "ECDSA-P224";

            Assert.That(CryptoUtils.VerifyMessageResults(message).Signatures[0].Status,
                        Is.EqualTo(JSONSignatureVerificationStatus.InvalidSignature));

        }

        #endregion

        #region One_invalid_signature_among_several_fails_the_message()

        [Test]
        public void One_invalid_signature_among_several_fails_the_message()
        {

            var message = JObject.Parse("""{ "sessionId": "DE*TEST*E1" }""");

            CryptoUtils.SignMessage(message, SignatureSuites.GenerateKeyPair("ECDSA-P256"));
            CryptoUtils.SignMessage(message, SignatureSuites.GenerateKeyPair("Ed25519"));

            // Corrupt the second signature only.
            var second = (JObject) message["signatures"]![1]!;
            var bytes  = Convert.FromBase64String((String) second["signature"]!);
            bytes[0]  ^= 0xFF;

            second["signature"]    = Convert.ToBase64String  (bytes);
            second["signatureHEX"] = Convert.ToHexStringLower(bytes);

            var results = CryptoUtils.VerifyMessageResults(message);

            Assert.Multiple(() => {
                Assert.That(results.IsValid,              Is.False);
                Assert.That(results.Signatures[0].IsValid, Is.True);
                Assert.That(results.Signatures[1].IsValid, Is.False);
                Assert.That(results.Description,          Is.EqualTo("At least one signature is invalid."));
            });

        }

        #endregion

        #region An_EdDSA_context_is_carried_in_the_signature()

        [Test]
        public void An_EdDSA_context_is_carried_in_the_signature()
        {

            // Without recording the context, a verifier could not reproduce the
            // signature even with the right key.
            var message = JObject.Parse("""{ "sessionId": "DE*TEST*E1" }""");
            var keyPair = SignatureSuites.GenerateKeyPair("Ed25519ctx");
            var context = "chargy"u8.ToArray();

            Assert.That(CryptoUtils.SignMessage(message, new SignatureOptions(Context: context), keyPair),
                        Is.True);

            var signature = (JObject) message["signatures"]![0]!;

            Assert.Multiple(() => {
                Assert.That((String?) signature["contextHEX"],  Is.EqualTo(Convert.ToHexStringLower(context)));
                Assert.That(CryptoUtils.VerifyMessage(message), Is.True);
            });

        }

        #endregion


        #region A_verification_trace_records_the_fields_of_a_signed_buffer()

        [Test]
        public void A_verification_trace_records_the_fields_of_a_signed_buffer()
        {

            // This is what replaces the HTML rendering of ChargyCore.TS: the same
            // information, as data a user interface can lay out itself.
            var trace = new VerificationTrace {
                            Description   = "EMHCrypt01",
                            SignedBuffer  = [ 0x01, 0x02 ],
                            HashedBuffer  = [ 0x03 ],
                            PublicKey     = [ 0x04 ],
                            Signature     = [ 0x05 ],
                            Result        = new CryptoResult(VerificationResult.ValidSignature)
                        };

            trace.Add         ("meterId",    "METER-1", "4d45544552");
            trace.AddLocalized("timestamp",  "2019",    "5eeca25c");

            var json = trace.ToJSON();

            Assert.Multiple(() => {

                Assert.That(trace.Lines,                         Has.Count.EqualTo(2));
                Assert.That(trace.Lines[0].IsLocalizable,        Is.False);
                Assert.That(trace.Lines[1].IsLocalizable,        Is.True);

                Assert.That(trace.SignedBufferHEX,               Is.EqualTo("0102"));
                Assert.That((String?) json["signedBuffer"],      Is.EqualTo("0102"));
                Assert.That((String?) json["description"],       Is.EqualTo("EMHCrypt01"));
                Assert.That((String?) json["result"]?["status"], Is.EqualTo("ValidSignature"));

            });

        }

        #endregion


    }

}
