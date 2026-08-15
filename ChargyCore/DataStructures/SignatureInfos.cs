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

#endregion

namespace cloud.charging.open.chargy
{

    /// <summary>
    /// How an energy meter signs its measurements: which hash, which algorithm,
    /// which curve, and how the resulting signature is encoded.
    ///
    /// The five enums below exist only to describe this, which is why they live
    /// in the same file.
    /// </summary>
    /// <param name="Hash">The hash algorithm.</param>
    /// <param name="Algorithm">The signature algorithm.</param>
    /// <param name="Curve">The elliptic curve.</param>
    /// <param name="Format">The signature format.</param>
    /// <param name="HashTruncation">An optional number of bits the hash is truncated to.</param>
    /// <param name="Encoding">An optional encoding of the signature.</param>
    public class SignatureInfos(CryptoHashAlgorithm  Hash,
                                CryptoAlgorithm      Algorithm,
                                ECCurve              Curve,
                                SignatureFormat      Format,
                                UInt16?              HashTruncation  = null,
                                DataEncoding?        Encoding        = null)
    {

        #region Properties

        /// <summary>The hash algorithm.</summary>
        public CryptoHashAlgorithm  Hash              { get; } = Hash;

        /// <summary>The signature algorithm.</summary>
        public CryptoAlgorithm      Algorithm         { get; } = Algorithm;

        /// <summary>The elliptic curve.</summary>
        public ECCurve              Curve             { get; } = Curve;

        /// <summary>The signature format.</summary>
        public SignatureFormat      Format            { get; } = Format;

        /// <summary>An optional number of bits the hash is truncated to.</summary>
        public UInt16?              HashTruncation    { get; } = HashTruncation;

        /// <summary>An optional encoding of the signature.</summary>
        public DataEncoding?        Encoding          { get; } = Encoding;

        #endregion


        #region (static) TryParse(JSON, out SignatureInfos)

        /// <summary>
        /// Try to parse the given JSON as signature information.
        /// </summary>
        /// <param name="JSON">A JSON representation of signature information.</param>
        /// <param name="SignatureInfos">The parsed signature information.</param>
        public static Boolean TryParse(JObject JSON, out SignatureInfos? SignatureInfos)
        {

            SignatureInfos = null;

            if (!CryptoHashAlgorithmExtensions.TryParse(JSON["hash"]?.     Value<String>() ?? "", out var hash)      ||
                !CryptoAlgorithmExtensions.    TryParse(JSON["algorithm"]?.Value<String>() ?? "", out var algorithm) ||
                !ECCurveExtensions.            TryParse(JSON["curve"]?.    Value<String>() ?? "", out var curve)     ||
                !SignatureFormatExtensions.    TryParse(JSON["format"]?.   Value<String>() ?? "", out var format))
            {
                return false;
            }

            SignatureInfos = new SignatureInfos(
                                 hash,
                                 algorithm,
                                 curve,
                                 format,
                                 JSON["hashTruncation"]?.Value<UInt16>(),
                                 DataEncodingExtensions.TryParse(JSON["encoding"]?.Value<String>() ?? "")
                             );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this signature information.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("hash",       Hash.     AsText()),
                           new JProperty("algorithm",  Algorithm.AsText()),
                           new JProperty("curve",      Curve.    AsText()),
                           new JProperty("format",     Format.   AsText())
                       );

            if (HashTruncation.HasValue)
                json.Add(new JProperty("hashTruncation",  HashTruncation.Value));

            if (Encoding.HasValue)
                json.Add(new JProperty("encoding",        Encoding.Value.AsText()));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this signature information.
        /// </summary>
        public override String ToString()

            => $"{Algorithm.AsText()} {Curve.AsText()}, {Hash.AsText()}, {Format.AsText()}";

        #endregion


    }


    /// <summary>
    /// The elliptic curves the supported energy meters sign with.
    /// </summary>
    public enum ECCurve
    {

        /// <summary>NIST/ANSI X9.62 secp192r1, used by the older EMH and GDF meters.</summary>
        secp192r1,

        /// <summary>Koblitz curve secp224k1.</summary>
        secp224k1,

        /// <summary>Koblitz curve secp256k1.</summary>
        secp256k1,

        /// <summary>NIST/ANSI X9.62 secp256r1.</summary>
        secp256r1,

        /// <summary>NIST/ANSI X9.62 secp384r1.</summary>
        secp384r1,

        /// <summary>NIST/ANSI X9.62 secp521r1.</summary>
        secp521r1

    }


    /// <summary>
    /// Extension methods for elliptic curves.
    /// </summary>
    public static class ECCurveExtensions
    {

        #region TryParse(Text, out ECCurve)

