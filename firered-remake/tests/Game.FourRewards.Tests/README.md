# Bounded four-slot reward continuation

Fixed implementation base: `909c645f2b1bd233272c26d5295055da7e5b1beb`.

`SourceFourActionTurns.Version` is now `firered-four-action-reward-continuation-v3`. Existing v2 diagnostic cursors reject. This is domain diagnostic JSON support, not player in-battle saving or campaign admission.

## Authority and API

- `SourceBattleFrame.Parties` is the permanent Pokemon authority. Optional `Rewards` holds six opponent-completion bits, six player-participation masks, explicit reward context/metadata, and one bounded recipient/learning cursor. No history ledger or duplicate party is stored.
- Begin and actual identity entry boundaries collect living active participants separately for each opposing party index. Both opposing occupants settle separately. Completed opponent tokens are retired before entry; completion bits remain for the battle.
- `SourceFourRewards.Settle(catalog, frame, continuation)` calls `SourceVictoryRewards.Calculate` against the actual current party. It awards recipients in original party order. Calling `SourceFourActionTurns.Advance` at a reward boundary delegates to this method.
- Genuine move choices pause the party loop. `SourceFourRewards.Learn(..., new(recipient, level, move, replaceSlot))` replaces the selected non-HM slot, or declines when `replaceSlot` is null. Remaining candidates are attempted in source order; already-known candidates skip automatically. Existing PP and PP Ups remain; replacement uses source PP and zero bonuses.
- All public continuations bind exact revision, event sequence, next-action index and phase. Stale, repeated, invalid and overflowing calls retain the identical input and emit no event/RNG result.
- Context/metadata must come from authoritative ownership/met/ball/Pokerus state. This owner does not manufacture missing facts. Even an empty result for all-fainted or level100 parties must pass the actual calculator's validation before completion and terminal outcome publication.
- Reward projection uses `ApplyPersistentPokemon`, preserving Transform/copy overlays and applying full six-stat refresh on both active player slots (approved BUG-04).

`ChooseReplacement` admits clean KO replacement only at the retained end-turn boundary. It binds actual reserve keys and entry serials, resets source entry state and clears stale action ownership. It rejects entry-trigger abilities needing unported entry recipes. With fewer reserves than vacancies, the unfilled positions become source-absent; no fake recipient or battler is created. Voluntary/Baton Pass/forced timing and their existing separate owners are untouched.

## Residuals, delayed hits and deferred target failure

The retained `SourceEndTurnCursor` admits only FutureAttacks resume positions (0–4) and terminal Complete. Normal field/battler residuals run once. Fresh `EndTurn` cannot carry a delayed cursor; `EndTurnResume` requires the retained cursor after an actual pause. Reward and replacement calls preserve the same delayed cursor. Future Sight/Doom Desire stored attacks use the shared source accuracy and random-damage preparation recipe now also consumed by the unchanged singles adapter; the four-slot adapter supplies actual slot storage/faint callbacks. Source-absent positions are skipped before decrement, fainted-present positions consume a due timer without a hit, and the attacker is the stored battle position even if its prior occupant departed. Wish/Perish and other unported delayed capabilities remain closed.

The approved end-turn KO policy creates a no-target boundary that original FR's immediate replacement normally avoided. This boundary uses the original `BattleScript_ButItFailedAtkStringPpReduce` sequence (attack string, shared PP command, failed result, normal move-end history), without accuracy/damage RNG or a fabricated hit. It retains the original PP predicates, including a selected attempted position's retained HP0 Pressure body. No new modern PP rule is substituted.

EXP Share/Lucky Egg/Soothe Bell have no admitted action/residual effects. Macho Brace additionally retains its already-shared integer speed halving in real action/end-turn ordering. Other item capabilities remain gated.

## Reproduce

Pass the complete recovered content project (without copying content or tools):

```sh
source "$CONTENT_PROJECT/scripts/env.sh"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.FourRewards.Tests --configfile tests/Game.FourRewards.Tests/NuGet.Config -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false
dotnet tests/Game.FourRewards.Tests/bin/Debug/net10.0/Game.FourRewards.Tests.dll "$CONTENT_PROJECT"
```

The focused suite exercises actual complete-turn KOs; remaining actions and no-target PP; poison and separate delayed callback positions; both opponents, reserves and shared modifiers; both-slot leveling; genuine learning/decline/HM/PP Ups; actual Explosion with no living recipients; level100; proven Transform projection; serialized pending state; and stale/duplicate/overflow rollback. The existing four-action source vectors, shared reward source/C vectors and untouched singles end-turn golden are separate regression checks. No native UI/audio, AI, preset encounter or campaign claim is made.

## Deferred Explosion / Selfdestruct target boundary

A legal turn that was accepted with four living battlers could previously stop after
an earlier KO: the later Explosion returned
`source_kernel_spread_deferred_target_not_migrated`. The focused reproduction
uses the public BeginActions, Advance and EXP settlement APIs; it does not inject
a fabricated mid-turn vacancy.

Original FireRed `037335f4c725d7c9aecdac87066f2002b4bd7e14`
(`src/battle_script_commands.c:6255–6331`) selects Explosion targets in numeric
order, excluding the attacker and source-absent slots, without testing HP.
The approved end-turn KO policy adds a separate filter for deferred vacancies.
The source absence list remains intact, and non-deferred HP0 bodies still reject
at the existing unsupported boundary. Defender counts, spread damage rules,
Damp/Pressure and weather/side-ability scans are unchanged.

If both opposing positions are deferred, a remaining ally is still a real
Explosion/Selfdestruct target. If all other positions are unavailable, the
existing deferred no-target sequence consumes PP once and fails without hit RNG,
damage, or self-faint. No reserve enters the target list before end-turn entry.

Run only these three focused groups after building the existing project:

```sh
dotnet tests/Game.FourRewards.Tests/bin/Debug/net10.0/Game.FourRewards.Tests.dll "$CONTENT_PROJECT" deferred-spread
```

The groups cover all four attacker positions across opposing/player directions,
prior KO then Explosion, two opposing KOs then ally-only Selfdestruct, and the
sole remaining actor's no-target action. They check actual EXP/completion exactly
once, permanent EXP totals, JSON continuation equality, stale continuation
rollback, original non-deferred HP0 rejection, retained source defender counts,
and complete residual/replacement continuation with untouched reserves.
