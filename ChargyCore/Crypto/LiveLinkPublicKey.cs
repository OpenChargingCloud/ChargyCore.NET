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

using System.Buffers.Text;

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.EdEC;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.chargy.Crypto
{

    /// <summary>
    /// A public key as a charge transparency live link lists it: a value, and the
    /// "encodings" pipeline that says how it was put into text, innermost first —
    /// [ "SubjectPublicKeyInfo", "DER", "hex" ], [ "raw", "hex" ],
    /// [ "COSE_Key", "CBOR", "base64url" ] and so on.
    ///
    /// The verifiers take a key in one of two shapes only: an elliptic curve key
    /// as a DER SubjectPublicKeyInfo, which names its curve by object identifier,
    /// and an EdDSA or ML-DSA key as its raw octets. This turns whatever the
    /// document chose into that shape, or says why it cannot.
    ///
    /// Saying why is the point. A key that cannot be read is a different
    /// statement from a signature that does not match, and a key that is
    /// quietly left out turns the one into the other: the readings it signed
    /// then fail against the remaining keys, and an EV driver is told their
    /// session does not verify when nobody actually looked.
    ///
    /// A COSE key can only name its curve through the IANA "COSE Elliptic
    /// Curves" registry, and secp192r1, secp192k1 and secp224k1 — the curves of
    /// the EMH, GDF, Alfen and legacy ChargePoint meters and of the OCMF secp192
    /// algorithms — are not in it. Such a key has no COSE form; it travels as a
    /// SubjectPublicKeyInfo, where the curve is an object identifier and every
    /// curve has one.
    /// </summary>
    public sealed class LiveLinkPublicKey
    {

        #region Data

        /// <summary>
        /// Why a COSE key cannot be used: its curve has no identifier in the
        /// IANA "COSE Elliptic Curves" registry, so it names none, or one that
        /// only its author can interpret.
        /// </summary>
        public const String COSECurveNotExpressible   = "Verification_COSEKeyCurveNotExpressible";

        /// <summary>
        /// Why a key cannot be used: its encodings pipeline is not one this
        /// library reads.
        /// </summary>
        public const String UnsupportedEncoding       = "UnknownOrInvalidPublicKeyFormat";

        /// <summary>
        /// Why a key cannot be used: its encodings pipeline is understood, but
        /// the value does not decode under it.
        /// </summary>
        public const String DecodingFailed            = "Verification_PublicKeyDecodingFailed";

        /// <summary>
        /// The lowest identifier of the IANA "COSE Elliptic Curves" registry that
        /// is not private use. Below it, a curve means whatever its author
        /// decided, which no one else can know.
        /// </summary>
        private const Int32 LowestNonPrivateUseCurve  = -65536;

        #endregion

        #region Properties

        /// <summary>The value, as the document lists it.</summary>
        public String                 Value        { get; }

        /// <summary>How the value was encoded, innermost first.</summary>
        public IReadOnlyList<String>  Encodings    { get; }

        /// <summary>The key type the document states, e.g. "ECDSA-secp256r1".</summary>
        public String?                Algorithm    { get; }

        /// <summary>
        /// The key as the verifiers take it, hexadecimal: a DER
        /// SubjectPublicKeyInfo for an elliptic curve key, the raw octets for an
        /// EdDSA or ML-DSA key. Null when the key cannot be used.
        /// </summary>
        public String?                KeyHEX       { get; }

        /// <summary>
        /// What a reading that only this key could have verified comes to:
        /// UnknownPublicKeyFormat or InvalidPublicKey. Null when the key can be used.
        /// </summary>
        public VerificationResult?    Status       { get; }

        /// <summary>The i18n key of the reason the key cannot be used.</summary>
        public String?                ReasonKey    { get; }

        /// <summary>A technical detail of the reason, deliberately not localized.</summary>
        public String?                Details      { get; }

        /// <summary>Whether this key can be handed to a verifier.</summary>
        public Boolean                IsUsable
            => KeyHEX is not null;

        #endregion

        #region Constructor(s)

        private LiveLinkPublicKey(String                 Value,
                                  IReadOnlyList<String>  Encodings,
                                  String?                Algorithm,
                                  String?                KeyHEX,
                                  VerificationResult?    Status     = null,
                                  String?                ReasonKey  = null,
                                  String?                Details    = null)
        {

            this.Value      = Value;
            this.Encodings  = Encodings;
            this.Algorithm  = Algorithm;
            this.KeyHEX     = KeyHEX;
            this.Status     = Status;
            this.ReasonKey  = ReasonKey;
            this.Details    = Details;

        }

        #endregion


        #region (static) Read(Entry)

        /// <summary>
        /// Read one entry of a live link's "publicKeys".
        /// </summary>
        /// <param name="Entry">An entry with a "value", and optionally "encodings" and "algorithm".</param>
        /// <returns>Null when the entry has no value at all.</returns>
        public static LiveLinkPublicKey? Read(JObject Entry)
        {

            if (Entry["value"]?.Type != JTokenType.String ||
                Entry["value"]!.Value<String>() is not String value ||
                value.Length == 0)
            {
                return null;
            }

            // An entry of another type than a list states no pipeline, exactly as
            // one without the property does.
            var encodings = Entry["encodings"] is JArray array
                                ? array.Select(step => step.Type == JTokenType.String ? step.Value<String>() ?? "" : "")
                                : null;

            return Read(
                       value,
                       encodings,
                       Entry["algorithm"]?.Type == JTokenType.String
                           ? Entry["algorithm"]!.Value<String>()
                           : null
                   );

        }

        #endregion

        #region (static) Read(Value, Encodings, Algorithm = null)

        /// <summary>
        /// Read a public key given as a value and its encodings pipeline.
        /// </summary>
        /// <param name="Value">The value, as the document lists it.</param>
        /// <param name="Encodings">How the value was encoded, innermost first, if stated.</param>
        /// <param name="Algorithm">The key type the document states, e.g. "ECDSA-secp256r1".</param>
        public static LiveLinkPublicKey Read(String                Value,
                                             IEnumerable<String>?  Encodings,
                                             String?               Algorithm = null)
        {

            String[] pipeline = [ .. Encodings ?? [] ];

            #region No pipeline: passed on as it is

            // The verifiers work out the encoding of a key nobody described, as
            // they do for a key from any other source.
            if (pipeline.Length == 0)
                return new (Value, pipeline, Algorithm, Value);

            #endregion

            #region The text the bytes were put into

            // PEM is base64 inside armor, and the pipeline says so by naming both.
            var textSteps  = pipeline is [ .., "base64", "PEM" ] ? 2 : 1;
            var text       = pipeline[^1];

            Byte[]? bytes;

            try
            {
                bytes = text switch {
                            "hex"        => Convert.FromHexString(Value),
                            "base64"     => Convert.FromBase64String(Value),
                            "base64url"  => Base64Url.DecodeFromChars(Value),
                            "PEM"        => FromPEM(Value),
                            _            => null
                        };
            }
            catch (Exception exception)
            {
                return Unusable(Value, pipeline, Algorithm, VerificationResult.InvalidPublicKey, DecodingFailed,
                                $"The value is not valid {text}: {exception.Message}");
            }

            if (bytes is null)
                return Unusable(Value, pipeline, Algorithm, VerificationResult.UnknownPublicKeyFormat, UnsupportedEncoding,
                                $"The encodings [ {String.Join(", ", pipeline)} ] end in a text encoding this library does not read.");

            #endregion

            #region The structure inside those bytes

            String[] structure = pipeline[..^textSteps];

            return structure switch {

                       [ "COSE_Key" ] or [ "COSE_Key", "CBOR" ]  => FromCOSEKey(Value, pipeline, Algorithm, bytes),

                       [ "ECPoint" ]                             => FromECPoint(Value, pipeline, Algorithm, bytes),

                       // An EdDSA or ML-DSA key is verified as its raw octets, and
                       // its SubjectPublicKeyInfo wraps exactly those.
                       [ "SubjectPublicKeyInfo", "DER" ] when PublicKeyParser.TryParseDER(bytes) is ParsedPublicKey { KeyType: not "ECC" } parsed
                                                                 => new (Value, pipeline, Algorithm, Convert.ToHexString(parsed.Value)),

                       // An elliptic curve SubjectPublicKeyInfo, a raw key, or a
                       // structure nobody named: the verifiers take these bytes as
                       // they are, and say themselves what they make of them. A
                       // hexadecimal value is passed on untouched, which is what
                       // it always was.
                       _                                         => new (Value, pipeline, Algorithm,
                                                                         text == "hex" ? Value : Convert.ToHexString(bytes))

                   };

            #endregion

        }

        #endregion


        #region ToError(I18N, Level = SeverityLevel.High)

        /// <summary>
        /// Why this key cannot be used, as an error a verification result can carry.
        /// </summary>
        /// <param name="I18N">The dictionary used to describe what went wrong.</param>
        /// <param name="Level">How severe it is where it is reported.</param>
        public Error? ToError(I18NDictionary  I18N,
                              SeverityLevel   Level = SeverityLevel.High)

            => ReasonKey is not null
                   ? new Error(
                         I18N.GetMultilanguageText(ReasonKey),
                         Level,
                         ReasonKey,
                         Details
                     )
                   : null;

        #endregion


        #region (private, static) FromCOSEKey(Value, Pipeline, Algorithm, CBOR)

        /// <summary>
        /// A COSE key [RFC 9052, Section 7], in the shape the verifiers take.
        ///
        /// Only a curve of the IANA registry can be named here, which is exactly
        /// the limitation the type describes: a missing, unregistered or
        /// private-use "crv" is a key whose curve this form cannot express, not
        /// a broken key, and it is reported as such.
        /// </summary>
        private static LiveLinkPublicKey FromCOSEKey(String    Value,
                                                     String[]  Pipeline,
                                                     String?   Algorithm,
                                                     Byte[]    CBOR)
        {

            if (!COSEKey.TryParse(CBOR, out var coseKey, out var errorResponse))
                return Unusable(Value, Pipeline, Algorithm, VerificationResult.InvalidPublicKey, DecodingFailed, errorResponse);

            #region A curve key has to name a curve the registry knows

            if (coseKey.KeyType is COSEKeyType.EC2 or COSEKeyType.OKP)
            {

                if (coseKey.Curve is not COSECurve curve)
                    return Unusable(Value, Pipeline, Algorithm, VerificationResult.UnknownPublicKeyFormat, COSECurveNotExpressible,
                                    "The COSE key names no curve (crv). Give the key as a SubjectPublicKeyInfo, which names every curve by its object identifier.");

                if (curve.Value < LowestNonPrivateUseCurve)
                    return Unusable(Value, Pipeline, Algorithm, VerificationResult.UnknownPublicKeyFormat, COSECurveNotExpressible,
                                    $"crv {curve.Value} is a private-use value of the IANA \"COSE Elliptic Curves\" registry, which only its author can interpret. Give the key as a SubjectPublicKeyInfo.");

                if (!curve.IsKnown)
                    return Unusable(Value, Pipeline, Algorithm, VerificationResult.UnknownPublicKeyFormat, COSECurveNotExpressible,
                                    $"crv {curve.Value} is not registered in the IANA \"COSE Elliptic Curves\" registry. Give the key as a SubjectPublicKeyInfo.");

            }

            #endregion

            if (!coseKey.TryToPublicKey(out var publicKey, out errorResponse))
                return Unusable(Value, Pipeline, Algorithm, VerificationResult.InvalidPublicKey, DecodingFailed, errorResponse);

            return publicKey switch {

                       ECPublicKeyParameters ecKey
                           => FromCurvePoint(Value, Pipeline, Algorithm, coseKey.Curve!.Value.Name, ecKey.Q.GetEncoded(false)),

                       Ed25519PublicKeyParameters ed25519Key
                           => new (Value, Pipeline, Algorithm, Convert.ToHexString(ed25519Key.GetEncoded())),

                       Ed448PublicKeyParameters ed448Key
                           => new (Value, Pipeline, Algorithm, Convert.ToHexString(ed448Key.GetEncoded())),

                       MLDsaPublicKeyParameters mlDsaKey
                           => new (Value, Pipeline, Algorithm, Convert.ToHexString(mlDsaKey.GetEncoded())),

                       _   => Unusable(Value, Pipeline, Algorithm, VerificationResult.UnknownPublicKeyFormat, UnsupportedEncoding,
                                       $"A COSE key of key type {coseKey.KeyType} is not a signature key this library verifies with.")

                   };

        }

        #endregion

        #region (private, static) FromECPoint(Value, Pipeline, Algorithm, Point)

        /// <summary>
        /// A bare elliptic curve point [SEC 1, Section 2.3.3]. It carries no curve
        /// of its own, so the stated algorithm has to name one.
        /// </summary>
        private static LiveLinkPublicKey FromECPoint(String    Value,
                                                     String[]  Pipeline,
                                                     String?   Algorithm,
                                                     Byte[]    Point)
        {

            var curveName = Algorithm is not null && Algorithm.StartsWith("ECDSA-", StringComparison.OrdinalIgnoreCase)
                                ? Algorithm["ECDSA-".Length..]
                                : Algorithm;

            if (ECCurveVerifier.TryGet(curveName) is not ECCurveVerifier curve)
                return Unusable(Value, Pipeline, Algorithm, VerificationResult.UnknownPublicKeyFormat, UnsupportedEncoding,
                                $"An ECPoint does not name its curve, and the algorithm '{Algorithm}' names none this library knows.");

            return FromCurvePoint(Value, Pipeline, Algorithm, curve.CurveName, Point);

        }

        #endregion

        #region (private, static) FromCurvePoint(Value, Pipeline, Algorithm, CurveName, Point)

        /// <summary>
        /// An elliptic curve point wrapped into the DER SubjectPublicKeyInfo the
        /// verifiers take, with the curve named by its object identifier.
        ///
        /// Named, not spelled out: a SubjectPublicKeyInfo that carries the curve
        /// parameters inline is valid ASN.1, but it is not what any energy meter
        /// emits, and not what the key parser reads.
        /// </summary>
        private static LiveLinkPublicKey FromCurvePoint(String    Value,
                                                        String[]  Pipeline,
                                                        String?   Algorithm,
                                                        String    CurveName,
                                                        Byte[]    Point)
        {

            // NIST and COSE call it "P-256", BouncyCastle files it as secp256r1.
            var curveOID = ECNamedCurveTable.GetOid(ECCurveVerifier.TryGet(CurveName)?.CurveName ?? CurveName);

            if (curveOID is null)
                return Unusable(Value, Pipeline, Algorithm, VerificationResult.UnknownPublicKeyFormat, UnsupportedEncoding,
                                $"The curve '{CurveName}' has no object identifier this library knows.");

            var subjectPublicKeyInfo = new SubjectPublicKeyInfo(
                                           new AlgorithmIdentifier(X9ObjectIdentifiers.IdECPublicKey, curveOID),
                                           Point
                                       );

            return new (Value, Pipeline, Algorithm, Convert.ToHexString(subjectPublicKeyInfo.GetDerEncoded()));

        }

        #endregion

        #region (internal, static) SubjectPublicKeyInfoOf(RawKey, Algorithm)

        /// <summary>
        /// The DER SubjectPublicKeyInfo of a key stored as its bare key material,
        /// or null when the stated algorithm names nothing that has one here.
        ///
        /// A key id is defined over the SubjectPublicKeyInfo whatever form a
        /// document stores the key in, so a raw key — as Ed25519 keys usually
        /// are — has to be wrapped before its id can be computed. The algorithm
        /// says what to wrap it with, which is why a document states it next to
        /// every key.
        /// </summary>
        /// <param name="RawKey">The bare key material: a SEC1 point, or the raw octets of an EdDSA or ML-DSA key.</param>
        /// <param name="Algorithm">The key type the document states, e.g. "EdDSA-Ed25519".</param>
        internal static Byte[]? SubjectPublicKeyInfoOf(Byte[]   RawKey,
                                                       String?  Algorithm)
        {

            var algorithmOID = Algorithm switch {
                                   "EdDSA-Ed25519"  => EdECObjectIdentifiers.id_Ed25519,
                                   "EdDSA-Ed448"    => EdECObjectIdentifiers.id_Ed448,
                                   "ML-DSA-44"      => new DerObjectIdentifier("2.16.840.1.101.3.4.3.17"),
                                   "ML-DSA-65"      => new DerObjectIdentifier("2.16.840.1.101.3.4.3.18"),
                                   "ML-DSA-87"      => new DerObjectIdentifier("2.16.840.1.101.3.4.3.19"),
                                   _                => null
                               };

            if (algorithmOID is not null)
                return new SubjectPublicKeyInfo(new AlgorithmIdentifier(algorithmOID), RawKey).GetDerEncoded();

            return FromECPoint("", [], Algorithm, RawKey).KeyHEX is String subjectPublicKeyInfo
                       ? Convert.FromHexString(subjectPublicKeyInfo)
                       : null;

        }

        #endregion

        #region (private, static) FromPEM(PEM)

        /// <summary>
        /// The bytes inside PEM armor [RFC 7468].
        /// </summary>
        private static Byte[] FromPEM(String PEM)

            => Convert.FromBase64String(
                   String.Concat(
                       PEM.Split('\n').
                           Select (line => line.Trim()).
                           Where  (line => !line.StartsWith("-----", StringComparison.Ordinal))
                   )
               );

        #endregion

        #region (private, static) Unusable(Value, Pipeline, Algorithm, Status, ReasonKey, Details)

        private static LiveLinkPublicKey Unusable(String              Value,
                                                  String[]            Pipeline,
                                                  String?             Algorithm,
                                                  VerificationResult  Status,
                                                  String              ReasonKey,
                                                  String              Details)

            => new (Value, Pipeline, Algorithm, null, Status, ReasonKey, Details);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this public key.
        /// </summary>
        public override String ToString()

            => $"[ {String.Join(", ", Encodings)} ] {(IsUsable ? KeyHEX : $"{Status}: {Details}")}";

        #endregion

    }

}
