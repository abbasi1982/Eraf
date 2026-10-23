# ERAF Project Status

**Last updated:** 2026-10-23 (M3 implemented, awaiting Cursor review)

## Roles

- **Cursor:** supervisor / reviewer only — see [ROLES.md](ROLES.md)
- **Claude Code:** implementer — executes authorized milestone briefs only

## Current phase

Phase 1 — Windows. **M0–M2 closed.** **M3 implemented, awaiting Cursor review** (see M3 evidence).

Phase 2 — Android remains blocked until the Windows release gate.

## Completed

| Item | Notes |
|---|---|
| M0 Tooling smoke | Closed |
| M0 namespace rename | Closed |
| M1 Format spec + docs | Closed — C1–C4 / ADR-006 |
| **M2 Eraf.Format + tests** | Closed by Cursor 2026-10-23 — 48/48 tests verified |

## In progress

Cursor review of M3 (not committed yet, per the brief).

## Next

1. **Authorized:** Commit M2 (library, tests, fixtures, docs) including Cursor’s stream-length spec sentence and this close-out
2. Push when product owner asks
3. Cursor reviews M3 → issues M4 brief (audio)

## Milestones

| ID | Status |
|---|---|
| M0–M1 | **Closed** |
| M2 Eraf.Format library + tests | **Closed** |
| M3 Image import/capture | **Done, awaiting Cursor review** — see [M3 evidence](#m3-evidence-2026-10-23) |
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

## M3 evidence (2026-10-23)

Image import with preview, and camera capture, in `Eraf.Windows`. No ERAF save/open (M6), no audio (M4), no state machine beyond photo preview (M5). Not committed (awaiting review).

### Files

- `src/Eraf.Format/ErafMedia.cs` (new public API): `ErafMedia.IsJpeg`, `ErafMedia.PlanImageImport(length, head)` returning `ImageImportAction` (`KeepAsJpeg`, `ConvertToJpeg`, `RejectEmpty`, `RejectTooLarge`). UI-free, so it is unit-tested. This is a public-API addition, **not** a wire-format change.
- `tests/Eraf.Format.Tests/ErafMediaTests.cs` (8 new tests).
- `src/Eraf.Windows/ImageSession.cs`: the photo's JPEG bytes plus origin; in memory only, so there are no temp files.
- `src/Eraf.Windows/ImageImportService.cs`: picker, size check before reading, keep-or-convert, conversion via `BitmapDecoder` / `BitmapEncoder`.
- `src/Eraf.Windows/CameraService.cs`: `MediaCapture` live preview (`MediaPlayer` fed from a `MediaFrameSource`) and JPEG still capture.
- `src/Eraf.Windows/MainPage.xaml(.cs)`: idle / camera / photo panels, `InfoBar` for errors. `App.MainWindow` exposed for the picker.
- `Package.appxmanifest`: `<DeviceCapability Name="webcam"/>` added (camera UI ships). No microphone.
- `Eraf.Windows.csproj`: project reference to `Eraf.Format`.

### Commands run and results

| Command | Result |
|---|---|
| `dotnet build src\Eraf.Windows\Eraf.Windows.csproj -p:Platform=x64` | PASS, 0 warnings, 0 errors |
| `dotnet test tests\Eraf.Format.Tests\Eraf.Format.Tests.csproj` | **PASS: 56 total, 56 passed** (48 from M2 + 8 new) |
| `winapp run src\Eraf.Windows\Eraf.Windows.csproj --detach --arch x64` | PASS, built and launched with the webcam capability in the manifest |

### Manual verification (driven through UI Automation, observed results only)

Test images were generated in the scratchpad and are **not** committed. Each row was a fresh launch of the registered package, **Import image**, file name typed into the picker, Enter.

| Input | Observed |
|---|---|
| Real JPEG | Preview shown; label `JPEG, 17 KB, kept as is` |
| Real PNG | Preview shown; label `converted to JPEG, 17 KB` |
| Real BMP | Preview shown; `converted to JPEG, 17 KB` |
| PNG file named `.jpg` | `converted to JPEG` (signature, not extension, decides) |
| Random bytes named `.png` | InfoBar: not an image Windows can read; stays Idle |
| Empty `.jpg` | InfoBar: file is empty; stays Idle |
| 70 MiB `.png` | InfoBar: larger than 64 MB; rejected **before** reading the file; stays Idle |
| Truncated JPEG (first 2000 bytes) | **Accepted**, kept as is; preview shows the top of the image and gray below (see limitations) |
| Picker Cancel (Esc) | Returns to Idle, no message, buttons enabled |
| Discard photo | Returns to Idle, preview and info cleared |
| Camera: Use camera | Windows prompt "Let Eraf.Windows access your camera?"; **Yes** was chosen; consent stored as Allow |
| Camera: live preview | Real video from the Surface front camera shown |
| Camera: Take photo | Photo captured as JPEG (123 KB), previewed, label `Camera photo (JPEG, 123 KB)` |
| Camera: Discard, then Use camera again | Preview works again (camera was released) |
| Camera: Cancel | Returns to Idle |

Hardware: Surface Camera Front and Surface IR Camera Front are present; the color camera was selected automatically.

### Problems found and fixed during verification

- `Windows.Storage.Pickers.FileOpenPicker` with `InitializeWithWindow` failed with `COMException 0x80004005` and an empty message when run under `winapp run`. Switched to `Microsoft.Windows.Storage.Pickers.FileOpenPicker(AppWindow.Id)` (documented for Windows App SDK 2.0; see ADR-007), which works.
- Early draft cleared the preview it had just set; fixed before the first run.
- Driving the picker: clicking its **Open** button through UI Automation was unreliable; sending Enter in the file-name box works. This is only a test-automation detail.

### Not verified (state this, do not assume)

- **Denied camera permission and no-camera messages** are implemented (`UnauthorizedAccessException` and empty source-group branches in `CameraService`) but were **not exercised**: permission was granted and a camera exists. Denying would have changed a persistent privacy setting on this machine.
- **Mouse/keyboard interaction** was not tested; buttons were activated through UI Automation, and keyboard navigation and screen-reader labels are M8.
- Non-JPEG formats beyond PNG/BMP (GIF, TIFF), EXIF orientation, very large images, and HEIC were not tested. The picker filter lists `.jpg .jpeg .png .bmp .gif .tif .tiff`.
- The picker's file-type dropdown shows "All Files" rather than the image list; the filter's effect on what the dialog displays was not examined.

### Limitations and notes for Cursor

- **Truncated or damaged JPEGs are accepted** when they start with `FF D8 FF` and Windows can partially decode them. The spec (C1) checks only the signature, so this is by design, but the user can end up saving a half-image. Options for later: require a trailing `FF D9` (needs a spec change) or warn when the decoder reports a partial image. Not changed here.
- Conversion drops alpha (`BitmapAlphaMode.Ignore`) and does not rotate by EXIF; PNG/BMP sources have no orientation tag in practice, but JPEGs are never converted so their EXIF is preserved as the spec requires.
- The camera path takes the photo from `CapturePhotoToStreamAsync` with default settings; resolution and exposure are not configurable.
- The brief's item 8 allowed manual UI checks; there is no UI test project. The only automated tests of M3 logic are the 8 `ErafMedia` tests.
- Privacy: camera screenshots from verification (they showed the user) were deleted from the scratchpad and never entered the repo.
