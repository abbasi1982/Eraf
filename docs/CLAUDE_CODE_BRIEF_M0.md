# Claude Code Brief — Milestone M0 (Tooling Smoke)

**Authorized by:** Cursor (supervisor)  
**Implementer:** Claude Code  
**Scope:** Tooling and blank WinUI smoke only. No ERAF features, format library, camera, or audio.

## Goal

Prove that a blank .NET 8 WinUI 3 packaged app can be created, restored, and built on this machine.

## Prerequisites to verify (do not assume)

- .NET SDK 8.x (`dotnet --info`)
- WinUI / Windows App SDK templates available, or install them
- Developer Mode if required for packaged deploy
- Record any missing tooling as blockers in `docs/STATUS.md`

## Required work

1. Read `docs/IMPLEMENTATION_PLAN.md`, `docs/STATUS.md`, and `docs/ROLES.md`.
2. Install WinUI project templates if missing, e.g.:
   - `dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates`
   - or document Visual Studio WinUI workload requirement if CLI templates fail
3. Create solution/project structure consistent with the plan:
   - `Eraf.sln`
   - `src/Eraf.Windows/` blank WinUI 3 .NET 8 app (packaged)
   - Do **not** implement `Eraf.Format` yet (that is M2)
4. Restore and build successfully.
5. If runnable without extra manual setup, launch once and confirm an empty window; otherwise document the exact blocker (Developer Mode, WASDK runtime, etc.).
6. Update `docs/STATUS.md` with:
   - Commands actually run
   - Pass/fail results
   - Package versions pinned
   - Remaining risks

## Out of scope

- ERAF format spec or library
- Camera, microphone, file format UI
- Android
- Commits/pushes unless the user explicitly asks

## Exit criteria (Cursor will verify)

- [ ] Solution exists under the agreed layout
- [ ] `dotnet build` (or documented VS build) succeeds with captured output
- [ ] STATUS.md lists evidence and any unverified assumptions
- [ ] No product feature code beyond the blank shell

## Report back to Cursor

Return: files changed, design decisions (template/WASDK version), commands + results, limitations, risks.
