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

**Status:** Accepted by Cursor (M3 review, 2026-10-23).

**Context.** ADR-002 left open whether a non-JPEG image is converted or rejected. Separately, the classic `Windows.Storage.Pickers.FileOpenPicker` initialised with `InitializeWithWindow` failed with `E_FAIL` in the packaged app launched via `winapp run`.

**Decision.**
- Non-JPEG images are decoded and re-encoded **once** to JPEG on import (`BitmapDecoder` / `BitmapEncoder`, alpha ignored). Files that already start with `FF D8 FF` are kept byte for byte, never recompressed. The decision is `ErafMedia.PlanImageImport` in `Eraf.Format`, so it follows the spec's limits and signature rule and is unit-tested; the converted output is planned again against the same rules.
- Image size is checked from file properties **before** reading, using the 64 MiB payload limit.
- The picker is `Microsoft.Windows.Storage.Pickers.FileOpenPicker(AppWindow.Id)` from the Windows App SDK (documented for 2.0). The old API is not used.
- The photo lives in an in-memory `ImageSession`, so there are no temp files to clean up.

**Consequences.** Import works for JPEG, PNG, BMP, GIF and TIFF in the picker filter. Conversion is lossy and drops alpha. A JPEG is trusted by signature only, so a truncated JPEG that the system can partially decode is accepted (noted in STATUS). The picker returns a path, which the app reads directly (full-trust package).

---

## ADR-008 — Truncated JPEG acceptance (signature-only)

**Status:** Accepted by Cursor (M3 review, 2026-10-23)

**Context.** A file that starts with `FF D8 FF` but is truncated can partially decode (top of image + gray). Spec §8.1 / C1 only requires the three-byte signature so the container and `ErafMedia` stay aligned.

**Decision.** **Keep accepting** such files in M3/v1. Do **not** add a mandatory trailing `FF D9` check to the wire format or to `ErafMedia` for v1.

**Consequences.** Users could save a half-image into an `.eraf` later. Mitigation deferred to **M6/M7**: at save (or preview), the app SHOULD surface a non-blocking warning if the system decoder reports an incomplete/corrupt decode, without rejecting the bytes solely for missing EOI. A future format version could tighten payload rules if needed.

---

## ADR-009 — Voice recording: in-memory AAC-LC M4A, stop/retry only

**Status:** Accepted by Cursor (M4 review, 2026-10-24).

**Context.** The format requires AAC-LC in MPEG-4. `MediaCapture` can record directly with `MediaEncodingProfile.CreateM4a`. The M4 brief asks for a documented pause/resume decision.

**Decision.**
- Record with `MediaCapture` (audio only) and `MediaEncodingProfile.CreateM4a(AudioEncodingQuality.Medium)` into an `InMemoryRandomAccessStream`. The result was verified on a real recording to be AAC-LC (audio object type 2), 44.1 kHz stereo, in an `mp42` MP4 (STATUS, M4 evidence).
- Playback streams the in-session bytes through `MediaPlayer` (`MediaSource.CreateFromStream`, `audio/mp4`). No temp files are created, so none must be deleted on discard or cancel.
- A finished recording is checked by `ErafMedia.CheckAudio` (empty, over 64 MiB, over 10 minutes, no `ftyp`) and must be at least 300 ms; otherwise no clip exists. The page stops the recording itself at 10 minutes.
- **No pause/resume.** `MediaCapture` offers it, but it was not evaluated on this stack, so it is not exposed (ADR rule: do not claim unverified features). The UI offers Stop, Play, Record again and Discard. A failed re-record keeps the previous clip.

**Consequences.** Memory use is about 10 MB at the 10 minute cap with Medium quality. The M6 save can write `AudioClip.M4aBytes` as is. If pause/resume is wanted later, it needs its own evaluation.

---

## ADR-010 — Session workflow: one pure state machine, tested as linked source

**Status:** Accepted by Cursor (M5 review, 2026-10-24).

**Context.** M3/M4 tracked the flow with scattered booleans and panel visibility. M6 (save) and M7 (errors) need explicit states. The tests project is plain `net8.0` and must not reference the WinUI project (ARCHITECTURE).

**Decision.**
- `SessionWorkflow` is a pure class with one transition table (`Evaluate`), one instance per page, no static state. The page renders from `State` and calls `TryApply` before acting.
- The file stays in `Eraf.Windows` (it is app logic, not format logic) and is compiled into `Eraf.Format.Tests` through a linked `<Compile Include=... Link=...>` item, rather than adding a project or referencing the WinUI project. It must remain free of WinUI/WinRT types, and the file says so at the top.
- `AudioPreview` means "the voice note is playing"; a successful recording goes directly to `ReadyToSave`. This keeps the M4 flow with no extra confirmation click.
- A voice note survives a failed re-record (`RecordingFailed` returns to `ReadyToSave` if audio is held).
- `StartOver` is illegal while `Saving`.

