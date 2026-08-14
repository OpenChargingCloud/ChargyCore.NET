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

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

#endregion

namespace cloud.charging.open.chargy.Crypto
{

    /// <summary>
    /// EdDSA over Curve25519 or Curve448, in its plain, context and pre-hash
    /// variants.
    ///
    /// The pre-hash variants are separate algorithms rather than an option: a
    /// signature made with Ed25519ph does not verify as Ed25519, and letting a
    /// caller flip a boolean would make the two confusable.
    /// </summary>
    /// <param name="Algorithm">
    /// One of "Ed25519", "Ed25519ctx", "Ed25519ph", "Ed448" or "Ed448ph".
    /// </param>
    public class EdDSASignatureSuite(String Algorithm) : ISignatureSuite
    {

        #region Properties

        /// <summary>The name of the algorithm.</summary>
        public String             Algorithm            { get; } = Algorithm;

        /// <summary>EdDSA signatures have only one layout.</summary>
        public SignatureEncoding  SignatureEncoding
            => SignatureEncoding.Raw;

        /// <summary>Whether this is one of the Curve448 variants.</summary>
        public Boolean            Is448
            => Algorithm.StartsWith("Ed448", StringComparison.Ordinal);

        /// <summary>The length of a private key in bytes.</summary>
        public Int32              PrivateKeyLength
            => Is448 ? Ed448PrivateKeyParameters.KeySize : Ed25519PrivateKeyParameters.KeySize;

        /// <summary>The length of a public key in bytes.</summary>
        public Int32              PublicKeyLength
            => Is448 ? Ed448PublicKeyParameters.KeySize  : Ed25519PublicKeyParameters.KeySize;

        #endregion


        #region GenerateKeyPair()

        /// <summary>
        /// Generate a new key pair.
        /// </summary>
        public SignatureKeyPair GenerateKeyPair()
        {

            var random     = new SecureRandom();
            var privateKey = new Byte[PrivateKeyLength];

            random.NextBytes(privateKey);

            return new SignatureKeyPair(
                       Algorithm,
                       privateKey,
                       GetPublicKey(privateKey)
                   );

        }

        #endregion

        #region GetPublicKey     (PrivateKey)

        /// <summary>
        /// The public key belonging to the given private key.
        /// </summary>
        /// <param name="PrivateKey">A private key.</param>
        public Byte[] GetPublicKey(ReadOnlySpan<Byte> PrivateKey)

            => Is448
                   ? new Ed448PrivateKeyParameters  (PrivateKey.ToArray()).GeneratePublicKey().GetEncoded()
                   : new Ed25519PrivateKeyParameters(PrivateKey.ToArray()).GeneratePublicKey().GetEncoded();

        #endregion

        #region IsValidPrivateKey(PrivateKey)

        /// <summary>
        /// Whether the given bytes have the length of a private key.
        /// </summary>
        /// <param name="PrivateKey">A private key.</param>
        public Boolean IsValidPrivateKey(ReadOnlySpan<Byte> PrivateKey)

            => PrivateKey.Length == PrivateKeyLength;

        #endregion

        #region IsValidPublicKey (PublicKey)

        /// <summary>
        /// Whether the given bytes decode to a public key.
        /// </summary>
        /// <param name="PublicKey">A public key.</param>
        public Boolean IsValidPublicKey(ReadOnlySpan<Byte> PublicKey)
        {

            if (PublicKey.Length != PublicKeyLength)
                return false;

            try
            {

                _ = CreatePublicKeyParameters(PublicKey);

                return true;

            }
            catch
            {
                return false;
            }

        }

        #endregion


        #region Sign  (Message, PrivateKey, Options = null)

        /// <summary>
        /// Sign the given message.
        /// </summary>
        /// <param name="Message">The message.</param>
        /// <param name="PrivateKey">The private key.</param>
        /// <param name="Options">Optional signing options.</param>
        /// <exception cref="ArgumentException">When options are given that EdDSA does not have.</exception>
        public Byte[] Sign(ReadOnlySpan<Byte>  Message,
                           ReadOnlySpan<Byte>  PrivateKey,
                           SignatureOptions?   Options = null)
        {

            AssertRawOptions(Options);

            var signer = CreateSigner(Options?.Context);

            signer.Init(
                true,
                Is448
                    ? new Ed448PrivateKeyParameters  (PrivateKey.ToArray())
                    : new Ed25519PrivateKeyParameters(PrivateKey.ToArray())
            );

            signer.BlockUpdate(Message.ToArray(), 0, Message.Length);

            return signer.GenerateSignature();

        }

        #endregion

        #region Verify(Message, Signature, PublicKey, Options = null)

        /// <summary>
        /// Verify the given signature, failing closed on anything malformed.
        /// </summary>
        /// <param name="Message">The message.</param>
        /// <param name="Signature">The signature.</param>
        /// <param name="PublicKey">The public key.</param>
        /// <param name="Options">Optional verification options.</param>
        public Boolean Verify(ReadOnlySpan<Byte>  Message,
                              ReadOnlySpan<Byte>  Signature,
                              ReadOnlySpan<Byte>  PublicKey,
                              SignatureOptions?   Options = null)
        {

            AssertRawOptions(Options);

            try
            {

                if (PublicKey.Length != PublicKeyLength)
                    return false;

                var signer = CreateSigner(Options?.Context);

                signer.Init(false, CreatePublicKeyParameters(PublicKey));
                signer.BlockUpdate(Message.ToArray(), 0, Message.Length);

                return signer.VerifySignature(Signature.ToArray());

            }
            catch
            {
                return false;
            }

        }

        #endregion


        #region (private) CreateSigner / CreatePublicKeyParameters / AssertRawOptions

        private ISigner CreateSigner(Byte[]? Context)

            => Algorithm switch {

                   "Ed25519"     => new Ed25519Signer(),
                   "Ed25519ctx"  => new Ed25519ctxSigner(Context ?? []),
                   "Ed25519ph"   => new Ed25519phSigner (Context ?? []),

                   // Ed448 always carries a context, empty by default; that is the
                   // shape RFC 8032 defines for it, not an extra Chargy notion.
                   "Ed448"       => new Ed448Signer     (Context ?? []),
                   "Ed448ph"     => new Ed448phSigner   (Context ?? []),

                   _             => throw new NotSupportedException($"Unsupported EdDSA algorithm '{Algorithm}'!")

               };

        private AsymmetricKeyParameter CreatePublicKeyParameters(ReadOnlySpan<Byte> PublicKey)

            => Is448
                   ? new Ed448PublicKeyParameters  (PublicKey.ToArray())
                   : new Ed25519PublicKeyParameters(PublicKey.ToArray());

        /// <summary>
        /// EdDSA has no encoding choice and no pre-hash flag: rejecting those
        /// options keeps a caller from believing they selected Ed25519ph when they
        /// are in fact producing a plain Ed25519 signature.
        /// </summary>
        private static void AssertRawOptions(SignatureOptions? Options)
        {

            if (Options is null)
                return;

            if (Options.Prehashed.HasValue)
                throw new ArgumentException("Select Ed25519ph or Ed448ph instead of using the prehashed option with EdDSA!", nameof(Options));

            if (Options.Encoding.HasValue && Options.Encoding != SignatureEncoding.Raw)
                throw new ArgumentException("EdDSA signatures use raw encoding!", nameof(Options));

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this signature suite.
        /// </summary>
        public override String ToString()

            => Algorithm;

        #endregion


    }

}
