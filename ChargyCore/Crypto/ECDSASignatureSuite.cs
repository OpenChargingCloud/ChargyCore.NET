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

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

#endregion

namespace cloud.charging.open.chargy.Crypto
{

    /// <summary>
    /// ECDSA over one named elliptic curve.
    /// </summary>
    /// <param name="Algorithm">The name of the algorithm, e.g. "ECDSA-P256".</param>
    /// <param name="CurveName">The SEC name of the curve, e.g. "secp256r1".</param>
    /// <param name="HashName">The hash the message is reduced with, e.g. "SHA-256".</param>
    public class ECDSASignatureSuite(String  Algorithm,
                                     String  CurveName,
                                     String  HashName) : ISignatureSuite
    {

        #region Data

        private readonly X9ECParameters   curve      = ECNamedCurveTable.GetByName(CurveName)
                                                           ?? throw new ArgumentException($"Unknown elliptic curve '{CurveName}'!", nameof(CurveName));

        private readonly ECDomainParameters domain   = new (ECNamedCurveTable.GetByName(CurveName)!);

        #endregion

        #region Properties

        /// <summary>The name of the algorithm.</summary>
        public String             Algorithm            { get; } = Algorithm;

        /// <summary>The SEC name of the curve.</summary>
        public String             CurveName            { get; } = CurveName;

        /// <summary>The hash the message is reduced with.</summary>
        public String             HashName             { get; } = HashName;

        /// <summary>ECDSA signatures are DER encoded unless a caller asks for the compact layout.</summary>
        public SignatureEncoding  SignatureEncoding
            => SignatureEncoding.DER;

        /// <summary>The length of one signature scalar in bytes.</summary>
        public Int32              ScalarLength
            => (domain.N.BitLength + 7) / 8;

        /// <summary>The length of one point coordinate in bytes.</summary>
        public Int32              CoordinateLength
            => (curve.Curve.FieldSize + 7) / 8;

        #endregion


        #region GenerateKeyPair()

        /// <summary>
        /// Generate a new key pair.
        /// </summary>
        public SignatureKeyPair GenerateKeyPair()
        {

            var generator = GeneratorUtilities.GetKeyPairGenerator("ECDSA");

            generator.Init(
                new ECKeyGenerationParameters(domain, new SecureRandom())
            );

            var keyPair    = generator.GenerateKeyPair();
            var privateKey = ((ECPrivateKeyParameters) keyPair.Private).D;

            return new SignatureKeyPair(
                       Algorithm,
                       BigIntegerToFixedLength(privateKey, ScalarLength),
                       ((ECPublicKeyParameters) keyPair.Public).Q.GetEncoded(false)
                   );

        }

        #endregion

        #region GetPublicKey     (PrivateKey)

        /// <summary>
        /// The uncompressed public key belonging to the given private key.
        /// </summary>
        /// <param name="PrivateKey">A private key.</param>
        public Byte[] GetPublicKey(ReadOnlySpan<Byte> PrivateKey)

            => domain.G.Multiply(new BigInteger(1, PrivateKey.ToArray())).Normalize().GetEncoded(false);

        #endregion

        #region IsValidPrivateKey(PrivateKey)

        /// <summary>
        /// Whether the given bytes are a scalar in [1, n-1].
        /// </summary>
        /// <param name="PrivateKey">A private key.</param>
        public Boolean IsValidPrivateKey(ReadOnlySpan<Byte> PrivateKey)
        {

            if (PrivateKey.Length == 0)
                return false;

            try
            {

                var d = new BigInteger(1, PrivateKey.ToArray());

                return d.SignValue > 0 &&
                       d.CompareTo(domain.N) < 0;

            }
            catch
            {
                return false;
            }

        }

        #endregion

        #region IsValidPublicKey (PublicKey)

        /// <summary>
        /// Whether the given bytes decode to a point on this curve.
        /// </summary>
        /// <param name="PublicKey">A public key.</param>
        public Boolean IsValidPublicKey(ReadOnlySpan<Byte> PublicKey)

            => TryDecodePoint(PublicKey) is not null;

        #endregion

        #region (private) TryDecodePoint(PublicKey)

        /// <summary>
        /// The given bytes as a point on this curve, or null when they are not one.
        ///
        /// Every caller of this treats null as "verification failed" rather than
        /// letting an exception escape: a charge transparency record can name
        /// several candidate public keys, and one malformed key must not abort the
        /// search for the one that fits.
        /// </summary>
        private Org.BouncyCastle.Math.EC.ECPoint? TryDecodePoint(ReadOnlySpan<Byte> PublicKey)
        {

            if (PublicKey.Length == 0)
                return null;

            try
            {

                var point = curve.Curve.DecodePoint(PublicKey.ToArray()).Normalize();

                return point.IsValid() && !point.IsInfinity
                           ? point
                           : null;

            }
            catch
            {
                return null;
            }

        }

        #endregion


        #region Sign  (Message, PrivateKey, Options = null)

        /// <summary>
        /// Sign the given message.
        /// </summary>
        /// <param name="Message">The message, or its hash when the options say so.</param>
        /// <param name="PrivateKey">The private key.</param>
        /// <param name="Options">Optional signing options.</param>
        /// <exception cref="ArgumentException">When raw encoding is requested, which ECDSA does not have.</exception>
        public Byte[] Sign(ReadOnlySpan<Byte>  Message,
                           ReadOnlySpan<Byte>  PrivateKey,
                           SignatureOptions?   Options = null)
        {

            var encoding = Options?.Encoding ?? SignatureEncoding;

            if (encoding == SignatureEncoding.Raw)
                throw new ArgumentException("ECDSA signatures must use compact or DER encoding!", nameof(Options));

            var hash   = Options?.Prehashed == true
                             ? Message.ToArray()
                             : Digest(Message);

            var signer = new ECDsaSigner(
                             new HMacDsaKCalculator(CreateDigest())
                         );

            signer.Init(
                true,
                new ECPrivateKeyParameters(new BigInteger(1, PrivateKey.ToArray()), domain)
            );

            var signature = signer.GenerateSignature(Truncate(hash));
            var r         = signature[0];
            var s         = signature[1];

            // Normalize to the lower of the two equivalent s values unless the
            // caller explicitly asked not to; this is what ChargyCore.TS does.
            if (Options?.LowS != false)
            {

                var halfN = domain.N.ShiftRight(1);

                if (s.CompareTo(halfN) > 0)
                    s = domain.N.Subtract(s);

            }

            return encoding == SignatureEncoding.DER
                       ? EncodeDER(r, s)
                       : EncodeCompact(r, s);

        }

        #endregion

        #region Verify(Message, Signature, PublicKey, Options = null)

        /// <summary>
        /// Verify the given signature, failing closed on anything malformed.
        /// </summary>
        /// <param name="Message">The message, or its hash when the options say so.</param>
        /// <param name="Signature">The signature, DER encoded or a compact r/s pair.</param>
        /// <param name="PublicKey">The public key.</param>
        /// <param name="Options">Optional verification options.</param>
        public Boolean Verify(ReadOnlySpan<Byte>  Message,
                              ReadOnlySpan<Byte>  Signature,
                              ReadOnlySpan<Byte>  PublicKey,
                              SignatureOptions?   Options = null)
        {

            try
            {

                var encoding = Options?.Encoding ?? SignatureSuites.DetectECDSAEncoding(Signature);

                if (encoding == SignatureEncoding.Raw)
                    return false;

                var point = TryDecodePoint(PublicKey);

                if (point is null)
                    return false;

                var (r, s) = encoding == SignatureEncoding.DER
                                 ? DecodeDER    (Signature)
                                 : DecodeCompact(Signature);

                if (r is null || s is null)
                    return false;

                // Fail closed on out-of-range scalars. ChargyCore.TS hardened its
                // secp224k1 implementation for exactly this: r and s must be
                // strictly positive and below the group order.
                if (r.SignValue <= 0 || r.CompareTo(domain.N) >= 0 ||
                    s.SignValue <= 0 || s.CompareTo(domain.N) >= 0)
                {
                    return false;
                }

                // Verification does not require low s: energy meters in the field
                // produced both, and rejecting the high half would fail genuine
                // measurements. Signing normalizes, verifying accepts.
                if (Options?.LowS == true &&
                    s.CompareTo(domain.N.ShiftRight(1)) > 0)
                {
                    return false;
                }

                var hash   = Options?.Prehashed == true
                                 ? Message.ToArray()
                                 : Digest(Message);

                var signer = new ECDsaSigner();

                signer.Init(false, new ECPublicKeyParameters(point, domain));

                return signer.VerifySignature(Truncate(hash), r, s);

            }
            catch
            {
                return false;
            }

        }

        #endregion


        #region (private) Digest / Truncate / CreateDigest

        private IDigest CreateDigest()

            => HashName switch {
                   "SHA-256"  => new Sha256Digest(),
                   "SHA-384"  => new Sha384Digest(),
                   "SHA-512"  => new Sha512Digest(),
                   _          => throw new NotSupportedException($"Unsupported hash algorithm '{HashName}'!")
               };

        private Byte[] Digest(ReadOnlySpan<Byte> Message)
        {

            var digest = CreateDigest();
            var result = new Byte[digest.GetDigestSize()];

            digest.BlockUpdate(Message);
            digest.DoFinal(result);

            return result;

        }

        /// <summary>
        /// ECDSA uses the leftmost bits of the hash when the hash is longer than
        /// the group order, e.g. SHA-256 with secp192r1.
        /// </summary>
        private Byte[] Truncate(Byte[] Hash)
        {

            var maxLength = (domain.N.BitLength + 7) / 8;

            return Hash.Length > maxLength
                       ? Hash[..maxLength]
                       : Hash;

        }

        #endregion

        #region (private) Encode / Decode

        private static Byte[] EncodeDER(BigInteger R, BigInteger S)

            => new DerSequence(
                   new DerInteger(R),
                   new DerInteger(S)
               ).GetDerEncoded();

        private Byte[] EncodeCompact(BigInteger R, BigInteger S)
        {

            var result = new Byte[2 * ScalarLength];

            BigIntegerToFixedLength(R, ScalarLength).CopyTo(result, 0);
            BigIntegerToFixedLength(S, ScalarLength).CopyTo(result, ScalarLength);

            return result;

        }

        private static (BigInteger?, BigInteger?) DecodeDER(ReadOnlySpan<Byte> Signature)
        {

            try
            {

                if (Asn1Object.FromByteArray(Signature.ToArray()) is not Asn1Sequence sequence ||
                    sequence.Count != 2)
                {
                    return (null, null);
                }

                return (
                    ((DerInteger) sequence[0]).PositiveValue,
                    ((DerInteger) sequence[1]).PositiveValue
                );

            }
            catch
            {
                return (null, null);
            }

        }

        private (BigInteger?, BigInteger?) DecodeCompact(ReadOnlySpan<Byte> Signature)
        {

            if (Signature.Length != 2 * ScalarLength)
                return (null, null);

            return (
                new BigInteger(1, Signature[..ScalarLength].          ToArray()),
                new BigInteger(1, Signature[ScalarLength..].          ToArray())
            );

        }

        private static Byte[] BigIntegerToFixedLength(BigInteger Value, Int32 Length)
        {

            var bytes  = Value.ToByteArrayUnsigned();
            var result = new Byte[Length];

            bytes.CopyTo(result, Length - bytes.Length);

            return result;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this signature suite.
        /// </summary>
        public override String ToString()

            => $"{Algorithm} ({CurveName}, {HashName})";

        #endregion


    }

}
