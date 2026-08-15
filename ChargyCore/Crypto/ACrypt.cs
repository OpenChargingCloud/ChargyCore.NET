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

#endregion

namespace cloud.charging.open.chargy.Crypto
{

    /// <summary>
    /// The base class of every charge transparency data format that verifies
    /// cryptographic signatures.
    ///
    /// A subclass reassembles the exact byte buffer the energy meter signed,
    /// hashes it, and checks the signature against the meter's public key. The
    /// buffer layout is the part that differs per vendor; everything around it is
    /// here.
    /// </summary>
    /// <param name="Description">The name of the data format, e.g. "EMHCrypt01".</param>
    /// <param name="I18N">The dictionary used to describe what went wrong.</param>
    /// <param name="MeterLookup">
    /// How to find the energy meter a measurement claims to come from.
    ///
    /// The meter is what carries the public key, and it lives elsewhere in the
    /// charge transparency record than the measurement does — so a measurement
    /// cannot be verified in isolation, only in the context of the record that
    /// declared its meter.
    /// </param>
    public abstract class ACrypt(String                    Description,
                                 I18NDictionary            I18N,
                                 Func<String, EnergyMeter?>?  MeterLookup = null)
    {

        #region Properties

        /// <summary>The name of the data format.</summary>
        public String          Description    { get; } = Description;

        /// <summary>The dictionary used to describe what went wrong.</summary>
        public I18NDictionary  I18N           { get; } = I18N;

        #endregion

        #region (protected) GetEnergyMeter(EnergyMeterId)

        /// <summary>
        /// Find the energy meter a measurement claims to come from.
        /// </summary>
        /// <param name="EnergyMeterId">The identification of an energy meter.</param>
        protected EnergyMeter? GetEnergyMeter(String? EnergyMeterId)

            => EnergyMeterId is not null && MeterLookup is not null
                   ? MeterLookup(EnergyMeterId)
                   : null;

        #endregion

        #region Elliptic curves

        /// <summary>NIST/ANSI X9.62 secp192r1, used by the EMH and GDF energy meters.</summary>
        protected static ECCurveVerifier Curve192r1
            => ECCurveVerifier.secp192r1;

        /// <summary>Koblitz curve secp224k1, used by the legacy ChargePoint and Alfen data.</summary>
        protected static ECCurveVerifier Curve224k1
            => ECCurveVerifier.secp224k1;

        /// <summary>NIST/ANSI X9.62 secp256r1.</summary>
        protected static ECCurveVerifier Curve256r1
            => ECCurveVerifier.secp256r1;

        /// <summary>NIST/ANSI X9.62 secp384r1.</summary>
        protected static ECCurveVerifier Curve384r1
            => ECCurveVerifier.secp384r1;

        /// <summary>NIST/ANSI X9.62 secp521r1.</summary>
        protected static ECCurveVerifier Curve521r1
            => ECCurveVerifier.secp521r1;

        #endregion


        #region VerifyChargingSession(ChargingSession)

        /// <summary>
        /// Verify an entire charging session.
        /// </summary>
        /// <param name="ChargingSession">A charging session.</param>
        public abstract SessionCryptoResult VerifyChargingSession(ChargingSession ChargingSession);

        #endregion

        #region VerifyMeasurement    (MeasurementValue)

        /// <summary>
        /// Verify a single signed energy meter reading.
        /// </summary>
        /// <param name="MeasurementValue">A signed energy meter reading.</param>
        public abstract CryptoResult VerifyMeasurement(MeasurementValue MeasurementValue);

        #endregion

        #region TraceMeasurement     (MeasurementValue)

        /// <summary>
        /// Verify a single signed energy meter reading and describe, field by
        /// field, which bytes went into the signature.
        ///
        /// This is what lets a user interface show an EV driver why a measurement
        /// is or is not valid, rather than only that it is not. The default
        /// implementation verifies without describing anything, so that a format
        /// which has no field layout to show does not have to pretend otherwise.
        /// </summary>
        /// <param name="MeasurementValue">A signed energy meter reading.</param>
        public virtual VerificationTrace TraceMeasurement(MeasurementValue MeasurementValue)

            => new () {
                   Description  = Description,
                   Result       = VerifyMeasurement(MeasurementValue)
               };

        #endregion


        #region (protected) AddVerificationError(CryptoResult, ReasonKey, Detail = null)

        /// <summary>
        /// Record why a verification step failed.
        ///
        /// The reason is kept as a stable, language-neutral key plus an optional
        /// technical detail, so a user interface can branch on the reason and
        /// still show a translated message. The detail is deliberately not
        /// localized: it is meant for a bug report, not for an EV driver.
        /// </summary>
        /// <param name="CryptoResult">The result to record the failure in.</param>
        /// <param name="ReasonKey">The i18n key of the reason.</param>
        /// <param name="Detail">An optional technical detail.</param>
        protected void AddVerificationError(CryptoResult  CryptoResult,
                                            String        ReasonKey,
                                            Object?       Detail = null)
        {

            CryptoResult.AddError(
                new Error(
                    I18N.GetMultilanguageText(ReasonKey),
                    SeverityLevel.High,
                    ReasonKey,
                    Detail switch {
                        Exception exception  => exception.Message,
                        String    text       => text,
                        _                    => null
                    }
                )
            );

        }

        #endregion

        #region (protected) AddVerificationError(SessionCryptoResult, ReasonKey, Detail = null)

        /// <summary>
        /// Record why the verification of a charging session failed.
        /// </summary>
        /// <param name="SessionCryptoResult">The result to record the failure in.</param>
        /// <param name="ReasonKey">The i18n key of the reason.</param>
        /// <param name="Detail">An optional technical detail.</param>
        protected void AddVerificationError(SessionCryptoResult  SessionCryptoResult,
                                            String               ReasonKey,
                                            Object?              Detail = null)
        {

            SessionCryptoResult.AddError(
                new Error(
                    I18N.GetMultilanguageText(ReasonKey),
                    SeverityLevel.High,
                    ReasonKey,
                    Detail switch {
                        Exception exception  => exception.Message,
                        String    text       => text,
                        _                    => null
                    }
                )
            );

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this verification method.
        /// </summary>
        public override String ToString()

            => Description;

        #endregion


    }

}
