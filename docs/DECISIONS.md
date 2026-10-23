# ERAF Decision Log

ADR-style records. Newest decisions are appended; a superseded decision stays and is marked. Approval by the supervisor (Cursor) is recorded per entry.

---

## ADR-001 — Binary container (Design A), not a renamed or zipped file

**Status:** Accepted (plan approval 2026-10-23)

**Context.** One `.eraf` must hold one photo and one voice note and open identically on Windows and Android. Options: rename or concatenate JPEG/WAV, a ZIP with two entries, or a purpose-built binary container.

**Decision.** A purpose-built container: 64-byte little-endian header, optional SHA-256 block, then raw JPEG and M4A payloads ([spec](ERAF_FORMAT_SPEC.md)).

**Consequences.**
- Exact-length validation, tiny fixed parse surface, no dependency on a ZIP library, payloads stored byte for byte.
- Standard tools will not open it; the app (and format libraries) are required.
- Any evolution needs a version bump and a new spec revision; v1 readers are strict.

**Rejected.** ZIP: more parsing surface (directories, compression, duplicate entries, zip-bomb handling) than a two-payload file justifies. Renamed JPEG/WAV with appended data: ambiguous, breaks in tools that sniff or rewrite the file.

---

## ADR-002 — v1 media: JPEG image and AAC-LC in M4A

**Status:** Accepted (plan approval 2026-10-23)

**Context.** The Windows recorder (`MediaCapture` with `MediaEncodingProfile.CreateM4a`) and Android's `MediaRecorder` (MPEG-4 + AAC) both produce AAC-LC in MPEG-4 natively; `MediaPlayer` plays it reliably.

**Decision.** `ImageMediaType = 1` (`image/jpeg`), `AudioMediaType = 1` (`audio/mp4`), `AudioCodec = 1` (`mp4a.40.2`). Non-JPEG imports are converted once to JPEG or rejected with a clear message; the preferred behavior is convert-on-import, to be confirmed when M3 is briefed.

**Consequences.** No codec negotiation in v1. Files are larger than Opus would give. IDs for other types stay reserved.

**Rejected.** Opus (extra native dependencies, uneven MediaCapture support), WAV (size), platform-specific containers.

---

## ADR-003 — Windows App SDK 2.5.1 on .NET 8

**Status:** Accepted by Cursor (M0 review, 2026-10-23)

**Context.** The plan expected Windows App SDK 1.6+. The current template and NuGet resolve `Microsoft.WindowsAppSDK` to 2.5.1, and Windows App Runtime 2.5.1 is installed on the development machine. The template used floating `*` versions.

**Decision.** Pin exactly: `Microsoft.WindowsAppSDK` 2.5.1, `Microsoft.Windows.SDK.BuildTools` 10.0.28000.2705, `Microsoft.Windows.SDK.BuildTools.WinApp` 0.7.1. Target `net8.0-windows10.0.26100.0`, `TargetPlatformMinVersion` 10.0.17763.0 (raise only when an API requires it).

**Consequences.** Reproducible restores. Upgrading any of these is a deliberate change recorded here. The app requires the matching Windows App Runtime at run time; distribution packaging is a later concern (M9).

---

## ADR-004 — Separate native format libraries on a shared spec and fixtures

**Status:** Accepted (plan approval 2026-10-23)

**Context.** Windows is C#/.NET; Android is Kotlin. Sharing code would need a cross-platform UI framework or a native core, both rejected as out of proportion for a 64-byte header.

