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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Aegir;
using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.IO;
using cloud.charging.open.chargy.Formats.OCMF;

#endregion

namespace cloud.charging.open.chargy.Formats.PTB
{

    /// <summary>
    /// One thing wrong with a PTB container, and where it is.
    /// </summary>
    /// <param name="Path">Where the problem is, as a JSON path.</param>
    /// <param name="Message">What is wrong with it.</param>
    public class PTBValidationIssue(String  Path,
                                    String  Message)
    {

        /// <summary>Where the problem is, as a JSON path.</summary>
        public String  Path       { get; } = Path;

        /// <summary>What is wrong with it.</summary>
        public String  Message    { get; } = Message;

        /// <summary>Return a text representation of this issue.</summary>
        public override String ToString()
            => $"{Path} {Message}";

    }


    /// <summary>
    /// A PTB container was rejected, with everything found wrong.
    /// </summary>
    /// <param name="Message">A summary of the rejection.</param>
    /// <param name="Issues">Everything found wrong.</param>
    /// <param name="Certainty">How sure we are that this was meant to be a PTB container.</param>
    public class PTBValidationResult(I18NString                     Message,
                                     IEnumerable<PTBValidationIssue>  Issues,
                                     Double                         Certainty = 1)

        : SessionCryptoResult(SessionVerificationResult.InvalidSessionFormat,
                              Message,
                              Certainty: Certainty)

    {

        /// <summary>Everything found wrong.</summary>
        public IReadOnlyList<PTBValidationIssue> Issues { get; } = Issues.ToArray();

    }


