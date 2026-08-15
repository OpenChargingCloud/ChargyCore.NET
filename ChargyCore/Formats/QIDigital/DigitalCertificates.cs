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

namespace cloud.charging.open.chargy.Formats.QIDigital
{

    /// <summary>
    /// The signatures a QI-Digital certificate carries.
    ///
    /// All three certificate kinds sign themselves the same way, so reading and
    /// writing that list lives in one place rather than three.
    /// </summary>
    public static class QIDigitalSignatures
    {

        #region Parse(JSON)

        /// <summary>
        /// The signatures a certificate carries.
        /// </summary>
        /// <param name="JSON">A JSON representation of a certificate.</param>
        public static IEnumerable<SignatureRS> Parse(JObject JSON)
        {

            var signatures = new List<SignatureRS>();

            if (JSON["signatures"] is JArray array)
                foreach (var element in array.OfType<JObject>())
                    if (Signature.TryParse(element, out var signature) &&
                        signature is SignatureRS signatureRS)
                    {
                        signatures.Add(signatureRS);
                    }

            return signatures;

        }

        #endregion

        #region AddTo(JSON, Signatures)

        /// <summary>
        /// Add the signatures to a certificate's JSON.
        /// </summary>
        /// <param name="JSON">The JSON of a certificate.</param>
        /// <param name="Signatures">The signatures it carries.</param>
        public static void AddTo(JObject                     JSON,
                                 IEnumerable<SignatureRS>    Signatures)
        {

            var signatures = Signatures.ToArray();

            if (signatures.Length > 0)
                JSON.Add(new JProperty("signatures", new JArray(signatures.Select(signature => signature.ToJSON()))));

        }

        #endregion

    }


    /// <summary>
    /// A Digital Certificate of Accreditation: the statement that a calibration
    /// laboratory is competent to issue the certificates it issues.
    ///
    /// One link further up the chain than a
    /// <see cref="DigitalCalibrationCertificate"/> — a calibration is only worth
    /// something if somebody accredited the laboratory that performed it.
    /// </summary>
    /// <param name="Id">The identification of the certificate.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Signatures">Optional digital signatures over the certificate.</param>
    public class CertificateOfAccreditation(String                     Id,
                                            IEnumerable<String>?       Context     = null,
                                            IEnumerable<SignatureRS>?  Signatures  = null) : IDCCElement
    {

        #region Properties

        /// <summary>The identification of the certificate.</summary>
        public String                      Id               { get; } = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>       JSONLDContext    { get; } = Context?.   ToArray() ?? [];

        /// <summary>Optional digital signatures over the certificate.</summary>
        public IReadOnlyList<SignatureRS>  Signatures       { get; } = Signatures?.ToArray() ?? [];

        #endregion

        #region (static) TryParse(JSON, out Certificate)

        /// <summary>
        /// Try to parse the given JSON as a certificate of accreditation.
        /// </summary>
        /// <param name="JSON">A JSON representation of a certificate.</param>
        /// <param name="Certificate">The parsed certificate.</param>
        public static Boolean TryParse(JObject JSON, out CertificateOfAccreditation? Certificate)
        {

            Certificate = null;

            var id = DCC.Text(JSON, "@id");

            if (id is null || id.Length == 0)
                return false;

            Certificate = new CertificateOfAccreditation(
                              id,
                              PublicKey.ParseContext(JSON["@context"]),
                              QIDigitalSignatures.Parse(JSON)
                          );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this certificate.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(new JProperty("@id", Id));

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context", JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context", new JArray(JSONLDContext)));

            QIDigitalSignatures.AddTo(json, Signatures);

            return json;

        }

        #endregion

        /// <summary>Return a text representation of this certificate.</summary>
        public override String ToString()
            => Id;

    }


    /// <summary>
    /// A Digital Certificate of Compliance: the statement that a device conforms
    /// to the rules it has to conform to.
    ///
    /// For a charging station under German Calibration Law, this is the conformity
    /// assessment — and the thing an EV driver is implicitly relying on when they
    /// accept a meter reading as binding.
    /// </summary>
    /// <param name="Id">The identification of the certificate.</param>
    /// <param name="Context">An optional JSON-LD context.</param>
    /// <param name="Signatures">Optional digital signatures over the certificate.</param>
    public class DigitalCertificateOfCompliance(String                     Id,
                                                IEnumerable<String>?       Context     = null,
                                                IEnumerable<SignatureRS>?  Signatures  = null) : IDCCElement
    {

        #region Properties

        /// <summary>The identification of the certificate.</summary>
        public String                      Id               { get; } = Id;

        /// <summary>An optional JSON-LD context.</summary>
        public IReadOnlyList<String>       JSONLDContext    { get; } = Context?.   ToArray() ?? [];

        /// <summary>Optional digital signatures over the certificate.</summary>
        public IReadOnlyList<SignatureRS>  Signatures       { get; } = Signatures?.ToArray() ?? [];

        #endregion

        #region (static) TryParse(JSON, out Certificate)

        /// <summary>
        /// Try to parse the given JSON as a certificate of compliance.
        /// </summary>
        /// <param name="JSON">A JSON representation of a certificate.</param>
        /// <param name="Certificate">The parsed certificate.</param>
        public static Boolean TryParse(JObject JSON, out DigitalCertificateOfCompliance? Certificate)
        {

            Certificate = null;

            var id = DCC.Text(JSON, "@id");

            if (id is null || id.Length == 0)
                return false;

            Certificate = new DigitalCertificateOfCompliance(
                              id,
                              PublicKey.ParseContext(JSON["@context"]),
                              QIDigitalSignatures.Parse(JSON)
                          );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this certificate.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(new JProperty("@id", Id));

            if (JSONLDContext.Count == 1)
                json.Add(new JProperty("@context", JSONLDContext[0]));

            else if (JSONLDContext.Count > 1)
                json.Add(new JProperty("@context", new JArray(JSONLDContext)));

            QIDigitalSignatures.AddTo(json, Signatures);

            return json;

        }

        #endregion

        /// <summary>Return a text representation of this certificate.</summary>
        public override String ToString()
            => Id;

    }

}
