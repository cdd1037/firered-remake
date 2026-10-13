# Current composition test scope after the League rematch

`tests/Game.CurrentComposition.Tests` tests the expanded current game: 257 maps,
139 player-owned species, 168 opponent species and 244 field pickups. It verifies
the three authenticated Cave source maps, the final ownership/opponent/machine
proofs and their Celio predecessors, the seven upgraded teams and five additional opponent-only species, deterministic independent source loads,
current-only startup, strict saves/Continue, and refusal of malformed bindings.
New Game and Restore must reject the same tampered composition.

`baseline-a52074b.json` and the existing refactor reports remain unchanged. Their
ContentVersion describes the pre-Cave composition. That fingerprint is not the
expected fingerprint of the expanded game, and the earlier byte-equivalence
results are not evidence that the two content sets are identical. The retained
baseline assertions cover the unchanged seeded New Game payload and, when
explicitly supplied, the two exact original checkpoint payloads and one public
movement command at generations 47785 and 47819.

The optional four-argument test mode first matches each original checkpoint's
complete file SHA256 against that immutable baseline. It reads the envelope's
ContentVersion, requires the baseline predecessor fingerprint, then performs a
strict Celio decode using that fingerprint and current semantic validation. It
also proves that current strict loading rejects both the original envelope and
a current-rules envelope carrying the predecessor content fingerprint.

Only after unchanged-payload, generation, Restore and movement checks pass does
the test write a current envelope to a new destination. Source-as-target and
existing-target attempts are refused; original files are checked byte-for-byte.
This is an explicit test-local compatibility exercise, not an automatic save
migration or verification of the product's Tooling conversion command.

Run only this project against the already-built current dependencies:

```sh
source scripts/env.sh
flock /tmp/firered-remake-build.lock dotnet build \
  tests/Game.CurrentComposition.Tests/Game.CurrentComposition.Tests.csproj \
  --no-restore -p:BuildProjectReferences=false -p:UseSharedCompilation=false \
  -m:1 -nr:false --nologo
dotnet tests/Game.CurrentComposition.Tests/bin/Debug/net10.0/Game.CurrentComposition.Tests.dll \
  "$PWD" /tmp/current-composition-new-output
```

For the optional checkpoint exercise, insert the original `handoff-scene6.json`
and `bill-fact.json` from
`tests/Game.CelioCampaign.Tests/evidence/integrated-run01/` between the project
root and output directory. Use a fresh output directory for each run. Historical
regional suites, complete route replays, native presentation and audio acceptance
are outside this focused test.

The rematch increment passed the 80-check two-argument mode, then the final133-check mode with53 original/rematch/early/Silph registry identity comparisons against the frozen current dependencies. This batch did not repeat the optional original-checkpoint conversion exercise; the actual rematch route independently converted the exact Cave generation48713 checkpoint, completed the fights and tested a new-process strict Continue.
