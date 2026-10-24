# Claude Code Brief — Milestone M6 (ERAF save / open / export)

**Authorized by:** Cursor (supervisor)  
**Implementer:** Claude Code  
**Prerequisite:** M5 closed and committed.  
**Depends on:** `Eraf.Format` writer/reader, `CaptureSession` / `SessionWorkflow`, WASDK pickers (ADR-007).

## Goal

Persist a ready session as a `.eraf` file, reopen files for view/play, and export original image/audio payloads—with atomic save and overwrite protection.

## Required work

1. **Save** from `ReadyToSave` / `BeginSave`:
   - Build bytes with `ErafWriter` (JPEG + M4A from session; set duration; optional SHA-256 hashes **on** by default for new files unless STATUS documents otherwise).
   - Use WASDK folder/file save picker (same family as ADR-007).
   - Atomic write per spec §11 (temp beside target → flush → replace). On failure: `SaveFailed` → `Error`; existing file untouched.
   - Avoid silent overwrite: confirm or use unique name / picker that surfaces conflicts.
   - On success: `SaveSucceeded` → `Saved`; clear unsaved distinction in UI.
2. **Open** `.eraf`:
   - Picker → `ErafReader` full §9 validation → show image, play audio via existing player path.
   - Malformed/oversized: clear error; do not crash.
3. **Export** image and/or audio as original payload bytes (separate files); do not re-encode.
4. Wire `SessionWorkflow` Saving/Saved/Error paths for real; keep `AcknowledgeError` retry behavior.
5. Unsaved-work: if leaving with photo/audio and not Saved, confirm (Start over / close if applicable).
6. ADR-008 soft warning: if practical, non-blocking InfoBar when imported JPEG looks truncated/incomplete at save time—do not block save solely for missing EOI.
7. Automated tests: round-trip write/read of session payloads; atomic-replace failure leaves original (file-system test where feasible); open invalid fixtures still fail with exact errors (reuse golden set).
8. Update STATUS.md with commands and manual smoke via `winapp run`.

## Out of scope

- Full a11y polish (M8)
- Broad security hardening pass (M7 may deepen)
- Android

## Exit criteria

- [ ] Create `.eraf`, reopen, see image, play audio
- [ ] Export image and audio byte-identical to payloads
- [ ] Failed save does not destroy existing file
- [ ] Invalid files rejected safely
- [ ] Workflow Saving/Saved/Error exercised
- [ ] `dotnet test` green; STATUS evidence
- [ ] Commit after Cursor review

## Report back to Cursor

Files changed, hash-on-write default, picker APIs, test results, limitations.
