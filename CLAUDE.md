# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository state

Planning only: there is no application code, solution, or build/test tooling yet. The tracked files are `README.md` and `docs/` (`IMPLEMENTATION_PLAN.md`, `ROLES.md`, `STATUS.md`, `CLAUDE_CODE_BRIEF_M0.md`). Build/lint/test commands will be added after Milestone M0 proves WinUI tooling works, so don't invent them. `docs/STATUS.md` is the live source of truth for what is done.

## Workflow rules (from docs/ROLES.md)

Work is split between **Cursor** (supervisor/reviewer) and **Claude Code** (implementer, i.e. you):

- Read `docs/IMPLEMENTATION_PLAN.md`, `docs/STATUS.md`, `docs/ROLES.md` and the current milestone brief before editing.
- Implement only the currently authorized milestone (currently M0, per `docs/CLAUDE_CODE_BRIEF_M0.md`: a blank packaged .NET 8 WinUI 3 app at `src/Eraf.Windows/` plus `Eraf.sln`; no format library, camera, or audio).
- Do not change the public ERAF format without an updated spec reviewed by Cursor, expand product scope, or start Android before the Windows release gate (M9).
- Do not commit or push unless the user explicitly asks.
- Report real evidence: record commands actually run and their pass/fail results in `docs/STATUS.md`. Never claim a test passed unless it was executed and inspected.
- Update docs when behavior, format, or workflow changes.

Milestones: M0 tooling smoke → M1 format spec + docs → M2 `Eraf.Format` library + tests → M3 image → M4 audio → M5 session workflow → M6 save/open/export → M7 hardening → M8 accessibility → M9 Windows release gate. Phase 2 (Android, Kotlin + Compose) is blocked until M9 is approved.

## Product and format

ERAF (`.eraf`) is a versioned binary container (not a renamed JPEG/WAV/ZIP) holding exactly one JPEG and one AAC-LC M4A voice recording. It must open and play identically on Windows and Android.

v1 sketch (the normative spec will be `docs/ERAF_FORMAT_SPEC.md` in M1; until then `docs/IMPLEMENTATION_PLAN.md` is authoritative):

- Fixed little-endian 64-byte header, magic `ERAF`, optional SHA-256 integrity block, then raw JPEG and M4A payloads.
- CRC-32 over the header core; SHA-256 is corruption detection only, not authentication.
- Default limits: 256 MiB file, 64 MiB per payload, 10 min recording.
- Cloud sync, accounts, and general media management are out of scope.

## Planned architecture

- `src/Eraf.Format/`: pure .NET format library with no UI dependencies.
- `src/Eraf.Windows/`: WinUI 3 app (MediaCapture for camera/M4A recording, MediaPlayer for playback, file import fallback when no camera).
- `tests/Eraf.Format.Tests/`: unit, negative, and interop fixture tests.
- `fixtures/eraf/`: golden conformance vectors shared across platforms. Android will use a separate native implementation validated against these same fixtures, so format changes must keep them in sync.
- No cross-platform UI frameworks (MAUI, Flutter, React Native).
