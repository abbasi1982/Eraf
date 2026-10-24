# ERAF Project Status

**Last updated:** 2026-10-24 (M7 closed by Cursor)

## Roles

- **Cursor:** supervisor / reviewer only — see [ROLES.md](ROLES.md)
- **Claude Code:** implementer — executes authorized milestone briefs only

## Current phase

Phase 1 — Windows. **M0–M7 closed.** Next: **M8** (accessibility / polish).

Phase 2 — Android remains blocked until the Windows release gate.

## Completed

| Item | Notes |
|---|---|
| M0–M6 | Closed |
| **M7 Hardening** | Closed by Cursor 2026-10-24 |

## In progress

None until Claude Code commits M7 + close-out, then starts [CLAUDE_CODE_BRIEF_M8.md](CLAUDE_CODE_BRIEF_M8.md).

## Next

1. **Authorized:** Commit M7 working tree + Cursor close-out (STATUS, M8 brief, README)
2. Push when asked
3. Execute M8

## Milestones

| ID | Status |
|---|---|
| M0–M6 | **Closed** |
| M7 Hardening | **Closed** |
| M8 Accessibility | **Authorized** — [CLAUDE_CODE_BRIEF_M8.md](CLAUDE_CODE_BRIEF_M8.md) |
| M9 Windows release gate | Pending |

## M7 close-out (Cursor)

**Verdict:** Pass.

| Criterion | Result |
|---|---|
| Orphan `.partial` cleanup (strict name + age) | Pass (manual + tests) |
| Path-free save/export/open errors | Pass |
| Limit auto-stop informational; JPEG warning wording | Pass |
| SECURITY.md / no src logging | Pass |
| `dotnet test` re-run by Cursor | **283 passed, 0 failed** |

**Accepted findings:** Save As UI blocks unwritable folders before app code; write-denied existing file is the valid app-level exercise. Denied cam/mic remain code-only (do not change user privacy settings without explicit ask).

### M7 evidence (implementer)

See prior STATUS *M7 evidence* section in git history / working tree before this rewrite: `RemoveOrphanedPartials`, `SaveErrorText`, `SaveHardeningTests`, smoke table.

## Known limitations carried forward

- Plain `dotnet run` / use `winapp run`
- Camera/mic deny paths unexercised on real denial
- Orphan cleanup only on next save to same target name
- SECURITY.md license identifiers until M9
- Keyboard / Narrator → M8
