# Claude Code Brief — Phase 2 planning only (no Android implementation yet)

**Authorized by:** Cursor (supervisor)  
**Implementer:** Claude Code  
**Prerequisite:** M9 committed; Windows Phase 1 gate **passed for Phase 2 planning** (see STATUS).  
**Hard rule:** Do **not** create an Android app, Kotlin sources, or Gradle project until the product owner explicitly authorizes Phase 2 implementation after reviewing this plan.

## Goal

Produce an Android Phase 2 plan that reuses the frozen ERAF v1 spec and golden fixtures, aligned with Windows behavior—planning docs only.

## Required deliverables

1. `docs/PHASE2_ANDROID_PLAN.md` covering:
   - Proposed module layout (`eraf-format` Kotlin library + Compose app)
   - CameraX + MediaRecorder AAC/MPEG-4 mapping to Windows media choices
   - SAF / document picker for open/save/export
   - Permission and lifecycle risks (rotation, backgrounding, audio focus, process death)
   - Conformance: consume `fixtures/eraf` + RECIPES.md; same §9 error codes
   - Milestone breakdown comparable to Windows M0–M9
   - Tooling assumptions to verify (JDK, Android SDK — `ANDROID_HOME` was unset at Phase 1 audit)
2. Update `docs/DECISIONS.md` with a pending ADR stub for Phase 2 start (blocked on PO approval).
3. Update STATUS.md: Phase 2 = planning; implementation blocked.

## Out of scope

- Any Android source tree or dependency downloads beyond documenting required tooling
- Changing the ERAF wire format
- Claiming Windows store readiness / signing

## Exit criteria

- [ ] Plan reviewable by Cursor and product owner
- [ ] Explicit “do not implement until PO says so” in the plan
- [ ] Commit after Cursor review of the plan docs
