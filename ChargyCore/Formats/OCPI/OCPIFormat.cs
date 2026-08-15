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

using org.GraphDefined.Vanaheimr.Aegir;

using cloud.charging.open.chargy.IO;
using cloud.charging.open.chargy.Formats.ChargeIT;
using cloud.charging.open.chargy.Formats.OCMF;

#endregion

namespace cloud.charging.open.chargy.Formats.OCPI
{

    /// <summary>
    /// The OCPI charge transparency container.
    ///
    /// Two shapes travel under this name. The older one is a thin envelope around
    /// a single signed meter value — "encoding_method", "public_key",
    /// "signed_values" — plus what the roaming protocol knows and the signed data
    /// does not: which EVSE was used, where it stands, and what kind of meter is
    /// installed there.
    ///
    /// The newer one declares itself as "ocpi-2.1" and is, field for field, the
    /// newer chargeIT container. It is handled by that reader rather than copied,
    /// because two readers for one shape would drift apart and an EV driver would
    /// then get a different answer depending on which name the file happened to
    /// carry.
    ///
    /// The interesting part is the meter. The container describes it and so does
    /// the signed payload, and the two are not equal: what the meter signed about
    /// itself wins, and the container may only fill the gaps. OCMF has no field
    /// for a manufacturer's web address or a hardware revision, so those can only
    /// come from the container — while its idea of the model must never override
    /// the signed one.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="OCMF">The OCMF format, which reads the signed values.</param>
    /// <param name="ChargeIT">The chargeIT container, which is what "ocpi-2.1" actually is.</param>
    public class OCPIFormat(I18NDictionary     I18N,
                            OCMFFormat?        OCMF      = null,
                            ChargeITContainer? ChargeIT  = null) : IJSONChargeTransparencyFormat
    {

        #region Data

        /// <summary>The JSON-LD context of the newer OCPI container.</summary>
        public const String ContainerContext = "https://open.charging.cloud/contexts/ocpi-2.1";

        private readonly I18NDictionary      i18n      = I18N;
        private readonly OCMFFormat?         ocmf      = OCMF;
        private readonly ChargeITContainer?  chargeIT  = ChargeIT;

        #endregion

        #region Properties

        /// <summary>The name of the data format.</summary>
        public String Name
            => "OCPI";

        #endregion


        #region TryParseJSON(JSON)

        /// <summary>
        /// Try to read a charge transparency record from an OCPI container.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        public Object TryParseJSON(JObject JSON)
        {

            try
            {

                var context = Text(JSON, "@context")?.Trim() ?? "";

                if (context.Length == 0)
                    return ParseOldContainer(JSON);

                // Same format, different name. Handing it on rather than copying
                // the reader is what keeps the two names from drifting into two
                // answers.
                if (context == ContainerContext)
                    return chargeIT is not null
                               ? chargeIT.TryParseNewContainer(JSON)
                               : Unsupported();

                return Invalid("No chargeIT charge transparency record");

            }
            catch (Exception exception)
            {
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSessionFormat,
                           i18n.GetMultilanguageText($"Exception occured: {exception.Message}"),
                           Exception: exception
                       );
            }

        }

        #endregion


        #region (private) ParseOldContainer(JSON)

        /// <summary>
        /// Read the older OCPI envelope, which carries a single signed meter value.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        private Object ParseOldContainer(JObject JSON)
        {

            var errors = new List<Error>();

            var encodingMethod  = Text(JSON, "encoding_method");
            var publicKey       = Text(JSON, "public_key");
            var signedValues    = JSON["signed_values"] as JArray;

            if (encodingMethod is null || encodingMethod.Length == 0)
                errors.Add(new Error(i18n.GetMultilanguageText("MissingOrInvalidEncodingMethod")));

            if (publicKey      is null || publicKey.Length == 0)
                errors.Add(new Error(i18n.GetMultilanguageText("MissingOrInvalidPublicKey")));

            if (signedValues   is null)
                errors.Add(new Error(i18n.GetMultilanguageText("MissingOrInvalidSignedValues")));

            if (errors.Count > 0)
            {

                var result = new SessionCryptoResult(SessionVerificationResult.InvalidSessionFormat);

                foreach (var error in errors)
                    result.AddError(error);

                return result;

            }

            #region Only OCMF is carried this way, and only its first document is read

            if (encodingMethod != "OCMF" ||
                signedValues!.Count == 0 ||
                Text(signedValues[0] as JObject, "signed_data") is not String signedData ||
                signedData.Length == 0)
            {
                return Invalid("No chargeIT charge transparency record");
            }

            if (ocmf is null)
                return Unsupported();

            #endregion

            return ocmf.TryParse(
                       [ signedData ],
                       publicKey!,
                       encodingMethod,
                       BuildContainerInfos(JSON)
                   );

        }

