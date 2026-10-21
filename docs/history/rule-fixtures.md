# Deterministic rule contracts and fixtures

Historical engineering-core contract. The console runner, 77-test result, and profile descriptions below belong to the early implementation, not the maintained MSTest suite or current campaign. See [current architecture](../architecture.md) and [test entry points](../../tests/README.md).

## Status and scope

This is a small executable engineering core, not a complete Gen III battle simulator or a playable FireRed content slice. The profile IDs are `fr-single-v0` and `fr-grid-v0`. These deliberately distinguish the harness from a future compatibility-complete rule version.

The self-contained console suite has **77 passing tests on .NET 10.0.12**, built for `net10.0` / C# 14 with zero compiler warnings. Evidence is in `tests/Game.Domain.Tests/evidence/`. The suite count is a test count, not content or game-completion coverage. No external testing NuGet package is used. `Game.Domain` has no project/package dependencies and no Godot, file, network, wall-clock, or ambient random calls.

P0 remains owned by the project-wide toolchain/presentation gate. A successful domain suite does not prove P0, export compatibility, original-game behavior, first-badge completion, or full-game completion.

## Public integration surface

- `Game.Domain.Ids`: immutable strong string IDs for maps, species, moves, items, scripts, trainers, text, flags, combatants, battles, and commands. Values are case-sensitive; no ordinal species number or array index is a persisted identity. `CombatantId` identifies an individual. `OriginalTrainerId` is preserved as an optional field for future ownership/trade work, but trade behavior is not implemented.
- `Game.Domain.Randomness.Gen3Rng.Next(RngState, purpose, bound = 65536)`: returns new full RNG state, raw `ushort`, and trace entry. `RngState(uint Seed, ulong Cursor = 0, string AlgorithmId = Gen3Rng.AlgorithmId)` serializes the algorithm identifier and rejects an unknown algorithm. Construct with an explicit seed; `default(RngState)` is not a valid versioned stream.
- `Game.Domain.Rules.StatRules.CalculateHp` / `CalculateOther` / `ApplyNature`: pure integer functions.
- `Game.Domain.Rules.TypeRules.Category`, `EffectivenessTenths`, and `OrderedEffects`: Gen III type category and source-table order.
- `Game.Domain.Rules.DamageRules.Calculate(DamageInput)`: ordinary controlled damage and ordered arithmetic steps.
- `Game.Domain.Battle.BattleRules.Apply(BattleSnapshot, UseMove, IBattleContent)`: pure command transition. `BattleSnapshot.Create` creates the single-active-per-side harness. `IBattleContent.FindMove` resolves a stable move ID.
- Public pure validation: `BattleRules.ValidateState(BattleSnapshot, IBattleContent)` and `WorldRules.ValidateState(WorldSnapshot, IWorldMap)` return `Diagnostic?` (`null` means valid). The command transitions and save/load callers share these checks. Battle validation checks every represented move reference. Valid queued selections can be restored by Domain; an Application that supports only stable save checkpoints must additionally reject a non-empty pending selection.
- `Game.Domain.World.WorldRules.Move(WorldSnapshot, MoveIntent, IWorldMap)`: cardinal tile transition. `GridMap` is a small immutable flat-grid implementation; it is not an imported FireRed map format.

Snapshots are immutable records with `ImmutableArray` collections. Returned rejected/fault transitions retain the exact input snapshot reference, with no events or RNG consumption. Commands must carry their expected domain revision. Accepted commands advance that revision once. Application owns durable transaction publication, command-ID replay/deduplication across committed turns, canonical hashes, and save boundaries.

## Battle command contract

1. Validate profile, snapshot invariants, command ID, revision, active actor/target, move slot and PP, move definition, and any already-queued selection.
2. The first accepted `UseMove` stores a pending selection and emits `MoveChosenEvent`. It changes neither HP nor PP nor RNG nor turn number.
3. A selection from the other side validates both moves before resolving anything. Selection order does not change semantic turn order: the canonical tie comparison is player versus opponent.
4. Higher move priority acts first; equal priority uses higher speed; exact ties use one `Random() % 2` draw. An odd result selects the opponent first.
5. On execution, PP decreases once. Ordinary accuracy1–100 uses `raw % 100 + 1`. Accuracy0 is explicitly unsupported: source always-hit behavior is selected by move effect (e.g. `EFFECT_ALWAYS_HIT`), not inferred from a zero accuracy number. A miss does not roll a critical or damage percentage.
6. A landed ordinary attack uses `raw % 16 == 0` for a base critical, then `100 - raw % 16` for the 85–100 damage percentage. It emits a full arithmetic trace with HP before/after.
7. A fainted target ends this single-active fixture battle. Its queued action consumes no PP or RNG. The state records player/opponent victory, clears pending moves, and advances the turn number to the next semantic turn. Further moves are rejected.

