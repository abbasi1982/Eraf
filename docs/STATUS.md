# ERAF Project Status

**Last updated:** 2026-10-24 (M8 closed by Cursor)

## Roles

- **Cursor:** supervisor / reviewer only — see [ROLES.md](ROLES.md)
- **Claude Code:** implementer — executes authorized milestone briefs only

## Current phase

Phase 1 — Windows. **M0–M8 closed.** Next: **M9** (Windows release gate evidence).

Phase 2 — Android remains blocked until the Windows release gate passes and is explicitly approved.

## Completed

| Item | Notes |
|---|---|
| M0–M7 | Closed |
| **M8 Accessibility / polish** | Closed by Cursor 2026-10-24 |

## In progress

None until Claude Code commits M8 + close-out, pushes with `a332282` (M7), then starts [CLAUDE_CODE_BRIEF_M9.md](CLAUDE_CODE_BRIEF_M9.md).

## Next

1. **Authorized:** Commit M8 working tree + Cursor close-out (STATUS, M9 brief, README)
2. **Authorized:** Push `a332282` + M8 commit(s) to `origin/master`
3. Execute M9

## Milestones

| ID | Status |
|---|---|
| M0–M7 | **Closed** |
| M8 Accessibility | **Closed** |
| M9 Windows release gate | **Authorized** — [CLAUDE_CODE_BRIEF_M9.md](CLAUDE_CODE_BRIEF_M9.md) |

## M8 close-out (Cursor)

**Verdict:** Pass with documented limitation.

| Criterion | Result |
|---|---|
| Automation names / help text on controls | Pass (UIA inspection) |
| Keyboard primary workflow | Pass (real keys + UIA focus) |
| Esc cancels camera; focus after steps | Pass |
| Export label polish | Pass |
| `dotnet test` | **283 passed** (no new tests expected) |
| Narrator heard announcements | **Not verified** — carry to M9 limitations; do not claim screen-reader certified |

Deliberate omission of live region on the recording timer is **accepted** (avoids spam; focus on Stop recording).

### M8 evidence (implementer)

See prior STATUS *M8 evidence* in working tree / git before this rewrite: MainPage automation properties, `FocusNextControl`, keyboard walk table.

## Known limitations carried forward

- Plain `dotnet run` / use `winapp run`
- Camera/mic deny paths unexercised on real denial
- Narrator not run (names exist via UIA only)
- High-contrast / text scaling / touch not tested
- SECURITY.md license identifiers → M9
- Orphan `.partial` cleanup only on next save to same name
