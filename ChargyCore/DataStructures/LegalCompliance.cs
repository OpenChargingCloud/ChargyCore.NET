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
    /// A transparency software an EV driver can use to verify a charge
    /// transparency record.
    /// </summary>
    /// <param name="Name">The name of the transparency software.</param>
    /// <param name="Version">An optional version.</param>
    /// <param name="Manufacturer">An optional manufacturer.</param>
    /// <param name="DownloadURLs">Optional URLs to download the software from.</param>
    public class TransparencySoftware(String                Name,
                                      String?               Version       = null,
                                      String?               Manufacturer  = null,
                                      IEnumerable<String>?  DownloadURLs  = null)
    {

        #region Properties

        /// <summary>The name of the transparency software.</summary>
        public String                 Name            { get; } = Name;

        /// <summary>An optional version.</summary>
        public String?                Version         { get; } = Version;

        /// <summary>An optional manufacturer.</summary>
        public String?                Manufacturer    { get; } = Manufacturer;

        /// <summary>Optional URLs to download the software from.</summary>
        public IReadOnlyList<String>  DownloadURLs    { get; } = DownloadURLs?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out TransparencySoftware)

        /// <summary>
        /// Try to parse the given JSON as a transparency software.
        /// </summary>
        /// <param name="JSON">A JSON representation of a transparency software.</param>
        /// <param name="TransparencySoftware">The parsed transparency software.</param>
        public static Boolean TryParse(JObject JSON, out TransparencySoftware? TransparencySoftware)
        {

            TransparencySoftware = null;

            var name = JSON["name"]?.Value<String>();

            if (name is null)
                return false;

            TransparencySoftware = new TransparencySoftware(
                                       name,
                                       JSON["version"]?.     Value<String>(),
                                       JSON["manufacturer"]?.Value<String>(),
                                       StringList.Parse(JSON["downloadURLs"])
                                   );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this transparency software.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("name", Name)
                       );

            if (Version      is not null)
                json.Add(new JProperty("version",       Version));

            if (Manufacturer is not null)
                json.Add(new JProperty("manufacturer",  Manufacturer));

            if (DownloadURLs.Count > 0)
                json.Add(new JProperty("downloadURLs",  new JArray(DownloadURLs)));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this transparency software.
        /// </summary>
        public override String ToString()

            => $"{Name}{(Version is not null ? $" v{Version}" : "")}";

        #endregion


    }


    /// <summary>
    /// A conformity assessment certificate: the proof that a charging station or
    /// an energy meter type was approved for use under the German Calibration Law.
    /// </summary>
    /// <param name="CertificateId">The identification of the certificate.</param>
    /// <param name="NotBefore">The start of the validity period.</param>
    /// <param name="NotAfter">The end of the validity period.</param>
    /// <param name="FreeText">A free text description.</param>
    /// <param name="URL">An optional URL of the certificate.</param>
    /// <param name="OfficialSoftware">The transparency software that is officially part of the charging station.</param>
    /// <param name="CompatibleSoftware">
    /// Other transparency software that can verify these records but is not
    /// officially part of the charging station.
    /// </param>
    public class Conformity(String                              CertificateId,
                            String                              NotBefore,
                            String                              NotAfter,
                            String                              FreeText,
                            String?                             URL                 = null,
                            IEnumerable<TransparencySoftware>?  OfficialSoftware    = null,
                            IEnumerable<TransparencySoftware>?  CompatibleSoftware  = null)
    {

        #region Properties

        /// <summary>The identification of the certificate.</summary>
        public String                                CertificateId         { get; } = CertificateId;

        /// <summary>The start of the validity period.</summary>
        public String                                NotBefore             { get; } = NotBefore;

        /// <summary>The end of the validity period.</summary>
        public String                                NotAfter              { get; } = NotAfter;

        /// <summary>A free text description.</summary>
        public String                                FreeText              { get; } = FreeText;

        /// <summary>An optional URL of the certificate.</summary>
        public String?                               URL                   { get; } = URL;

        /// <summary>The transparency software that is officially part of the charging station.</summary>
        public IReadOnlyList<TransparencySoftware>   OfficialSoftware      { get; } = OfficialSoftware?.  ToArray() ?? [];

        /// <summary>Other transparency software that can verify these records.</summary>
        public IReadOnlyList<TransparencySoftware>   CompatibleSoftware    { get; } = CompatibleSoftware?.ToArray() ?? [];

        #endregion


        #region (static) TryParse(JSON, out Conformity)

        /// <summary>
        /// Try to parse the given JSON as a conformity certificate.
        /// </summary>
        /// <param name="JSON">A JSON representation of a conformity certificate.</param>
        /// <param name="Conformity">The parsed conformity certificate.</param>
        public static Boolean TryParse(JObject JSON, out Conformity? Conformity)
        {

            Conformity = null;

            var certificateId = JSON["certificateId"]?.Value<String>();

            if (certificateId is null)
                return false;

            Conformity = new Conformity(
                             certificateId,
                             JSON["notBefore"]?.Value<String>() ?? "",
                             JSON["notAfter"]?. Value<String>() ?? "",
                             JSON["freeText"]?. Value<String>() ?? "",
                             JSON["url"]?.      Value<String>(),
                             TransparencySoftwareList.Parse(JSON["officialSoftware"]),
                             TransparencySoftwareList.Parse(JSON["compatibleSoftware"])
                         );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this conformity certificate.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("certificateId", CertificateId)
                       );

            if (URL is not null)
                json.Add(new JProperty("url",                 URL));

            json.Add(new JProperty("notBefore",               NotBefore));
            json.Add(new JProperty("notAfter",                NotAfter));

            if (OfficialSoftware.  Count > 0)
                json.Add(new JProperty("officialSoftware",    new JArray(OfficialSoftware.  Select(software => software.ToJSON()))));

            if (CompatibleSoftware.Count > 0)
                json.Add(new JProperty("compatibleSoftware",  new JArray(CompatibleSoftware.Select(software => software.ToJSON()))));

            json.Add(new JProperty("freeText",                FreeText));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this conformity certificate.
        /// </summary>
        public override String ToString()

            => CertificateId;

        #endregion


    }


    /// <summary>
    /// A calibration certificate: the proof that an energy meter was verified and
    /// until when that verification is valid.
    /// </summary>
    /// <param name="CertificateId">The identification of the certificate.</param>
    /// <param name="NotBefore">The start of the validity period.</param>
    /// <param name="NotAfter">The end of the validity period.</param>
    /// <param name="FreeText">A free text description.</param>
    /// <param name="URL">An optional URL of the certificate.</param>
    public class Calibration(String   CertificateId,
                             String   NotBefore,
                             String   NotAfter,
                             String   FreeText,
                             String?  URL = null)
    {

        #region Properties

        /// <summary>The identification of the certificate.</summary>
        public String   CertificateId    { get; } = CertificateId;

        /// <summary>The start of the validity period.</summary>
        public String   NotBefore        { get; } = NotBefore;

        /// <summary>The end of the validity period.</summary>
        public String   NotAfter         { get; } = NotAfter;

        /// <summary>A free text description.</summary>
        public String   FreeText         { get; } = FreeText;

        /// <summary>An optional URL of the certificate.</summary>
        public String?  URL              { get; } = URL;

        #endregion


        #region (static) TryParse(JSON, out Calibration)

        /// <summary>
        /// Try to parse the given JSON as a calibration certificate.
        /// </summary>
        /// <param name="JSON">A JSON representation of a calibration certificate.</param>
        /// <param name="Calibration">The parsed calibration certificate.</param>
        public static Boolean TryParse(JObject JSON, out Calibration? Calibration)
        {

            Calibration = null;

            var certificateId = JSON["certificateId"]?.Value<String>();

            if (certificateId is null)
                return false;

            Calibration = new Calibration(
                              certificateId,
                              JSON["notBefore"]?.Value<String>() ?? "",
                              JSON["notAfter"]?. Value<String>() ?? "",
                              JSON["freeText"]?. Value<String>() ?? "",
                              JSON["url"]?.      Value<String>()
                          );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this calibration certificate.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject(
                           new JProperty("certificateId", CertificateId)
                       );

            if (URL is not null)
                json.Add(new JProperty("url",        URL));

            json.Add(new JProperty("notBefore",      NotBefore));
            json.Add(new JProperty("notAfter",       NotAfter));
            json.Add(new JProperty("freeText",       FreeText));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this calibration certificate.
        /// </summary>
        public override String ToString()

            => CertificateId;

        #endregion


    }


    /// <summary>
    /// Everything that documents that a device may legally be used for billing:
    /// its conformity assessments and its calibrations.
    /// </summary>
    /// <param name="FreeText">A free text description.</param>
    /// <param name="Conformity">Optional conformity certificates.</param>
    /// <param name="Calibration">Optional calibration certificates.</param>
    /// <param name="URL">An optional URL with more information.</param>
    public class LegalCompliance(String                     FreeText,
                                 IEnumerable<Conformity>?   Conformity   = null,
                                 IEnumerable<Calibration>?  Calibration  = null,
                                 String?                    URL          = null)
    {

        #region Properties

        /// <summary>A free text description.</summary>
        public String                       FreeText       { get; } = FreeText;

        /// <summary>Optional conformity certificates.</summary>
        public IReadOnlyList<Conformity>    Conformity     { get; } = Conformity?. ToArray() ?? [];

        /// <summary>Optional calibration certificates.</summary>
        public IReadOnlyList<Calibration>   Calibration    { get; } = Calibration?.ToArray() ?? [];

        /// <summary>An optional URL with more information.</summary>
        public String?                      URL            { get; } = URL;

        #endregion


        #region (static) TryParse(JSON, out LegalCompliance)

        /// <summary>
        /// Try to parse the given JSON as legal compliance information.
        /// </summary>
        /// <param name="JSON">A JSON representation of legal compliance information.</param>
        /// <param name="LegalCompliance">The parsed legal compliance information.</param>
        public static Boolean TryParse(JObject JSON, out LegalCompliance? LegalCompliance)
        {

            var conformity  = new List<Conformity>();
            var calibration = new List<Calibration>();

            if (JSON["conformity"]  is JArray conformityArray)
                foreach (var conformityJSON in conformityArray.OfType<JObject>())
                    if (chargy.Conformity. TryParse(conformityJSON,  out var singleConformity))
                        conformity. Add(singleConformity!);

            if (JSON["calibration"] is JArray calibrationArray)
                foreach (var calibrationJSON in calibrationArray.OfType<JObject>())
                    if (chargy.Calibration.TryParse(calibrationJSON, out var singleCalibration))
                        calibration.Add(singleCalibration!);

            LegalCompliance = new LegalCompliance(
                                  JSON["freeText"]?.Value<String>() ?? "",
                                  conformity,
                                  calibration,
                                  JSON["url"]?.     Value<String>()
                              );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this legal compliance information.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (Conformity. Count > 0)
                json.Add(new JProperty("conformity",   new JArray(Conformity. Select(conformity  => conformity. ToJSON()))));

            if (Calibration.Count > 0)
                json.Add(new JProperty("calibration",  new JArray(Calibration.Select(calibration => calibration.ToJSON()))));

            if (URL is not null)
                json.Add(new JProperty("url",          URL));

            json.Add(new JProperty("freeText",         FreeText));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this legal compliance information.
        /// </summary>
        public override String ToString()

            => $"{Conformity.Count} conformity, {Calibration.Count} calibration certificate(s)";

        #endregion


    }


    /// <summary>
    /// Where an EV driver can look up this charging session and which transparency
    /// software can verify it.
    /// </summary>
    /// <param name="ChargingSessionURL">An optional URL of this charging session at the operator.</param>
    /// <param name="OfficialSoftware">The transparency software that is officially part of the charging station.</param>
    /// <param name="CompatibleSoftware">Other transparency software that can verify this record.</param>
    /// <param name="FreeText">An optional free text description.</param>
    public class TransparencyInfos(String?                             ChargingSessionURL  = null,
                                   IEnumerable<TransparencySoftware>?  OfficialSoftware    = null,
                                   IEnumerable<TransparencySoftware>?  CompatibleSoftware  = null,
                                   String?                             FreeText            = null)
    {

        #region Properties

        /// <summary>An optional URL of this charging session at the operator.</summary>
        public String?                               ChargingSessionURL    { get; } = ChargingSessionURL;

        /// <summary>The transparency software that is officially part of the charging station.</summary>
        public IReadOnlyList<TransparencySoftware>   OfficialSoftware      { get; } = OfficialSoftware?.  ToArray() ?? [];

        /// <summary>Other transparency software that can verify this record.</summary>
        public IReadOnlyList<TransparencySoftware>   CompatibleSoftware    { get; } = CompatibleSoftware?.ToArray() ?? [];

        /// <summary>An optional free text description.</summary>
        public String?                               FreeText              { get; } = FreeText;

        #endregion


        #region (static) TryParse(JSON, out TransparencyInfos)

        /// <summary>
        /// Try to parse the given JSON as transparency information.
        /// </summary>
        /// <param name="JSON">A JSON representation of transparency information.</param>
        /// <param name="TransparencyInfos">The parsed transparency information.</param>
        public static Boolean TryParse(JObject JSON, out TransparencyInfos? TransparencyInfos)
        {

            TransparencyInfos = new TransparencyInfos(
                                    JSON["chargingSessionURL"]?.Value<String>(),
                                    TransparencySoftwareList.Parse(JSON["officialSoftware"]),
                                    TransparencySoftwareList.Parse(JSON["compatibleSoftware"]),
                                    JSON["freeText"]?.          Value<String>()
                                );

            return true;

        }

        #endregion

        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this transparency information.
        /// </summary>
        public JObject ToJSON()
        {

            var json = new JObject();

            if (ChargingSessionURL is not null)
                json.Add(new JProperty("chargingSessionURL",  ChargingSessionURL));

            if (OfficialSoftware.  Count > 0)
                json.Add(new JProperty("officialSoftware",    new JArray(OfficialSoftware.  Select(software => software.ToJSON()))));

            if (CompatibleSoftware.Count > 0)
                json.Add(new JProperty("compatibleSoftware",  new JArray(CompatibleSoftware.Select(software => software.ToJSON()))));

            if (FreeText           is not null)
                json.Add(new JProperty("freeText",            FreeText));

            return json;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this transparency information.
        /// </summary>
        public override String ToString()

            => ChargingSessionURL ?? FreeText ?? "<no transparency infos>";

        #endregion


    }


    /// <summary>
    /// Helpers for the repeating list shapes of a charge transparency record.
    /// </summary>
    internal static class TransparencySoftwareList
    {

        #region Parse(JSON)

        /// <summary>
        /// The transparency software entries of the given JSON array.
        /// </summary>
        /// <param name="JSON">A JSON array of transparency software.</param>
        internal static List<TransparencySoftware> Parse(JToken? JSON)
        {

            var software = new List<TransparencySoftware>();

            if (JSON is JArray softwareArray)
                foreach (var softwareJSON in softwareArray.OfType<JObject>())
                    if (TransparencySoftware.TryParse(softwareJSON, out var singleSoftware))
                        software.Add(singleSoftware!);

            return software;

        }

        #endregion

    }


    /// <summary>
    /// Helpers for the string arrays of a charge transparency record.
    /// </summary>
    internal static class StringList
    {

        #region Parse(JSON)

        /// <summary>
        /// The strings of the given JSON array, ignoring everything that is not one.
        /// </summary>
        /// <param name="JSON">A JSON array of strings.</param>
        internal static List<String> Parse(JToken? JSON)

            => JSON is JArray array
                   ? [.. array.Where (element => element.Type == JTokenType.String).
                               Select(element => element.Value<String>()!)]
                   : [];

        #endregion

    }

}
