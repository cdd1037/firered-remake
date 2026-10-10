# Source end-turn phase continuation

Pinned source: pret/pokefirered 037335f4c725d7c9aecdac87066f2002b4bd7e14.
This module is a source single-battle scheduler over the existing `WildBattleRules.Execution`
effect handlers. It does not enable the historical Fuchsia `ResolveMove` entry, choose
opposing actions, award EXP, synthesize reserve parties, increment the turn, call `Ready`,
or draw the next random turn number. It is not application or native-play acceptance.

## Consumer contract

Create/use the existing `Execution` with `deferForestOutcome: true`, supply genuine
`CalledParty` arrays, and call `AdvanceSourceEndTurn(catalog, cursor)`.
`ResolveSourceEndTurnPhase` is the public atomic phase wrapper for executable tests and
other explicit phase clients; its output revision advances once per call.

1. With a null cursor, cancel sleeping multi-turn locks and clear flinch/protection,
   calculate the end-turn order once, and run all ordinary field and battler effects.
   Whole-party exhaustion is checked after damaging steps. Normal faint replacement
   never occurs in the middle of these residuals. The returned cursor exposes the
   `FaintedActions` boundary before any Future Sight/Doom Desire or Perish Song timer.
2. The consumer observes faint events, awards actual EXP, and resolves actual required
   party choices and any Spikes-induced replacement chain. Both sides may require EXP
   and/or replacement. It then resumes with the returned cursor and updated state.
   A still-pending replacement rejects atomically. A deferred switch consumer must also
   resolve its new faint/outcome deltas before resuming; an ongoing state with an
   unresolved fainted active is rejected even if its pending-switch field was cleared.
3. Resumption runs the source faint-action item pass, then handles silent delayed timer
   decrements until one actual delayed script callback. Future attacks visit numeric
   battler IDs (player, opponent); Perish Song uses the already-fixed end-turn order.
   Each callback returns another `FaintedActions` boundary. Incoming members can receive
   later slot-based Future attacks but never repeat ordinary residuals or inherit a
   departed member's Perish Song.
4. Once delayed callbacks are exhausted, the module clears Magic Coat and the B09
   physical/special damage registers at the final cleanup boundary. The consumer owns
   the remaining new-turn `Ready`/turn-number/random-number work. Do not replay a completed
   cursor or discard a pending cursor to start another residual sequence.

The internal `SourceActionsOrder(catalog)` sorts two selected move actions using the
same comparator as end turn, with an Actions event and Actions RNG purpose. Item,
switch and run precedence remains the turn owner’s job. Entry ordering is unchanged.

The internal field/battler functions are also narrow integration points. The complete
`AdvanceSourceEndTurn` routine owns the pause order. Calling the narrow functions alone
is not a whole end turn.

## Source ordering

Field order is Reflect (player/opponent), Light Screen (player/opponent), Mist
(player/opponent), Safeguard (player/opponent), Wish (retained order), Rain, Sandstorm
(duration then retained-order damage), Sun, Hail (duration then retained-order damage).
An expired damaging weather has no damage pass. The chosen-move priority still
participates because `DoFieldEndTurnEffects` calls `GetWhoStrikesFirst(..., FALSE)`;
weather speed, stage, badge and paralysis modifiers apply before any field duration tick.
The admitted 107-species/item profile has no Quick Claw, Macho Brace, Rain Dish,
Speed Boost, Truant, Cloud Nine, Air Lock or Forecast actor; expanding that admission
requires their remaining source branches to be added and independently checked.

Each original battler then receives Ingrain, end-turn ability, items1, Leech Seed,
poison, Toxic, burn, Nightmare, Curse, Wrap, Uproar, Thrash, Disable, Encore, Lock-On,
Charge, Taunt, Yawn and items2 in that order. Uproar wakes at its own place in this
sequence, so a faster sleeper's Nightmare can occur before a slower Uproar user's wake.
The old lock-only diagnostic entry keeps its earlier bounded semantics; this module
places its reused helpers at the full source positions.

## Verification

The console suite uses authenticated source catalog creatures, genuine supplied parties,
the public phase entry and the public real replacement entry. It checks useful outcomes
and exact event ordering, timer values, source RNG placement, no residual replay,
whole-party survival, strict old-profile rejection, and transactional rollback.
`source-vectors.json` records independently read source command order and SHA256s;
these are source-transcription checks, not original ROM execution.

Build/run against the live B08 and B09 implementation:

```sh
source scripts/env.sh
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceEndTurn.Tests/Game.SourceEndTurn.Tests.csproj --configfile .toolhome/recovery-local-only.config -m:1 -nr:false
dotnet tests/Game.SourceEndTurn.Tests/bin/Debug/net10.0/Game.SourceEndTurn.Tests.dll "$PWD"
```

Early isolated builds used the exact shared Domain/Content source plus the pending
scheduler and an explicit no-op B09 cleanup dependency stub in `/tmp`. Those runs proved
only scheduling behavior. The integrated runs use B09's real method and verify that
Future overwrites the physical register, preserves the special register, Perish
callbacks preserve both, and final cleanup clears both sides' registers.
Failed attempts are retained in `evidence/`; a passing result is recorded only after the
corresponding executable actually exits successfully with a nonempty suite.
