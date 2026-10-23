# ERAF Testing Strategy

Complements [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) §9. Evidence rule: a milestone is incomplete without the commands run and the observed results recorded in [STATUS.md](STATUS.md). A test that was not executed and inspected is not evidence.

## Layers

| Layer | Where | Covers | Introduced |
|---|---|---|---|
| Format unit | `tests/Eraf.Format.Tests` | Header parse/serialize, CRC-32 (incl. the `123456789` check value), SHA-256 handling, length arithmetic and overflow, limits, each §9 check in isolation | M2 |
| Format negative | same | Every negative recipe N01–N21 in `fixtures/eraf/RECIPES.md`, plus empty, random bytes, truncated at every header offset | M2 |
| Format round-trip | same | Writer → reader returns byte-identical payloads; with and without hashes; limit boundaries (exact max passes, max+1 fails) built in memory | M2 |
| Fixture conformance | same, reading `fixtures/eraf` | Reader result equals the sidecar for every fixture; writer output equals valid fixtures byte for byte | M2 |
| Workflow | app tests or a UI-free workflow assembly | State-machine transitions, cancel/discard cleanup, "incomplete audio is never saved" | M5 |
| Save/open integration | tests with real temp files | Atomic replace leaves the original intact when the write fails; export returns the original payload bytes | M6 |
| Manual UI | checklist in STATUS evidence | Capture/import, record/replay/discard, open, denied mic, missing camera, corrupt file, unsaved-work prompt, keyboard use | M3–M8 |

## Fixture rules

- Golden fixtures in `fixtures/eraf` are shared with the future Android implementation; see [../fixtures/eraf/README.md](../fixtures/eraf/README.md) for naming, sidecars and how tests consume them.
- Expected values come from the spec and `RECIPES.md`, never from the code under test.
- At least one valid fixture is assembled outside the C# writer, so a writer bug and a matching reader bug cannot cancel out.
- Negative tests assert the **exact** error code from spec §9, not just "failed".
- Large inputs (limit tests) are generated in memory, not checked in.

## Running tests

From the repo root:

```powershell
dotnet test tests\Eraf.Format.Tests\Eraf.Format.Tests.csproj
```

Constraint from M0: in this environment, run project-level commands with output redirected to a file; piped output and solution-level restore have hung. Last recorded result (M2): 48 passed, 0 failed ([STATUS.md](STATUS.md)). The tests locate `fixtures/eraf` by walking up from the build output to `Eraf.sln`, so run them from inside the repo.

## Not tested automatically (state this, do not imply otherwise)

- Real camera and microphone behavior, MediaCapture M4A encoding, and MediaPlayer playback are verified manually on hardware until a UI test harness is justified.
- Whether an AAC-LC stream inside an M4A actually decodes is the media stack's job, not the format library's (spec §8.1).
