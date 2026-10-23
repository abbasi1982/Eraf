# ERAF Project Status

**Last updated:** 2026-10-23 (M0 launch verified; awaiting Cursor close-out)

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

M0 build and launch evidence recorded below; awaiting Cursor to close M0. Follow-up: rename `RootNamespace` `Eraf_Windows` to `Eraf.Windows` (approved by Cursor).

## Next

1. Approve M0 brief for Claude Code execution
2. Claude Code runs M0 and reports evidence
3. Cursor reviews M0; if passed, issues M1 brief
4. M1 — Author normative `ERAF_FORMAT_SPEC.md` and remaining governance docs

## Milestones

| ID | Status |
|---|---|
| M0 Tooling smoke | Build + launch verified (see M0 evidence) — awaiting Cursor close-out |
| M1 Format spec + docs | Pending |
| M2 Eraf.Format library + tests | Pending |
| M3 Image import/capture | Pending |
| M4 Audio record/playback | Pending |
| M5 Session workflow | Pending |
| M6 Save/open/export | Pending |
| M7 Hardening | Pending |
| M8 Accessibility | Pending |
| M9 Windows release gate | Pending |

## M0 evidence (2026-10-23)

**Result:** restore + build PASS. Launch NOT confirmed (blocked, see below).

### Commands run and results

| Command | Result |
|---|---|
| `dotnet --info` | SDK 8.0.426, win-x64, no workloads installed |
| `dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates` | PASS; `winui` (WinUI Blank App) template available |
| `dotnet new sln -n Eraf` | PASS |
| `dotnet new winui -n Eraf.Windows -o src/Eraf.Windows -tfm net8.0 -tpmv 10.0.19041.0` | Files generated; command then hung (no output, no restore) and was killed. Template default TFM is net10.0, so `-tfm net8.0` is required. |
| `dotnet sln add src/Eraf.Windows/Eraf.Windows.csproj` | PASS |
| `dotnet restore Eraf.sln` (piped output) | Hung 10 min, killed |
| `dotnet restore src/Eraf.Windows/Eraf.Windows.csproj -v:d` (output redirected to file) | PASS, 39 s |
| `dotnet build src/Eraf.Windows/Eraf.Windows.csproj -c Debug -p:Platform=x64` | PASS, 0 warnings, 0 errors, 2m34s (NuGet retries on transient SSL/timeouts) |
| Run `bind\Debug\...\Eraf.Windows.exe` directly | FAIL: `COMException 0x80040154 REGDB_E_CLASSNOTREG` in `DeploymentManagerCS.AutoInitialize` (exit -532462766) |
| `dotnet run --project src/Eraf.Windows/Eraf.Windows.csproj` (WinApp build-tools run hook) | FAIL: same `REGDB_E_CLASSNOTREG` |

### Package versions pinned (in `Eraf.Windows.csproj`; template used floating `*`)

- `Microsoft.WindowsAppSDK` 2.5.1
- `Microsoft.Windows.SDK.BuildTools` 10.0.28000.2705
- `Microsoft.Windows.SDK.BuildTools.WinApp` 0.7.1
- TFM `net8.0-windows10.0.26100.0`, `TargetPlatformMinVersion` 10.0.17763.0 (template value; `-tpmv` 10.0.19041.0 was not applied to the csproj)

### Machine facts

- Windows App Runtime 2.5.1.0 is installed (matches the pinned SDK).
- `HKLM\...\AppModelUnlock` key absent: Developer Mode appears **not enabled** (not confirmed in Settings).
- No VS Installer checked; build succeeded with the .NET SDK alone.

### Blocker: window launch unverified

The unpackaged exe and `dotnet run` both fail activating a Windows App SDK class, so no empty window was observed. Likely causes (unverified): Developer Mode off, so the debug package identity cannot be registered; or the WinApp run hook did not register identity. Next step needs the user: enable Developer Mode, then retry `dotnet run`, or deploy the MSIX from Visual Studio.

### Remaining risks

- Launch/deploy path unproven; camera/mic capability declarations cannot be tested until it is.
- `dotnet new` and solution-level `dotnet restore` hung when run with piped output in this shell; use project-level restore with output redirected to a file.
- NuGet fetches were slow with transient SSL/timeouts.
- WASDK 2.x is a newer major line than the plan's "1.6+" expectation; Cursor should confirm it is acceptable before M1.
- `RootNamespace` is the template's `Eraf_Windows`; confirm whether to rename to `Eraf.Windows`.

### M0 launch retry (after Developer Mode enabled)

Cursor decisions: WASDK 2.5.1 accepted; `TargetPlatformMinVersion` 10.0.17763.0 OK; rename `RootNamespace` in a follow-up.

| Command | Result |
|---|---|
| `AppModelUnlock\AllowDevelopmentWithoutDevLicense` | 1 (Developer Mode on) |
| `dotnet run --project src/Eraf.Windows/Eraf.Windows.csproj` | FAIL: same `REGDB_E_CLASSNOTREG`. Dev Mode alone did not fix it. The `Microsoft.Windows.SDK.BuildTools.WinApp` run hook never activated (no `[WinAppRunSupport]` messages in `-v:n` output), so `dotnet run` started the bare exe without package identity. Root cause of the inactive hook not determined. |
| `winapp.exe run src\Eraf.Windows\Eraf.Windows.csproj --detach --arch x64` (from `~/.nuget/packages/microsoft.windows.sdk.buildtools.winapp/0.7.1/tools/win-x64/`) | PASS: build 0 warnings / 0 errors, debug package registered, app launched (PID 15352) |

Observed: process `Eraf.Windows` running and responding, main window titled `Eraf.Windows`, screenshot shows an empty WinUI window. Process was then killed. The dev package stays registered on this machine (package name `90DA0760-F0D9-4200-A4DB-1BDF858BEB2B`, dev mode, loose layout under `bin\x64\Debug\...\AppX`; `Remove-AppxPackage` to remove).

**Working launch command for this machine:** `winapp run src\Eraf.Windows\Eraf.Windows.csproj --arch x64` using the winapp.exe above. Plain `dotnet run` does not work yet.

Unpackaged fallback (`WindowsPackageType=None`) was not needed.
