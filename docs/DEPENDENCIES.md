# Dependency and license inventory

Read from the NuGet package metadata (`.nuspec` in the local NuGet cache) on 2026-10-24. Versions are pinned in the project files. `Eraf.Format` has **no** package dependencies.

## Shipped with the Windows app (`src/Eraf.Windows`)

| Package | Version | Purpose | License (from package metadata) |
|---|---|---|---|
| Microsoft.WindowsAppSDK | 2.5.1 | WinUI 3 / app platform | Microsoft Software License Terms, "Microsoft Windows App SDK" (`license.txt` in the package; no SPDX id) |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2705 | Build tooling (not shipped) | Microsoft Windows SDK license (package gives only `aka.ms/WinSDKLicenseURL`, no SPDX id) |
| Microsoft.Windows.SDK.BuildTools.WinApp | 0.7.1 | `winapp` CLI for packaged launch (dev only) | MIT |

`Microsoft.WindowsAppSDK` 2.5.1 pulls in these Microsoft packages, all declaring the same Microsoft license file: `.Base` 2.0.4, `.Foundation` 2.3.12, `.InteractiveExperiences` 2.1.9, `.WinUI` 2.3.9, `.DWrite` 2.1.0, `.Widgets` 2.0.5, `.AI` 2.5.5, `.ML` 2.1.94, `.Search` 2.5.5, `.Runtime` [2.5.1].

## Tests only (`tests/Eraf.Format.Tests`, not shipped)

| Package | Version | License |
|---|---|---|
| Microsoft.NET.Test.Sdk | 17.8.0 | MIT (`LICENSE_MIT.txt` in package) |
| xunit | 2.5.3 | Apache-2.0 |
| xunit.runner.visualstudio | 2.5.3 | Apache-2.0 |

## Still open

- Microsoft's WindowsAppSDK and Windows SDK licenses are not SPDX identifiers; a human must read them (redistribution rights for the packaged app) before any public release.
- The project's own license is not declared (README). The product owner must choose one.
- Transitive packages of xunit (assert/core/extensibility) were not individually checked; they are the same Apache-2.0 family and are not shipped.
