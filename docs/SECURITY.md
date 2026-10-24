# ERAF Security Notes

Threat model and rules for handling `.eraf` files. Format rules: [ERAF_FORMAT_SPEC.md](ERAF_FORMAT_SPEC.md).

## Threat model

An `.eraf` file received from anywhere is **untrusted input**. The app also handles the user's own photos and voice, which are private. Primary risks: malformed files that crash or exhaust memory, payloads that exploit the system decoders, leaked temp media, and a mistaken belief that checksums protect against tampering.

Out of scope for v1: encryption, signatures, access control, DRM, and protection against someone who can edit the file.

## Integrity is not authentication

CRC-32 and SHA-256 in the format detect **accidental** corruption only. Anyone who modifies a file can recompute both. Nothing in the UI or docs may describe a passing hash as "verified", "authentic" or "safe". Authentication would need a signature scheme and a spec version bump.

## Parsing untrusted files

- Apply the full ordered validation in spec §9 before any payload is decoded or played; reject on the first failure and never "repair".
- Validate claimed lengths against the limits and the real file length **before** allocating. Use checked arithmetic. A header claiming `0xFFFFFFFFFFFFFFFF` bytes must fail cleanly, not allocate (negative test N17).
- Limits: 256 MiB file, 64 MiB per payload, 10 minutes of audio. Stream or bound reads; do not read an unbounded stream into memory.
- Unsupported version: fail immediately without interpreting the rest.
- The format library does not decode media. The signature check (spec §8.1) is a plausibility check, not a defense against a crafted JPEG or M4A. JPEG and AAC decoding is done by the operating system's media stack, which is the real attack surface; keep Windows updated and do not add third-party decoders without review.
- Never execute, load or interpret anything from a payload other than as image or audio data.
- Opening a file reads straight from a `FileStream`: the reader sees the real length and allocates only after the claimed lengths are checked, so there is no separate size pre-check that could change which error is reported. A valid container whose photo cannot be decoded is reported, not shown.
- Saving and exporting write a unique temp file next to the target, flush it, verify it, and swap it in (`AtomicFile`); a failure leaves any existing file unchanged. A temp `.partial` can remain only if the process is killed mid-write; the next save to the same target name deletes such leftovers (M7: only `{target name}.{32 hex}.partial`, untouched for 5+ minutes, not open elsewhere — never any other file).
- New files store the **save time (UTC)** as `CreatedUtcUnixMs`; this is the only metadata the app writes (see ADR-011).

## Temp files and privacy

- Temp media (extracted audio for playback, capture output, `.partial` save files) lives only in app-controlled directories, preferably under the app's local data folder with restrictive ACLs.
- Delete temp files on every path: success, cancel, discard, error and app close. Test cleanup, do not assume it.
- Never write incomplete audio as a saved file. Failed saves must not damage an existing file (atomic save, spec §11).
- The container adds no GPS, device IDs, contacts or paths. A JPEG's own Exif (which can include location) is preserved as imported; surface this to the user before any sharing feature exists, and treat stripping as a product decision.
- Do not log photo or audio contents, file paths of user files, or bytes from `.eraf` files. M7 audit: the source contains no logging or console/trace output at all; failed save/export messages (`SaveErrorText`) contain neither paths nor raw system messages. The success message names the chosen path on screen only.

## File association

The app registers itself as a handler for `.eraf` through its package manifest; Windows writes the keys under the current user (`HKCU\Software\Classes`) when the package is registered, and removes them with the package. Nothing in the app writes to the registry itself. A file opened this way goes through the same validation as any other (spec 9), and its path is used only to read it.

## Screen, clipboard and shortcut

Screenshots and clipboard pictures are held in memory and encoded to a JPEG in memory; nothing is written until the user saves. The "picture copied" offer looks only at which formats are on the clipboard (not at their contents) and only while the app is at the file list; the picture is read when the user presses Make ERAF or Paste. It can be turned off in Settings. The global shortcut Ctrl+Shift+F9 is registered only while the app runs and can be turned off. Window and screen capture use plain GDI calls and add no permission or package. Right-click Delete moves items to the Recycle Bin and never deletes for good except where Windows itself does not use the bin, and the dialog says so first.

## Network and telemetry

None. The app does not use the network, send telemetry, check for updates, or embed code that runs from file contents. Adding any of these is a scope change needing explicit approval.

## Permissions

Declare only what is needed: webcam and microphone capabilities in the package manifest. As of M4 the manifest declares `webcam` and `microphone`. Voice notes and photos are kept in memory only during a session (no temp files). A denial or missing device must produce an actionable message and leave image import available.

## Dependencies and license inventory

The full inventory with versions, purposes and license identifiers read from package metadata is in [DEPENDENCIES.md](DEPENDENCIES.md). Summary: `Eraf.Format` has no dependencies; the app uses Microsoft Windows App SDK 2.5.1 (Microsoft license text, no SPDX id), the Windows SDK build tools (Microsoft license) and `winapp` 0.7.1 (MIT); tests use xunit (Apache-2.0) and Microsoft.NET.Test.Sdk (MIT). **Still open for a public release:** a human reading of the two Microsoft license texts, and the project's own license.

## Reporting

No process defined yet. Add a contact and handling policy before any public release.
