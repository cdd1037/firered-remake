# Bounded four-action owner foundation

This module completes non-KO clean four-actor turns and exposes real KO action/deferred replacement work for later cross-layer continuation. It does not claim complete doubles, encounter admission, AI, EXP, replacement entry, or presentation.

## APIs and state ownership

- `SourceFourActionTurns.BeginActions(catalog, frame, expectedRevision, selections)` validates one selection for each available real occupant, then commits one four-entry queue. It requires the authenticated 386-species catalog and the explicit `SourceBattleKernel.Version` mechanics marker. `BattleRulesProfile.CanonicalSourceSingles`, campaign admission and preset encounters are unchanged.
- `Advance(catalog, frame, continuation)` consumes one action or the clean end-turn boundary. Continuations bind revision, event sequence, action cursor and phase; serialized frames retain exact queue, occupants, RNG and events. The shared M02 `SourceBattleKernel.ResolveAction` executes every actual move; no two-combatant projection or duplicated damage/effect recipe exists.
- `SourceBattleTurnOrder.Resolve` preserves source numeric initial order, nested `(i,j)` swaps, move priority, integer speed/weather/stage/badge/held-item/paralysis arithmetic and one fresh RNG low bit per same-speed/same-priority comparison. End turn reorders numeric battler IDs while retaining chosen move priority. Quick Claw uses the existing turn random number, not a new draw. The owner currently rejects held items/weather; ordering support alone does not admit those turn capabilities.
- The source scalar Ready cleanup is extracted once as `SourceTurnBoundaryRules.ReadyBody` and used by the established singles owner and this owner. The old singles Ready profile guards and random draw remain unchanged. Clean end turn executes the source order draw schedule, proves all residual/delayed callbacks empty, clears turn damage, applies shared cleanup, draws the next turn random number and publishes Ready.

## KO and target boundaries

M02 emits real shared-recipe damage, synchronized permanent party HP, source faint cleanup and `SourceFaintedEvent`. Only then does this owner mark the separate deferred vacancy mask and cancel the original occupant's selected action. It retains every defeated occupant and faint event sequence. Player KOs can continue remaining actions. An opponent KO pauses immediately at `RewardsPending`; EXP/learning is not silently waived. A terminal party outcome that still requires EXP is recorded as `OutcomeAfterRewards`, without publishing final battle completion.

After all actions and the proven empty residual/delayed phases, actual legal reserves determine replacement requests in source numeric slot order. Two vacancies with one reserve expose one choice. Requests retain the real party, active exclusions and M01 side-wide reservation seam. No incoming Pokémon is bound, so it cannot take a later queued hit. `ReplacementsPending` rejects with `source_four_action_entry_continuation_not_migrated` until the shared entry/EXP layer is integrated.

Selected-target handling uses source absent-position fallback: a vacant enemy position falls back to its partner; a vacant ally falls back to the attacker's opposite position, then that position's partner. Applying that fallback to the separately recorded deferred KO vacancy is a consequence of the user's approved KO scheduling change, not a claim that original FireRed used this vacancy timing. No vacancy mask is globally merged into source absence or within-action damage/ability scans. If no eligible target remains, the next action is rejected atomically with `source_four_action_no_target_continuation_not_migrated`; no guessed PP behavior, target, reroll or replacement occurs.

## Unsupported capabilities

- EXP/EV rewards, participant ledger, leveling/move-learning acknowledgments and continuation after rewards
- Actual KO replacement entry, Spikes, entry abilities/items and replacement-entry KO loops
- Voluntary switches/items, Baton Pass, Roar/Whirlwind, Pursuit, forced Struggle and locked moves in this owner (their old singles implementations remain untouched)
- Prepared moves, redirection, spread/multiple-target traversal, callers, reflection/snatching, recoil/self-destruct and simultaneous double KO
- Any nonempty ordinary/delayed residual state, statuses/cancellers, held items, weather, field timers, positional Wish/Future attacks or unported end-turn abilities
- No-target PP/failure continuation
- Source controller/AI move-selection RNG scheduling, campaign/preset activation, Application and native UI

Selections that can create an unported status/confusion phase are rejected before ordering/cost/RNG. Other dynamic KO boundaries remain explicit paused results. These are an extensible owner boundary, not alternate move implementations.

## Verification

Run `bash tests/Game.FourActionTurns.Tests/run.sh PROJECT_ROOT REFERENCE_ROOT NEW_EVIDENCE_DIRECTORY` with the pinned official toolchain configured by `scripts/env.sh`. The reference must be `037335f4c725d7c9aecdac87066f2002b4bd7e14`. Builds use `/tmp/firered-remake-build.lock` and only the small Domain/Content test project. No GUI or full solution runs are involved.

The compiled original C oracle extracts `GetWhoStrikesFirst`, `SetActionsAndBattlersTurnOrder`, `SwapTurnOrder`, and the `DoFieldEndTurnEffects` ordering block from the pinned local reference; explicit environment stubs supply battler registers. 2,048 vectors compare exact order, final RNG seed and draw count. This is extracted-C function execution, not original ROM execution.

The focused product tests cover four genuine distinct species/party identities and real learned-slot actions, both sides' ally targets, complete clean turns, every action/Ready JSON pause, stale continuation rejection, original-occupant cancellation, real KO vacancy target fallback, two actual KOs in one turn, limited reserves, whole-team exhaustion, rewards before terminal outcome and an actual no-target fault after two KOs. The two KOs occur in separate actions; simultaneous multi-target KO is not credited.

The existing M01 runner at fixed parent `12fad11` additionally checks 31 groups / 1,732 checks, 30 real singles turns, 56 snapshots and 503 typed events after the shared Ready extraction. Its four malformed-frame tests require the exact five-line identity-shape guard from that parent commit. The earlier mixed-baseline failure log is retained as an invalid test pairing, not represented as a passing run.

Integrated wrapper uses an empty local NuGet source and locked restore. `./dev test-four-action-turns NEW_RUN_NAME` selects a new evidence directory without overwriting prior results.
