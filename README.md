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

**Planning complete for Phase 1. Application code is not implemented yet.**

Next authorized engineering step: Milestone **M0** (WinUI tooling smoke build), executed by Claude Code under [docs/CLAUDE_CODE_BRIEF_M0.md](docs/CLAUDE_CODE_BRIEF_M0.md).

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

The normative specification will live in `docs/ERAF_FORMAT_SPEC.md` (Milestone M1). Until then, the approved sketch is in [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md).

## Documentation

| Document | Purpose |
|---|---|
| [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md) | Audit report + Windows Phase 1 plan |
| [docs/STATUS.md](docs/STATUS.md) | Milestone status and evidence log |
| [docs/ROLES.md](docs/ROLES.md) | Cursor (supervisor) vs Claude Code (implementer) |
| [docs/CLAUDE_CODE_BRIEF_M0.md](docs/CLAUDE_CODE_BRIEF_M0.md) | Current implementer brief for M0 |

Planned after M1: `ERAF_FORMAT_SPEC.md`, `ARCHITECTURE.md`, `TESTING.md`, `SECURITY.md`, `DECISIONS.md`, and a user guide.

## Delivery model

| Role | Responsibility |
|---|---|
| **Cursor** | Architecture, reviews, security/quality gates, milestone briefs |
| **Claude Code** | Implementation of the currently authorized milestone only |

Windows milestones run M0 → M9. Android does not start until Cursor and the product owner approve the Windows release gate.

## Building (not available yet)

Build and run instructions will be added after M0 proves a blank WinUI 3 app builds on the development machine. Until then:

1. Read [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md)
2. Do not treat WinUI tooling as available without completing M0 evidence in [docs/STATUS.md](docs/STATUS.md)

## License

License not yet declared. Do not assume redistributable rights until a license file is added.
