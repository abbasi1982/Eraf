# ERAF Project Status

**Last updated:** 2026-10-23 (M2 closed by Cursor)

## Roles

- **Cursor:** supervisor / reviewer only — see [ROLES.md](ROLES.md)
- **Claude Code:** implementer — executes authorized milestone briefs only

## Current phase

Phase 1 — Windows. **M0–M2 closed.** Next: **M3** (image import/preview).

Phase 2 — Android remains blocked until the Windows release gate.

## Completed

| Item | Notes |
|---|---|
| M0 Tooling smoke | Closed |
| M0 namespace rename | Closed |
| M1 Format spec + docs | Closed — C1–C4 / ADR-006 |
| **M2 Eraf.Format + tests** | Closed by Cursor 2026-10-23 — 48/48 tests verified |

## In progress

None until Claude Code starts [CLAUDE_CODE_BRIEF_M3.md](CLAUDE_CODE_BRIEF_M3.md) after committing M2.

## Next

1. **Authorized:** Commit M2 (library, tests, fixtures, docs) including Cursor’s stream-length spec sentence and this close-out
2. Push when product owner asks
3. Execute M3

## Milestones

| ID | Status |
|---|---|
| M0–M1 | **Closed** |
| M2 Eraf.Format library + tests | **Closed** |
| M3 Image import/capture | **Authorized** — [CLAUDE_CODE_BRIEF_M3.md](CLAUDE_CODE_BRIEF_M3.md) |
| M4–M9 | Pending |

## M2 close-out (Cursor)

**Verdict:** Pass.

| Criterion | Result |
|---|---|
| `net8.0` library, no UI refs | Pass (`TreatWarningsAsErrors`) |
| Ordered §9 validation + exact error names | Pass |
| Writer refusals + recipe hex match | Pass (per tests + STATUS) |
| N01–N21 + valid fixtures | Pass |
| Hostile claimed length / no huge alloc | Pass (HostileInputTests) |
| `dotnet test` re-run by Cursor | **48 passed, 0 failed** |

**Accepted known limitations (not blocking):**

- Negative fixtures not rebuilt from recipes in-test (checked-in binaries + sidecars + prior Python generation)
- `MaxFileBytes` unreachable as deciding check in v1 (defensive)
- Full dual-payload max (~128 MiB) allocation on read — acceptable for v1; app must not open many huge files concurrently

**Spec clarification approved:** seekable streams define `FileLength` as `Length - Position` (added to `ERAF_FORMAT_SPEC.md` §6). No wire-format change.

**Commit authorization:** Claude Code **may commit** the full M2 working tree now, then await push instruction.

### M2 evidence (implementer summary)

See prior STATUS section content in git history if needed. Summary: `ErafReader` / `ErafWriter` / validator; fixtures under `fixtures/eraf/{valid,invalid,expected}`; 48 tests; root `.gitignore`.

## Known limitations carried forward

- Plain `dotnet run` does not register package identity; use `winapp run`.
- Prefer project-level restore; avoid piping NuGet/dotnet output when it hangs.
- SECURITY.md license identifiers unverified until nearer M9.
- Negative fixture recipe rebuild in C# tests still optional follow-up.
