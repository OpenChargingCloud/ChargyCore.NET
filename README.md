# Chargy Core .NET

Chargy Core is a transparency software library for the validation of secure and transparent e-mobility charging processes, as defined by the *German Calibration Law ("Eichrecht")* in combination with the [Alternative Fuels Infrastructure Regulation (AFIR)](https://transport.ec.europa.eu/transport-themes/clean-transport/alternative-fuels-sustainable-mobility-europe/alternative-fuels-infrastructure_en) and the new [Measuring instruments (MID)](https://single-market-economy.ec.europa.eu/single-market/goods/european-standards/harmonised-standards/measuring-instruments-mid_en) of the European Commission and the [European Digital Quality Infrastructure](https://www.qi-digital.de/en/). The software allows you to verify the cryptographic signatures of energy measurements within charge detail records and comes with a couple of useful extentions to simplify the entire process for endusers and operators.

ChargyCore.NET is the C# / .NET 10 port of [ChargyCore.TS](https://github.com/OpenChargingCloud/ChargyCore.TS), for server-side verification, backend integrations and .NET-based applications.


## Benefits of Chargy

1. Chargy comes with __*meta data*__. True charging transparency is more than just signed smart meter values. Chargy allows you to group multiple signed smart meter values to entire charging sessions and to add additional meta data like EVSE information, geo coordinates, tariffs, ... within your backend in order to improve the user experience for the ev drivers.
2. Chargy is __*secure*__. Chargy implements a public key infrastructure for managing certificates of smart meters, EVSEs, charging stations, charging station operators and e-mobility providers. By this the ev driver will always retrieve the correct public key to verify a charging process automatically and without complicated manual lookups in external databases.
3. Chargy is __*Open Source*__. In contrast to other vendors in e-mobility, we belief that true transparency is only trustworthy if the entire process and the required software is open and reusable under a fair copyleft license (AGPL).
4. Chargy is __*open for your contributions*__. We currently support adapters for the protocols of different charging station vendors like chargeIT mobility, ABL (OCMF), chargepoint. The certification at the Physikalisch-Technische Bundesanstalt (PTB) is provided by chargeIT mobility. If you want to add your protocol or a protocol adapter feel free to read the contributor license agreement and to send us a pull request.


## Supported Charge Transparency Data Formats

ChargyCore supports a broad range of charge transparency data formats used by charging stations, energy meters, backend systems, and invoice-related exports.

Currently supported formats include:
- **Alfen** charge transparency data
- **Bauer** energy meter data (2 format variants)
- **ChargePoint** transparency data (2 format variants)
- **EDL40** and **ISA-EDL40 SML** data
- **EMH** energy meter data
- **Mennekes** XML
- **OCMF**, versions v1.1 to v1.4
  - Bonner Eichrechtstage **Tariff Text** Extensions
  - EdDSA support: Ed25519 and Ed448
  - Post-Quantum Cryptography support: ML-DSA-44, ML-DSA-65, ML-DSA-87
- **Porsche Charging Data Format (PCDF)**

Detailed per-format documentation (data structures, signed payloads and signature verification) is available in [`documentation/`](documentation/README.md).

The long-standing, consumer-oriented CTR data model is described in the draft
[Charge Transparency Record format specification](documentation/CTR_Format.md).
CTR connects signed technical evidence with charging sessions, tariffs, costs,
and PDF/A-3 invoices so EV drivers can assess a bill without having to understand
each meter vendor's cryptographic format.
Additional draft CTR extension specifications are available for
[legally relevant log messages](documentation/CTR_Legally_Relevant_Log_Messages.md)
and [time synchronization sources](documentation/CTR_Time_Synchronization_Sources.md),
including event pagination, hash chaining, NTS source information, clock
quality, and significant time-source changes.


## Supported Data Representations

ChargyCore accepts multiple data representations in order to simplify the validation of individual charge transparency files as well as larger collections of transparency data, for example data sets attached to monthly invoices or exported from backend systems.

Supported representations include:
- **Plain Files** containing a single charge transparency data set.
- **chargeIT Container Format**, a JSON-based container format for a single charging session (2 format variants).
- **Chargy Container Format**, a JSON-based container format for multiple charging sessions.
- **SAFE XML Container Format**, an XML-based container format for a single charging session, optionally enriched with additional Chargy metadata about the charging session.
- **PTB Container Format**, a JSON-based container format for a single charging session.
- **Archive formats** such as ***tar, ZIP, tar.gz***, and similar formats that combine or compress multiple charge transparency files.
- **QR-Code images**, such as ***PNG, JPG, JPEG or SVG files***, where the QR-Code represents a charge transparency data set.
- **PDF/A-3** files transporting a charge transparency file as an embedded additional data stream.

This allows applications to pass transparency data to ChargyCore in the form in which it was originally received, without having to manually unpack, decode, or normalize every file beforehand.


## Project Status

ChargyCore.NET is currently being ported from ChargyCore.TS.
The porting strategy, the solution layout and the phase plan are documented in
[`PORTING_PLAN.md`](PORTING_PLAN.md).

| Phase | Scope | Status |
|---|---|---|
| 0 | Repository, solution, projects, test fixtures | ✅ **done** |
| 1 | ChargyLib, data structures, i18n, validation rules | ✅ **done** |
| 2 | Cryptography (ECDSA, EdDSA, ML-DSA), ACrypt, signed JSON | ✅ **done** |
| 3 | Content format detection, archives, PDF/A-3, QR codes | 🚧 next |
| 4 | The charge transparency data formats | ⬜ planned |
| 5 | Charge Transparency Live Link, URL resolution | ⬜ planned |
| 6 | Documentation, samples, packaging | ⬜ planned |

Phase 1 in detail:

| Component | Status |
|---|---|
| `ChargyLib` — hex, OBIS, timestamps, signature buffer writers | ✅ **done** |
| Verification results, severity levels, `Warning`, `Error`, `CryptoResult` | ✅ **done** |
| `I18NDictionary` — 286 messages, language fallback | ✅ **done** |
| `ValidationRules` — plausibility rules | ✅ **done** |
| `ChargeTransparencyRecord`, `ChargingSession`, `Measurement`, `MeasurementValue` | ✅ **done** |
| `Signature`, `SignatureInfos`, `PublicKey`, `PublicKeySignature`, `OIDInfo` | ✅ **done** |
| `Address`, contacts, device info, legal compliance | ✅ **done** |
| `EnergyMeter`, `EVSE`, `ChargingStation`, `ChargingPool`, `ChargingStationOperator` | ✅ **done** |
| `ChargingTariff`, `ParkingTariff` and the OCPI tariff elements | ✅ **done** |
| Costs, authorization, parking, legally relevant log messages | ✅ **done** |
| `SimpleURL`, `ChargeTransparencyLiveLink`, `FileInfo` | ✅ **done** |
| Resolved object references in `ChargingSession` | ✅ **done** |
| `EMobilityProvider`, `Contract`, and the full record collections | ✅ **done** |
| The ported `data-structures` / `chargyInterfaces` / `Timestamps` test cases | ✅ **done** |


## Related projects

- [Chargy Core TS](https://github.com/OpenChargingCloud/ChargyCore.TS), the TypeScript implementation this port is derived from
- [Chargy Web App](https://github.com/OpenChargingCloud/ChargyWebApp/) (node.js), live demo at [https://chargy.charging.cloud](https://chargy.charging.cloud)
- [Chargy Desktop App](https://github.com/OpenChargingCloud/ChargyDesktopApp/) (Electron)
- [Chargy Mobile Apps](https://github.com/OpenChargingCloud/ChargyMobileApp/) (Android/iOS)


## Solution layout

```
ChargyCore.slnx
├── ChargyCore/            The library, assembly "cloud.charging.open.chargy"
└── ChargyCoreTests/       NUnit test project incl. all charge transparency test fixtures
```

ChargyCore.NET references [Vanaheimr Styx](https://github.com/Vanaheimr/Styx) and
[Vanaheimr Hermod](https://github.com/Vanaheimr/Hermod) as sibling directories, exactly
how Hermod itself references Styx. Clone all three next to each other:

```
<your source directory>/
├── ChargyCore.NET/
├── Hermod/
└── Styx/
```

```bash
git clone https://github.com/Vanaheimr/Styx.git
```

```bash
git clone https://github.com/Vanaheimr/Hermod.git
```

```bash
git clone https://github.com/OpenChargingCloud/ChargyCore.NET.git
```


## Development

```bash
dotnet build ChargyCore.slnx
```

```bash
dotnet test ChargyCore.slnx
```

All cryptography is provided by [BouncyCastle](https://www.bouncycastle.org/) — ECDSA over
secp192r1, secp224k1, secp256k1, secp256r1, secp384r1 and secp521r1; Ed25519, Ed25519ctx,
Ed25519ph, Ed448 and Ed448ph; and the FIPS 204 parameter sets ML-DSA-44, ML-DSA-65 and
ML-DSA-87.


### Verification report parity

The `*.expected.txt` files under `ChargyCoreTests/TestData/` are shared byte-for-byte with
ChargyCore.TS. Both implementations render every parsed Charge Transparency Record into the
same implementation-independent plain text report and compare it line by line. This makes
semantic equivalence of the two implementations an automatically verified property rather
than an assumption.


## Funding

This Open Source project is partially funded by the [NGI Zero Commons Fund](https://nlnet.nl/commonsfund/) as part of our [EVQI project](https://nlnet.nl/project/EVQI/).

We also appreciate any additional funding and long-term support for the Chargy family, for example via [GitHub Sponsors](https://github.com/sponsors/GraphDefined), as it helps us keep the project sustainable, independent and useful for the entire e-mobility community.

<center>
  <img src="images/NGI0_tag.svg" height="30">
</center>
