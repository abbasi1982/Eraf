# ERAF Format Specification — Version 1

**Status:** Normative for format version 1. This document is the authority; where it disagrees with [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) §5, this document wins (the plan sketch is the approved basis).
**Audience:** Independent implementers (C# in `Eraf.Format`, Kotlin later). An implementation must be buildable from this document and `fixtures/eraf/` alone.

The key words MUST, MUST NOT, SHOULD and MAY are used as in RFC 2119.

Items marked **[Cn]** (C1–C4) are clarifications beyond the original plan sketch. They are **approved** by Cursor (2026-10-23) and are normative; see [§13](#13-clarifications-added-to-the-plan-sketch) and [DECISIONS.md](DECISIONS.md) ADR-006.

---

## 1. Overview

An ERAF file (`.eraf`) holds exactly one JPEG image and one AAC-LC voice recording in MPEG-4 audio (`.m4a`) form, in a fixed binary container. It is not a renamed JPEG, WAV or ZIP.

Layout, in order:

1. Header — 64 bytes
2. Integrity block — 0, 32 or 64 bytes (selected by header flags)
3. Image payload — raw JPEG bytes
4. Audio payload — raw M4A bytes

There is no padding, alignment, trailing data or extension area in v1.

## 2. Conventions

- All multi-byte integers are **little-endian**. `u16`, `u32`, `u64` are unsigned 16/32/64-bit.
- Offsets are zero-based byte offsets from the start of the file.
- `MiB` = 1,048,576 bytes.
- Implementations MUST serialize and parse field by field with explicit byte order. They MUST NOT use in-memory struct layout as the wire format.
- Length arithmetic MUST be overflow-checked (use `u64` with checked operations or a wider type).

## 3. Header (64 bytes)

| Offset | Size | Type | Field | v1 value / rule |
|---:|---:|---|---|---|
| 0 | 4 | bytes | `Magic` | `45 52 41 46` (ASCII `ERAF`) |
| 4 | 2 | u16 | `FormatVersion` | `1` |
| 6 | 2 | u16 | `HeaderLength` | `64` |
| 8 | 2 | u16 | `Flags` | see §3.1 |
| 10 | 2 | u16 | `Reserved0` | `0` |
| 12 | 2 | u16 | `ImageMediaType` | `1` = `image/jpeg` |
| 14 | 2 | u16 | `AudioMediaType` | `1` = `audio/mp4` |
| 16 | 2 | u16 | `AudioCodec` | `1` = AAC-LC, `mp4a.40.2` |
| 18 | 2 | u16 | `Reserved1` | `0` |
| 20 | 8 | u64 | `ImagePayloadLength` | bytes, `1 … 64 MiB` |
| 28 | 8 | u64 | `AudioPayloadLength` | bytes, `1 … 64 MiB` |
| 36 | 8 | u64 | `AudioDurationMs` | milliseconds; `0` = unknown; `≤ 600000` |
| 44 | 8 | u64 | `CreatedUtcUnixMs` | ms since 1970-01-01T00:00:00Z; `0` = unknown |
| 52 | 4 | u32 | `HeaderChecksumCrc32` | CRC-32 of bytes `0 … 51` (§4) |
| 56 | 8 | bytes | `Reserved2` | all `00` |

Total: 64 bytes.

### 3.1 Flags

| Bit | Meaning when set |
|---:|---|
| 0 | An `ImageSha256` (32 bytes) is present in the integrity block |
| 1 | An `AudioSha256` (32 bytes) is present in the integrity block |
| 2–15 | Reserved. Writers MUST write `0`. Readers MUST reject the file if any is set. |

### 3.2 Field semantics

- `AudioDurationMs` and `CreatedUtcUnixMs` are informational. Readers MUST NOT use them to size buffers or make trust decisions. `AudioDurationMs` is checked only against the 10-minute limit (§7). `CreatedUtcUnixMs` has no validation.
- `Reserved0`, `Reserved1`, `Reserved2` exist so a future version can assign them. In v1 they MUST be zero on write and readers MUST reject nonzero values.

## 4. Header checksum

- Algorithm: **CRC-32/ISO-HDLC** (the zlib / PNG / Ethernet CRC): polynomial `0x04C11DB7` reflected (`0xEDB88320`), initial value `0xFFFFFFFF`, input and output reflected, final XOR `0xFFFFFFFF`. Check value: CRC of ASCII `123456789` = `0xCBF43926`.
- Input: header bytes `0 … 51` inclusive (52 bytes: everything before the checksum field).
- Stored: `u32` little-endian at offset 52.
- `Reserved2` (bytes 56–63) is **not** covered by the CRC. Readers still reject it if nonzero (§3.2).
- The CRC detects accidental corruption only. It is not a security feature.

## 5. Integrity block

Immediately after the header, starting at offset 64:

1. If `Flags` bit 0: 32 bytes — SHA-256 of the exact image payload bytes.
2. If `Flags` bit 1: 32 bytes — SHA-256 of the exact audio payload bytes.

Present entries appear in that order with nothing between them. `IntegrityBlockLength` = `32 × (number of set bits among bits 0–1)`, i.e. 0, 32 or 64.

SHA-256 detects accidental corruption only. It is **not** authentication: anyone who can edit the file can recompute the digests. See [SECURITY.md](SECURITY.md).

## 6. Payloads and total size

- Image payload: `ImagePayloadLength` bytes of JPEG, starting at offset `HeaderLength + IntegrityBlockLength`.
- Audio payload: `AudioPayloadLength` bytes of M4A, immediately after the image payload.
- Payload bytes are stored exactly as captured or imported. Writers MUST NOT re-encode or strip them as part of saving.

The file length MUST satisfy exactly:

```
FileLength = HeaderLength + IntegrityBlockLength + ImagePayloadLength + AudioPayloadLength
           = 64 + IntegrityBlockLength + ImagePayloadLength + AudioPayloadLength
```

A shorter file is truncated; a longer file has trailing data. Both are rejected.

When the input is a seekable stream rather than a discrete file, **FileLength** is the number of bytes from the stream's current position to its end (`Length - Position`). Readers MUST NOT treat bytes before the current position as part of the ERAF file.

## 7. Limits (v1)

| Limit | Value | Constant |
|---|---:|---|
| Total file size | 256 MiB = 268,435,456 bytes | `MaxFileBytes` |
| Image payload | 64 MiB = 67,108,864 bytes | `MaxImageBytes` |
| Audio payload | 64 MiB = 67,108,864 bytes | `MaxAudioBytes` |
| Recording duration | 10 minutes = 600,000 ms | `MaxAudioDurationMs` |
| Minimum payload | 1 byte each | — |

`MaxFileBytes` cannot be the deciding check in v1 (two 64 MiB payloads plus 128 bytes of header and hashes stay below 256 MiB); it is kept as a defensive bound that a future version may rely on.

Limits are checked against the **claimed** header values before any payload allocation or read. Writers MUST NOT produce a file that violates them. The recorder (application) enforces the duration limit at capture time; the format only rejects a `AudioDurationMs` above the limit.

## 8. Media and codec identifiers (v1)

| Field | Value | Meaning |
|---|---:|---|
| `ImageMediaType` | 1 | `image/jpeg` — JPEG (JFIF/Exif) |
| `AudioMediaType` | 1 | `audio/mp4` — MPEG-4 Part 14 audio-only (`.m4a`) |
| `AudioCodec` | 1 | AAC-LC, RFC 6381 codec string `mp4a.40.2` |

Any other value is rejected in v1. Values `0` and `≥ 2` are reserved for future versions.

### 8.1 Payload signature check **[C1]**

A cheap check that the payload is plausibly the declared type. It is not a decode and does not prove validity.

- Image: the first 3 bytes MUST be `FF D8 FF` (JPEG SOI followed by a marker prefix).
- Audio: bytes 4–7 of the audio payload MUST be ASCII `ftyp` (the first ISO-BMFF box type). This requires `AudioPayloadLength ≥ 8`.

Verifying the codec is AAC-LC inside the M4A is **not** required of the format library; the application's media stack decides whether it can play the audio.

## 9. Validation and rejection

A reader MUST apply these checks **in this order** and stop at the first failure, reporting the listed error. The fixed order makes different implementations report the same error for the same file.

| # | Check | Error |
|---:|---|---|
| 1 | File length ≥ 64 | `TooShort` |
| 2 | `Magic` = `ERAF` | `BadMagic` |
| 3 | `FormatVersion` = 1 | `UnsupportedVersion` |
| 4 | `HeaderLength` = 64 | `BadHeaderLength` |
| 5 | CRC-32 of bytes 0–51 equals `HeaderChecksumCrc32` | `HeaderChecksumMismatch` |
| 6 | `Flags` bits 2–15 are 0 | `UnknownFlags` |
| 7 | `Reserved0`, `Reserved1`, `Reserved2` are all 0 | `ReservedNonZero` |
| 8 | `ImageMediaType` = 1 and `AudioMediaType` = 1 | `UnsupportedMediaType` |
| 9 | `AudioCodec` = 1 | `UnsupportedCodec` |
| 10 | `ImagePayloadLength ≥ 1` and `AudioPayloadLength ≥ 1` | `EmptyPayload` |
| 11 | `ImagePayloadLength ≤ MaxImageBytes`, `AudioPayloadLength ≤ MaxAudioBytes`, `AudioDurationMs ≤ MaxAudioDurationMs` | `LimitExceeded` |
| 12 | `FileLength` ≤ `MaxFileBytes` | `LimitExceeded` |
| 13 | `FileLength` = exact total from §6 (checked arithmetic) | `LengthMismatch` |
| 14 | Payload signatures (§8.1) | `InvalidPayloadSignature` |
| 15 | If flagged, SHA-256 of image then audio payload matches the stored digest | `HashMismatch` |

Notes:

- **Unsupported version (check 3):** the reader MUST fail with `UnsupportedVersion` and MUST NOT interpret any other field, validate the CRC, or partially open the file. A future version may change every field after offset 6.
- Checks 1–13 need only the first 64 bytes and the file length, so they run before reading any payload. A reader MUST NOT allocate buffers based on claimed lengths until checks 10–13 have passed.
- Checks 14–15 read payload bytes. A reader MUST NOT hand payload bytes to a decoder or player until all 15 checks have passed.
- A reader MUST NOT "repair" a file or ignore a failed check.

## 10. Compatibility rules

- **Strict v1:** a v1 reader accepts only files that pass §9. There are no ignorable fields, no optional trailing data, and no "unknown but harmless" bits.
- **Evolution:** any change to the wire format (new flag bit, new media or codec ID, longer header, new field, new limit) requires a new `FormatVersion` and a spec revision approved by the supervisor. A v1 reader will then report `UnsupportedVersion`.
- **Writers** MUST emit exactly the v1 layout with all reserved fields zero and MUST emit `FormatVersion = 1`, `HeaderLength = 64`.
- **File extension:** `.eraf`. No MIME type is registered or claimed by this specification.

## 11. Atomic save (application responsibility)

The format library produces and validates bytes; the application owns file-system safety. A save MUST:

1. Build the complete file in a uniquely named temp file in the **same directory** as the target (for example `name.eraf.partial`, or a unique name).
2. Flush it to disk.
3. Replace the target with `File.Replace`/atomic move only after the temp file is fully written (and, SHOULD, re-validated by reading it back with §9).
4. On any failure, delete the temp file and leave any existing target untouched.

A reader MUST treat a `.partial` leftover like any other file: it fails §9 if incomplete. The application MUST NOT write incomplete audio and report success.

## 12. Privacy

- The container carries no GPS, device identifiers, contact data, file paths or user names of its own.
- Payload bytes are preserved as captured. A JPEG may carry its own Exif metadata (including GPS) and an M4A may carry its own tags; the container does not add, remove or inspect them. Stripping them is a product decision outside v1.

## 13. Clarifications added to the plan sketch

**Approved by Cursor (2026-10-23)** — normative. None changes the header layout in §3. Recorded as [DECISIONS.md](DECISIONS.md) ADR-006.

| ID | Clarification | Where |
|---|---|---|
| C1 | Payload signature check (`FF D8 FF` / `ftyp`) is mandatory for v1 readers. The plan did not specify it. | §8.1, check 14 |
| C2 | Fixed validation order and named error codes so independent readers agree. | §9 |
| C3 | `AudioDurationMs > 600000` is rejected (the plan said only that the recorder enforces the limit). `CreatedUtcUnixMs` is never validated. | §3.2, §7, check 11 |
| C4 | Trailing bytes after the audio payload are rejected (implied by the plan's exact-length rule; stated explicitly). `Reserved2` is explicitly outside the CRC scope. | §4, §6 |

## 14. Worked example

Full byte-level construction recipes, expected parse results and mutation (negative) recipes are in [../fixtures/eraf/RECIPES.md](../fixtures/eraf/RECIPES.md). They were computed independently of the C# writer, so they can be checked by hand or by a script in any language.

Summary of the minimal valid file `valid-minimal` (no hashes, 92 bytes):

| Bytes | Content |
|---|---|
| 0–63 | Header: magic, version 1, header length 64, flags 0, media/codec 1/1/1, image length 8, audio length 20, duration 0, created 0, CRC `7F D1 2A 8C`, 8 zero bytes |
| 64–71 | Image payload `FF D8 FF E0 00 02 FF D9` (synthetic, container-conformance only) |
| 72–91 | Audio payload `00 00 00 14 66 74 79 70 4D 34 41 20 00 00 00 00 4D 34 41 20` (a bare `ftyp` box, synthetic) |
