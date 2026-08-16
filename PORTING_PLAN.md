# ChargyCore.NET — Porting Plan

Porting [ChargyCore.TS](https://github.com/OpenChargingCloud/ChargyCore.TS) (v0.11.3)
to C# / .NET 10 as [ChargyCore.NET](https://github.com/OpenChargingCloud/ChargyCore.NET).

* **License:** AGPL-3.0-only (same as ChargyCore.TS)
* **Target framework:** `net10.0`
* **Test framework:** NUnit, in a separate test project
* **Dependencies:** [Vanaheimr Styx/Illias](https://github.com/Vanaheimr/Styx),
  [Vanaheimr Hermod](https://github.com/Vanaheimr/Hermod), BouncyCastle for all cryptography

---

## 1. What is being ported

Source inventory of ChargyCore.TS:

| Area | TypeScript files | LOC |
|---|---|---:|
| Charge transparency data formats | `OCMF`, `Alfen`, `EMHCrypt01`, `GDFCrypt01`, `BSMCrypt01`, `EDL40`, `Mennekes`, `chargePoint`, `PCDF`, `QIDigital_*`, `OCMF_BET_TariffTextExtension` | ~11,000 |
| Containers & orchestration | `chargy`, `chargeIT`, `SAFE_XML`, `PTBContainer`, `XMLContainer`, `qrCodeReader` | ~5,600 |
| Data model & helpers | `interfaces/*`, `ACrypt`, `SignatureCrypto`, `OCPI` | ~4,900 |
| **Total source** | 34 files | **~23,600** |
| Tests | 28 spec files | ~4,300 |
| Test fixtures | 203 files, of which **25 are `*.expected.txt` golden reports** | — |

Expected C# size: roughly 28,000–35,000 LOC, because the C# data model uses the
GraphDefined house style (`ToJSON()` / `TryParse()` per class) instead of bare
TypeScript interfaces.

### 1.1 Explicitly *not* ported

These exist only because ChargyCore.TS is consumed by a browser/Electron GUI:

* All `HTMLDivElement` rendering: `ACrypt.ViewMeasurement()`, `CreateLine()`,
  `CreateLocalizedLine()`, `AddToVisualBuffer()`, `chargyLib.CreateDiv/CreateDiv2`,
  `openFullscreen()` / `closeFullscreen()`.
  **Replacement:** a pure data `VerificationTrace` model (see §4.3) that a GUI can render.
  Nothing is lost — the same information is produced, just without DOM coupling.
* The dual browser/Node build split (`src/pdfjs/*`, conditional package exports).
  .NET needs one assembly; runtime-specific concerns become injectable interfaces (§4.5).
* `IChargingSession.GUI?: HTMLDivElement` and `setUILocale()`.

---

## 2. Solution layout

```
ChargyCore.NET/
├── ChargyCore.slnx
├── Directory.Build.props               settings and package metadata, shared
├── Directory.Build.targets             the packed README (needs IsPackable, so: targets)
├── LICENSE                             AGPL-3.0 (copied from ChargyCore.TS)
├── README.md  SECURITY.md  CONTRIBUTING.md  CODE_OF_CONDUCT.md
├── .gitignore  .gitattributes
├── .github/workflows/ci.yml
├── documentation/                      copied verbatim from ChargyCore.TS
│
├── ChargyCore/
│   ├── ChargyCore.csproj
│   ├── Chargy.cs                       orchestrator  (chargy.ts)
│   ├── ChargyLib.cs                    hex/OBIS/timestamp/byte helpers (chargyLib.ts)
│   │
│   ├── DataStructures/                 the CTR data model (interfaces/*.ts)
│   │   ├── ChargeTransparencyRecord.cs
│   │   ├── ChargingSession.cs
│   │   ├── Measurement.cs
│   │   ├── MeasurementValue.cs
│   │   ├── ChargingStationOperator.cs  ChargingPool.cs  ChargingStation.cs
│   │   ├── EVSE.cs  Connector.cs  EnergyMeter.cs
│   │   ├── Manufacturer.cs  DeviceModel.cs  Firmware.cs  Hardware.cs
│   │   ├── LegalCompliance.cs  Conformity.cs  Calibration.cs
│   │   ├── Tariffs/                    ChargingTariff, ParkingTariff, PriceComponent, …
│   │   ├── PublicKeys/                 PublicKey.cs, PublicKeyLookup.cs, KeyInfo.cs
│   │   ├── Results/                    SessionCryptoResult, CryptoResult,
│   │   │                               Warning, Error, all result enums
│   │   ├── ChargeTransparencyLiveLink.cs
│   │   ├── SimpleURL.cs
│   │   └── FileInfo.cs  ExtendedFileInfo.cs
│   │
│   ├── Crypto/
│   │   ├── ACrypt.cs                   abstract signature-verification base
│   │   ├── SignatureSuite.cs           ISignatureSuite + registry
│   │   ├── ECDSASignatureSuite.cs      secp192r1/224k1/256k1/256r1/384r1/521r1
│   │   ├── EdDSASignatureSuite.cs      Ed25519, Ed25519ctx, Ed25519ph, Ed448, Ed448ph
│   │   ├── MLDSASignatureSuite.cs      ML-DSA-44 / -65 / -87  (FIPS 204)
│   │   ├── Secp224k1.cs                (thin wrapper over BouncyCastle, see §4.2)
│   │   ├── PublicKeyParser.cs          DER/PEM/HEX SubjectPublicKeyInfo parsing
│   │   ├── CryptoUtils.cs              signed-JSON message sign/verify
│   │   └── VerificationTrace.cs        replaces the HTML "view" API
│   │
│   ├── Formats/                        one sub-directory per transparency data format
│   │   ├── OCMF/         OCMFFormat.cs, OCMFCrypt01.cs, OCMFDocument.cs,
│   │   │                 OCMFDocumentScanner.cs, OCMFMeasurement.cs,
│   │   │                 OCMFSignatureAlgorithm.cs, OCMFSignatureValidator.cs,
│   │   │                 OCMFSignatureDisplay.cs, OCMFBonnTariff.cs,
│   │   │                 OCMFChargeTransparencyRecord.cs
│   │   ├── Alfen/        Alfen.cs, AlfenCrypt01.cs
│   │   ├── EMH/          EMHCrypt01.cs
│   │   ├── GDF/          GDFCrypt01.cs
│   │   ├── BSM/          BSMFormat.cs, BSMCrypt01.cs, BSMSnapshot.cs,
│   │   │                 BSMMeasurement.cs
│   │   ├── ChargeIT/     ChargeITContainer.cs, ChargeITMeterValue.cs,
│   │   │                 ChargeITOperator.cs, ChargeITFormatChecks.cs
│   │   ├── EDL40/        EDL40Format.cs, EDL40Crypt01.cs, EDL40Document.cs,
│   │   │                 EDL40SignatureData.cs, EDL40Measurement.cs,
│   │   │                 SmlReader.cs, SmlValue.cs
│   │   ├── Mennekes/     MennekesFormat.cs, MennekesCrypt01.cs,
│   │   │                 MennekesChargingProcess.cs, MennekesMeasurement.cs
│   │   ├── ChargePoint/  ChargePointFormat.cs, ChargePointCrypt01.cs
│   │   ├── PCDF/         PCDFFormat.cs, PCDFCrypt01.cs, PCDFDocument.cs
│   │   ├── PTB/          PTBContainer.cs
│   │   ├── XMLContainer/ XMLContainerFormat.cs
│   │   ├── OCPI/         OCPIFormat.cs
│   │   └── QIDigital/    DigitalCalibrationCertificate.cs, DigitalCertificates.cs,
│   │                     DCCTypes.cs, DCCMeasurement.cs
│   │
│   ├── Containers/                     data *representations* (not formats)
│   │   ├── ChargeITContainer.cs        both chargeIT variants
│   │   ├── ChargyContainer.cs          multi-session ".chargy"
│   │   ├── SAFEXMLContainer.cs
│   │   ├── PTBContainer.cs
│   │   ├── XMLContainer.cs
│   │   └── Archives/  ZipReader.cs  TarReader.cs  GZipReader.cs  BZip2Reader.cs
│   │
│   ├── IO/
│   │   ├── ContentFormatDetector.cs    DetectAndConvertContentFormat pipeline
│   │   ├── IPDFAttachmentExtractor.cs  + PDFAttachmentExtractor.cs
│   │   ├── IQRCodeDecoder.cs           + ZXingQRCodeDecoder.cs
│   │   └── IURLResolver.cs             + HermodURLResolver.cs
│   │
│   ├── OCPI/                           OCPI tariff helpers (OCPI.ts)
│   └── Resources/                      i18n.json, validationRules.json (EmbeddedResource)
│
├── ChargyVerify/                       command line verifier, a worked example
│   ├── ChargyVerify.csproj
│   ├── Program.cs                      read the files, hand them to the detector
│   ├── CommandLine.cs
│   └── Report.cs                       ..., and print what came back
│
└── ChargyCoreTests/                    (naming follows HermodTests / StyxTests)
    ├── ChargyCoreTests.csproj
    ├── AChargyTests.cs                 base fixture + report formatter (testHelper.ts)
    ├── VerificationReportFormatter.cs
    ├── Formats/                        one test class per format
    ├── Containers/
    ├── Crypto/
    └── TestData/                       all 203 fixtures copied 1:1
```

**Namespace / assembly:** `cloud.charging.open.chargy` (+ `.DataStructures`, `.Crypto`,
`.Formats.OCMF`, …), matching the existing `cloud.charging.open.*` house convention;
tests live in `cloud.charging.open.chargy.tests`.

**Project references** (siblings on disk, exactly as Hermod already references Styx):

```xml
<ProjectReference Include="..\..\Styx\Styx\Styx.csproj" />
<ProjectReference Include="..\..\Hermod\Hermod\Hermod.csproj" />
```

---

## 3. Dependency mapping

| ChargyCore.TS | ChargyCore.NET |
|---|---|
| `@noble/curves`, `elliptic` | **BouncyCastle.Cryptography 2.7.0** (all six SEC curves incl. `secp224k1`, verified present) |
| `@noble/post-quantum` (ML-DSA) | **BouncyCastle** `MLDsaParameters` / `MLDsaSigner` (FIPS 204, verified present) |
| `asn1.js` | BouncyCastle `Asn1` / `SubjectPublicKeyInfo` |
| `decimal.js` | `System.Decimal` (28–29 significant digits ≫ what meter readings need); `Illias.WattHour` where the value is semantically energy |
| `moment` | `DateTimeOffset` + `Illias.Timestamp` / `DateTimeExtensions` |
| `DOMParser` | `System.Xml.Linq.XDocument` with namespace-agnostic local-name lookups |
| `JSON.parse` / JSON-LD | `Newtonsoft.Json.Linq.JObject` (what Illias' JSON helpers and `CanonicalJSON` are built on) |
| own `canonicalJSONStringify` | **`Illias.CanonicalJSON.Serialize()`** — already implemented and unit-tested in Styx |
| own `I18NString` | **`Illias.I18NString`** |
| `IGeoLocation` | **`Aegir.GeoCoordinate`** |
| `fetch` (URL resolver, live link) | **Hermod** `HTTPClient` / SSE / WebSocket clients |
| `seek-bzip` | BouncyCastle `CBZip2InputStream` (verified present) |
| tar / zip / gzip | `System.Formats.Tar`, `System.IO.Compression` (BCL, no dependency) |
| `jsqr` + canvas/pngjs/jpeg-js | `ZXing.Net` + `SkiaSharp`, behind `IQRCodeDecoder` |
| `pdfjs-dist` (attachments only) | own minimal PDF `/EmbeddedFiles` reader behind `IPDFAttachmentExtractor` (see §4.5) |
| `vitest` | **NUnit 4.6.x** + `NUnit3TestAdapter` + `Microsoft.NET.Test.Sdk` (same versions as StyxTests) |

Everything ChargyCore.TS gets from JS crypto libraries is available in BouncyCastle 2.7.0 —
confirmed against the assembly: `MLDsa*`, `Ed25519ctxSigner`, `Ed448phSigner`,
`CBZip2InputStream`, `secp192r1`, `secp224k1`, `secp521r1`.

---

## 4. Key design decisions

### 4.1 Data model: interfaces → classes with `ToJSON()` / `TryParse()`

The TS `interface I…` shapes become C# classes in GraphDefined house style: immutable
properties, a static `TryParse(JObject, out T, out String?)`, and `JObject ToJSON()`.
This is the only way to round-trip JSON-LD faithfully *and* keep the public API idiomatic
for the other OpenChargingCloud .NET projects.

Enums (`SessionVerificationResult`, `VerificationResult`, `ErrorLevel`, …) map to C# enums
whose `ToString()` **must** stay identical to the TS string values, because those strings
appear verbatim in the golden test reports.

### 4.2 Cryptography

One `ISignatureSuite` abstraction over BouncyCastle covering all three families:

```csharp
public interface ISignatureSuite
{
    String            Algorithm          { get; }
    SignatureEncoding SignatureEncoding  { get; }

    SignatureKeyPair  GenerateKeyPair();
    Boolean           Verify(ReadOnlySpan<Byte> Message,
                             ReadOnlySpan<Byte> Signature,
                             ReadOnlySpan<Byte> PublicKey,
                             SignatureOptions?  Options = null);
    Byte[]            Sign  (ReadOnlySpan<Byte> Message,
                             ReadOnlySpan<Byte> PrivateKey,
                             SignatureOptions?  Options = null);
}
```

`secp224k1`: ChargyCore.TS ships a hand-written BigInt implementation because JS libraries
did not offer that Koblitz curve. BouncyCastle *does* (`SecNamedCurves.GetByName("secp224k1")`),
so `Secp224k1.cs` becomes a thin wrapper. The hand-rolled arithmetic is dropped — but the
existing secp224k1 test vectors are ported to prove equivalence before the old code goes.

### 4.3 `VerificationTrace` — replacing the DOM "view" API

`ACrypt.ViewMeasurement(…, HTMLDivElement×8)` exists to show *which bytes of the signed
buffer correspond to which field*. That is genuinely useful data, so it is kept — as data:

```csharp
public readonly record struct TraceLine(String  Id,            // i18n key or literal label
                                        String  Value,         // human-readable value
                                        String  ValueHEX,      // its hex bytes
                                        Range   BufferRange);  // position in the signed buffer

public sealed class VerificationTrace
{
    public IReadOnlyList<TraceLine>  Lines               { get; }
    public ReadOnlyMemory<Byte>      SignedBuffer        { get; }
    public ReadOnlyMemory<Byte>      HashedBuffer        { get; }
    public ReadOnlyMemory<Byte>      PublicKey           { get; }
    public ReadOnlyMemory<Byte>      ExpectedSignature   { get; }
    public CryptoResult              Result              { get; }
}
```

`ACrypt` then exposes `VerifyMeasurement(...)`, `VerifyChargingSession(...)` and
`TraceMeasurement(...)` — no UI types in the core library.

### 4.4 Modern .NET ("Span & friends")

The binary-heavy code is exactly where this pays off. ChargyCore.TS builds fixed-size
signature buffers with `DataView` + `SetHex/SetUInt32/SetTimestamp/SetText` helpers
(`chargyLib.ts`, `EMHCrypt01`, `GDFCrypt01`, `BSMCrypt01`, `Alfen`, `EDL40`).

* `stackalloc byte[N]` + `Span<Byte>` for every signature buffer (they are all ≤ a few hundred bytes)
* `System.Buffers.Binary.BinaryPrimitives` instead of manual `getInt32Bytes()` / endian juggling
* `Convert.FromHexString` / `Convert.ToHexStringLower` instead of the hand-rolled hex helpers
* `ReadOnlySpan<Char>` + `MemoryExtensions.Split` for OCMF / PCDF / Alfen text parsing — zero-allocation tokenizing
* `SearchValues<Char>` for hot scanning paths
* `[GeneratedRegex]` source generators for the OBIS regex and friends
* `FrozenDictionary` for the i18n dictionary and the OBIS → measurement-name table
* `record` / `required` / `init` / primary constructors / collection expressions throughout
* Nullable reference types enabled, warnings as errors in CI

Async: TypeScript makes everything `async` because JS crypto is promise-based. In .NET,
parsing and verification are synchronous. Only genuinely I/O-bound APIs
(URL resolution, live-link transports) stay `Task`-based. This removes a large amount of
incidental complexity.

Code style follows Hermod/Styx: block-scoped namespaces, `#region` sections, aligned
declarations, `Byte`/`String`/`Boolean` BCL type names.

### 4.5 Pluggable runtime concerns

Two features drag in heavy or platform-specific dependencies. Both go behind interfaces
with a default implementation registered in `Chargy`'s constructor, so a consumer can
swap or omit them:

* **`IQRCodeDecoder`** — default `ZXingQRCodeDecoder` (ZXing.Net + SkiaSharp for
  PNG/JPEG/WEBP/GIF/BMP raster decoding; SVG QR codes are rasterized first).
* **`IPDFAttachmentExtractor`** — Chargy only needs the PDF/A-3 `/Names /EmbeddedFiles`
  name tree and its `FlateDecode`d streams; it never renders a page. A focused ~400 LOC
  reader avoids taking a PDF library dependency. `PdfPig` is the fallback if edge cases
  (object streams, encrypted PDFs, cross-reference streams) prove too costly.

---

## 5. Test strategy

### 5.1 The golden-file harness is the backbone

`tests/testHelper.ts` renders every parsed CTR into a **plain-text verification report**
and compares it line by line against a checked-in `*.expected.txt`, e.g.:

```
format: ctr
sessions: 1
session 1: 19
session 1 evseId: DE*GEF*EVSE*CHARGY*1
session 1 meterId: 0a01445a470033008506
session 1 status: ValidSignature
session 1 measurements: 1
measurement 1.1 name: ENERGY_TOTAL
measurement 1.1 obis: 1-0:1.8.0*255
measurement 1.1 status: unknown
measurement 1.1 values: 2
value 1.1.1 timestamp: 2019-04-05T14:54:50.000Z
value 1.1.1 value: 22675
value 1.1.1 signatures: 1
value 1.1.1 status: ValidSignature
…
```

This format is implementation-independent. So `VerificationReportFormatter.cs` is ported
1:1, all 25 `.expected.txt` files are copied **unchanged**, and the .NET port must
reproduce them byte for byte. That gives cross-implementation parity with ChargyCore.TS
as a hard, automatically-checked contract — not a hope.

`expect.soft` (report *all* differing lines, not just the first) maps to
`Assert.Multiple(() => { … Assert.That(actualLine, Is.EqualTo(expectedLine), $"line {i+1}"); … })`.

> **Watch out:** the expected timestamps are not uniformly formatted —
> `2019-06-26T08:57:44.337+00:00` (OCMF) vs `2019-04-05T14:54:50.000Z` (Alfen). They
> reflect how each parser normalizes its input via `moment`. The C# port must reproduce
> the per-format formatting exactly; this is the single most likely source of diff noise
> and gets a dedicated helper (`ChargyLib.FormatTimestamp`) plus focused unit tests.

### 5.2 Test project

* NUnit 4.6.1 + NUnit3TestAdapter 6.2.0 + Microsoft.NET.Test.Sdk 18.8.1 (identical to StyxTests)
* `<Using Include="NUnit.Framework" />`
* All 203 fixtures under `TestData/`, `CopyToOutputDirectory="PreserveNewest"`
* One `[TestFixture]` per TS spec file, ~151 test cases total:

| TS spec | NUnit fixture | cases |
|---|---|---:|
| `PCDF.tests.ts` | `Formats/PCDFTests` | 16 |
| `data-structures.test.ts` | `DataStructures/DataStructureTests` | 12 |
| `chargeIT.tests.ts` | `Containers/ChargeITTests` | 11 |
| `CryptoUtils.test.ts` | `Crypto/CryptoUtilsTests` | 11 |
| `ALFEN.tests.ts` | `Formats/AlfenTests` | 9 |
| `SAFE.tests.ts`, `CanonicalJSON.test.ts` | `Containers/SAFEXMLTests`, `Crypto/CanonicalJSONTests` | 8 each |
| `Mennekes` | … | 7 |
| `SimpleURLs` | `IO/SimpleURLTests` | 7 → 9 |
| `ChargeTransparencyLiveLink` | `ChargeTransparencyLiveLinkTests` | 3 → 4 |
| `ChargePoint`, `EMHCrypt01`, `OCMFVersions`, `SAFE_withChargyExtensions` | … | 6 each |
| `PublicKeyFiles` | … | 5 |
| `EDL40`, `OCMFTariffText` | `Formats/OCMFTariffTextTests` | 4 each |
| `OCMF`, `chargyInterfaces` | … | 3 each |
| `OCMFDiagnostics` + `OCMFErrorPropagation` | `Formats/OCMFDiagnosticsTests` (merged: three tests, one subject) | 3 |
| `PTBContainer` | … | 2 |
| `KEBA` | … | 1 |
| `OCMFModernSignatures`, `OCMF_BET_TariffTextExtension`, `OCPI` | table-driven fixtures | n |

### 5.3 Additional .NET-only tests

* Round-trip tests `ToJSON()` → `TryParse()` → `ToJSON()` for every data structure
* Signature-suite conformance tests per algorithm (sign → verify, tampered message → fail,
  wrong key → fail) including the ML-DSA and Ed448 paths
* `secp224k1` vectors proving the BouncyCastle wrapper matches the TS implementation
* Archive/PDF/QR extraction tests against the existing binary fixtures

---

## 6. Implementation phases

Each phase ends with a compiling solution and green tests, so progress is verifiable
at every step.

### Phase 0 — Repository & solution scaffolding ✅ **done**
Git repo, AGPL `LICENSE`, `README.md`, `SECURITY.md`, `CONTRIBUTING.md`,
`CODE_OF_CONDUCT.md`, `.gitignore`, `.gitattributes`, GitHub Actions workflow
(`.github/workflows/ci.yml`, which checks out Styx and Hermod as siblings),
`ChargyCore.slnx` + both `.csproj` files with the Hermod/Styx project references,
`i18n.json` + `validationRules.json` as embedded resources, all 203 fixtures copied
into `ChargyCoreTests/TestData/`.

Two additions over the original plan:

* `ChargyResources` and `AChargyTests` were written already, so Phase 0 could ship
  **8 scaffolding tests** instead of an empty test run. They assert that the embedded
  resources parse, that all 203 fixtures and all 25 golden reports reach the output
  directory, that binary fixtures survive byte-exactly (ZIP magic intact) and that the
  golden reports carry no CRLF. An empty green test run would have proven nothing.
* `.gitattributes` marks `ChargyCoreTests/TestData/**` as `-text`. End-of-line
  normalisation on a signed fixture invalidates its signature, which would surface much
  later as a puzzling "invalid signature" on Windows checkouts only.

*Exit:* `dotnet build ChargyCore.slnx` — 0 warnings, 0 errors;
`dotnet test ChargyCore.slnx` — 8/8 passed.

> Note: `TestData/dataStructures.ts` and `TestData/OCMF/versionTestData.ts` are
> TypeScript *test data definitions*, not charge transparency fixtures. They are kept
> as the reference for the C# table-driven fixtures written in Phase 1 and Phase 4,
> and are part of the 203 files asserted above. `versionTestData.ts` is now mirrored
> by `ChargyCoreTests/Formats/OCMFVersionTestData.cs`, whose seeded generator is the
> same one bit for bit — so both implementations are fed the same generated documents.

### Phase 1 — Foundation ✅ **done**
`ChargyLib` (hex, byte, OBIS, timestamp helpers — Span-based), the complete
`DataStructures/` model with `ToJSON()`/`TryParse()`, results & enums, i18n dictionary
loading, validation rules.
*Exit:* `DataStructureTests`, `chargyInterfacesTests` green (~15 cases).

### Phase 2 — Cryptography ✅ **done**
`ISignatureSuite` + ECDSA/EdDSA/ML-DSA suites over BouncyCastle, `Secp224k1`,
`PublicKeyParser` (DER/PEM/HEX), `CryptoUtils` signed-JSON messages on top of
`Illias.CanonicalJSON`, `ACrypt` base class, `VerificationTrace`.
*Exit:* `CryptoUtilsTests`, `CanonicalJSONTests`, `PublicKeyFilesTests` green (~24 cases).

### Phase 3 — I/O & container pipeline ✅ **done**
`ContentTypes` (magic-byte sniffing), `ArchiveReader` (zip/tar/gz/bz2),
`PDFAttachmentExtractor` behind `IPDFAttachmentExtractor`, `QRCodeDecoder` behind
`IQRCodeDecoder`, `PublicKeyFiles`, `HTTPURLResolver` behind `IURLResolver`, and
`ContentFormatDetector` — the `DetectAndConvertContentFormat` state machine, with the
data formats plugged in through `ChargeTransparencyFormats`.
*Exit:* 86 cases green — the five chargeIT container variants, the ChargePoint
`secrrct` combination, nested archives, the SAFE PDF/A-3 invoice, the PNG/JPEG/SVG QR
codes, and the public key file handling end to end.

### Phase 4 — Formats, one directory at a time ✅ **done**
Ordered so each step unlocks the largest number of golden tests with the least new
infrastructure. Every step is a self-contained increment: format + its `ACrypt` + its tests.

1. **SAFE XML container + Alfen** ✅ **done** → `AlfenTests` (7). Note the plan miscounted here:
   the `SAFE-Testdata-*` fixtures carry **OCMF** payloads, not Alfen ones, so `SAFETests`
   and `SAFE_withChargyExtensionsTests` unlock with step 2, not this one.
2. **OCMF** ✅ **done** → `OCMFScannerTests` (14), `OCMFSecp192Tests` (6), `OCMFTests` (9).
   The session id is `"OCMF-"` plus the SHA-256 of `canonicalJSON({payload, signature})`
   per document, joined by a newline — the **canonical form, never the document text**,
   which upstream corrected in `456252f` after a Windows checkout rewrote the line
   endings of a pretty-printed fixture and gave the same record a different identity.
   Two further details matter: the hash is not `hashValue` (that follows the signature
   algorithm and is empty for the directly signing ones), and `begin`/`end` are ordered
   **by instant, not lexically**, because the timestamps keep the meter's own UTC offset.
   Documents are grouped by their session identity and only the first group becomes a
   record, which is what keeps two drivers on one meter from being merged into one bill.

   The remaining OCMF sub-features were pulled in afterwards, see step 10.
3. **EMH + EDL40 (SML)** ✅ **done** → `EMHCrypt01Tests` (6), `EDL40Tests` (12).
   EDL40 is the only format that is *never* detected on its own: an SML message carries
   no public key, so it can only be read inside a container that supplies one. It
   therefore has no slot among the text formats and is reached solely through the SAFE
   container — which is also why `EDL40/edl-40-0*.xml`, which declare no signed data
   format, never reach the pipeline and are tested by parsing them directly.
   The layout deduces its own curve from the lengths of key and signature (48 bytes of
   key → secp192r1 with a 24 byte hash, 64 → secp256r1 with 32), and the meter signs
   its *own local time*, not the UTC instant behind it. An ISA document is already a
   whole charging session: start and stop reading sit in one signed block, so unlike
   every other format this one does not insist on two documents.
   Beyond the four upstream tests, the unused ISA fixtures are now covered: two of them
   are genuine tampering and are reported as invalid, two are incomplete transactions
   whose signatures nevertheless hold, and `isa-edl-40p-veri-fail` is byte-for-byte
   identical to `isa-edl-40p-ok` — its "failure" lives only in the container's unsigned
   `context` attribute, which must never be allowed to downgrade a signature.
4. **chargeIT container + BSM + GDF** ✅ **done** → `ChargeITTests` (13), `GDFCrypt01Tests` (3).
   Two generations of the chargeIT container are in the field, and the older one
   declares no context at all — so recognising it means checking whether it has the
   right shape, and the share of those checks that passed becomes the record's
   certainty. That number is what separates "a damaged chargeIT file" from "not a
   chargeIT file", which are very different things to tell somebody holding a receipt.
   BSM is the first format whose measurement reports **several quantities under one
   signature** — energy since the session began, the meter's lifetime total, and
   momentary power — so `Measurement.Name` and `.OBIS` had to become optional and
   the parts name themselves per `Phenomenon`. The golden files say so directly:
   they print `name: undefined`.
   The two `chargeIT/bsm/ocmf*.xml` fixtures are not chargeIT at all — the path is
   SAFE XML to OCMF, and only the meter is a BSM. They passed unchanged, but the
   identification flags they carry did **not** reach the record: the verification
   report does not print them, so the golden files could not catch it. That gap is
   why upstream asserts them separately, and it is now closed here too.
   GDF is ported but has no fixture in either implementation. Its tests confirm the
   plumbing — dispatch, curve, full-hash verification — and say plainly that they
   cannot confirm the byte layout, because they sign the very buffer this port builds.
5. **Mennekes** ✅ **done** → `MennekesTests` (7).
   The only format that describes whole *charging processes* rather than a stream of
   readings: each one carries exactly two signed readings plus the token the driver
   authorized with, so there is nothing to reassemble into sessions — the document
   already says which readings belong together.
   Which is exactly why it needs checks a signature cannot give. The meter signs each
   reading on its own, so two perfectly genuine readings from two *different* charging
   processes would both verify, and billing the difference between them would be
   wrong. The event counter, the page numbers and the direction of the reading are
   what tie them together, and a process failing those is reported as
   `InvalidMeasurement` rather than as a bad signature — the signatures are real, and
   saying otherwise would accuse the meter of something it did not do.
   Two details: the meter signs *the time it displays*, so the stated UTC offset is
   added rather than applied, and a 50 byte signature is not a longer signature —
   only its first 48 bytes are checked, and the other two belong inside the signed
   block where a 48 byte signature puts the event counter instead.
6. **ChargePoint** ✅ **done** → `ChargePointTests` (18).
   The only format that signs the *document* rather than the readings: the bytes of
   "secrrct", with the signature alongside in "secrrct.sign". So the readings carry
   no signatures of their own and are labelled by their place in the session —
   `StartValue`, `StopValue` — rather than as valid or invalid. They are as good as
   the document and no better, and saying "valid signature" about one would claim
   evidence that does not exist.
   Both upstream fixes from §7b landed with it: the public-key fallback stops at the
   first key that verifies, and the hash variable is spelled `sha384Value`.
   Two shapes exist. Upstream tests only the newer one, so the older *invoice* shape
   — tariffs, parking periods, and a charging session whose span has to be worked out
   from the line items — had no coverage in either implementation. It does now: six
   tariff variants on both curves, twelve records, all verifying. Whether a variant
   records a parking period is stated per case, because the ones billed purely by
   time or energy do not, and a test that expected parking everywhere would have to
   be weakened until it checked nothing.
7. **PCDF** ✅ **done** → `PCDFTests` (17).
   A whole charging session on one line: fourteen parenthesised fields, the last of
   which signs the thirteen before it. What is signed is the document's **text** up
   to the signature — not a reassembled buffer — which makes verification unusually
   direct and leaves nothing in the layout to get subtly wrong. The layout *is* the
   document.
   What can go wrong instead is everything the fields claim, and those faults arrive
   in groups: a meter that lost its clock reports several impossible things at once.
   So they are collected and reported together rather than one at a time.
   Unlike every meter format, PCDF needs no second reading — it states the energy
   delivered during the session directly rather than as the difference between two
   meter states, so one reading is the whole answer.
   The document carries its own public key. That is not circular, but it does mean a
   key handed over alongside is a second opinion rather than a missing piece: when
   the two disagree the reading stops, because there is no honest way to choose
   between them.
8. **PTB container + XMLContainer + KEBA** ✅ **done** → `ContainerTests` (10).
   KEBA is a SAFE XML container carrying **100 OCMF documents** that group into one
   charging session of 190 readings — by far the largest fixture, and it passed
   unchanged except for the session id (see below).
   PTB is a small envelope around two OCMF documents plus the one thing OCMF cannot
   say: where the charging station stands. Its schema is checked strictly and every
   violation reported at once, because the place is exactly what somebody would have
   to falsify to bill a driver for a session at a station they never visited.
   The `XMLContainer` is ported faithfully and **stops where ChargyCore.TS stops**:
   it reads the container, checks it for internal consistency — one key, one
   signature method, one encoding throughout — and then reports that it cannot turn
   the values into a charging session. Upstream leaves that conversion as a ToDo, and
   inventing one here would mean claiming to verify something the reference
   implementation does not. A test pins that, so the day upstream finishes it, it says so.

   Two findings. The two `PTBContainer/*.expected.txt` files are **orphans in both
   repositories**: their report format (`format: ptb`, `energyDifferenceWh`) is
   produced by no code in ChargyCore.TS, and the three tests that would use their
   fixtures are `test.skip` — the fixtures are hand-made demo data that OCMF itself
   rejects. So the golden-file count is 23 reachable, not 25. A test pins that the
   PTB *envelope* accepts them and OCMF is what turns them down, since the fixtures
   are checked in, look usable, and are not.
   And OCMF was **discarding everything the container knew** — the address, the
   geographical location, the description, the firmware — rebuilding the charging
   station from identifications alone. No golden file prints an address, so nothing
   caught it. That is the whole reason a container exists, and an EV driver shown
   only a meter serial number had been told less than the file contained.
9. **QIDigital DCC/DCoA/DCoC + OCPI** ✅ **done** → `OCPITests` (4), `QIDigitalTests` (4).
   OCPI carries two shapes. The older is a thin envelope around a single signed OCMF
   value plus what the roaming protocol knows and the signed data does not. The newer
   declares itself as `ocpi-2.1` and is, field for field, the newer chargeIT
   container — so it is **handed to that reader** rather than copied, because two
   readers for one shape drift apart and an EV driver would then get a different
   answer depending on which name the file happened to carry. (ChargyCore.TS does
   copy it; `ChargeITContainer.TryParseNewContainer` is the entry point that makes
   delegation possible here.)
   The substance is the meter: the container describes it and so does the signed
   payload, and they are not equal. What the meter signed wins; the container may
   only fill the gaps. OCMF has no field for a manufacturer's web address or a
   hardware revision, so those can only come from the container — which is also the
   clearest evidence that the merge happened at all.
   The three QIDigital files are **34 TypeScript interface declarations and nothing
   else** — no parser, no verification, no fixture, and referenced nowhere but
   `index.ts`. A TypeScript interface costs nothing at runtime; a C# data model has
   to be written out, so it is, with `TryParse`/`ToJSON` throughout and a round-trip
   test that says nothing was dropped and nothing invented. That is the only claim
   available without a real certificate, and it is stated as such.
10. **The remaining OCMF sub-features** ✅ **done** → `OCMFTariffTextTests` (4),
   `OCMFBETTariffTests` (20), `OCMFModernSignatureTests` (20), `OCMFVersionTests` (16),
   `OCMFDiagnosticsTests` (3). Deferred from step 2 and pulled in here.

   **The BET tariff text extension.** OCMF signs the tariff as free text in `TT`, which
   leaves an EV driver with a price they cannot check: `001;EUR;100;59;10;120` states
   what was charged only to somebody who already knows what the fields mean. The three
   profiles agreed at the Bonner Eichrechtstage give that string a meaning, and
   `OCMFBonnTariff` turns it into an ordinary charging tariff — the same shape a roaming
   platform would have delivered, except that this one arrived inside the meter's
   signature. A text that is *not* one of the three profiles is not an error: `TT` was
   free text before the extension existed and still is, so it becomes a tariff carrying
   only its own identification. Nine fixtures with expected mappings shared with
   ChargyCore.TS pin the result, and each fixture's signature is checked against the key
   it ships with — by BouncyCastle directly, not through Chargy, because a fixture is
   only evidence of something if it is what it claims to be.

   **What the payload says beyond the readings** now reaches the record.
   `OCMFChargeTransparencyRecord` carries an `OCMFInfo` with the gateway, the meter, the
   tariff text and its interpretation, the controller firmware and the loss
   compensation; `OCMFMeasurementValue` carries the pagination counter, the transaction
   type, the error index or flags, the cumulated loss and the meter status. `LC` becomes
   the connector's cable, `CF` the charging station's firmware — but only where the
   document or the container actually **named** a station: where nothing did, Chargy
   invents one to hold the meter, and giving that invention a signed firmware version
   would attach a real fact to a device no file ever mentioned.

   **Errors reach the reading.** Every verification failure was already recorded on the
   document as a stable key plus, where there is one, a technical detail. Those errors
   are now copied onto each reading's `CryptoResult`, which is the difference between
   telling an EV driver "invalid" and telling them why — and between "the signature does
   not match", which says something about the charging session, and "this curve is not
   implemented", which says something about Chargy.

   **The version matrix.** The fixtures in this repository come from the meters somebody
   happened to send a file from, which leaves whole OCMF versions covered by nothing.
   `OCMFVersionTestData` generates one signed document per version, every field holding a
   different value, and reads it back twice — with and without `FV`, because OCMF made
   the version optional. The generator is ChargyCore.TS's, reproduced down to the bit
   (FNV-1a seeding an xorshift32), so both implementations are fed the same documents;
   the signature is made here with a key derived from the same stream, since neither
   implementation signs deterministically and neither needs to.

   **Modern signatures.** Ed25519, Ed448 and all three ML-DSA parameter sets verify end
   to end, from a raw hex key and from the PEM the pipeline detects on its own. Each
   fixture's PEM is checked to be the SPKI wrapper of its own raw key, since two files
   claiming to hold the same key while holding different ones would make one half of
   these tests verify a document the other half cannot. `OCMFSignatureDisplay` was ported
   alongside: an EdDSA signature is r and s written one after the other and can be split,
   an ML-DSA signature has no components to split, and an ECDSA signature is a DER
   structure around two numbers — presenting all three alike would tell a reader that a
   value is something it is not.

   Two things were found along the way and fixed. `MergeChargeTransparencyRecords` built
   a fresh record even when a single file had held the only one, silently dropping its
   warnings, its certainty and everything a format-specific record carries: handing over
   a public key alongside a charging session cost the session half of what had been read
   out of it. And the `001-01__xss.ocmf` fixture, unused by either implementation, is now
   read: HTML and JavaScript inside payload strings have to arrive **untouched**, because
   those exact characters are what the meter signed and escaping them is the presentation
   layer's job at the moment of presentation.

### Phase 5 — Live link & online features ✅ **done, as far as there is anything to port**
→ `ChargeTransparencyLiveLinkTests` (4), `SimpleURLTests` (9).

Most of this arrived with the Phase 3 detector and only lacked tests, which is where
the two defects below came from. What the phase covers:

**A live link** is the one input that is not evidence: it is a list of addresses,
printed on a sticker anybody can replace, that somebody is being invited to contact.
`IsAChargeTransparencyLiveLink` therefore checks the whole document rather than only
its JSON-LD context, and does so through `TryParse`, so that the predicate and the
reader can never answer differently — a predicate saying yes where the reader says no
would send an application off to fetch data through a document nobody could read. A
transport Chargy cannot speak rejects the link entirely: keeping the readable ones
would leave an application believing it had been told everything. A link without a
timestamp is stamped when it is read, because live data is only worth something if its
age is known and a sticker carries no clock.

**A bare URL** is recognised from a text file and from a photographed QR code, and is
**not** contacted unless the application supplies a resolver. Following the link is
useful and it is also an observation — it tells the operator that this driver is
looking at this charging session, right now — so it happens only when asked. Only
`http:` and `https:` are accepted at all.

**`HTTPURLResolver`** is where Hermod earns its place, and it is the only genuinely new
code in this phase, so it is tested against a real socket rather than a stub: a local
`HttpListener` checks that the request really asks for `application/chargy`, that a
service answering with that content type comes back marked as one with its answer
attached, that anything else stays a bare URL, and that an unreachable service leaves
the URL as it was — an EV driver whose link is down has not thereby failed their
verification.

Two defects surfaced, both mine. `IsAChargeTransparencyLiveLink` checked only the
context while the structural validation sat in `TryParse`, so the published predicate
answered a weaker question than upstream's. And `TryParse` rejected a `timestamp` that
Newtonsoft had turned into a date on its own — the whole reason `ChargyLib.ParseJSON`
exists (§7a) — which would have cost any caller who reached the public API with a plain
`JObject.Parse` their live link. It now accepts a date token and renders it the way
Chargy writes instants; nothing here is signed, so nothing depends on the original
characters.

**What ChargyCore.TS declares but does not implement**, and therefore has no
counterpart to port:

| Declared | State upstream |
|---|---|
| The three transports (`https`, `httpSSE`, `websocket`) | Data model only. No client connects to any of them — `grep` over `src/` finds the transport types nowhere but in the interface that declares them. |
| `TOTPConfig` (`initialSharedSecret`, `timeStep`) | Data model only. No code computes a one-time password; the algorithm lives in the separate `DynamicQRCodes` repository. |

Both are carried faithfully as data: a link's transports, their weighted endpoint
lists and their TOTP configuration are read, kept and written back.

### Phase 5b — The live link client ✅ **done, and new rather than ported**
→ `TOTPGeneratorTests` (6), `LiveLinkEndpointTests` (9), `LiveLinkClientTests` (7).

A charge transparency file is written after the fact; a live link is the same evidence
while the car is still plugged in. `ChargeTransparencyLiveLinkClient` follows one and
reports what the station sends, over all three transports. **Every update goes through
the same pipeline as a file** — what arrives over a WebSocket is not more trustworthy
for having arrived quickly, so it is verified the same way, and the tests assert a
valid signature at the end of every transport.

Nothing here happens unless an application asks for it: this is the first part of
Chargy that opens a network connection on an EV driver's behalf.

**The TOTP was not written.** Hermod — already a dependency — implements the same
scheme as the Dynamic QR-Code reference implementations, together with a `TOTPConfig`,
a `TOTP` request header and clients that send it themselves. A second implementation of
one algorithm inside one dependency chain is exactly the drift that locks a driver out
of their own charging session, so ChargyCore only translates what a live link states
into what Hermod takes. What *is* tested here is the dependency, against the reference
implementation's own vectors: a one-time password is worth nothing unless the station
and the phone derive the same one, which makes those vectors a compatibility contract
of the same kind as the shared golden reports. Hermod passes all of them.

The password travels in the `TOTP` header, not in the address — an address ends up in
every server log along the way, which would outlive the ten seconds the password is
good for. The `{totp}` placeholder of the Dynamic QR-Codes convention is honoured where
a URL carries one, because there the address is all a QR code can hold.

Endpoints are chosen the way DNS chooses service records, which is what `priority` and
`weight` are named after: lower priority first, and equal priorities **drawn** in
proportion to their weight rather than sorted — a client that always took the heaviest
endpoint first would send every driver to one host and leave the weights meaning
nothing.

Two things are worth recording:

* **A polling loop must give up on an address that has never answered.** The first
  version kept asking, so a station whose first address was dead was never reached at
  its second — the fallback addresses were decoration. Once an address has answered,
  a failure is a moment with nothing new rather than a wrong address, and polling
  carries on.
* **Hermod cannot yet stream a server-sent event response**, so that one transport uses
  the runtime's own HTTP client. A chunked `text/event-stream` response is consumed
  before the call returns, so a stream that never ends never returns; a close-delimited
  one comes back with its socket already disposed. Both were found with a probe against
  a local server and are worth fixing in Hermod — when they are, the three transports
  become one stack again. The other two transports use Hermod throughout.

### Phase 6 — Polish & release ✅ **done**

**Documentation comments** were never a separate task in the end: `GenerateDocumentationFile`
has been on since Phase 0, so an undocumented public member has been a build warning
throughout and the build has run warning-free throughout. The one gap this phase found
was a `<param>` tag missing from `Authorization` after Phase 5 added a parameter to it —
which is exactly the kind of thing a compiler should be catching rather than a reviewer.

**`Directory.Build.props`** now holds what the two library projects had been repeating:
the target framework and language settings, the package metadata, deterministic builds,
symbol packages and the packed README. `IsPackable` defaults to **false** there and is
turned on per project, so a new project has to say that it wants to be published rather
than discover that it already is.

**`ChargyVerify`** is a command line verifier and a worked example of the API in one:
read the files, hand them to the detector, print what came back. It is deliberately thin
— every interesting decision belongs in the library, and an application should not have
to know that an OCMF string, a ZIP of chargeIT containers and a photograph of a QR code
are three different problems. It exits `0` when everything read verified, `1` when
something did not, `2` when the input was not charge transparency data at all and `64` on
a command line it could not parse, so it is usable from a script as well as readable by a
person. Numbers are printed culture-invariantly: a reading written as `50,387945` on one
machine and `50.387945` on the next cannot be compared, pasted into a bug report or piped
into anything.

**The README** gained a usage section, the command line section, and corrected status
tables — they still claimed Phase 4 was in progress and OCMF was next.

**Packaging** works: the two library projects pack with their documentation, the README
and a symbol package, and CI builds them as artifacts. Individually rather than
solution-wide, because Styx and Hermod are in the solution too. ChargyCore.NET is
consumed as a project reference, exactly as its own CI does and as Hermod consumes
Styx.

---

## 7. Risks & mitigations

| Risk | Mitigation |
|---|---|
| **Timestamp formatting drift** between `moment` and `DateTimeOffset` breaks golden files | Dedicated `ChargyLib.FormatTimestamp` with per-format behaviour + focused unit tests, tackled in Phase 1 before any format work |
| **Decimal formatting drift** (`decimal.js` `toString()` vs `System.Decimal.ToString()`), e.g. trailing zeros in `value 1.1.1 value: 268.978` | Explicit `FormatDecimal` helper mirroring `decimal.js` semantics + unit tests |
| **JSON property order / JSON-LD `@id`, `@context` keys** | `JObject` preserves insertion order; `CanonicalJSON` used wherever signatures depend on serialization |
| PDF/A-3 edge cases in the hand-rolled extractor | Interface-based; swap in PdfPig if the fixtures demand it — decision deferred to Phase 3 with a concrete fallback |
| Scope: ~24k LOC TS → multi-week effort | Strict phase boundaries; every phase leaves a building solution with more green golden tests than the previous one |
| Semantic drift from ChargyCore.TS over time | The 25 shared `*.expected.txt` files are the contract; any future TS change shows up as a .NET test failure |

---

## 7a. Findings from the port

Things discovered while porting that are worth knowing in ChargyCore.TS as well.

### The EMH and GDF signature buffers depend on the verifying machine's time zone

`chargyLib.SetTimestamp()` / `SetTimestamp32()` compute

```ts
timestamp.unix() + (addLocalOffset ? 60 * timestamp.utcOffset() : 0)
```

where `utcOffset()` is the offset of the **local time zone of whoever runs the
verification**, because `parseUTC()` returns `moment.utc(...).local()`.

`EMHCrypt01` and `GDFCrypt01` use the default `addLocalOffset = true`; `Alfen`
explicitly passes `false`. So an EMH or GDF measurement that verifies in Germany
fails in Portugal, and vice versa — the fixtures happen to pass because they were
produced by German meters and are tested on German machines. A CI runner on UTC
would fail them.

ChargyCore.NET reproduces the arithmetic exactly, but takes the time zone as an
explicit parameter (`TimeZoneInfo`, defaulting to `TimeZoneInfo.Local` so the
default behaviour is unchanged). The tests pin `Europe/Berlin`, which makes them
reproducible everywhere, including the UTC CI runners.

The proper fix in both implementations is to carry the charging station's time
zone in the charge transparency record instead of guessing it from the verifier.
That is a data model change and out of scope for the port.

### Newtonsoft.Json rewrites timestamps unless told not to

Found while porting BSM. `JObject.Parse` turns every string that looks like a date
into a `DateTime`, and reading it back out as a string then yields .NET's rendering
rather than the one in the file. `"2021-11-30T12:38:47+01:00"` came back as the
empty string, and every BSM snapshot failed its own consistency check.

This is not a formatting detail. The meters sign their timestamps as text, several
formats keep the meter's own UTC offset in them, and `MeasurementValue.Timestamp`
is documented to preserve exactly what the parser produced. A re-rendered timestamp
is a different timestamp, and the reports shared with ChargyCore.TS print it
verbatim. Every JSON entry point now goes through `ChargyLib.ParseJSON`, which sets
`DateParseHandling.None` — and, while it is there, `FloatParseHandling.Decimal` so a
meter reading never becomes a binary float, and a check that nothing follows the
JSON object.

This has no counterpart upstream: `JSON.parse` leaves strings alone.

Numbers, however, must stay **doubles** — Newtonsoft's default. Reading them as
decimals looks like the safer choice and is not: a decimal remembers that a meter
wrote `0.00` rather than `0`, canonical JSON renders numbers the ECMAScript way,
and the OCMF session identity is a hash over that canonical form. Preserving the
trailing zeros gives the same charging session a different identity from the one
every other implementation computes, which is exactly what the KEBA golden file
caught. The precision a double costs is irrelevant here — meter readings stay far
inside the fifteen significant digits a double round-trips exactly — while the
identity is not negotiable. `FloatParseHandling.Decimal` was added speculatively
alongside the date fix and has been removed again.

### `getInt64Bytes()` is a 32 bit conversion

JavaScript bitwise operators coerce to signed 32 bit, so `getInt64Bytes()` emits
eight bytes but sign-extends everything at or above 2^31 and wraps to zero at
2^32. Verified against Node: `2147483648` → `ffffffff80000000`, `4294967296` →
`0000000000000000`.

Harmless for real meter readings, which stay well below 2^31, and faithfully
reproduced by `ChargyLib.GetInt64Bytes` so that genuine measurements keep
verifying. Worth a bounds check in both implementations rather than a silent wrap.

### Merging one record into a fresh one loses what the format put on it

`MergeChargeTransparencyRecords` builds a new record and copies the fields it
knows about. Its own comment says as much — *"the CTRs might have different
@context values and additional context/format specific data!"* — and the case
where it costs something is not an exotic one: an EV driver hands over a charging
session **and its public key**, which is the ordinary way to verify anything. Two
files, one record, and the merge runs.

What does not survive the copy is everything the merge has no field for: the
record's warnings, its certainty, its overall status, and — once a format carries
more than the common model, as OCMF now does with its payload block — the
format-specific data the comment already anticipated.

This port returns the record itself when exactly one file held one, because there
is nothing to merge in that case; the public keys and unreadable files are added
to it. Worth the same in ChargyCore.TS.

---

## 7b. Upstream changes to track

ChargyCore.TS is being worked on in parallel. Changes that land there after a
part of this port was written have to be carried over deliberately, because the
golden reports alone will not catch a behavioural difference in a code path no
fixture exercises.

### `46eec22` — "Fix secp521r1 typo and repair the verify pipeline"

Baseline of this port moved from `f6b3b3b` to `46eec22`. Four changes matter:

* **`IECCurves.secp512r1` → `secp521r1`** — already the case here; `ECCurve`
  keeps accepting the misspelling on input for records written before the fix.
* **`chargePoint.ts` public key fallback** — a `break` was added so the first
  matching public key wins. Without it, when no key matched the EVSE Id and all
  available keys were tried as a fallback, a later non-matching key overwrote an
  already successful result. ✅ Ported with the fix in Phase 4 step 6.
* **`CryptoUtils.signJSONMessage` verifies before attaching** — a signature that
  does not validate is no longer left behind in the caller's message.
  **Phase 2.**
* **`CryptoUtils.signJSONMessage` returns `signaturesCreated > 0`** — signing
  with only unusable key pairs used to report success, so callers could mistake
  an unsigned message for a signed one. **Phase 2.**

`tests/fixtures/OCMF/versionTestData.ts` changed with it and has been re-synced;
all 203 fixtures are byte-identical with upstream again.

### `9338dae`…`8d7735e` — the timestamp, secp224k1 and CI batch

Baseline moved to `8d7735e`. No fixture data changed. What matters here:

* **`4d9f778` — the signed timestamp offset** ✅ **ported.** This is the bug
  reported in §7a, and upstream fixed it more thoroughly than proposed: a
  timestamp ending in a numeric offset (`…+01:00`) states the offset the meter
  used and wins; a `Z` suffix or no zone says nothing about the meter, so
  Europe/Berlin is resolved for that instant, daylight saving included. Upstream
  confirms the impact — under UTC, five chargeIT tests reported
  `InvalidSignature` for a valid record.

  `ChargyLib.MeterLocalTime()` now mirrors this, `MeterTimeZone` is
  Europe/Berlin, and the parameter is called `AddMeterOffset` rather than
  `addLocalOffset`: the offset is the meter's, never the verifying machine's,
  and conflating the two is what caused the bug.

* **`6dac201` — secp224k1 hardened.** `validate()` is the only production
  method and now fails closed: `r`/`s` must be strictly positive (the old
  `== 0` check let negative values through), and the public key is checked to
  be a point on the curve before it reaches the group arithmetic. Any failure
  returns `false` instead of throwing, so a caller trying several candidate keys
  is never interrupted. `Sign()` and `PublicKeyGenerate()` are documented as not
  for production — not constant time, caller-supplied nonce.
  **Phase 2:** the BouncyCastle wrapper gets the point validation for free, but
  must keep the fail-closed contract and the strict `r`/`s` bounds.

* **`7ad9897` — `sha385Value` → `sha384Value`.** A misspelled variable, not a
  behavioural change, but the name is worth carrying over correctly.
  **Phase 4** (chargePoint).

* `9338dae` (PDF.js 6.2 API), `2ddd0d5`, `47ff213`, `a367fb7`, `503a16d`,
  `0dab5cb`, `cbd16f6`, `8d7735e` — dependency, CI and README changes with no
  counterpart in this port.

---

## 7c. Known limitations of this port

### ML-DSA context strings are not supported

FIPS 204 defines an optional context string for ML-DSA, and ChargyCore.TS passes
it through to Noble. **BouncyCastle 2.7.0 does not expose it** — there is no
`ParametersWithContext` and `MLDsaSigner.Init()` takes the key parameters alone.

`MLDSASignatureSuite` therefore **throws `NotSupportedException`** when a non-empty
context is supplied, in both `Sign()` and `Verify()`. Dropping it quietly would
produce signatures that do not match the record, and on the verifying side it
would report a valid measurement as invalid without saying why — the one failure
mode a transparency software must not have.

An empty or absent context, which is what the OCMF records in the fixtures use,
works normally. If a context is ever needed, the options are a newer BouncyCastle
once it exposes one, or a hand-rolled FIPS 204 domain separator in front of the
message — the latter only with test vectors to prove it matches.

---

## 8. Decisions

| # | Question | Decision |
|---|---|---|
| 1 | Root namespace / assembly name | **`cloud.charging.open.chargy`** — consistent with `cloud.charging.open.API` / `.protocols` / `.utils`. Tests: `cloud.charging.open.chargy.tests` |
| 2 | Hermod/Styx referencing | **Sibling project references** `..\..\Hermod\Hermod\Hermod.csproj` and `..\..\Styx\Styx\Styx.csproj` — exactly how Hermod already references Styx |
| 3 | Test project name | **`ChargyCoreTests`** — matching `HermodTests` / `StyxTests` |
| 4 | QR code decoding & PDF/A-3 extraction | **In the core library, behind `IQRCodeDecoder` / `IPDFAttachmentExtractor`** with default implementations registered by `Chargy` — same out-of-the-box behaviour as ChargyCore.TS, still swappable |
| 5 | Measurement value type | **`System.Decimal`** — 28–29 significant digits, and full control over formatting, which matters because the values are printed raw into the golden reports |
| 6 | QR code decoding lives where? | **Revised in Phase 3.** Decision 4 put the default implementations in the core library. The PDF reader is there, and needs no dependency at all. The QR decoder is not: it pulls SkiaSharp with its native binaries, plus Svg.Skia and ZXing.Net. It therefore moved into **`ChargyCore.QRCodes`**, behind `IQRCodeDecoder`. This is closer to ChargyCore.TS than the original decision was — there the `canvas` / `pngjs` / `jpeg-js` modules are *optional* dependencies loaded at runtime, and QR reading degrades gracefully when they are absent. Without a decoder Chargy passes QR images through untouched, exactly as the TypeScript does |
| 7 | PDF/A-3 reading: hand-rolled or `PdfPig`? | **Hand-rolled**, as decision 4 preferred. Rather than parse cross-reference tables, `PDFDocument` scans the file for `N G obj` definitions directly. That treats classic tables, cross-reference streams, incremental updates and *damaged* tables all the same way — and a charge transparency record is far too important to lose to a table some invoice generator got wrong. Object streams, `FlateDecode`, `ASCIIHexDecode`, `ASCII85Decode` and the PNG predictors are supported; encrypted PDFs yield nothing |
| 8 | Where does the PDF reader live? | **Styx**, as `org.GraphDefined.Vanaheimr.Illias.PDFDocument` / `PDFParser` / `PDFObject` / `PDFEmbeddedFile`, next to the existing CBOR, COSE, CSV and JSON readers. Reading embedded files out of a PDF/A-3 is not a Chargy concern — ZUGFeRD and Factur-X invoices work the same way. The code was written from scratch against the PDF specification rather than derived from ChargyCore.TS, which uses `pdfjs-dist`, so it carries Styx's Apache-2.0 header. What stays in ChargyCore is `PDFAttachmentExtractor`: the closed list of attachment types worth looking at, behind `IPDFAttachmentExtractor` |
| 9 | The OCMF secp192 curves | **Deviation from ChargyCore.TS, on purpose.** ChargyCore.TS recognises `ECDSA-secp192k1-SHA256` and `ECDSA-secp192r1-SHA256` but maps neither to a curve, so both end as `InvalidPublicKey`: its JavaScript curve library does not carry them. BouncyCastle does, so this port verifies them. The difference is one-directional and safe — every record that verifies in ChargyCore.TS verifies here, and the only records judged differently are the ones the TypeScript implementation declines to judge at all. No golden file is affected, because no fixture uses these curves. The brainpool curves stay unverified in both, which is now a scope decision rather than a limitation: BouncyCastle carries those too |

### Deviations from ChargyCore.TS

Behaviour where this port deliberately differs. Kept together so the list stays
short and reviewable, because every entry weakens the golden-file parity contract.

| Where | Difference | Why |
|---|---|---|
| OCMF `secp192k1` / `secp192r1` | Verified here, `InvalidPublicKey` in ChargyCore.TS | BouncyCastle carries the curves; the JavaScript library does not. Decision 9 |
| `SmlReader` | SML lists may nest at most 64 levels | The reader is recursive and the input is a file somebody sent us. A few kilobytes of nothing but "list of one" exhausts the stack, and in .NET that ends the process instead of raising something catchable — a JavaScript engine turns the same input into an ordinary exception, so upstream needs no limit. A real GetListRes nests about seven levels. |
| OCMF `ocmf.lossCompensation` | Carried only when it is one: `LR` and `LU` both present | ChargyCore.TS keeps the raw `LC` object whatever it holds and separately checks the two fields before building a cable. A typed model cannot hold a malformed one, and a resistance without a unit is not a resistance — carrying it as if it were would put a number into the record that means nothing. The cable is built under exactly the same condition in both. |
| OCMF pagination on a reading | Each reading carries **its own** document's counter | ChargyCore.TS stamps every reading with the *first* document's counter. Invisible for a single-document record and wrong for the KEBA records, where a hundred documents with a hundred different counters make up one charging session. |
| `Authorization.IdentificationStatusText` | New field, alongside the boolean | OCMF 1.x writes a boolean in `IS`; the 0.1 reference data of the SAFE transparency software wrote `"VERIFIED"`. ChargyCore.TS keeps whichever arrived in one loosely typed field. Reducing the word to `true` would claim the meter said something simpler than it did, and dropping it lost the only thing it said at all. |

This costs exactly one golden file. `SAFE-Testdata-04` is signed with
`ECDSA-secp192k1-SHA256`, so ChargyCore.TS reports `InvalidPublicKey` on three lines
where this port reports `ValidSignature`. `SAFETestdata04_IsSignedOnACurveChargyCoreTSCannotVerify`
compares against the golden file with exactly that substitution applied, rather than
disabling the test or editing the fixture: every other line still has to match, and
the day ChargyCore.TS gains the curve the test fails and says so.

Everything else that looked like a bug was reported upstream and fixed there rather
than worked around here — the `secp512r1` typo, the host-timezone dependency in the
signed timestamps, and the hardcoded OCMF session id. That is the preferred route:
a deviation has to be maintained forever, an upstream fix does not.

### Still to settle (not blocking)

* **A record that verifies and delivers nothing is reported simply as verified.**
  The ISA fixtures `isa-edl-40p-begin-fail` and `isa-edl-40p-update-fail` are a
  `Transaction.Begin` and a `Transaction.Update` snapshot: their start and stop
  readings are identical, so the session delivered 0 kWh, and their signatures are
  perfectly valid. The SAFE reference software calls both "Ungültig" because they are
  not completed transactions. Neither ChargyCore.TS nor this port says anything about
  it — a caller has to compare the readings itself. Raising a warning would be a
  behaviour change and belongs upstream first, so it is recorded rather than
  implemented. `AnIncompleteTransactionStillCarriesAValidSignature` pins the current
  answer either way.

* **`PublicKeyParser.TryParseDER` handles named curves only.** A SubjectPublicKeyInfo
  that spells its curve out as explicit domain parameters instead of naming it by
  object identifier yields null, and therefore `InvalidPublicKey`. Every fixture and
  every meter seen so far names the curve, and an unreadable key shape is reported
  honestly rather than guessed at, so this is not urgent — but it is a real gap and
  was found by accident while testing the secp192 curves.

* **Existing prior art** — `VanaheimrElectric/libs/WWCP_OCPP/WWCP_OCPP_Common/Chargy/`
  holds a small (~700 LOC) partial Chargy port (`ChargyLib`, `ACrypt`, `EMHCrypt01`,
  `Signature`, `Ids`) under `cloud.charging.open.protocols.GermanCalibrationLaw`.
  Its `SetHex` / `SetTimestamp` / `SetUInt32` helper signatures are a good starting point
  for `ChargyLib.cs`. Whether WWCP_OCPP later drops its copy in favour of a reference to
  ChargyCore.NET is a separate decision, taken once ChargyCore.NET is published.
