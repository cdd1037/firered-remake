# Called turn locks (B06)

This batch extends `firered-called-action-v1`. It does not enable natural
Metronome, replace its pool, enable ordinary Fuchsia turns, or invent a learned
move slot. Every recipe uses the authenticated source current move through the
shared `ForestMove` execution path.

The 21 assigned IDs are Razor Wind, Fly, Hyper Beam, Solar Beam, Petal Dance,
Earthquake, Bide, Skull Bash, Sky Attack, Mind Reader, Lock-On, Outrage, Fury
Cutter, Uproar, Dive, Ice Ball, Blast Burn, Hydro Cannon, Sky Uppercut, Frenzy
Plant and Bounce. Existing called Dig, Thrash and Rollout use the same corrected
continuation state; Gust/Twister share the airborne interaction recipe.

## State and execution boundary

`WildBattleCombatant.CalledTurnState` is null and omitted in historical snapshots.
Its lock retains current move, original PP position and learned identity, target,
source counters, Bide damage/source, and the specific end-turn inability flag.
Its separate sure-hit fields belong to the target, and retain their source. Charge
and recharge use one remaining forced action; this is a semantic continuation
count, not a claim that FRLG has a one-valued rechargeTimer (the source writes
that otherwise unused timer to 2, then clears it when recharge is consumed).
Ordinary validation rejects the extension. Called validation authenticates its
move, learned PP slot, phase, counters and compatibility with the older locks.

`ResolveCalledMoveContinuation` executes the locked move with both chosen and
current source registers set to that move, preserving the original PP position.
It calls the actual executor and cancellation path. It does not invoke Metronome
again, spend target RNG again, fabricate slots, or consume PP globally once.

`ResolveCalledTurnLockEndTurn` is an explicit, effect-specific command boundary.
It executes Uproar, Thrash and sure-hit countdowns in supplied battler order and
then the source sleeping-lock cleanup. It does not increment the game turn or
claim to run poison, weather, Disable, Encore, Yawn, replacement or whole-party
processing. Individual internal Uproar/Thrash/sure-hit methods allow the complete
turn successor to place each command at its original position.

## Source details

