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

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.tests
{

    /// <summary>
    /// Tests for the signature suites.
    ///
    /// The EdDSA cases use the test vectors of RFC 8032 rather than values this
    /// implementation produced itself. A round-trip test only proves that signing
    /// and verifying agree with each other; these prove that they agree with every
    /// other correct implementation, which is what actually matters for a charge
    /// transparency record signed by somebody else's energy meter.
    /// </summary>
    [TestFixture]
    public class SignatureSuiteTests
    {

        #region Data

        private static Byte[] Hex(String Value)
            => Convert.FromHexString(Value.Replace(" ", "").Replace("\n", ""));

        #endregion


        #region All_supported_algorithms_are_registered()

        [Test]
        public void All_supported_algorithms_are_registered()
        {

            // The names are the wire format: they appear in the "algorithm" field
            // of a signed JSON message and in OCMF records.
            Assert.That(
                SignatureSuites.Algorithms,
                Is.EquivalentTo(new[] {
                    "ECDSA-secp256k1", "ECDSA-P256", "ECDSA-P384", "ECDSA-P521",
                    "Ed25519", "Ed25519ctx", "Ed25519ph", "Ed448", "Ed448ph",
                    "ML-DSA-44", "ML-DSA-65", "ML-DSA-87"
                })
            );

        }

        #endregion

        #region An_unknown_algorithm_is_rejected()

        [Test]
        public void An_unknown_algorithm_is_rejected()
        {

            Assert.Multiple(() => {
                Assert.That(SignatureSuites.IsKnown("ECDSA-P224"),  Is.False);
                Assert.That(SignatureSuites.TryGet ("ECDSA-P224"),  Is.Null);
                Assert.That(() => SignatureSuites.Get("ECDSA-P224"),  Throws.ArgumentException);
            });

        }

        #endregion


        #region RFC8032_Ed25519_test_vector_1()

        [Test]
        public void RFC8032_Ed25519_test_vector_1()
        {

            // RFC 8032, section 7.1, TEST 1: an empty message.
            var privateKey = Hex("9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60");
            var publicKey  = Hex("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a");
            var expected   = Hex("e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e06522490155" +
                                 "5fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b");

            var suite      = SignatureSuites.Get("Ed25519");
            var signature  = suite.Sign([], privateKey);

            Assert.Multiple(() => {
                Assert.That(suite.GetPublicKey(privateKey),  Is.EqualTo(publicKey));
                Assert.That(signature,                       Is.EqualTo(expected));
                Assert.That(suite.Verify([], expected, publicKey),  Is.True);
            });

        }

        #endregion

        #region RFC8032_Ed25519_test_vector_2()

        [Test]
        public void RFC8032_Ed25519_test_vector_2()
        {

            // RFC 8032, section 7.1, TEST 2: a one byte message.
            var privateKey = Hex("4ccd089b28ff96da9db6c346ec114e0f5b8a319f35aba624da8cf6ed4fb8a6fb");
            var publicKey  = Hex("3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c");
            var message    = Hex("72");
            var expected   = Hex("92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da" +
                                 "085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00");

            var suite      = SignatureSuites.Get("Ed25519");

            Assert.Multiple(() => {
                Assert.That(suite.GetPublicKey(privateKey),               Is.EqualTo(publicKey));
                Assert.That(suite.Sign(message, privateKey),              Is.EqualTo(expected));
                Assert.That(suite.Verify(message, expected, publicKey),   Is.True);
            });

        }

        #endregion

        #region RFC8032_Ed448_test_vector()

        [Test]
        public void RFC8032_Ed448_test_vector()
        {

            // RFC 8032, section 7.4, "Blank": an empty message and an empty context.
            var privateKey = Hex("6c82a562cb808d10d632be89c8513ebf6c929f34ddfa8c9f63c9960ef6e348a3" +
                                 "528c8a3fcc2f044e39a3fc5b94492f8f032e7549a20098f95b");
            var publicKey  = Hex("5fd7449b59b461fd2ce787ec616ad46a1da1342485a70e1f8a0ea75d80e96778" +
                                 "edf124769b46c7061bd6783df1e50f6cd1fa1abeafe8256180");
            var expected   = Hex("533a37f6bbe457251f023c0d88f976ae2dfb504a843e34d2074fd823d41a591f" +
                                 "2b233f034f628281f2fd7a22ddd47d7828c59bd0a21bfd3980ff0d2028d4b18a" +
                                 "9df63e006c5d1c2d345b925d8dc00b4104852db99ac5c7cdda8530a113a0f4db" +
                                 "b61149f05a7363268c71d95808ff2e652600");

            var suite      = SignatureSuites.Get("Ed448");

            Assert.Multiple(() => {
                Assert.That(suite.GetPublicKey(privateKey),        Is.EqualTo(publicKey));
                Assert.That(suite.Sign([], privateKey),            Is.EqualTo(expected));
                Assert.That(suite.Verify([], expected, publicKey), Is.True);
            });

        }

        #endregion

        #region RFC8032_Ed25519ctx_test_vector()

        [Test]
        public void RFC8032_Ed25519ctx_test_vector()
        {

            // RFC 8032, section 7.2. The context is what makes this a different
            // signature from plain Ed25519 over the same message.
            var privateKey = Hex("0305334e381af78f141cb666f6199f57bc3495335a256a95bd2a55bf546663f6");
            var publicKey  = Hex("dfc9425e4f968f7f0c29f0259cf5f9aed6851c2bb4ad8bfb860cfee0ab248292");
            var message    = Hex("f726936d19c800494e3fdaff20b276a8");
            var context    = Hex("666f6f");
            var expected   = Hex("55a4cc2f70a54e04288c5f4cd1e45a7bb520b36292911876cada7323198dd87a" +
                                 "8b36950b95130022907a7fb7c4e9b2d5f6cca685a587b4b21f4b888e4e7edb0d");

            var suite      = SignatureSuites.Get("Ed25519ctx");
            var options    = new SignatureOptions(Context: context);

            Assert.Multiple(() => {

                Assert.That(suite.Sign(message, privateKey, options),             Is.EqualTo(expected));
                Assert.That(suite.Verify(message, expected, publicKey, options),  Is.True);

                // The very same signature must not verify under a different context.
                Assert.That(suite.Verify(message, expected, publicKey, new SignatureOptions(Context: Hex("626172"))),
                            Is.False);

            });

        }

        #endregion

        #region Ed25519ph_is_a_different_algorithm_from_Ed25519()

        [Test]
        public void Ed25519ph_is_a_different_algorithm_from_Ed25519()
        {

            // A pre-hash signature must not verify as a plain one, which is why
            // these are separate algorithms rather than a boolean option.
            var privateKey = Hex("833fe62409237b9d62ec77587520911e9a759cec1d19755b7da901b96dca3d42");
            var message    = Hex("616263");

            var plain      = SignatureSuites.Get("Ed25519");
            var prehash    = SignatureSuites.Get("Ed25519ph");
            var publicKey  = plain.GetPublicKey(privateKey);

            var plainSig   = plain.  Sign(message, privateKey);
            var prehashSig = prehash.Sign(message, privateKey);

            Assert.Multiple(() => {

                Assert.That(plainSig,   Is.Not.EqualTo(prehashSig));

                Assert.That(plain.  Verify(message, plainSig,   publicKey),  Is.True);
                Assert.That(prehash.Verify(message, prehashSig, publicKey),  Is.True);

                Assert.That(plain.  Verify(message, prehashSig, publicKey),  Is.False);
                Assert.That(prehash.Verify(message, plainSig,   publicKey),  Is.False);

            });

        }

        #endregion

        #region EdDSA_rejects_the_options_it_does_not_have()

        [Test]
        public void EdDSA_rejects_the_options_it_does_not_have()
        {

            var suite      = SignatureSuites.Get("Ed25519");
            var privateKey = suite.GenerateKeyPair().PrivateKey;

            Assert.Multiple(() => {

                // Silently ignoring "prehashed" would let a caller believe they
                // selected Ed25519ph while producing a plain Ed25519 signature.
                Assert.That(() => suite.Sign([1], privateKey, new SignatureOptions(Prehashed: true)),
                            Throws.ArgumentException);

                Assert.That(() => suite.Sign([1], privateKey, new SignatureOptions(Encoding: SignatureEncoding.DER)),
                            Throws.ArgumentException);

            });

        }

        #endregion


        #region ECDSA_signs_and_verifies_over_every_supported_curve(...)

        [TestCase("ECDSA-secp256k1")]
        [TestCase("ECDSA-P256")]
        [TestCase("ECDSA-P384")]
        [TestCase("ECDSA-P521")]
        public void ECDSA_signs_and_verifies_over_every_supported_curve(String Algorithm)
        {

            var suite     = SignatureSuites.Get(Algorithm);
            var keyPair   = suite.GenerateKeyPair();
            var message   = "A charge transparency record"u8.ToArray();
            var signature = suite.Sign(message, keyPair.PrivateKey);

            Assert.Multiple(() => {

                Assert.That(suite.Verify(message, signature, keyPair.PublicKey),  Is.True);

                // A different message must not verify.
                Assert.That(suite.Verify("Another record"u8.ToArray(), signature, keyPair.PublicKey),  Is.False);

                Assert.That(suite.IsValidPrivateKey(keyPair.PrivateKey),  Is.True);
                Assert.That(suite.IsValidPublicKey (keyPair.PublicKey),   Is.True);

            });

        }

        #endregion

        #region ECDSA_signatures_round_trip_in_both_encodings(...)

        [TestCase("ECDSA-P256",  64)]
        [TestCase("ECDSA-P384",  96)]
        [TestCase("ECDSA-P521", 132)]
        public void ECDSA_signatures_round_trip_in_both_encodings(String Algorithm, Int32 CompactLength)
        {

            var suite    = SignatureSuites.Get(Algorithm);
            var keyPair  = suite.GenerateKeyPair();
            var message  = "measurement"u8.ToArray();

            var der      = suite.Sign(message, keyPair.PrivateKey, new SignatureOptions(Encoding: SignatureEncoding.DER));
            var compact  = suite.Sign(message, keyPair.PrivateKey, new SignatureOptions(Encoding: SignatureEncoding.Compact));

            Assert.Multiple(() => {

                Assert.That(der[0],           Is.EqualTo(0x30));
                Assert.That(compact.Length,   Is.EqualTo(CompactLength));

                // The encoding is detected from the bytes, so neither needs to be
                // announced by the caller.
                Assert.That(suite.Verify(message, der,     keyPair.PublicKey),  Is.True);
                Assert.That(suite.Verify(message, compact, keyPair.PublicKey),  Is.True);

            });

        }

        #endregion

        #region A_compact_signature_starting_with_0x30_is_not_mistaken_for_DER()

        [Test]
        public void A_compact_signature_starting_with_0x30_is_not_mistaken_for_DER()
        {

            // The known compact lengths are checked first for exactly this reason.
            var compactP256 = new Byte[64];
            compactP256[0]  = 0x30;

            var derLike     = new Byte[70];
            derLike[0]      = 0x30;

            Assert.Multiple(() => {
                Assert.That(SignatureSuites.DetectECDSAEncoding(compactP256),  Is.EqualTo(SignatureEncoding.Compact));
                Assert.That(SignatureSuites.DetectECDSAEncoding(derLike),      Is.EqualTo(SignatureEncoding.DER));
                Assert.That(SignatureSuites.DetectECDSAEncoding(new Byte[64]), Is.EqualTo(SignatureEncoding.Compact));
            });

        }

        #endregion

        #region ECDSA_verification_fails_closed_on_malformed_input()

        [Test]
        public void ECDSA_verification_fails_closed_on_malformed_input()
        {

            // A charge transparency record can name several candidate public keys,
            // and one malformed key must not abort the search for the one that fits.
            var suite    = SignatureSuites.Get("ECDSA-P256");
            var keyPair  = suite.GenerateKeyPair();
            var message  = "measurement"u8.ToArray();
            var good     = suite.Sign(message, keyPair.PrivateKey);

            Assert.Multiple(() => {

                Assert.That(suite.Verify(message, good,          []),                    Is.False);   // no key
                Assert.That(suite.Verify(message, good,          [ 0x04, 0x01 ]),        Is.False);   // truncated key
                Assert.That(suite.Verify(message, good,          new Byte[65]),          Is.False);   // not a point
                Assert.That(suite.Verify(message, [],            keyPair.PublicKey),     Is.False);   // no signature
                Assert.That(suite.Verify(message, [ 0x30, 0x01 ],keyPair.PublicKey),     Is.False);   // truncated DER
                Assert.That(suite.Verify(message, new Byte[64],  keyPair.PublicKey),     Is.False);   // r = s = 0

            });

        }

        #endregion

        #region ECDSA_rejects_out_of_range_scalars()

        [Test]
        public void ECDSA_rejects_out_of_range_scalars()
        {

            // The secp224k1 hardening of ChargyCore.TS made r and s strictly
            // positive and below the group order; the old "== 0" check let
            // out-of-range values through.
            var suite   = SignatureSuites.Get("ECDSA-P256");
            var keyPair = suite.GenerateKeyPair();
            var message = "measurement"u8.ToArray();

            // r = 0, s = 1
            var zeroR   = new Byte[64];
            zeroR[63]   = 1;

            Assert.That(suite.Verify(message, zeroR, keyPair.PublicKey),  Is.False);

        }

        #endregion

        #region ECDSA_verifies_a_signature_over_a_precomputed_hash()

        [Test]
        public void ECDSA_verifies_a_signature_over_a_precomputed_hash()
        {

            // This is the path the energy meter formats take: they assemble the
            // signed buffer themselves and hand Chargy the hash.
            var suite   = SignatureSuites.Get("ECDSA-P256");
            var keyPair = suite.GenerateKeyPair();
            var hash    = System.Security.Cryptography.SHA256.HashData("measurement"u8.ToArray());

            var options   = new SignatureOptions(Prehashed: true);
            var signature = suite.Sign(hash, keyPair.PrivateKey, options);

            Assert.Multiple(() => {
                Assert.That(suite.Verify(hash, signature, keyPair.PublicKey, options),  Is.True);
                // Without the flag the hash would be hashed again.
                Assert.That(suite.Verify(hash, signature, keyPair.PublicKey),           Is.False);
            });

        }

        #endregion


        #region MLDSA_signs_and_verifies(...)

        [TestCase("ML-DSA-44")]
        [TestCase("ML-DSA-65")]
        [TestCase("ML-DSA-87")]
        public void MLDSA_signs_and_verifies(String Algorithm)
        {

            var suite     = SignatureSuites.Get(Algorithm);
            var keyPair   = suite.GenerateKeyPair();
            var message   = "A post-quantum charge transparency record"u8.ToArray();
            var signature = suite.Sign(message, keyPair.PrivateKey);

            Assert.Multiple(() => {

                Assert.That(suite.Verify(message, signature, keyPair.PublicKey),  Is.True);
                Assert.That(suite.Verify("Another record"u8.ToArray(), signature, keyPair.PublicKey),  Is.False);

                Assert.That(suite.IsValidPublicKey (keyPair.PublicKey),   Is.True);
                Assert.That(suite.IsValidPrivateKey(keyPair.PrivateKey),  Is.True);
                Assert.That(suite.IsValidPublicKey ([ 0x01, 0x02 ]),      Is.False);

                Assert.That(suite.GetPublicKey(keyPair.PrivateKey),  Is.EqualTo(keyPair.PublicKey));

            });

        }

        #endregion

        #region MLDSA_binds_the_context_string_into_the_signature(Algorithm)

        // FIPS 204 lets a signature carry a context of up to 255 bytes, which is
        // not part of the message and is signed all the same. That is domain
        // separation: a signature made to vouch for one thing cannot be replayed
        // as vouching for another, because the mathematics fails rather than some
        // policy check being expected to notice.
        //
        // The point of the test is that the context is genuinely part of the
        // signature and not decoration: every combination in which the two sides
        // disagree has to fail.
        [TestCase("ML-DSA-44")]
        [TestCase("ML-DSA-65")]
        [TestCase("ML-DSA-87")]
        public void MLDSA_binds_the_context_string_into_the_signature(String Algorithm)
        {

            var suite    = SignatureSuites.Get(Algorithm)!;
            var keyPair  = suite.GenerateKeyPair();
            var message  = "OCMF|{\"FV\":\"1.4\"}"u8.ToArray();

            var chargy   = new SignatureOptions(Context: "chargy-transparency"u8.ToArray());
            var invoice  = new SignatureOptions(Context: "chargy-invoice"u8.ToArray());

            var signed   = suite.Sign(message, keyPair.PrivateKey, chargy);
            var plain    = suite.Sign(message, keyPair.PrivateKey);

            Assert.Multiple(() => {

                Assert.That(suite.Verify(message, signed, keyPair.PublicKey, chargy),   Is.True);

                // The same signature, the same message, the same key — and a
                // different purpose.
                Assert.That(suite.Verify(message, signed, keyPair.PublicKey, invoice),  Is.False);
                Assert.That(suite.Verify(message, signed, keyPair.PublicKey),           Is.False);

                // ..., and the other way round: a signature made without a context
                // does not become one made with it.
                Assert.That(suite.Verify(message, plain,  keyPair.PublicKey),           Is.True);
                Assert.That(suite.Verify(message, plain,  keyPair.PublicKey, chargy),   Is.False);

                // An empty context is no context, which is what OCMF signs with:
                // the format defines none, so every fixture verifies this way.
                Assert.That(suite.Verify(message, plain,  keyPair.PublicKey, new SignatureOptions(Context: [])),  Is.True);

            });

        }

        #endregion

        #region MLDSA_refuses_a_context_longer_than_FIPS_204_allows()

        [Test]
        public void MLDSA_refuses_a_context_longer_than_FIPS_204_allows()
        {

            var suite      = SignatureSuites.Get("ML-DSA-44")!;
            var keyPair    = suite.GenerateKeyPair();

            // The length goes into the signed domain separator as a single byte,
            // so 255 is the ceiling. Refused rather than left to the signer,
            // because verification fails closed: an over-long context would come
            // back as "this signature is invalid", which says something about the
            // charging record instead of about the call that was made.
            Assert.Multiple(() => {

                Assert.That(() => suite.Sign  ([1], keyPair.PrivateKey, new SignatureOptions(Context: new Byte[256])),
                            Throws.ArgumentException);

                Assert.That(() => suite.Verify([1], [1], keyPair.PublicKey, new SignatureOptions(Context: new Byte[256])),
                            Throws.ArgumentException);

                Assert.That(() => suite.Sign  ([1], keyPair.PrivateKey, new SignatureOptions(Context: new Byte[255])),
                            Throws.Nothing);

            });

        }

        #endregion

        #region A_signature_of_one_algorithm_does_not_verify_under_another()

        [Test]
        public void A_signature_of_one_algorithm_does_not_verify_under_another()
        {

            var p256    = SignatureSuites.Get("ECDSA-P256");
            var k1      = SignatureSuites.Get("ECDSA-secp256k1");
            var keyPair = p256.GenerateKeyPair();
            var message = "measurement"u8.ToArray();

            var signature = p256.Sign(message, keyPair.PrivateKey);

            // The P-256 public key is not a point on secp256k1, so this fails
            // closed rather than throwing.
            Assert.That(k1.Verify(message, signature, keyPair.PublicKey),  Is.False);

        }

        #endregion


    }

}
