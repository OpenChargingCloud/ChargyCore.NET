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

using System.Security.Cryptography;
using System.Text;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Signs OCMF payloads the way an energy meter would, for the curves that
    /// have no fixture to test against.
    ///
    /// Shared rather than copied into each test class on purpose. The two details
    /// below are easy to get subtly wrong, and a second copy that got one of them
    /// wrong would still pass its own tests while proving something else than the
    /// first copy proves.
    /// </summary>
    public static class OCMFTestSigner
    {

        #region Sign(CurveName, Payload, HashName = "SHA256")

        /// <summary>
        /// Sign an OCMF payload on the given curve: the public key as a DER
        /// SubjectPublicKeyInfo, the signature as DER over the digest the
        /// algorithm name prescribes.
        /// </summary>
        /// <param name="CurveName">The name of an elliptic curve, as BouncyCastle spells it.</param>
        /// <param name="Payload">The raw OCMF payload.</param>
        /// <param name="HashName">The digest the OCMF algorithm names, "SHA256" or "SHA384".</param>
        public static (String PublicKeyHEX, String SignatureHEX) Sign(String  CurveName,
                                                                      String  Payload,
                                                                      String  HashName = "SHA256")
        {

            var curveOID    = ECNamedCurveTable.GetOid(CurveName)
                                  ?? throw new InvalidOperationException($"BouncyCastle does not know the curve '{CurveName}'!");

            var curve       = ECNamedCurveTable.GetByOid(curveOID);

            // Named domain parameters, not plain ones: a key built from plain
            // parameters is written out with the whole curve spelled out inline,
            // while a real energy meter names the curve by its object identifier.
            // Signing with the wrong one would test a key shape nobody emits.
            var domain      = new ECNamedDomainParameters(curveOID, curve);

            var generator   = new ECKeyPairGenerator();
            generator.Init(new ECKeyGenerationParameters(domain, new SecureRandom()));

            var keyPair     = generator.GenerateKeyPair();

            var signer      = new ECDsaSigner();
            signer.Init(true, new ParametersWithRandom((ECPrivateKeyParameters) keyPair.Private, new SecureRandom()));

            var message     = Encoding.UTF8.GetBytes(Payload);

            var hash        = HashName switch {
                                  "SHA256"  => SHA256.HashData(message),
                                  "SHA384"  => SHA384.HashData(message),
                                  "SHA512"  => SHA512.HashData(message),
                                  _         => throw new ArgumentException($"Unsupported digest '{HashName}'!", nameof(HashName))
                              };

            var signature   = signer.GenerateSignature(hash);

            var der         = new DerSequence(
                                  new DerInteger(signature[0]),
                                  new DerInteger(signature[1])
                              ).GetDerEncoded();

            var publicKey   = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public).GetDerEncoded();

            return (
                       Convert.ToHexStringLower(publicKey),
                       Convert.ToHexStringLower(der)
                   );

        }

        #endregion

    }

}
