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

namespace cloud.charging.open.chargy.Formats.OCMF
{

    /// <summary>
    /// What an OCMF "SA" value says about how a document was signed.
    ///
    /// OCMF names the curve and the digest in a single string, e.g.
    /// "ECDSA-secp256r1-SHA256". Two of the combinations in the wild pair a
    /// 384 bit curve with SHA-256, which is cryptographically the wrong digest
    /// for that curve — but meters were built that way and signed real charging
    /// sessions with it. Chargy has to keep verifying those exactly as they were
    /// signed, so this must never be "corrected".
    /// </summary>
    /// <param name="Name">The OCMF name of the algorithm.</param>
    /// <param name="CurveName">The name of the elliptic curve, for the ECDSA algorithms, as its defining standard spells it.</param>
    /// <param name="HashName">The digest applied before signing, or null when the message is signed directly.</param>
    /// <param name="HashDescription">How the hash is described in a verification trace.</param>
    /// <param name="SignsMessageDirectly">
    /// Whether the algorithm signs the payload itself rather than a digest of it,
    /// as EdDSA and ML-DSA do.
    /// </param>
    /// <param name="SuiteName">
    /// The name this algorithm has in the signature suite registry. OCMF spells
    /// the Edwards curves "EdDSA-Ed25519" while the registry, and everyone else,
    /// calls that one "Ed25519".
    /// </param>
    public class OCMFSignatureAlgorithm(String   Name,
                                        String?  CurveName,
                                        String?  HashName,
                                        String   HashDescription,
                                        Boolean  SignsMessageDirectly = false,
                                        String?  SuiteName            = null)
    {

        #region Properties

        /// <summary>The OCMF name of the algorithm.</summary>
        public String   Name                    { get; } = Name;

        /// <summary>The name of the elliptic curve, for the ECDSA algorithms.</summary>
        public String?  CurveName               { get; } = CurveName;

        /// <summary>The digest applied before signing.</summary>
        public String?  HashName                { get; } = HashName;

        /// <summary>How the hash is described in a verification trace.</summary>
        public String   HashDescription         { get; } = HashDescription;

        /// <summary>Whether the algorithm signs the payload itself rather than a digest of it.</summary>
        public Boolean  SignsMessageDirectly    { get; } = SignsMessageDirectly;

        /// <summary>The name this algorithm has in the signature suite registry.</summary>
        public String   SuiteName               { get; } = SuiteName ?? Name;

        #endregion

        #region Data

        /// <summary>
        /// Every signature algorithm OCMF documents have been seen to use.
        ///
        /// A null curve means Chargy recognises the algorithm but cannot verify
        /// it, which is reported as an unsupported curve rather than as a bad
        /// signature — the difference between "this library cannot check this"
        /// and "this charging session is not what it claims". Only the algorithms
        /// that sign the message directly have a null curve now; every ECDSA entry
        /// names a curve this port can check.
        ///
        /// The two secp192 curves and the two brainpool curves are a deliberate
        /// difference from ChargyCore.TS, which leaves all four unverifiable
        /// because its JavaScript curve library does not carry them. BouncyCastle
        /// does, so this port verifies them rather than declining to look. A
        /// charging session signed on brainpoolP256r1 is checkable here and is
        /// reported as unverifiable there; nothing that verifies in one
        /// implementation fails in the other.
        ///
        /// The curve names are spelled as RFC 5639 and the object identifier
        /// registry spell them, not as OCMF does: OCMF drops the "P" from
        /// "brainpoolP256r1". The name here has to be the one the verifier can
        /// resolve, and it is the one a parsed public key will report as well.
        /// </summary>
        private static readonly OCMFSignatureAlgorithm[] known = [

            // The OCMF standard algorithms.
            new ("ECDSA-secp256r1-SHA256",       "secp256r1",        "SHA256",  "SHA256"),
            new ("ECDSA-secp192k1-SHA256",       "secp192k1",        "SHA256",  "SHA256"),
            new ("ECDSA-secp192r1-SHA256",       "secp192r1",        "SHA256",  "SHA256, 256 Bits, hex"),
            new ("ECDSA-secp256k1-SHA256",       "secp256k1",        "SHA256",  "SHA256, 256 Bits, hex"),
            new ("ECDSA-brainpool256r1-SHA256",  "brainpoolP256r1",  "SHA256",  "SHA256, 256 Bits, hex"),

            // Cryptographically the wrong digest for the curve — but meters were
            // built this way, so their signatures have to keep verifying.
            new ("ECDSA-secp384r1-SHA256",       "secp384r1",        "SHA256",  "SHA256, 256 Bits, hex"),
            new ("ECDSA-brainpool384r1-SHA256",  "brainpoolP384r1",  "SHA256",  "SHA256, 256 Bits, hex"),

            // Not part of the OCMF standard, but in use.
            new ("ECDSA-secp384r1-SHA384",       "secp384r1",        "SHA384",  "SHA384, 384 Bits, hex"),
            new ("ECDSA-brainpool384r1-SHA384",  "brainpoolP384r1",  "SHA384",  "SHA384, 384 Bits, hex"),
            new ("ECDSA-secp521r1-SHA512",       "secp521r1",        "SHA512",  "SHA512, 512 Bits, hex"),

            // These sign the payload itself, with no separate digest step.
            new ("EdDSA-Ed25519",                null,               null,      "none; message signed directly", SignsMessageDirectly: true, SuiteName: "Ed25519"),
            new ("EdDSA-Ed448",                  null,               null,      "none; message signed directly", SignsMessageDirectly: true, SuiteName: "Ed448"),
            new ("ML-DSA-44",                    null,               null,      "none; message signed directly", SignsMessageDirectly: true),
            new ("ML-DSA-65",                    null,               null,      "none; message signed directly", SignsMessageDirectly: true),
            new ("ML-DSA-87",                    null,               null,      "none; message signed directly", SignsMessageDirectly: true)

        ];

        /// <summary>
        /// What an OCMF document is signed with when it does not say.
        /// </summary>
        public static OCMFSignatureAlgorithm Default
            => known[0];

        /// <summary>
        /// Every signature algorithm OCMF documents have been seen to use.
        /// </summary>
        public static IReadOnlyList<OCMFSignatureAlgorithm> All
            => known;

        #endregion


        #region (static) TryGet(SA)

        /// <summary>
        /// The signature algorithm the given OCMF "SA" value names.
        /// </summary>
        /// <param name="SA">An OCMF signature algorithm name, or null.</param>
        public static OCMFSignatureAlgorithm? TryGet(String? SA)
        {

            if (SA is null || SA.Length == 0)
                return Default;

            foreach (var algorithm in known)
                if (algorithm.Name == SA)
                    return algorithm;

            return null;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this signature algorithm.
        /// </summary>
        public override String ToString()

            => Name;

        #endregion


    }

}