**Decision.** `Eraf.Format` (C#) and a later `eraf-format` (Kotlin) are independent implementations of one normative spec, kept in agreement by golden fixtures in `fixtures/eraf` with expected values that do not come from either implementation. No MAUI, Flutter or React Native.

**Consequences.** Drift risk is real and is controlled by the spec's ordered validation (§9) and exact error codes, and by fixtures that both suites must pass. A format change touches the spec, fixtures and both libraries.

---

## ADR-005 — `winapp run` is the documented Windows launch path

**Status:** Accepted by Cursor (M0 close-out, 2026-10-23)

**Context.** A packaged WinUI app needs package identity to start. Running the exe directly, or `dotnet run`, fails with `REGDB_E_CLASSNOTREG`. The `Microsoft.Windows.SDK.BuildTools.WinApp` package is meant to hook `dotnet run` and register a debug identity, but on this machine the hook never activates (no `[WinAppRunSupport]` messages; cause not determined). Developer Mode must be on. Invoking the bundled `winapp.exe` directly builds, registers a development package and launches the app.

**Decision.** Launch with `winapp run src\Eraf.Windows\Eraf.Windows.csproj --arch x64`, using `%USERPROFILE%\.nuget\packages\microsoft.windows.sdk.buildtools.winapp\0.7.1\tools\win-x64\winapp.exe`. Do not document plain `dotnet run` as working until the hook is fixed.

**Consequences.** The debug package (`90DA0760-F0D9-4200-A4DB-1BDF858BEB2B`) is re-registered on each run and can be removed with `Remove-AppxPackage`. Unpackaged mode (`WindowsPackageType=None`) remains a fallback but loses package identity, which camera/microphone capability declarations depend on.

---

## ADR-006 — Spec clarifications C1–C4 accepted into v1

**Status:** Accepted by Cursor (M1 review, 2026-10-23)

**Context.** [ERAF_FORMAT_SPEC.md](ERAF_FORMAT_SPEC.md) §13 proposed four clarifications beyond the plan sketch without changing the 64-byte header layout.

**Decision.** All four are **normative for v1**:

| ID | Clarification |
|---|---|
| C1 | Mandatory payload signature check: image `FF D8 FF`; audio bytes 4–7 = `ftyp` |
| C2 | Fixed validation order and named error codes (spec §9) |
| C3 | `AudioDurationMs > 600000` rejected; `CreatedUtcUnixMs` not validated |
| C4 | Trailing bytes rejected; `Reserved2` outside CRC scope |

**Consequences.** `Eraf.Format` (M2) and the future Kotlin library must implement these exactly. Fixture recipes N14, N16, N18, N19 already encode them.

---

## ADR-007 — Convert-on-import confirmed; Windows App SDK file picker

**Status:** Convert-on-import accepted by Cursor (M3 brief); picker choice made by the implementer in M3, for review.

**Context.** ADR-002 left open whether a non-JPEG image is converted or rejected. Separately, the classic `Windows.Storage.Pickers.FileOpenPicker` initialised with `InitializeWithWindow` failed with `E_FAIL` in the packaged app launched via `winapp run`.

**Decision.**
- Non-JPEG images are decoded and re-encoded **once** to JPEG on import (`BitmapDecoder` / `BitmapEncoder`, alpha ignored). Files that already start with `FF D8 FF` are kept byte for byte, never recompressed. The decision is `ErafMedia.PlanImageImport` in `Eraf.Format`, so it follows the spec's limits and signature rule and is unit-tested; the converted output is planned again against the same rules.
- Image size is checked from file properties **before** reading, using the 64 MiB payload limit.
- The picker is `Microsoft.Windows.Storage.Pickers.FileOpenPicker(AppWindow.Id)` from the Windows App SDK (documented for 2.0). The old API is not used.
- The photo lives in an in-memory `ImageSession`, so there are no temp files to clean up.

**Consequences.** Import works for JPEG, PNG, BMP, GIF and TIFF in the picker filter. Conversion is lossy and drops alpha. A JPEG is trusted by signature only, so a truncated JPEG that the system can partially decode is accepted (noted in STATUS). The picker returns a path, which the app reads directly (full-trust package).

---

## Pending decisions (not yet made)

| Topic | Needed by |
|---|---|
| Pause/resume recording (only if `MediaCapture` supports it reliably) | M4 |
| Project license | Before any public release |
