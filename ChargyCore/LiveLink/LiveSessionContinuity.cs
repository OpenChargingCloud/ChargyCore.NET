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
using System.Security.Cryptography;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.Crypto;

#endregion

namespace cloud.charging.open.chargy.LiveLink
{

    /// <summary>
    /// Why a live session document, or an update of one, was not accepted.
    /// </summary>
    /// <param name="Message">What is wrong with it.</param>
    public class LiveSessionException(String Message) : Exception(Message);


    /// <summary>
    /// A live session document as it was read and checked: the original JSON,
    /// who it is about, its signed readings so far, and the reference the next
    /// update has to name.
    /// </summary>
    /// <param name="JSON">The document, as it was read.</param>
    /// <param name="Id">Its "@id".</param>
    /// <param name="EVSEId">The EVSE it is about.</param>
    /// <param name="MeterSerialNumber">The meter of that EVSE.</param>
    /// <param name="Readings">The original OCMF documents received so far, cumulative.</param>
    /// <param name="Reference">The SHA-256 of the canonical document, lowercase hexadecimal: what its successor names in "updates".</param>
    public sealed record LiveSessionDocument(JObject                JSON,
                                             String                 Id,
                                             String                 EVSEId,
                                             String                 MeterSerialNumber,
                                             IReadOnlyList<String>  Readings,
                                             String                 Reference);


    /// <summary>
    /// The operator, station and EVSE of a live session, and the document keys
    /// of that operator — as a roaming network vouches for them, not as the
    /// document lists them.
    /// </summary>
    /// <param name="EVSEId">The EVSE.</param>
    /// <param name="ChargingStationId">Its charging station.</param>
    /// <param name="OperatorId">The charging station operator.</param>
    /// <param name="Keys">The operator's document keys.</param>
    public sealed record LiveCPOAuthority(String                            EVSEId,
                                          String                            ChargingStationId,
                                          String                            OperatorId,
                                          IReadOnlyList<DocumentPublicKey>  Keys);


    /// <summary>
    /// Check that a live session document is the next one of the session it
    /// continues: signed over its whole self, following exactly the document it
    /// names as its predecessor, about the same session, EVSE, meter and
    /// operator, and only ever adding to the signed readings already received.
    ///
    /// The keys a document lists establish continuity with themselves, not
    /// authority: that a document is the operator's own is checked separately,
    /// with keys the caller obtained independently
    /// (<see cref="VerifyLiveSessionAuthority"/>).
    ///
    /// This is a pilot for cumulative OCMF readings.
    /// </summary>
    public static class LiveSessionContinuity
    {

        #region Data

        /// <summary>The JSON-LD context of a live session document.</summary>
        public const  String  JSONLDContext            = "https://open.charging.cloud/contexts/chargeTransparency/live/link/1.0";

        /// <summary>The longest document read, in characters.</summary>
        public const  Int32   MaxDocumentLength        = 4 * 1024 * 1024;

        /// <summary>The most readings a document may hold.</summary>
        public const  Int32   MaxReadings              = 2000;

        /// <summary>The key usage of a key that signs charge transparency documents.</summary>
        public const  String  SignCTRs                 = "signCTRs";

        private static readonly String[] referencePipeline   = [ "SHA-256", "hex" ];

        #endregion


        #region ReadLiveSessionDocument   (Input, Now = null)

