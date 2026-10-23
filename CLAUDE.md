# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository state

M0 (blank WinUI 3 shell), M1 (spec and governance docs) and M2 (`Eraf.Format` + tests) are done or in review; the WinUI app is still a blank shell. `docs/STATUS.md` is the live source of truth, and each milestone has a brief at `docs/CLAUDE_CODE_BRIEF_*.md`.

Commands (project-level only; redirect output to a file if a command hangs, since piped output and solution-level restore hung in this shell):

- Test format library: `dotnet test tests\Eraf.Format.Tests\Eraf.Format.Tests.csproj`; one test: add `--filter "FullyQualifiedName~ConformanceTests"` (or any test class/method name)
- Build: `dotnet build src\Eraf.Windows\Eraf.Windows.csproj -p:Platform=x64`
- Launch: `winapp run src\Eraf.Windows\Eraf.Windows.csproj --arch x64`, using `winapp.exe` from `%USERPROFILE%\.nuget\packages\microsoft.windows.sdk.buildtools.winapp\0.7.1\tools\win-x64\`. Plain `dotnet run` does not register package identity here (Developer Mode must be on).
- Pinned: Windows App SDK 2.5.1, target `net8.0-windows10.0.26100.0`. Don't unpin or upgrade without a `docs/DECISIONS.md` entry.

## Workflow rules (from docs/ROLES.md)

Work is split between **Cursor** (supervisor/reviewer) and **Claude Code** (implementer, i.e. you):

- Read `docs/IMPLEMENTATION_PLAN.md`, `docs/STATUS.md`, `docs/ROLES.md` and the current milestone brief before editing.
- Implement only the currently authorized milestone (check `docs/STATUS.md` and the newest brief; check for the newest brief).
- Do not change the public ERAF format without an updated spec reviewed by Cursor, expand product scope, or start Android before the Windows release gate (M9).
- Do not commit or push unless the user explicitly asks.
- Report real evidence: record commands actually run and their pass/fail results in `docs/STATUS.md`. Never claim a test passed unless it was executed and inspected.
- Update docs when behavior, format, or workflow changes.

Milestones: M0 tooling smoke → M1 format spec + docs → M2 `Eraf.Format` library + tests → M3 image → M4 audio → M5 session workflow → M6 save/open/export → M7 hardening → M8 accessibility → M9 Windows release gate. Phase 2 (Android, Kotlin + Compose) is blocked until M9 is approved.

## Product and format

ERAF (`.eraf`) is a versioned binary container (not a renamed JPEG/WAV/ZIP) holding exactly one JPEG and one AAC-LC M4A voice recording. It must open and play identically on Windows and Android.

v1 summary (`docs/ERAF_FORMAT_SPEC.md` is normative and its clarifications C1–C4 await supervisor approval; read it before touching format code):

- Fixed little-endian 64-byte header, magic `ERAF`, optional SHA-256 integrity block, then raw JPEG and M4A payloads.
- CRC-32 over the header core; SHA-256 is corruption detection only, not authentication.
- Default limits: 256 MiB file, 64 MiB per payload, 10 min recording.
- Validation is an ordered list with named error codes (spec §9). Tests assert the exact code, and expected values come from `fixtures/eraf/RECIPES.md`, never from the code under test.
- Cloud sync, accounts, and general media management are out of scope.

## Planned architecture

- `src/Eraf.Format/`: pure .NET format library with no UI dependencies.
- `src/Eraf.Windows/`: WinUI 3 app (MediaCapture for camera/M4A recording, MediaPlayer for playback, file import fallback when no camera).
- `tests/Eraf.Format.Tests/`: unit, negative, and interop fixture tests.
- `fixtures/eraf/`: golden conformance vectors shared across platforms. Android will use a separate native implementation validated against these same fixtures, so format changes must keep them in sync.
- No cross-platform UI frameworks (MAUI, Flutter, React Native).
