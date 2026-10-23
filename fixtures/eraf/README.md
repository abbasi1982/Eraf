# fixtures/eraf — golden conformance vectors

Shared by every ERAF implementation (C# now, Kotlin in Phase 2). A fixture is the **specification made executable**: if an implementation and a fixture disagree, the spec ([../../docs/ERAF_FORMAT_SPEC.md](../../docs/ERAF_FORMAT_SPEC.md)) decides, and the loser is fixed.

Byte-level definitions are in [RECIPES.md](RECIPES.md). The binary files and sidecars are checked in and consumed by `tests/Eraf.Format.Tests`.

## Status

| Item | State |
|---|---|
| Recipes for 2 valid + 21 negative cases | Done (M1) |
| Binary `.eraf` files + `expected/` sidecars (2 valid, 21 invalid) | Done (M2) |
| Decodable JPEG/M4A payload fixtures | M2/M3 |

## Naming

```
fixtures/eraf/
  valid/      valid-<description>.eraf
  invalid/    invalid-<ID>-<error-in-kebab-case>.eraf    e.g. invalid-N06-header-checksum-mismatch.eraf
  expected/   <fixture-name>.json                        expected result for every file above
  RECIPES.md
```

- Valid files are named for what they exercise (`valid-minimal`, `valid-hashes`).
- Negative files carry the recipe ID from `RECIPES.md` so a failing test points at the recipe.
- Never reuse a name for different bytes. Add a new fixture instead.

## Expected-result sidecar

Each fixture has `expected/<name>.json`:

```json
{ "fixture": "invalid/invalid-N06-header-checksum-mismatch.eraf",
  "valid": false,
  "error": "HeaderChecksumMismatch" }
```

and for valid files:

```json
{ "fixture": "valid/valid-hashes.eraf",
  "valid": true,
  "imageLength": 8, "audioLength": 20,
  "audioDurationMs": 1500, "createdUtcUnixMs": 1700000000000,
  "hasImageSha256": true, "hasAudioSha256": true,
  "imageSha256": "499af7b9…", "audioSha256": "f2b4fd7e…" }
```

Error names are exactly the codes in spec §9, so the C# and Kotlin suites assert the same strings.

## How conformance tests consume fixtures

1. Enumerate `valid/` and `invalid/`; for each `.eraf`, load the sidecar.
2. Run the reader/validator. For `valid: true`, assert the listed fields and that the returned payloads are byte-identical to the recipe payloads. For `valid: false`, assert the validator reports **exactly** `error`, not just "some failure".
3. For valid fixtures, also check the writer: build from the same inputs and assert the output bytes equal the fixture file byte for byte.
4. Expected values come from RECIPES.md, never from the code under test. At least one valid fixture (`valid-minimal`) must be hand-assembled or script-assembled outside the C# writer so writer and reader bugs cannot hide each other.

Android later copies or references this directory and runs the same steps in its own test framework.

## Rules

- Fixtures are binary. `.gitattributes` in this directory marks `*.eraf` and `*.jpg`/`*.m4a` as non-text so Git never applies CRLF conversion; keep it.
- Keep fixtures small. Large-payload limit tests are generated in memory by unit tests, not checked in.
- Fixtures contain no personal data. Use synthetic or purpose-made media only.
- Any format change needs a spec revision and version bump first, then fixtures.
