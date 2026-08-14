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

#endregion

namespace cloud.charging.open.chargy.Crypto
{

    /// <summary>
    /// A public key on a named elliptic curve, ready to verify the signatures of
    /// an energy meter.
    ///
    /// The energy meter formats assemble the signed buffer themselves and hash it,
    /// so verification here always works on a hash that is already computed.
    /// </summary>
    /// <param name="Curve">The curve this key lives on.</param>
    /// <param name="PublicKey">The uncompressed SEC1 encoding of the key.</param>
    public class ECVerificationKey(ECCurveVerifier  Curve,
                                   Byte[]           PublicKey)
    {

        #region Properties

        /// <summary>The curve this key lives on.</summary>
        public ECCurveVerifier  Curve        { get; } = Curve;

        /// <summary>The uncompressed SEC1 encoding of the key.</summary>
        public Byte[]           PublicKey    { get; } = PublicKey;

        #endregion


        #region Verify(Hash, Signature)

        /// <summary>
        /// Verify a signature over an already computed hash.
        /// </summary>
        /// <param name="Hash">The hash of the signed buffer.</param>
        /// <param name="Signature">The signature, DER encoded or a compact r/s pair.</param>
        public Boolean Verify(ReadOnlySpan<Byte> Hash,
                              ReadOnlySpan<Byte> Signature)

            => Curve.Suite.Verify(
                   Hash,
                   Signature,
                   PublicKey,
                   new SignatureOptions(
                       Prehashed: true,
                       // Energy meters in the field produced both halves of s.
                       LowS:      false
                   )
               );

        #endregion

        #region Verify(Hash, R, S)

        /// <summary>
        /// Verify a signature given as its two hexadecimal integers.
        ///
        /// Several charge transparency data formats carry r and s separately
        /// rather than as a DER blob.
        /// </summary>
        /// <param name="Hash">The hash of the signed buffer.</param>
        /// <param name="R">The r value, hexadecimal.</param>
        /// <param name="S">The s value, hexadecimal.</param>
        public Boolean Verify(ReadOnlySpan<Byte>  Hash,
                              String              R,
                              String              S)
        {

            var signature = Curve.TryEncodeCompactSignature(R, S);

            return signature is not null &&
                   Verify(Hash, signature);

        }

        #endregion

        #region Verify(HashHEX, Signature)

        /// <summary>
        /// Verify a signature over an already computed hash given as hexadecimal.
        /// </summary>
        /// <param name="HashHEX">The hash of the signed buffer, hexadecimal.</param>
        /// <param name="Signature">The signature, DER encoded or a compact r/s pair.</param>
        public Boolean Verify(String              HashHEX,
                              ReadOnlySpan<Byte>  Signature)
        {

            var hash = ECCurveVerifier.TryParseHex(HashHEX);

            return hash is not null &&
                   Verify(hash, Signature);

        }

        #endregion

        #region Verify(HashHEX, R, S)

        /// <summary>
        /// Verify a signature given as its two hexadecimal integers, over a hash
        /// given as hexadecimal.
        /// </summary>
        /// <param name="HashHEX">The hash of the signed buffer, hexadecimal.</param>
        /// <param name="R">The r value, hexadecimal.</param>
        /// <param name="S">The s value, hexadecimal.</param>
        public Boolean Verify(String  HashHEX,
                              String  R,
                              String  S)
        {

            var hash = ECCurveVerifier.TryParseHex(HashHEX);

            return hash is not null &&
                   Verify(hash, R, S);

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this public key.
        /// </summary>
        public override String ToString()

            => $"{Curve.CurveName}: {Convert.ToHexStringLower(PublicKey)}";

        #endregion


    }


    /// <summary>
    /// One named elliptic curve, with everything needed to turn the public key of
    /// a charge transparency record into something that can verify a signature.
    ///
    /// Every method here fails softly: a key that does not parse yields null
    /// rather than an exception, because a record can offer several candidate
    /// keys and Chargy tries them in turn.
    /// </summary>
    /// <param name="CurveName">The SEC name of the curve.</param>
    /// <param name="HashName">The hash the meters on this curve use.</param>
    public class ECCurveVerifier(String  CurveName,
                                 String  HashName)
    {

        #region Data

        /// <summary>NIST/ANSI X9.62 secp192r1, used by the EMH and GDF energy meters.</summary>
        public static readonly ECCurveVerifier secp192r1  = new ("secp192r1", "SHA-256");

        /// <summary>Koblitz curve secp224k1, used by the legacy ChargePoint and Alfen data.</summary>
        public static readonly ECCurveVerifier secp224k1  = new ("secp224k1", "SHA-256");

        /// <summary>Koblitz curve secp256k1.</summary>
        public static readonly ECCurveVerifier secp256k1  = new ("secp256k1", "SHA-256");

        /// <summary>NIST/ANSI X9.62 secp256r1, also known as P-256.</summary>
        public static readonly ECCurveVerifier secp256r1  = new ("secp256r1", "SHA-256");

        /// <summary>NIST/ANSI X9.62 secp384r1, also known as P-384.</summary>
        public static readonly ECCurveVerifier secp384r1  = new ("secp384r1", "SHA-384");

        /// <summary>NIST/ANSI X9.62 secp521r1, also known as P-521.</summary>
        public static readonly ECCurveVerifier secp521r1  = new ("secp521r1", "SHA-512");

        #endregion

        #region Properties

        /// <summary>The SEC name of the curve.</summary>
        public String                CurveName    { get; } = CurveName;

        /// <summary>The signature suite doing the actual work.</summary>
        public ECDSASignatureSuite   Suite        { get; } = new (CurveName, CurveName, HashName);

        /// <summary>The length of one point coordinate in bytes.</summary>
        public Int32                 CoordinateLength
            => Suite.CoordinateLength;

        /// <summary>The length of one signature scalar in bytes.</summary>
        public Int32                 ScalarLength
            => Suite.ScalarLength;

        #endregion


        #region (static) Get(Curve)

        /// <summary>
        /// The verifier of the given curve.
        /// </summary>
        /// <param name="Curve">An elliptic curve.</param>
        public static ECCurveVerifier Get(ECCurve Curve)

            => Curve switch {
                   ECCurve.secp192r1  => secp192r1,
                   ECCurve.secp224k1  => secp224k1,
                   ECCurve.secp256k1  => secp256k1,
                   ECCurve.secp384r1  => secp384r1,
                   ECCurve.secp521r1  => secp521r1,
                   _                  => secp256r1
               };

        #endregion

        #region (static) TryGet(CurveName)

        /// <summary>
        /// The verifier of the given curve name, or null when it is unknown.
        /// Both the SEC and the NIST spellings are accepted.
        /// </summary>
        /// <param name="CurveName">The name of an elliptic curve.</param>
        public static ECCurveVerifier? TryGet(String? CurveName)

            => CurveName?.Trim().ToLowerInvariant() switch {
                   "secp192r1" or "p192" or "p-192"  => secp192r1,
                   "secp224k1"                       => secp224k1,
                   "secp256k1"                       => secp256k1,
                   "secp256r1" or "p256" or "p-256"  => secp256r1,
                   "secp384r1" or "p384" or "p-384"  => secp384r1,
                   "secp521r1" or "p521" or "p-521"  => secp521r1,
                   _                                 => null
               };

        #endregion


        #region ParsePublicKey(PublicKeyHEX)

        /// <summary>
        /// The given hexadecimal public key as a key on this curve, or null when
        /// it is not a point on it.
        ///
        /// A key of exactly two coordinates without the SEC1 prefix gets the
        /// uncompressed marker 0x04 prepended, which is how several charge
        /// transparency data formats write it.
        /// </summary>
        /// <param name="PublicKeyHEX">A public key, hexadecimal.</param>
        public ECVerificationKey? ParsePublicKey(String? PublicKeyHEX)
        {

            if (PublicKeyHEX is null)
                return null;

            var bytes = TryParseHex(PublicKeyHEX);

            if (bytes is null)
                return null;

            if (bytes.Length == 2 * CoordinateLength)
                bytes = [ 0x04, .. bytes ];

            return Suite.IsValidPublicKey(bytes)
                       ? new ECVerificationKey(this, bytes)
                       : null;

        }

        #endregion

        #region ParsePublicKey(X, Y)

        /// <summary>
        /// The given pair of coordinates as a key on this curve, or null when it
        /// is not a point on it.
        /// </summary>
        /// <param name="X">The x coordinate, hexadecimal.</param>
        /// <param name="Y">The y coordinate, hexadecimal.</param>
        public ECVerificationKey? ParsePublicKey(String? X, String? Y)
        {

            if (X is null || Y is null)
                return null;

            var x = TryParseHex(X);
            var y = TryParseHex(Y);

            if (x is null || y is null ||
                x.Length > CoordinateLength ||
                y.Length > CoordinateLength)
            {
                return null;
            }

            var bytes = new Byte[1 + 2 * CoordinateLength];

            bytes[0] = 0x04;
            x.CopyTo(bytes, 1                    + CoordinateLength - x.Length);
            y.CopyTo(bytes, 1 + CoordinateLength + CoordinateLength - y.Length);

            return Suite.IsValidPublicKey(bytes)
                       ? new ECVerificationKey(this, bytes)
                       : null;

        }

        #endregion

        #region (internal) TryEncodeCompactSignature(R, S)

        /// <summary>
        /// The given hexadecimal r and s as a compact signature, or null when
        /// either does not fit this curve.
        /// </summary>
        internal Byte[]? TryEncodeCompactSignature(String R, String S)
        {

            var r = TryParseHex(R);
            var s = TryParseHex(S);

            if (r is null || s is null ||
                r.Length > ScalarLength ||
                s.Length > ScalarLength)
            {
                return null;
            }

            var signature = new Byte[2 * ScalarLength];

            r.CopyTo(signature,                ScalarLength - r.Length);
            s.CopyTo(signature, ScalarLength + ScalarLength - s.Length);

            return signature;

        }

        #endregion

        #region (internal) TryParseHex(Value)

        /// <summary>
        /// The given hexadecimal string as bytes, or null when it is not
        /// hexadecimal. An odd number of digits is left padded, matching the
        /// "hexToBytes()" of ChargyCore.TS.
        /// </summary>
        internal static Byte[]? TryParseHex(String? Value)
        {

            if (Value is null)
                return null;

            var hex = Value.Trim();

            if (hex.Length == 0)
                return [];

            if (hex.Length % 2 != 0)
                hex = "0" + hex;

            var bytes = new Byte[hex.Length / 2];

            for (var i = 0; i < bytes.Length; i++)
                if (!Byte.TryParse(hex.AsSpan(2 * i, 2),
                                   NumberStyles.HexNumber,
                                   CultureInfo.InvariantCulture,
                                   out bytes[i]))
                {
                    return null;
                }

            return bytes;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this curve.
        /// </summary>
        public override String ToString()

            => CurveName;

        #endregion


    }

}
