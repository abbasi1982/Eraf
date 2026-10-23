# ERAF Project Status

**Last updated:** 2026-10-23 (M3 closed by Cursor)

## Roles

- **Cursor:** supervisor / reviewer only — see [ROLES.md](ROLES.md)
- **Claude Code:** implementer — executes authorized milestone briefs only

## Current phase

Phase 1 — Windows. **M0–M3 closed.** Next: **M4** (audio).

Phase 2 — Android remains blocked until the Windows release gate.

## Completed

| Item | Notes |
|---|---|
| M0–M2 | Closed |
| **M3 Image import/capture** | Closed by Cursor 2026-10-23 — ADR-007, ADR-008 |

## In progress

None until Claude Code starts [CLAUDE_CODE_BRIEF_M4.md](CLAUDE_CODE_BRIEF_M4.md). Include this close-out (ADR-008, M4 brief, STATUS) in the next commit.

## Next

1. Commit Cursor M3 close-out docs if not already on remote
2. Execute M4
3. Cursor reviews M4 → M5 brief

## Milestones

| ID | Status |
|---|---|
| M0–M2 | **Closed** |
| M3 Image import/capture | **Closed** |
| M4 Audio record/playback | **Authorized** — [CLAUDE_CODE_BRIEF_M4.md](CLAUDE_CODE_BRIEF_M4.md) |
| M5–M9 | Pending |

## M3 close-out (Cursor)

**Verdict:** Pass with documented limitations.

| Criterion | Result |
|---|---|
| Import JPEG keep / non-JPEG convert | Pass (manual + `ErafMedia` tests) |
| Size check before full read | Pass |
| Camera preview + JPEG capture + release | Pass (manual) |
| In-memory `ImageSession` | Pass |
| Webcam capability; no mic yet | Pass |
| `dotnet test` | **56 passed** (re-verified by Cursor) |
| ADR-007 WASDK picker | **Accepted** |

**Truncated JPEG (ADR-008):** Accept for v1 (signature-only, matches C1). Soft warning at save deferred to M6/M7 — no format change.

**Not verified (carry forward):** denied camera permission; no-camera hardware path; GIF/TIFF/HEIC; EXIF orientation; mouse/keyboard a11y (M8).

**Privacy note:** Camera consent Allow was stored for the dev package; revoke via Settings > Privacy & security > Camera if undesired. Screenshots with faces correctly kept out of the repo.

### M3 evidence (implementer)

See git history / prior STATUS body: `ErafMedia`, `ImageImportService`, `CameraService`, MainPage panels, WASDK picker fix, UI Automation-driven checks.

## Known limitations carried forward

- Plain `dotnet run` / use `winapp run`
- Piped restore hangs in agent shell
- SECURITY.md licenses unverified until nearer M9
- Truncated JPEG can enter session (ADR-008)
- Camera deny / no-camera paths unexercised on this machine
