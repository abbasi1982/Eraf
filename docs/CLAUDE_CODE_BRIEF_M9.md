# Claude Code Brief — Milestone M9 (Windows Phase 1 release gate)

**Authorized by:** Cursor (supervisor)  
**Implementer:** Claude Code  
**Prerequisite:** M8 closed, committed, and pushed with `a332282` (M7).  
**Goal:** Prove Phase 1 acceptance criteria with evidence, then stop for Cursor’s Windows release-gate review. **Do not start Android.**

## Required work

1. **Regression package**
   - `dotnet test` (full suite) — capture pass count.
   - `dotnet build` Release x64 for `Eraf.Windows` — 0 warnings/errors preferred.
   - `winapp run` (or Release deploy path) smoke: import → record → save → open → play → export photo → export audio; open 2–3 invalid fixtures; Start over / unsaved dialog once.
2. **Acceptance checklist** in STATUS.md mapping each Phase 1 criterion from `IMPLEMENTATION_PLAN.md` §10 / product brief §6.3 to **Pass / Fail / Not verified**, with evidence pointers.
3. **Known limitations** consolidated (dotnet run, Narrator not heard, cam/mic deny unexercised, orphan partial rules, license IDs, etc.).
4. **SECURITY.md**: fill exact license identifiers for current NuGet packages if obtainable from package metadata; else mark explicitly still open with how to finish.
5. **USER_GUIDE.md** (short): create/open/export `.eraf` for a nontechnical user; mention camera/mic Settings paths.
6. **Dependency inventory** in SECURITY.md or a short `docs/DEPENDENCIES.md`.
7. No new features unless a gate blocker is found; fix only blockers Cursor would reject.

## Out of scope

- Android / Phase 2
- Marketing site, store listing, code signing beyond what’s already required to run unpackaged/debug
- Claiming Narrator certification without a real Narrator session

## Exit criteria

- [ ] Full test suite green with captured output
- [ ] Release build succeeds
- [ ] Smoke checklist completed
- [ ] Acceptance table + limitations + user guide + deps/licenses as far as possible
- [ ] STATUS marked “awaiting Cursor Windows release gate”
- [ ] Commit after Cursor review of this milestone package

## Report back to Cursor

Commands + results, acceptance table summary, residual risks, recommendation whether the Windows gate should open for Phase 2 planning.
