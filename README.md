# ERAF

**ERAF** is a self-contained file format and application ecosystem that pairs **one photograph** with **one voice recording** in a single `.eraf` file.

Users can capture or import a photo, record a voice note in the same session (or afterward), review both, and save them together. The same files must open and play correctly on **Windows** and **Android**.

| | |
|---|---|
| **Format** | Versioned binary container (Design A) — not a renamed JPEG, WAV, or ZIP |
| **Extension** | `.eraf` |
| **v1 contents** | Exactly one JPEG image + one AAC-LC (M4A) voice recording |
| **Phase 1** | Windows application (.NET 8 + WinUI 3) |
| **Phase 2** | Android application (Kotlin + Jetpack Compose) — starts only after the Windows release gate |

## What users can do (product goals)

- Take a photo or import an existing JPEG
- Record, preview, replace, or discard a voice note
- Save photo + audio as one `.eraf` file
- Open an `.eraf` file, view the image, and play the recording
- Export the original image or audio payloads
- Get clear errors for denied permissions, missing hardware, and corrupt files

Cloud sync, accounts, and general media-management features are out of scope unless explicitly approved.

## Repository status

**Application features are not implemented yet.**

**Windows Phase 1 gate: PASSED** (M0–M9). Next: Phase 2 **planning only** — [docs/CLAUDE_CODE_BRIEF_PHASE2_PLAN.md](docs/CLAUDE_CODE_BRIEF_PHASE2_PLAN.md). Android implementation stays blocked until the product owner approves.

Live progress: [docs/STATUS.md](docs/STATUS.md)

## Planned layout

```text
Eraf/
  docs/                 Governance, format spec, plans, status
  fixtures/eraf/        Golden conformance vectors (shared across platforms)
  src/
    Eraf.Format/        Pure .NET format library (no UI)
    Eraf.Windows/       WinUI 3 desktop app
  tests/
    Eraf.Format.Tests/  Unit, negative, and interop fixture tests
  tools/                Optional fixture generators
```

Android sources will be added only after Phase 1 passes its release gate.

## Technology choices

| Area | Choice |
|---|---|
| Windows UI | .NET 8 + WinUI 3 (Windows App SDK) |
| Windows media | MediaCapture (camera / M4A record); MediaPlayer (playback); file import when camera is unavailable |
| Android UI | Kotlin + Jetpack Compose (Phase 2) |
| Android media | CameraX + MediaRecorder / AudioRecord (Phase 2) |
| Format code | Separate native libraries + one normative spec + shared golden fixtures |
| Cross-platform UI frameworks | Not used (no MAUI / Flutter / React Native for this project) |

## ERAF format (v1 sketch)

Fixed little-endian **64-byte header**, optional SHA-256 integrity block, then raw JPEG and M4A payloads.

- Magic: `ERAF`
- Mandatory image: JPEG
- Mandatory audio: AAC-LC in MPEG-4 / M4A (`audio/mp4`, codec `mp4a.40.2`)
- CRC-32 over the header core; optional SHA-256 over payloads (corruption detection only — not authentication)
- Default limits: 256 MiB total file; 64 MiB per payload; 10 minutes max recording

The normative specification is [docs/ERAF_FORMAT_SPEC.md](docs/ERAF_FORMAT_SPEC.md). It adds precision to the approved sketch in [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md) §5; where they differ, the spec wins.

## Documentation

