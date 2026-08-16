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


## Usage

Everything starts at `ContentFormatDetector`. Hand it the files as they arrived —
whatever they are — and it works out the rest: PDF attachments out, archives
unpacked (repeatedly, because they nest), QR codes read, public keys paired with the
records they belong to, and each remaining file given to the format it looks like.

```csharp
using cloud.charging.open.chargy;
using cloud.charging.open.chargy.IO;
using cloud.charging.open.chargy.qrcodes;

var i18n     = I18NDictionary.Default();

var detector = new ContentFormatDetector(
                   i18n,
                   ChargeTransparencyFormats.All(i18n),
                   new PDFAttachmentExtractor(),   // optional: PDF/A-3 invoices
                   new QRCodeDecoder()             // optional: photographed QR codes
               );

var result   = await detector.DetectAndConvertContentFormat([
                   new FileInfo("OCMF-Testdata-01.ocmf",
                                await File.ReadAllBytesAsync("OCMF-Testdata-01.ocmf")),
                   new FileInfo("OCMF-Testdata-01_publicKey.txt",
                                await File.ReadAllBytesAsync("OCMF-Testdata-01_publicKey.txt"))
               ]);
```

The file **name** is handed over alongside the bytes on purpose: a public key file is
paired with its record by name, and some formats are recognised by theirs.

What comes back is one of a handful of things, and which one is itself information —
a QR code that turns out to hold a link is a different answer from a charging session
that failed to verify:

```csharp
switch (result)
{

    case ChargeTransparencyRecord record:
        foreach (var session in record.ChargingSessions)
            Console.WriteLine($"{session.Id}: {session.VerificationResult?.Status}");
        break;

    // A pointer to charging data, not the data itself. Nothing was fetched:
    // following the link would tell the operator who is looking.
    case SimpleURL url:
        Console.WriteLine(url.URL);
        break;

    case ChargeTransparencyLiveLink liveLink:
        Console.WriteLine($"{liveLink.Transports.Count} way(s) to reach the station");
        break;

    case PublicKeyLookup keys:
        Console.WriteLine($"{keys.PublicKeys.Count} public key(s) and no charging data");
        break;

    // Not charge transparency data at all — and this says why, in the
    // languages the dictionary was built with.
    case SessionCryptoResult failure:
        Console.WriteLine(i18n.GetLocalizedText(failure.Message));
        break;

}
```

A signature that verifies says the meter really reported these numbers. What the
numbers **are** is the other half:

```csharp
foreach (var measurement in record.ChargingSessions[0].Measurements)
    foreach (var value in measurement.Values)
        Console.WriteLine($"{value.Timestamp}  {value.Value} {measurement.Unit}  " +
                          $"{value.Result?.Status}");
```

Each reading carries its own verdict, and where a verification failed it carries the
reason as well — a stable `Code` an application can switch on, the sentence to show a
driver in their own language, and a technical detail where there is one. That
distinction matters: "the signature does not match the signed data" says something
about the charging session, while "this curve is not implemented" says something about
Chargy.

When the format is already known and the file has already been unpacked, a format can
be used on its own:

```csharp
var record = new OCMFFormat(i18n).TryParseText(ocmfDocument, publicKeyHEX);
```


## Command line

`ChargyVerify` is a small worked example of the API — read the files, hand them to the
detector, print what came back:

```bash
dotnet run --project ChargyVerify -- <file>...
```

```
Charging session 1: verified
  identification:   1546961267168:463645022654661183:1
  measured:         2019-01-08T15:27:50.000Z .. 2019-01-08T15:50:02.000Z
  EVSE:             DE*BDO*74778874*1
  energy meter:     0901454D4800007F9F3E
  authorized by:    235DD5BB
  ENERGY_TOTAL: 2 reading(s), 2 with a valid signature
    6484 WATT_HOUR (110281 .. 116765)
```

It exits `0` when everything read verified, `1` when something did not, `2` when the
input was not charge transparency data at all and `64` when the command line made no
sense — so it can be used from a script as well as read by a person. `--help` lists
the options; `--json` prints the charge transparency record itself instead of a
report.


## Project Status

ChargyCore.NET has been ported from ChargyCore.TS. The porting strategy, the phase
plan and every deliberate deviation are documented in
[`PORTING_PLAN.md`](PORTING_PLAN.md).

| Phase | Scope | Status |
|---|---|---|
| 0 | Repository, solution, projects, test fixtures | ✅ **done** |
| 1 | ChargyLib, data structures, i18n, validation rules | ✅ **done** |
| 2 | Cryptography (ECDSA, EdDSA, ML-DSA), ACrypt, signed JSON | ✅ **done** |
| 3 | Content format detection, archives, PDF/A-3, QR codes | ✅ **done** |
| 4 | The charge transparency data formats | ✅ **done** |
| 5 | Charge Transparency Live Link, URL resolution | ✅ **done** |
| 6 | Documentation, samples, packaging | ✅ **done** |

Phase 4 in detail:

| Format | Status |
|---|---|
| SAFE XML container | ✅ **done** |
| Alfen | ✅ **done** |
| OCMF, incl. the BET tariff text extension and the modern signatures | ✅ **done** |
| EMH, EDL40 (SML) | ✅ **done** |
| chargeIT, BSM, GDF | ✅ **done** |
| Mennekes, ChargePoint, PCDF | ✅ **done** |
| PTB, XMLContainer, KEBA | ✅ **done** |
| QIDigital (DCC / DCoA / DCoC), OCPI | ✅ **done** |

