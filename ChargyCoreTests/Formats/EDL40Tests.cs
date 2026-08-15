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

using cloud.charging.open.chargy.Formats.EDL40;

#endregion

namespace cloud.charging.open.chargy.tests.Formats
{

    /// <summary>
    /// Tests for the EDL40 and ISA-EDL40 formats: the SML parser, the 320 byte
    /// signature block, and the charge transparency records built from them.
    /// </summary>
    [TestFixture]
    public partial class EDL40Tests : AChargyTests
    {

        #region ParsesAnEDL40Document()

        /// <summary>
        /// An EDL40 document, taken straight out of its XML file.
        ///
        /// This checks the parser against the values the SML message plainly
        /// contains — the meter, the reading, the page number — before any
        /// signature is involved, because a signature failure cannot tell you
        /// which of the two halves went wrong.
        /// </summary>
        [Test]
        public void ParsesAnEDL40Document()
        {

            var parsed = AEDL40SignatureData.Parse(
                             SignedDataOf(ReadTextFixture("EDL40/edl-40-01.xml"))
                         );

            Assert.Multiple(() => {

                Assert.That(parsed.Variant,             Is.EqualTo(EDL40Variant.EDL_40_P));
                Assert.That(parsed.SignedData,          Has.Length.EqualTo(320));
                Assert.That(parsed.Pagination,          Is.EqualTo(33));

                Assert.That(parsed,                     Is.InstanceOf<EDL40PSignatureData>());
                Assert.That(((EDL40PSignatureData) parsed).MeterValue,
                                                        Is.EqualTo(new System.Numerics.BigInteger(3275)));

                Assert.That(parsed.ServerId,            Is.EqualTo(new Byte[] { 0x09, 0x01, 0x45, 0x53, 0x59, 0x11, 0x03, 0x95, 0x69, 0x40 }));

            });

        }

        #endregion

        #region ParsesAnISADocument()

        /// <summary>
        /// An ISA document carries a start and a stop reading in one signed block,
        /// and says which of the two it concludes.
        /// </summary>
        [Test]
        public void ParsesAnISADocument()
        {

            var parsed = AEDL40SignatureData.Parse(
                             SignedDataOf(ReadTextFixture("ISA_EDL40/isa-edl-40p-ok.xml"))
                         );

            Assert.Multiple(() => {

                Assert.That(parsed.Variant,     Is.EqualTo(EDL40Variant.ISA_EDL_40_P));
                Assert.That(parsed.SignedData,  Has.Length.EqualTo(320));
                Assert.That(parsed,             Is.InstanceOf<ISAEDL40SignatureData>());

            });

            var isa = (ISAEDL40SignatureData) parsed;

            Assert.Multiple(() => {

                Assert.That(isa.StartECValue,     Is.EqualTo(new System.Numerics.BigInteger(0x0f50e354)));
                Assert.That(isa.ActualECValue,    Is.EqualTo(new System.Numerics.BigInteger(0x0f544b92)));
                Assert.That(isa.ListNameContext,  Is.EqualTo("STOP"));

            });

        }

        #endregion


        #region EDL40DocumentsVerifyAgainstTheirOwnPublicKey(Fixture)

        /// <summary>
        /// Every EDL40 document verifies against the public key filed next to it.
        ///
        /// These fixtures never reach the pipeline — they declare no signed data
        /// format, so the container cannot tell whose data it is carrying — but the
        /// signatures in them are real, and they are the only coverage the plain
        /// EDL40 layout has on the secp192r1 path.
        /// </summary>
        /// <param name="Fixture">A fixture path relative to "TestData".</param>
        [TestCase("EDL40/edl-40-01.xml")]
        [TestCase("EDL40/edl-40-02.xml")]
        [TestCase("EDL40/edl-40-03.xml")]
        public void EDL40DocumentsVerifyAgainstTheirOwnPublicKey(String Fixture)
        {

            var xml       = ReadTextFixture(Fixture);

            var document  = EDL40Document.Verify(
                                AEDL40SignatureData.Parse(SignedDataOf(xml)),
                                PublicKeyOf(xml),
                                SignedDataOf(xml)
                            );

            Assert.Multiple(() => {

                Assert.That(document.Variant,           Is.EqualTo(EDL40Variant.EDL_40_P));
                Assert.That(document.Curve,             Is.EqualTo(ECCurve.secp192r1));
                Assert.That(document.HashValue,         Has.Length.EqualTo(48));  // 24 bytes, the width of the curve
                Assert.That(document.ValidationStatus,  Is.EqualTo(VerificationResult.ValidSignature));

            });

        }

