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

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.chargy.Formats.QIDigital;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the QI-Digital certificates: the Digital Calibration Certificate
    /// and the two certificates that stand behind it.
    ///
    /// ChargyCore.TS declares these as TypeScript interfaces and nothing more —
    /// no parser, no verification, no fixture. In C# a data model has to be written
    /// out, and something has to say whether the writing is right. These tests do
    /// the only thing that can be checked without a real certificate: that a
    /// document survives being read and written back, with nothing dropped and
    /// nothing invented.
    /// </summary>
    [TestFixture]
    public class QIDigitalTests : AChargyTests
    {

        #region Data

        /// <summary>
        /// A Digital Calibration Certificate for an energy meter, using the parts
        /// of the schema a charging station's certificate would actually fill.
        /// </summary>
        private const String Certificate = """
            {
              "@id": "DCC-2026-000123",
              "@context": "https://ptb.de/dcc",
              "schemaVersion": "3.2.1",
              "comments": [ "Calibrated for use under German Calibration Law." ],
              "administrativeData": {
                "software": [
                  { "name": { "en": "DCC Generator" }, "release": "2.4.0", "type": "application" }
                ],
                "coreData": {
                  "countryCodeISO3166_1": "DE",
                  "usedLangCodeISO639_1": [ "de", "en" ],
                  "mandatoryLangCodeISO639_1": [ "de" ],
                  "uniqueIdentifier": "PTB-2026-000123",
                  "beginPerformanceDate": "2026-01-12",
                  "endPerformanceDate": "2026-01-14",
                  "issueDate": "2026-01-20",
                  "performanceLocation": { "location": "laboratory" }
                },
                "items": [
                  {
                    "name": { "en": "Energy meter" },
                    "manufacturer": { "name": { "en": "EMH metering" } },
                    "model": "eHZ IW8E",
                    "identifications": [
                      { "issuer": "manufacturer", "value": "0901454D4800007F9F3E" }
                    ]
                  }
                ],
                "calibrationLaboratory": {
                  "calibrationLaboratoryCode": "DE-PTB-01",
                  "cryptElectronicSeal": true,
                  "contact": {
                    "name": { "en": "Physikalisch-Technische Bundesanstalt" },
                    "eMail": "info@ptb.de",
                    "location": {
                      "city": "Braunschweig",
                      "countryCode": "DE",
                      "postCode": "38116",
                      "street": "Bundesallee",
                      "streetNo": "100"
                    }
                  }
                },
                "respPersons": [
                  {
                    "person": { "name": { "en": "A. Calibrator" } },
                    "role": "head of laboratory",
                    "mainSigner": true
                  }
                ],
                "customer": {
                  "name": { "en": "chargeIT mobility GmbH" },
                  "location": { "city": "Kitzingen", "countryCode": "DE" }
                }
              },
              "measurementResults": [
                {
                  "name": { "en": "Energy measurement" },
                  "usedMethods": [
                    { "name": { "en": "Comparison measurement" }, "norm": [ "EN 50470-3" ] }
                  ],
                  "results": [
                    {
                      "name": { "en": "Deviation" },
                      "data": {
                        "text": [ { "content": [ { "value": "within limits", "lang": "en" } ] } ]
                      }
                    }
                  ]
                }
              ],
              "signatures": [
                {
                  "algorithm": "ECC",
                  "format": "rs",
                  "r": "aabb",
                  "s": "ccdd"
                }
              ]
            }
            """;

        #endregion


        #region ACertificateSurvivesBeingReadAndWrittenBack()

        /// <summary>
        /// A certificate read and written back must come out as it went in.
        ///
        /// That is the whole of what can be checked here, and it is not nothing:
        /// a field the reader forgot or the writer invented shows up immediately,
        /// and every level of the four-ring schema is exercised on the way.
        /// </summary>
        [Test]
        public void ACertificateSurvivesBeingReadAndWrittenBack()
        {

            var original = ChargyLib.ParseJSON(Certificate);

            Assert.That(
                DigitalCalibrationCertificate.TryParse(original, out var certificate),
                Is.True
            );

            Assert.That(certificate, Is.Not.Null);

            var roundTripped = certificate!.ToJSON();

            // Compared by value rather than as text: the claim is that nothing was
            // dropped and nothing invented, not that the writer happens to emit the
            // properties in the order the file listed them. Unlike an OCMF payload,
            // no signature here is computed over the serialisation.
            Assert.That(
                Newtonsoft.Json.Linq.JToken.DeepEquals(roundTripped, original),
                Is.True,
                $"Round-tripped:\n{roundTripped.ToString(Newtonsoft.Json.Formatting.Indented)}\n\nOriginal:\n{original.ToString(Newtonsoft.Json.Formatting.Indented)}"
            );

        }

        #endregion

        #region TheCertificateSaysWhatItSays()

        /// <summary>
        /// The parts of a certificate a charge transparency record would actually
        /// follow: which laboratory calibrated which meter, and when.
        /// </summary>
        [Test]
        public void TheCertificateSaysWhatItSays()
        {

            DigitalCalibrationCertificate.TryParse(ChargyLib.ParseJSON(Certificate), out var certificate);

            Assert.That(certificate, Is.Not.Null);

            var administrativeData  = certificate!.AdministrativeData;
            var calibratedItem      = administrativeData!.Items[0];

            Assert.Multiple(() => {

                Assert.That(certificate.Id,                             Is.EqualTo("DCC-2026-000123"));
                Assert.That(certificate.SchemaVersion,                  Is.EqualTo("3.2.1"));
                Assert.That(certificate.Signatures,                     Has.Count.EqualTo(1));

                // Which meter was calibrated — the link back to a charge
                // transparency record's energy meter identification.
                Assert.That(calibratedItem.Identifications[0].Value,    Is.EqualTo("0901454D4800007F9F3E"));
                Assert.That(calibratedItem.Model,                       Is.EqualTo("eHZ IW8E"));

                // ..., by whom, and when.
                Assert.That(administrativeData.CalibrationLaboratory?.CalibrationLaboratoryCode,  Is.EqualTo("DE-PTB-01"));
                Assert.That(administrativeData.CalibrationLaboratory?.Contact?.Location?.City,    Is.EqualTo("Braunschweig"));
                Assert.That(administrativeData.CoreData?.UniqueIdentifier,                        Is.EqualTo("PTB-2026-000123"));
                Assert.That(administrativeData.CoreData?.EndPerformanceDate,                      Is.EqualTo("2026-01-14"));
                Assert.That(administrativeData.CoreData?.PerformanceLocation?.Location,           Is.EqualTo("laboratory"));

                // Multi-language names survive as multi-language names.
                Assert.That(administrativeData.Customer?.Name?[Languages.en],                     Is.EqualTo("chargeIT mobility GmbH"));

                Assert.That(certificate.MeasurementResults[0].UsedMethods[0].Norms,               Is.EqualTo(new[] { "EN 50470-3" }));

            });

        }

        #endregion

        #region ACertificateWithoutAnIdentificationIsNotOne()

        /// <summary>
        /// A certificate that does not say which certificate it is cannot be
        /// referred to, and therefore cannot back anything up.
        /// </summary>
        [Test]
        public void ACertificateWithoutAnIdentificationIsNotOne()

            => Assert.Multiple(() => {

                   Assert.That(DigitalCalibrationCertificate. TryParse(ChargyLib.ParseJSON("{ \"schemaVersion\": \"3.2.1\" }"), out _),  Is.False);
                   Assert.That(CertificateOfAccreditation.    TryParse(ChargyLib.ParseJSON("{ }"),                              out _),  Is.False);
                   Assert.That(DigitalCertificateOfCompliance.TryParse(ChargyLib.ParseJSON("{ }"),                              out _),  Is.False);

               });

        #endregion

        #region TheTwoSupportingCertificatesRoundTrip()

        /// <summary>
        /// The accreditation and the compliance certificate — one link further up
        /// the chain each.
        /// </summary>
        [Test]
        public void TheTwoSupportingCertificatesRoundTrip()
        {

            const String json = """
                {
                  "@id": "DAkkS-1234",
                  "@context": "https://dakks.de/accreditation",
                  "signatures": [ { "algorithm": "ECC", "format": "rs", "r": "1122", "s": "3344" } ]
                }
                """;

            var original = ChargyLib.ParseJSON(json);

            Assert.Multiple(() => {

                Assert.That(CertificateOfAccreditation.TryParse(original, out var accreditation), Is.True);
                Assert.That(Newtonsoft.Json.Linq.JToken.DeepEquals(accreditation?.ToJSON(), original), Is.True);

                Assert.That(DigitalCertificateOfCompliance.TryParse(original, out var compliance), Is.True);
                Assert.That(Newtonsoft.Json.Linq.JToken.DeepEquals(compliance?.ToJSON(), original), Is.True);

            });

        }

        #endregion

    }

}
