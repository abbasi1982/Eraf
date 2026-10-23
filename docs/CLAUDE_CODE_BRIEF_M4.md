# Claude Code Brief — Milestone M4 (Audio record / playback / retry / discard)

**Authorized by:** Cursor (supervisor)  
**Implementer:** Claude Code  
**Prerequisite:** M3 closed.  
**Depends on:** Session JPEG from M3 (`ImageSession`); no ERAF write yet (M6).

## Goal

After a photo is in session (import or camera), record an AAC-LC M4A voice note, preview it, replace or discard it, with enforced duration and size limits. Photo+audio session ready for M5/M6 wiring.

## Required work

1. Extend session model to hold optional audio bytes + duration (keep JPEG + audio together; still not a global singleton dump).
2. Declare **microphone** capability in `Package.appxmanifest`.
3. **Record** via `MediaCapture` + `MediaEncodingProfile.CreateM4a` (or documented equivalent that yields AAC-LC in MPEG-4). Prefer tearing down camera preview before mic record if both conflict (photo-then-record).
4. UI must show: recording active, elapsed time, start/stop, then playback, retry, discard.
5. **Enforce** `ErafLimits.MaxAudioDurationMs` (10 min) and `MaxAudioBytes` (64 MiB) — refuse empty/partial audio as success.
6. **Playback** of the in-session M4A (temp extract under app data if needed; delete on discard/cancel).
7. Permission denial / no mic: clear message; do not block viewing the photo; saving ERAF later will require audio (v1).
8. Pause/resume: only if reliable on this stack; otherwise stop/retry only and record in DECISIONS / STATUS.
9. Automated tests for any pure limit/helpers; media hardware paths may be manual with STATUS checklist.
10. Update STATUS.md with commands, `winapp run` evidence, and what was / was not verified (denied mic, etc.).

## Out of scope

- ERAF save/open/export (M6)
- Full state-machine polish (M5 may consolidate transitions)
- Incomplete-JPEG warning (ADR-008 → M6/M7)
- Android

## Exit criteria

- [ ] Record → stop → play → retry → discard works with a photo in session
- [ ] Duration/size limits enforced
- [ ] Empty/failed recording never treated as success
- [ ] Mic denial / missing device handled with actionable text (code path present; exercise if safe)
- [ ] Build + `winapp run`; STATUS evidence
- [ ] Commit after Cursor review (unless already instructed otherwise)

## Report back to Cursor

Files changed, encoding profile used, pause/resume decision, permission behavior, test/manual results, risks.
