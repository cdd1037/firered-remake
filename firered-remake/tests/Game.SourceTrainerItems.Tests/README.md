# Source trainer item transactions

Focused coverage of medicine and Poké Flute under
`firered-source-single-trainer-v1`, with genuine source parties and the source
action/end-turn scheduler. Historical profiles keep their existing dispatch.

The shared `SourceBattleItemRules` authenticates every supplied owned member
against `CalledParty`, including actual HP, PP, status and permanent held item.
Medicine effects and inventory changes come from `EarlyShopMedicineRules.Use`.
The helper applies those results to the real battler/party and returns the whole
owned party. It does not reconstruct reserves from stale owned snapshots.

Coverage includes:

- Active and reserve Potion/status recovery, owned metadata and Defense badge
- Original player-item-before-enemy-move/item/switch ordering, with no player PP
- Both complete parties' flute status masks and Soundproof reserves
- Canonical party Toxic and the separate live Toxic damage counter
- Independent permanent/active Soundproof after an ability change, including
  HP-only medicine without a false Nightmare-cure event
- Permanent Knock Off/Recycle overrides on active and reserve members, real
  Trick, and an executed berry → switch → Knock Off → Recycle → item chain
- Actual Fly continuation locks, Baton Pass pending choice and atomic rejection
- Real Transform's copied PP and permanent owner identity across item turns
- Source residual faint/replacement and a delayed Future Sight callback without
  replaying the item, opponent action or earlier residuals

Pinned source references (source commit
`037335f4c725d7c9aecdac87066f2002b4bd7e14`):

- `content/raw/rocktunnel-trainers/src/battle_main.c:3532`,
  `SetActionsAndBattlersTurnOrder`, especially lines 3582–3616
- `content/raw/rocktunnel-trainers/src/battle_main.c:4150`,
  `HandleAction_UseItem` and its Fury Cutter/Destiny Bond/Grudge clear
- `content/raw/pokemon-tower-items/src/battle_script_commands.c:6157`,
  `VARIOUS_CHECK_POKEFLUTE`, separate active abilities and both actual parties
- Source medicine definitions and effects are authenticated by the existing
  imported shop/opening-item catalogs loaded through the actual Gym composition

Run from the repository root, using the pinned toolchain and local restore:

```sh
source scripts/env.sh
flock /tmp/firered-remake-build.lock dotnet build \
  tests/Game.SourceTrainerItems.Tests/Game.SourceTrainerItems.Tests.csproj \
  --configfile tests/Game.SourceTrainerItems.Tests/NuGet.Config \
  -m:1 -p:UseSharedCompilation=false -v:q
dotnet tests/Game.SourceTrainerItems.Tests/bin/Debug/net10.0/Game.SourceTrainerItems.Tests.dll "$PWD"
```

`evidence/build-final.log`, `evidence/run-final.log` and
`evidence/verification.json` retain the final result. Earlier failing runs are
retained: the first exposed invalid Insomnia/sleep test fixtures, and the second
exposed an assertion that omitted Future Sight's source damage-percent draw.
Those fixtures/assertions were corrected without changing accepted source rules.
This suite establishes the focused Domain contract, not native UI acceptance or
complete-game coverage.
