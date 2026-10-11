# Shared late-turn recipes

This bounded executable covers Bide, Perish Song, Destiny Bond and Grudge on the
actual four-position owner. It uses the pinned authenticated source catalog and
source Pokemon factory, complete turns, actual reward and replacement APIs, and
JSON round-trip replay at each pause. No paired-single approximation is used.

Run the one suite:

```sh
bash tests/Game.SharedLateTurn.Tests/run.sh PRODUCT_ROOT PINNED_SOURCE NEW_EVIDENCE_DIR
```

The build uses the product's `scripts/env.sh`, SDK/runtime 10, local-only NuGet
configuration and the shared `/tmp/firered-remake-build.lock`. It does not install
packages or run other project test suites. Empty/missing baseline or C vectors
fail. The original-C probe is documented in `source-proof.md`; all authored C/H
inputs and immutable vectors are included.

## Baseline and intentional corrections

The selected source baseline is product commit
`a51e8a633b36739e398e2e3ef95bab32f15d6a72`. The primary `singles-baseline.json`
was captured before any product edit and keeps 24 full-turn serialized
state/event/RNG hashes. These remain unchanged. The immutable
`pressure-before.json` and `bide-before.json` were captured with a separate
selected-source archive of that exact baseline and the same test executable.
They retain original results, never rewritten expected results.

Two canonical implementation corrections have explicit minimal-delta assertions:

- Perish Song now executes original `PressurePPLoseOnUsingPerishSong` after
  numeric field application. Each other Pressure holder, including allies and
  absent/zero-HP holders, charges the first learned Perish Song position. It
  charges even when every position was already affected. The canonical singles
  result differs only in that PP value, the added PP-command event and consequent
  event sequence numbers; invocation, RNG, histories and other state are exact.
  Historical noncanonical profiles retain their prior behavior.
- Full paralysis now cancels canonical singles' Bide lock, as the original
  `BattleScript_MoveUsedIsParalyzed` command requires. The captured ordinary
  Bide storing/releasing actions remain exact; paralysis differs only in lock
  removal, its event and consequent event sequence numbers. Historical profile
  enablement remains unchanged. These are fixes to this remake, not changes to
  original FireRed behavior.

## Ownership and source timing

Shared recipes own setup, Bide accumulation/release, Grudge PP loss, Destiny Bond
HP loss and Perish residual mutation. Adapter hooks supply slot/legacy storage,
retained target selection and existing scalar compatibility commands. Frame
state uses the existing Body flags, CalledVolatile record and Registers.Turn
lock. No new phase owner, state format, or scheduling subsystem is introduced.

Perish applies to all numeric positions without an HP/absence gate; Soundproof
positions are excluded and reported only after successful setup. The existing
end-turn owner advances Future attacks first, then Perish in retained speed
order; source-absent positions are skipped there. Its existing Perish cursor is
now admitted by the frame validator. Each KO resumes through existing reward and
entry boundaries exactly once. Ordinary switching clears the timer; the
existing Baton Pass implementation preserves it.

Bide uses original priority zero and 100 accuracy. It spends PP on setup only,
retains the real chosen PP position, stores commanded damage and the latest
source position (including a later effective zero-damage hit once energy exists),
then releases twice accumulated damage. Immunity never refreshes that source. No crit/damage/secondary
RNG is introduced. Substitute never contributes stored damage. A still-present
source position receives release even if its occupant changed or is an ally;
source absence or an approved deferred KO vacancy invokes the existing target
resolver. Sleeping Bide is cleared before field residuals; other inability
branches use the existing canceller.

Four-slot Grudge runs before Destiny Bond damage, then the attacker's faint and
original target's faint, matching the source script stack. Grudge uses the chosen
move position and excludes same-side KOs, zero-HP attackers, Struggle and passive
callbacks. Destiny Bond ignores same-side and passive KOs. Both flags clear at
the next action attempt before inability checks. Historical singles retain their
existing faint event adapter order. Approved end-turn KO replacement remains in
force; no new target or replacement is fabricated midway through an action.

## Scope

The checks include real Bide charge/store/release and a normal next turn;
Protect, Ghost immunity, Substitute, incapacity and no-energy branches; source
position retention and absence/deferred retargeting; Soundproof/Pressure and
Perish duplicate setup; three real Perish KOs with JSON-resumed EXP and entries;
combined Grudge/Destiny Bond KO order; next-action cleanup; same-side/passive
exclusions; Future-before-Perish traversal; switching; malformed atomic rejects.
This is family-level rules evidence, not native UI, full campaign or full-suite
verification. Caller dispatch integration belongs to the parent task.
