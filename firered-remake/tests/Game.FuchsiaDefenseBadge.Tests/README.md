# Fuchsia fifth-badge Defense boundary

This focused Domain suite verifies the explicit player-side `DefenseBadgeBoost` in
107-species / 354-authenticated-move source battles. False values are omitted from
JSON. The 107-species / 244-move historical Fuchsia profile rejects a true flag,
and opponents reject it without events, RNG or state mutation. Application badge
award and new-profile setup belong to the separate gym integration.

The damage calculation applies `floor(Defense × 110 / 100)` to raw physical
Defense before Metal Powder, Explosion's halving and stat stages. It does not
change persistent Pokemon stats, special Defense or fixed/counter damage.
Transform retains the owning side's flag and copies unboosted stats. Real source
party switches preserve it through voluntary replacement, Baton Pass, Roar and
fainted replacement. Doom Desire records physical Defense at cast; Future Sight
uses special Defense. Later replacement preserves the already calculated damage.

Confusion calls `CalculateBaseDamage(self, self, MOVE_POUND, ..., 40, ...)` in the
source. Its fifth-badge Defense boost therefore also applies. Within the new
107/354 source boundary, the helper observes Metal Powder and the source's
`gCurrentMove` Explosion check before stages. The latter still applies when the
confused actor had selected Selfdestruct. These changes are gated away from the
historical 107/244 profile. Marvel Scale's position was reviewed, but no admitted
species supplies that ability; this suite does not claim expanded ability support.

## Independent source evidence

`generate-source-vectors.py` reads pinned pret/pokefirered commit
`037335f4c725d7c9aecdac87066f2002b4bd7e14` through `git show`. It extracts
`CalculateBaseDamage`, its stage-ratio table, held-type table and badge macro
unchanged, and compiles them with a local test harness and the pinned constants.
No scripts from the reference repository are executed. Temporary compiler inputs
are removed afterward. `--check` compares with the checked-in fixture and cannot
rewrite it.

The 2,688 C vectors vary integer-edge Defense values, stages, badge, critical hit,
burn, Reflect, Metal Powder, Explosion and special-move controls. They compare the
source function's exact base-damage result against the product's `plus-two`
boundary. Source file hashes and extracted-function hash are in
`source-vectors.json`. The source order is pokemon.c:2442 (badge), 2476 (Metal
Powder), 2490 (Marvel Scale), 2505 (Explosion), then 2527 (Defense stages).
Confusion's call is battle_util.c:1424; delayed calculation is
battle_script_commands.c:8555.

## Reproduce

From the repository root, with the restored official toolchain configuration:

```sh
python3 tests/Game.FuchsiaDefenseBadge.Tests/verify.py /absolute/path/to/pokefirered
```

The verifier uses `/tmp/firered-remake-build.lock`, captures SDK/runtime versions,
checks the source fixture, then builds and runs this suite plus the historical
Transform, source party-effects and source volatile-effects regression suites.
`evidence/commands.json` records exact commands and exit codes;
`evidence/manifest.json` binds the tested integration seam and focused files.

The suite is deliberately a controlled Domain check, not a continuous campaign
lineage, badge-award test, visual check or audio acceptance. Initial harness
preparation exposed missing transitive C headers, and the first C# test build
needed one nullable annotation; both were corrected before the recorded final
verification. The extracted C function retains its original unused local warning.