**Consequences.** Illegal actions are impossible by construction rather than by hidden buttons. If a second consumer of the workflow appears, or the linked-file trick becomes awkward, move it to a small `net8.0` library (for example `Eraf.Session`) and update ARCHITECTURE; nothing else changes.

---

## ADR-011 — Save, open and export: atomic writer, hashes on, `Saved` is a first-class state

**Status:** Accepted by Cursor (M6 review, 2026-10-24).

**Context.** M6 persists a ready session as a `.eraf` file, reopens files, and exports the original payloads. The spec (§11) requires atomic save; ADR-007 chose the Windows App SDK pickers; ADR-010 chose a pure workflow tested as linked source.

**Decision.**
- **Atomic writer** (`AtomicFile`): unique temp file in the target's folder, `Flush(true)`, re-validation of the finished temp file as an ERAF file (exports compare bytes instead), then `File.Replace` for an existing target or `File.Move(overwrite: false)` for a new one. The temp file is deleted on every failure. It is pure `System.IO`, so it is unit-tested, including a target locked with `FileShare.None`.
- **Overwrite protection**: the save picker's own prompt (`ShowOverwritePrompt = true`) asks first; the writer then replaces atomically. A cancelled picker is not a failure and does nothing.
- **New files carry both SHA-256 digests** and the recording length. They detect accidental corruption only. `CreatedUtcUnixMs` is the save time in UTC (a timestamp is the only metadata written).
- **Open** goes straight from a `FileStream` to `ErafReader.TryRead(Stream)`, with no size pre-check, so §9's check order is preserved and a hostile file cannot cause a large allocation. A valid container whose photo cannot be decoded is reported, not shown.
- **Export** writes the stored payload bytes unchanged through the same atomic writer and compares them after writing.
- **Workflow**: `Saved` now means "what is held is exactly what is on disk" and is reached by `SaveSucceeded` or `FileOpened`. Playback and a failed re-record return to the state they started from (`Resume`), so they never change saved-ness. Editing from `Saved` (re-record, discard voice note) is allowed and makes the session unsaved. `Saving` blocks Start over and window close.
- **Unsaved work** (`HasUnsavedWork`) triggers a "Discard unsaved work?" `ContentDialog` on Start over and on window close.
- **ADR-008 hint**: a pure `ErafMedia.JpegHasEndMarker` drives a non-blocking warning after a successful save; it never blocks a save.

**Consequences.** A crash mid-save can leave a `.partial` file but never a damaged target. Opened files can be edited and saved as new files. Changing any default above (hashes, timestamp) is a one-line change in `SessionFile.Build`.

---

## ADR-013 — Attach a saved audio file as the voice note

**Status:** Implemented at the product owner's request (2026-10-24); awaiting Cursor review.

**Context.** The format requires AAC-LC in MPEG-4 (ADR-002). Users may already have a recording in another format.

**Decision.** `AudioImportService` takes a file from the picker (.m4a .mp3 .wav .wma .aac .flac .mp4). The pure rule is `ErafMedia.PlanAudioImport`: an MPEG-4 file (`ftyp`) that Windows reports as a single AAC audio stream with no video is kept byte for byte; anything else is transcoded once with `MediaTranscoder` and the recorder's `CreateM4a` profile. The length comes from the file's music properties; empty, over 512 MiB source, unreadable, under 300 ms or over 10 minutes are rejected before converting, and the result must pass `ErafMedia.CheckAudio` (64 MiB, `ftyp`). The workflow gets one event, `AudioAttached` (PhotoPreview, ReadyToSave, Saved, AudioPreview → ReadyToSave). A failed attach changes nothing, so an earlier voice note survives.

**Consequences.** Attached audio is lossy-converted unless already AAC. "AAC" is the platform's probe: an HE-AAC file in M4A would be kept and would not be strictly AAC-LC (the spec's reader checks only the `ftyp` signature); this is a known gap, not verified either way. The kept file's duration comes from file properties, not from re-measuring.

---

## ADR-012 — Start Android implementation (Phase 2)

**Status:** **Proposed — blocked on product-owner approval.** Nothing in this entry is decided.

**Context.** The Windows Phase 1 gate passed (STATUS). [PHASE2_ANDROID_PLAN.md](PHASE2_ANDROID_PLAN.md) proposes a Kotlin `eraf-format` JVM library plus a Compose app under `android/`, validated against the same `fixtures/eraf`.

**Decision (to be made by the PO).** Whether to start Phase 2 implementation, and the answers to plan section 10: minimum API level, save policy (new documents only vs overwrite), recording when backgrounded, process-death handling, test device, shared state-machine table.

**Consequences if accepted.** An A0 tooling brief is written; `android/` may be created; dependencies are pinned and inventoried. Until then no Android source, Gradle project or download is created.

---

## Pending decisions (not yet made)

| Topic | Needed by |
|---|---|
| Pause/resume recording (not offered in M4; needs evaluation if wanted) | Later |
| Wording/placement of the incomplete-JPEG warning (basic version shipped in M6) | M7 |
| Project license | Before any public release |
