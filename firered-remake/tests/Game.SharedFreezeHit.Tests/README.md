# Shared Freeze Hit command slice

Pinned remake baseline: `88543cb9ee09fd1a56a5567bc1aaa7bc0aeea900`.
Pinned pret source: `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

This suite admits the four authenticated `EFFECT_FREEZE_HIT` moves into the existing
four-slot kernel: Ice Punch, Ice Beam, Blizzard and Powder Snow. The two BOTH moves
run separate source passes against the real opposing slots with one PP debit.
The singles field recipe and four-slot generic dispatcher execute the same new
recipe and status command. Existing scalar singles damage/accuracy/result hooks
are retained. Tri Attack's existing singles recursive freeze also reuses the new
status command. Tri Attack, thaw-user moves and Fire move-end thaw are not newly
admitted to four-slot execution. Serene Grace remains behind existing ability
admission; its source chance vectors alone do not establish action acceptance.

The suite retains 960 initial complete-result hashes and 320 supplementary
positive-infliction hashes, captured by executing the original fixed-baseline
singles dispatch before migrating that dispatch. No prior golden was replaced. The final normal corpus contains successful freeze
inflictions, Aspear cures and Substitute interceptions, not only non-triggering
rolls.
The whole serialized public result is hashed, including state, invocation, ordered
events and complete RNG traces. Two additional immutable boundary corpora retain
192 initial and 64 successful-roll Endure/Focus Band results from that same original
dispatch. Canonical singles correct the zero-adjusted-damage status omission:
`Cmd_adjustnormaldamage` preserves effectiveness after leaving one HP, and
`Cmd_seteffectwithchance` therefore still applies the rolled effect. Historical
noncanonical singles retain their prior adjusted-damage condition. The suite
requires exact unchanged results outside those declared successful zero-loss
freeze cases and unchanged RNG inside them. The separately owned Focus Band result-message
change may add one verified typed survival event after successful HP update; the
comparator records that delta separately and normalizes only those two documented
commands before comparing the entire remaining public result.

`source_probe.py` authenticates every consumed C, assembler and constant-header
file against the pinned git blob before running authored host code. It extracts
unchanged `SetMoveEffect` prechecks, the freeze case and its status-write tail,
plus the complete unchanged `Cmd_seteffectwithchance`. Other switch cases cannot
run in this bounded harness. The authored stubs only observe RNG draws, status
writes and multi-turn cancellation. This is not execution of a complete original
battle engine, an upstream build script, or a ROM.

The 2,400 golden vectors cover all 100 chance residues, ordinary/Serene Grace
chance, Ice/Magma Armor/Shield Dust/Safeguard, Sun/suppressed Sun, existing status,
fainted target, Substitute, no-effect and certain-effect branches. Both optimized
and UBSan builds must agree byte-for-byte. Assertions authenticate exact hit-script
command order and zero-loss adjustment/critical behavior. Tests then compare
actual four-slot actions against applicable C vectors and execute complete real
four-action turns, KO/reward waits, stale continuation rejection, natural learned
moves, per-slot party synchronization, Aspear, selected allies, two Pressure
holders, independent BOTH rolls, charge cancellation and atomic closed gates.

Run in the official environment:

```sh
bash tests/Game.SharedFreezeHit.Tests/run.sh PROJECT_WITH_AUTHENTICATED_CONTENT PINNED_PRET_CHECKOUT OUTPUT_DIRECTORY
```

The selected-source development workspace can use the unchanged main project's
content path as its first argument; code, intermediates and evidence remain local
to the isolated workspace. The script sources `scripts/env.sh`, uses the shared
build lock, and passes `-m:1 -nr:false -p:UseSharedCompilation=false`. It does not
run old aggregate suites, launch Godot, or establish presentation/campaign/full
four-slot acceptance. Existing Substitute integration is separately owned; the
fixed baseline excludes four-slot Substitute while the immutable singles corpus
and C command vectors cover its freeze precheck.
