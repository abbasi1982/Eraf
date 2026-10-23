# ERAF golden fixture recipes (v1)

Normative companion to [../../docs/ERAF_FORMAT_SPEC.md](../../docs/ERAF_FORMAT_SPEC.md). Each recipe is precise enough to build the file by hand or with a script in any language, without the C# writer. All values were computed and cross-checked with an independent reference validator written outside the C# code base (see "Provenance").

Payloads below are **synthetic**: they satisfy the container rules and the §8.1 signature check but are not decodable media. They exist to test the container. Decodable JPEG/M4A fixtures for playback tests are separate and arrive with M2/M3.

## Shared payloads

| Name | Bytes (hex) | Length |
|---|---|---:|
| `IMG` | `FF D8 FF E0 00 02 FF D9` | 8 |
| `AUD` | `00 00 00 14 66 74 79 70 4D 34 41 20 00 00 00 00 4D 34 41 20` | 20 |

`AUD` is one bare ISO-BMFF `ftyp` box: size `0x14`, type `ftyp`, major brand `M4A `, minor version 0, one compatible brand `M4A `.

SHA-256 digests:

- `SHA256(IMG)` = `499af7b940c9579b2062696bdabd7b849ac18c9246e6f5abf6c5a52d4de43850`
- `SHA256(AUD)` = `f2b4fd7e42d3d8fea2eb46afd725fc51964c6ba7834d0585ca78c6547fd239b2`

## `valid-minimal.eraf` — no hashes, 92 bytes

Header fields: version 1, header length 64, flags 0, reserved 0, image/audio media type 1/1, codec 1, `ImagePayloadLength` 8, `AudioPayloadLength` 20, `AudioDurationMs` 0, `CreatedUtcUnixMs` 0, `Reserved2` zero.

CRC-32 over bytes 0–51 = `0x8C2AD17F`, stored little-endian as `7F D1 2A 8C`.

Header (bytes 0–63):

```
45 52 41 46 01 00 40 00 00 00 00 00 01 00 01 00
01 00 00 00 08 00 00 00 00 00 00 00 14 00 00 00
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
00 00 00 00 7F D1 2A 8C 00 00 00 00 00 00 00 00
```

Then `IMG` (bytes 64–71), then `AUD` (bytes 72–91). Full file, one line:

```
455241460100400000000000010001000100000008000000000000001400000000000000000000000000000000000000000000007FD12A8C0000000000000000FFD8FFE00002FFD900000014667479704D344120000000004D344120
```

Expected parse: valid; image = `IMG`, audio = `AUD`, duration unknown, created unknown, no hashes.

## `valid-hashes.eraf` — both hashes, all informational fields set, 156 bytes

Header fields: as above, but flags `0x0003`, `AudioDurationMs` 1500 (`DC 05 00 00 00 00 00 00`), `CreatedUtcUnixMs` 1700000000000 = `0x0000018BCFE56800` (`00 68 E5 CF 8B 01 00 00`).

CRC-32 over bytes 0–51 = `0x5C5C7DBE`, stored as `BE 7D 5C 5C`.

Header (bytes 0–63):

```
45 52 41 46 01 00 40 00 03 00 00 00 01 00 01 00
01 00 00 00 08 00 00 00 00 00 00 00 14 00 00 00
00 00 00 00 DC 05 00 00 00 00 00 00 00 68 E5 CF
8B 01 00 00 BE 7D 5C 5C 00 00 00 00 00 00 00 00
```

Then bytes 64–95 `SHA256(IMG)`, bytes 96–127 `SHA256(AUD)`, bytes 128–135 `IMG`, bytes 136–155 `AUD`. Full file:

```
455241460100400003000000010001000100000008000000000000001400000000000000DC050000000000000068E5CF8B010000BE7D5C5C0000000000000000499AF7B940C9579B2062696BDABD7B849AC18C9246E6F5ABF6C5A52D4DE43850F2B4FD7E42D3D8FEA2EB46AFD725FC51964C6BA7834D0585CA78C6547FD239B2FFD8FFE00002FFD900000014667479704D344120000000004D344120
```

Expected parse: valid; duration 1500 ms; created 1700000000000 ms (2023-11-14T22:13:20Z); both hashes verified.

## Negative recipes

Each is a byte-level edit of a base file, "CRC recomputed" meaning bytes 52–55 are set to the CRC-32 of the edited bytes 0–51. Offsets are absolute and zero-based. The expected error is the first failing check of spec §9 and is the only acceptable result.

| ID | Base | Edit | Expected error |
|---|---|---|---|
| N01 | — | empty file (0 bytes) | `TooShort` |
| N02 | V1 | keep only the first 63 bytes | `TooShort` |
| N03 | V1 | byte 0 = `58` | `BadMagic` |
| N04 | V1 | `FormatVersion` (offset 4, u16) = 2; CRC not recomputed | `UnsupportedVersion` |
| N05 | V1 | `HeaderLength` (offset 6, u16) = 65; CRC not recomputed | `BadHeaderLength` |
| N06 | V1 | `ImagePayloadLength` (offset 20, u64) = 9; CRC **not** recomputed | `HeaderChecksumMismatch` |
| N07 | V1 | `Flags` (offset 8, u16) = `0x0004`; CRC recomputed | `UnknownFlags` |
| N08 | V1 | `Reserved0` (offset 10, u16) = 1; CRC recomputed | `ReservedNonZero` |
| N09 | V1 | byte 56 = `01` (`Reserved2`, outside the CRC); no recompute | `ReservedNonZero` |
| N10 | V1 | `ImageMediaType` (offset 12, u16) = 2; CRC recomputed | `UnsupportedMediaType` |
| N11 | V1 | `AudioCodec` (offset 16, u16) = 2; CRC recomputed | `UnsupportedCodec` |
| N12 | V1 | `ImagePayloadLength` = 0; CRC recomputed | `EmptyPayload` |
| N13 | V1 | `ImagePayloadLength` = 67,108,865 (64 MiB + 1); CRC recomputed | `LimitExceeded` |
| N14 | V1 | `AudioDurationMs` (offset 36, u64) = 600,001; CRC recomputed | `LimitExceeded` |
| N15 | V1 | remove the last byte (91 bytes) | `LengthMismatch` |
| N16 | V1 | append one `00` byte (93 bytes) | `LengthMismatch` |
| N17 | V1 | `ImagePayloadLength` = `0xFFFFFFFFFFFFFFFF`; CRC recomputed | `LimitExceeded` (no overflow, no allocation) |
| N18 | V1 | byte 64 (first image byte) = `00` | `InvalidPayloadSignature` |
| N19 | V1 | byte 79 = `78` (audio `ftyp` becomes `ftyx`) | `InvalidPayloadSignature` |
| N20 | V2 | byte 135 (last image byte) XOR `FF` | `HashMismatch` |
| N21 | V2 | byte 155 (last audio byte) XOR `FF` | `HashMismatch` |

`V1` = `valid-minimal`, `V2` = `valid-hashes`.

Not constructible as a small file and therefore covered by unit tests only: a file of more than 256 MiB (spec §9 check 12; unreachable in v1 once per-payload limits pass, see spec §7).

## Provenance

The values above were produced by a throwaway Python reference (header packing with `struct`, CRC with `zlib.crc32`, digests with `hashlib.sha256`) that also implements the §9 checks. Running every recipe through it produced exactly the expected errors above and `OK` for both valid files. The CRC routine reproduces the standard check value (`CRC-32("123456789") = 0xCBF43926`). M2 must reproduce these byte sequences from `Eraf.Format` and must not generate its expected values from its own writer.