Pinned source commit: `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

- `battle_main.c:3989`: chosen/current become `gLockedMoves`; PP position survives.
- `battle_util.c:3054`: target selection writes the persistent move target.
- `battle_scripts_1.s:777,794,1807,1899,1969`: charge first/release scripts,
  PP suppression, Skull Bash defense, semi-invulnerability and Solar Beam sun.
- `battle_script_commands.c:898,960`: first charging turns bypass Protect;
  normal accuracy checks Protect, sure-hit, semi-invulnerability, then RNG.
- `battle_scripts_1.s:585,2068`: Rampage/Uproar successful continuations skip
  `ppreduce`; misses jump to the common miss script and spend PP again.
- `battle_scripts_1.s:1592`, `battle_script_commands.c:8161`: Rollout/Ice Ball
  countdown and power; a miss cancels the lock. Protect cancels before the PP
  skip test, while an ordinary accuracy miss on an existing lock spends no PP.
- `battle_util.c:1350,1484`: recharge cancellation is after sleep/freeze and
  before flinch; Bide storage/release is after infatuation. FRLG paralysis leaves
  multi-turn state until the recipe-specific residual checks.
- `battle_script_commands.c:1758,6839`, `battle_scripts_1.s:3246`: Bide starts at
  two storage counters, records actual damage, and releases twice the total
  without PP, critical, random damage or secondary-effect draws.
- `battle_script_commands.c:2440,2600,2720,2774`: Uproar/Rampage duration draws,
  and recharge's CERTAIN effect, which skips the secondary chance draw on a hit.
- `battle_util.c:905,954,1018`, `battle_main.c:2941`: global Uproar waking,
  Soundproof exceptions, residual lock endings, sure-hit expiry, and final
  sleeping-lock cleanup after all battlers have had their Uproar wake pass.
- `pokemon.c:2625`: Solar Beam halves base damage in rain/sand/hail before +2;
  Cloud Nine/Air Lock suppress effective weather. The sun branch also executes
  when sun appears on a previously charged release, with its additional first
  PP command followed by the suppressed PP command.
- `battle_script_commands.c:7217`: underground/underwater evade sand and hail;
  being airborne does not prevent that weather damage.

The original PP command helper remains authoritative for Pressure, clamping,
NO_PPDEDUCT consumption and NO_ATTACKSTRING. The current source107 species
profile has no legal Pressure, Cloud Nine or Air Lock user; their existing
source-command/field primitives are reused, without fabricating ability data.
Focus Band is also outside current held-item admission. The tests check that
rejection explicitly and separately exercise its fixed-damage adjustment
primitive; the public Bide immunity case uses an admitted item-free target.

## Verification

`source-oracle.c` is an independent bounded transcription, not a ROM emulator.
Nine pinned source-file hashes are mandatory inputs; set `FIRERED_B06_SOURCE_ROOT`
when the pinned reference is not the supplied sibling checkout. Its 296 vectors cover all 21 assigned IDs across eight seeds and all applicable
first/continuation branches, retaining source target/accuracy/critical/damage/
secondary/duration draws, exact damage, PP command count/cursor/amount and lock
counter. Fixture base stats are authenticated in the executable tests.

Additional real-executor groups cover every immediate script's Protect failure,
all charging releases, zero-PP continuations, sun transitions, capped Skull Bash,
flinch/paralysis and residual interruption, recharge ordering, Rampage/Uproar
miss PP, five executed Rollout/Ice Ball hits, Bide between actual enemy actions,
all semi-invulnerability exceptions, sure-hit setup/expiry/OHKO, Uproar sleep/Rest/
Yawn/Soundproof, Fury Cutter chains, weather exposure, departure cleanup,
serialization, malformed metadata/state and atomic instrumentation failure.

Final B06 run: **19 groups, 4,440 assertions, zero failures**, including all
296 source vectors. Eight affected kernel, generic, stat/recovery, scalar,
field, Transform and historical Fuchsia suites also pass: **118 groups,
16,431 checks**. The combined evidence in `evidence/` totals **137 groups,
20,871 checks**. The focused build reports zero warnings and errors. These are
domain/source checks, not application, native presentation or complete-turn
acceptance.

Reproduce from the project root, with the installed offline toolchain:

```sh
source scripts/env.sh
cc -std=c11 -Wall -Wextra -Werror tests/Game.CalledTurnLocks.Tests/source-oracle.c -o /tmp/called-turn-source-oracle
/tmp/called-turn-source-oracle > /tmp/called-turn-source-vectors.json
cmp tests/Game.CalledTurnLocks.Tests/source-vectors.json /tmp/called-turn-source-vectors.json
flock /tmp/firered-remake-build.lock "$DOTNET_ROOT/dotnet" build tests/Game.CalledTurnLocks.Tests/Game.CalledTurnLocks.Tests.csproj --configfile .toolhome/recovery-local-only.config -m:1 -nr:false
"$DOTNET_ROOT/dotnet" tests/Game.CalledTurnLocks.Tests/bin/Debug/net10.0/Game.CalledTurnLocks.Tests.dll .
```

## Remaining complete-turn composition obligations

The successor scheduler must consume the forced current move and preserved PP
position, retain original target/register history, invoke granular residuals in
source order, reset action-local inability markers at the correct boundary, and
process sleeping-lock cleanup after all Uproar passes. Ability/type overlays,
Substitute, real party replacement and Baton Pass belong to their own batches;
Substitute must not emit an actual HP-damage event for absorbed damage. Bide's
central HP-update hook covers all currently executed direct called damage;
complete residual composition must also route the source damage-tracking rules
for poison, binding, Leech Seed and other passive damage while excluding weather's
SKIP_DMG_TRACK branch and preserving the source last-damage battler register.