        /// <summary>
        /// Try to parse the given text as an elliptic curve.
        /// </summary>
        /// <param name="Text">A text representation of an elliptic curve.</param>
        /// <param name="ECCurve">The parsed elliptic curve.</param>
        public static Boolean TryParse(String Text, out ECCurve ECCurve)
        {

            switch (Text.Trim().ToLowerInvariant())
            {

                case "secp192r1":
                    ECCurve = ECCurve.secp192r1;
                    return true;

                case "secp224k1":
                    ECCurve = ECCurve.secp224k1;
                    return true;

                case "secp256k1":
                    ECCurve = ECCurve.secp256k1;
                    return true;

                case "secp256r1":
                    ECCurve = ECCurve.secp256r1;
                    return true;

                case "secp384r1":
                    ECCurve = ECCurve.secp384r1;
                    return true;

                case "secp521r1":
                // "secp512r1" is not a curve that exists. It was a typo in the
                // IECCurves enum of ChargyCore.TS, fixed there in the meantime,
                // but charge transparency records written before that carry the
                // misspelling. Accepted on input, never written back out.
                case "secp512r1":
                    ECCurve = ECCurve.secp521r1;
                    return true;

                default:
                    ECCurve = ECCurve.secp256r1;
                    return false;

            }

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text as an elliptic curve.
        /// </summary>
        /// <param name="Text">A text representation of an elliptic curve.</param>
        public static ECCurve? TryParse(String Text)

            => TryParse(Text, out var curve)
                   ? curve
                   : null;

        #endregion

        #region AsText  (this ECCurve)

        /// <summary>
        /// The wire representation of the given elliptic curve.
        /// </summary>
        /// <param name="ECCurve">An elliptic curve.</param>
        public static String AsText(this ECCurve ECCurve)

            => ECCurve.ToString();

        #endregion

    }


    /// <summary>
    /// How a signature or a public key is encoded.
    /// </summary>
    public enum DataEncoding
    {

        /// <summary>Hexadecimal.</summary>
        Hex,

        /// <summary>Base64.</summary>
        Base64,

        /// <summary>
        /// Base32, as defined by RFC 4648.
        ///
        /// Used by the Alfen format, because its signed meter values have to
        /// survive being printed on a receipt and typed back in by hand: base32
        /// has no lower case and no characters an EV driver could confuse.
        /// </summary>
        Base32

    }


    /// <summary>
    /// Extension methods for signature encodings.
    /// </summary>
    public static class DataEncodingExtensions
    {

        #region TryParse(Text, out DataEncoding)

        /// <summary>
        /// Try to parse the given text as a signature encoding.
        /// </summary>
        /// <param name="Text">A text representation of a signature encoding.</param>
        /// <param name="DataEncoding">The parsed signature encoding.</param>
        public static Boolean TryParse(String Text, out DataEncoding DataEncoding)
        {

            switch (Text.Trim().ToLowerInvariant())
            {

                case "hex":
                    DataEncoding = DataEncoding.Hex;
                    return true;

                case "base64":
                    DataEncoding = DataEncoding.Base64;
                    return true;

                case "base32":
                    DataEncoding = DataEncoding.Base32;
                    return true;

                default:
                    DataEncoding = DataEncoding.Hex;
                    return false;

            }

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a signature encoding.
        /// </summary>
        /// <param name="Text">A text representation of a signature encoding.</param>
        public static DataEncoding? TryParse(String Text)

            => TryParse(Text, out var encoding)
                   ? encoding
                   : null;

        #endregion

        #region AsText  (this DataEncoding)

        /// <summary>
        /// The wire representation of the given signature encoding.
        /// </summary>
        /// <param name="DataEncoding">A signature encoding.</param>
        public static String AsText(this DataEncoding DataEncoding)

            => DataEncoding switch {
                   DataEncoding.Base64  => "base64",
                   DataEncoding.Base32  => "base32",
                   _                    => "hex"
               };

        #endregion

    }


    /// <summary>
    /// How the two integers of an ECDSA signature are serialized.
    /// </summary>
    public enum SignatureFormat
    {

        /// <summary>ASN.1 DER.</summary>
        DER,

        /// <summary>The bare r and s values.</summary>
        RS

    }


    /// <summary>
    /// Extension methods for signature formats.
    /// </summary>
    public static class SignatureFormatExtensions
    {

        #region TryParse(Text, out SignatureFormat)

