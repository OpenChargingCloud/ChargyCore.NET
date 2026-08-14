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
    /// A collection of public keys handed to Chargy alongside a charge transparency
    /// record, e.g. a public key file an operator publishes separately from the
    /// charging data itself.
    /// </summary>
    /// <param name="PublicKeys">The public keys.</param>
    /// <param name="Status">An optional verification status.</param>
    public class PublicKeyLookup(IEnumerable<PublicKey>      PublicKeys,
                                 SessionVerificationResult?  Status = null)
    {

        #region Properties

        /// <summary>The public keys.</summary>
        public IReadOnlyList<PublicKey>     PublicKeys    { get; } = PublicKeys.ToArray();

        /// <summary>An optional verification status.</summary>
        public SessionVerificationResult?   Status        { get; } = Status;

        #endregion


        #region (static) TryParse(JSON, out PublicKeyLookup)

        /// <summary>
        /// Try to parse the given JSON as a public key lookup.
        /// </summary>
        /// <param name="JSON">A JSON representation of a public key lookup.</param>
        /// <param name="PublicKeyLookup">The parsed public key lookup.</param>
        public static Boolean TryParse(JObject JSON, out PublicKeyLookup? PublicKeyLookup)
        {

            PublicKeyLookup = null;

            if (JSON["publicKeys"] is not JArray publicKeyArray)
                return false;

            var publicKeys = new List<PublicKey>();

            foreach (var publicKeyJSON in publicKeyArray.OfType<JObject>())
                if (PublicKey.TryParse(publicKeyJSON, out var publicKey))
                    publicKeys.Add(publicKey!);

            PublicKeyLookup = new PublicKeyLookup(
                                  publicKeys,
                                  SessionVerificationResultExtensions.TryParse(JSON["status"]?.Value<String>() ?? "")
                              );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this public key lookup.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("publicKeys", new JArray(PublicKeys.Select(publicKey => publicKey.ToJSON())))
                       );

            if (Status.HasValue)
                json.Add(new JProperty("status", Status.Value.AsText()));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this public key lookup.
        /// </summary>
        public override String ToString()

            => $"{PublicKeys.Count} public key(s)";

        #endregion


    }

}
