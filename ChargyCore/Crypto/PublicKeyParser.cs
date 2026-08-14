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

using System.Text.RegularExpressions;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.EdEC;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1.X9;

#endregion

namespace cloud.charging.open.chargy.Crypto
{

    /// <summary>
    /// A public key read from a DER, PEM or hexadecimal SubjectPublicKeyInfo.
    /// </summary>
    /// <param name="Algorithm">
    /// The algorithm name Chargy uses for this key, e.g. "ECDSA-P256", "Ed25519"
    /// or "ML-DSA-65".
    /// </param>
    /// <param name="KeyType">The key type, e.g. "ECC", "EdDSA" or "ML-DSA".</param>
    /// <param name="Value">
    /// The key material: the SEC1 point for ECDSA, the raw key for EdDSA and ML-DSA.
    /// </param>
    /// <param name="CurveName">The curve, for the elliptic curve keys.</param>
    /// <param name="AlgorithmOID">The object identifier the key was labelled with.</param>
    public class ParsedPublicKey(String   Algorithm,
                                 String   KeyType,
                                 Byte[]   Value,
                                 String?  CurveName     = null,
                                 String?  AlgorithmOID  = null)
    {

        #region Properties

        /// <summary>The algorithm name Chargy uses for this key.</summary>
        public String   Algorithm       { get; } = Algorithm;

        /// <summary>The key type, e.g. "ECC", "EdDSA" or "ML-DSA".</summary>
        public String   KeyType         { get; } = KeyType;

        /// <summary>The key material.</summary>
        public Byte[]   Value           { get; } = Value;

        /// <summary>The curve, for the elliptic curve keys.</summary>
        public String?  CurveName       { get; } = CurveName;

        /// <summary>The object identifier the key was labelled with.</summary>
        public String?  AlgorithmOID    { get; } = AlgorithmOID;

        /// <summary>The key material as a hexadecimal string.</summary>
        public String   ValueHEX
            => Convert.ToHexStringLower(Value);

        #endregion


        #region ToPublicKey()

        /// <summary>
        /// This key as the public key data structure of a charge transparency record.
        /// </summary>
        public PublicKey ToPublicKey()

            => new (
                   ValueHEX,
                   new OIDInfo(Algorithm, AlgorithmOID),
                   Encoding: "hex",
                   Type:     new OIDInfo(KeyType)
               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this public key.
        /// </summary>
        public override String ToString()

            => $"{Algorithm}: {ValueHEX}";

        #endregion


    }


    /// <summary>
    /// Reads the public key files that come alongside a charge transparency
    /// record — an operator publishing the key of a charging station, or a PTB
    /// certificate handed over separately.
    ///
    /// Accepts what those files contain in practice: PEM, raw DER, and DER written
    /// out as a hexadecimal string.
    /// </summary>
    public static partial class PublicKeyParser
    {

        #region TryParse    (Text)

        /// <summary>
        /// Try to read a public key from PEM or from a hexadecimal DER string.
        /// </summary>
        /// <param name="Text">The contents of a public key file.</param>
        public static ParsedPublicKey? TryParse(String? Text)
        {

            if (String.IsNullOrWhiteSpace(Text))
                return null;

            var text = Text.Trim();

            if (text.Contains("-----BEGIN", StringComparison.Ordinal))
            {

                var base64 = PEMBodyRegex().Replace(text, "");

                try
                {
                    return TryParseDER(Convert.FromBase64String(WhitespaceRegex().Replace(base64, "")));
                }
                catch
                {
                    return null;
                }

            }

            var hex = WhitespaceRegex().Replace(text, "");

            return ECCurveVerifier.TryParseHex(hex) is Byte[] der
                       ? TryParseDER(der)
                       : null;

        }

        #endregion

        #region TryParseDER (DER)

        /// <summary>
        /// Try to read a public key from an ASN.1 DER encoded SubjectPublicKeyInfo.
        /// </summary>
        /// <param name="DER">A DER encoded SubjectPublicKeyInfo.</param>
        public static ParsedPublicKey? TryParseDER(ReadOnlySpan<Byte> DER)
        {

            if (DER.Length == 0)
                return null;

            try
            {

                var info      = SubjectPublicKeyInfo.GetInstance(Asn1Object.FromByteArray(DER.ToArray()));
                var algorithm = info.Algorithm.Algorithm.Id;
                var keyBytes  = info.PublicKey.GetBytes();

                // Elliptic curve keys name their curve in the algorithm parameters.
                if (algorithm == X9ObjectIdentifiers.IdECPublicKey.Id)
                {

                    var curveOID   = DerObjectIdentifier.GetInstance(info.Algorithm.Parameters);
                    var curveName  = NormalizeCurveName(ECNamedCurveTable.GetName(curveOID));

                    return new ParsedPublicKey(
                               ChargyAlgorithmName(curveName),
                               "ECC",
                               keyBytes,
                               curveName,
                               algorithm
                           );

                }

                if (algorithm == EdECObjectIdentifiers.id_Ed25519.Id)
                    return new ParsedPublicKey("Ed25519", "EdDSA", keyBytes, null, algorithm);

                if (algorithm == EdECObjectIdentifiers.id_Ed448.Id)
                    return new ParsedPublicKey("Ed448",   "EdDSA", keyBytes, null, algorithm);

                // FIPS 204, as registered by NIST under 2.16.840.1.101.3.4.3.x
                var mlDsa = algorithm switch {
                                "2.16.840.1.101.3.4.3.17"  => "ML-DSA-44",
                                "2.16.840.1.101.3.4.3.18"  => "ML-DSA-65",
                                "2.16.840.1.101.3.4.3.19"  => "ML-DSA-87",
                                _                          => null
                            };

                if (mlDsa is not null)
                    return new ParsedPublicKey(mlDsa, "ML-DSA", keyBytes, null, algorithm);

                return null;

            }
            catch
            {
                return null;
            }

        }

        #endregion

        #region LooksLikeAPublicKeyFile(FileName, Text)

        /// <summary>
        /// Whether the given file looks like a hexadecimal public key file.
        ///
        /// Chargy accepts a public key as a bare hex blob next to a charge
        /// transparency record, so the file name has to carry the intent: a
        /// hexadecimal file that nobody called a public key is far more likely to
        /// be something else.
        /// </summary>
        /// <param name="FileName">The name of the file.</param>
        /// <param name="Text">The contents of the file.</param>
        public static Boolean LooksLikeAPublicKeyFile(String FileName, String Text)
        {

            var fileName = FileName.ToLowerInvariant();

            if (!fileName.Contains("publickey", StringComparison.Ordinal) &&
                !fileName.Contains("public_key", StringComparison.Ordinal))
            {
                return false;
            }

            var hex = WhitespaceRegex().Replace(Text, "");

            return hex.Length >= 80 &&
                   hex.Length % 2 == 0 &&
                   hex.StartsWith("30", StringComparison.OrdinalIgnoreCase) &&
                   HexRegex().IsMatch(hex);

        }

        #endregion

        #region (private) NormalizeCurveName(CurveName)

        /// <summary>
        /// The SEC name of a curve.
        ///
        /// BouncyCastle answers with the ANSI X9.62 spelling for the two curves
        /// that have one — "prime256v1" for secp256r1 — while Chargy names its
        /// curves after SEC throughout.
        /// </summary>
        private static String? NormalizeCurveName(String? CurveName)

            => CurveName switch {
                   "prime192v1"  => "secp192r1",
                   "prime256v1"  => "secp256r1",
                   _             => CurveName
               };

        #endregion

        #region (private) ChargyAlgorithmName(CurveName)

        /// <summary>
        /// The Chargy algorithm name of an elliptic curve.
        ///
        /// The curves the signature suites know are named after their algorithm;
        /// the legacy verification-only curves keep their SEC name, because there
        /// is no signing algorithm to name them after.
        /// </summary>
        private static String ChargyAlgorithmName(String? CurveName)

            => CurveName switch {
                   "secp256k1"  => "ECDSA-secp256k1",
                   "secp256r1"  => "ECDSA-P256",
                   "prime256v1" => "ECDSA-P256",
                   "secp384r1"  => "ECDSA-P384",
                   "secp521r1"  => "ECDSA-P521",
                   null         => "ECDSA",
                   _            => CurveName
               };

        #endregion

        #region (private) Regular expressions

        [GeneratedRegex(@"-----(BEGIN|END)[^-]*-----")]
        private static partial Regex PEMBodyRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();

        [GeneratedRegex("^[0-9a-fA-F]+$")]
        private static partial Regex HexRegex();

        #endregion

    }

}
