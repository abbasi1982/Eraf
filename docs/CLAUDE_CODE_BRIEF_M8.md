# Claude Code Brief — Milestone M8 (Accessibility and UI polish)

**Authorized by:** Cursor (supervisor)  
**Implementer:** Claude Code  
**Prerequisite:** M7 closed and committed.  
**Focus:** Keyboard access, screen-reader labels, and restrained polish—no redesign of unrelated chrome.

## Goal

Make the main Windows flow usable with keyboard and basic screen-reader support, with clear primary/secondary actions and consistent terminology.

## Required work

1. **Keyboard**
   - Tab order through Idle → import/camera → preview → record → play → save/open/export → Start over.
   - Enter/Space activate focused buttons; Esc cancels dialogs / camera where sensible.
   - Focus lands usefully after state changes (e.g. after import, after stop recording).
2. **Screen reader**
   - `AutomationProperties.Name` (and HelpText where useful) on all interactive controls and key status texts (recording indicator, saved/not saved, InfoBar).
   - Do not rely on color alone for recording/saved state.
3. **Dialogs**
   - Unsaved-work ContentDialog: accessible names, default/cancel buttons clear.
4. **Polish (small)**
   - Consistent labels: “Import image”, “Use camera”, “Record voice note”, “Save ERAF file”, “Open ERAF file”, “Export photo”, “Export voice note”.
   - Touch targets / spacing reasonable if cheap; no decorative animation focus.
5. Manual checklist in STATUS.md (keyboard walk + Narrator smoke if available).
6. Keep `dotnet test` green; no format changes.

## Out of scope

- Full Narrator certification suite
- Visual redesign / theming overhaul
- Android
- M9 release packaging

## Exit criteria

- [ ] Full primary workflow operable by keyboard alone
- [ ] Controls have automation names
- [ ] STATUS checklist completed (what was / wasn’t verified with Narrator)
- [ ] Tests still green
- [ ] Commit after Cursor review

## Report back to Cursor

Files changed, keyboard/Narrator evidence, residual gaps.
