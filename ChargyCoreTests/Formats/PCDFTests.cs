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

using cloud.charging.open.chargy.Crypto;
using cloud.charging.open.chargy.Formats.PCDF;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the Porsche Charging Data Format: the parser, what the fields are
    /// allowed to claim, the signature, and the records built from them.
    /// </summary>
    [TestFixture]
    public class PCDFTests : AChargyTests
    {

        #region ParsesEveryMandatoryFieldAndTheSignedPayload()

        /// <summary>
        /// All fourteen fields, and the payload the signature covers.
        ///
        /// The payload has to end exactly at the public key, because the signature
        /// is over the document's text up to the point where the signature itself
        /// begins — one character either way and nothing verifies.
        /// </summary>
        [Test]
        public void ParsesEveryMandatoryFieldAndTheSignedPayload()
        {

            var generated = GeneratePCDF();
            var parsed    = PCDFDocument.Parse(generated.Document);

            Assert.Multiple(() => {

                Assert.That(parsed.Fields["ST"],     Is.EqualTo("260101120000"));
                Assert.That(parsed.Fields["RV"],     Is.EqualTo("0001.234*kWh"));
                Assert.That(parsed.Fields["PK"],     Is.EqualTo(generated.PublicKeyHEX));
                Assert.That(parsed.SignedPayload,    Is.EqualTo(generated.SignedPayload));
                Assert.That(parsed.SignedPayload,    Does.EndWith($"(PK:{generated.PublicKeyHEX})"));

            });

        }

        #endregion

        #region HandlesSTXAndETXWrapping()

        /// <summary>
        /// A document that came off a serial line, wrapped in STX and ETX.
        /// </summary>
        [Test]
        public void HandlesSTXAndETXWrapping()
        {

            var generated = GeneratePCDF();
            var parsed    = PCDFDocument.Parse($"{generated.Document}");

            Assert.That(parsed.Fields["SG"], Is.EqualTo(generated.Fields["SG"]));

        }

        #endregion

        #region RejectsSomethingThatIsNotPCDF()

        /// <summary>
        /// Another format's data, which must not be read as a damaged PCDF document.
        /// </summary>
        [Test]
        public void RejectsSomethingThatIsNotPCDF()

            => Assert.That(
                   () => PCDFDocument.Parse("OCMF|{}|{}"),
                   Throws.TypeOf<PCDFParseException>()
               );

        #endregion

        #region RejectsAMissingFieldAndSaysWhichOne()

        /// <summary>
        /// A field removed from an otherwise valid document.
        ///
        /// Naming the missing field matters: the fields are a fixed sequence, so
        /// "this is not valid" would leave somebody comparing fourteen parentheses
        /// by hand.
        /// </summary>
        [Test]
        public void RejectsAMissingFieldAndSaysWhichOne()
        {

            var withoutST = GeneratePCDF().Document.Replace("(ST:260101120000)", "");

            Assert.That(
                () => PCDFDocument.Parse(withoutST),
                Throws.TypeOf<PCDFParseException>().With.Message.Contains("Missing fields")
            );

        }

        #endregion


        #region ValidatesAndNormalizesAGoodDocument()

        /// <summary>
        /// What the fields become once they have been read.
        /// </summary>
        [Test]
        public void ValidatesAndNormalizesAGoodDocument()
        {

            var generated = GeneratePCDF();
            var document  = PCDFDocument.Read(generated.Document);

            Assert.Multiple(() => {

                Assert.That(document.StopTime,           Is.EqualTo("2026-01-01T12:05:00.000Z"));
                Assert.That(document.DurationSeconds,    Is.EqualTo(300));
                Assert.That(document.ReadingValue,       Is.EqualTo(1.234m));
                Assert.That(document.HardwareSerial,     Is.EqualTo("12345678901"));
                Assert.That(document.PublicKeyHEX,       Is.EqualTo(generated.Fields["PK"]));

            });

        }

        #endregion

        #region RejectsAnUnbillableSessionAndSaysWhy()

        /// <summary>
        /// A meter reporting a billing error and a missing closing reading.
        ///
        /// Neither is a formatting problem — the document is perfectly well
        /// formed, and the meter is saying the session cannot be billed. Both
        /// reasons are reported, because a meter in trouble usually reports more
        /// than one thing at once and the first is rarely the informative one.
        /// </summary>
        [Test]
        public void RejectsAnUnbillableSessionAndSaysWhy()
        {

            var generated = GeneratePCDF(("BV", "0"), ("SP", "0"));

            Assert.That(
                () => PCDFDocument.Read(generated.Document),
                Throws.TypeOf<PCDFValidationException>().
                       With.Message.Contains("Billing not possible").
                       And. Message.Contains("last data")
            );

        }

        #endregion

        #region RejectsACorruptTimestampAndAMisspelledReading()

        /// <summary>
        /// The 31st of February, and a meter reading one digit short.
        /// </summary>
        [Test]
        public void RejectsACorruptTimestampAndAMisspelledReading()
        {

            var generated = GeneratePCDF(("ST", "260231120000"), ("RV", "001.234*kWh"));

            Assert.That(
                () => PCDFDocument.Read(generated.Document),
                Throws.TypeOf<PCDFValidationException>().
                       With.Message.Contains("Corrupt time information").
                       And. Message.Contains("Session information is invalid")
            );

        }

        #endregion


        #region ParsesADERSignature()

        /// <summary>
        /// The signature arrives DER encoded and has to become two integers.
        /// </summary>
        [Test]
        public void ParsesADERSignature()
        {

            var signature = PCDFDocument.ParseSignature(GeneratePCDF().Fields["SG"]);

            Assert.Multiple(() => {
                Assert.That(signature.R, Is.Not.Empty);
                Assert.That(signature.S, Is.Not.Empty);
            });

        }

        #endregion

        #region NormalizesADERPublicKeyToItsBarePoint()

        /// <summary>
        /// A public key filed as a whole SubjectPublicKeyInfo rather than as the
        /// bare point. Both describe the same key.
        /// </summary>
        [Test]
        public void NormalizesADERPublicKeyToItsBarePoint()

            => Assert.That(
                   PCDFDocument.NormalizePublicKeyHEX(
                       ReadTextFixture("OCMF/OCMF-Testdata-01_publicKey.txt")
                   ),
                   Has.Length.EqualTo(130)
               );

        #endregion

        #region VerifiesAndRejectsASignature(Tamper, ExpectedResult)

        /// <summary>
        /// A good signature, and the same document with one digit of the meter
        /// reading changed.
        /// </summary>
        /// <param name="Tamper">Whether to change the reading before checking.</param>
        /// <param name="ExpectedResult">What the check has to conclude.</param>
        [TestCase(false, VerificationResult.ValidSignature)]
        [TestCase(true,  VerificationResult.InvalidSignature)]
        public void VerifiesAndRejectsASignature(Boolean             Tamper,
                                                 VerificationResult  ExpectedResult)
        {

            var generated = GeneratePCDF();

            var text      = Tamper
                                ? generated.Document.Replace("0001.234*kWh", "0001.235*kWh")
                                : generated.Document;

            Assert.That(
                PCDFDocument.Read(text).ValidationStatus,
                Is.EqualTo(ExpectedResult)
            );

        }

        #endregion


        #region ImportsARealFixture()

        /// <summary>
        /// A complete PCDF document from a real charging station.
        /// </summary>
        [Test]
        public async Task ImportsARealFixture()
        {

            var result = await VerifyFixtures([ "PCDF/pcdf-valid-session-01.pcdf" ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var chargingSession = ((ChargeTransparencyRecord) result).ChargingSessions[0];
            var value           = chargingSession.Measurements[0].Values[0];

            Assert.Multiple(() => {

                Assert.That(chargingSession.VerificationResult?.Status,  Is.EqualTo(SessionVerificationResult.ValidSignature));
                Assert.That(chargingSession.Begin,                       Is.EqualTo("2025-04-15T08:30:15.000Z"));
                Assert.That(chargingSession.End,                         Is.EqualTo("2025-04-15T09:17:45.000Z"));
                Assert.That(chargingSession.AuthorizationStart?.Id,      Is.EqualTo("DE"));
                Assert.That(value.Value,                                 Is.EqualTo(12.345m));
                Assert.That(value.Result?.Status,                        Is.EqualTo(VerificationResult.ValidSignature));

            });

        }

        #endregion

        #region ImportsAWrappedFixture()

        /// <summary>
        /// The same, off a serial line and wrapped in STX and ETX.
        /// </summary>
        [Test]
        public async Task ImportsAWrappedFixture()
        {

            var result = await VerifyFixtures([ "PCDF/pcdf-valid-session-02-wrapped.pcdf" ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var chargingSession = ((ChargeTransparencyRecord) result).ChargingSessions[0];
            var value           = chargingSession.Measurements[0].Values[0];

            Assert.Multiple(() => {

                Assert.That(chargingSession.VerificationResult?.Status,  Is.EqualTo(SessionVerificationResult.ValidSignature));
                Assert.That(chargingSession.AuthorizationStart?.Id,      Is.EqualTo("RFID-4711"));
                Assert.That(value.Value,                                 Is.EqualTo(3.21m));
                Assert.That(value.Result?.Status,                        Is.EqualTo(VerificationResult.ValidSignature));

                Assert.That(value,                                       Is.InstanceOf<PCDFMeasurementValue>());
                Assert.That(((PCDFMeasurementValue) value).Document.DCMeterType,  Is.EqualTo(1));

            });

        }

        #endregion

        #region DetectsATamperedRealFixture()

        /// <summary>
        /// One digit changed in a real signed document.
        ///
        /// This is what the format exists for, and the one case an EV driver is
        /// entitled to have caught: everything else about the document still reads
        /// perfectly well.
        /// </summary>
        [Test]
        public async Task DetectsATamperedRealFixture()
        {

            var tampered = ReadTextFixture("PCDF/pcdf-valid-session-01.pcdf").
                               Replace("0012.345*kWh", "0012.346*kWh");

            var result   = await VerifyText("pcdf-valid-session-01-tampered.pcdf", tampered);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var chargingSession = ((ChargeTransparencyRecord) result).ChargingSessions[0];

            Assert.Multiple(() => {

                Assert.That(chargingSession.VerificationResult?.Status,
                            Is.EqualTo(SessionVerificationResult.InvalidSignature));

                Assert.That(chargingSession.Measurements[0].Values[0].Result?.Status,
                            Is.EqualTo(VerificationResult.InvalidSignature));

            });

        }

        #endregion

        #region ConvertsAGeneratedRecord()

        /// <summary>
        /// A freshly signed document, all the way through the pipeline.
        /// </summary>
        [Test]
        public async Task ConvertsAGeneratedRecord()
        {

            var generated  = GeneratePCDF();
            var result     = await VerifyText("PCDF-valid-01.pcdf", generated.Document);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var chargingSession  = ((ChargeTransparencyRecord) result).ChargingSessions[0];
            var measurement      = chargingSession.Measurements[0];

            Assert.Multiple(() => {

                Assert.That(chargingSession.VerificationResult?.Status,  Is.EqualTo(SessionVerificationResult.ValidSignature));
                Assert.That(chargingSession.JSONLDContext,               Has.Some.Contains("PCDF"));
                Assert.That(chargingSession.AuthorizationStart?.Id,      Is.EqualTo("testuser"));
                Assert.That(chargingSession.AuthorizationStop,           Is.Null);
                Assert.That(measurement.OBIS,                            Is.EqualTo(PCDFDocument.Prefix));
                Assert.That(measurement.Values[0].Value,                 Is.EqualTo(1.234m));
                Assert.That(measurement.Values[0].Result?.Status,        Is.EqualTo(VerificationResult.ValidSignature));

            });

        }

        #endregion

        #region DetectsAGeneratedRecordWithABadSignature()

        /// <summary>
        /// A freshly signed document with the reading changed afterwards.
        /// </summary>
        [Test]
        public async Task DetectsAGeneratedRecordWithABadSignature()
        {

            var tampered  = GeneratePCDF().Document.Replace("0001.234*kWh", "0001.235*kWh");
            var result    = await VerifyText("PCDF-invalid-signature.pcdf", tampered);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var chargingSession = ((ChargeTransparencyRecord) result).ChargingSessions[0];

            Assert.Multiple(() => {

                Assert.That(chargingSession.VerificationResult?.Status,
                            Is.EqualTo(SessionVerificationResult.InvalidSignature));

                Assert.That(chargingSession.Measurements[0].Values[0].Result?.Status,
                            Is.EqualTo(VerificationResult.InvalidSignature));

            });

        }

        #endregion

        #region AKeyFiledSeparatelyThatDisagreesStopsTheReading()

        /// <summary>
        /// A PCDF document carries its own public key, so a key handed over
        /// alongside it is not filling a gap — it is a second opinion.
        ///
        /// When the two disagree, one of them is describing a different meter, and
        /// there is no honest way to choose. Reading on with either would be
        /// telling an EV driver that something was checked which was not.
        /// </summary>
        [Test]
        public void AKeyFiledSeparatelyThatDisagreesStopsTheReading()
        {

            var generated  = GeneratePCDF();
            var otherKey   = GeneratePCDF().PublicKeyHEX;

            var result     = new PCDFFormat(I18NDictionary.Default()).
                                 TryParseText(generated.Document, otherKey);

            Assert.That(result, Is.InstanceOf<SessionCryptoResult>());

            Assert.That(
                ((SessionCryptoResult) result).Status,
                Is.EqualTo(SessionVerificationResult.InvalidPublicKey)
            );

        }

        #endregion


        #region (private, static) VerifyText   (FileName, Text)

        /// <summary>
        /// Run a PCDF document through the whole pipeline, as an application would.
        /// </summary>
        /// <param name="FileName">The name the file arrived under.</param>
        /// <param name="Text">The contents of the file.</param>
        private static Task<Object> VerifyText(String  FileName,
                                               String  Text)

            => Verify([
                   new FileInfo(
                       FileName,
                       Encoding.UTF8.GetBytes(Text),
                       "text/plain"
                   )
               ]);

        #endregion

        #region (private, static) GeneratePCDF (Overrides)

        /// <summary>
        /// A PCDF document signed with a key generated for this test.
        ///
        /// Signing rather than hand-writing a signature is what makes the negative
        /// cases meaningful: a document whose fields were changed after signing is
        /// exactly the thing the format is meant to catch, and only a real
        /// signature can be made to fail for the right reason.
        /// </summary>
        /// <param name="Overrides">Fields to replace before signing.</param>
        private static (String Document, String SignedPayload, String PublicKeyHEX, Dictionary<String, String> Fields)
            GeneratePCDF(params (String Field, String Value)[] Overrides)
        {

            var suite    = ECCurveVerifier.secp256r1.Suite;
            var keyPair  = suite.GenerateKeyPair();

            var fields   = new Dictionary<String, String>(StringComparer.Ordinal) {
                               [ "ST"  ] = "260101120000",
                               [ "CT"  ] = "260101120500",
                               [ "CD"  ] = "000500",
                               [ "TV"  ] = "1",
                               [ "BV"  ] = "1",
                               [ "CSC" ] = "7",
                               [ "SP"  ] = "1",
                               [ "RV"  ] = "0001.234*kWh",
                               [ "SI"  ] = "testuser*1*tx-001",
                               [ "CS"  ] = "aabbccdd",
                               [ "HW"  ] = "12345678901",
                               [ "DT"  ] = "0",
                               [ "PK"  ] = Convert.ToHexStringLower(keyPair.PublicKey),
                               [ "SG"  ] = ""
                           };

            foreach (var (field, value) in Overrides)
                fields[field] = value;

            var payload = PCDFDocument.Prefix +
                          String.Concat(
                              PCDFDocument.FieldOrder.
                                           Where (field => field != "SG").
                                           Select(field => $"({field}:{fields[field]})")
                          );

            var signature = suite.Sign(
                                SHA256.HashData(Encoding.UTF8.GetBytes(payload)),
                                keyPair.PrivateKey,
                                new SignatureOptions(
                                    Prehashed:  true,
                                    Encoding:   SignatureEncoding.DER
                                )
                            );

            fields["SG"] = Convert.ToHexStringLower(signature);

            return (
                       $"{payload}(SG:{fields["SG"]})",
                       payload,
                       fields["PK"],
                       fields
                   );

        }

        #endregion

    }

}