        /// <summary>
        /// Read a live session document and check it on its own: its context, its
        /// timestamps, its signatures over the complete document with the
        /// operator keys it lists, and its cumulative original OCMF readings.
        /// </summary>
        /// <param name="Input">The document, as it arrived.</param>
        /// <param name="Now">The time a document may not be younger than.</param>
        /// <exception cref="LiveSessionException">When the document is not acceptable, saying why.</exception>
        public static LiveSessionDocument ReadLiveSessionDocument(String           Input,
                                                                  DateTimeOffset?  Now = null)
        {

            if (Input.Length > MaxDocumentLength)
                throw new LiveSessionException("Invalid live-session input or clock");

            var json = Object(Parse(Input));

            if (json["@context"]?.Type != JTokenType.String || json["@context"]!.Value<String>() != JSONLDContext)
                throw new LiveSessionException("Unsupported live-session context");

            var at       = Timestamp(Text(json["lastUpdated"]));
            var created  = Timestamp(Text(json["created"]));

            if (at is null || created is null || created > at || at > (Now ?? DateTimeOffset.UtcNow))
                throw new LiveSessionException("Invalid/future live-session timestamps");

            var station  = Object(json["chargingStation"]);
            var evse     = Object(station["EVSE"]);
            var meter    = Object(evse["energyMeter"]);
            var cpo      = Object(json["chargingStationOperator"]);
            var keys     = SigningKeysOf(cpo);

            if (json["signatures"] is not JArray signatures || signatures.Count == 0 ||
                signatures.Any(raw => !SameStrings(Object(Object(raw)["signedData"])["excludedProperties"], [ "signatures" ])))
            {
                throw new LiveSessionException("Live pilot requires signatures covering the complete document");
            }

            if (DocumentSignatures.VerifyDocumentSignaturesWithKeys(json, keys).Status != DocumentSignaturesStatus.AllValid)
                throw new LiveSessionException("Live document signature is not valid with the imported CPO document keys");

            var readings = new List<String>();

            if (json["signedMeterValues"] is JToken section)
            {

                var values = Object(section);

                if (!SameStrings(values["encodings"], [ "OCMF" ]) ||
                    values["values"] is not JArray originals ||
                    originals.Any(raw => raw.Type != JTokenType.String || !raw.Value<String>()!.StartsWith("OCMF|", StringComparison.Ordinal)))
                {
                    throw new LiveSessionException("Live pilot requires cumulative original OCMF documents");
                }

                readings.AddRange(originals.Select(raw => raw.Value<String>()!));

            }

            if (readings.Count > MaxReadings || readings.Distinct(StringComparer.Ordinal).Count() != readings.Count)
                throw new LiveSessionException("Duplicate or excessive live readings");

            if (json["docRefIdGeneration"] is JToken pipeline && !SameStrings(pipeline, referencePipeline))
                throw new LiveSessionException("Unsupported live document reference pipeline");

            return new LiveSessionDocument(
                       json,
                       Text(json["@id"]),
                       Text(evse["@id"]),
                       Text(meter["serialNumber"]),
                       readings,
                       Convert.ToHexStringLower(SHA256.HashData(CanonicalJSON.ToUTF8Bytes(json)))
                   );

        }

        #endregion

        #region VerifyLiveSessionTransition(PreviousText, NextText, Now = null, StructureOnly = false)

        /// <summary>
        /// Check that a live session document is the next one after the document
        /// accepted before it, before it replaces that one: both read and checked
        /// on their own, the same session, EVSE, meter and operator, the exact
        /// predecessor named, signed with a key of the predecessor's operator, and
        /// every reading received before still there, in its place.
        /// </summary>
        /// <param name="PreviousText">The document accepted before.</param>
        /// <param name="NextText">The document that would replace it.</param>
        /// <param name="Now">The time a document may not be younger than.</param>
        /// <param name="StructureOnly">
        /// Leave the predecessor's keys out — a preparation for checking the
        /// operator's authority independently, never a verdict of its own.
        /// </param>
        /// <exception cref="LiveSessionException">When the document is not the next one, saying why.</exception>
        public static LiveSessionDocument VerifyLiveSessionTransition(String           PreviousText,
                                                                      String           NextText,
                                                                      DateTimeOffset?  Now            = null,
                                                                      Boolean          StructureOnly  = false)
        {

            var previous = ReadLiveSessionDocument(PreviousText, Now);
            var next     = ReadLiveSessionDocument(NextText,     Now);

            if (previous.Id                != next.Id                ||
                previous.EVSEId            != next.EVSEId            ||
                previous.MeterSerialNumber != next.MeterSerialNumber ||
                !JToken.DeepEquals(previous.JSON["created"],                            next.JSON["created"])           ||
                !JToken.DeepEquals(previous.JSON["chargingSessionId"],                  next.JSON["chargingSessionId"]) ||
                !JToken.DeepEquals(Object(previous.JSON["chargingStationOperator"])["@id"],
                                   Object(next.    JSON["chargingStationOperator"])["@id"]))
            {
                throw new LiveSessionException("Live-session identity changed");
            }

            if (Timestamp(Text(next.JSON["lastUpdated"])) <= Timestamp(Text(previous.JSON["lastUpdated"])) ||
                Text(next.JSON["updates"]).ToLowerInvariant() != previous.Reference)
            {
                throw new LiveSessionException("Live update does not follow the original accepted predecessor");
            }

            if (!StructureOnly &&
                DocumentSignatures.VerifyDocumentSignaturesWithKeys(next.JSON, SigningKeysOf(Object(previous.JSON["chargingStationOperator"]))).Status != DocumentSignaturesStatus.AllValid)
            {
                throw new LiveSessionException("Live CPO document key changed without an authorized transition");
            }

            if (next.Readings.Count < previous.Readings.Count ||
                previous.Readings.Where((raw, index) => next.Readings[index] != raw).Any())
            {
                throw new LiveSessionException("Previously received signed readings were removed, reordered or replaced");
            }

            return next;

        }

