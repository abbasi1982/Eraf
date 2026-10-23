# ERAF Project Status

**Last updated:** 2026-10-23

## Roles

- **Cursor:** supervisor / reviewer only — see [ROLES.md](ROLES.md)
- **Claude Code:** implementer — executes authorized milestone briefs only

## Current phase

Phase 1 — Windows (planning → ready for M0 handoff). Phase 2 — Android is blocked until the Windows release gate passes.

## Completed

| Item | Notes |
|---|---|
| Repository audit | Greenfield: empty workspace, no existing code |
| Stack decision | .NET 8 + WinUI 3 (Windows); Kotlin + Compose (Android, later) |
| Container decision | Design A — binary ERAF container |
| Phase 1 plan copied into repo | See [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) |
| Role split documented | See [ROLES.md](ROLES.md) |
| M0 brief for Claude Code | See [CLAUDE_CODE_BRIEF_M0.md](CLAUDE_CODE_BRIEF_M0.md) |

## In progress

Awaiting product-owner approval for Claude Code to execute **M0** using [CLAUDE_CODE_BRIEF_M0.md](CLAUDE_CODE_BRIEF_M0.md). Cursor will review evidence afterward; Cursor will not implement M0.

## Next

1. Approve M0 brief for Claude Code execution
2. Claude Code runs M0 and reports evidence
3. Cursor reviews M0; if passed, issues M1 brief
4. M1 — Author normative `ERAF_FORMAT_SPEC.md` and remaining governance docs

## Milestones

| ID | Status |
|---|---|
| M0 Tooling smoke | Pending approval |
| M1 Format spec + docs | Pending |
| M2 Eraf.Format library + tests | Pending |
| M3 Image import/capture | Pending |
| M4 Audio record/playback | Pending |
| M5 Session workflow | Pending |
| M6 Save/open/export | Pending |
| M7 Hardening | Pending |
| M8 Accessibility | Pending |
| M9 Windows release gate | Pending |
