# ERAF Project Status

**Last updated:** 2026-10-23 (M0 closed by Cursor)

## Roles

- **Cursor:** supervisor / reviewer only — see [ROLES.md](ROLES.md)
- **Claude Code:** implementer — executes authorized milestone briefs only

## Current phase

Phase 1 — Windows. **M0 closed.** M0 namespace follow-up done. Next: M1 docs.

Phase 2 — Android remains blocked until the Windows release gate.

## Completed

| Item | Notes |
|---|---|
| Repository audit | Greenfield at start |
| Stack / container decisions | WinUI 3 + Kotlin/Compose later; binary ERAF Design A |
| Phase 1 plan | [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) |
| **M0 Tooling smoke** | Build + packaged launch via `winapp run` verified — **closed by Cursor 2026-10-23** |

## In progress

1. Claude Code: [CLAUDE_CODE_BRIEF_M1.md](CLAUDE_CODE_BRIEF_M1.md)

## Next

1. Push local commits (product owner / Claude Code when asked)
2. Execute M1 (format spec + governance docs)
3. Cursor reviews M1 → issues M2 brief (`Eraf.Format`)

## Milestones

| ID | Status |
|---|---|
| M0 Tooling smoke | **Closed** — build + `winapp run` launch verified |
| M0 follow-up namespace rename | **Done** — `Eraf_Windows` → `Eraf.Windows` (see below) |
| M1 Format spec + docs | Authorized after namespace follow-up — see [CLAUDE_CODE_BRIEF_M1.md](CLAUDE_CODE_BRIEF_M1.md) |
| M2 Eraf.Format library + tests | Pending |
| M3 Image import/capture | Pending |
| M4 Audio record/playback | Pending |
| M5 Session workflow | Pending |
| M6 Save/open/export | Pending |
| M7 Hardening | Pending |
| M8 Accessibility | Pending |
| M9 Windows release gate | Pending |

## M0 close-out (Cursor)

**Verdict:** Pass with documented limitation.

| Criterion | Result |
|---|---|
| Solution + blank WinUI 3 .NET 8 app | Pass |
| Restore + build (0 errors) | Pass |
| Empty window observed | Pass via `winapp run ... --arch x64` |
| `dotnet run` packaged launch | **Fail** — WinApp run hook inactive; `REGDB_E_CLASSNOTREG` without identity |
| Unpackaged fallback | Not required |

**Accepted pins:** Windows App SDK **2.5.1**; BuildTools **10.0.28000.2705**; BuildTools.WinApp **0.7.1**.

**Documented Windows launch path:** use `winapp` from the BuildTools.WinApp package (`winapp run src\Eraf.Windows\Eraf.Windows.csproj --arch x64`). Do not claim plain `dotnet run` works until the hook is fixed.

**Housekeeping:** Debug Appx `90DA0760-F0D9-4200-A4DB-1BDF858BEB2B_1.0.0.0_x64__1z32rh13vfry6` may remain registered; remove with `Remove-AppxPackage` when desired.

### Historical M0 evidence

See git history for full command tables. Summary:

- Templates installed; `-tfm net8.0` required (template default net10).
- Project-level restore with output redirected to a file; piped/`dotnet restore` on the solution hung in this environment.
- Launch succeeded only after Developer Mode on **and** invoking `winapp run` directly.

## Known limitations carried forward

- Plain `dotnet run` does not register package identity on this machine.
- Prefer project-level restore; avoid piping NuGet/dotnet output in the agent shell when it hangs.

## M0 follow-up: namespace rename (2026-10-23)

- `RootNamespace`, C# `namespace` declarations, and XAML `x:Class` / `xmlns:local` in `App`, `MainPage`, `MainWindow` changed from `Eraf_Windows` to `Eraf.Windows`. `grep Eraf_Windows` over `src/` (excluding `bin`/`obj`) returns nothing.
- `winapp run src\Eraf.Windows\Eraf.Windows.csproj --detach --arch x64`: build 0 warnings, 0 errors; app launched (PID 5564), window `Eraf.Windows` responding. Process then killed.
- Leftover debug package `90DA0760-…` removed with `Remove-AppxPackage`; it re-registers on the next `winapp run`.
