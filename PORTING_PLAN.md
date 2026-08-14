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
│   │   ├── OCMF/         OCMF.cs, OCMFv1_x.cs, OCMFDataStructures.cs,
│   │   │                 OCMFVersion.cs, BETTariffTextExtension.cs
│   │   ├── Alfen/        Alfen.cs, AlfenCrypt01.cs
│   │   ├── EMH/          EMHCrypt01.cs
│   │   ├── GDF/          GDFCrypt01.cs
│   │   ├── BSM/          BSMCrypt01.cs
│   │   ├── EDL40/        EDL40.cs, SMLReader.cs
│   │   ├── Mennekes/     Mennekes.cs, MennekesCrypt01.cs
│   │   ├── ChargePoint/  ChargePoint.cs, ChargePointCrypt01.cs
│   │   ├── PCDF/         PCDF.cs, PCDFCrypt01.cs
│   │   └── QIDigital/    DCC.cs, DCoA.cs, DCoC.cs
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
| `Mennekes`, `SimpleURLs` | … | 7 each |
| `ChargePoint`, `EMHCrypt01`, `OCMFVersions`, `SAFE_withChargyExtensions` | … | 6 each |
| `PublicKeyFiles` | … | 5 |
| `EDL40`, `OCMFTariffText` | … | 4 each |
| `OCMF`, `ChargeTransparencyLiveLink`, `chargyInterfaces` | … | 3 each |
| `OCMFDiagnostics`, `PTBContainer` | … | 2 each |
| `KEBA`, `OCMFErrorPropagation` | … | 1 each |
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
> and are part of the 203 files asserted above.

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

### Phase 3 — I/O & container pipeline 🚧 next
`ContentFormatDetector` (the `DetectAndConvertContentFormat` state machine), archive
readers (zip/tar/gz/bz2), `IPDFAttachmentExtractor`, `IQRCodeDecoder`, XML/JSON format
dispatch, `SimpleURL`, `ChargeTransparencyLiveLink`.
*Exit:* `SimpleURLsTests`, `ChargeTransparencyLiveLinkTests` green; archives/PDF/QR
extraction proven against binary fixtures (~15 cases).

### Phase 4 — Formats, one directory at a time ⬜
Ordered so each step unlocks the largest number of golden tests with the least new
infrastructure. Every step is a self-contained increment: format + its `ACrypt` + its tests.

1. **SAFE XML container + Alfen** → `SAFETests`, `SAFE_withChargyExtensionsTests`, `AlfenTests` (23)
2. **OCMF** (+ versions, modern signatures, tariff text, BET extension, diagnostics, error propagation) → (~16+)
3. **EMH + EDL40 (SML)** → `EMHCrypt01Tests`, `EDL40Tests` (10)
4. **chargeIT container + BSM + GDF** → `ChargeITTests` (11)
5. **Mennekes** → `MennekesTests` (7)
6. **ChargePoint** → `ChargePointTests` (6)
7. **PCDF** → `PCDFTests` (16)
8. **PTB container + XMLContainer + KEBA** → (3)
9. **QIDigital DCC/DCoA/DCoC + OCPI** → `OCPITests`

### Phase 5 — Live link & online features ⬜
Hermod-based `IURLResolver`, live-link transports (HTTPS, HTTP SSE, WebSocket), TOTP.

### Phase 6 — Polish & release ⬜
XML documentation comments on the whole public surface, `README.md` with usage examples,
a small sample/CLI project for verifying a file from the command line, `Directory.Build.props`
with the shared package metadata, optional NuGet packaging.

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

### `getInt64Bytes()` is a 32 bit conversion

JavaScript bitwise operators coerce to signed 32 bit, so `getInt64Bytes()` emits
eight bytes but sign-extends everything at or above 2^31 and wraps to zero at
2^32. Verified against Node: `2147483648` → `ffffffff80000000`, `4294967296` →
`0000000000000000`.

Harmless for real meter readings, which stay well below 2^31, and faithfully
reproduced by `ChargyLib.GetInt64Bytes` so that genuine measurements keep
verifying. Worth a bounds check in both implementations rather than a silent wrap.

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
  already successful result. **Port the fixed behaviour in Phase 4**, not the
  loop as it was read earlier.
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

### Still to settle (not blocking)

* **Existing prior art** — `VanaheimrElectric/libs/WWCP_OCPP/WWCP_OCPP_Common/Chargy/`
  holds a small (~700 LOC) partial Chargy port (`ChargyLib`, `ACrypt`, `EMHCrypt01`,
  `Signature`, `Ids`) under `cloud.charging.open.protocols.GermanCalibrationLaw`.
  Its `SetHex` / `SetTimestamp` / `SetUInt32` helper signatures are a good starting point
  for `ChargyLib.cs`. Whether WWCP_OCPP later drops its copy in favour of a reference to
  ChargyCore.NET is a separate decision, taken once ChargyCore.NET is published.
