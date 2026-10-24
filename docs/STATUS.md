# ERAF Project Status

**Last updated:** 2026-10-24 (M5 closed by Cursor)

## Roles

- **Cursor:** supervisor / reviewer only — see [ROLES.md](ROLES.md)
- **Claude Code:** implementer — executes authorized milestone briefs only

## Current phase

Phase 1 — Windows. **M0–M5 closed.** Next: **M6** (save / open / export).

Phase 2 — Android remains blocked until the Windows release gate.

## Completed

| Item | Notes |
|---|---|
| M0–M4 | Closed |
| **M5 Session workflow** | Closed by Cursor 2026-10-24 — ADR-010 accepted |

## In progress

None until Claude Code commits M5 (with this close-out), then starts [CLAUDE_CODE_BRIEF_M6.md](CLAUDE_CODE_BRIEF_M6.md).

## Next

1. **Authorized:** Commit M5 working tree + Cursor close-out (STATUS, ADR-010 acceptance, M6 brief, README)
2. Push when asked (or with the commit if you usually push after review)
3. Execute M6

## Milestones

| ID | Status |
|---|---|
| M0–M4 | **Closed** |
| M5 Session workflow | **Closed** |
| M6 Save/open/export | **Authorized** — [CLAUDE_CODE_BRIEF_M6.md](CLAUDE_CODE_BRIEF_M6.md) |
| M7–M9 | Pending |

## M5 close-out (Cursor)

**Verdict:** Pass.

| Criterion | Result |
|---|---|
| Explicit transition table; illegal events rejected | Pass |
| Page gated by workflow; late results dropped | Pass (STATUS + code) |
| ReadyToSave only with photo + audio; Save stub | Pass |
| `dotnet test` re-run by Cursor | **202 passed, 0 failed** |
| ADR-010 linked-source testing | **Accepted** (optional later `Eraf.Session` library) |
| `AudioPreview` = playing; record → ReadyToSave | **Accepted** (keeps M4 UX) |

**Not verified (carry forward):** Saving/Saved/Error until M6 drives them; denied cam/mic; mouse/keyboard a11y.

### M5 evidence (implementer)

See prior STATUS body: `SessionWorkflow`, MainPage `Render`, 138 new tests (full grid + random walk), UI smoke.

## Known limitations carried forward

- Plain `dotnet run` / use `winapp run`
- Truncated JPEG (ADR-008) — soft warn at save in M6
- Camera/mic deny paths largely unexercised
- SECURITY.md licenses unverified until nearer M9
