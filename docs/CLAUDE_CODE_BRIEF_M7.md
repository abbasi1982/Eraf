# Claude Code Brief — Milestone M7 (Hardening: permissions, errors, security)

**Authorized by:** Cursor (supervisor)  
**Implementer:** Claude Code  
**Prerequisite:** M6 closed, committed, and pushed with M5.  
**Focus:** Make failure modes and security posture production-ready without changing the wire format.

## Goal

Harden denied/missing devices, save/open edge cases, temp cleanup, and user-facing errors so Phase 1 can approach the Windows release gate.

## Required work

1. **Permissions / devices**
   - Ensure camera-denied, mic-denied, no-camera, no-mic paths show actionable messages (code exists; exercise safely if possible, or document why not).
   - Import must remain available when camera/mic fail.
2. **File / disk edge cases**
   - Read-only folder / access-denied save: clear message, `Error` state, no partial left.
   - Prefer deleting orphaned `*.partial` next to a chosen save path when safe (same stem / app-written pattern only—never delete unrelated files).
3. **Error UX**
   - Review InfoBar severity (limit auto-stop vs true errors).
   - Incomplete-JPEG warning wording/placement polish (ADR-008 / ADR-011).
4. **Security pass (docs + code)**
   - Confirm no logging of payload bytes or user paths beyond what STATUS already allows.
   - Update `SECURITY.md` with any new deps; note remaining license-ID gap for M9.
   - Reaffirm: hashes ≠ authentication; untrusted `.eraf` always full §9.
5. **Regression**
   - Full `dotnet test`; `winapp run` smoke: import → record → save → open → export; open 2–3 invalid fixtures.
6. Update STATUS.md with evidence and explicit unverified items.

## Out of scope

- Full accessibility (M8)
- Android
- Format changes

## Exit criteria

- [ ] Denied/missing device messages present and documented (exercised or explicitly not)
- [ ] Save failure paths leave originals intact; partial cleanup improved if feasible
- [ ] SECURITY.md current
- [ ] Full test suite green; STATUS evidence
- [ ] Commit after Cursor review

## Report back to Cursor

What was exercised vs code-only, cleanup behavior, test results, residual risks.
