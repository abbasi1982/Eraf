# ERAF Phase 2 — Android plan

**Status:** Draft for review by Cursor and the product owner (PO). **Planning only.**
**Written:** 2026-10-24, after the Windows Phase 1 gate passed (STATUS).

> **Do not implement until the PO says so.** No Android project, Kotlin source, Gradle file or dependency download exists or may be created until the PO explicitly authorizes Phase 2 implementation after reviewing this plan (see ADR-012, *Proposed*).

## 1. Goal and constraints

An Android app that makes, opens, plays and exports the same `.eraf` files as the Windows app: one JPEG plus one AAC-LC M4A voice note in the frozen v1 container.

- The wire format does not change. [ERAF_FORMAT_SPEC.md](ERAF_FORMAT_SPEC.md) is normative; Android is a second independent implementation (ADR-004).
- Same limits (256 MiB file, 64 MiB per payload, 10 min), same ordered §9 validation, same error codes.
- Native Kotlin + Jetpack Compose. No MAUI, Flutter or React Native.
- No network, accounts, cloud sync or telemetry (SECURITY.md applies unchanged).
- A file made on either platform must open and play on the other.

## 2. What Windows taught us (carried over)

| Windows lesson | Android consequence |
|---|---|
| Format library separate from UI, pure and fully testable | `eraf-format` is a pure Kotlin/JVM module with no Android imports, so its tests run on the JVM without a device |
| One pure `SessionWorkflow` state machine (9 states, 15 events, 28 legal pairs, ADR-010) | Port as a pure Kotlin class; UI only renders its state |
| Golden fixtures with expected values independent of code (`fixtures/eraf`, `RECIPES.md`) | Android tests consume the same files; no copies |
| Atomic save, never damage the target (§11, ADR-011) | Needs a different mechanism on SAF (section 5) |
| A valid container with an undecodable photo must be reported, not crash | Same check before showing a photo |
| UI automation can mis-target dialogs | Prefer Compose UI tests and instrumentation over screen scripting |
| Evidence discipline: record commands actually run; list unverified items | Unchanged |

## 3. Module layout (proposed)

A new top-level folder `android/` beside `src/`, so the Gradle project does not mix with the .NET tree.

```
android/
  settings.gradle.kts
  gradle/libs.versions.toml        # pinned version catalog
  eraf-format/                     # pure Kotlin JVM library (kotlin("jvm")), no Android dependency
    src/main/kotlin/.../ErafLimits, ErafError, ErafReader, ErafWriter, ErafDocument, Crc32?, ErafMedia
    src/test/kotlin/...            # conformance, writer, hostile-input, media tests
  app/                             # com.android.application, Compose UI
    src/main/kotlin/.../ui, session (SessionWorkflow, CaptureSession), media (camera, recorder, player), storage (SAF)
    src/test, src/androidTest
```

- **`eraf-format`** mirrors `Eraf.Format`: `ErafLimits`, `ErafError` (same 14 names), `ErafReader` (`tryRead`/`validate`), `ErafWriter`, `ErafDocument`, `ErafMedia` (JPEG/M4A signature, import plan, audio check, JPEG end-marker hint). CRC-32 via `java.util.zip.CRC32` (CRC-32/ISO-HDLC, as the spec); SHA-256 via `MessageDigest`. u64 fields are read as `Long` and rejected when negative (>= 2^63), with `Math.addExact`-style checked arithmetic so overflow gives `LengthMismatch`/`LimitExceeded` in spec order.
- **Streaming reader (a deliberate difference).** Windows reads a file (up to 256 MiB) into memory. On Android a 256 MiB heap allocation can fail, so the Kotlin reader takes an `InputStream`/file descriptor: it reads 64 header bytes, runs checks 1–13 using the real file length, then streams the payloads into app-private cache files while hashing, then runs checks 14–15. Observable results (error codes, order) must be identical. Payload bytes reach a decoder or player only after all 15 checks pass (§9).
- **App** modules follow the Windows split: workflow (pure), services (camera, recorder, player, storage), UI.

## 4. Media mapping Windows → Android

