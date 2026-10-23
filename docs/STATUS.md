# ERAF Project Status

**Last updated:** 2026-10-23 (M1 closed by Cursor)

## Roles

- **Cursor:** supervisor / reviewer only — see [ROLES.md](ROLES.md)
- **Claude Code:** implementer — executes authorized milestone briefs only

## Current phase

Phase 1 — Windows. **M0 and M1 closed.** Next: **M2** (`Eraf.Format`).

Phase 2 — Android remains blocked until the Windows release gate.

## Completed

| Item | Notes |
|---|---|
| M0 Tooling smoke | Closed — `winapp run` launch verified |
| M0 namespace rename | `Eraf.Windows` |
| **M1 Format spec + docs** | Closed by Cursor 2026-10-23 — C1–C4 approved (ADR-006) |

## In progress

Claude Code executes [CLAUDE_CODE_BRIEF_M2.md](CLAUDE_CODE_BRIEF_M2.md).

## Next

1. **Authorized:** Commit uncommitted M1 docs/fixtures (and this STATUS / ADR-006 / M2 brief update), then push when asked
2. Implement M2; report evidence for Cursor review

## Milestones

| ID | Status |
|---|---|
| M0 Tooling smoke | **Closed** |
| M0 follow-up namespace rename | **Closed** |
| M1 Format spec + docs | **Closed** — C1–C4 approved |
| M2 Eraf.Format library + tests | **Authorized** — [CLAUDE_CODE_BRIEF_M2.md](CLAUDE_CODE_BRIEF_M2.md) |
| M3–M9 | Pending |

## M1 close-out (Cursor)

**Verdict:** Pass.

| Criterion | Result |
|---|---|
| Normative `ERAF_FORMAT_SPEC.md` | Pass — header matches plan; C1–C4 approved |
| Architecture / testing / security / decisions | Pass |
| Fixture recipes + negative matrix | Pass — independent Python check reported by implementer (script not in repo) |
| No `Eraf.Format` / `src` feature creep | Pass |
| Dependency license identifiers | Deferred to before M9 (noted in SECURITY.md) |

**C1–C4:** All **approved** (ADR-006).

**Commit authorization:** Claude Code **may commit** the M1 documentation set and related STATUS/README/CLAUDE updates. Push when the product owner requests (several commits remain ahead of `origin/master`).

### M1 evidence (implementer, 2026-10-23)

Docs and fixture policy only; no format library code.

Files: `ERAF_FORMAT_SPEC.md`, `ARCHITECTURE.md`, `TESTING.md`, `SECURITY.md`, `DECISIONS.md`, `fixtures/eraf/README.md`, `RECIPES.md`, `.gitattributes`; README/STATUS/CLAUDE updates.

Independent recipe validation (throwaway Python, not committed): both valid files OK; N01–N21 matched expected errors; CRC check value `0xCBF43926` reproduced. No `.eraf` binaries in repo yet (M2 may check them in).

## Known limitations carried forward

- Plain `dotnet run` does not register package identity; use `winapp run`.
- Prefer project-level restore; avoid piping NuGet/dotnet output when it hangs.
- `MaxFileBytes` is defensive in v1 (per-payload caps dominate).
- SECURITY.md license identifiers unverified until nearer M9.