| Document | Purpose |
|---|---|
| [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md) | Audit report + Windows Phase 1 plan |
| [docs/ERAF_FORMAT_SPEC.md](docs/ERAF_FORMAT_SPEC.md) | Normative v1 format specification |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Module boundaries and workflow design |
| [docs/TESTING.md](docs/TESTING.md) | Test strategy and fixture policy |
| [docs/SECURITY.md](docs/SECURITY.md) | Threat model, untrusted-input rules, dependency inventory |
| [docs/DEPENDENCIES.md](docs/DEPENDENCIES.md) | Packages, versions and licenses |
| [docs/USER_GUIDE.md](docs/USER_GUIDE.md) | Short guide for making, opening and exporting ERAF files |
| [docs/DECISIONS.md](docs/DECISIONS.md) | Decision log (ADRs) |
| [fixtures/eraf/README.md](fixtures/eraf/README.md) | Golden fixtures: naming and use; byte recipes in `RECIPES.md` |
| [docs/STATUS.md](docs/STATUS.md) | Milestone status and evidence log |
| [docs/ROLES.md](docs/ROLES.md) | Cursor (supervisor) vs Claude Code (implementer) |
| [docs/CLAUDE_CODE_BRIEF_M0.md](docs/CLAUDE_CODE_BRIEF_M0.md) | M0 brief (closed) |
| [docs/CLAUDE_CODE_BRIEF_M0_FOLLOWUP_NAMESPACE.md](docs/CLAUDE_CODE_BRIEF_M0_FOLLOWUP_NAMESPACE.md) | Namespace rename (done) |
| [docs/CLAUDE_CODE_BRIEF_M1.md](docs/CLAUDE_CODE_BRIEF_M1.md) | M1 brief (closed) |
| [docs/CLAUDE_CODE_BRIEF_M2.md](docs/CLAUDE_CODE_BRIEF_M2.md) | M2 brief (closed) |
| [docs/CLAUDE_CODE_BRIEF_M3.md](docs/CLAUDE_CODE_BRIEF_M3.md) | M3 brief (closed) |
| [docs/CLAUDE_CODE_BRIEF_M4.md](docs/CLAUDE_CODE_BRIEF_M4.md) | M4 brief (closed) |
| [docs/CLAUDE_CODE_BRIEF_M5.md](docs/CLAUDE_CODE_BRIEF_M5.md) | M5 brief (closed) |
| [docs/CLAUDE_CODE_BRIEF_M6.md](docs/CLAUDE_CODE_BRIEF_M6.md) | M6 brief (closed) |
| [docs/CLAUDE_CODE_BRIEF_M7.md](docs/CLAUDE_CODE_BRIEF_M7.md) | M7 brief (closed) |
| [docs/CLAUDE_CODE_BRIEF_M8.md](docs/CLAUDE_CODE_BRIEF_M8.md) | M8 brief (closed) |
| [docs/CLAUDE_CODE_BRIEF_M9.md](docs/CLAUDE_CODE_BRIEF_M9.md) | M9 brief (closed — gate pass) |
| [docs/CLAUDE_CODE_BRIEF_PHASE2_PLAN.md](docs/CLAUDE_CODE_BRIEF_PHASE2_PLAN.md) | Phase 2 planning brief (no Android code yet) |
| [docs/USER_GUIDE.md](docs/USER_GUIDE.md) | End-user guide (Windows) |
| [docs/DEPENDENCIES.md](docs/DEPENDENCIES.md) | Dependency and license inventory |

Windows milestones M0–M9 are complete for the Phase 1 gate.

## Delivery model

| Role | Responsibility |
|---|---|
| **Cursor** | Architecture, reviews, security/quality gates, milestone briefs |
| **Claude Code** | Implementation of the currently authorized milestone only |

Android implementation does not start until the product owner approves Phase 2 after the planning review.

## Building and running (Windows, development machine)

Requires .NET SDK 8, the matching Windows App Runtime (2.5.1), and Developer Mode on.

```powershell
# Build
dotnet build src\Eraf.Windows\Eraf.Windows.csproj -p:Platform=x64

# Launch (registers a debug package, then starts the app)
& "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools.winapp\0.7.1\tools\win-x64\winapp.exe" run src\Eraf.Windows\Eraf.Windows.csproj --arch x64
```

Plain `dotnet run` does not work on this machine (no package identity); see [docs/DECISIONS.md](docs/DECISIONS.md) ADR-005. 

Tests (format library): `dotnet test tests\Eraf.Format.Tests\Eraf.Format.Tests.csproj`.

## License

License not yet declared. Do not assume redistributable rights until a license file is added.
