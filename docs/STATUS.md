# ERAF Project Status

**Last updated:** 2026-10-24 (M6 closed by Cursor)

## Roles

- **Cursor:** supervisor / reviewer only — see [ROLES.md](ROLES.md)
- **Claude Code:** implementer — executes authorized milestone briefs only

## Current phase

Phase 1 — Windows. **M0–M6 closed.** Next: **M7** (hardening).

Phase 2 — Android remains blocked until the Windows release gate.

## Completed

| Item | Notes |
|---|---|
| M0–M5 | Closed |
| **M6 Save/open/export** | Closed by Cursor 2026-10-24 — ADR-011 accepted |

## In progress

None until Claude Code commits M6 + close-out, pushes with `3ccacce` (M5), then starts [CLAUDE_CODE_BRIEF_M7.md](CLAUDE_CODE_BRIEF_M7.md).

## Next

1. **Authorized:** Commit M6 working tree + Cursor close-out (STATUS, ADR-011 acceptance, M7 brief, README)
2. **Authorized:** Push `3ccacce` + M6 commit(s) to `origin/master`
3. Execute M7

## Milestones

| ID | Status |
|---|---|
| M0–M5 | **Closed** |
| M6 Save/open/export | **Closed** |
| M7 Hardening | **Authorized** — [CLAUDE_CODE_BRIEF_M7.md](CLAUDE_CODE_BRIEF_M7.md) |
| M8–M9 | Pending |

## M6 close-out (Cursor)

**Verdict:** Pass.

| Criterion | Result |
|---|---|
| Atomic save + verify + replace | Pass (`AtomicFile` + locked-target manual) |
| Open + export byte-identical | Pass (independent Python parse) |
| Invalid fixtures rejected safely | Pass |
| Unsaved-work / overwrite prompts | Pass |
| ADR-008 non-blocking incomplete JPEG warn | Pass |
| `dotnet test` re-run by Cursor | **272 passed, 0 failed** |
| ADR-011 | **Accepted** |

**Defaults accepted:**

| Default | Decision |
|---|---|
| SHA-256 hashes on for new files | **Keep** |
| `CreatedUtcUnixMs` = save time UTC | **Keep** (not 0) |
| Open → edit → save as new / overwrite | **Keep** |
| No size pre-check before open | **Keep** (reader §9 order) |

**Incident note:** Accidental OneDrive Documents test save via UI Automation path — cleaned; no remaining test media in user folders. Treat as automation risk, not an app defect.

**Not verified (carry forward to M7):** read-only/full disk; close during save; denied cam/mic; near-max file size (unit/N17 only).

### M6 evidence (implementer)

See prior STATUS body: `AtomicFile`, `SessionFile`, `FilePickers`, workflow `Saved`/`FileOpened`/`Resume`, golden open via `SessionFile.Open`, UI smoke + independent parser.

## Known limitations carried forward

- Plain `dotnet run` / use `winapp run`
- Camera/mic deny paths largely unexercised
- Orphan `.partial` if process killed mid-write
- SECURITY.md license identifiers until M9
