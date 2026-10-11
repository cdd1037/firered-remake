# Battle-system completion matrix

Inventory: 2026-10-10 UTC. Product base `67c89d36c0dbdeabfe906ff73a00d9c4c7ce3cba`; fixed FireRed reference `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

Current integrated checkpoint: **integrated working tree based on `b01522c721121548c43c841371be2702b4b86833`**, inspected 2026-10-11 UTC. Four-slot recipe admission is **295 / 354**, with **59 gated**; dedicated script coverage is **17 / 354**, with **337 remaining**. Neither is a complete battle-system percentage.

The initial inventory is bound to that product base. The fifth-badge singles stage subsequently closed at `5a5b6f7`, with actual Godot Koga verification and a separate native save/Continue checkpoint. This battle-completion stage is now active. Genuine preset doubles now has a bounded authentic Route8 Twins public victory/save branch; full held-item/recipe and presentation completion remains open. The JSON retains initial inspected-file hashes and records later implementation updates separately.

Read with [source turn integration](source-turn-integration.md), [move implementation plan](called-move-implementation.md) and [implementation roadmap](implementation-roadmap.md). The [machine-readable companion](battle-system-completion-matrix.json) contains every one of the 354 move IDs, all 76 official abilities, source/implementation locations, dependency lists and evidence hashes. The initial inventory itself did not execute tests. Later bounded implementation results are identified below and in their test directories.

## Denominators and status boundaries

- 354 authenticated move definitions and registered singles recipes. Focus Punch, Snatch, Mimic and Sketch have bounded shared singles execution coverage. Registration does not establish complete interactions, doubles or presentation.
- Metronome: 336 direct candidates and six additional indirect Mirror Move results (Counter, Struggle, Thief, Mirror Coat, Trick, Covet), totaling 342. The other 12 are outside this closure; they are not automatically missing.
- 76 official Gen III abilities from the 78-entry source table, excluding `ABILITY_NONE` and unused `ABILITY_CACOPHONY`. Current campaign actor admission is still 107 species. A separate authenticated mechanics catalog now supports 386 real species for explicit fixtures, with individual ability/recipe capability gates.
- Definition present, recipe registered, source branches tested, complete turn integrated, Application exercised, animation rendered and sound heard are separate claims. The imported `battleEffectImplemented` field and symbol occurrence counts are not execution coverage.

| Status | Meaning |
| --- | --- |
| `implemented-with-bounded-tests` | An identified implementation and bounded retained tests exist. Only the stated scope is credited; no exhaustive coverage or fresh run is implied. |
| `missing` | A required executable branch/state/owner is absent or explicitly rejected in the inspected scope, even when metadata/AI references exist. |
| `interaction-unverified` | Implementation/helper and/or bounded tests may exist; expanded actor admission, full source branches, combinations or integration have not been established. |

The initial per-move JSON marked the 350 registered recipes `interaction-unverified` for **complete battle-system acceptance**. It does not erase the bounded tests of their shared effect groups. It deliberately avoids assigning a complete label to every move based on a single registration assertion.

## Stage exit: rules first, presentation continues in parallel

User decision on 2026-10-11 (`Sentinel_23a5c65a12ac819190dd9441bae2d3fa`): resume the main campaign once the 354-move/76-ability core rules and affected key regressions are complete. Dedicated animation/audio completion is no longer a prerequisite for the next mainline region. Prioritize presentation needed by that region and clearly record any generic substitute; actual outcomes and settlement still require complete rules. Safari, roamers and other independent field/special-mode dependencies retain their explicit matrix boundaries and must not be silently counted as core acceptance.

## Priority and dependency order

1. Preserve the current-stage acceptance boundary and existing regression evidence. Establish explicit capabilities and battler/party identity interfaces only where expansion needs them (C23).
2. Extend shared ordering, pause/resume, ability hooks and permanent-state projection (C04–C09, C18). Mimic/Sketch and Focus Punch/Snatch now use those same singles paths (C02–C03); expanded interactions remain separate.
3. Deliver genuine four-slot state/action ownership before target redirection, spread moves and ally abilities (C10 → C11 → the doubles portions of C09/C19/C21). This is a substantial independent block, not a cosmetic two-opponent view.
4. Fill the bounded remaining item/capture/growth/AI gaps by source effect groups (C13–C20). Safari and roamers have their own field/story dependencies and must be reported as independent blocks.
5. Close chosen cross-state source branches and Application/native scenarios while each corresponding presentation/audio increment is wired (C06, C21–C22). Keep the rule and presentation ledgers separate.

P0 means enabling contract work, P1 means a dependency needed before wider admission, and P2 means closure/integration work after its prerequisites. These are dependencies, not calendar estimates. Existing passing modules should be reused; this is not a full engine rewrite.

## Actionable mechanism matrix

| ID | Area | Current status | Priority / shape |
| --- | --- | --- | --- |
| [C01](#c01) | Authenticated move definitions and recipe admission | `implemented-with-bounded-tests` | P0 / bounded |
| [C02](#c02) | Mimic and Sketch | `implemented-with-bounded-tests` | P1 / bounded |
| [C03](#c03) | Focus Punch and Snatch | `implemented-with-bounded-tests` | P1 / bounded |
| [C04](#c04) | Singles order, priority, speed and ties | `implemented-with-bounded-tests` | P1 / bounded |
| [C05](#c05) | Residual and delayed callback scheduling | `implemented-with-bounded-tests` | P1 / bounded |
| [C06](#c06) | Move effect groups and cross-state interactions | `interaction-unverified` | P2 / bounded |
| [C07](#c07) | Ability entry, weather and forms | `interaction-unverified` | P1 / independent-block |
| [C08](#c08) | Ability damage, absorption, contact and residual gaps | `interaction-unverified` | P1 / bounded |
| [C09](#c09) | Ability blocking and topology gaps | `interaction-unverified` | P1 / bounded |
| [C10](#c10) | Genuine four-slot doubles owner | `implemented-with-bounded-tests` | P1 / independent-block |
| [C11](#c11) | Doubles targets and multi-target effects | `implemented-with-bounded-tests` | P1 / independent-block |
| [C12](#c12) | Switch, Baton Pass, phasing and faint ownership | `implemented-with-bounded-tests` | P2 / bounded |
| [C13](#c13) | Run and ordinary capture transaction | `implemented-with-bounded-tests` | P2 / bounded |
| [C14](#c14) | Remaining balls and special battle modes | `missing` | P2 / independent-block |
| [C15](#c15) | Bag medicine, flute and remaining battle items | `interaction-unverified` | P2 / bounded |
| [C16](#c16) | Held-item effects and ownership | `interaction-unverified` | P2 / bounded |
| [C17](#c17) | EXP, EV, levels, move learning and settlement | `interaction-unverified` | P2 / bounded |
| [C18](#c18) | Live versus permanent state and persistence | `implemented-with-bounded-tests` | P1 / bounded |
| [C19](#c19) | Trainer AI and legal actions | `interaction-unverified` | P2 / bounded |
| [C20](#c20) | FireRed field abilities and post-battle Pickup | `implemented-with-bounded-tests` | P2 / bounded |
| [C21](#c21) | Battle event text, controls and presentation routing | `interaction-unverified` | P2 / bounded |
| [C22](#c22) | Move/ability animation and audio inventory | `interaction-unverified` | P2 / independent-block |
| [C23](#c23) | Useful refactor: shared rule policy and effect hooks | `interaction-unverified` | P0 / bounded-enabling-work |

<a id="c01"></a>
### C01 — Authenticated move definitions and recipe admission

354 authenticated definitions; 354 registered singles recipes; 336 direct Metronome choices plus six distinct indirect Mirror Move results. Admission is not effect/interaction/campaign completeness.

Evidence: `src/Game.Content/PretMetronomeMoveCatalog.cs`, `src/Game.Domain/WildBattle/WildBattleCalledMoveKernel.cs`, `src/Game.Domain/WildBattle/MetronomeSourceMoveDefinition.cs`.
Bounded tests/evidence: `tests/Game.MetronomeMetadata.Tests/Program.cs`, `tests/Game.NestedMoveCallers.Tests/Program.cs`.
Pinned source: `src/data/battle_moves.h`, `data/battle_scripts_1.s`, `include/constants/moves.h`.

Next acceptance:

- Keep definition authentication, registration, source-branch execution, complete-turn interaction, Application and presentation as separate fields.
- Do not filter/reroll a legal random result or infer completion from battleEffectImplemented metadata.

<a id="c02"></a>
### C02 — Mimic and Sketch

Implemented through the shared singles invocation kernel. Mimic holds an independent permanent slot while the copied live slot spends temporary PP; Sketch follows the original all-slot controller write. Controlled tests pass 16 groups / 392 assertions / 14 complete turns, including both actor sides, Sleep Talk, switch/faint/escape/capture, Transform, learning and ordinary field save/Continue. This is not natural Smeargle admission or native move-rendering acceptance.

Dependencies: C01, C18.

Evidence: `src/Game.Domain/WildBattle/WildBattleCalledMoveKernel.cs`, `src/Game.Domain/WildBattle/WildBattleCalledCallerEffects.cs`, `src/Game.Domain/WildBattle/WildBattleTransformationRules.cs`.
Bounded tests/evidence: `tests/Game.CopyMoveRules.Tests/README.md`, `tests/Game.CopyMoveRules.Tests/SOURCE_CONTRACT.md`.
Pinned source: `data/battle_scripts_1.s:BattleScript_EffectMimic`, `data/battle_scripts_1.s:BattleScript_EffectSketch`, `src/battle_script_commands.c:Cmd_mimicattackcopy`.

Next acceptance:

- Exercise authentic newly admitted donors through the centralized catalog contract.
- Carry copy-slot lifetime and history into genuine four-slot execution; verify dedicated presentation separately.

<a id="c03"></a>
### C03 — Focus Punch and Snatch

Focus Punch preparation and Snatch interception now execute through the shared singles turn owners, with distinct PP ownership and source move-end registers. Focused coverage includes all 44 steal flags, copied short movesets, real canonical Pressure donors, forced switching and suspended-state validation. See `tests/Game.PreparedMoveActions.Tests/README.md`. Genuine four-slot and Truant combinations remain subsequent increments.

Dependencies: C04, C18.

Evidence: `src/Game.Domain/WildBattle/WildBattleCalledMoveKernel.cs`, `src/Game.Domain/WildBattle/WildBattleCalledCallerEffects.cs`, `src/Game.Domain/Trainers/SourceTrainerExecution.cs`, `src/Game.Domain/WildBattle/WildBattleSourceWildTurns.cs`.
Bounded tests/evidence: `tests/Game.NestedMoveCallers.Tests/Program.cs`.
Pinned source: `src/battle_main.c:RunTurnActionsFunctions`, `data/battle_scripts_1.s:BattleScript_EffectFocusPunch`, `data/battle_scripts_1.s:BattleScript_EffectSnatch`, `src/battle_script_commands.c:Cmd_attackcanceler`, `src/battle_script_commands.c:Cmd_trysetsnatch`.

Next acceptance:

- Integrate the Truant cancellation/preparation hook and verify dedicated rendered cues.
- Lift the verified singles ownership/PP/history path to multiple eligible interceptors in genuine doubles.

<a id="c04"></a>
### C04 — Singles order, priority, speed and ties

Shared action/end-turn comparator covers selected priority, current chosen slot, weather speed, stages, badges, paralysis and tie RNG in the admitted singles profile.

Dependencies: C01.

Evidence: `src/Game.Domain/WildBattle/WildBattleSourceEndTurn.cs`, `src/Game.Domain/Trainers/SourceTrainerExecution.cs`, `src/Game.Domain/WildBattle/WildBattleSourceWildTurns.cs`.
Bounded tests/evidence: `tests/Game.SourceEndTurn.Tests/README.md`, `tests/Game.SourceTrainerTurns.Tests/README.md`, `tests/Game.SourceWildTurns.Tests/README.md`.
Pinned source: `src/battle_main.c:GetWhoStrikesFirst`, `src/battle_main.c:SetActionsAndBattlersTurnOrder`.

Next acceptance:

- Retain the slot-three Baton Pass/Transform regression: absent compact-array slot means source MOVE_NONE, not the old chosen move ID.
- Quick Claw/Macho Brace now share canonical singles entry/action/end-turn speed calculation; Macho Brace now also changes four-slot order and canonical reward EVs; other four-slot action items remain separate. See `docs/source-order-held-items.md`.
- For four battlers, preserve source comparator invocation order and every tie draw; do not substitute a generic stable-sort policy.

<a id="c05"></a>
### C05 — Residual and delayed callback scheduling

One shared phase cursor serves singles and four-slot weather, screen, status and end-turn-ability commands. Four-slot binding residuals, Future Sight/Doom Desire, bounded reward/learning pauses and end-turn KO entry now resume without replaying ordinary residuals. Wish, Perish and other unported four-slot residuals remain gated.

Dependencies: C04.

Evidence: `src/Game.Domain/WildBattle/WildBattleSourceEndTurn.cs`, `src/Game.Domain/WildBattle/WildBattleCalledVolatileEffects.cs`, `src/Game.Domain/Trainers/SourceTrainerExecution.cs`, `src/Game.Domain/WildBattle/WildBattleSourceWildTurns.cs`.
Bounded tests/evidence: `tests/Game.SourceEndTurn.Tests/evidence/result.json`, `tests/Game.SourceTrainerTurns.Tests/evidence/run2.log`, `tests/Game.SourceWildTurns.Tests/evidence/run-3.log`.
Pinned source: `src/battle_main.c:HandleEndTurn_ContinueBattle`, `src/battle_util.c:DoFieldEndTurnEffects`, `src/battle_util.c:HandleFaintedMonActions`.

Next acceptance:

- Extend the existing cursor, not a second residual engine; prove no duplicated damage, item activation, EXP or RNG across every yield.
- Add missing end-turn ability branches and four-slot ordering before widening admission.
- Prove whole-team exhaustion, simultaneous faint, Spikes replacement chain and slot-based delayed target behavior.

<a id="c06"></a>
### C06 — Move effect groups and cross-state interactions

Shared generic, recovery, field, turn-lock and party recipes retain bounded source tests. The dirty 16a45ea-based integration adds Counter, Mirror Coat and Vital Throw plus 12 selected confusion/trapping moves to the previous 211 admitted recipes. Static four-slot admission is 228/354 (126 gated). Actual retaliation and volatile-target evidence passes 13 groups/922 checks and 16 groups/616 checks/14 complete turns respectively; this is not complete interaction acceptance.

Dependencies: C01, C02, C03, C07, C08, C09.

Evidence: `src/Game.Domain/WildBattle/WildBattleGenericEffects.cs`, `src/Game.Domain/WildBattle/WildBattleStatRecoveryEffects.cs`, `src/Game.Domain/WildBattle/WildBattleScalarDamageEffects.cs`, `src/Game.Domain/WildBattle/WildBattleCalledFieldEffects.cs`, `src/Game.Domain/WildBattle/WildBattleCalledTurnLocks.cs`, `src/Game.Domain/WildBattle/WildBattleCalledVolatileEffects.cs`, `src/Game.Domain/WildBattle/WildBattleCalledPartyEffects.cs`.
Bounded tests/evidence: `docs/called-move-implementation.md`, `tests/Game.CalledScalarDamage.Tests/evidence/verification.json`, `tests/Game.CalledPartyEffects.Tests/evidence/result.json`.
Pinned source: `data/battle_scripts_1.s`, `src/battle_script_commands.c`, `src/battle_util.c`.

Next acceptance:

- Choose source branch equivalence classes, not an unbounded Cartesian product: substitute/protect/soundproof/safeguard; immunity/absorption; contact versus direct HP; lock/inability; weather/ability suppression; switch/history.
- Exercise both learned and naturally selected caller paths through the actual turn owners; keep controlled vectors distinct from natural campaigns.

<a id="c07"></a>
### C07 — Ability entry, weather and forms

Shared actual initial/KO-entry recipes now cover Drizzle, Drought, Sand Stream, Intimidate, Trace and Forecast, with real four-slot occupant tokens, source tie RNG and ordered pending callbacks. Voluntary/Baton/Roar entry and Spikes ordering now have bounded actual coverage. Color Change shares its canonical recipe with the four-slot owner; remaining move-end immunity and full AI/Application combinations remain open.

Dependencies: C01, C05, C18.

Evidence: `src/Game.Domain/WildBattle/WildBattleRoute3Execution.cs`, `src/Game.Domain/WildBattle/WildBattleCalledFieldEffects.cs`, `src/Game.Domain/WildBattle/WildBattleCalledPartyEffects.cs`, `src/Game.Domain/WildBattle/WildBattleTransformationRules.cs`.
Bounded tests/evidence: `tests/Game.CalledFieldEffects.Tests/README.md`, `tests/Game.CalledPartyEffects.Tests/README.md`.
Pinned source: `src/battle_util.c:AbilityBattleEffects`, `src/battle_util.c:CastformDataTypeChange`, `src/battle_main.c:TryDoEventsBeforeFirstTurn`.

Next acceptance:

- Carry the tested weather register and shared queries into full four-slot entry and residual ownership without duplicating recipes.
- Complete remaining copied-ability/move-end combinations while preserving the tested Color Change, Trace/Forecast and actual occupant lifecycle.
- Admit authentic ability fixtures without fabricating ability provenance or silently unlocking unrelated campaign species.

<a id="c08"></a>
### C08 — Ability damage, absorption, contact and residual gaps

Authentic mechanics donors execute Huge Power, Pure Power, Hustle, Marvel Scale and Pressure, plus Volt Absorb, Rough Skin and Flame Body through shared paths. Hustle now has 3 cases/196 checks covering all four actor positions, physical-type damage/status accuracy cutoffs, unaffected special accuracy and one whole four-action turn. Truant and Speed Boost retain authentic-donor complete-turn/lifecycle tests; Rain Dish retains bounded canonical coverage; Color Change now also has bounded actual four-slot shared-recipe coverage. Ability labels remain 36 bounded/40 interaction-unverified; broader combinations remain separate.

Dependencies: C04, C05, C07.

Evidence: `src/Game.Domain/WildBattle/ForestBattleDamage.cs`, `src/Game.Domain/WildBattle/WildBattleScalarDamageEffects.cs`, `src/Game.Domain/WildBattle/WildBattleRoute11Execution.cs`, `src/Game.Domain/WildBattle/WildBattleSourceEndTurn.cs`, `src/Game.Domain/WildBattle/WildBattleGenericEffects.cs`.
Bounded tests/evidence: `tests/Game.CalledScalarDamage.Tests/README.md`, `tests/Game.SourceEndTurn.Tests/README.md`.
Pinned source: `src/pokemon.c:CalculateBaseDamage`, `src/battle_util.c:AbilityBattleEffects`, `src/battle_util.c:AtkCanceller_UnableToUseMove`.

Next acceptance:

- Admit authentic new ability donors and verify the implemented arithmetic in complete turns.
- Reuse contact/HP/status events; distinguish substitute hits, recoil, indirect damage, actor faint and immunity.
- Add first-turn Speed Boost, weather-suppressed Rain Dish and Truant cancellation/residual counter cases.

<a id="c09"></a>
### C09 — Ability blocking and topology gaps

Shadow Tag and Suction Cups have authenticated canonical singles execution. M04 now provides learned/called target selection, Gen III Lightning Rod redirection and Plus/Minus shared generic damage on actual four slots. Full trapping/presence and unported recipe interactions remain open. Actual four-slot cases now bound Limber, Immunity, Synchronize, Clear Body, Water Veil and Soundproof; primitive-only Hyper Cutter/White Smoke vectors do not upgrade their ledger status.

Dependencies: C10, C11.

Evidence: `src/Game.Domain/WildBattle/WildBattleEarlyPartyExecution.cs`, `src/Game.Domain/WildBattle/WildBattleSourceWildSwitches.cs`, `src/Game.Domain/WildBattle/WildBattleRules.cs`, `src/Game.Domain/WildBattle/WildBattleCalledMoveKernel.cs`, `src/Game.Domain/Trainers/CeruleanTrainerActionAi.cs`.
No positive execution acceptance is credited for this missing/enabling scope.
Pinned source: `data/battle_scripts_1.s:BattleScript_EffectRoar`, `src/battle_main.c:IsRunningFromBattleImpossible`, `src/battle_util.c:GetMoveTarget`, `src/pokemon.c:CalculateBaseDamage`.

Next acceptance:

- Add source Suction Cups phasing failure and Shadow Tag switch/run checks without later-generation immunity changes.
- Implement original Lightning Rod redirection and Plus/Minus field conditions after genuine slots/targets exist; do not add later-generation Electric absorption or stat boosts.
- Prove Pressure cost by source target mode/count and ability overlay provenance when more than one eligible battler exists.

<a id="c10"></a>
### C10 — Genuine four-slot doubles owner

M01 identities through M06 reward/learning and end-of-turn KO replacement are integrated. Initial, KO and voluntary/Baton/Roar entry share weather/Trace/Intimidate/Forecast recipes. Four choices, numeric immunity, Follow Me/Helping Hand, Rollout/Disable/Defense Curl/Minimize, multihit and Cute Charm are integrated. Authentic Route8 Twins roster/default moves/CHECK_BAD_MOVE AI now has a full-original-party public victory/save branch, source insufficient-team and sight admission, and controlled exhausted-PP Struggle defeat through nurse restoration, save/reload and retry (138 commands/284 checks).

The first branch used public PC preparation; the current branch retains all six earned creatures and its Pecha Berry. Seven active and five source-inert berries and forced Struggle are integrated. Source-map positioning and controlled defeat HP/PP are explicit fixtures, not a newly walked route. Ordinary medicine now has bounded public and native four-slot acceptance. Remaining battle item categories, recipes/residuals/callers, expanded AI and wider dedicated doubles animations remain open. See `docs/preset-doubles-route8.md` for current presentation evidence and boundaries.
Dependencies: C04, C05, C18.

Evidence: `src/Game.Domain/Battle/SourceFourActionTurns.cs`, `src/Game.Domain/Battle/SourceFourRewards.cs`, `src/Game.Application/CampaignDoubleBattle.cs`, `tests/Game.Route8TwinsCampaign.Tests/README.md`, `tests/Game.FourStruggle.Tests/README.md`.
Pinned source: `include/constants/battle.h`, `src/battle_main.c:SetActionsAndBattlersTurnOrder`, `src/battle_util.c:HandleFaintedMonActions`.

Next acceptance:

- Complete remaining reusable effect/entry families and explicit held-item boundaries.
- Integrate broader trainer AI and battle bag commands without changing existing single-battle outcomes.
- Verify original dedicated doubles animations independently of functional UI input and settled geometry.

<a id="c11"></a>
### C11 — Doubles targets and multi-target effects

Real selected/random/side/user/both-opponent targeting, Lightning Rod and Plus/Minus queries, generic spread/Explosion and original doubles screen damage are implemented. Follow Me and Helping Hand now share original lifetime, target precedence and damage rules. Deferred-KO no-target actions retain source cancellation/PP/history behavior. Callers and additional multi-target effect families remain open.
Dependencies: C10.

Evidence: `src/Game.Domain/WildBattle/WildBattleCalledMoveKernel.cs`, `src/Game.Domain/WildBattle/WildBattleCalledPartyEffects.cs`, `src/Game.Domain/WildBattle/WildBattleGenericEffects.cs`, `src/Game.Domain/WildBattle/ForestBattleDamage.cs`.
Positive bounded execution is covered by the targeting/spread, ally-support and real Twins suites; additional families below remain open.
Pinned source: `src/battle_util.c:GetMoveTarget`, `src/battle_script_commands.c:Cmd_attackcanceler`, `src/battle_script_commands.c:Cmd_trysethelpinghand`, `src/pokemon.c:CalculateBaseDamage`.

Next acceptance:

- Extend shared target classes to remaining recipes while retaining tested empty/fainted target and retargeting behavior.
- Add Teeter Dance/Perish Song and remaining per-target secondary families to the established Follow Me/Lightning Rod/Helping Hand, ally attack and Explosion boundaries.
- Use FireRed spread/screen rules and RNG consumption; no later-generation damage reduction or priority semantics.

<a id="c12"></a>
### C12 — Switch, Baton Pass, phasing and faint ownership

Actual party arrays and indices, Baton Pass subset, Roar/Whirlwind random reserve selection, Pursuit, Toxic counter reset, Natural Cure and source faint continuations are implemented for singles. The bounded four-slot owner now shares voluntary/Baton/Roar switching and entry, with pending action cancellation and slot relationship cleanup; Spikes and wider switching combinations remain gated.

Dependencies: C07, C09, C10.

Evidence: `src/Game.Domain/WildBattle/WildBattleCalledPartyEffects.cs`, `src/Game.Domain/WildBattle/WildBattleSourceWildSwitches.cs`, `src/Game.Domain/Trainers/SourceTrainerExecution.cs`, `src/Game.Application/CampaignSourceTrainerParty.cs`, `src/Game.Application/CampaignSourceWildParty.cs`.
Bounded tests/evidence: `tests/Game.CalledPartyEffects.Tests/evidence/result.json`, `tests/Game.SourceTrainerTurns.Tests/README.md`, `tests/Game.SourceWildTurns.Tests/README.md`, `tests/Game.SourceCampaignProjection.Tests/evidence/run-20261010-1817/result.json`.
Pinned source: `src/battle_script_commands.c:Cmd_switchindataupdate`, `src/battle_util.c:HandleFaintedMonActions`, `data/battle_scripts_1.s:BattleScript_EffectBatonPass`.

Next acceptance:

- Extend only uncovered ability/held-item interactions; preserve identical-data distinct identities and no obsolete selected priority after copied moves.
- Prove incoming entry effects, Spikes death loops, pursuit KO and interruption resume on both owners and four slots.

<a id="c13"></a>
### C13 — Run and ordinary capture transaction

Source wild owner routes Run/capture with retained opponent and RNG; Ghost/Marowak/Snorlax restrictions and party/PC identity exist. Capture core supports Master/Ultra/Great/Poke only.

Evidence: `src/Game.Domain/WildBattle/WildBattleSourceWildTurns.cs`, `src/Game.Domain/WildBattle/WildBattleSourceWildItems.cs`, `src/Game.Domain/Capture/CaptureRules.cs`, `src/Game.Application/CampaignCapture.cs`.
Bounded tests/evidence: `tests/Game.SourceWildTurns.Tests/README.md`, `tests/Game.CaptureAudit.Tests/README.md`, `tests/Game.TransformCampaign.Tests/README.md`.
Pinned source: `src/battle_main.c:TryRunFromBattle`, `src/battle_script_commands.c:Cmd_handleballthrow`.

Next acceptance:

- Retain failed-throw enemy action, run failure, ball inventory atomicity and exact generated wild identity.
- Test trapping and Run Away after new ability admission; capture and run remain separate from doubles/special modes.

<a id="c14"></a>
### C14 — Remaining balls and special battle modes

Capture enum includes later balls and Safari but CaptureRules rejects every ball >= Safari and all non-ordinary encounter kinds; encounter profiles reject active roamers.

Dependencies: C13.

Evidence: `src/Game.Domain/Capture/CaptureRules.cs`, `src/Game.Domain/Capture/CaptureContracts.cs`, `src/Game.Domain/Encounters/EarlyLandEncounterRules.cs`.
Bounded tests/evidence: `tests/Game.CaptureAudit.Tests/README.md`.
Pinned source: `src/battle_script_commands.c:Cmd_handleballthrow`, `src/battle_main.c:HandleAction_ThrowBait`, `src/battle_main.c:TryRunFromBattle`, `src/wild_encounter.c`.

Next acceptance:

- Complete applicable Gen III ball modifiers with actual encounter context and source capture RNG; keep unavailable acquisition distinct from rule support.
- Treat Safari action loop and roaming encounters as independently bounded blocks with their story/field dependencies; report those dependencies before claiming the battle stage complete.
- Do not turn unsupported trainer/double/link capture into ordinary capture.

<a id="c15"></a>
### C15 — Bag medicine, flute and remaining battle items

Shared ordinary medicine now executes in the genuine four-slot owner and public Twins Application. Selection immediately applies owned HP/status/inventory changes and records prepared markers; execution does not apply or spend twice. Partner/bench use, two medicines, capped HP, no-effect/stale identity/overflow atomicity and continuation pass 3 cases/102 checks/44 commands. Main X11 native BAG/party/input/presentation verification passes 223 checks with source and DLL identities unchanged across the run and exit 0. Flute, revival, PP and stat items remain separate doubles gaps; physical audio is unverified.

Dependencies: C05, C18.

Evidence: `src/Game.Domain/Items/SourceBattleItemRules.cs`, `src/Game.Domain/Trainers/SourceTrainerItems.cs`, `src/Game.Domain/WildBattle/WildBattleSourceWildItems.cs`, `src/Game.Domain/Items/EarlyShopMedicineRules.cs`.
Bounded tests/evidence: `tests/Game.SourceTrainerItems.Tests/README.md`, `tests/Game.SourceWildTurns.Tests/README.md`, `tests/Game.BattleMedicineCampaign.Tests/README.md`, `evidence/shared-battle-families-20261011/double-medicine-main-02/result.json`, `evidence/shared-battle-families-20261011/native/result.json`, `evidence/shared-battle-families-20261011/native/runner-result.json`.
Pinned source: `src/battle_main.c:HandleAction_UseItem`, `src/battle_script_commands.c:Cmd_various`, `src/item_use.c`.

Next acceptance:

- Inventory actual battle-use items; add missing X-item/Guard Spec/Dire Hit/revival/PP paths only after checking existing medicine implementations and content admission.
- Extend the verified active/reserve identity, no-effect atomicity, source selection-time use and prepared-marker continuation contract to each newly admitted item category; retain the ban on player battle saves.

<a id="c16"></a>
### C16 — Held-item effects and ownership

Canonical singles Choice Band now executes physical Attack scaling, chosen-move locking, called/copied moves, item transfer and departure cleanup. Quick Claw/Macho Brace order and reward modifiers are shared. Canonical singles Focus Band now shares post-random damage adjustment, slot-sticky protection and actual ActionFinished cleanup across ordinary, scalar, confusion and delayed attacks. General four-slot held-item execution remains unfinished. See `docs/source-choice-band.md`.

Dependencies: C04, C05, C18.

Evidence: `src/Game.Domain/WildBattle/WildBattleCalledPartyEffects.cs`, `src/Game.Domain/WildBattle/WildBattleScalarDamageEffects.cs`, `src/Game.Domain/WildBattle/WildBattleSourceEndTurn.cs`, `src/Game.Domain/WildBattle/ForestBattleDamage.cs`, `src/Game.Domain/WildBattle/FuchsiaBattleScope.cs`.
Bounded tests/evidence: `tests/Game.SourceTrainerItems.Tests/README.md`, `tests/Game.CalledPartyEffects.Tests/README.md`, `tests/Game.RockTunnelHeldItemRecovery.Tests/README.md`, `tests/Game.CalledTurnLocks.Tests/README.md`.
Pinned source: `src/battle_util.c:ItemBattleEffects`, `src/battle_main.c:GetWhoStrikesFirst`, `src/pokemon.c:CalculateBaseDamage`.

Next acceptance:

- Implement remaining King's Rock/Shell Bell, herbs and applicable berries/species modifiers by effect group.
- Prove threshold/RNG timing, Choice lock after copied moves, consumed-item history, permanent versus battle-only Knock Off and wild/trainer restoration.
- An item constant, a helper branch or a species held-item slot is not full execution admission.

<a id="c17"></a>
### C17 — EXP, EV, levels, move learning and settlement

The authenticated source reward calculator and M06 four-slot owner now apply per-opponent participation, ordered EXP/EV and real learning pauses to the authoritative permanent party, including both active slots full stat refresh. Empty level100/all-fainted plans, shared reward items and exact-once continuation are covered. Canonical Application, evolution/callback integration and ownership admission remain separate. See `docs/source-victory-rewards.md` and `tests/Game.SourceRewards.Tests/README.md`.

Dependencies: C05, C16, C18.

Evidence: `src/Game.Domain/WildBattle/WildVictoryRewards.cs`, `src/Game.Domain/Growth/GrowthRules.cs`, `src/Game.Domain/Pokemon/LevelEvolutionRules.cs`, `src/Game.Domain/Trainers/ForestTrainerCombatExecution.cs`, `src/Game.Application/CampaignPartyTraining.cs`.
Bounded tests/evidence: `tests/Game.WildRewards.Tests/Program.cs`, `tests/Game.ForestGrowthAudit.Tests/README.md`, `tests/Game.FuchsiaRewards.Tests/README.md`, `tests/Game.SourceTrainerTurns.Tests/README.md`.
Pinned source: `src/battle_script_commands.c:Cmd_getexp`, `src/pokemon.c:MonGainEVs`, `src/pokemon.c:IsTradedMon`.

Next acceptance:

- Connect the bounded four-slot reward owner to actual Application/UI and authoritative owner/Pokerus facts; do not manufacture absent metadata.
- Prove fainted/level100 exclusions, participant identity after replacement, multi-level learning/decline and evolution/callback resume exactly once.
- Separate rule support for traded ownership from actual communications; test obedience once such ownership is admitted.

<a id="c18"></a>
### C18 — Live versus permanent state and persistence

Explicit source owners preserve real full-party projection, Transform identity/original PP, Trick items and canonical Toxic. Player saves stay outside battle; diagnostic serialization is not an in-battle save feature.

Evidence: `src/Game.Application/CampaignSourceTrainerParty.cs`, `src/Game.Application/CampaignSourceWildParty.cs`, `src/Game.Application/CampaignFuchsiaGymSaveUpgrade.cs`, `src/Game.Application/SaveCodec.cs`, `src/Game.Domain/WildBattle/WildBattleTransformationRules.cs`, `src/Game.Domain/WildBattle/WildBattleCalledPartyEffects.cs`.
Bounded tests/evidence: `tests/Game.SourceCampaignProjection.Tests/evidence/run-20261010-1817/result.json`, `tests/Game.SourceTrainerValidation.Tests/README.md`, `tests/Game.TransformCampaign.Tests/README.md`.
Pinned source: `src/battle_script_commands.c:Cmd_switchindataupdate`, `src/battle_main.c:BattleStartClearSetData`, `src/battle_main.c:FreeResetData_ReturnToOvOrDoEvolutions`.

Next acceptance:

- Carry new permanent Sketch, item and ownership changes through one projection boundary; battle-only counters/forms/slots must not leak into owned records.
- Keep old rules/version/hash/profile rejection and explicit migration; stable field save/fresh Continue is required after each new persistent feature.
- A suspended battle may serialize for diagnostics but cannot become a player save.

<a id="c19"></a>
### C19 — Trainer AI and legal actions

Source adapter reuses bounded original scoring/switch/item/replacement logic, live overlays, known/unknown abilities and source RNG quirks; not all trainer AI flags or doubles AI.

Dependencies: C02, C03, C09, C10, C16.

Evidence: `src/Game.Domain/Trainers/SourceTrainerAi.cs`, `src/Game.Domain/Trainers/CeruleanTrainerAi.cs`, `src/Game.Domain/Trainers/CeruleanTrainerActionAi.cs`, `src/Game.Domain/WildBattle/WildBattleCalledCallerEffects.cs`.
Bounded tests/evidence: `tests/Game.SourceTrainerAi.Tests/README.md`, `tests/Game.CalledSelection.Tests/README.md`.
Pinned source: `data/battle_ai_scripts.s`, `src/battle_ai_script_commands.c`, `src/battle_ai_switch_items.c`.

Next acceptance:

- Inventory used source AI flags and effect dispatch; add missing newly admitted effects/abilities without inventing a stronger strategy.
- Prove PP/Disable/Encore/Imprison/Taunt/Torment/choice locks and forced Struggle; test no illegal action choice or RNG on rejected commands.
- Add doubles target/ally scoring and replacement only over actual four-slot state.
- Inspect ownership/obedience explicitly: the current cancellation path marks Obeys, which is not a disobedience engine.

<a id="c20"></a>
### C20 — FireRed field abilities and post-battle Pickup

Stench/Illuminate modify encounter checks; Pickup checks real party in order after victory, with empty-item/egg gates and original item buckets. These are the observed FireRed field/post-battle ability hooks.

Evidence: `src/Game.Domain/Encounters/EarlyLandEncounterRules.cs`, `src/Game.Domain/Encounters/EncounterRateRng.cs`, `src/Game.Domain/Pokemon/VermilionPickupRules.cs`.
Bounded tests/evidence: `tests/Game.MtMoonEncounterRecovery.Tests/Program.cs`, `tests/Game.PokemonTowerFieldRules.Tests/Program.cs`, `tests/Game.VermilionBattle.Tests/Program.cs`, `tests/Game.VermilionCampaign.Tests/Program.cs`.
Pinned source: `src/wild_encounter.c:GetAbilityEncounterRateModType`, `src/wild_encounter.c:TryStandardWildEncounter`, `src/battle_script_commands.c:Cmd_pickup`.

Next acceptance:

- Retain lead slot/egg/held-item and fixed-point/RNG semantics as encounter and party admission expands.
- Keep FireRed source applicability explicit: do not add Emerald-only Synchronize nature, Compound Eyes held-item, Flame Body/Magma Armor hatch or other later field bonuses.
- Recheck Pickup after new terminal modes and transferred items rather than replacing its tested table.

<a id="c21"></a>
### C21 — Battle event text, controls and presentation routing

The actual campaign view has bounded four-slot healthbox, target/party/replacement and ordinary medicine BAG controls. Main X11 medicine verification passes 223 checks with unchanged source/DLL identity and exit 0. Ten dedicated scripts now have bounded actual four-slot kernel animation dispatch evidence. The current focused Counter/Vital Throw batch passes 99 checks with 20 real four-slot cases; four same-side Counter cases remain visual-only. Tail Whip/Flash pass 151 checks with eight real four-slot stat invocations. Earlier native runs retain their separate source identities; no combined current-main native replay is claimed. Main headless stat presentation passes 4,455 checks (three real singles/four real four-slot cases); a separate worker X11 Flash sample passes 6,443 checks/exit 0. These retain distinct executable/source identities and Dummy-audio limits.

Dependencies: C10, C11, C18.

Evidence: `src/Game.Godot/SourceTrainerPresentationModel.cs`, `src/Game.Godot/CampaignSourceTrainerPresentationView.cs`, `src/Game.Godot/CampaignWildBattleView.cs`, `src/Game.Godot/CampaignForestTrainerView.cs`.
Bounded tests/evidence: `tests/Game.FuchsiaGymPresentation.Tests/README.md`, `tests/Game.FuchsiaGymUi.Tests/README.md`.
Pinned source: `src/battle_message.c`, `src/battle_controller_player.c`, `src/battle_controller_opponent.c`.

Next acceptance:

- Use typed event/cue mapping with exact owner/battler identity and invocation deduplication; preserve authoritative ACK sequencing.
- Extend bounded four-slot controls and presentation to newly admitted effect/item families, preserving cancellation, repeated input, owner identity and authoritative ACK sequencing.
- Verify real Godot and native inputs separately from the BCL text adapter and headless draw probes.

<a id="c22"></a>
### C22 — Move/ability animation and audio inventory

All 354 source animation roots retain an authenticated conservative dependency inventory. Ten bounded dedicated scripts are accepted: Pound, Tackle, Scratch, Cut, Slash, False Swipe, Counter, Vital Throw, Tail Whip and Flash; 344 lack that layer. Current Counter/Vital Throw dispatch has 20 actual four-slot rule cases and four explicitly visual-only same-side Counter fixtures. Tail Whip/Flash add 151 checks, 2,730 source-comparison frames and 1,671,168 RGB555 vectors, with real singles/four-slot stat invocations. Main comparisons reuse hashed fixed original-C output from the worker rather than rerunning C. Prior eight-script frame/native evidence remains historical; original-ROM pixel equality and physical listening remain unverified. Main headless stat presentation passes 4,455 checks (three real singles/four real four-slot cases); a separate worker X11 Flash sample passes 6,443 checks/exit 0. These retain distinct executable/source identities and Dummy-audio limits.

Dependencies: C21.

Evidence: `src/Game.Godot/CampaignAudioView.cs`, `src/Game.Godot/SynthSourceAudioPlayer.cs`, `src/Game.Godot/OriginalAudioCore_Sequence.cs`, `src/Game.Godot/OriginalAudioCore_Synth.cs`, `docs/synth-audio-integration.md`, `docs/fuchsia-arrival.md`.
Bounded tests/evidence: `tests/Game.FuchsiaGymPresentation.Tests/README.md`, `docs/source-ui-integration.md`.
Pinned source: `data/battle_anim_scripts.s`, `src/battle_anim.c`, `src/battle_gfx_sfx_util.c`, `src/battle_message.c`.

Next acceptance:

- Extend the authenticated script inventory through C callback execution and actual per-move renderer/sound timing acceptance; list required ability cues only where FireRed has them.
- Use a dedicated test battle to trigger success/failure/miss/protect/substitute/status/weather/charge/release/contact/faint across body sizes and 1v1/2v2 slots.
- Wire rules and their corresponding cues in the same bounded increment while reporting rule, rendered animation and audio acceptance separately.
- Retain the physical-audio blocker; synthesized PCM and Dummy requests are not audible playback proof.

<a id="c23"></a>
### C23 — Useful refactor: shared rule policy and effect hooks

Shared Execution, PP/caller recipes, SourceBattleItemRules, source end-turn cursor and permanent-party projection remain the shared seams. Explicit authenticated mechanics capability now separates386-species source definitions from107-species campaign admission, reusing existing factory/stat algorithms. Four-slot identity, bounded shared recipes and clean turn ownership now exist. Remaining effect/phase migration consumes these shared interfaces.

Evidence: `src/Game.Domain/WildBattle/WildBattleCalledMoveKernel.cs`, `src/Game.Domain/WildBattle/WildBattleSourceEndTurn.cs`, `src/Game.Domain/WildBattle/WildBattleCalledPartyEffects.cs`, `src/Game.Domain/Items/SourceBattleItemRules.cs`, `src/Game.Domain/WildBattle/ForestBattleDamage.cs`, `src/Game.Domain/WildBattle/WildVictoryRewards.cs`.
Bounded factory/turn parity is recorded in `docs/source-mechanics-admission.md`; serialized-state value equality is covered by `tests/Game.SourcePartyRoundTrip.Tests/README.md`.

Next acceptance:

- Preserve explicit authenticated capabilities for the new profile instead of spreading further species-count checks; retain historical adapters and unchanged old snapshots.
- Separate battler slot from side through a narrow state/access interface, then port shared effect helpers once instead of cloning a doubles engine.
- Consolidate entry/absorption/contact/immunity/move-end/end-turn/field ability and held-item dispatch by source phase; do not create a monolithic rewritten battle engine.
- Factor shared pause/resume orchestration only where trainer/wild duplication demonstrably causes inconsistent behavior; keep mode-specific AI, capture, EXP and outcomes explicit.
- Keep Godot free of rules; render typed cues. Require output/event/RNG parity for every behavior-preserving refactor.

## Official ability inventory

Each of the 76 entries below has source and current execution locations in the JSON. `missing` means the stated runtime requirement is absent, not that its name is absent from metadata. `interaction-unverified` means a branch/helper is present but complete applicability and combinations are not established. The three bounded field/post-battle effects have explicit test references in C20.

| ID | Ability | Status | Work | Specific boundary |
| --- | --- | --- | --- | --- |
| 1 | `ABILITY_STENCH` | `implemented-with-bounded-tests` | C20 | Field encounter modifier exists and has bounded fixed-point/RNG tests. FireRed Stench does not gain a later-generation flinch effect. |
| 2 | `ABILITY_DRIZZLE` | `implemented-with-bounded-tests` | C07 | Shared canonical singles and actual four-slot initial/KO-entry callbacks verified with source ordering, exact occupancy, weather/form suppression and explicit copied-ability lifecycle. General voluntary/Baton/Roar/Spikes and full Application admission remain pending. |
| 3 | `ABILITY_SPEED_BOOST` | `implemented-with-bounded-tests` | C08 | Authentic Ninjask complete-turn tests cover lead versus switched-in timing, stage cap, faint/status order, copied/acquired ability and frame/JSON continuation. Four-slot ordering remains unverified. |
| 4 | `ABILITY_BATTLE_ARMOR` | `implemented-with-bounded-tests` | C06 | Authentic Kabuto critical prevention skips the critical draw in canonical singles and actual four-slot turns; shared armor command retained. |
| 5 | `ABILITY_STURDY` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 6 | `ABILITY_DAMP` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 7 | `ABILITY_LIMBER` | `implemented-with-bounded-tests` | C06 | Authentic Persian prevents Stun Spore before accuracy in an actual selected-slot four-slot kernel action; PP is spent once and no status/RNG mutation occurs. Full-turn combinations, other status sources and presentation remain unverified. |
| 8 | `ABILITY_SAND_VEIL` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 9 | `ABILITY_STATIC` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 10 | `ABILITY_VOLT_ABSORB` | `implemented-with-bounded-tests` | C08 | Both real holder sides, dynamic Electric type, full-HP absorption and shared PP/history verified in canonical singles; no doubles or audible claim. |
| 11 | `ABILITY_WATER_ABSORB` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 12 | `ABILITY_OBLIVIOUS` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 13 | `ABILITY_CLOUD_NINE` | `implemented-with-bounded-tests` | C07 | Authentic canonical donor and all-body versus living-body suppression queries tested; shared recipes consume the same queries. Full doubles weather phases remain pending. |
| 14 | `ABILITY_COMPOUND_EYES` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 15 | `ABILITY_INSOMNIA` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 16 | `ABILITY_COLOR_CHANGE` | `implemented-with-bounded-tests` | C07 | Authenticated canonical singles exact HP-loss receipt, dynamic type, Explosion/recoil/BeatUp/Pursuit, copied abilities and presentation pairing verified. Full four-slot move-end remains gated. |
| 17 | `ABILITY_IMMUNITY` | `implemented-with-bounded-tests` | C06 | Authentic Snorlax prevents Toxic before accuracy in an actual selected-slot four-slot kernel action; PP is spent once and no status/RNG mutation occurs. Full-turn combinations, other poison sources and presentation remain unverified. |
| 18 | `ABILITY_FLASH_FIRE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 19 | `ABILITY_SHIELD_DUST` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 20 | `ABILITY_OWN_TEMPO` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 21 | `ABILITY_SUCTION_CUPS` | `implemented-with-bounded-tests` | C09 | Authentic Octillery/Lileep/Cradily cover Roar/Whirlwind after PP and before Rooted/accuracy, effective ability changes and full source turn continuation; canonical trainer admission remains separate. |
| 22 | `ABILITY_INTIMIDATE` | `implemented-with-bounded-tests` | C07 | Shared canonical and actual four-slot entry callbacks have bounded order/occupant tests. Original earned Gyarados forced replacement additionally reached actual native Intimidate text/stat event in the Twins view. This does not certify all entry/hazard/ability-change combinations. |
| 23 | `ABILITY_SHADOW_TAG` | `implemented-with-bounded-tests` | C09 | Authenticated Wobbuffet/Wynaut and real ability-changing moves cover Run, Teleport, voluntary switch, Baton, forced and faint replacement distinctions; no Gen IV Ghost/self exemption. Four-slot field scan remains separate. |
| 24 | `ABILITY_ROUGH_SKIN` | `implemented-with-bounded-tests` | C08 | One shared recipe now covers authenticated singles and actual four-slot contact, retained receipt guards, per-hit status/KO, reward pauses and delayed replacements. Controlled canonical receipt/Substitute and future Bide boundaries remain documented; this is not complete ability/campaign/animation acceptance. |
| 25 | `ABILITY_WONDER_GUARD` | `implemented-with-bounded-tests` | C06 | Shared type-result/history commands cover normal/fixed/OHKO/retaliation/crash/Present, real Shedinja/Trace/Transform, Focus Band raw adjustment, ordinary source misses and spread independence. 13 combined groups/3747 checks including weather/Serene and utility boundaries. |
| 26 | `ABILITY_LEVITATE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 27 | `ABILITY_EFFECT_SPORE` | `implemented-with-bounded-tests` | C06 | One shared recipe now covers authenticated singles and actual four-slot contact, retained receipt guards, per-hit status/KO, reward pauses and delayed replacements. Controlled canonical receipt/Substitute and future Bide boundaries remain documented; this is not complete ability/campaign/animation acceptance. |
| 28 | `ABILITY_SYNCHRONIZE` | `implemented-with-bounded-tests` | C06 | Authentic Abra receives ally-targeted paralysis/Toxic in actual four-slot kernel actions and reflects to the real actor only; Gen III Toxic reflection becomes ordinary poison. Full-turn combinations, all status sources and presentation remain unverified. |
| 29 | `ABILITY_CLEAR_BODY` | `implemented-with-bounded-tests` | C06 | Authentic Regice blocks its Growl stat drop in a complete four-action turn while the other opposing holder independently uses Soundproof. Source command vectors also cover stat/Mist precedence; Intimidate/secondary/ability-change combinations and presentation are not fully accepted. |
| 30 | `ABILITY_NATURAL_CURE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 31 | `ABILITY_LIGHTNING_ROD` | `implemented-with-bounded-tests` | C09 | M04 original field-query predicates, targeting or shared generic damage verified with real canonical donors. Unmigrated scalar recipes remain atomically rejected; this is not whole-battle/campaign ability closure. |
| 32 | `ABILITY_SERENE_GRACE` | `implemented-with-bounded-tests` | C06 | Shared source secondary-effect command handles multiplier, inclusive roll, certain flag and >=100 semantics; 11 groups/2331 checks and authentic donors/31 complete turns. Approved strict Twineedle retained, Kings Rock independent. |
| 33 | `ABILITY_SWIFT_SWIM` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 34 | `ABILITY_CHLOROPHYLL` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 35 | `ABILITY_ILLUMINATE` | `implemented-with-bounded-tests` | C20 | Field encounter modifier exists with source fixed-point/RNG checks; no battle effect is required by this source. |
| 36 | `ABILITY_TRACE` | `implemented-with-bounded-tests` | C07 | Shared canonical singles and actual four-slot initial/KO-entry callbacks verified with source ordering, exact occupancy, weather/form suppression and explicit copied-ability lifecycle. General voluntary/Baton/Roar/Spikes and full Application admission remain pending. |
| 37 | `ABILITY_HUGE_POWER` | `implemented-with-bounded-tests` | C08 | Real Azumarill source full turn and damage match independently extracted C; four-slot interactions unverified. |
| 38 | `ABILITY_POISON_POINT` | `implemented-with-bounded-tests` | C06 | One shared recipe now covers authenticated singles and actual four-slot contact, retained receipt guards, per-hit status/KO, reward pauses and delayed replacements. Controlled canonical receipt/Substitute and future Bide boundaries remain documented; this is not complete ability/campaign/animation acceptance. |
| 39 | `ABILITY_INNER_FOCUS` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 40 | `ABILITY_MAGMA_ARMOR` | `implemented-with-bounded-tests` | C06 | Authentic Slugma freeze/Tri Attack prevention and actual Trace acquisition with move-end cleanup tested in canonical singles and four slots. |
| 41 | `ABILITY_WATER_VEIL` | `implemented-with-bounded-tests` | C06 | Authentic Seaking prevents Will-O-Wisp before accuracy in an actual selected-slot four-slot kernel action; PP is spent once and no status/RNG mutation occurs. Full-turn combinations, other burn sources and presentation remain unverified. |
| 42 | `ABILITY_MAGNET_PULL` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 43 | `ABILITY_SOUNDPROOF` | `implemented-with-bounded-tests` | C06 | Authentic Mr. Mime blocks its Growl pass in a complete four-action turn while a separate Regice uses Clear Body; only the Soundproof holder skips accuracy. Other sound moves, lifecycle combinations and presentation remain unverified. |
| 44 | `ABILITY_RAIN_DISH` | `implemented-with-bounded-tests` | C08 | Authentic canonical donor heals in the sole source ability phase, with suppression, poison order and JSON tests; four-turn residual dispatch remains pending. |
| 45 | `ABILITY_SAND_STREAM` | `implemented-with-bounded-tests` | C07 | Shared canonical singles and actual four-slot initial/KO-entry callbacks verified with source ordering, exact occupancy, weather/form suppression and explicit copied-ability lifecycle. General voluntary/Baton/Roar/Spikes and full Application admission remain pending. |
| 46 | `ABILITY_PRESSURE` | `implemented-with-bounded-tests` | C09 | Authentic donor single-turn, ally slot equality, spread/all-target stored-ability counts and retained-fainted no-target costs have bounded tests. Forced Struggle calculates the Pressure command but spends no learned PP. All callers and remaining shared recipes still need their own coverage. |
| 47 | `ABILITY_THICK_FAT` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 48 | `ABILITY_EARLY_BIRD` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 49 | `ABILITY_FLAME_BODY` | `implemented-with-bounded-tests` | C08 | One shared recipe now covers authenticated singles and actual four-slot contact, retained receipt guards, per-hit status/KO, reward pauses and delayed replacements. Controlled canonical receipt/Substitute and future Bide boundaries remain documented; this is not complete ability/campaign/animation acceptance. |
| 50 | `ABILITY_RUN_AWAY` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 51 | `ABILITY_KEEN_EYE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 52 | `ABILITY_HYPER_CUTTER` | `implemented-with-bounded-tests` | C06 | Canonical singles now uses shared stat command, matching four-slot direct/secondary/intimidation/certain self-drop branches. |
| 53 | `ABILITY_PICKUP` | `implemented-with-bounded-tests` | C20 | Original victory-only ordered party checks and item buckets exist; test ownership after newly admitted modes/items. |
| 54 | `ABILITY_TRUANT` | `implemented-with-bounded-tests` | C08 | Authentic Slakoth/Slaking complete-turn tests cover raw loafing register, cancellation precedence, ability changes, switches, prepared actions, recharge expiry and serialized continuation. Four-slot and campaign admission remain separate. |
| 55 | `ABILITY_HUSTLE` | `implemented-with-bounded-tests` | C08 | Real Togetic singles damage and physical-accuracy miss turns remain verified. New bounded four-slot evidence covers authentic Corsola in all four actor positions, natural Togetic Charm and controlled Poison Powder physical-type status cutoffs, unaffected Bubble accuracy, one complete four-action turn and stale-turn atomicity (3 cases/196 checks). Wider ability/caller/campaign combinations remain unverified. |
| 56 | `ABILITY_CUTE_CHARM` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 57 | `ABILITY_PLUS` | `implemented-with-bounded-tests` | C09 | M04 original field-query predicates, targeting or shared generic damage verified with real canonical donors. Unmigrated scalar recipes remain atomically rejected; this is not whole-battle/campaign ability closure. |
| 58 | `ABILITY_MINUS` | `implemented-with-bounded-tests` | C09 | M04 original field-query predicates, targeting or shared generic damage verified with real canonical donors. Unmigrated scalar recipes remain atomically rejected; this is not whole-battle/campaign ability closure. |
| 59 | `ABILITY_FORECAST` | `implemented-with-bounded-tests` | C07 | Shared canonical singles and actual four-slot initial/KO-entry callbacks verified with source ordering, exact occupancy, weather/form suppression and explicit copied-ability lifecycle. General voluntary/Baton/Roar/Spikes and full Application admission remain pending. |
| 60 | `ABILITY_STICKY_HOLD` | `implemented-with-bounded-tests` | C06 | Shared item transfer now verifies actual Sticky Hold guard order and four-slot ownership/settlement; broader special modes remain separate. |
| 61 | `ABILITY_SHED_SKIN` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 62 | `ABILITY_GUTS` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 63 | `ABILITY_MARVEL_SCALE` | `implemented-with-bounded-tests` | C08 | Real burned Milotic source defense arithmetic and complete turn verified; four-slot interactions unverified. |
| 64 | `ABILITY_LIQUID_OOZE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 65 | `ABILITY_OVERGROW` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 66 | `ABILITY_BLAZE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 67 | `ABILITY_TORRENT` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 68 | `ABILITY_SWARM` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 69 | `ABILITY_ROCK_HEAD` | `implemented-with-bounded-tests` | C06 | Shared ordinary recoil exempts Rock Head; Struggle and crash remain distinct. Six recoil/crash recipes have actual singles/four-slot and double-KO/reward continuation proof. |
| 70 | `ABILITY_DROUGHT` | `implemented-with-bounded-tests` | C07 | Shared canonical singles and actual four-slot initial/KO-entry callbacks verified with source ordering, exact occupancy, weather/form suppression and explicit copied-ability lifecycle. General voluntary/Baton/Roar/Spikes and full Application admission remain pending. |
| 71 | `ABILITY_ARENA_TRAP` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 72 | `ABILITY_VITAL_SPIRIT` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 73 | `ABILITY_WHITE_SMOKE` | `implemented-with-bounded-tests` | C06 | Canonical singles now uses shared stat command, matching four-slot direct/secondary stat prevention and self-drop boundaries. |
| 74 | `ABILITY_PURE_POWER` | `implemented-with-bounded-tests` | C08 | Real Medicham source full turn and damage match independently extracted C; four-slot interactions unverified. |
| 75 | `ABILITY_SHELL_ARMOR` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 77 | `ABILITY_AIR_LOCK` | `implemented-with-bounded-tests` | C07 | Authentic canonical donor and all-body versus living-body suppression queries tested; shared recipes consume the same queries. Full doubles weather phases remain pending. |

## Guardrails and exit evidence

- **Doubles change is narrowly scoped:** KO replacement moves to the end of the turn as approved. Normal voluntary switching, Roar/Whirlwind and their interception/entry phases keep their source timing. No other Gen IV or later-generation rules are introduced.
- Preserve source integer truncation, target/slot order, RNG purpose/count, permanent identity and atomic rejection. A source transcription oracle is not original ROM execution.
- FireRed field abilities follow the pinned FireRed code. Do not add Emerald-only nature, held-item, hatch-time, fishing or encounter bonuses merely because they are often described as “Gen III abilities.”
- Every new capability needs success, source failure, interruption/resume and historical-profile rejection cases. Validate all actual party members, not just the active visual.
- Keep full-turn learned/caller behavior, controlled diagnostic scenarios, natural campaign admission, real Godot rendering, native input and physical audio as distinct evidence.
- For each finished effect group, preserve exact source references, actual commands, nonempty results and failure history. Native animation/audio scenes may be directly triggered in a test battle; they need not wait for natural move acquisition.
- Do not infer coverage percentages or delivery time from registration totals, test counts or random samples.

## Historical evidence and integration notes (2026-10-10–2026-10-11)

These entries describe the scope at their earlier checkpoints, not the latest dirty-tree acceptance below. Retained evidence was inspected, not rerun for this documentation update.

- `tests/Game.SourceEndTurn.Tests/evidence/result.json`: 21 groups / 405 checks / zero failures; integrated historical evidence, phase contract only.
- `tests/Game.SourceTrainerTurns.Tests/evidence/run2.log`: 5 groups / 514 turns / 727 commands / 495 called transitions; controlled supplied party.
- `tests/Game.SourceWildTurns.Tests/evidence/run-3.log`: 10 groups / 1733 turns / 1635 calls / 332 distinct Metronome results; sampling, not exhaustive closure.
- `tests/Game.SourceCampaignProjection.Tests/evidence/run-20261010-1817/result.json`: 4 groups / 205 checks / 87 commands; controlled Application projection and stable field saves.
- `tests/Game.CalledScalarDamage.Tests/evidence/verification.json`: 20 groups / 2318 checks / zero failures; global interaction closure explicitly false.
- `tests/Game.CalledPartyEffects.Tests/evidence/result.json`: Focused action/switch evidence; retains a separately documented Conversion 2 policy. This inventory neither changes nor newly authorizes that existing policy..

The remaining work should close these named gaps and interaction classes. Further catalog or species admission requires updating this matrix and authenticating the newly reachable branches; it is not permission to widen historical profiles silently.

Truant/Speed Boost bounded integration: `docs/source-turn-cycle-abilities.md` and `evidence/battle-turn-cycle-integration/result.json`. Existing source singles and all old golden outputs remain covered; this does not enable canonical species in player saves or four-action turns.

Shared kernel/weather integration: `evidence/battle-kernel-weather-integration/result.json`. Permanent weather uses the same recipe queries across the singles bridge and canonical action context. The four-action owner currently rejects weather and other nonempty end-turn phases; no general doubles battle is claimed.

Recovery integration (2026-10-11): Quick Claw/Macho Brace and Trace/Forecast were reconstructed after the workspace reset and freshly tested. See `evidence/recovery-20261011/held-trace/result.json`; old unuploaded candidate hashes are not recovered Git identities.

M04/M05 combined recovery increment: actual four-slot learned/called targeting, Lightning Rod, Plus/Minus, generic spread/Explosion and a shared temporary-weather/status/end-turn-ability scheduler now pass together with canonical singles Color Change, Trace/Forecast and held-item order. See `tests/Game.SourceTargeting.Tests/SOURCE_CONTRACT.md`, `docs/source-four-slot-end-turn.md`, `docs/source-color-change-ability.md` and `evidence/recovery-20261011/combined-four/result.json`. EXP/entry continuation, no-target costs, the remaining recipes/residuals/cancellers, AI and campaign doubles remain distinct open work.

Combined presentation increment: Original Pound/Tackle/Scratch/Cut/Slash/False Swipe scripts now run through the actual campaign view with original C callback/frame comparison, current terrain/Transform/substitute art, owned sound cancellation and a single committed ACK. Native X11 viewport coverage is limited to these six moves; sound uses Dummy and actual pan/listening remain unverified. See `docs/source-hit-animation-slice.md` and `evidence/recovery-20261011/hit-combined-proof/RESULT.json`.

M06 integration shares actual EXP/EV/learning and delayed callbacks with the permanent party authority, and combines weather/screens with KO reward and replacement resume. The authoritative continuation version is now `firered-four-action-reward-continuation-v3`; old diagnostic cursors reject. This remains a Domain boundary, not campaign doubles or player in-battle save support. See `tests/Game.FourRewards.Tests/README.md`.

## Historical shared cancellation and Focus Band integration (2026-10-11)

Canonical singles and four-slot frames now share ordered sleep, natural thaw, Truant, recharge, flinch, confusion and paralysis cancellation. Actual M06 no-target actions run these checks before PP or an attack string. Canonical singles Focus Band uses shared post-random damage adjustment and slot-owned protection through action completion, including confusion and delayed attacks. Other four-slot held effects, additional locks/recipes, AI, Application and full doubles presentation remain open. See [shared cancellers](source-action-cancellers.md) and the [Focus Band tests](../tests/Game.SourceFocusBand.Tests/README.md).

## Historical shared initial and KO-entry callbacks (2026-10-11)

Drizzle, Drought, Sand Stream, Intimidate, Trace and Forecast use the same slot-typed entry recipes for canonical singles and the actual four-slot owner. Initial ordering and ties, delayed Intimidate/Trace, real KO replacements, and weather/form suppression are tested. Current four-slot tests include 14 full turns and 3 actual KO replacements. Spikes, voluntary/Baton/Roar entry integration, general move-end immunity and Application/AI remain open. See [entry ownership and evidence](source-four-slot-entry-abilities.md).

## Historical shared slice animation increment (2026-10-11)

Scratch, Cut, Slash and False Swipe now share the original script/callback renderer with Pound and Tackle: six bounded scripts, 444 exact source-C snapshots and 14 final native animation cases. Existing panX and cue ownership remain in use. The prior native quit timeout is retained as an unresolved intermittent failure; final uninstrumented retry exited normally. Other 348 scripts and general doubles presentation remain incomplete. See [source slice animation coverage](source-basic-slice-animations.md).

## Historical shared support and move-end immunity (2026-10-11)

Follow Me and Helping Hand now execute through shared single/four-slot recipes, including exact PP/target ordering, pre-type damage and distinct cleanup timing. The main integrated support suite passes 16 groups/5,834 checks and eight complete turns; shared numeric immunity adds four groups/193 checks. Original singles support-action hashes remain exact. Source-only infatuation/Nightmare vectors do not establish complete four-slot lifecycles. The next playable target is the original Route8 Twins encounter, with its actual party and AI; complete battle/UI coverage is still open. See the two test READMEs.

## Historical inventory checkpoints

The f7e26f6 inventory recorded 176 / 354 statically admitted four-slot recipes and 28 bounded / 48 interaction-unverified ability records. The subsequent shared protection, Spikes and contact checkpoint recorded 180 / 354, 30 bounded / 46 interaction-unverified abilities and six dedicated scripts. At that earlier checkpoint five scripts had real four-slot dispatch and False Swipe was a presentation-only fixture. These are historical counts; the JSON retains the previous snapshot and its recorded hashes separately.

## Historical shared-family checkpoint (2026-10-11, dirty tree based on 3c78f0a)

Static evaluation of the actual `SourceBattleKernel.SupportsRecipe` target/effect conditions and referenced family predicates admits **211 / 354** authenticated moves, with **143 gated**. This adds eight fixed-damage moves, 18 basic-stat moves and five additional primary-status moves to the prior 180. Live status, ability, item and phase guards still apply. Every move's admission is recorded in the JSON; its complete battle-system status is not upgraded merely by that admission.

- Fixed damage: False Swipe, Dragon Rage, Sonic Boom, Seismic Toss, Night Shade, Psywave, Super Fang and Endeavor. Main evidence passes **9 groups / 5,924 checks**, including actual actor/target slots, immunity, Pressure, Protect/Endure, contact, exact Psywave RNG and four-action KO/reward continuation. It retains 347 unchanged full singles hashes and 37 explicitly declared Super Fang source-RNG corrections. Public Wonder Guard, Substitute and four-slot Focus Band remain outside this family acceptance. See `tests/Game.FixedDamage.Tests/README.md` and `evidence/shared-battle-families-20261011/shared-fixed-main-01/result.json`.
- Basic stats and primary status: **23 moves**, with main evidence passing **16 groups / 32,267 checks / 29 complete four-action turns**. Source ordering, BOTH-target ownership, per-slot PP/Pressure, caps, Protect/Mist/Safeguard, ally targeting and atomic failures are bounded. The immutable singles baseline retains 170 unchanged hashes and 14 declared Screech/Sweet Scent source-order corrections. See `tests/Game.SharedBasicStatStatus.Tests/Program.cs` and `evidence/shared-battle-families-20261011/shared-stats-main-01/results.json`.
- Abilities: **36 bounded-test records / 40 interaction-unverified records**. Actual four-slot cases support precise upgrades for Limber, Immunity, Synchronize, Clear Body, Water Veil and Soundproof. Clear Body/Soundproof have complete four-action holder-separation cases; the other four are bounded actual kernel actions. The 4,368 original-C stat-command vectors use controlled registers and do not establish authentic full-turn coverage for Hyper Cutter or White Smoke; those remain interaction-unverified.
- Ordinary medicine: public Twins and Domain evidence passes **3 cases / 102 checks / 44 commands**, including partner/bench use, two medicines, selection-time effects, prepared-marker execution, capped HP, no-effect/stale identity/overflow atomicity and continuation. The main X11 native run passes **223 checks**, exits 0 and retains identical source/DLL identities before and after. Representative BAG, party and medicine-result screenshots were visually reviewed. The earlier capped-HP test failure remains retained. HP/status/inventory are controlled field fixtures; revival, PP, stat items and Poké Flute remain separate doubles gaps. See `tests/Game.BattleMedicineCampaign.Tests/README.md`, `evidence/shared-battle-families-20261011/double-medicine-main-02/result.json` and `evidence/shared-battle-families-20261011/native/runner-result.json`.

Dedicated script coverage is now **8 / 354**, with **346 remaining**: Pound, Tackle, Scratch, Cut, Slash, False Swipe, Counter and Vital Throw. The current BCL animation batch passes **42,069 checks / 20,706 source-C comparison frames**: 834 singles, 5,004 all-visible four-slot, 5,004 absent-partner and 9,864 explicitly synthetic callback frames. The latter are callback boundary vectors, not additional move scripts. All six common scripts now have actual four-slot kernel dispatch, including False Swipe (72 combined real dispatches, zero False Swipe presentation-only dispatches). Counter and Vital Throw remain unadmitted four-slot rules; their 24 four-slot dispatch fixtures are typed presentation only. See `docs/source-motion-animations.md` and `tests/SourceHitAnimation.Tests/evidence/shared-motion-20261011/main/result.json.gz`.

The new motion scripts passed eight representative native cases on the separate worker identity: six real singles and two typed-only four-slot cases. That retained source manifest predates the main actual-False-Swipe fixture update; it does not certify a fresh native replay of this combined dirty tree. Evidence is retained under `tests/SourceHitAnimation.Tests/evidence/shared-motion-20261011/`. No additional combined native hit-animation run is claimed. Original-ROM pixel parity and physical audible quality/alignment remain unverified; the native medicine run also does not close that audio gate.

Remaining work includes further doubles move/volatile/caller/residual families, general held-item and battle-item categories, broader trainer AI and special modes, full ability-context combinations, and the remaining dedicated animations. Tail Whip's sound-loop/non-damaging invocation and palette/fade families remain distinct presentation work. Source comparison, real rule execution, native rendering and audible playback stay separate acceptance layers. The current predicate input hashes are in the JSON; initial inventory hashes and prior isolated native identities remain historical. This documentation refresh ran no product builds, tests or native sessions.

## Current shared-family checkpoint (2026-10-11, dirty tree based on 16a45ea)

Read-only evaluation of the actual `SourceBattleKernel.SupportsRecipe` target whitelist and every referenced effect-family predicate admits **226 / 354** authenticated moves, with **128 gated**. Exactly 15 IDs are newly admitted from the historical 211 checkpoint; none were removed. Live capability and phase guards still apply. Individual complete battle-system labels remain conservative.

- Retaliation: **Counter, Mirror Coat and Vital Throw**, with main evidence passing **13 groups / 922 checks**. Actual complete turns retain independent last physical/special HP-loss source positions, ally invalidation, Follow Me, failure-time Pressure suppression, contact identity, exact PP/RNG, source-position persistence through real switching, reward/deferred replacement and JSON continuation. The unchanged-C proof has 144 calculator and 36 accuracy vectors plus 11 script assertions; 128 calculator and 15 accuracy inputs are within actual kernel admission. Sixty immutable complete-turn singles hashes remain unchanged. Canonical Vital Throw's pre-existing Bounce/Dig source charge-lock defect is corrected separately; historical profiles and ordinary goldens are retained. Wonder Guard and source-only guarded vectors do not become four-slot acceptance. See `tests/Game.Retaliation.Tests/README.md`, `tests/Game.Retaliation.Tests/vital-throw-correction.json` and `evidence/shared-target-families-20261011/retaliation-result.json`.
- Selected volatile targets: **Supersonic, Confuse Ray, Sweet Kiss; Bind, Wrap, Fire Spin, Clamp, Whirlpool, Sand Tomb; Spider Web, Mean Look and Block**, with **16 groups / 616 checks / 14 complete four-action turns**. Actual slot targeting, Follow Me, ally Pressure, Own Tempo/Soundproof, Safeguard/Protect, PP/RNG and early failures are bounded. Independent same-species targets, binding expiry, source/target switch/Baton Pass/Roar/faint lifetimes and Wrap KO/reward/deferred replacement retain serialized cursor parity without replaying residuals. Sixteen original-C duration/RNG and six residual vectors, 36 unchanged singles actions and 36 canonical adapter comparisons are retained. The earlier adapter fixture-validation failure remains recorded. Combatant definitions are authenticated; selected move sets do not claim natural learnsets. Teeter Dance and other volatile/caller/residual families remain separate. See `tests/Game.FourVolatileTargets.Tests/README.md` and `evidence/shared-target-families-20261011/volatile-result.json`.
- Hustle expands its existing bounded record; the ability ledger remains **36 bounded / 40 interaction-unverified**. **3 cases / 196 checks** cover natural Corsola in all four actor positions, physical Tackle's 76/77 cutoff, unaffected special Bubble, natural Togetic Charm's 80/81 cutoff and a controlled Poison Powder 60/61 cutoff. Original Gen III accuracy applies the physical-type 80% integer floor to zero-power status moves too. One actual complete four-action turn retains both natural Hustle identities and rejects stale continuation atomically. Poison Powder is a controlled ability/move pairing, not a natural learnset or Metronome claim. See `tests/Game.DamageAbilityGaps.Tests/FourHustleChecks.cs` and `evidence/shared-target-families-20261011/hustle-result.json`.

Dedicated script acceptance is now **10 / 354**, with **344 remaining**: the previous eight plus **Tail Whip and battle Flash**. The current focused Counter/Vital Throw dispatch batch passes **99 checks**, with four real singles successes, two failures and **20 actual four-slot rule cases** (eight Counter, 12 Vital Throw). Four same-side Counter cases remain explicit visual fixtures. See `evidence/shared-target-families-20261011/motion-result.json`.

Tail Whip/Flash pass **151 checks / 2,730 source-comparison frames / 1,671,168 RGB555 blend vectors**, including three real singles successes, one suppressed singles invocation and eight actual four-slot stat invocations. Frame counts are 210 singles, 1,260 all-visible four-slot and 1,260 absent-partner. Tail Whip retains source loop-sound frames 0/24/49 and BOTH-target ownership with one PP expenditure; a capped first target can yield a successful second-target animation. Caps, Keen Eye, malformed PP passes, missing results, Scene Off, cancellation and the source 90-frame sound bound are covered. The unchanged six-script 444-frame pixel digest is retained. This main C# run reuses the worker's hashed fixed original-C outputs; it does not claim a new C oracle run. See `docs/source-stat-animations.md`, `evidence/shared-target-families-20261011/stat-result.json` and `evidence/shared-target-families-20261011/reused-stat-source-inputs.json`.

Integrated main Godot stat presentation passes **4,455 headless checks**, with three real singles/four real four-slot animation cases, zero typed-only cases and exit 0. Scene On/Off final state/RNG/ACK, repeated input/synchronization, cancellation and quit are bounded; source/DLL identities stayed unchanged during that run. See `evidence/shared-target-families-20261011/stat-host-result.json`, its source/assembly identities and quit result. The host evidence is tied to that retained source snapshot before the subsequent deferred-spread change.

The separate stat-animation worker passes **one representative X11 battle Flash sample / 6,443 checks / exit 0**. It contains one real singles animation and no native four-slot cases. Its 718-file source/executable identity differs from the main 724-file headless host. The earlier failed assertion on transparent healthbox background pixels is retained; the corrected check verifies opaque healthbox pixels and the text panel. See `tests/SourceHitAnimation.Tests/evidence/stat-family-20261011/delivery.json`, `native-flash/result.json.gz` and `native-mask-diagnostic/result.json` within that evidence directory. These records do not establish one combined current-main native run, original-ROM pixel equality or physical listening. Battle Flash does not establish new field-lighting acceptance. General stat scripts and independently transformed background subpalettes remain outside this increment.

This refresh edits documentation only and runs no product builds, test executables or native sessions. The JSON preserves the full previous snapshot, records all 354 per-move admission decisions, and hashes current predicate inputs separately from retained evidence.

## Shared type-change follow-on

Conversion and Conversion 2 add two recipes to the preceding 226 checkpoint: **228/354 admitted, 126 gated**. Color Change is already one of the 36 bounded ability records; this expands its scope without changing that count. Main integration passes seven groups/1,038 checks, nine complete turns and 64 exact singles/shared state/event/RNG comparisons. Type-only overlays preserve Trace and clear on actual departure. Original Color Change uses retained SpecialStatus damage, including the independently demonstrated delayed-hit/zero-loss case. Conversion 2 retains the user-approved modern sampler. See [source details](source-four-type-changes.md) and `evidence/shared-type-changes-20261011/README.md`. Dedicated-script coverage remains ten; no new campaign/native run or general caller/Transform admission is claimed.

### Shared charge, seed/roots and Focus Band — 2026-10-11

Eight charge scripts now share lifecycle and PP rules across canonical singles and
four-slot execution. Leech Seed/Ingrain use shared source-slot application and the
existing residual scheduler, including Liquid Ooze double KO, EXP and deferred
replacement. Focus Band uses the same held-damage primitive with per-position
sticky state and correct completed-action cleanup. Current measured recipe
admission is 238/354; neither this count nor item support establishes complete
mechanics or presentation. Nightmare/Curse/Yawn, Substitute and broad callers
remain explicit gates. Focus Band dedicated result text is still open.

[Combined evidence and exact scope](../evidence/shared-charge-residual-focus-20261011/README.md).

### Shared stat result animation — 2026-10-11

The original General_StatsChange task now has one shared masked BG1/OBJ-window
renderer, including source timing, sound pan and Scene Off exception. Direct
ordinary successful Growth/Harden/Tail Whip/Flash results are connected; it adds
zero dedicated move scripts. Completion releases existing result text without
ACK or state/RNG mutation. The isolated worker ran grouped source/headless tests
and one representative HARDEN desktop check; main reused that result and built
the integrated Godot project. [Scope and evidence](../tests/SourceHitAnimation.Tests/evidence/stat-mask-20261011/README.md).

The later Focus Band message increment closes the four-slot successful-hit text
gap with a separate result event. Mechanical pre-hit adjustment stays silent,
including an eventual Explosion miss. Dedicated Focus Band animation and the
canonical singles message timing remain separate open work.

### Substitute, sleep residuals, target marks and three dedicated scripts — 2026-10-11

Current measured recipe admission is **246/354**, with **108 gated**. Added shared
Substitute, Nightmare/Curse/Yawn and Lock-On/Mind Reader/Foresight/Odor Sleuth.
Final main targeted groups pass 14/472, 16/843 and 19/883, with 35 actual complete
four-action turns across the three families. Protect's miss marker retains
source script ordering around Substitute, Yawn abilities and Safeguard; failure
text follows resultmessage priority. Exact normal singles fingerprints remain
frozen. [Combined scope and evidence](../evidence/shared-substitute-sleep-20261011/README.md).

Dedicated presentation is **13/354**, with **341 remaining**: Growth, Harden and
Iron Defense add three reusable call/mask/affine roots. The worker's grouped
source and production-view checks plus one representative Iron Defense window
passed. Main reused the exact 22 files, authenticated the small material and
built the combined Godot source; it did not repeat the window or animation suite.
Withdraw remains deferred at the rotated BIOS affine boundary. Abilities remain
36 bounded records and 40 interaction-unverified records.
[Presentation evidence](../tests/SourceHitAnimation.Tests/evidence/stat-moves-20261011/README.md).

### Canonical survival result text — 2026-10-11

Canonical singles now publish Focus Band result text after successful HP update,
including scalar/multi-hit/delayed cases. Shared confusion self-hit also publishes
its source result message in both adapters. Main focused singles 7/352 and affected
four-slot 5/4489 pass; no arithmetic or RNG policy changes. Dedicated item animation
and a fresh native visual check remain open. Recipe/animation totals are unchanged.
See `evidence/focus-result-messages-20261011/README.md`.

### Dynamic power, freeze and action locks — 2026-10-11

Eighteen further recipes share canonical/four-slot commands: seven dynamic-power
moves, four ice freeze-hit moves and seven Recharge/Rampage moves. Admission is
now **264/354**, with **90 gated**. Main combined builds and affected suites pass;
Yawn cancels Rampage while retaining unrelated Lock-On state. Canonical Low Kick
PP placement and accuracy gates follow the source, and zero-loss freeze chance
uses the source command even when Endure/Focus Band leaves one HP. No new
dedicated animation or campaign claim. See
`evidence/shared-dynamic-freeze-rampage-20261011/README.md`.

### Withdraw and Minimize affine choreography — 2026-10-11

Dedicated scripts are now **15/354**, with **339 remaining**. The shared signed
Q14 rotation and cloned-battler compositor serve both roots, with separate
General stat-result presentation. Fixed-worker source24,326 checks/4,860 frames,
headless7,671 and representative native1,223 passed. Main imports the identical
33-path patch, authenticates regenerated material, and compiles the combined
application. Worker native proof retains its original Domain/DLL identity.
Defense Curl's orb family remains open; physical listening and original-runtime
pixel parity are not claimed. See `docs/source-affine-stat-choreography.md`.

## Shared copy, OHKO, hit items and sleep attacks — 2026-10-11

Transform/Mimic/Sketch, four one-hit-KO moves, Tri Attack/Flame Wheel/Sacred Fire and Rest/Snore/Dream Eater now use shared recipes in canonical singles and actual four-slot frames. King’s Rock/Shell Bell use the unchanged shared item formulas with actual battler ownership, per-target move-end timing and the already-approved King parameter correction. These thirteen admissions raise the predicate inventory from 264 to 277; 77 recipes remain gated.

The [combined evidence](../evidence/shared-copy-ohko-hit-sleep-20261011/README.md) records the affected builds and cases. Immutable singles baselines, original source-fragment witnesses and narrow canonical corrections are identified separately. No new Application, campaign, rendered animation or physical-audio claim is made by this Domain increment. The [source animation grouping](battle-animation-batch-plan.md#current-script-grouping-2026-10-11) confirms 352 strict graphs, not a few dozen identical scripts; shared callback families remain the batching strategy.

### Ground spread, restrictions, dynamic types and fixed-two hits — 2026-10-11

Earthquake/Magnitude, Encore/Imprison, Hidden Power/Weather Ball and Double Kick/Bonemerang/Twineedle add nine shared recipes. The actual predicate reports **286/354 admitted, 68 gated**. Original all-other target order, source PP/RNG rules, copied slot identity and end-turn replacement remain under the shared owners. Dedicated Self-Destruct/Explosion roots raise presentation to **17/354**. [Combined evidence](../evidence/shared-ground-restriction-type-20261011/README.md) identifies affected builds/results, reused C comparisons, narrow canonical corrections and the separate animation native identity. No new full campaign or original-ROM replay is claimed.

### Recoil, crash and critical-status family — 2026-10-11

Six recoil/crash moves plus Blaze Kick/Poison Tail/Poison Fang bring runtime admission to **295/354, 59 gated**. The [combined evidence](../evidence/shared-recoil-critical-20261011/README.md) records actual complete turns, immutable singles comparisons and bounded source corrections. Dedicated scripts remain **17/354**. The [76-ability gap map](battle-ability-gap-map.md) identifies current real adapter/lifecycle work: its 73/76 four-slot and 68/76 singles predicates are static admission counts, while the old 36/40 labels describe retained bounded evidence rather than 40 missing abilities.

### Shared utility/weather and ability commands (2026-10-11)

Current runtime admission is **302/354; 52 gated**. Canonical singles and four-slot ability predicates now admit **76/76 official identities**, which does not claim every lifecycle or move combination complete. The retained bounded-evidence ledger is **43 bounded / 33 interaction-unverified**. Seven new moves share real recipes: Gust, Twister, Thunder, Sky Uppercut, Pain Split, Memento and Splash. Common armor/stat/Plus-Minus adapters, Serene Grace secondary commands and Wonder Guard type-result/history are covered by [combined focused evidence](../evidence/shared-weather-abilities-20261011/README.md). Dedicated scripts remain **17/354**.

The remaining move work is concentrated in caller/prepared actions, late-turn/party ownership, item transfer, ability changes and special hit families. Identity admission does not remove those guards. Original gameplay counters such as USED_SPLASH remain outside the current campaign statistics model. Mainline still resumes after core rule closure and affected key regressions, with dedicated animation/audio continuing in parallel.

### Shared item ownership and selected volatiles (2026-10-11)

Nine shared recipes extend static admission to **311/354,43 gated**. [Main focused evidence](../evidence/shared-items-volatiles-20261011/README.md) covers real four-action item transfer/Choice/Recycle, Attract/Taunt/Torment/Spite and KO/reward/replacement. Immutable singles results remain unchanged for these captured cases; Attract's permanent gender under Transform is an explicit corrected boundary. Ability identity admission remains76/76, while the bounded ledger is44/32. These are scoped records, not full interaction completion. Dedicated animations remain17/354.

### Shared callers and late-turn lifetimes — 2026-10-11

The combined main dispatcher now admits **320/354** authentic recipes; **34 remain gated**. Five caller moves and Bide/Perish Song/Destiny Bond/Grudge share real slot, PP and continuation ownership. [Focused combined evidence](../evidence/shared-callers-late-turn-20261011/README.md) includes Choice Band plus Metronome, caller Bide over three turns, called Perish Pressure, self-KO/EXP, and late-turn multi-KO replacement. The original336 Metronome pool is unchanged; an unsupported selected leaf rejects atomically rather than being redrawn. Thus caller entry support does not claim complete caller coverage. Official ability identities remain76/76 admitted and dedicated scripts17/354; core closure remains the mainline prerequisite, dedicated visuals continue independently.