Typed events include metadata with battle ID, monotonic sequence, and causing command ID. Queued commands retain their own causes even when the other side's submission resolves the turn. `System.Text.Json` polymorphic discriminators preserve event types when serialized.

The roster representation and stable IDs are not capped to 151 species or permanently defined as a pair of named fields. Nevertheless, this profile requires exactly one active on each side. `BattleMode.Doubles` faults explicitly; there is no silent conversion into singles. Full party management, replacements, double battles, Trainer Tower, and single-player content remain future work.

## Source-backed arithmetic

Reference commit for every source below: `037335f4c725d7c9aecdac87066f2002b4bd7e14`. Source was read only; no reference repository scripts or ROM files were executed/downloaded.

| Contract | Pinned evidence | Important detail |
|---|---|---|
| LCG | [random.c:Random](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/random.c), [random.h:ISO_RANDOMIZE1](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/include/random.h) | Next unsigned state is `state * 0x41C64E6D + 0x6073` modulo 2^32, output is high 16 bits. Modulo bounded mapping retains source bias; it is not rejection sampling. |
| HP / other stats | [pokemon.c:CalculateMonStats](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/pokemon.c) | EV/4 floors before level scaling; HP adds level+10; other stats add 5; Shedinja HP is explicitly 1. |
| Natures | [pokemon.c:ModifyStatByNature](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/pokemon.c) | Multiplication by 110 or 90 then /100, with original non-BUGFIX unsigned-16 storage wrap. The wrap is observable only beyond official Nature-affected base-stat combinations. |
| Categories and type effects | [pokemon.h:type constants](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/include/constants/pokemon.h), [battle_main.c:gTypeEffectiveness](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/battle_main.c) | Nine physical types, eight special types; Ghost physical, Dark special; Steel resists both. Mystery is explicitly unsupported by the damage profile. No Fairy fallback. |
| Basic damage | [pokemon.c:CalculateBaseDamage](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/pokemon.c) | Attack × power × floor(2×level/5+2), /defense, /50. Physical base has minimum 1 before +2; special base does not use that minimum. |
| Damage modifiers | [battle_script_commands.c:Cmd_damagecalc, Cmd_typecalc, ModulateDmgByType, ApplyRandomDmgMultiplier](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/battle_script_commands.c) | Critical after base+2, then STAB, then distinct matching type modifiers in source-table order, then random percentage. Each step uses integer truncation. Immunity yields no applied HP damage. |
| Ordinary hit/crit/order | [battle_script_commands.c:Cmd_accuracycheck, Cmd_critcalc](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/battle_script_commands.c), [battle_main.c:GetWhoStrikesFirst](https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/src/battle_main.c) | Controlled ordinary attacks with no accuracy/evasion stages, statuses, special effects or battle-specific exceptions only. |

Downloaded source SHA-256 values inspected during implementation:

- `include/random.h`: `8a6049183598b2d865b5ff522abdb3d0e94012a127d724af46b9b7b69803f5b9`
- `src/pokemon.c`: `116a470cd9ebfa226aa8d85c2eb63591e176d726837c67baaa1ac6260d50f2ec`
- `src/battle_script_commands.c`: `3b7341db8cd58c54fb0b54b47e6eef89c5c05e8a60ab4e9b45bdc907845c19b3`
- `src/battle_main.c`: `c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928`

Per-stat inputs accept base 1–255, IV 0–31, EV 0–255, level 1–100. The team/member layer must later enforce total EV limits and legal species data. Ordinary damage accepts power 1–255 and already-selected Attack/Defense values 1–999. This profile does not emulate every possible hacked-stat signed-integer overflow path.

## Fixed fixtures and independent expectations

`tests/Game.Domain.Tests/Fixtures/rng-v1.json` pins eight sequential outputs from seed 0. The expected states were evaluated independently from the pinned recurrence, before calling the C# implementation. First states/outputs are 24691/0, 3917380458/59774, and 1383151765/21105. The max-uint seed overflow vector is 3191476742/48698.

`tests/Game.Domain.Tests/Fixtures/battle-turn-v1.json` includes source references, preconditions, two commands, six controlled raw RNG results, the complete expected event order, a full expected final snapshot, and its canonical state hash. The snapshot was constructed independently from the fixed schema and hand-calculated arithmetic, not copied from an observed domain result.

For that ordinary turn: both combatants are Normal, level 5, HP100, Attack/Defense50; power40, accuracy100, PP35; player speed20 and opponent speed10. Base damage is floor(floor(50×40×4/50)/50)+2 = 5; STAB gives 7. Percentages99 and88 both yield 6 HP applied. Both end at HP94 and PP34; revision2, next-turn2, eventSequence8, RNG seed3805062638 cursor6.

The expected canonical snapshot SHA-256 is:

`01be1f2531748bb3b24d05e8472f6cae25390ca94723f83eb544256fe1dc601d`

