# Claude Code Brief — Milestone M2 (`Eraf.Format` + tests)

**Authorized by:** Cursor (supervisor)  
**Implementer:** Claude Code  
**Prerequisite:** M1 closed. Spec clarifications **C1–C4 approved** (see [DECISIONS.md](DECISIONS.md) ADR-006).  
**Normative input:** [ERAF_FORMAT_SPEC.md](ERAF_FORMAT_SPEC.md), [fixtures/eraf/RECIPES.md](../fixtures/eraf/RECIPES.md)

## Goal

Implement a pure `net8.0` library that reads, writes, and validates ERAF v1 exactly per the spec, with automated tests that consume the golden recipes (including all 21 negative cases).

## Required work

1. Add `src/Eraf.Format/` (`net8.0`, no WinRT/UI references).
2. Add `tests/Eraf.Format.Tests/` (xUnit), referenced from `Eraf.sln`.
3. Implement (names may vary; keep UI-free):
   - Limits constants matching spec §7
   - CRC-32/ISO-HDLC as defined in spec §4 (must match `0xCBF43926` for `123456789`)
   - Ordered validator per spec §9 with the **exact** error code names
   - Reader returning document (payloads + metadata) or typed failure
   - Writer that always produces files the reader accepts
   - Writers MUST refuse to emit: empty payloads, `AudioPayloadLength < 8`, `ImagePayloadLength < 3`, duration `> 600000`, unsupported IDs, nonzero reserved/unknown flags
4. Emit/check fixture binaries from `RECIPES.md` (either check in `.eraf` under `fixtures/eraf/` built from recipes, or generate in test setup from the hex — prefer **checked-in binaries** plus a test that rebuilds from hex and byte-compares, so Git + `.gitattributes` protect them).
5. Cover at minimum:
   - Round-trip write → read (image/audio bytes identical)
   - `valid-minimal` and `valid-hashes` parse OK
   - N01–N21 each fail with the **exact** expected error (first failing check)
   - Overflow / huge claimed length (N17) allocates nothing oversized
6. Update [STATUS.md](STATUS.md) with commands and results.
7. Do **not** change the wire format. If the spec is wrong, stop and ask Cursor.

## Out of scope

- WinUI features, camera, audio capture
- Real decodable JPEG/M4A fixtures for playback (may add a note; optional tiny real files only if needed for writer smoke — not required)
- Android / Kotlin
- Fixing `dotnet run`

## Exit criteria (Cursor will verify)

- [ ] `dotnet test` on `Eraf.Format.Tests` passes with captured output
- [ ] Library has no Windows-only TFM / WinUI refs
- [ ] All N01–N21 expectations match the spec
- [ ] STATUS.md updated; no silent format drift
- [ ] Commit authorized after Cursor review of this milestone (ask before push if unsure)

## Report back to Cursor

Files changed, public API sketch, test commands + results, any ambiguity found in the spec, risks.
