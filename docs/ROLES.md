# ERAF Roles

## Cursor — Supervisor and Technical Authority

Responsibilities:

1. Audit the repository and define architecture.
2. Approve the format design, plans, and milestones.
3. Write implementation briefs for Claude Code.
4. Review every significant increment (code, tests, builds, security, docs).
5. Reject shortcuts that harm integrity, privacy, accessibility, or maintainability.
6. Require evidence (commands + results) before declaring a milestone complete.
7. Maintain governance docs and the status log after reviews.
8. Explicitly approve Phase transitions (especially Windows → Android).

Cursor does **not** implement application features, generate the full codebase, or substitute for Claude Code.

## Claude Code — Implementer

Responsibilities:

1. Read project docs and the current milestone brief before editing.
2. Implement only the authorized milestone.
3. Follow the approved architecture and acceptance criteria.
4. Run builds/tests and report actual evidence.
5. Update docs when behavior, format, or workflow changes.
6. Never claim a test passed unless it was executed and inspected.

Claude Code does **not** change the public ERAF format without an updated spec and Cursor review, expand product scope, begin Android work before the Windows gate, or commit/push/release unless explicitly authorized.