        #endregion


        #region SMLWithinASAFEContainerBecomesAnOrdinaryChargingSession()

        /// <summary>
        /// A SAFE container carrying SML_EDL40_P, all the way to a verified
        /// charging session.
        ///
        /// The container is what makes this possible at all: an SML message
        /// carries no public key, so the whole reading rests on the key the
        /// container supplies alongside it.
        /// </summary>
        [Test]
        public async Task SMLWithinASAFEContainerBecomesAnOrdinaryChargingSession()
        {

            var result = await VerifyFixtures([ "EDL40plus/edl40plus-sml-within-safe-xml-container-01.xml" ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var record           = (ChargeTransparencyRecord) result;

            Assert.That(record.ChargingSessions, Has.Count.EqualTo(1));

            var chargingSession  = record.ChargingSessions[0];

            Assert.Multiple(() => {

                Assert.That(chargingSession.JSONLDContext,                   Does.Contain(EDL40Format.SessionContext));
                Assert.That(chargingSession.VerificationResult?.Status,      Is.EqualTo(SessionVerificationResult.ValidSignature));
                Assert.That(chargingSession.Measurements,                    Has.Count.EqualTo(1));
                Assert.That(chargingSession.Measurements[0].Values,          Has.Count.EqualTo(2));

                Assert.That(chargingSession.Measurements[0].Values.Select(value => value.Result?.Status),
                            Is.EqualTo(new[] {
                                VerificationResult.ValidSignature,
                                VerificationResult.ValidSignature
                            }));

            });

        }

        #endregion

        #region ISAWithinASAFEContainerBecomesAStartAndAStopReading()

        /// <summary>
        /// One ISA document is already a whole charging session: the block it
        /// signs holds both the reading the session started at and the one it
        /// ended at.
        ///
        /// The two numbers are what an EV driver is billed the difference of, so
        /// they are checked as quantities of energy rather than as raw meter
        /// values — the meter's own scale factor sits between the two.
        /// </summary>
        [Test]
        public async Task ISAWithinASAFEContainerBecomesAStartAndAStopReading()
        {

            var result = await VerifyFixtures([ "ISA_EDL40/isa-edl-40p-ok.xml" ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var values = ((ChargeTransparencyRecord) result).ChargingSessions[0].Measurements[0].Values;

            Assert.Multiple(() => {

                Assert.That(values,           Has.Count.EqualTo(2));
                Assert.That(values[0].Value,  Is.EqualTo(25695.9316m).Within(0.0001m));
                Assert.That(values[1].Value,  Is.EqualTo(25718.261m). Within(0.0001m));

            });

        }

        #endregion


        #region ATamperedISADocumentDoesNotVerify(Fixture)

        /// <summary>
        /// The two ISA fixtures where something really was changed.
        ///
        /// One was filed with a public key that does not belong to the signature,
        /// the other with readings that do not belong to it. Both are what an EV
        /// driver actually needs protection from, and both have to come out as an
        /// invalid signature rather than as a parse error — the file is perfectly
        /// well-formed, it is the claim inside it that does not hold.
        /// </summary>
        /// <param name="Fixture">A fixture path relative to "TestData".</param>
        [TestCase("ISA_EDL40/isa-edl-40p-sign-fail.xml")]
        [TestCase("ISA_EDL40/isa-edl-40p-data-fail.xml")]
        public async Task ATamperedISADocumentDoesNotVerify(String Fixture)
        {

            var result = await VerifyFixtures([ Fixture ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var chargingSession = ((ChargeTransparencyRecord) result).ChargingSessions[0];

            Assert.Multiple(() => {

                Assert.That(chargingSession.VerificationResult?.Status,
                            Is.EqualTo(SessionVerificationResult.InvalidSignature));

                Assert.That(chargingSession.Measurements.SelectMany(measurement => measurement.Values).
                                            All(value => value.Result?.Status == VerificationResult.ValidSignature),
                            Is.False);

            });

        }

        #endregion

        #region AnUnsignedContextAttributeCannotOverruleTheSignature()

        /// <summary>
        /// "isa-edl-40p-veri-fail" is byte-for-byte the same document as
        /// "isa-edl-40p-ok" — same signed data, same public key. The only thing
        /// that differs is the container's "context" attribute, which says
        /// "Data not verified".
        ///
        /// That attribute is unsigned prose written by whoever produced the file,
        /// and the signature is a statement by the meter. So the honest answer is
        /// the same for both files, and this test exists to keep it that way: a
        /// port that let the wrapper's opinion downgrade a signature would be
        /// letting the party who wrote the file decide what the meter said.
        /// </summary>
        [Test]
        public async Task AnUnsignedContextAttributeCannotOverruleTheSignature()
        {

            var good      = await VerifyFixtures([ "ISA_EDL40/isa-edl-40p-ok.xml"        ]);
            var doubted   = await VerifyFixtures([ "ISA_EDL40/isa-edl-40p-veri-fail.xml" ]);

            Assert.Multiple(() => {

                Assert.That(SignedDataOf(ReadTextFixture("ISA_EDL40/isa-edl-40p-veri-fail.xml")),
                            Is.EqualTo(SignedDataOf(ReadTextFixture("ISA_EDL40/isa-edl-40p-ok.xml"))),
                            "The two fixtures are supposed to carry identical signed data!");

                Assert.That(VerificationReport.Format(doubted),
                            Is.EqualTo(VerificationReport.Format(good)));

                Assert.That(((ChargeTransparencyRecord) doubted).ChargingSessions[0].VerificationResult?.Status,
                            Is.EqualTo(SessionVerificationResult.ValidSignature));

            });

        }

        #endregion

        #region AnIncompleteTransactionStillCarriesAValidSignature(Fixture)

        /// <summary>
        /// A "Transaction.Begin" and a "Transaction.Update" record.
        ///
        /// The SAFE reference software calls both of them invalid, and their
        /// signatures nevertheless hold: what is missing is not evidence but the
        /// end of the charging session. Both readings of such a record are the
        /// same, so the session delivered nothing — which is exactly what a
        /// snapshot taken at the start of one should say.
        ///
        /// Note that neither ChargyCore.TS nor this port raises a warning about it.
        /// A record that verifies and delivers zero energy is reported as verified,
        /// and telling those two things apart is left to the caller.
        /// </summary>
        /// <param name="Fixture">A fixture path relative to "TestData".</param>
        [TestCase("ISA_EDL40/isa-edl-40p-begin-fail.xml")]
        [TestCase("ISA_EDL40/isa-edl-40p-update-fail.xml")]
        public async Task AnIncompleteTransactionStillCarriesAValidSignature(String Fixture)
        {

            var result = await VerifyFixtures([ Fixture ]);

            Assert.That(result, Is.InstanceOf<ChargeTransparencyRecord>(), VerificationReport.Format(result));

            var chargingSession  = ((ChargeTransparencyRecord) result).ChargingSessions[0];
            var values           = chargingSession.Measurements[0].Values;

            Assert.Multiple(() => {

                Assert.That(chargingSession.VerificationResult?.Status,  Is.EqualTo(SessionVerificationResult.ValidSignature));
                Assert.That(values,                                      Has.Count.EqualTo(2));
                Assert.That(values[^1].Value - values[0].Value,          Is.EqualTo(0m));

            });

        }

        #endregion


        #region (private, static) SignedDataOf(XML)

        /// <summary>
        /// The contents of the "signedData" element of a SAFE XML container.
        ///
        /// The parser tests deliberately reach past the container: an EDL40
        /// document has to parse on its own, so that a fault in the container and
        /// a fault in the SML message never look alike.
        /// </summary>
        /// <param name="XML">A SAFE XML container.</param>
        private static String SignedDataOf(String XML)
        {

            var match = SignedDataRegex().Match(XML);

            Assert.That(match.Success, Is.True, "The fixture holds no signed data!");

            return match.Groups[1].Value.Trim();

        }

        [GeneratedRegex(@"<signedData[^>]*>([\s\S]*?)</signedData>", RegexOptions.IgnoreCase)]
        private static partial Regex SignedDataRegex();

        #endregion

        #region (private, static) PublicKeyOf(XML)

        /// <summary>
        /// The contents of the "publicKey" element of a SAFE XML container.
        /// </summary>
        /// <param name="XML">A SAFE XML container.</param>
        private static String PublicKeyOf(String XML)
        {

            var match = PublicKeyRegex().Match(XML);

            Assert.That(match.Success, Is.True, "The fixture holds no public key!");

            return match.Groups[1].Value.Trim();

        }

        [GeneratedRegex(@"<publicKey[^>]*>([\s\S]*?)</publicKey>", RegexOptions.IgnoreCase)]
        private static partial Regex PublicKeyRegex();

        #endregion

    }

}