    /// <summary>
    /// The PTB container format.
    ///
    /// A small envelope around two OCMF documents — the one that opened a charging
    /// session and the one that closed it — plus the thing OCMF cannot say: where
    /// the charging station stands. The signatures inside are ordinary OCMF, and
    /// this format neither adds to them nor stands between them and the reader.
    ///
    /// What it does add is a schema, and it is checked strictly: an envelope that
    /// mislabels the place its readings came from is worth rejecting even though
    /// the readings themselves would verify, because the place is precisely what
    /// somebody would have to falsify to bill a driver for a charging session at a
    /// station they never visited. Every violation is collected rather than the
    /// first one reported.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="OCMF">The OCMF format, which reads the documents inside.</param>
    public partial class PTBContainer(I18NDictionary  I18N,
                                      OCMFFormat?     OCMF = null) : IJSONChargeTransparencyFormat
    {

        #region Data

        private readonly I18NDictionary  i18n  = I18N;
        private readonly OCMFFormat?     ocmf  = OCMF;

        #endregion

        #region Properties

        /// <summary>The name of the data format.</summary>
        public String Name
            => "PTB";

        #endregion


        #region TryParseJSON(JSON)

        /// <summary>
        /// Try to read a charge transparency record from a PTB container.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        public Object TryParseJSON(JObject JSON)
        {

            var issues = Validate(JSON).ToArray();

            if (issues.Length > 0)
                return new PTBValidationResult(
                           i18n.GetMultilanguageText("Invalid PTB OCMF container!"),
                           issues
                       );

            if (ocmf is null)
                return new SessionCryptoResult(
                           SessionVerificationResult.UnknownCTRFormat,
                           i18n.GetMultilanguageText("UnknownOrInvalidChargingSessionFormat")
                       );

            #region What the envelope knows and the OCMF documents do not

            var chargeboxId     = Text(JSON, "chargeboxIdentifier")!;
            var address         = JSON["address"]     as JObject;
            var geoLocation     = JSON["geoLocation"] as JObject;

            var containerInfos  = new ContainerInfos();

            containerInfos.AddChargingStation(
                new ChargingStation(
                    chargeboxId,
                    Address:      new Address(
                                      Street:       Text(address, "street"),
                                      HouseNumber:  Text(address, "houseNumber"),
                                      // The format grew two names for each of these,
                                      // and a container may use either.
                                      PostalCode:   Text(address, "postalCode") ?? Text(address, "zipCode"),
                                      City:         Text(address, "city")       ?? Text(address, "town"),
                                      Country:      Text(address, "country")
                                  ),
                    GeoLocation:  GeoCoordinate.Create(
                                      Latitude. Parse((Double) Number(geoLocation, "lat")!.Value),
                                      Longitude.Parse((Double) Number(geoLocation, "lng")!.Value)
                                  ),
                    EVSEs:        [ new EVSE(chargeboxId) ]
                )
            );

            #endregion

            return ocmf.TryParse(
                       [ Text(JSON, "ocmfBegin")!, Text(JSON, "ocmfEnd")! ],
                       Text(JSON, "publicKey")!,
                       "base64",
                       containerInfos
                   );

        }

        #endregion


        #region (private, static) Validate(JSON)

        /// <summary>
        /// Everything wrong with a PTB container.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        private static IEnumerable<PTBValidationIssue> Validate(JObject JSON)
        {

            var issues = new List<PTBValidationIssue>();

            void Require(JObject? Parent, String Property, String ParentPath = "$")
            {

                var value = Text(Parent, Property);

                if (value is null || value.Length == 0)
                    issues.Add(new PTBValidationIssue($"{ParentPath}.{Property}", "must be a non-empty string"));

            }

            if (Text(JSON, "format") != "ptb")
                issues.Add(new PTBValidationIssue("$.format", "must equal ptb"));

            Require(JSON, "publicKey");
            Require(JSON, "chargeboxIdentifier");
            Require(JSON, "ocmfBegin");
            Require(JSON, "ocmfEnd");

            #region The version, when the container states one

            if (JSON["formatVersion"] is JToken formatVersion &&
                formatVersion.Type != JTokenType.Null &&
                (formatVersion.Type != JTokenType.String ||
                 !FormatVersionRegex().IsMatch(formatVersion.Value<String>()!)))
            {
                issues.Add(new PTBValidationIssue("$.formatVersion", @"must match ^1(?:\.[0-9]+)?$"));
            }

            #endregion

            #region The public key, which PTB files as base64 rather than as hexadecimal

            if (Text(JSON, "publicKey") is String publicKey &&
                publicKey.Length > 0 &&
                !Base64Regex().IsMatch(publicKey))
            {
                issues.Add(new PTBValidationIssue("$.publicKey", "must be a base64 encoded string"));
            }

            #endregion

            #region The two OCMF documents, which have to be untouched

            foreach (var property in new[] { "ocmfBegin", "ocmfEnd" })
                if (Text(JSON, property) is String document &&
                    (document.Length < 10 || !document.StartsWith("OCMF|", StringComparison.Ordinal)))
                {
                    issues.Add(new PTBValidationIssue($"$.{property}", "must be an unmodified OCMF record beginning with OCMF|"));
                }

            #endregion

            #region Where the charging station stands

            if (JSON["address"] is not JObject address)
                issues.Add(new PTBValidationIssue("$.address", "must be an object"));
            else
                ValidateAddress(address, issues, Require);

            if (JSON["geoLocation"] is not JObject geoLocation)
                issues.Add(new PTBValidationIssue("$.geoLocation", "must be an object"));
            else
                ValidateGeoLocation(geoLocation, issues);

            #endregion

            return issues;

        }

        #endregion

        #region (private, static) ValidateAddress(Address, Issues, Require)

        /// <summary>
        /// Check the address of a PTB container.
        /// </summary>
        private static void ValidateAddress(JObject                           Address,
                                            List<PTBValidationIssue>          Issues,
                                            Action<JObject?, String, String>  Require)
        {

            Require(Address, "street", "$.address");

            foreach (var property in new[] { "houseNumber", "zipCode", "postalCode", "town", "city", "country" })
            {

                var value = Address[property];

                if (value is not null && value.Type != JTokenType.Null && value.Type != JTokenType.String)
                    Issues.Add(new PTBValidationIssue($"$.address.{property}", "must be a string"));

                else if ((property == "town" || property == "city") &&
                         value?.Type == JTokenType.String &&
                         value.Value<String>()!.Length == 0)
                {
                    Issues.Add(new PTBValidationIssue($"$.address.{property}", "must be a non-empty string"));
                }

            }

            // The two names are alternatives, not both optional: a charging station
            // without a town is not locatable, which is the whole point of the
            // envelope.
            if (String.IsNullOrEmpty(Text(Address, "town")) &&
                String.IsNullOrEmpty(Text(Address, "city")))
            {
                Issues.Add(new PTBValidationIssue("$.address", "must contain a non-empty town or city"));
            }

        }

        #endregion

        #region (private, static) ValidateGeoLocation(GeoLocation, Issues)

        /// <summary>
        /// Check the geographical location of a PTB container.
        ///
        /// Unknown properties are rejected rather than ignored: this is the one
        /// place in the container where an extra field could carry a second, quietly
        /// contradicting answer.
        /// </summary>
        private static void ValidateGeoLocation(JObject                   GeoLocation,
                                                List<PTBValidationIssue>  Issues)
        {

            var latitude   = Number(GeoLocation, "lat");
            var longitude  = Number(GeoLocation, "lng");

            if (!latitude.HasValue || latitude < -90 || latitude > 90)
                Issues.Add(new PTBValidationIssue("$.geoLocation.lat", "must be a number between -90 and 90"));

            if (!longitude.HasValue || longitude < -180 || longitude > 180)
                Issues.Add(new PTBValidationIssue("$.geoLocation.lng", "must be a number between -180 and 180"));

            foreach (var property in GeoLocation.Properties())
                if (property.Name != "lat" && property.Name != "lng")
                    Issues.Add(new PTBValidationIssue($"$.geoLocation.{property.Name}", "is not allowed"));

        }

        #endregion

        #region (private, static) JSON helpers

        /// <summary>A string property, or null when it is absent or not a string.</summary>
        private static String? Text(JObject?  JSON,
                                    String    Key)

            => JSON?[Key]?.Type == JTokenType.String
                   ? JSON[Key]!.Value<String>()
                   : null;

        /// <summary>A numeric property, or null when it is absent or not a number.</summary>
        private static Decimal? Number(JObject?  JSON,
                                       String    Key)

            => JSON?[Key]?.Type == JTokenType.Integer ||
               JSON?[Key]?.Type == JTokenType.Float
                   ? JSON[Key]!.Value<Decimal>()
                   : null;

        #endregion

        #region (private) Regular expressions

        [GeneratedRegex(@"^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$")]
        private static partial Regex Base64Regex();

        [GeneratedRegex(@"^1(?:\.[0-9]+)?$")]
        private static partial Regex FormatVersionRegex();

        #endregion


    }

}
