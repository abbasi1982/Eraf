# ERAF Project Status

**Last updated:** 2026-10-24 (M4 closed by Cursor)

## Roles

- **Cursor:** supervisor / reviewer only — see [ROLES.md](ROLES.md)
- **Claude Code:** implementer — executes authorized milestone briefs only

## Current phase

Phase 1 — Windows. **M0–M4 closed.** Next: **M5** (session state machine).

Phase 2 — Android remains blocked until the Windows release gate.

## Completed

| Item | Notes |
|---|---|
| M0–M3 | Closed |
| **M4 Audio record/playback** | Closed by Cursor 2026-10-24 — ADR-009 accepted |

## In progress

None until Claude Code commits/pushes M4 (+ unpushed `ee60ded` M3 close-out), then starts [CLAUDE_CODE_BRIEF_M5.md](CLAUDE_CODE_BRIEF_M5.md).

## Next

1. **Authorized:** Commit M4 working tree (include this close-out, ADR-009 acceptance, M5 brief, README)
2. **Authorized:** Push `ee60ded` + M4 commit(s) to `origin/master`
3. Execute M5

## Milestones

| ID | Status |
|---|---|
| M0–M3 | **Closed** |
| M4 Audio record/playback | **Closed** |
| M5 Session workflow | **Authorized** — [CLAUDE_CODE_BRIEF_M5.md](CLAUDE_CODE_BRIEF_M5.md) |
| M6–M9 | Pending |

## M4 close-out (Cursor)

**Verdict:** Pass with documented limitations.

| Criterion | Result |
|---|---|
| AAC-LC M4A record via `CreateM4a` | Pass (box parse evidence in STATUS) |
| Play / retry / discard; failed re-record keeps prior clip | Pass (manual) |
| Duration/size enforcement | Pass (unit tests + 5 s auto-stop proxy; full 10 min not waited) |
| Empty/failed never success | Pass (code + too-short manual) |
| Mic capability declared | Pass |
| `ErafMedia.CheckAudio` / `IsM4a` | Pass |
| `dotnet test` re-run by Cursor | **64 passed, 0 failed** |
| ADR-009 no pause/resume | **Accepted** |

**Not verified (carry forward):** denied/missing mic; subjective audibility; full 10-minute wall-clock recording.

**Privacy:** Microphone Allow stored for the dev package; revoke via Settings > Privacy & security > Microphone if undesired.

### M4 evidence (implementer)

See prior STATUS body / working tree: `AudioRecorder`, `ClipPlayer`, `CaptureSession`, MainPage voice UI, 8 new `ErafMedia` tests.

## Known limitations carried forward

- Plain `dotnet run` / use `winapp run`
- Truncated JPEG can enter session (ADR-008)
- Camera/mic deny paths largely unexercised
- Soft incomplete-JPEG warning deferred to M6/M7
- SECURITY.md licenses unverified until nearer M9
