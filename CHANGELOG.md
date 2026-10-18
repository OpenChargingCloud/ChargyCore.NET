# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
While the version number is below 1.0.0, breaking changes are released in minor
versions and are always listed first below.

No version of ChargyCore.NET has been released yet, so everything below is
*Unreleased* and describes what the first release will contain as a whole. That is
why this file can start now without leaving out any history: until something has
been published, there is no earlier version for an entry to be a change from. How
the port came about, phase by phase, is in [PORTING_PLAN.md](PORTING_PLAN.md).


## [Unreleased]

### Added

- **The port of [ChargyCore.TS](https://github.com/OpenChargingCloud/ChargyCore.TS)**,
  following upstream up to `29d1d01` (v0.16.1 and the reading of live link keys after
  it). Every one of the shared test fixtures is byte-identical with upstream, and the
  verification reports are compared line by line against upstream's golden files.

  - **Formats:** Alfen, Bauer (2 variants), ChargePoint (2 variants), EDL40 and
    ISA-EDL40 SML, EMH, GDF, Mennekes XML, OCMF v1.1 to v1.4 including the Bonner
    Eichrechtstage tariff text extension, and the Porsche Charging Data Format.
  - **Signatures:** ECDSA on secp192r1, secp192k1, secp224k1, secp256k1, P-256,
    P-384, P-521, brainpoolP256r1 and brainpoolP384r1; Ed25519 and Ed448;
    ML-DSA-44, ML-DSA-65 and ML-DSA-87, with FIPS 204 context strings.
  - **Representations:** plain files, the chargeIT, Chargy, SAFE XML, PTB, OCPI and
    generic XML containers, tar, ZIP, tar.gz and similar archives, and PDF/A-3
    files carrying a record as an embedded file.
  - **Live link:** `ChargeTransparencyLiveLinkClient` follows a charging session while
    it happens, including signed log messages and document signatures.

- **`cloud.charging.open.chargy.qrcodes`**, a separate package that reads records from
  QR code images (PNG, JPEG, GIF, WEBP, BMP and SVG), so that consumers who never see a
  QR code do not carry an image decoding stack.

- **`chargy-verify`**, a command line verifier and a worked example of the API. It
  exits `0` when everything read verified, `1` when something did not, `2` when the
  input was not charge transparency data and `64` on a command line it could not
  parse.

- **A public key with explicit curve parameters is read.** `PublicKeyParser.TryParseDER`
  used to accept a `SubjectPublicKeyInfo` only when it named its curve by object
  identifier, and one that spelled the curve out as explicit domain parameters became
  `InvalidPublicKey`. Such a key is now read as the curve its parameters describe, when
  that is one of the curves above and field, coefficients, generator, order and
  cofactor all match. Parameters of any other curve still yield no key, and so does a
  well-known curve with a generator of its own, the shape of CVE-2020-0601.

- **Every assembly records the commit it was built from** as `GitRepository` and
  `GitCommit` assembly metadata, marked `-dirty` when the working tree was not clean.

### Deviating from ChargyCore.TS

Deliberate, and listed with their reasons under "Deviations from ChargyCore.TS" in
[PORTING_PLAN.md](PORTING_PLAN.md). Every record that verifies upstream verifies
here as well, short of an SML file nested deeper than any meter writes one, and
the verdicts that differ are on records upstream declines to judge at all.

- OCMF on secp192k1, secp192r1, brainpool256r1 and brainpool384r1 verifies here and is
  `InvalidPublicKey` upstream, because the JavaScript curve library does not carry
  those curves.
- A public key with explicit curve parameters is read here and refused upstream.
- SML lists may nest at most 64 levels, because a deeper recursion ends a .NET process
  instead of raising an exception.
- An OCMF loss compensation is carried only when both `LR` and `LU` are present, each
  OCMF reading carries its own document's pagination counter, and
  `Authorization.IdentificationStatusText` keeps the word a SAFE 0.1 record wrote in
  `IS`.