        #endregion

        #region (private, static) BuildContainerInfos(JSON)

        /// <summary>
        /// What the OCPI envelope knows and the signed meter value does not.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        private static ContainerInfos BuildContainerInfos(JObject JSON)
        {

            var containerInfos  = new ContainerInfos();

            var placeInfo       = JSON["placeInfo"]   as JObject;
            var meterInfo       = JSON["meterInfo"]   as JObject;
            var geoLocation     = placeInfo?["geoLocation"] as JObject;
            var address         = placeInfo?["address"]     as JObject;

            var evseId          = Text(placeInfo, "evseId");

            #region The meter, which the signed payload will override wherever the two overlap

            if (meterInfo is not null)
                containerInfos.EnergyMeter = new EnergyMeter(
                                                 Text(meterInfo, "meterId") ?? "",
                                                 Manufacturer:  new Manufacturer(
                                                                    Text(meterInfo, "manufacturer"),
                                                                    Contact: Text(meterInfo, "manufacturerURL") is String manufacturerURL
                                                                                 ? new Contact(Web: manufacturerURL)
                                                                                 : null
                                                                ),
                                                 Model:         new DeviceModel(
                                                                    Text(meterInfo, "model"),
                                                                    Text(meterInfo, "modelURL")
                                                                ),
                                                 Hardware:      Text(meterInfo, "hardwareVersion") is String hardwareVersion
                                                                    ? new Hardware(Revision: hardwareVersion)
                                                                    : null,
                                                 Firmware:      Text(meterInfo, "firmwareVersion") is String firmwareVersion
                                                                    ? new Firmware(firmwareVersion)
                                                                    : null
                                             );

            #endregion

            #region ..., and the place, which it never states at all

            if (evseId is not null)
                containerInfos.AddChargingStation(
                    new ChargingStation(
                        ChargeITOperator.ChargingStationIdOf(evseId),
                        Address:      address is not null
                                          ? new Address(
                                                Street:      Text(address, "street"),
                                                PostalCode:  Text(address, "zipCode") ?? "",
                                                City:        Text(address, "town"),
                                                Country:     Text(address, "country") ?? ""
                                            )
                                          : null,
                        GeoLocation:  Number(geoLocation, "lat") is Decimal latitude &&
                                      Number(geoLocation, "lon") is Decimal longitude
                                          ? GeoCoordinate.Create(
                                                Latitude. Parse((Double) latitude),
                                                Longitude.Parse((Double) longitude)
                                            )
                                          : null,
                        EVSEs:        [ new EVSE(evseId) ]
                    )
                );

            #endregion

            return containerInfos;

        }

        #endregion


        #region (private) Invalid    (MessageKey)

        /// <summary>
        /// Report that the data is not a valid OCPI container.
        /// </summary>
        /// <param name="MessageKey">The i18n key of the reason.</param>
        private SessionCryptoResult Invalid(String MessageKey)

            => new (
                   SessionVerificationResult.InvalidSessionFormat,
                   i18n.GetMultilanguageText(MessageKey)
               );

        #endregion

        #region (private) Unsupported()

        /// <summary>
        /// Report that the container carries a format Chargy was not built with.
        /// </summary>
        private SessionCryptoResult Unsupported()

            => new (
                   SessionVerificationResult.UnknownCTRFormat,
                   i18n.GetMultilanguageText("UnknownOrInvalidChargingSessionFormat")
               );

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


    }

}
