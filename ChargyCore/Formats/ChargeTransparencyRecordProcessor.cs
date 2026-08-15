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

using cloud.charging.open.chargy.Crypto;
using cloud.charging.open.chargy.Formats.Alfen;
using cloud.charging.open.chargy.Formats.BSM;
using cloud.charging.open.chargy.Formats.ChargePoint;
using cloud.charging.open.chargy.Formats.EDL40;
using cloud.charging.open.chargy.Formats.EMH;
using cloud.charging.open.chargy.Formats.GDF;
using cloud.charging.open.chargy.Formats.Mennekes;
using cloud.charging.open.chargy.Formats.OCMF;
using cloud.charging.open.chargy.Formats.PCDF;

#endregion

namespace cloud.charging.open.chargy.Formats
{

    /// <summary>
    /// Turns a parsed charge transparency record into a verified one.
    ///
    /// A format parser produces a record that is structurally complete but
    /// cryptographically unexamined: it says a charging station delivered this
    /// much energy, and nothing yet says whether that claim holds. This walks the
    /// record, connects each measurement to the energy meter that supposedly
    /// produced it, and hands every charging session to the verification method
    /// its own "@context" names.
    ///
    /// The session names its own method, which sounds circular but is not: the
    /// method decides only *how* to check the signature, never *whether* it
    /// matches. A record that named the wrong method would simply fail.
    /// </summary>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="ValidationRules">
    /// The plausibility rules to apply. A signature says the meter really
    /// reported this; it does not say the meter was right.
    /// </param>
    public class ChargeTransparencyRecordProcessor(I18NDictionary    I18N,
                                                   ValidationRules?  ValidationRules = null)
    {

        #region Properties

        /// <summary>The dictionary used to describe what went wrong.</summary>
        public I18NDictionary   I18N               { get; } = I18N;

        /// <summary>The plausibility rules to apply.</summary>
        public ValidationRules  ValidationRules    { get; } = ValidationRules ?? ValidationRules.Default;

        #endregion


        #region Process(Record)

        /// <summary>
        /// Resolve the references within a charge transparency record and verify
        /// every charging session in it.
        /// </summary>
        /// <param name="Record">A charge transparency record.</param>
        public ChargeTransparencyRecord Process(ChargeTransparencyRecord Record)
        {

            var energyMeters = CollectEnergyMeters(Record);

            EnergyMeter? LookupMeter(String meterId)
                => energyMeters.TryGetValue(meterId, out var meter) ? meter : null;

            foreach (var chargingSession in Record.ChargingSessions)
            {

                #region Connect each measurement and reading back to its session

                foreach (var measurement in chargingSession.Measurements)
                {

                    measurement.ChargingSession = chargingSession;

                    MeasurementValue? previousValue = null;

                    foreach (var measurementValue in measurement.Values)
                    {

                        measurementValue.Measurement    = measurement;

                        // Some formats chain each reading to the one before it, so
                        // that removing a reading from the middle is detectable.
                        measurementValue.PreviousValue  = previousValue;
                        previousValue                   = measurementValue;

                    }

                }

                #endregion

                var verificationResult = Verify(chargingSession, LookupMeter);

                #region A valid signature still has to be a plausible reading

                // The cryptography can only say that the meter really reported
                // this. Whether a single charging session plausibly delivered
                // 2.1 MWh is a different question, and one an EV driver is
                // entitled to have raised on their behalf.
                if (verificationResult.Status == SessionVerificationResult.ValidSignature &&
                    IsImplausible(chargingSession))
                {

                    Record.AddWarning(
                        new Warning(
                            I18N.GetMultilanguageText("InplausibleTotalEnergyMeasurementWarning"),
                            ValidationRules.TotalEnergy?.Level ?? SeverityLevel.Low
                        )
                    );

                    verificationResult.Status = SessionVerificationResult.InplausibleMeasurement;

                }

                #endregion

                chargingSession.VerificationResult = verificationResult;

            }

            Record.Status = Record.ChargingSessions.Count > 0 &&
                            Record.ChargingSessions.All(session => session.VerificationResult?.Status == SessionVerificationResult.ValidSignature)
                                ? SessionVerificationResult.ValidSignature
                                : Record.Status;

            return Record;

        }

        #endregion


        #region (private) IsImplausible      (ChargingSession)

        /// <summary>
        /// Whether the energy a charging session claims to have delivered is
        /// implausible.
        ///
        /// Only the total energy is judged, and only as the difference between
        /// the first and the last reading: that difference is what the EV driver
        /// is billed for, and it is the only number a plausibility rule can be
        /// stated about without knowing the meter's history.
        /// </summary>
        /// <param name="ChargingSession">A charging session.</param>
        private Boolean IsImplausible(ChargingSession ChargingSession)
        {

            foreach (var measurement in ChargingSession.Measurements)
            {

                if (measurement.Name != "ENERGY_TOTAL" ||
                    measurement.Values.Count < 2)
                {
                    continue;
                }

                var first = MeasurementValueInKWh(measurement, measurement.Values[0]);
                var last  = MeasurementValueInKWh(measurement, measurement.Values[^1]);

                if (first is null || last is null)
                    continue;

                if (ValidationRules.TotalEnergy?.IsViolatedBy(last.Value - first.Value) == true)
                    return true;

            }

            return false;

        }

        #endregion

        #region (private, static) MeasurementValueInKWh(Measurement, MeasurementValue)

        /// <summary>
        /// A meter reading in kWh, or null when the measurement does not say
        /// which unit it is in.
        ///
        /// A rule stated in kWh must never be compared against a number whose
        /// unit is unknown: that is how a perfectly ordinary charging session
        /// gets flagged as impossible, or an impossible one waved through.
        /// </summary>
        /// <param name="Measurement">A measurement.</param>
        /// <param name="MeasurementValue">One of its readings.</param>
        private static Decimal? MeasurementValueInKWh(Measurement       Measurement,
                                                      MeasurementValue  MeasurementValue)
        {

            var unit = Measurement.Unit?.Trim().ToLowerInvariant();

            if (unit == "kwh")
                return MeasurementValue.Value;

            // 30 is the DLMS/COSEM code for watt hours.
            if (unit == "wh" || Measurement.UnitEncoded == 30)
                return MeasurementValue.Value / 1000;

            return null;

        }

        #endregion

        #region (private) Verify             (ChargingSession, LookupMeter)

        /// <summary>
        /// Verify one charging session with the method its "@context" names.
        /// </summary>
        /// <param name="ChargingSession">A charging session.</param>
        /// <param name="LookupMeter">How to find the energy meter that carries the public key.</param>
        private SessionCryptoResult Verify(ChargingSession              ChargingSession,
                                           Func<String, EnergyMeter?>   LookupMeter)
        {

            ACrypt? method = null;

            foreach (var context in ChargingSession.JSONLDContext)
                switch (context)
                {

                    case AlfenFormat.SessionContext:
                        method = new AlfenCrypt01(I18N, LookupMeter);
                        break;

                    case OCMFFormat.SessionContext:
                        method = new OCMFCrypt01(I18N, LookupMeter);
                        break;

                    case EDL40Format.SessionContext:
                        method = new EDL40Crypt01(I18N, LookupMeter);
                        break;

                    case BSMFormat.SessionContext:
                        method = new BSMCrypt01(I18N, LookupMeter);
                        break;

                    case EMHCrypt01.SessionContext:
                        method = new EMHCrypt01(I18N, LookupMeter);
                        break;

                    case GDFCrypt01.SessionContext:
                        method = new GDFCrypt01(I18N, LookupMeter);
                        break;

                    case MennekesFormat.SessionContext:
                        method = new MennekesCrypt01(I18N, LookupMeter);
                        break;

                    case ChargePointFormat.SessionContext:
                        method = new ChargePointCrypt01(I18N, LookupMeter);
                        break;

                    case PCDFFormat.SessionContext:
                        method = new PCDFCrypt01(I18N, LookupMeter);
                        break;

                }

            if (method is null)
                return new SessionCryptoResult(
                           SessionVerificationResult.UnknownSessionFormat,
                           I18N.GetMultilanguageText("UnknownOrInvalidChargingSessionFormat")
                       );

            try
            {
                return method.VerifyChargingSession(ChargingSession);
            }
            catch (Exception exception)
            {
                return new SessionCryptoResult(
                           SessionVerificationResult.InvalidSignature,
                           I18N.GetMultilanguageText("Verification_UnexpectedError"),
                           Exception: exception
                       );
            }

        }

        #endregion

        #region (private, static) CollectEnergyMeters(Record)

        /// <summary>
        /// Index every energy meter in the record by its identification.
        ///
        /// A meter may be declared at any level — on the record, on an operator,
        /// on a pool, on a station or on an EVSE — because that is how the
        /// installation it describes is actually shaped.
        /// </summary>
        /// <param name="Record">A charge transparency record.</param>
        private static Dictionary<String, EnergyMeter> CollectEnergyMeters(ChargeTransparencyRecord Record)
        {

            var energyMeters = new Dictionary<String, EnergyMeter>(StringComparer.Ordinal);

            void Add(IEnumerable<EnergyMeter> Meters)
            {
                foreach (var meter in Meters)
                    energyMeters.TryAdd(meter.Id, meter);
            }

            void FromChargingStation(ChargingStation ChargingStation)
            {

                Add(ChargingStation.EnergyMeters);

                foreach (var evse in ChargingStation.EVSEs)
                    Add(evse.EnergyMeters);

            }

            foreach (var chargingStationOperator in Record.ChargingStationOperators)
            {

                foreach (var chargingPool in chargingStationOperator.ChargingPools)
                    foreach (var chargingStation in chargingPool.ChargingStations)
                        FromChargingStation(chargingStation);

                foreach (var chargingStation in chargingStationOperator.ChargingStations)
                    FromChargingStation(chargingStation);

            }

            foreach (var chargingPool in Record.ChargingPools)
                foreach (var chargingStation in chargingPool.ChargingStations)
                    FromChargingStation(chargingStation);

            foreach (var chargingStation in Record.ChargingStations)
                FromChargingStation(chargingStation);

            return energyMeters;

        }

        #endregion


    }

}
