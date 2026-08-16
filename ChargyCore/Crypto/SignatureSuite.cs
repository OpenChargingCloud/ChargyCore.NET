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

using System.Collections.Frozen;

#endregion

namespace cloud.charging.open.chargy.Crypto
{

    /// <summary>
    /// How the bytes of a signature are laid out.
    ///
    /// Note: This is about the signature itself, not about how it is written into
    /// a charge transparency record as text — that is <see cref="DataEncoding"/>.
    /// ChargyCore.TS uses the name "SignatureEncoding" for this one too.
    /// </summary>
    public enum SignatureEncoding
    {

        /// <summary>The bare r and s values, each padded to the curve's scalar length.</summary>
        Compact,

        /// <summary>An ASN.1 DER encoded SEQUENCE of r and s.</summary>
        DER,

        /// <summary>A signature that has no alternative layout, as with EdDSA and ML-DSA.</summary>
        Raw

    }


    /// <summary>
    /// Options for a single signing or verification operation.
    /// </summary>
    /// <param name="Context">
    /// An optional context string, for the algorithms that take one:
    /// Ed25519ctx, Ed448 and ML-DSA.
    ///
    /// It separates domains and nothing else. A signature made under one context
    /// does not verify under another, so a signature meant to vouch for a charging
    /// record cannot be replayed as vouching for something else — the arithmetic
    /// fails rather than a policy check being expected to notice.
    ///
    /// It is <b>not</b> an identity and must not be used as one. Everyone holding
    /// the key can sign under any context they like, so a context naming a person,
    /// a device or a department proves only what the signer chose to claim. Two
    /// signers sharing one key stay indistinguishable, and worse than if the name
    /// had been written into the message: a context travels out of band, so a
    /// verifier who guesses it wrongly is told "invalid signature" and cannot tell
    /// that apart from a forgery. Who signed is a question for one key per signer
    /// and the public key infrastructure that binds a key to its owner.
    /// </param>
    /// <param name="Prehashed">
    /// Whether <c>Message</c> is already the hash to be signed rather than the
    /// message itself. Only meaningful for ECDSA — the pre-hash variants of EdDSA
    /// and ML-DSA are separate algorithms and must be selected by name.
    /// </param>
    /// <param name="LowS">
    /// Whether an ECDSA signature must use the lower of the two equivalent s
    /// values. Signing normalizes to low s by default; verification does not
    /// require it, because energy meters in the field produced both.
    /// </param>
    /// <param name="Encoding">The signature layout; defaults to the suite's own.</param>
    public class SignatureOptions(Byte[]?             Context    = null,
                                  Boolean?            Prehashed  = null,
                                  Boolean?            LowS       = null,
                                  SignatureEncoding?  Encoding   = null)
    {

        /// <summary>An optional context string.</summary>
        public Byte[]?             Context      { get; } = Context;

        /// <summary>Whether the message is already a hash.</summary>
        public Boolean?            Prehashed    { get; } = Prehashed;

        /// <summary>Whether an ECDSA signature must use the lower s value.</summary>
        public Boolean?            LowS         { get; } = LowS;

        /// <summary>The signature layout.</summary>
        public SignatureEncoding?  Encoding     { get; } = Encoding;

    }


    /// <summary>
    /// A signature key pair.
    /// </summary>
    /// <param name="Algorithm">The signature algorithm.</param>
    /// <param name="PrivateKey">The private key.</param>
    /// <param name="PublicKey">The public key.</param>
    public class SignatureKeyPair(String  Algorithm,
                                  Byte[]  PrivateKey,
                                  Byte[]  PublicKey)
    {

        /// <summary>The signature algorithm.</summary>
        public String  Algorithm     { get; } = Algorithm;

        /// <summary>The private key.</summary>
        public Byte[]  PrivateKey    { get; } = PrivateKey;

        /// <summary>The public key.</summary>
        public Byte[]  PublicKey     { get; } = PublicKey;

    }


    /// <summary>
    /// One signature algorithm, with everything Chargy needs to verify a charge
    /// transparency record signed with it.
    /// </summary>
    public interface ISignatureSuite
    {

        /// <summary>The name of the algorithm, as it appears in a charge transparency record.</summary>
        String             Algorithm          { get; }

        /// <summary>The signature layout this algorithm uses unless told otherwise.</summary>
        SignatureEncoding  SignatureEncoding  { get; }

        /// <summary>Generate a new key pair.</summary>
        SignatureKeyPair   GenerateKeyPair();

        /// <summary>The public key belonging to the given private key.</summary>
        /// <param name="PrivateKey">A private key.</param>
        Byte[]             GetPublicKey     (ReadOnlySpan<Byte> PrivateKey);

        /// <summary>Whether the given bytes are a usable private key.</summary>
        /// <param name="PrivateKey">A private key.</param>
        Boolean            IsValidPrivateKey(ReadOnlySpan<Byte> PrivateKey);

        /// <summary>Whether the given bytes are a usable public key.</summary>
        /// <param name="PublicKey">A public key.</param>
        Boolean            IsValidPublicKey (ReadOnlySpan<Byte> PublicKey);

        /// <summary>
        /// Sign the given message.
        /// </summary>
        /// <param name="Message">The message, or its hash when <c>Prehashed</c> is set.</param>
        /// <param name="PrivateKey">The private key.</param>
        /// <param name="Options">Optional signing options.</param>
        Byte[]             Sign  (ReadOnlySpan<Byte>  Message,
                                  ReadOnlySpan<Byte>  PrivateKey,
                                  SignatureOptions?   Options = null);