The verification reports of all 23 shared golden files match ChargyCore.TS
byte-for-byte. Two formats are worth naming explicitly: **GDF** is ported and
exercised by no fixture in either implementation, and **QIDigital** is a data model
only — upstream declares 34 TypeScript interfaces and no parser, so the round trip is
the only claim available without a real calibration certificate.

ChargyCore.TS **declares** the three live-link transports (`https`, `httpSSE`,
`websocket`) and a TOTP configuration and implements neither, so there was nothing to
port. ChargyCore.NET implements them as new functionality — see below.


## Watching a charging session while it happens

A charge transparency file is written after the fact. A *live link* — the QR code on
the station's display — is the same evidence while the car is still plugged in.

```csharp
using cloud.charging.open.chargy.LiveLink;

var client = new ChargeTransparencyLiveLinkClient(detector);

await foreach (var update in client.Connect(liveLink, cancellationToken: token))
    if (update.Result is ChargeTransparencyRecord record)
        Console.WriteLine($"{update.Endpoint}: {record.ChargingSessions[0].VerificationResult?.Status}");
```

Every update goes through the same pipeline as a file: what arrives over a WebSocket is
not more trustworthy for having arrived quickly, so it is verified the same way.
Nothing happens unless an application asks for it — this is the only part of Chargy
that opens a network connection on an EV driver's behalf.

The transports are tried in the order the live link states them, or in the order the
application prefers. Within a transport, addresses are chosen the way DNS chooses
service records — lower `priority` first, equal priorities drawn in proportion to their
`weight` — and an address that never answers is passed over for the next.

Where a transport is protected by a time-based one-time password, the current one is
sent in the `TOTP` header of every request. The scheme is the one of the
[Dynamic QR-Codes](https://github.com/OpenChargingCloud/DynamicQRCodes) reference
implementations, built for the EU AFIR and adopted into OCPP v2.1, and it is
implemented in Hermod rather than here: two implementations of one algorithm would
drift, and a password that the station and the phone derive differently locks a driver
out of their own charging session. ChargyCore's tests check Hermod against the
reference implementation's own vectors.


## Related projects

- [Chargy Core TS](https://github.com/OpenChargingCloud/ChargyCore.TS), the TypeScript implementation this port is derived from
- [Chargy Web App](https://github.com/OpenChargingCloud/ChargyWebApp/) (node.js), live demo at [https://chargy.charging.cloud](https://chargy.charging.cloud)
- [Chargy Desktop App](https://github.com/OpenChargingCloud/ChargyDesktopApp/) (Electron)
- [Chargy Mobile Apps](https://github.com/OpenChargingCloud/ChargyMobileApp/) (Android/iOS)


## Solution layout

```
ChargyCore.slnx
├── ChargyCore/            The library, assembly "cloud.charging.open.chargy"
├── ChargyCore.QRCodes/    QR code reading, assembly "cloud.charging.open.chargy.qrcodes"
├── ChargyVerify/          Command line verifier, a worked example of the API
└── ChargyCoreTests/       NUnit test project incl. all charge transparency test fixtures
```

Reading a QR code means decoding PNG, JPEG, GIF, WEBP, BMP and SVG, which is by far the
heaviest dependency in this project. It therefore lives in its own assembly behind
`IQRCodeDecoder`, so that a consumer who only ever verifies OCMF strings on a server does
not have to carry an image stack. This mirrors ChargyCore.TS, where the image modules are
optional dependencies and QR code reading simply degrades when they are absent.

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

```bash
dotnet run --project ChargyVerify -- --help
```

The build settings and the package metadata shared by every project live in
[`Directory.Build.props`](Directory.Build.props). `GenerateDocumentationFile` is on, so
an undocumented public member is a warning, and the build runs warning-free.

CI builds and tests on Linux **and** Windows. Windows is there for a specific reason: the
test fixtures are signed charge transparency records, so their bytes are what is under
test, and a Windows checkout rewrites line endings by default.
[`.gitattributes`](.gitattributes) prevents that, and the Windows leg is what proves it —
before building, every tracked file on disk is compared against the bytes the repository
holds. The GitHub Windows image runs with `core.autocrlf=true` and does rewrite 197 of the
repository's other files; all 204 fixtures come through untouched.

All cryptography is provided by [BouncyCastle](https://www.bouncycastle.org/) — ECDSA over
secp192k1, secp192r1, secp224k1, secp256k1, secp256r1, secp384r1 and secp521r1, plus the
RFC 5639 curves brainpoolP256r1 and brainpoolP384r1 — which OCMF names without their "P";
Ed25519, Ed25519ctx, Ed25519ph, Ed448 and Ed448ph; and the FIPS 204 parameter sets
ML-DSA-44, ML-DSA-65 and ML-DSA-87.

Every ECDSA algorithm OCMF names can therefore be checked. Two of them, `brainpool384r1`
paired with SHA-256 and `secp384r1` paired with SHA-256, use a digest that does not match
their curve — but meters were built that way and signed real charging sessions with it, so
Chargy verifies them exactly as they were signed rather than "correcting" anything.


### Packaging

`dotnet pack` on the two library projects produces `cloud.charging.open.chargy` and
`cloud.charging.open.chargy.qrcodes`, each with the XML documentation, this README and
a `.snupkg` of debug symbols. Project by project rather than solution-wide, because
Styx and Hermod are in the solution as well.

ChargyCore.NET is consumed as a project reference, exactly as this repository's own CI
does, and as Hermod itself consumes Styx.


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
