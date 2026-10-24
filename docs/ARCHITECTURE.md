# ERAF Architecture — Windows Phase 1

Design authority for module boundaries. Format rules live in [ERAF_FORMAT_SPEC.md](ERAF_FORMAT_SPEC.md); rationale for major choices in [DECISIONS.md](DECISIONS.md); the original diagram and risk analysis in [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md). Only `Eraf.Windows` exists today (blank shell, M0). The rest is the target for M2–M7.

## Modules and dependency direction

```
Eraf.Windows  ──▶  Eraf.Format
Eraf.Format.Tests ──▶ Eraf.Format   (and reads fixtures/eraf)
```

Dependencies point one way. `Eraf.Format` references nothing from the app, WinRT or the UI.

| Module | Target | Responsibility | Must not |
|---|---|---|---|
| `src/Eraf.Format` | `net8.0` (not `net8.0-windows`) | Read, write and validate v1 files per the spec; model types; limits as constants; CRC-32/SHA-256; the named error codes | Touch the file system beyond the streams it is given, use WinRT/UI types, capture or play media, decode JPEG/M4A |
| `src/Eraf.Windows` | `net8.0-windows10.0.26100.0`, WinUI 3 packaged | Shell and navigation, session workflow, camera and audio capture, playback, file pickers, atomic save, error and permission UX, accessibility | Parse or write ERAF bytes itself — always through `Eraf.Format` |
| `tests/Eraf.Format.Tests` | `net8.0`, xUnit | Unit, negative, round-trip and fixture conformance tests for the format library | Depend on `Eraf.Windows` |
| `fixtures/eraf` | data | Golden vectors shared across platforms | Hold generated-by-the-code-under-test expectations |
| `tools/Eraf.FixtureGen` | console, optional | Emits fixture binaries from recipes | Ship in the app |

Keeping the format library UI-free is what lets the same tests run in CI without a WinUI toolchain, and keeps the Kotlin port a straight translation of the spec.

## `Eraf.Format` shape (M2)

- `ErafReader`: validates per spec §9 in order and returns an `ErafDocument` or a typed `ErafException`/result carrying the §9 error code. Reads the 64-byte header and the file length first, validates, and only then reads payloads.
- `ErafWriter`: takes JPEG bytes, M4A bytes, optional duration/created time and a hash option; produces a file that always passes the reader.
- `ErafValidator`: the shared rule set used by both.
- `ErafLimits`: the spec §7 constants.
- Payloads are exposed as spans/streams over the original bytes, not re-encoded or copied more than necessary.

Public API details are decided in M2 and recorded in [DECISIONS.md](DECISIONS.md) if they are non-obvious.

## `Eraf.Windows` shape (M3–M7)

Services (UI-adjacent, each behind an interface so the workflow can be tested without hardware):

| Service | Role |
|---|---|
| `CameraService` | `MediaCapture` preview and JPEG still capture |
| `ImageImportService` | Windows App SDK `FileOpenPicker` import; JPEG kept as is, others converted once (ADR-007); rules from `ErafMedia` in `Eraf.Format` |
| `AudioRecorder` | AAC-LC M4A recording via `MediaEncodingProfile.CreateM4a` into memory; the page enforces the 10-minute cap; checks results with `ErafMedia.CheckAudio` (ADR-009) |
| `ClipPlayer` | `MediaPlayer` playback of the in-session M4A straight from memory (no temp file) |
| `FilePickers`, `AtomicFile`, `SessionFile` | Windows App SDK pickers; **atomic save** (spec §11) and export of original payloads; session to/from `.eraf` bytes via `Eraf.Format` (ADR-011). The last two are pure and unit-tested |
| `SessionWorkflow` | Pure state machine in `SessionWorkflow.cs` (ADR-010); the page renders from it |

Session state machine (one session object, not a global singleton):

```
Idle → CapturingPhoto → PhotoPreview → RecordingAudio → ReadyToSave → Saving → Saved | Error
Idle → Saved (an .eraf file was opened)
                                         AudioPreview (while the note plays) ⇄ ReadyToSave / Saved
```

The legal transitions are tabled in [STATUS.md](STATUS.md) (M5 evidence) and in the `SessionWorkflow` source; every other (state, event) pair is rejected. `StartOver` and `DiscardAudio` clear the held media, `StartOver` is refused while `Saving`, and incomplete audio is never reported as saved. Camera preview and microphone recording are not required to run at the same time: after the photo is taken the session holds the JPEG bytes and records audio afterward.

Camera absence or denial never blocks the app: image import stays available.

## File-manager shell (post-gate)

`MainPage` hosts, from the top: the top bar, then the folder tree (`PlacesBlade`) and a content area. The content area shows the file list (`FileListPane`) while the workflow is `Idle`, and the camera or photo panels in every other state. Pure logic (`FileBrowsing.cs`: classification, sort, filter, history, folder reading; `TrimRange.cs`; `MicrophoneChoice.cs`) is linked into the test project; WinUI types stay in the panes, `TrimDialog` and the page. See ADR-014.

## Build and launch (this machine)

- Build: `dotnet build src\Eraf.Windows\Eraf.Windows.csproj -p:Platform=x64`
- Launch: `winapp run src\Eraf.Windows\Eraf.Windows.csproj --arch x64`, using the `winapp.exe` shipped in the `Microsoft.Windows.SDK.BuildTools.WinApp` NuGet package (`tools\win-x64\`). Plain `dotnet run` does not register package identity here. See [DECISIONS.md](DECISIONS.md) ADR-005 and [STATUS.md](STATUS.md).

## Platform boundary for Android (Phase 2)

Android gets its own `eraf-format` Kotlin module written from the spec and validated against `fixtures/eraf`. No code is shared with `Eraf.Format`; the spec and fixtures are the shared contract (ADR-004). Android work starts only after the Windows release gate.
