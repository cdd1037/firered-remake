# Shared Stockpile / Spit Up / Swallow / Charge

Fixed product input: `a7d1936f9e5f446a36209599dc909749ec96eec0`.
Original FireRed source: `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

The four genuine battler slots and canonical singles now execute the same recipe.
The existing body counter/timer fields, shared damage/type/held-item commands,
residual scheduler, faint cleanup, party identity, and switch/Baton owner are reused.
There is no paired singles simulation, new scheduler, new battle state, or campaign admission.
The similarly named preexisting Charge recipe owns two-turn attacks; this family owns
MOVE_CHARGE's Electric boost.

- Stockpile increments a 0–3 counter and does not raise any stat in Gen III.
- Spit Up uses original base power 100, multiplies the base damage including +2
  by the stored count, then applies Helping Hand, STAB/type and adjustsetdamage.
  It has no critical or damage-variance draw. A miss keeps the counter and executes
  a second PP command; Protect spends PP once, skips accuracy and consumes a
  nonzero counter. Empty storage remains an empty-storage failure even on Protect.
- Swallow heals max HP / 4, / 2 or all max HP, minimum one, with HP clamping.
  It consumes storage even at full HP; the full-HP failure does not write healing damage.
- Charge sets two ticks, with no modern Special Defense boost. The existing Charge
  residual position decrements it on the setup turn and next turn, including incapacity.
  Original Electric metadata receives the existing ×2 damage multiplier while positive;
  using a move does not itself consume the timer. A zero-PP attempt cannot refresh it.
- Ordinary switches and Baton Pass both clear these two fields; Baton keeps its
  existing allowed subset (the tests preserve Substitute). Faint cleanup already clears both.
- Source callers retain their original PP register. Metronome ignores the PP of a
  learned copy of its selected move. Sleep Talk can select a zero-PP learned move,
  but the called attackcanceler then rejects it; Sleep Talk's own PP remains spent.

## Reproduce

Required inputs are inventoried with byte lengths and SHA-256 in `required-inputs.json`.
Use the existing authenticated private content root and its official local-only toolchain;
no source assets are copied into this patch. The runner sources that root's `scripts/env.sh`,
uses its `recovery-local-only.config`, locked restore, and `/tmp/firered-remake-build.lock`.
All compiler outputs remain in this candidate's own bin/obj directories.

```
bash tests/Game.SharedStockpileCharge.Tests/run.sh CONTENT_ROOT NEW_OUTPUT_DIRECTORY
```

The original-C proof was run once for this delivery. To reproduce it separately:

```
python3 tests/Game.SharedStockpileCharge.Tests/source_probe.py --source PINNED_SOURCE_ROOT --report NEW_REPORT.json
```

The proof authenticates source bytes against the pin, compiles four unmodified command
bodies plus original Charge damage/residual fragments, and checks 23 vectors. Its
CalculateBaseDamage stub returns 13 to expose multiplication and consumption boundaries;
it is explicitly not a GBA runtime or an independent proof of the entire damage formula.
The script additionally audits Spit Up's command order, repeated miss PP, and absence of
stat/critical/random-damage commands.

`singles-before.json` is immutable full before-state/result data captured before product
changes, not regenerated expected hashes: 96 direct branch results and six sequences of
four real turns. All 102 after records must match the complete original JSON bytes,
including state, PP, events, invocation, RNG state, cursor and trace. Capture refuses to
overwrite. Final before/after SHA-256 is
`adf9f1ef7a23627826f53cd35332bf0d43424ebe8b328ce76d604c66baacb875`.

Final targeted verification: 9 groups / 554 checks, zero failures, hosted .NET 10.0.12.
It includes actual four-action turns with JSON restore/replay at continuation boundaries,
slot 2 identity, screens/Helping Hand arithmetic, cap/failure branches, no-PP/incapacity,
Substitute/immunity/Endure/Focus Band, ordinary switch/Baton, real Metronome selection,
Sleep Talk, and invalid-counter atomic rejection. Existing historical volatile tests,
shared end-turn phase tests and shared late-turn tests are separate regression evidence.
No GUI or historical campaign was rerun or claimed.