        #endregion

        #region VerifyLiveSessionAuthority (Input, Authority, PreviousText = null, Now = null)

        /// <summary>
        /// Check that a live session document is the operator's own: about the
        /// operator, station and EVSE a roaming network vouches for, and signed
        /// with a document key the network vouches for — never by a key the
        /// document lists for itself.
        ///
        /// An update has to be the next one after its predecessor, and, where the
        /// operator changed its key, signed with an old key too: a document signed
        /// with the old and the new key bridges the change, after which the new key
        /// alone will do.
        /// </summary>
        /// <param name="Input">The document, as it arrived.</param>
        /// <param name="Authority">The operator, station, EVSE and keys the caller obtained independently.</param>
        /// <param name="PreviousText">The document accepted before, for an update.</param>
        /// <param name="Now">The time a document may not be younger than.</param>
        /// <exception cref="LiveSessionException">When the document is not the operator's, or not the next one, saying why.</exception>
        public static LiveSessionDocument VerifyLiveSessionAuthority(String            Input,
                                                                     LiveCPOAuthority  Authority,
                                                                     String?           PreviousText  = null,
                                                                     DateTimeOffset?   Now           = null)
        {

            var current  = ReadLiveSessionDocument(Input, Now);
            var cpo      = Object(current.JSON["chargingStationOperator"]);
            var station  = Object(current.JSON["chargingStation"]);

            if (current.EVSEId != Authority.EVSEId ||
                station["@id"]?.Type != JTokenType.String || station["@id"]!.Value<String>() != Authority.ChargingStationId ||
                cpo    ["@id"]?.Type != JTokenType.String || cpo    ["@id"]!.Value<String>() != Authority.OperatorId)
            {
                throw new LiveSessionException("The live document does not belong to the POI-authorized CPO/station/EVSE");
            }

            if (DocumentSignatures.VerifyDocumentSignaturesWithKeys(current.JSON, Authority.Keys.Where(key => key.KeyUsage.Contains(SignCTRs))).Status != DocumentSignaturesStatus.AllValid)
                throw new LiveSessionException("The live document signature is not authorized by the proven CPO");

            if (PreviousText is not null)
            {

                var previous = ReadLiveSessionDocument(PreviousText, Now);

                VerifyLiveSessionTransition(PreviousText, Input, Now, StructureOnly: true);

                if (DocumentSignatures.VerifyDocumentSignaturesWithKeys(current.JSON, SigningKeysOf(Object(previous.JSON["chargingStationOperator"]))).ValidCount == 0)
                    throw new LiveSessionException("A live document-key change requires an old-key co-signature on the transition envelope");

            }

            return current;

        }

        #endregion


        #region (private) Parse / Object / Text / Timestamp / SameStrings / SigningKeysOf

        private static JToken Parse(String Input)
        {

            try
            {

                using var reader = new JsonTextReader(new StringReader(Input)) {
                                       DateParseHandling   = DateParseHandling.None,
                                       FloatParseHandling  = FloatParseHandling.Double
                                   };

                var token = JToken.ReadFrom(reader);

                if (reader.Read())
                    throw new LiveSessionException("Expected a live-session object");

                return token;

            }
            catch (JsonException e)
            {
                throw new LiveSessionException("Expected a live-session object: " + e.Message);
            }

        }

        private static JObject Object(JToken? Value)

            => Value as JObject ?? throw new LiveSessionException("Expected a live-session object");

        private static String Text(JToken? Value)

            => Value?.Type == JTokenType.String && Value.Value<String>() is { Length: > 0 } text
                   ? text
                   : throw new LiveSessionException("Missing live-session identity");

        private static DateTimeOffset? Timestamp(String Text)

            => DateTimeOffset.TryParse(Text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
                   ? timestamp
                   : null;

        private static Boolean SameStrings(JToken? Value, IReadOnlyList<String> Expected)

            => Value is JArray array &&
               array.Count == Expected.Count &&
               array.Select((item, index) => item.Type == JTokenType.String && item.Value<String>() == Expected[index]).All(same => same);

        private static IReadOnlyList<DocumentPublicKey> SigningKeysOf(JObject ChargingStationOperator)

            => [ .. DocumentSignatures.CollectDocumentPublicKeys(new JObject(new JProperty("chargingStationOperator", ChargingStationOperator.DeepClone()))).
                                       Where(key => key.KeyUsage.Contains(SignCTRs)) ];

        #endregion

    }

}