Canonicalization sorts object keys ordinally, preserves array order, uses compact UTF-8 JSON, and has no timestamps or machine paths. The fixture self-validates its stored expected snapshot against its stored hash before comparing the implementation. The test runner also round-trips a queued battle snapshot through JSON and verifies the resumed transition and repeated replay traces.

Golden expectations must not be rewritten merely because implementation changed. A changed public schema or rule profile requires an explicit fixture/schema version decision; a numerical mismatch first requires source review and a failing regression case.

## Known gaps and behavior decisions

| ID | State | Consequence |
|---|---|---|
| `single-v0-abilities` | Not implemented | Ability effects, immunity, activation order and ability RNG are absent. Active modifier flags fault. |
| `single-v0-badges` | Not implemented | FireRed first-badge Attack boost and other badge boosts are not applied. A badge-bearing full-game battle cannot be claimed compatible. |
| `single-v0-weather` | Not implemented | Weather mechanics and weather-dependent accuracy/power are absent; weather context faults. |
| `single-v0-items-status-stages` | Not implemented | Held items, major/volatile status, stages, screens, PP Ups, Struggle, recoil, multi-hit, multi-target, special move effects and advanced critical rates and effect-driven always-hit behavior are absent. Unsupported definitions fault. |
| `single-v0-roster` | Harness scope | Only one active per side; no switching, replacement, party roster, experience, reward, capture, escape, AI, evolution or learn-move decision. Mode must be versioned before extending. |
| `single-v0-rng-schedule` | Explicit simplification | Replays preserve this semantic command schedule. Hardware frames, source AI/pre-turn calls, encounter generation and other original shared-stream consumers are absent. Same project seed does **not** promise a same-seed GBA outcome. |
| `single-v0-accuracy-zero` | Reviewed and guarded | The initial incorrect zero-means-always-hit assumption was removed after comparison with `AccuracyCalcHelper`. A regression now requires zero-accuracy ordinary definitions to fault without mutation. |
| `single-v0-content-contract` | Caller responsibility | Source move data must retain effect metadata and cannot label an unsupported source move as ordinary damage. Definitions must remain immutable for a content version. No complete content importer is supplied by this module. |
| `nature-u16-wrap` | Source behavior preserved | Original `u16` multiply wrap remains; it is not silently “fixed” with a wider type. |
| `grid-v0-movement` | Project contract only | Four-way flat-grid walking, solid cells, boundary block, facing, and same-elevation movement. Source metatile encoding, NPC occupancy, layer transition, ledges, doors/warps, connections, grass encounters and step poison are unimplemented. Non-floor source/destination behaviors fault. |
| `grid-v0-blocked-facing` | Versioned contract | Blocked directional input is Accepted, updates facing/revision, but not position/step count/RNG. Illegal direction and stale revision are Rejected with no change. This has not been validated against a captured original-game movement trace. |
| `evidence-original-traces` | Missing | No emulator/ROM trace, full move matrix, source-to-content closure, or complete Gen III conformance evidence exists yet. |

World and battle counters reject exhausted ranges rather than wrapping into invalid authoritative state. Unsupported mechanics are never silently converted into successful no-ops. Null host API arguments throw programmer exceptions; representable malformed snapshots/content return `Fault`. External content/map adapters are expected to be pure reads; thrown adapter exceptions are the Application transaction boundary's responsibility.

## Reproduction and negative gates

From project root:

```sh
source scripts/env.sh
dotnet build tests/Game.Domain.Tests/Game.Domain.Tests.csproj -m:1 -nr:false
dotnet run --project tests/Game.Domain.Tests --no-build
```

The console writes one JSON report and exits 0 only when at least one selected test ran and all passed. It accepts `--filter substring`; an empty selection returns exit 2. An invalid argument returns exit 2. A missing fixture makes its test fail with exit 1. The actual negative-gate results are recorded in `tests/Game.Domain.Tests/evidence/negative-gates.json`; the test-output fixture was restored and the full 77-test suite rerun successfully afterward.

Evidence files:

- `build.log`: actual build, zero warnings/errors
- `domain-tests.json`: complete passing test report on runtime10.0.12
- `empty-suite.json`: intentional empty-suite failure
- `invalid-arguments.json`: intentional invalid-invocation failure
- `missing-fixture.json`: intentional missing pinned fixture failure
- `negative-gates.json`: expected and actual process exit codes

The Domain source was also checked for Godot, file/network, wall-clock, `System.Random`, and ambient RNG references: none were present. Replay stability and these tests establish the documented limited contracts only.

## Pallet sign collision extension
The profile now accepts facing a physically blocked Sign cell without entering it, consuming RNG or increasing step count. The independent source chain is field_player_avatar.c PlayerNotOnBikeMoving (487–505) → PlayerNotOnBikeCollide (884–887) → directional walk-in-place. Sign interaction is separate and only exact msgbox/end scripts are enabled. Door/warp, water, grass and ledges are still distinct unsupported actions; they are not flattened into floor.
