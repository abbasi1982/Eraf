# Claude Code Brief — Milestone M1 (Format spec + governance docs)

**Authorized by:** Cursor (supervisor)  
**Implementer:** Claude Code  
**Prerequisite:** M0 closed. Complete [CLAUDE_CODE_BRIEF_M0_FOLLOWUP_NAMESPACE.md](CLAUDE_CODE_BRIEF_M0_FOLLOWUP_NAMESPACE.md) first if not already done.  
**Scope:** Documentation and fixture policy only. No `Eraf.Format` implementation code (that is M2).

## Goal

Publish the normative ERAF binary format specification and the remaining Phase 1 governance docs so M2 can implement against a frozen v1 wire format.

## Required documents

Create or complete under `docs/`:

1. **`ERAF_FORMAT_SPEC.md`** (normative authority) — must define:
   - Magic `ERAF`, little-endian layout, 64-byte header field table (from [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) §5)
   - Flags, reserved fields, CRC-32 scope, optional SHA-256 blocks
   - Payload order (JPEG then M4A), exact total-size equation
   - Media/codec IDs for v1 (JPEG; audio/mp4 + AAC-LC / mp4a.40.2)
   - Size/duration limits
   - Validation / rejection rules; unsupported-version behavior
   - Atomic-save expectations (app responsibility, referenced here)
   - Compatibility rules for unknown bits/fields
   - At least one independently verifiable fixture description (hex or construction recipe) under `fixtures/eraf/` — a **hand-specified** minimal valid file recipe is enough in M1; binary may be generated in M2 if easier, but the recipe must be precise enough to build without the C# writer
2. **`ARCHITECTURE.md`** — Windows Phase 1 module boundaries (Format vs Windows app vs tests)
3. **`TESTING.md`** — unit/negative/interop fixture strategy; note golden fixtures shared with future Android
4. **`SECURITY.md`** — untrusted input, limits, temp files, no telemetry; hash ≠ authentication
5. **`DECISIONS.md`** — ADR-style entries for: binary container; AAC-LC/M4A; WASDK 2.5.1; dual native format libs; `winapp run` as documented Windows launch path
6. Update **`STATUS.md`** and **`README.md`** links to the new docs
7. **`fixtures/eraf/README.md`** — naming conventions and how conformance tests will consume fixtures

## Out of scope

- Implementing `Eraf.Format` reader/writer (M2)
- UI, camera, audio features
- Android
- Fixing `dotnet run` registration hook

## Exit criteria (Cursor will verify)

- [ ] Spec matches the approved header sketch unless a documented, reviewed correction is required
- [ ] Spec is precise enough for an independent Kotlin reader later
- [ ] Governance docs exist and do not contradict the plan
- [ ] No application feature code added beyond docs/fixtures policy
- [ ] STATUS.md records M1 complete with file list

## Report back to Cursor

Files changed, any format clarifications needing approval, commands run (if any fixture bytes were produced), risks.