        /// <summary>
        /// Try to parse the given text as a signature format.
        /// </summary>
        /// <param name="Text">A text representation of a signature format.</param>
        /// <param name="SignatureFormat">The parsed signature format.</param>
        public static Boolean TryParse(String Text, out SignatureFormat SignatureFormat)
        {

            switch (Text.Trim().ToUpperInvariant())
            {

                case "DER":
                    SignatureFormat = SignatureFormat.DER;
                    return true;

                case "RS":
                    SignatureFormat = SignatureFormat.RS;
                    return true;

                default:
                    SignatureFormat = SignatureFormat.DER;
                    return false;

            }

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a signature format.
        /// </summary>
        /// <param name="Text">A text representation of a signature format.</param>
        public static SignatureFormat? TryParse(String Text)

            => TryParse(Text, out var format)
                   ? format
                   : null;

        #endregion

        #region AsText  (this SignatureFormat)

        /// <summary>
        /// The wire representation of the given signature format.
        /// </summary>
        /// <param name="SignatureFormat">A signature format.</param>
        public static String AsText(this SignatureFormat SignatureFormat)

            => SignatureFormat.ToString();

        #endregion

    }


    /// <summary>
    /// The family of signature algorithm.
    /// </summary>
    public enum CryptoAlgorithm
    {

        /// <summary>RSA.</summary>
        RSA,

        /// <summary>Elliptic curve cryptography.</summary>
        ECC

    }


    /// <summary>
    /// Extension methods for signature algorithms.
    /// </summary>
    public static class CryptoAlgorithmExtensions
    {

        #region TryParse(Text, out CryptoAlgorithm)

        /// <summary>
        /// Try to parse the given text as a signature algorithm.
        /// </summary>
        /// <param name="Text">A text representation of a signature algorithm.</param>
        /// <param name="CryptoAlgorithm">The parsed signature algorithm.</param>
        public static Boolean TryParse(String Text, out CryptoAlgorithm CryptoAlgorithm)
        {

            switch (Text.Trim().ToUpperInvariant())
            {

                case "RSA":
                    CryptoAlgorithm = CryptoAlgorithm.RSA;
                    return true;

                case "ECC":
                    CryptoAlgorithm = CryptoAlgorithm.ECC;
                    return true;

                default:
                    CryptoAlgorithm = CryptoAlgorithm.ECC;
                    return false;

            }

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a signature algorithm.
        /// </summary>
        /// <param name="Text">A text representation of a signature algorithm.</param>
        public static CryptoAlgorithm? TryParse(String Text)

            => TryParse(Text, out var algorithm)
                   ? algorithm
                   : null;

        #endregion

        #region AsText  (this CryptoAlgorithm)

        /// <summary>
        /// The wire representation of the given signature algorithm.
        /// </summary>
        /// <param name="CryptoAlgorithm">A signature algorithm.</param>
        public static String AsText(this CryptoAlgorithm CryptoAlgorithm)

            => CryptoAlgorithm.ToString();

        #endregion

    }


    /// <summary>
    /// The hash algorithm a signature is computed over.
    /// </summary>
    public enum CryptoHashAlgorithm
    {

        /// <summary>SHA-256.</summary>
        SHA256,

        /// <summary>SHA-384.</summary>
        SHA384,

        /// <summary>SHA-512.</summary>
        SHA512

    }


    /// <summary>
    /// Extension methods for hash algorithms.
    /// </summary>
    public static class CryptoHashAlgorithmExtensions
    {

        #region TryParse(Text, out CryptoHashAlgorithm)

        /// <summary>
        /// Try to parse the given text as a hash algorithm.
        /// Both "SHA256" and "SHA-256" are accepted.
        /// </summary>
        /// <param name="Text">A text representation of a hash algorithm.</param>
        /// <param name="CryptoHashAlgorithm">The parsed hash algorithm.</param>
        public static Boolean TryParse(String Text, out CryptoHashAlgorithm CryptoHashAlgorithm)
        {

            switch (Text.Trim().ToUpperInvariant().Replace("-", ""))
            {

                case "SHA256":
                    CryptoHashAlgorithm = CryptoHashAlgorithm.SHA256;
                    return true;

                case "SHA384":
                    CryptoHashAlgorithm = CryptoHashAlgorithm.SHA384;
                    return true;

                case "SHA512":
                    CryptoHashAlgorithm = CryptoHashAlgorithm.SHA512;
                    return true;

                default:
                    CryptoHashAlgorithm = CryptoHashAlgorithm.SHA256;
                    return false;

            }

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a hash algorithm.
        /// </summary>
        /// <param name="Text">A text representation of a hash algorithm.</param>
        public static CryptoHashAlgorithm? TryParse(String Text)

            => TryParse(Text, out var hash)
                   ? hash
                   : null;

        #endregion

        #region AsText  (this CryptoHashAlgorithm)

        /// <summary>
        /// The wire representation of the given hash algorithm.
        /// </summary>
        /// <param name="CryptoHashAlgorithm">A hash algorithm.</param>
        public static String AsText(this CryptoHashAlgorithm CryptoHashAlgorithm)

            => CryptoHashAlgorithm.ToString();

        #endregion

    }

}