        /// <summary>
        /// Verify the given signature.
        ///
        /// Implementations fail closed: a malformed key, a malformed signature or
        /// any failure while computing returns false rather than throwing, so that
        /// a caller trying several candidate public keys is never interrupted.
        /// </summary>
        /// <param name="Message">The message, or its hash when <c>Prehashed</c> is set.</param>
        /// <param name="Signature">The signature.</param>
        /// <param name="PublicKey">The public key.</param>
        /// <param name="Options">Optional verification options.</param>
        Boolean            Verify(ReadOnlySpan<Byte>  Message,
                                  ReadOnlySpan<Byte>  Signature,
                                  ReadOnlySpan<Byte>  PublicKey,
                                  SignatureOptions?   Options = null);

    }


    /// <summary>
    /// The signature algorithms ChargyCore supports, by name.
    /// </summary>
    public static class SignatureSuites
    {

        #region Data

        private static readonly FrozenDictionary<String, ISignatureSuite> suites =
            new Dictionary<String, ISignatureSuite>(StringComparer.Ordinal) {

                { "ECDSA-secp256k1",  new ECDSASignatureSuite("ECDSA-secp256k1", "secp256k1", "SHA-256") },
                { "ECDSA-P256",       new ECDSASignatureSuite("ECDSA-P256",      "secp256r1", "SHA-256") },
                { "ECDSA-P384",       new ECDSASignatureSuite("ECDSA-P384",      "secp384r1", "SHA-384") },
                { "ECDSA-P521",       new ECDSASignatureSuite("ECDSA-P521",      "secp521r1", "SHA-512") },

                { "Ed25519",          new EdDSASignatureSuite("Ed25519")    },
                { "Ed25519ctx",       new EdDSASignatureSuite("Ed25519ctx") },
                { "Ed25519ph",        new EdDSASignatureSuite("Ed25519ph")  },
                { "Ed448",            new EdDSASignatureSuite("Ed448")      },
                { "Ed448ph",          new EdDSASignatureSuite("Ed448ph")    },

                { "ML-DSA-44",        new MLDSASignatureSuite("ML-DSA-44") },
                { "ML-DSA-65",        new MLDSASignatureSuite("ML-DSA-65") },
                { "ML-DSA-87",        new MLDSASignatureSuite("ML-DSA-87") }

            }.ToFrozenDictionary(StringComparer.Ordinal);

        #endregion

        #region Properties

        /// <summary>The names of all supported signature algorithms.</summary>
        public static IEnumerable<String> Algorithms
            => suites.Keys;

        #endregion


        #region Get     (Algorithm)

        /// <summary>
        /// The signature suite of the given algorithm.
        /// </summary>
        /// <param name="Algorithm">The name of a signature algorithm.</param>
        /// <exception cref="ArgumentException">When the algorithm is not supported.</exception>
        public static ISignatureSuite Get(String Algorithm)

            => suites.TryGetValue(Algorithm, out var suite)
                   ? suite
                   : throw new ArgumentException($"Unsupported signature algorithm '{Algorithm}'!", nameof(Algorithm));

        #endregion

        #region TryGet  (Algorithm)

        /// <summary>
        /// The signature suite of the given algorithm, or null when it is not supported.
        /// </summary>
        /// <param name="Algorithm">The name of a signature algorithm.</param>
        public static ISignatureSuite? TryGet(String? Algorithm)

            => Algorithm is not null && suites.TryGetValue(Algorithm, out var suite)
                   ? suite
                   : null;

        #endregion

        #region IsKnown (Algorithm)

        /// <summary>
        /// Whether the given signature algorithm is supported.
        /// </summary>
        /// <param name="Algorithm">The name of a signature algorithm.</param>
        public static Boolean IsKnown(String? Algorithm)

            => Algorithm is not null && suites.ContainsKey(Algorithm);

        #endregion

        #region GenerateKeyPair(Algorithm)

        /// <summary>
        /// Generate a new key pair for the given algorithm.
        /// </summary>
        /// <param name="Algorithm">The name of a signature algorithm.</param>
        public static SignatureKeyPair GenerateKeyPair(String Algorithm)

            => Get(Algorithm).GenerateKeyPair();

        #endregion


        #region (internal) DetectECDSAEncoding(Signature)

        /// <summary>
        /// Whether the given ECDSA signature is DER encoded or a compact r/s pair.
        ///
        /// Note: The known compact lengths are checked first, because a compact
        /// signature whose r happens to start with 0x30 would otherwise be
        /// mistaken for a DER SEQUENCE. This is "detectECDSAEncoding()" of
        /// ChargyCore.TS.
        /// </summary>
        /// <param name="Signature">A signature.</param>
        internal static SignatureEncoding DetectECDSAEncoding(ReadOnlySpan<Byte> Signature)
        {

            var isKnownCompactLength = Signature.Length ==  64 ||   // P-256, secp256k1
                                       Signature.Length ==  96 ||   // P-384
                                       Signature.Length == 132;     // P-521

            return Signature.Length > 0 && Signature[0] == 0x30 && !isKnownCompactLength
                       ? SignatureEncoding.DER
                       : SignatureEncoding.Compact;

        }

        #endregion

    }

}
