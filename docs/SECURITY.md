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

## Temp files and privacy

- Temp media (extracted audio for playback, capture output, `.partial` save files) lives only in app-controlled directories, preferably under the app's local data folder with restrictive ACLs.
- Delete temp files on every path: success, cancel, discard, error and app close. Test cleanup, do not assume it.
- Never write incomplete audio as a saved file. Failed saves must not damage an existing file (atomic save, spec §11).
- The container adds no GPS, device IDs, contacts or paths. A JPEG's own Exif (which can include location) is preserved as imported; surface this to the user before any sharing feature exists, and treat stripping as a product decision.
- Do not log photo or audio contents, file paths of user files, or bytes from `.eraf` files.

## Network and telemetry

None. The app does not use the network, send telemetry, check for updates, or embed code that runs from file contents. Adding any of these is a scope change needing explicit approval.

## Permissions

Declare only what is needed: webcam and microphone capabilities in the package manifest. As of M4 the manifest declares `webcam` and `microphone`. Voice notes and photos are kept in memory only during a session (no temp files). A denial or missing device must produce an actionable message and leave image import available.

## Dependencies and license inventory

Maintain here as dependencies are added; each needs name, version, license and purpose. Current (M0):

| Package | Version | Purpose | License |
|---|---|---|---|
| Microsoft.WindowsAppSDK | 2.5.1 | WinUI 3 / app platform | see package (Microsoft) |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2705 | Build tooling | see package (Microsoft) |
| Microsoft.Windows.SDK.BuildTools.WinApp | 0.7.1 | `winapp` CLI for packaged launch | see package (Microsoft) |

Licenses are not yet verified from the packages; record exact license identifiers here before the M9 release gate. The project's own license is not yet declared (see README).

## Reporting

No process defined yet. Add a contact and handling policy before any public release.