| Concern | Windows (done) | Android (proposed, to verify on device) |
|---|---|---|
| Camera preview + still | `MediaCapture` / `MediaFrameSource` | CameraX `Preview` + `ImageCapture` (JPEG output). `ImageCapture` yields JPEG bytes; keep them byte for byte. |
| Import photo | `FileOpenPicker`, convert non-JPEG once (ADR-007) | Photo Picker (`PickVisualMedia`) with `OpenDocument` fallback; same `PlanImageImport` rules: keep `FF D8 FF` bytes unchanged, otherwise decode with `ImageDecoder`/`BitmapFactory` and re-encode once as JPEG (alpha ignored). HEIC from phone galleries is the likely Android-specific input; it must go through convert-on-import. |
| Record voice note | `MediaCapture` + `CreateM4a`, AAC-LC, in memory (ADR-009) | `MediaRecorder`: `OutputFormat.MPEG_4`, `AudioEncoder.AAC` (AAC-LC), explicit sample rate/bit rate. MPEG-4 output needs a seekable file, so recording goes to a **cache file** (a departure from Windows' in-memory stream) that is read into the session and deleted. |
| Recording length | App stops at 10 min | `setMaxDuration(600_000)` plus the app's own timer; a recorded clip must still pass `CheckAudio` (not empty, <= 64 MiB, <= 10 min, `ftyp` at bytes 4–7). Minimum 300 ms as on Windows. |
| Pause/resume | Not offered (ADR-009) | `MediaRecorder.pause()/resume()` exist on API 24+, but, following ADR-009, not offered in v1 of the Android app unless the PO asks and it is verified. |
| Playback | `MediaPlayer` from stream, `audio/mp4` | `MediaPlayer` or Media3 `ExoPlayer` from the cached payload file. Media3 is a new dependency; start with platform `MediaPlayer` and decide in milestone A5. |
| Decode photo for display | `BitmapImage` | `BitmapFactory`/`ImageDecoder` with bounds-first sampling so a large photo cannot exhaust memory. |
| Cross-platform check | n/a | An Android-recorded M4A must play on Windows `MediaPlayer`, and a Windows-recorded one on Android. This matrix is a gate item (A9). |

Open verification items: the exact MPEG-4 brand Android writes (any `ftyp` is accepted by the spec's signature rule, but Windows playback must be confirmed); EXIF orientation of camera JPEGs (Windows did not verify orientation handling either).

## 5. Storage: open, save, export (SAF)

- **Open:** `ActivityResultContracts.OpenDocument` for `.eraf` (MIME not registered, so filter `*/*` and validate content). Read via `ContentResolver.openFileDescriptor`/`openInputStream`; the file length comes from the descriptor, never from the name. Invalid files give the same plain messages as Windows (`ErafErrorText` ported).
- **Save and export:** `ActivityResultContracts.CreateDocument`. Build and validate the complete file in app-private cache first (the equivalent of the temp file in §11), then copy to the chosen document.
- **Atomicity gap (needs a PO decision).** SAF has no portable atomic rename. Using `CreateDocument` creates a new document (the system picker renames rather than overwrites a name clash), so the default is that **save never overwrites an existing document**, which satisfies §11 trivially. Overwriting an existing `.eraf` chosen through `OpenDocument` would write over the target in place and could leave a truncated file on failure. Recommended v1 behavior: no in-place overwrite; "save as new" only. Alternative: overwrite via a verified cache copy plus `"wt"` mode with an explicit warning. This becomes an ADR before milestone A6.
- **Permissions:** no storage permission is needed with the picker and SAF. Do not request `READ_EXTERNAL_STORAGE`/`MANAGE_EXTERNAL_STORAGE`.
- **Export** writes the stored payload bytes unchanged, then compares them (as ADR-011).
- Temp files in `cacheDir` are cleaned on every path (success, cancel, error, discard) and swept on app start; never log paths or bytes.

## 6. Permissions, lifecycle and risks

| Risk | Plan |
|---|---|
| Camera / microphone runtime permissions, including "don't ask again" | Request on use; on denial show an actionable message with a button to the app's system settings page; import and open stay available. Declare camera `uses-feature` with `required="false"` so devices without a camera can install. |
| Rotation / configuration change | Workflow and session in a `ViewModel` (survives rotation). Media is **not** put in a `Bundle` (the binder transaction limit is about 1 MB); it stays in memory or cache files. |
| Backgrounding during recording | Recording needs the app visible (Android restricts background microphone access). Policy to decide with the PO: on `onStop`, stop the recorder and keep the clip so far, or discard with a message. Default recommendation: stop and keep, then show a notice on return. No foreground service in v1. |
| Audio focus / interruptions (calls, other media) | Request focus for playback and abandon it after; stop playback when focus is lost. A recording interrupted by the system (`MediaRecorder` error/info callbacks) ends as stopped, never as a corrupt saved file. |
| Process death | Unsaved work is lost unless recovered. Plan: keep the in-progress photo and clip as cache files plus a small saved-state record; on restart offer to resume or discard, or accept loss and show the unsaved-work warning earlier. PO decision. |
| Large files | Streaming reader (section 3); never decode or play before validation passes. |
| Hostile `.eraf` | Same §9 order, no allocation from claimed lengths before checks 10–13 pass, no repair, no partial open. |
| Photo privacy | Camera JPEGs may carry GPS Exif if the permission and setting allow; preserved as imported, same disclosure as Windows. |
| Accessibility | TalkBack: `contentDescription`/semantics on all controls, same labels as Windows ("Import image", "Use camera", "Record voice note", "Save ERAF file", "Open ERAF file", "Export photo", "Export voice note"); focus moves to the next likely control after each step. Real TalkBack testing required before claiming support. |
| Device fragmentation | Test on at least one physical phone plus the emulator; record the API level and model in evidence. |

## 7. Conformance and testing

- `eraf-format` tests read `fixtures/eraf` in place (Gradle test resources pointing at `../fixtures/eraf`, no copies). For each file in `invalid/`, assert the exact error named by its `expected/*.json`; for `valid/`, assert lengths, duration, timestamps and SHA-256 values from the sidecar. Expected values come from `RECIPES.md` and the sidecars, never from the Kotlin code.
- Port the Windows test groups: CRC-32 vectors, conformance, writer round-trip, hostile input (overflow, huge claimed lengths, truncated streams), media checks, workflow legal/illegal transitions.
- **Shared state-machine table (proposal):** extract the 28 legal (state, event) pairs into a data file next to the fixtures so both platforms test the same table. Needs Cursor's approval because it adds a fixture-like artifact.
- **Bidirectional interop:** files written by Windows open on Android and the reverse; an independent Python parser (`parse_eraf.py` style) checks files produced by the Android app, as it did for Windows.
- Instrumented/UI tests for the real flow on a device; manual TalkBack walk; evidence rules as in `TESTING.md`.
- Any fixture or spec change would touch the spec, fixtures, **both** libraries, and needs Cursor's review.

## 8. Milestones (comparable to Windows M0–M9)

| ID | Milestone | Exit evidence |
|---|---|---|
| A0 | Tooling smoke: JDK, Android SDK, Gradle wrapper, empty Compose app builds and launches on emulator/device | Commands and outputs in STATUS |
| A1 | Spec/fixture alignment review; Android decisions (ADR-012 set: minSdk, overwrite policy, backgrounding, process death) | Accepted ADRs |
| A2 | `eraf-format` Kotlin library + full conformance suite against `fixtures/eraf` | JVM tests green; exact codes for all 21 invalid fixtures |
| A3 | Image import and CameraX capture, convert-on-import, preview | Device evidence |
| A4 | Voice note record/play/replace/discard with limits | Device evidence; AAC-LC verified in file |
| A5 | Session workflow state machine + UI wiring | Pure tests + Compose tests |
| A6 | Save / open / export through SAF; temp cleanup; unsaved-work guard | Independent parser check of an Android-written file |
| A7 | Hardening: denied permissions, interruptions, process death, disk errors | Documented exercised/unverified list |
| A8 | Accessibility (TalkBack, large text, keyboard where applicable) | Checklist; state what was not heard |
| A9 | Android release gate: regression, interop matrix with Windows, acceptance table, user guide update, dependency/license inventory | Cursor gate review |

Each milestone gets a brief in `docs/` (like `CLAUDE_CODE_BRIEF_Ax.md`), is reviewed by Cursor before commit, and records real commands and results.

## 9. Tooling assumptions to verify (A0)

Observed on the dev machine on 2026-10-24 (read-only check; nothing installed or downloaded):

| Item | Observed |
|---|---|
| JDK | OpenJDK 17.0.20 (Microsoft build), `JAVA_HOME` set |
| `ANDROID_HOME` / `ANDROID_SDK_ROOT` | **Unset** |
| Android SDK | Present at `%LOCALAPPDATA%\Android\Sdk`: platforms `android-35`, `android-36`, `android-37.0`; build-tools 34.0.0, 35.0.0, 36.0.0; `cmdline-tools\latest`; `platform-tools`; licenses folder |
| Emulator / system images | **Not present** (no `emulator` or `system-images` folders) |
| Gradle | Not on `PATH`; the project would use the Gradle wrapper (downloads Gradle on first run) |
| Android Studio | Not checked |
| Physical device | Unknown; needs USB debugging and driver |

To verify at A0: SDK license acceptance state; which compileSdk/targetSdk to pin (35 is installed; a newer target may be required for current store rules, a PO/release question); AGP, Kotlin, Compose BOM, CameraX and Gradle versions compatible with JDK 17; whether an emulator image (with camera and microphone) is acceptable or a physical device is required (emulator microphone and camera are weak proxies for recording behavior). Every dependency is pinned and added to the inventory (`DEPENDENCIES.md` pattern) with its license before use.

Expected dependencies (all to be pinned and license-checked, none downloaded yet): Kotlin, Android Gradle Plugin, Compose BOM, AndroidX Activity/Lifecycle, CameraX (core, camera2, lifecycle, view), JUnit, and optionally Media3 (decision at A5).

## 10. Decisions needed from the PO

1. Authorize Phase 2 implementation (and A0 downloads) at all.
2. Minimum Android version (proposal: API 26+; to confirm against device availability and CameraX requirements).
3. Save policy: new documents only (recommended) vs overwrite with the extra risk.
4. Recording when the app goes to the background: stop and keep (recommended) vs discard.
5. Process death: resume draft vs accept loss with an earlier warning.
6. Physical test device available, or emulator-only evidence acceptable.
7. Whether to add the shared state-machine table.

## 11. Out of scope for Phase 2

Format changes, cloud sync, accounts, sharing intents, store listing or signing, widgets, iOS, and any feature that Windows does not have, unless the PO adds it explicitly.

## 12. Approval

- [ ] Cursor review of this plan
- [ ] PO review and answers to section 10
- [ ] PO explicit "start Phase 2 implementation" (then ADR-012 moves from *Proposed* to *Accepted* and an A0 brief is written)
