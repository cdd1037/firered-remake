# Battle-system completion matrix

Inventory: 2026-10-10 UTC. Product base `67c89d36c0dbdeabfe906ff73a00d9c4c7ce3cba`; fixed FireRed reference `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

The initial inventory is bound to that product base. The fifth-badge singles stage subsequently closed at `5a5b6f7`, with actual Godot Koga verification and a separate native save/Continue checkpoint. This battle-completion stage is now active. Genuine preset doubles remains an implementation gap. The JSON retains initial inspected-file hashes and records later implementation updates separately.

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
| [C10](#c10) | Genuine four-slot doubles owner | `interaction-unverified` | P1 / independent-block |
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

354 authenticated definitions; 350 registered singles recipes; 336 direct Metronome choices plus six distinct indirect Mirror Move results. Admission is not effect/interaction/campaign completeness.

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

One shared phase cursor serves singles and four-slot weather, screen, status and end-turn-ability commands. Four-slot Future Sight/Doom Desire, bounded reward/learning pauses and end-turn KO entry now resume without replaying ordinary residuals. Wish, Perish and other unported four-slot residuals remain gated.

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

Parameterized damage/status/stages/recovery, fields, turn locks, delayed/volatile and party recipes have focused source tests. All combinations of 354 registered moves are not verified.

Dependencies: C01, C02, C03, C07, C08, C09.

Evidence: `src/Game.Domain/WildBattle/WildBattleGenericEffects.cs`, `src/Game.Domain/WildBattle/WildBattleStatRecoveryEffects.cs`, `src/Game.Domain/WildBattle/WildBattleScalarDamageEffects.cs`, `src/Game.Domain/WildBattle/WildBattleCalledFieldEffects.cs`, `src/Game.Domain/WildBattle/WildBattleCalledTurnLocks.cs`, `src/Game.Domain/WildBattle/WildBattleCalledVolatileEffects.cs`, `src/Game.Domain/WildBattle/WildBattleCalledPartyEffects.cs`.
Bounded tests/evidence: `docs/called-move-implementation.md`, `tests/Game.CalledScalarDamage.Tests/evidence/verification.json`, `tests/Game.CalledPartyEffects.Tests/evidence/result.json`.
Pinned source: `data/battle_scripts_1.s`, `src/battle_script_commands.c`, `src/battle_util.c`.

Next acceptance:

- Choose source branch equivalence classes, not an unbounded Cartesian product: substitute/protect/soundproof/safeguard; immunity/absorption; contact versus direct HP; lock/inability; weather/ability suppression; switch/history.
- Exercise both learned and naturally selected caller paths through the actual turn owners; keep controlled vectors distinct from natural campaigns.

<a id="c07"></a>
### C07 — Ability entry, weather and forms

Authenticated permanent weather, Rain Dish, Trace and Forecast have bounded canonical singles coverage. Color Change now has actual HP-receipt and source move-end canonical singles coverage. Four-slot entry/form phases remain unfinished.

Dependencies: C01, C05, C18.

Evidence: `src/Game.Domain/WildBattle/WildBattleRoute3Execution.cs`, `src/Game.Domain/WildBattle/WildBattleCalledFieldEffects.cs`, `src/Game.Domain/WildBattle/WildBattleCalledPartyEffects.cs`, `src/Game.Domain/WildBattle/WildBattleTransformationRules.cs`.
Bounded tests/evidence: `tests/Game.CalledFieldEffects.Tests/README.md`, `tests/Game.CalledPartyEffects.Tests/README.md`.
Pinned source: `src/battle_util.c:AbilityBattleEffects`, `src/battle_util.c:CastformDataTypeChange`, `src/battle_main.c:TryDoEventsBeforeFirstTurn`.

Next acceptance:

- Carry the tested weather register and shared queries into full four-slot entry and residual ownership without duplicating recipes.
- Carry tested Color Change and Trace/Forecast into the four-slot owner with preserved actual identity and source immunity/cleanup hooks.
- Admit authentic ability fixtures without fabricating ability provenance or silently unlocking unrelated campaign species.

<a id="c08"></a>
### C08 — Ability damage, absorption, contact and residual gaps

Authentic mechanics donors now execute Huge Power, Pure Power, Hustle, Marvel Scale and Pressure, plus Volt Absorb, Rough Skin and Flame Body through shared paths. Truant and Speed Boost now have authentic-donor complete-turn and lifecycle tests; Rain Dish and Color Change now have bounded canonical singles coverage; full-field and remaining interaction coverage is separate.

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

Shadow Tag and Suction Cups have authenticated canonical singles execution. M04 now provides learned/called target selection, Gen III Lightning Rod redirection and Plus/Minus shared generic damage on actual four slots. Full trapping/presence and unported recipe interactions remain open.

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

M01 identities through M06 bounded reward/learning and clean end-turn KO replacement are integrated. Actual parties remain authoritative; six opponent completion bits and one recipient cursor avoid an unbounded ledger. Full entry abilities, cancellers/recipes/residuals, AI, preset encounter and campaign presentation remain unfinished.

M01 provides four fixed battler slots, party/occupant identities, reservations and distinct absence/KO masks. M02 executes shared slot-typed hit/stat/status/recovery actions; the same recipes serve existing singles. M03 executes real four-action clean turns and pauses at explicit KO EXP/replacement boundaries. Full residuals, entry/rewards, target redirection/spread, AI and campaign admission remain unfinished. See `docs/source-shared-battle-kernel.md` and `tests/Game.FourActionTurns.Tests/README.md`.

Dependencies: C04, C05, C18.

Evidence: `src/Game.Domain/WildBattle/WildBattleContracts.cs`, `src/Game.Domain/WildBattle/WildBattleCalledMoveKernel.cs`, `src/Game.Domain/WildBattle/WildBattleSourceEndTurn.cs`, `src/Game.Domain/Trainers/SourceTrainerExecution.cs`, `src/Game.Domain/Trainers/FuchsiaRouteTrainerCombatValidation.cs`.
No positive execution acceptance is credited for this missing/enabling scope.
Pinned source: `include/constants/battle.h`, `src/battle_main.c:SetActionsAndBattlersTurnOrder`, `src/battle_util.c:HandleFaintedMonActions`.

Next acceptance:

- Use the canonical slot/party/occupant substrate in the shared execution kernel; own two legal player selections and two actual opposing actions.
- Implement authentic preset roster entry, duplicate-party-selection rejection, per-slot locks and whole-team outcomes.
- Apply the user-approved KO replacement at end of turn as one explicit policy; retain original voluntary switching and Roar/Whirlwind timing.
- Carry EXP, simultaneous faint, delayed attacks and pending replacements through four-slot Application/presentation states.

<a id="c11"></a>
### C11 — Doubles targets and multi-target effects

Real selected/random/side/user/both-opponent targeting, Lightning Rod and Plus/Minus queries, generic spread/Explosion and original doubles screen damage are implemented in the bounded four-slot owner. Deferred-KO no-target failure uses original attack-string/PP/failure/history commands. Follow Me, Helping Hand, callers and additional multi-target effect families remain open.

Dependencies: C10.

Evidence: `src/Game.Domain/WildBattle/WildBattleCalledMoveKernel.cs`, `src/Game.Domain/WildBattle/WildBattleCalledPartyEffects.cs`, `src/Game.Domain/WildBattle/WildBattleGenericEffects.cs`, `src/Game.Domain/WildBattle/ForestBattleDamage.cs`.
No positive execution acceptance is credited for this missing/enabling scope.
Pinned source: `src/battle_util.c:GetMoveTarget`, `src/battle_script_commands.c:Cmd_attackcanceler`, `src/battle_script_commands.c:Cmd_trysethelpinghand`, `src/pokemon.c:CalculateBaseDamage`.

Next acceptance:

- Add selected, random, both-opponents, foes-and-ally, side and user target classes, legal empty/fainted targets and source retargeting.
- Prove Follow Me/Lightning Rod precedence, Helping Hand, ally attacks, Teeter Dance/Perish Song/Explosion and per-target hit/secondary/contact behavior.
- Use FireRed spread/screen rules and RNG consumption; no later-generation damage reduction or priority semantics.

<a id="c12"></a>
### C12 — Switch, Baton Pass, phasing and faint ownership

Actual party arrays and indices, Baton Pass subset, Roar/Whirlwind random reserve selection, Pursuit, Toxic counter reset, Natural Cure and source faint continuations are implemented for singles.

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

Shared medicine validates all owned party members and reuses authoritative inventory effects. Trainer item decisions remain pure until committed. This is not all battle-use item categories.

Dependencies: C05, C18.

Evidence: `src/Game.Domain/Items/SourceBattleItemRules.cs`, `src/Game.Domain/Trainers/SourceTrainerItems.cs`, `src/Game.Domain/WildBattle/WildBattleSourceWildItems.cs`, `src/Game.Domain/Items/EarlyShopMedicineRules.cs`.
Bounded tests/evidence: `tests/Game.SourceTrainerItems.Tests/README.md`, `tests/Game.SourceWildTurns.Tests/README.md`, `tests/Game.BattleMedicineCampaign.Tests/README.md`.
Pinned source: `src/battle_main.c:HandleAction_UseItem`, `src/battle_script_commands.c:Cmd_various`, `src/item_use.c`.

Next acceptance:

- Inventory actual battle-use items; add missing X-item/Guard Spec/Dire Hit/revival/PP paths only after checking existing medicine implementations and content admission.
- Verify active/reserve targets, cannot-use/no-effect atomicity, item-before-action ordering, locks, faint/EXP and delayed continuation.

<a id="c16"></a>
### C16 — Held-item effects and ownership

Canonical singles Choice Band now executes physical Attack scaling, chosen-move locking, called/copied moves, item transfer and departure cleanup. Quick Claw/Macho Brace order and reward modifiers are shared. Focus Band and general four-slot held-item execution remain unfinished. See `docs/source-choice-band.md`.

Dependencies: C04, C05, C18.

Evidence: `src/Game.Domain/WildBattle/WildBattleCalledPartyEffects.cs`, `src/Game.Domain/WildBattle/WildBattleScalarDamageEffects.cs`, `src/Game.Domain/WildBattle/WildBattleSourceEndTurn.cs`, `src/Game.Domain/WildBattle/ForestBattleDamage.cs`, `src/Game.Domain/WildBattle/FuchsiaBattleScope.cs`.
Bounded tests/evidence: `tests/Game.SourceTrainerItems.Tests/README.md`, `tests/Game.CalledPartyEffects.Tests/README.md`, `tests/Game.RockTunnelHeldItemRecovery.Tests/README.md`, `tests/Game.CalledTurnLocks.Tests/README.md`.
Pinned source: `src/battle_util.c:ItemBattleEffects`, `src/battle_main.c:GetWhoStrikesFirst`, `src/pokemon.c:CalculateBaseDamage`.

Next acceptance:

- Implement remaining Focus Band, King's Rock/Shell Bell, herbs and applicable berries/species modifiers by effect group.
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

Versioned source trainer/wild event adapter and move selection exist; retained BCL adapter tests cover 14 event types. Current fifth-badge Application pass does not establish native completion.

Dependencies: C10, C11, C18.

Evidence: `src/Game.Godot/SourceTrainerPresentationModel.cs`, `src/Game.Godot/CampaignSourceTrainerPresentationView.cs`, `src/Game.Godot/CampaignWildBattleView.cs`, `src/Game.Godot/CampaignForestTrainerView.cs`.
Bounded tests/evidence: `tests/Game.FuchsiaGymPresentation.Tests/README.md`, `tests/Game.FuchsiaGymUi.Tests/README.md`.
Pinned source: `src/battle_message.c`, `src/battle_controller_player.c`, `src/battle_controller_opponent.c`.

Next acceptance:

- Use typed event/cue mapping with exact owner/battler identity and invocation deduplication; preserve authoritative ACK sequencing.
- Add four healthboxes, two move selections/target choices and replacement controls for doubles, including cancellation and repeated input.
- Verify real Godot and native inputs separately from the BCL text adapter and headless draw probes.

<a id="c22"></a>
### C22 — Move/ability animation and audio inventory

The authenticated 354-root script inventory now records calls, graphics and sound dependencies (`docs/battle-animation-catalog.md`). It is a conservative script graph, not full C callback execution or dedicated animation acceptance. Existing imported cues, synthesis and portraits are reusable; generic effects are not dedicated original move animations.

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
| 2 | `ABILITY_DRIZZLE` | `implemented-with-bounded-tests` | C07 | Authenticated canonical singles permanent-rain entry, source order, switch/residual/JSON tests pass; four-turn entry remains pending. |
| 3 | `ABILITY_SPEED_BOOST` | `implemented-with-bounded-tests` | C08 | Authentic Ninjask complete-turn tests cover lead versus switched-in timing, stage cap, faint/status order, copied/acquired ability and frame/JSON continuation. Four-slot ordering remains unverified. |
| 4 | `ABILITY_BATTLE_ARMOR` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 5 | `ABILITY_STURDY` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 6 | `ABILITY_DAMP` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 7 | `ABILITY_LIMBER` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 8 | `ABILITY_SAND_VEIL` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 9 | `ABILITY_STATIC` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 10 | `ABILITY_VOLT_ABSORB` | `implemented-with-bounded-tests` | C08 | Both real holder sides, dynamic Electric type, full-HP absorption and shared PP/history verified in canonical singles; no doubles or audible claim. |
| 11 | `ABILITY_WATER_ABSORB` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 12 | `ABILITY_OBLIVIOUS` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 13 | `ABILITY_CLOUD_NINE` | `implemented-with-bounded-tests` | C07 | Authentic canonical donor and all-body versus living-body suppression queries tested; shared recipes consume the same queries. Full doubles weather phases remain pending. |
| 14 | `ABILITY_COMPOUND_EYES` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 15 | `ABILITY_INSOMNIA` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 16 | `ABILITY_COLOR_CHANGE` | `implemented-with-bounded-tests` | C07 | Authenticated canonical singles exact HP-loss receipt, dynamic type, Explosion/recoil/BeatUp/Pursuit, copied abilities and presentation pairing verified. Full four-slot move-end remains gated. |
| 17 | `ABILITY_IMMUNITY` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 18 | `ABILITY_FLASH_FIRE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 19 | `ABILITY_SHIELD_DUST` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 20 | `ABILITY_OWN_TEMPO` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 21 | `ABILITY_SUCTION_CUPS` | `implemented-with-bounded-tests` | C09 | Authentic Octillery/Lileep/Cradily cover Roar/Whirlwind after PP and before Rooted/accuracy, effective ability changes and full source turn continuation; canonical trainer admission remains separate. |
| 22 | `ABILITY_INTIMIDATE` | `interaction-unverified` | C07 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 23 | `ABILITY_SHADOW_TAG` | `implemented-with-bounded-tests` | C09 | Authenticated Wobbuffet/Wynaut and real ability-changing moves cover Run, Teleport, voluntary switch, Baton, forced and faint replacement distinctions; no Gen IV Ghost/self exemption. Four-slot field scan remains separate. |
| 24 | `ABILITY_ROUGH_SKIN` | `implemented-with-bounded-tests` | C08 | Both real holder sides, per-hit actual HP loss, source 1/16 passive damage and faint order verified; no doubles or native effect claim. |
| 25 | `ABILITY_WONDER_GUARD` | `interaction-unverified` | C06 | Damage/type and copy-restriction branches exist; authentic actor admission and every affected source script are not proven. |
| 26 | `ABILITY_LEVITATE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 27 | `ABILITY_EFFECT_SPORE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 28 | `ABILITY_SYNCHRONIZE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 29 | `ABILITY_CLEAR_BODY` | `interaction-unverified` | C06 | Stat-drop protection helper exists; source admission and Intimidate/secondary/Mist/ability-change combinations need proof. |
| 30 | `ABILITY_NATURAL_CURE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 31 | `ABILITY_LIGHTNING_ROD` | `implemented-with-bounded-tests` | C09 | M04 original field-query predicates, targeting or shared generic damage verified with real canonical donors. Unmigrated scalar recipes remain atomically rejected; this is not whole-battle/campaign ability closure. |
| 32 | `ABILITY_SERENE_GRACE` | `interaction-unverified` | C06 | Secondary chance branches exist; current species admission and affected primary/certain/multihit scripts need proof. |
| 33 | `ABILITY_SWIFT_SWIM` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 34 | `ABILITY_CHLOROPHYLL` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 35 | `ABILITY_ILLUMINATE` | `implemented-with-bounded-tests` | C20 | Field encounter modifier exists with source fixed-point/RNG checks; no battle effect is required by this source. |
| 36 | `ABILITY_TRACE` | `implemented-with-bounded-tests` | C07 | Authenticated canonical singles entry, full turns, copied abilities/forms, source position queries, identity and failure atomicity verified after recovery. Full four-slot execution and native form graphics remain unimplemented. |
| 37 | `ABILITY_HUGE_POWER` | `implemented-with-bounded-tests` | C08 | Real Azumarill source full turn and damage match independently extracted C; four-slot interactions unverified. |
| 38 | `ABILITY_POISON_POINT` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 39 | `ABILITY_INNER_FOCUS` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 40 | `ABILITY_MAGMA_ARMOR` | `interaction-unverified` | C06 | Freeze-prevention/overlay cleanup branch exists; this is not a FireRed egg-hatching speed bonus. |
| 41 | `ABILITY_WATER_VEIL` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 42 | `ABILITY_MAGNET_PULL` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 43 | `ABILITY_SOUNDPROOF` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 44 | `ABILITY_RAIN_DISH` | `implemented-with-bounded-tests` | C08 | Authentic canonical donor heals in the sole source ability phase, with suppression, poison order and JSON tests; four-turn residual dispatch remains pending. |
| 45 | `ABILITY_SAND_STREAM` | `implemented-with-bounded-tests` | C07 | Authenticated canonical singles permanent-sand entry, source order, suppression and residual tests pass; four-turn entry remains pending. |
| 46 | `ABILITY_PRESSURE` | `interaction-unverified` | C09 | PP command helper exists; current authentic roster admission and four-slot counts/targets require validation. |
| 47 | `ABILITY_THICK_FAT` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 48 | `ABILITY_EARLY_BIRD` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 49 | `ABILITY_FLAME_BODY` | `implemented-with-bounded-tests` | C08 | Both real holder sides, raw contact RNG before status/type guards and shared burn projection verified; no doubles or native effect claim. |
| 50 | `ABILITY_RUN_AWAY` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 51 | `ABILITY_KEEN_EYE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 52 | `ABILITY_HYPER_CUTTER` | `interaction-unverified` | C06 | Attack-drop protection helper exists; source admission and Intimidate/secondary/Mist/ability-change combinations need proof. |
| 53 | `ABILITY_PICKUP` | `implemented-with-bounded-tests` | C20 | Original victory-only ordered party checks and item buckets exist; test ownership after newly admitted modes/items. |
| 54 | `ABILITY_TRUANT` | `implemented-with-bounded-tests` | C08 | Authentic Slakoth/Slaking complete-turn tests cover raw loafing register, cancellation precedence, ability changes, switches, prepared actions, recharge expiry and serialized continuation. Four-slot and campaign admission remain separate. |
| 55 | `ABILITY_HUSTLE` | `implemented-with-bounded-tests` | C08 | Real Togetic damage and physical-accuracy miss full turns verified; four-slot interactions unverified. |
| 56 | `ABILITY_CUTE_CHARM` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 57 | `ABILITY_PLUS` | `implemented-with-bounded-tests` | C09 | M04 original field-query predicates, targeting or shared generic damage verified with real canonical donors. Unmigrated scalar recipes remain atomically rejected; this is not whole-battle/campaign ability closure. |
| 58 | `ABILITY_MINUS` | `implemented-with-bounded-tests` | C09 | M04 original field-query predicates, targeting or shared generic damage verified with real canonical donors. Unmigrated scalar recipes remain atomically rejected; this is not whole-battle/campaign ability closure. |
| 59 | `ABILITY_FORECAST` | `implemented-with-bounded-tests` | C07 | Authenticated canonical singles entry, full turns, copied abilities/forms, source position queries, identity and failure atomicity verified after recovery. Full four-slot execution and native form graphics remain unimplemented. |
| 60 | `ABILITY_STICKY_HOLD` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 61 | `ABILITY_SHED_SKIN` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 62 | `ABILITY_GUTS` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 63 | `ABILITY_MARVEL_SCALE` | `implemented-with-bounded-tests` | C08 | Real burned Milotic source defense arithmetic and complete turn verified; four-slot interactions unverified. |
| 64 | `ABILITY_LIQUID_OOZE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 65 | `ABILITY_OVERGROW` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 66 | `ABILITY_BLAZE` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 67 | `ABILITY_TORRENT` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 68 | `ABILITY_SWARM` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 69 | `ABILITY_ROCK_HEAD` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 70 | `ABILITY_DROUGHT` | `implemented-with-bounded-tests` | C07 | Authenticated canonical singles permanent-sun entry, source order, switch/residual/JSON tests pass; four-turn entry remains pending. |
| 71 | `ABILITY_ARENA_TRAP` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 72 | `ABILITY_VITAL_SPIRIT` | `interaction-unverified` | C06 | One or more runtime conditional branches exist. They do not establish complete source semantics, legal actor admission, all interactions or presentation. |
| 73 | `ABILITY_WHITE_SMOKE` | `interaction-unverified` | C06 | Stat-drop protection helper exists; source admission and Intimidate/secondary/Mist/ability-change combinations need proof. |
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

## Retained evidence inspected, not rerun

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

Combined presentation increment: Original Pound/Tackle scripts now run through the actual campaign view with original C callback/frame comparison, current terrain/Transform/substitute art, owned sound cancellation and a single committed ACK. Native X11 viewport coverage is limited to these two moves; sound uses Dummy and actual pan/listening remain unverified. See `docs/source-hit-animation-slice.md` and `evidence/recovery-20261011/hit-combined-proof/RESULT.json`.

M06 integration shares actual EXP/EV/learning and delayed callbacks with the permanent party authority, and combines weather/screens with KO reward and replacement resume. The authoritative continuation version is now `firered-four-action-reward-continuation-v3`; old diagnostic cursors reject. This remains a Domain boundary, not campaign doubles or player in-battle save support. See `tests/Game.FourRewards.Tests/README.md`.
