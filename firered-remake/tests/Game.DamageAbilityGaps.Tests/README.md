# Bounded damage ability arithmetic

Candidate base: `71085eefb6f6d22d312d6e817547f30b1d1f94a6`.
Reference: pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`.
Only product change: `ForestBattleDamage.cs`.

The existing authenticated `sourceDefinition` contract enables the new arithmetic
for both ordinary source selections and called source moves. Historical damage
entry points without this contract retain their existing arithmetic. There is no
new catalog-count protocol and no change to species or ability admission.

`source-contract.json` records ordering. Huge/Pure Power precedes the attack
badge's integer division. Hustle follows existing badge/item modifiers and precedes
Guts. Marvel Scale follows existing defense badge/item modifiers, then Explosion
and stat stages consume the modified Defense. All individual truncations remain
separate. Critical hits retain these abilities and bypass only the source-defined
stat stages. Traditional FireRed Explosion, burn, screens and held-item math are
unchanged. Hustle accuracy code is untouched.

## Verification

Run from the independent candidate or an integrated repository:

```sh
bash tests/Game.DamageAbilityGaps.Tests/run.sh CONTENT_REPOSITORY_ROOT PINNED_REFERENCE_ROOT NEW_OUTPUT_DIRECTORY
```

The output directory must not exist; earlier evidence is preserved. The run script uses the project's official `scripts/env.sh`, restores outside the
build lock, holds `/tmp/firered-remake-build.lock` only for the focused compilation,
and executes the test program outside the lock. No solution or GUI build occurs.

The C oracle reads and checks clean pinned source, extracts the complete
`CalculateBaseDamage` function with its exact stat/badge macros, then compiles that
text with reviewed bounded dependency stubs. These stubs represent singles with
no weather, sport or ally effects and only the selected item cases. The source
function itself is not rewritten. One original unused local requires GCC's
`-Wno-unused-but-set-variable`; all other enabled warnings are errors.

Actual result: **6 groups, 161 assertions, 33 C-oracle numeric vectors, 5 source
full turns, zero failures**, official SDK **10.0.401**, runtime **10.0.12**.
The final evidence is in `evidence/result.json`, `source-vectors.json`,
`source-build-run.log`, `build-final.log`, and `run-final.log`.

- Numeric vectors cover separate badge/item rounding, positive/negative stages,
  normal/critical damage, burn/Reflect, Marvel Scale's healthy and eight status
  registers, ordinary physical/special moves and Explosion's traditional halving.
- All 33 vectors also check legacy isolation or the existing unsupported-move
  rejection. The independent predecessor Tackle oracle remains **13** damage.
- Authentic Clefairy and PID-selected Guts Rattata complete Role Play, Tackle,
  Nature Power→Swift, Skill Swap and a subsequent selected Tackle. Tackle and Swift
  match the extracted C oracle at **16** and **30** actual HP loss. Chosen PP,
  called-move history, both-side execution, serialization, and party/permanent
  ability retention are checked.
- Unauthenticated source move metadata and each of the four fabricated overlays
  fail without changing HP, PP, status, RNG, revision or party state; failure events
  and random traces remain empty.

## Explicit acceptance boundary

The authentic 107-species catalog contains no Huge Power, Pure Power, Hustle or
Marvel Scale donor. Positive new-ability vectors are controlled low-level Domain
register fixtures using temporary overlays and explicit stats. They do not claim
valid battle admission and never modify `PokemonInstance.AbilityId`. The five
authentic turns prove the existing Guts provenance/shared execution route; they
are **not new-ability full-turn acceptance**. Central source-authenticated donor
admission remains necessary for those four abilities.

No campaign content, permanent ability, accuracy handler, source badge ordering,
screens, held-item arithmetic, presentation or assets changed. No broad regression
was run.

## Retained initial failures

The first C compile failed because the verbatim source declares an unused
`defenderHoldEffectParam`; the narrow compiler warning exemption above preserves
the source text. The first test run (`evidence/run-1.log`) passed 27 numeric vectors
and all five actual turns, then exposed a harness assumption that legacy Explosion
was admitted. The corrected harness checks its existing rejection instead. No
product change was made to satisfy that failed test. `run-2.log` retains the first
complete pass.

## Main integration

The combined Mimic/Sketch + arithmetic product passed all 6 groups / 161 assertions / 33 numeric vectors / 5 real turns again in `evidence/integrated-with-oracle/`. The earlier `integrated-copy/` attempt omitted the executable oracle required by two dynamic cases; its setup failure is retained. Regenerating that oracle from the pinned reference fixed the harness input, with no product edit. The extracted C source and executable are reproducible local outputs and are excluded from Git.

## Four-position Hustle increment

After building the same project, run `dotnet tests/Game.DamageAbilityGaps.Tests/bin/Debug/net10.0/Game.DamageAbilityGaps.Tests.dll CONTENT_ROOT four-hustle NEW_RESULT_JSON`.
This selects only the new bounded checks, without re-running the older arithmetic suite.
Natural Corsola and Togetic holders exercise exact physical accuracy cutoffs, special
moves, physical-type Charm, all four actor positions, actual PP ownership and a
complete four-action turn. A separately labelled controlled Poison Powder moveset
checks the same accuracy command for primary status; it is not a campaign learnset
or Metronome demonstration. FireRed's Hustle test is based on type category even
when move power is zero. The existing source damage arithmetic remains shared.
