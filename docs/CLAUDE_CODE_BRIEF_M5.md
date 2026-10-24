# Claude Code Brief — Milestone M5 (Session workflow state machine)

**Authorized by:** Cursor (supervisor)  
**Implementer:** Claude Code  
**Prerequisite:** M4 closed and committed.  
**Depends on:** `CaptureSession` (photo + optional audio) from M3/M4.

## Goal

Make the capture flow an explicit, invalid-transition-safe state machine so later save/open (M6) and error handling (M7) plug into clear states—not ad-hoc UI flags alone.

## Required states (adapt names if needed; keep semantics)

`Idle → CapturingPhoto → PhotoPreview → RecordingAudio → AudioPreview → ReadyToSave → Saving → Saved | Error`

Plus explicit **Cancel** / **Discard** edges that clear session media and never mark incomplete audio as saved.

M3/M4 already implement most of the photo/audio UI. M5 should:

1. Centralize transitions in a `SessionWorkflow` (or equivalent) owned by the page/session—not a process-wide mutable dump.
2. Reject illegal transitions (e.g. Record while Idle with no photo; Save while recording).
3. Map current MainPage panels to these states; `ReadyToSave` when photo **and** valid audio clip exist (v1 requires both).
4. Leave **Saving/Saved** as states that M6 will drive (stubs/disabled Save OK, or a disabled Save button that enables only in `ReadyToSave`).
5. Unit-test transition table (pure, no WinUI).
6. Update STATUS.md with evidence; note any M3/M4 UI behavior unchanged vs refactored.

## Out of scope

- Actual ERAF file write/open/export (M6)
- Incomplete-JPEG soft warning (ADR-008 → M6/M7)
- Accessibility polish (M8)
- Android

## Exit criteria

- [ ] Illegal transitions rejected with clear behavior
- [ ] Discard/cancel/start-over reset to Idle and clear media
- [ ] ReadyToSave only with photo + valid audio
- [ ] Transition unit tests pass; full `dotnet test` green
- [ ] Build + `winapp run` smoke (import → record → ready)
- [ ] Commit after Cursor review

## Report back to Cursor

State diagram (or table), files changed, test results, any UX change vs M4.
