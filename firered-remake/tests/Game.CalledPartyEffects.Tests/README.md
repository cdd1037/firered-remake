# Called party and overlay effects (B08)

This adds source party, type, ability and item registers to the authenticated
single-action kernel. It does not enable natural Metronome selection, normal
Fuchsia turns, campaign persistence, doubles, or the complete callable closure.

The implementation executes through the real shared ForestMove kernel and
actual immutable party state. The focused suite includes independent C command
vectors, caller PP/history, exact RNG, failure atomicity and genuine switches.
Final focused result: **31 groups / 8,251 assertions / zero failures**, including
322 independent C vectors. Six affected composition regressions add93 groups
and8,906 checks; combined **124 groups / 17,157 checks**. All builds report zero
warnings/errors on .NET10.0.12. Per-suite logs are under `evidence/`.

## Genuine parties and overlays

`WildBattleState.CalledParty` contains two immutable actual party arrays and
active indices. A member has authenticated permanent `PokemonInstance`, current
battle item, status, egg flag, Knock Off provenance and an explicit permanent
item override when the source battle and party item differ. Stable array indices
identify members only within this battle. The application must bind those to its
existing ownership IDs; this contract never invents an ID or a reserve.

`WildBattleCombatant.CalledOverlay` holds only battle type/ability overrides.
`WildBattleEffective.Ability`, `Types` and `HasType` supply mechanical reads across
damage, status, cancellation, weather and entry. Species, PID, OT, IV, permanent
ability and original Transform data retain their authenticated values. Overlay
abilities must occur on actual supplied party members or authenticated active
Transform donors. No Cloud Nine, Air Lock, Forecast, Pressure or new species is
fabricated. Type changes admit every source-reachable Gen III type, excluding
Mystery (Conversion maps a Mystery move to Ghost/Normal as the source does).

Active member Pokemon/HP/PP, status, battle item and Knock Off registers are
synchronized after every called transaction. Content equality, rather than
array reference identity, makes genuine snapshots survive JSON round trips.
Absent extensions retain old JSON omission; the historical v1 APIs reject them.

## Switch transitions and items

Baton Pass returns an explicit pending choice containing only real living,
non-egg reserves. `ResolveCalledPartySwitch(catalog,state,new(side,index))`
consumes that choice or a faint replacement. With no pending choice it executes
an explicit voluntary switch, validating the real reserve and source trapping.
This is one switch boundary, without an opposing action or residual turn.
Application revision checks remain at the application boundary.

The consumer clears ordinary volatile state, copied types/ability and Transform,
preserves side weather/screens/Spikes and slot-owned Wish/Future attacks, and
passes precisely the source Baton subset. It installs the actual reserve,
resets its Toxic counter, applies grounded Spikes damage, runs represented entry
ability behavior, and exposes further genuine replacement when entry faints.
Baton Pass bypasses escape-prevention checks; ordinary switching does not.
Trainer Roar/Whirlwind uses the same real consumer immediately, with source
level failure and Random()%6 rejection over actual healthy non-egg reserves.
The Forced transition lets a future turn adapter cancel the departed battler's
queued action. No healthy reserve fails the source script; wild force-out still
escapes. Teleport reads the actual battle kind and fails in trainer battles.
Wish attribution derives the actual active party index and permanent caster
snapshot; genuine caster/target departures preserve the delayed field slots.

Recycle's consumed-item register belongs to a battler slot, not a party member,
and survives switching in the pinned source. Knock Off only suppresses a battle
item and sets the party-index flag; its permanent item remains. Recycle can then
write a different permanent item without removing that flag, and later entry
suppresses only the battle item again. Trick and Thief stage changed battle
items until source MOVEEND_CHANGED_ITEMS, after contact/immunity checks and
before held-item effects. Their true party writes are separate. Thief makes
contact; Covet does not. The same source recipe reads each actual definition's
flags. Regular opposing battlers cannot steal/swap; Sticky Hold, mail, Enigma,
empty/occupied items and Knock Off restrictions retain their source order.
Trick, Thief and Covet remain direct-Metronome-forbidden. Their final end-to-end
called tests belong to B09's genuine Mirror Move path.

## Pinned source details

Pin: `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

- `battle_script_commands.c:7003`: Conversion, learned-slot rejection RNG and Mystery mapping
- `:7699`: Conversion 2, exact source table order, rejection RNG and 1000-attempt fallback
- `:7995`: Heal Bell excludes eggs and Soundproof but includes fainted reserves; Aromatherapy writes all slots
- `:8110`: Spikes increments before PP; a fourth layer marks Pressure suppression
- `:8571`: Beat Up skips eggs/fainted/statused members, uses each member's level/base Attack and target species base Defense, without normal STAB/type calculation
- `:8681`: Memento's minimum-stat gate; Protect/Substitute still allow self-faint
- `:8800,8884,8999,9366`: Trick, Role Play, Skill Swap and Recycle
- `:2610,2732,4164,4489,4971`: theft, Knock Off, changed-item ordering, entry suppression and Spikes
- `battle_main.c:2329`: exact Baton transfer/reset and history registers
- `data/battle_scripts_1.s:2566`: Teeter Dance primary confusion branch order

Conversion2 intentionally uses the user's latest approved modern design instead
of the original random table-row loop. After the original last-hit/two-turn
validity checks, it enumerates every real Gen III defensive type in numeric
source-ID order, keeps resistances and immunities to the last landed move's
type, excludes both current types, and keeps each type once. Mystery is never a
candidate. An empty set produces normal script failure with caller PP spent and
no selection draw. Otherwise, exactly one standard engine uint16 draw is mapped
by `raw % candidateCount`; the selected type is assigned to both battle types.

This uses the existing engine's single-draw convention. Across all65536 raw
values, bucket sizes differ by at most one, so it is a near-uniform discrete
mapping rather than a claim of mathematically exact equality for every count.
There is no table-index rejection, 1000-attempt limit or fallback. Metronome's
pool and selection remain unchanged. Old same-seed Conversion2 traces are
intentionally not an acceptance target. Historical pre-repair vectors are kept
under `evidence/history/` only, with no executable-test dependency.

Current independent C evidence comprises322 action vectors and complete
resistance candidate sets for all18 type IDs (Mystery has none). Tests compare
all5202 legal current-type pairs against those independent sets, verify actual
dual-type exclusion and deterministic actions, exercise original validity
gates and empty sets, and count every uint16 mapping for candidate sizes1..17.

## Reproduce

From the repository root:

```sh
cc -std=c11 -Wall -Wextra -Werror tests/Game.CalledPartyEffects.Tests/source-oracle.c -o /tmp/called-party-source-oracle
/tmp/called-party-source-oracle > /tmp/called-party-source-vectors.json
cmp tests/Game.CalledPartyEffects.Tests/source-vectors.json /tmp/called-party-source-vectors.json
source scripts/env.sh
flock /tmp/firered-remake-build.lock "$DOTNET_ROOT/dotnet" build tests/Game.CalledPartyEffects.Tests/Game.CalledPartyEffects.Tests.csproj --configfile .toolhome/recovery-local-only.config -m:1 -nr:false
"$DOTNET_ROOT/dotnet" tests/Game.CalledPartyEffects.Tests/bin/Debug/net10.0/Game.CalledPartyEffects.Tests.dll .
```

Run `bash tests/Game.CalledPartyEffects.Tests/verify.sh` for the focused suite and
six affected composition regressions. No network,
new package, GUI, fabricated party or separate battle test engine is used.
