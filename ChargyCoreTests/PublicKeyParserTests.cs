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
    /// Tests for reading the public key files that come alongside a charge
    /// transparency record, using the very fixtures ChargyCore.TS uses.
    /// </summary>
    [TestFixture]
    public class PublicKeyParserTests : AChargyTests
    {

        #region The_OCMF_test_public_key_is_read_as_a_P256_key()

        [Test]
        public void The_OCMF_test_public_key_is_read_as_a_P256_key()
        {

            var key = PublicKeyParser.TryParse(ReadTextFixture("OCMF/OCMF-Testdata-01_publicKey.txt"));

            Assert.That(key,  Is.Not.Null);

            Assert.Multiple(() => {

                Assert.That(key!.Algorithm,     Is.EqualTo("ECDSA-P256"));
                Assert.That(key. KeyType,       Is.EqualTo("ECC"));
                Assert.That(key. CurveName,     Is.EqualTo("secp256r1"));
                Assert.That(key. AlgorithmOID,  Is.EqualTo("1.2.840.10045.2.1"));

                // The uncompressed SEC1 point: 0x04 plus two 32 byte coordinates.
                Assert.That(key. Value.Length,  Is.EqualTo(65));
                Assert.That(key. Value[0],      Is.EqualTo(0x04));

            });

        }

        #endregion

        #region The_same_key_reads_the_same_from_PEM_and_from_hexadecimal()

        [Test]
        public void The_same_key_reads_the_same_from_PEM_and_from_hexadecimal()
        {

            // The BET tariff text fixtures ship the very same key in both forms.
            var fromPEM = PublicKeyParser.TryParse(ReadTextFixture("OCMF/BET_TariffTextExtension/publicKey.pem"));
            var fromHEX = PublicKeyParser.TryParse(ReadTextFixture("OCMF/BET_TariffTextExtension/publicKey.txt"));

            Assert.Multiple(() => {

                Assert.That(fromPEM,  Is.Not.Null);
                Assert.That(fromHEX,  Is.Not.Null);

                Assert.That(fromPEM!.ValueHEX,   Is.EqualTo(fromHEX!.ValueHEX));
                Assert.That(fromPEM. Algorithm,  Is.EqualTo("ECDSA-P256"));
                Assert.That(fromHEX. Algorithm,  Is.EqualTo("ECDSA-P256"));

            });

        }

        #endregion

        #region A_parsed_key_verifies_a_signature_it_made()

        [Test]
        public void A_parsed_key_verifies_a_signature_it_made()
        {

            // Reading a key is only useful if the result is actually usable for
            // verification, so this closes the loop rather than only checking
            // the parsed fields.
            var suite     = SignatureSuites.Get("ECDSA-P256");
            var keyPair   = suite.GenerateKeyPair();
            var message   = "measurement"u8.ToArray();
            var hash      = System.Security.Cryptography.SHA256.HashData(message);
            var signature = suite.Sign(hash, keyPair.PrivateKey, new SignatureOptions(Prehashed: true));

            var verificationKey = ECCurveVerifier.secp256r1.ParsePublicKey(Convert.ToHexStringLower(keyPair.PublicKey));

            Assert.That(verificationKey,  Is.Not.Null);
            Assert.That(verificationKey!.Verify(hash, signature),  Is.True);

        }

        #endregion

        #region An_EdDSA_and_an_MLDSA_key_are_recognised_by_their_object_identifier()

        [Test]
        public void An_EdDSA_and_an_MLDSA_key_are_recognised_by_their_object_identifier()
        {

            // Round-tripped through BouncyCastle's own SubjectPublicKeyInfo writer,
            // so this checks the object identifier mapping rather than a
            // hand-written blob.
            var ed25519 = Org.BouncyCastle.X509.SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(
                              new Org.BouncyCastle.Crypto.Parameters.Ed25519PublicKeyParameters(
                                  SignatureSuites.Get("Ed25519").GenerateKeyPair().PublicKey
                              )
                          ).GetDerEncoded();

            var ed448   = Org.BouncyCastle.X509.SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(
                              new Org.BouncyCastle.Crypto.Parameters.Ed448PublicKeyParameters(
                                  SignatureSuites.Get("Ed448").GenerateKeyPair().PublicKey
                              )
                          ).GetDerEncoded();

            Assert.Multiple(() => {
                Assert.That(PublicKeyParser.TryParseDER(ed25519)?.Algorithm,  Is.EqualTo("Ed25519"));
                Assert.That(PublicKeyParser.TryParseDER(ed25519)?.KeyType,    Is.EqualTo("EdDSA"));
                Assert.That(PublicKeyParser.TryParseDER(ed448)?.  Algorithm,  Is.EqualTo("Ed448"));
            });

        }

        #endregion

        #region Malformed_public_keys_yield_null_rather_than_throwing()

        [Test]
        public void Malformed_public_keys_yield_null_rather_than_throwing()
        {

            // A charge transparency record can hand Chargy any file. None of them
            // may abort the verification of the records that came with it.
            Assert.Multiple(() => {
                Assert.That(PublicKeyParser.TryParse(null),                    Is.Null);
                Assert.That(PublicKeyParser.TryParse(""),                      Is.Null);
                Assert.That(PublicKeyParser.TryParse("not a key"),             Is.Null);
                Assert.That(PublicKeyParser.TryParse("3059301306072A"),        Is.Null);   // truncated DER
                Assert.That(PublicKeyParser.TryParse("-----BEGIN PUBLIC KEY-----\nnot base64\n-----END PUBLIC KEY-----"),  Is.Null);
                Assert.That(PublicKeyParser.TryParseDER([]),                   Is.Null);
            });

        }

        #endregion

        #region A_public_key_file_is_recognised_by_its_name_and_its_content()

        [Test]
        public void A_public_key_file_is_recognised_by_its_name_and_its_content()
        {

            var hex = ReadTextFixture("OCMF/OCMF-Testdata-01_publicKey.txt");

            Assert.Multiple(() => {

                Assert.That(PublicKeyParser.LooksLikeAPublicKeyFile("OCMF-Testdata-01_publicKey.txt", hex),  Is.True);
                Assert.That(PublicKeyParser.LooksLikeAPublicKeyFile("some_public_key.txt",            hex),  Is.True);

                // Hexadecimal, but nobody called it a public key.
                Assert.That(PublicKeyParser.LooksLikeAPublicKeyFile("measurements.txt",  hex),        Is.False);

                // Called a public key, but not a DER blob.
                Assert.That(PublicKeyParser.LooksLikeAPublicKeyFile("publicKey.txt",     "hello"),    Is.False);

            });

        }

        #endregion

        #region A_parsed_key_becomes_a_charge_transparency_record_public_key()

        [Test]
        public void A_parsed_key_becomes_a_charge_transparency_record_public_key()
        {

            var parsed    = PublicKeyParser.TryParse(ReadTextFixture("OCMF/OCMF-Testdata-01_publicKey.txt"));
            var publicKey = parsed!.ToPublicKey();

            Assert.Multiple(() => {
                Assert.That(publicKey.Value,            Is.EqualTo(parsed.ValueHEX));
                Assert.That(publicKey.Algorithm?.Name,  Is.EqualTo("ECDSA-P256"));
                Assert.That(publicKey.Algorithm?.OID,   Is.EqualTo("1.2.840.10045.2.1"));
                Assert.That(publicKey.Encoding,         Is.EqualTo("hex"));
            });

        }

        #endregion


        #region A_public_key_is_read_from_a_bare_coordinate_pair()

        [Test]
        public void A_public_key_is_read_from_a_bare_coordinate_pair()
        {

            // Several charge transparency data formats carry x and y separately.
            var keyPair = SignatureSuites.Get("ECDSA-P256").GenerateKeyPair();
            var point   = Convert.ToHexStringLower(keyPair.PublicKey);

            var x       = point[2..66];
            var y       = point[66..];

            var fromXY      = ECCurveVerifier.secp256r1.ParsePublicKey(x, y);
            var fromPoint   = ECCurveVerifier.secp256r1.ParsePublicKey(point);
            var withoutPrefix = ECCurveVerifier.secp256r1.ParsePublicKey(point[2..]);

            Assert.Multiple(() => {

                Assert.That(fromXY,         Is.Not.Null);
                Assert.That(fromPoint,      Is.Not.Null);

                // A point without the 0x04 marker gets it prepended.
                Assert.That(withoutPrefix,  Is.Not.Null);

                Assert.That(fromXY!.       PublicKey,  Is.EqualTo(keyPair.PublicKey));
                Assert.That(withoutPrefix!.PublicKey,  Is.EqualTo(keyPair.PublicKey));

            });

        }

        #endregion

        #region A_point_that_is_not_on_the_curve_is_rejected()

        [Test]
        public void A_point_that_is_not_on_the_curve_is_rejected()
        {

            // Without this check an attacker-supplied point would be fed straight
            // into the group arithmetic — the same hardening ChargyCore.TS applied
            // to its secp224k1 implementation.
            Assert.Multiple(() => {

                Assert.That(ECCurveVerifier.secp256r1.ParsePublicKey("04" + new String('0', 128)),  Is.Null);
                Assert.That(ECCurveVerifier.secp256r1.ParsePublicKey("not hex"),                    Is.Null);
                Assert.That(ECCurveVerifier.secp256r1.ParsePublicKey((String?) null),               Is.Null);

                // A valid P-256 point is not a point on secp256k1.
                var p256 = Convert.ToHexStringLower(SignatureSuites.Get("ECDSA-P256").GenerateKeyPair().PublicKey);
                Assert.That(ECCurveVerifier.secp256k1.ParsePublicKey(p256),  Is.Null);

            });

        }

        #endregion

        #region Curves_are_found_by_their_SEC_and_their_NIST_name(...)

        [TestCase("secp256r1", "secp256r1")]
        [TestCase("p256",      "secp256r1")]
        [TestCase("P-256",     "secp256r1")]
        [TestCase("secp192r1", "secp192r1")]
        [TestCase("p192",      "secp192r1")]
        [TestCase("secp224k1", "secp224k1")]
        [TestCase("secp521r1", "secp521r1")]
        public void Curves_are_found_by_their_SEC_and_their_NIST_name(String Name, String Expected)
        {

            Assert.That(ECCurveVerifier.TryGet(Name)?.CurveName,  Is.EqualTo(Expected));

        }

        #endregion

        #region An_unknown_curve_yields_null()

        [Test]
        public void An_unknown_curve_yields_null()
        {

            Assert.Multiple(() => {
                Assert.That(ECCurveVerifier.TryGet("secp224r1"),  Is.Null);
                Assert.That(ECCurveVerifier.TryGet(null),         Is.Null);
            });

        }

        #endregion

        #region The_legacy_verification_curves_are_available()

        [Test]
        public void The_legacy_verification_curves_are_available()
        {

            // secp192r1 is what the EMH and GDF meters sign with, secp224k1 what
            // the legacy ChargePoint and Alfen data uses. Neither has a signing
            // algorithm in the registry, but both must verify.
            Assert.Multiple(() => {

                Assert.That(ECCurveVerifier.secp192r1.CoordinateLength,  Is.EqualTo(24));
                Assert.That(ECCurveVerifier.secp224k1.CoordinateLength,  Is.EqualTo(28));

                var keyPair   = ECCurveVerifier.secp192r1.Suite.GenerateKeyPair();
                var hash      = System.Security.Cryptography.SHA256.HashData("measurement"u8.ToArray());
                var signature = ECCurveVerifier.secp192r1.Suite.Sign(hash, keyPair.PrivateKey, new SignatureOptions(Prehashed: true));
                var key       = ECCurveVerifier.secp192r1.ParsePublicKey(Convert.ToHexStringLower(keyPair.PublicKey));

                Assert.That(key,  Is.Not.Null);
                Assert.That(key!.Verify(hash, signature),  Is.True);

            });

        }

        #endregion

        #region A_signature_given_as_r_and_s_verifies()

        [Test]
        public void A_signature_given_as_r_and_s_verifies()
        {

            // Several formats carry r and s separately rather than as a DER blob.
            var curve     = ECCurveVerifier.secp256r1;
            var keyPair   = curve.Suite.GenerateKeyPair();
            var hash      = System.Security.Cryptography.SHA256.HashData("measurement"u8.ToArray());

            var compact   = curve.Suite.Sign(
                                hash,
                                keyPair.PrivateKey,
                                new SignatureOptions(Prehashed: true, Encoding: SignatureEncoding.Compact)
                            );

            var r         = Convert.ToHexStringLower(compact[..32]);
            var s         = Convert.ToHexStringLower(compact[32..]);
            var key       = curve.ParsePublicKey(Convert.ToHexStringLower(keyPair.PublicKey));

            Assert.Multiple(() => {

                Assert.That(key!.Verify(hash, r, s),  Is.True);

                // Also via the all-hexadecimal overload the meter formats use.
                Assert.That(key. Verify(Convert.ToHexStringLower(hash), r, s),  Is.True);

                // A wrong s must not verify.
                Assert.That(key. Verify(hash, r, new String('1', 64)),  Is.False);

                // Malformed input fails softly.
                Assert.That(key. Verify(hash, "not hex", s),  Is.False);

            });

        }

        #endregion


    }

}
