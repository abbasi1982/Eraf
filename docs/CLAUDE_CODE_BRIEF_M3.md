# Claude Code Brief — Milestone M3 (Image import + preview; camera if available)

**Authorized by:** Cursor (supervisor)  
**Implementer:** Claude Code  
**Prerequisite:** M2 closed and committed.  
**Depends on:** `Eraf.Format` (read/write later in M6; M3 holds JPEG bytes in session only)

## Goal

In `Eraf.Windows`, let the user obtain a JPEG (import required; camera optional) and preview it in-app. No ERAF save/open yet (M6). No audio yet (M4).

## Required work

1. Wire a minimal shell navigation: Idle → choose **Import image** (and **Capture** if camera path is ready).
2. **Import (mandatory):** `FileOpenPicker` for `.jpg`/`.jpeg`.
   - If bytes already start with `FF D8 FF`, keep them without recompression.
   - Non-JPEG: **convert-on-import to JPEG** (ADR-002 preference confirmed for M3). Use a Windows imaging API (e.g. `BitmapDecoder`/`BitmapEncoder`). On failure, show a clear error and stay Idle / return to picker.
3. **Preview:** display the session image; retain original JPEG bytes in a session object (not a global singleton dump).
4. **Camera (best-effort):** if `MediaCapture` init succeeds, allow capture to JPEG bytes into the same session. If camera missing or permission denied, show an actionable message; **import must still work**.
5. Declare webcam capability in `Package.appxmanifest` only if camera UI is shipped; microphone not required in M3.
6. Cancel/discard clears session image and any temps.
7. Update `docs/STATUS.md` and `docs/DECISIONS.md` (convert-on-import confirmed) with evidence: build + `winapp run` manual steps.
8. Automated tests: unit-test any pure conversion helpers if extracted; UI/camera may be manual with a checklist in STATUS.

## Out of scope

- Audio recording/playback (M4)
- ERAF save/open/export (M6)
- Full workflow state machine beyond photo preview (M5 can extend)
- Android

## Exit criteria

- [ ] Import JPEG → preview works
- [ ] Non-JPEG import converts or fails clearly
- [ ] Camera path works **or** documented unavailable with import still OK
- [ ] Session holds JPEG bytes for later milestones
- [ ] Build succeeds; launch via `winapp run`
- [ ] STATUS.md evidence recorded
- [ ] Commit after Cursor review (same rule as M2)

## Report back to Cursor

Files changed, APIs used, permission behavior, commands + results, limitations (no camera hardware, etc.).
