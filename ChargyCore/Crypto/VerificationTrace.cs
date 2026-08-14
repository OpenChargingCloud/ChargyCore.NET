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

namespace cloud.charging.open.chargy.Crypto
{

    /// <summary>
    /// One field of a signed buffer: what it means, what it says, and which bytes
    /// it occupies.
    /// </summary>
    /// <param name="Id">
    /// The name of the field. Either a literal label or an i18n message key,
    /// depending on <paramref name="IsLocalizable"/>.
    /// </param>
    /// <param name="Value">The value of the field, as shown to a user.</param>
    /// <param name="ValueHEX">The bytes of the field within the signed buffer, hexadecimal.</param>
    /// <param name="IsLocalizable">Whether <paramref name="Id"/> is an i18n message key.</param>
    public readonly record struct TraceLine(String   Id,
                                            String   Value,
                                            String   ValueHEX,
                                            Boolean  IsLocalizable = false)
    {

        /// <summary>Return a text representation of this trace line.</summary>
        public override String ToString()

            => $"{Id}: {Value} ({ValueHEX})";

    }


    /// <summary>
    /// Everything a user interface needs to show <em>why</em> a measurement did or
    /// did not verify: which field of the signed buffer sits at which bytes, the
    /// buffer itself, its hash, the public key and the signature.
    ///
    /// This replaces the ViewMeasurement() methods of ChargyCore.TS, which wrote
    /// the same information straight into HTML elements. Keeping it as data means
    /// the verification logic stays usable on a server, in a test, and in a GUI
    /// alike — and the GUI decides how to render it.
    /// </summary>
    public class VerificationTrace
    {

        #region Data

        private readonly List<TraceLine> lines = [];

        #endregion

        #region Properties

        /// <summary>The fields of the signed buffer, in the order they appear in it.</summary>
        public IReadOnlyList<TraceLine>  Lines
            => lines;

        /// <summary>The buffer the signature was computed over.</summary>
        public Byte[]                    SignedBuffer         { get; internal set; } = [];

        /// <summary>The hash of the signed buffer.</summary>
        public Byte[]                    HashedBuffer         { get; internal set; } = [];

        /// <summary>The public key the signature was checked against.</summary>
        public Byte[]                    PublicKey            { get; internal set; } = [];

        /// <summary>The signature found in the charge transparency record.</summary>
        public Byte[]                    Signature            { get; internal set; } = [];

        /// <summary>An optional introduction, e.g. the name of the data format.</summary>
        public String?                   Description          { get; internal set; }

        /// <summary>The result of the verification this trace belongs to.</summary>
        public CryptoResult?             Result               { get; internal set; }

        /// <summary>The signed buffer as a hexadecimal string.</summary>
        public String                    SignedBufferHEX
            => Convert.ToHexStringLower(SignedBuffer);

        /// <summary>The hash of the signed buffer as a hexadecimal string.</summary>
        public String                    HashedBufferHEX
            => Convert.ToHexStringLower(HashedBuffer);

        /// <summary>The public key as a hexadecimal string.</summary>
        public String                    PublicKeyHEX
            => Convert.ToHexStringLower(PublicKey);

        /// <summary>The signature as a hexadecimal string.</summary>
        public String                    SignatureHEX
            => Convert.ToHexStringLower(Signature);

        #endregion


        #region Add          (Id, Value, ValueHEX)

        /// <summary>
        /// Add a field with a literal label.
        /// </summary>
        /// <param name="Id">The name of the field.</param>
        /// <param name="Value">The value of the field.</param>
        /// <param name="ValueHEX">The bytes of the field, hexadecimal.</param>
        public VerificationTrace Add(String  Id,
                                     String  Value,
                                     String  ValueHEX)
        {
            lines.Add(new TraceLine(Id, Value, ValueHEX));
            return this;
        }

        #endregion

        #region AddLocalized (MessageKey, Value, ValueHEX)

        /// <summary>
        /// Add a field whose label is an i18n message key.
        /// </summary>
        /// <param name="MessageKey">The i18n key of the field name.</param>
        /// <param name="Value">The value of the field.</param>
        /// <param name="ValueHEX">The bytes of the field, hexadecimal.</param>
        public VerificationTrace AddLocalized(String  MessageKey,
                                              String  Value,
                                              String  ValueHEX)
        {
            lines.Add(new TraceLine(MessageKey, Value, ValueHEX, IsLocalizable: true));
            return this;
        }

        #endregion


        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this verification trace.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("lines", new JArray(
                               lines.Select(line => new JObject(
                                   new JProperty("id",             line.Id),
                                   new JProperty("value",          line.Value),
                                   new JProperty("valueHEX",       line.ValueHEX),
                                   new JProperty("isLocalizable",  line.IsLocalizable)
                               ))
                           ))
                       );

            if (Description        is not null)
                json.Add(new JProperty("description",    Description));

            if (SignedBuffer.Length > 0)
                json.Add(new JProperty("signedBuffer",   SignedBufferHEX));

            if (HashedBuffer.Length > 0)
                json.Add(new JProperty("hashedBuffer",   HashedBufferHEX));

            if (PublicKey.   Length > 0)
                json.Add(new JProperty("publicKey",      PublicKeyHEX));

            if (Signature.   Length > 0)
                json.Add(new JProperty("signature",      SignatureHEX));

            if (Result             is not null)
                json.Add(new JProperty("result",         Result.ToJSON()));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this verification trace.
        /// </summary>
        public override String ToString()

            => $"{lines.Count} field(s), {SignedBuffer.Length} byte(s): {Result?.Status.AsText() ?? "unverified"}";

        #endregion


    }

}
