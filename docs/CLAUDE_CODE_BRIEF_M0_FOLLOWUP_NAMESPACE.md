# Claude Code Brief — M0 follow-up: RootNamespace rename

**Authorized by:** Cursor (supervisor)  
**Implementer:** Claude Code  
**When:** Immediately, before M1. Does not reopen M0.

## Goal

Rename template `RootNamespace` / C# namespaces from `Eraf_Windows` to `Eraf.Windows`.

## Required work

1. Set `<RootNamespace>Eraf.Windows</RootNamespace>` in `src/Eraf.Windows/Eraf.Windows.csproj`.
2. Update all `namespace` / `using` / XAML `x:Class` declarations accordingly.
3. Rebuild with the documented working path:
   - `winapp run src\Eraf.Windows\Eraf.Windows.csproj --arch x64`  
   or at least `dotnet build ... -p:Platform=x64` if a full launch retest is unnecessary after a pure rename.
4. Confirm the app still builds (0 warnings preferred, 0 errors required).
5. Update `docs/STATUS.md`: note rename done; clear the follow-up item.
6. Optionally remove the leftover debug Appx package if still registered:
   - `Remove-AppxPackage 90DA0760-F0D9-4200-A4DB-1BDF858BEB2B_1.0.0.0_x64__1z32rh13vfry6`  
   (re-register on next `winapp run`).

## Out of scope

- Format spec, architecture docs (those are M1)
- Fixing inactive `dotnet run` WinApp hook (known limitation; document only)
- Android

## Exit criteria

- [ ] No remaining `Eraf_Windows` namespace/root references in source
- [ ] Build succeeds
- [ ] STATUS.md updated
- [ ] Commit locally; push when product owner asks (or with the pending M0 commits)
