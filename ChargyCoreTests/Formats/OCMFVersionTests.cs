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

using cloud.charging.open.chargy.Formats.OCMF;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests across every OCMF version this port claims to read.
    ///
    /// The fixtures in this repository come from the meters somebody happened to
    /// send us a file from, which leaves whole versions of the format covered by
    /// nothing at all. These tests fill that in: one generated document per
    /// version, every field filled with a different value, so that reading a
    /// field out of the wrong place fails rather than passing by coincidence.
    ///
    /// Each version is read twice, with and without the "FV" field. OCMF made the
    /// version optional, so a document that does not state one is not malformed —
    /// it is a document whose version has to be inferred from what it contains,
    /// and everything else about it has to be read exactly the same way.
    /// </summary>
    [TestFixture]
    public class OCMFVersionTests : AChargyTests
    {

        #region EveryVersionIsReadTheSameWay(Version, IncludesFormatVersion)

        /// <summary>
        /// Every OCMF version, with and without a stated version, has to reach
        /// the record the version's own fields say it should.
        /// </summary>
        /// <param name="Version">An OCMF version.</param>
        /// <param name="IncludesFormatVersion">Whether the document states its version.</param>
        [TestCase("0.1", true)]
        [TestCase("0.1", false)]
        [TestCase("1.0", true)]
        [TestCase("1.0", false)]
        [TestCase("1.1", true)]
        [TestCase("1.1", false)]
        [TestCase("1.2", true)]
        [TestCase("1.2", false)]
        [TestCase("1.3", true)]
        [TestCase("1.3", false)]
        [TestCase("1.4", true)]
        [TestCase("1.4", false)]
        public void EveryVersionIsReadTheSameWay(String   Version,
                                                 Boolean  IncludesFormatVersion)
        {

            var testData  = OCMFVersionTestData.Create(Version, IncludesFormatVersion);
            var record    = Parse(testData);
            var expected  = testData.Expected;
            var session   = record.ChargingSessions[0];

            Assert.Multiple(() => {

                #region The signature, and what the payload said about itself

                Assert.That(record.Status,                            Is.EqualTo(SessionVerificationResult.ValidSignature));

                // The version is reported only where the document stated one.
                // Inferring it from the fields and then reporting the inference
                // as if the meter had written it would put a claim into the
                // record that nobody signed.
                Assert.That(record.OCMF.FormatVersion,                Is.EqualTo(IncludesFormatVersion ? Version : null));

                Assert.That(record.OCMF.GatewayInformation,           Is.EqualTo(expected.GatewayInformation));
                Assert.That(record.OCMF.GatewaySerial,                Is.EqualTo(expected.GatewaySerial));
                Assert.That(record.OCMF.GatewayVersion,               Is.EqualTo(expected.GatewayVersion));
                Assert.That(record.OCMF.MeterVendor,                  Is.EqualTo(expected.MeterVendor));
                Assert.That(record.OCMF.MeterModel,                   Is.EqualTo(expected.MeterModel));
                Assert.That(record.OCMF.MeterSerial,                  Is.EqualTo(expected.MeterSerial));
                Assert.That(record.OCMF.MeterFirmware,                Is.EqualTo(expected.MeterFirmware));
                Assert.That(record.OCMF.TariffText,                   Is.EqualTo(expected.TariffText));
                Assert.That(record.OCMF.TariffTextInterpretation?.Code,          Is.EqualTo(expected.TariffProfile));
                Assert.That(record.OCMF.ControllerFirmwareVersion,    Is.EqualTo(expected.ControllerFirmwareVersion));
                Assert.That(record.OCMF.ChargePointIdentificationType,           Is.EqualTo(expected.ChargePointIdentificationType));
                Assert.That(record.OCMF.ChargePointIdentification,    Is.EqualTo(expected.ChargePointIdentification));

                Assert.That(record.OCMF.LossCompensation?.Name,           Is.EqualTo(expected.LossCompensationName));
                Assert.That(record.OCMF.LossCompensation?.Identification, Is.EqualTo(expected.LossCompensationId));
                Assert.That(record.OCMF.LossCompensation?.Resistance,     Is.EqualTo(expected.LossCompensationOhms));
                Assert.That(record.OCMF.LossCompensation?.Unit,           Is.EqualTo(expected.LossCompensationOhms.HasValue ? "mOhm" : null));

                #endregion

                #region Who was charging

                Assert.That(session.AuthorizationStart?.Id,                        Is.EqualTo(expected.IdentificationData));
                Assert.That(session.AuthorizationStart?.Type,                      Is.EqualTo(expected.IdentificationType));
                Assert.That(session.AuthorizationStart?.IdentificationStatus,      Is.EqualTo(expected.IdentificationStatus));
                Assert.That(session.AuthorizationStart?.IdentificationStatusText,  Is.EqualTo(expected.IdentificationStatusText));
                Assert.That(session.AuthorizationStart?.IdentificationLevel,       Is.EqualTo(expected.IdentificationLevel));
                Assert.That(session.AuthorizationStart?.IdentificationFlags,       Is.EqualTo(new[] { "RFID_PLAIN", "OCPP_AUTH_TLS" }));

                #endregion

                #region Where they were charging

                Assert.That(session.ChargingStationId,  Is.EqualTo(expected.ChargingStationId));
                Assert.That(session.EVSEId,             Is.EqualTo(expected.EVSEId));
                Assert.That(session.ConnectorId,        Is.EqualTo(expected.ConnectorId));

                // "CF" is signed, so it goes onto the charging station — but only
                // where the document actually named one.
                if (expected.ControllerFirmwareVersion is not null &&
                    expected.ChargingStationId         is not null)
                {
                    Assert.That(session.ChargingStation?.Firmware?.Version,      Is.EqualTo(expected.ControllerFirmwareVersion));
                    Assert.That(record.ChargingStations[0].Firmware?.Version,    Is.EqualTo(expected.ControllerFirmwareVersion));
                }

                if (expected.LossCompensationOhms.HasValue)
                {
                    Assert.That(session.Connector?.Cable?.LossCompensation,    Is.EqualTo(expected.LossCompensationName));
                    Assert.That(session.Connector?.Cable?.LossCompensationId,  Is.EqualTo(expected.LossCompensationId?.ToString()));
                    Assert.That(session.Connector?.Cable?.Resistance,          Is.EqualTo(expected.LossCompensationOhms));
                    Assert.That(session.Connector?.Cable?.ResistanceUnit,      Is.EqualTo("mOhm"));
                }

                #endregion

                #region What the tariff came to

                Assert.That(session.TariffId,  Is.EqualTo(expected.TariffText));

                if (expected.TariffText is null)
                {
                    Assert.That(record.ChargingTariffs,   Is.Empty);
                    Assert.That(session.ChargingTariffs,  Is.Empty);
                }
                else
                {
                    Assert.That(record.ChargingTariffs[0].Id,        Is.EqualTo(expected.TariffText));
                    Assert.That(record.ChargingTariffs[0].Currency,  Is.EqualTo("EUR"));
                    Assert.That(record.ChargingTariffs[0].Elements,  Is.Not.Empty);

                    // The record and the session point at the same tariff rather
                    // than at two equal ones, because they are the same tariff.
                    Assert.That(session.ChargingTariffs[0],          Is.SameAs(record.ChargingTariffs[0]));
                }

                #endregion

                #region ..., and what the meter measured

                var measurement = session.Measurements[0];

                Assert.That(measurement.EnergyMeterId,  Is.EqualTo(expected.MeterSerial));
                Assert.That(measurement.OBIS,           Is.EqualTo(expected.OBIS));
                Assert.That(measurement.Unit,           Is.EqualTo(expected.Unit));
                Assert.That(measurement.CurrentType,    Is.EqualTo("AC"));
                Assert.That(measurement.Values,         Has.Count.EqualTo(2));

                var values = measurement.Values.Cast<OCMFMeasurementValue>().ToArray();

                Assert.That(values.Select(value => value.Timestamp),        Is.EqualTo(new[] { expected.BeginTimestamp, expected.EndTimestamp }));
                Assert.That(values.Select(value => value.Value),            Is.EqualTo(new[] { expected.BeginValue,     expected.EndValue }));
                Assert.That(values.Select(value => value.Pagination),       Is.EqualTo(new[] { expected.Pagination,     expected.Pagination }));
                Assert.That(values.Select(value => value.TransactionType),  Is.EqualTo(new[] { OCMFTransactionType.Transaction, OCMFTransactionType.Transaction }));
                Assert.That(values.Select(value => value.ErrorIndex),       Is.EqualTo(new Decimal?[] { expected.ErrorIndex, expected.ErrorIndex }));
                Assert.That(values.Select(value => value.Result?.Status),   Is.EqualTo(new[] { VerificationResult.ValidSignature, VerificationResult.ValidSignature }));
                Assert.That(values.Select(value => value.Document.Raw),     Is.EqualTo(new[] { testData.Document, testData.Document }));

                // A cumulated loss of zero is not shown, because a compensation
                // of nothing did not happen.
                Assert.That(values.Select(value => value.CumulatedLoss),    Is.EqualTo(new Decimal?[] { null, expected.CumulatedLoss }));

                #endregion

            });

        }

        #endregion

        #region BothWaysOfNamingAChargePointAreExercised()

        /// <summary>
        /// The generated documents between them name a charge point both ways OCMF
        /// allows — by EVSE identification and by charge box plus connector.
        ///
        /// Without this the version tests could all land on one of the two and
        /// leave the other read by nothing, which is exactly the gap they exist to
        /// close.
        /// </summary>
        [Test]
        public void BothWaysOfNamingAChargePointAreExercised()

            => Assert.That(
                   OCMFVersionTestData.SupportedVersions.
                       Select(version => OCMFVersionTestData.Create(version, true).Expected.ChargePointIdentificationType).
                       Where (type    => type is not null).
                       Distinct().
                       Order(),
                   Is.EqualTo(new[] { "CBIDC", "EVSEID" })
               );

        #endregion

        #region ASignedChargeBoxIdentificationNamesTheChargingStation()

        /// <summary>
        /// "CT":"CBIDC" means "CI" holds a charge box identification and a
        /// connector, and that charge box is a charging station the record can
        /// name — so the signed controller firmware belongs on it.
        /// </summary>
        [Test]
        public void ASignedChargeBoxIdentificationNamesTheChargingStation()
        {

            var testData  = OCMFVersionTestData.Create("1.3", true, "CBIDC");
            var record    = Parse(testData);

            Assert.Multiple(() => {

                Assert.That(record.ChargingSessions[0].ChargingStation?.Id,        Is.EqualTo(testData.Expected.ChargingStationId));
                Assert.That(record.ChargingSessions[0].ChargingStation?.Firmware?.Version,  Is.EqualTo(testData.Expected.ControllerFirmwareVersion));
                Assert.That(record.ChargingStations[0].Id,                         Is.EqualTo(testData.Expected.ChargingStationId));
                Assert.That(record.ChargingStations[0].Firmware?.Version,          Is.EqualTo(testData.Expected.ControllerFirmwareVersion));

            });

        }

        #endregion

        #region TheSignedFirmwareBeatsTheContainersFirmware()

        /// <summary>
        /// A container that names the charging station and claims a firmware
        /// version for it, next to a document whose meter signed a different one.
        ///
        /// The signed one wins, and the container's own object is left as it was:
        /// what the container said is still what the container said, and a reader
        /// comparing the two must be able to see that they disagreed.
        /// </summary>
        [Test]
        public void TheSignedFirmwareBeatsTheContainersFirmware()
        {

            var testData          = OCMFVersionTestData.Create("1.4", false, "EVSEID");

            var containerStation  = new ChargingStation(
                                        "unsigned-container-station",
                                        Firmware:  new Firmware("unsigned-container-firmware"),
                                        EVSEs:     [ new EVSE(testData.Expected.EVSEId ?? "missing-evse-id") ]
                                    );

            var containerInfos    = new ContainerInfos();
            containerInfos.AddChargingStation(containerStation);

            var record = Parse(testData, containerInfos);

            Assert.Multiple(() => {

                Assert.That(record.ChargingSessions[0].ChargingStationId,                   Is.EqualTo("unsigned-container-station"));
                Assert.That(record.ChargingSessions[0].EVSEId,                              Is.EqualTo(testData.Expected.EVSEId));
                Assert.That(record.ChargingSessions[0].ChargingStation?.Firmware?.Version,  Is.EqualTo(testData.Expected.ControllerFirmwareVersion));
                Assert.That(record.ChargingStations[0].Firmware?.Version,                   Is.EqualTo(testData.Expected.ControllerFirmwareVersion));

                Assert.That(containerStation.Firmware?.Version,                             Is.EqualTo("unsigned-container-firmware"));

            });

        }

        #endregion

        #region TheSignedCableDataBeatsTheContainersCableData()

        /// <summary>
        /// The container knows how long the cable is; the meter knows what it
        /// compensated for. Only the second is signed, and only the second may
        /// overwrite — the length survives, because the meter never spoke to it.
        /// </summary>
        [Test]
        public void TheSignedCableDataBeatsTheContainersCableData()
        {

            var testData        = OCMFVersionTestData.Create("1.2", true, "EVSEID");

            var containerInfos  = new ContainerInfos();

            containerInfos.AddEVSE(
                new EVSE(
                    testData.Expected.EVSEId ?? "missing-evse-id",
                    Connectors: [
                        new Connector(
                            "container-connector",
                            Cable: new Cable(
                                       Length:              7.5m,
                                       Resistance:          999,
                                       ResistanceUnit:      "uOhm",
                                       LossCompensation:    "unsigned-name",
                                       LossCompensationId:  "999"
                                   )
                        )
                    ]
                )
            );

            var connector = Parse(testData, containerInfos).ChargingSessions[0].Connector;

            Assert.Multiple(() => {

                Assert.That(connector?.Id,                          Is.EqualTo("container-connector"));

                // Unsigned, and the meter said nothing about it, so it stands.
                Assert.That(connector?.Cable?.Length,               Is.EqualTo(7.5m));

                // ..., and everything the meter did sign about the cable replaces
                // what the container claimed.
                Assert.That(connector?.Cable?.LossCompensation,     Is.EqualTo(testData.Expected.LossCompensationName));
                Assert.That(connector?.Cable?.LossCompensationId,   Is.EqualTo(testData.Expected.LossCompensationId?.ToString()));
                Assert.That(connector?.Cable?.Resistance,           Is.EqualTo(testData.Expected.LossCompensationOhms));
                Assert.That(connector?.Cable?.ResistanceUnit,       Is.EqualTo("mOhm"));

            });

        }

        #endregion


        #region (private, static) Parse(TestData, ContainerInfos = null)

        /// <summary>
        /// Read a generated OCMF document, with its public key.
        /// </summary>
        /// <param name="TestData">The generated test data.</param>
        /// <param name="ContainerInfos">What a surrounding container would have known.</param>
        private static OCMFChargeTransparencyRecord Parse(OCMFVersionTestData  TestData,
                                                          ContainerInfos?      ContainerInfos = null)
        {

            var result = new OCMFFormat(I18NDictionary.Default()).TryParse(
                             [ TestData.Document ],
                             TestData.PublicKeyBase64,
                             "base64",
                             ContainerInfos
                         );

            Assert.That(result, Is.InstanceOf<OCMFChargeTransparencyRecord>(), VerificationReport.Format(result));

            return (OCMFChargeTransparencyRecord) result;

        }

        #endregion

    }

}
