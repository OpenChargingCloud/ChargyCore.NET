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

using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

#endregion

namespace cloud.charging.open.chargy.Crypto
{

    /// <summary>
    /// ML-DSA, the FIPS 204 post-quantum signature scheme, in its three parameter
    /// sets.
    ///
    /// OCMF added these so that charging records signed today can still be
    /// verified once elliptic curve signatures no longer are trustworthy.
    /// </summary>
    /// <param name="Algorithm">One of "ML-DSA-44", "ML-DSA-65" or "ML-DSA-87".</param>
    public class MLDSASignatureSuite(String Algorithm) : ISignatureSuite
    {

        #region Data

        private readonly MLDsaParameters parameters = Algorithm switch {
                                                          "ML-DSA-44"  => MLDsaParameters.ml_dsa_44,
                                                          "ML-DSA-65"  => MLDsaParameters.ml_dsa_65,
                                                          "ML-DSA-87"  => MLDsaParameters.ml_dsa_87,
                                                          _            => throw new ArgumentException($"Unsupported ML-DSA parameter set '{Algorithm}'!", nameof(Algorithm))
                                                      };

        #endregion

        #region Properties

        /// <summary>The name of the algorithm.</summary>
        public String             Algorithm            { get; } = Algorithm;

        /// <summary>ML-DSA signatures have only one layout.</summary>
        public SignatureEncoding  SignatureEncoding
            => SignatureEncoding.Raw;

        #endregion


        #region GenerateKeyPair()

        /// <summary>
        /// Generate a new key pair.
        /// </summary>
        public SignatureKeyPair GenerateKeyPair()
        {

            var generator = new MLDsaKeyPairGenerator();

            generator.Init(
                new MLDsaKeyGenerationParameters(new SecureRandom(), parameters)
            );

            var keyPair = generator.GenerateKeyPair();

            return new SignatureKeyPair(
                       Algorithm,
                       ((MLDsaPrivateKeyParameters) keyPair.Private).GetEncoded(),
                       ((MLDsaPublicKeyParameters)  keyPair.Public). GetEncoded()
                   );

        }

        #endregion

        #region GetPublicKey     (PrivateKey)

        /// <summary>
        /// The public key belonging to the given private key.
        /// </summary>
        /// <param name="PrivateKey">A private key.</param>
        public Byte[] GetPublicKey(ReadOnlySpan<Byte> PrivateKey)

            => MLDsaPrivateKeyParameters.FromEncoding(parameters, PrivateKey.ToArray()).
                   GetPublicKey().
                   GetEncoded();

        #endregion

        #region IsValidPrivateKey(PrivateKey)

        /// <summary>
        /// Whether the given bytes decode to a private key of this parameter set.
        /// </summary>
        /// <param name="PrivateKey">A private key.</param>
        public Boolean IsValidPrivateKey(ReadOnlySpan<Byte> PrivateKey)
        {

            try
            {

                _ = MLDsaPrivateKeyParameters.FromEncoding(parameters, PrivateKey.ToArray());

                return true;

            }
            catch
            {
                return false;
            }

        }

        #endregion

        #region IsValidPublicKey (PublicKey)

        /// <summary>
        /// Whether the given bytes decode to a public key of this parameter set.
        /// </summary>
        /// <param name="PublicKey">A public key.</param>
        public Boolean IsValidPublicKey(ReadOnlySpan<Byte> PublicKey)
        {

            try
            {

                _ = MLDsaPublicKeyParameters.FromEncoding(parameters, PublicKey.ToArray());

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
        /// <exception cref="ArgumentException">When options are given that ML-DSA does not have.</exception>
        public Byte[] Sign(ReadOnlySpan<Byte>  Message,
                           ReadOnlySpan<Byte>  PrivateKey,
                           SignatureOptions?   Options = null)
        {

            AssertRawOptions(Options);

            var signer = new MLDsaSigner(parameters, deterministic: false);

            signer.Init(
                true,
                MLDsaPrivateKeyParameters.FromEncoding(parameters, PrivateKey.ToArray())
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

                var signer = new MLDsaSigner(parameters, deterministic: false);

                signer.Init(
                    false,
                    MLDsaPublicKeyParameters.FromEncoding(parameters, PublicKey.ToArray())
                );

                signer.BlockUpdate(Message.ToArray(), 0, Message.Length);

                return signer.VerifySignature(Signature.ToArray());

            }
            catch
            {
                return false;
            }

        }

        #endregion


        #region (private) AssertRawOptions(Options)

        /// <summary>
        /// ML-DSA has no encoding choice, and its pre-hash mode is a separate
        /// scheme rather than a boolean, so neither option is accepted here.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// When a context string is given. FIPS 204 defines one and ChargyCore.TS
        /// passes it through to Noble, but BouncyCastle 2.7.0 does not expose it.
        /// This throws rather than signing or verifying without the context:
        /// quietly dropping it would produce signatures that do not match the
        /// record, and — worse on the verifying side — report a valid measurement
        /// as invalid without saying why.
        /// </exception>
        private static void AssertRawOptions(SignatureOptions? Options)
        {

            if (Options is null)
                return;

            if (Options.Prehashed.HasValue)
                throw new ArgumentException("The ML-DSA pre-hash mode must be selected explicitly and is not exposed as a boolean option!", nameof(Options));

            if (Options.Encoding.HasValue && Options.Encoding != SignatureEncoding.Raw)
                throw new ArgumentException("ML-DSA signatures use raw encoding!", nameof(Options));

            if (Options.Context is not null && Options.Context.Length > 0)
                throw new NotSupportedException("BouncyCastle 2.7.0 does not expose the FIPS 204 context string for ML-DSA. Signing or verifying without it would silently produce a different result!");

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
