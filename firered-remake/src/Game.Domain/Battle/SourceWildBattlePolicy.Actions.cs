using System.Collections.Immutable;
using Game.Domain.Capture;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public static partial class SourceWildBattlePolicy
{
    /// <summary>Menu move limitations only. Recipe capability never grants an artificial Struggle.</summary>
    public static string? ValidateMoveSelection(PokemonCatalog catalog, SourceBattleFrame frame, int slot)
    {
        if (SourceFourActionTurns.ValidateCapability(catalog, frame) is { } invalid) return invalid;
        if (frame.Outcome != WildBattleOutcome.Ongoing || frame.FourTurn is not null || !frame.PendingSwitches.IsEmpty)
            return "called_move_action_phase";
        var cell = frame.Get(new(0)); var body = cell.Body!;
        if (cell.Registers.Turn?.Lock is { } locked) return slot == locked.PpSlot ? null : "called_move_action_locked";
        if (SourceFourActionTurns.RequiresStruggle(catalog, frame, cell.Slot)) return slot == WildBattleRules.StruggleSlot ? null : "called_move_struggle_required";
        if (slot == WildBattleRules.StruggleSlot) return "called_move_struggle_not_required";
        if (slot < 0 || slot >= body.Pokemon.Moves.Length) return "called_move_chosen_slot";
        var learned = body.Pokemon.Moves[slot]; var move = catalog.Moves[learned.Move];
        if (!SourceBattleHeldItems.ChoiceAllows(body, learned.Move)) return "called_move_choice_locked";
        if (learned.CurrentPp == 0) return "called_move_no_pp";
        if (body.DisableTurnsRemaining > 0 && body.DisabledMove == learned.Move) return "called_move_disabled";
        if (body.EncoreTurnsRemaining > 0 && body.EncoreMoveSlot != slot) return "called_move_encore";
        if (SourceMoveRestrictionRules.Imprisoned(learned.Move, SourceMoveRestrictionRules.Opponents(frame, cell.Slot))) return "called_move_imprisoned";
        if (body.CalledVolatile is { Tormented: true } && body.LastUsedMove == learned.Move) return "called_move_tormented";
        if (body.CalledVolatile is { TauntTurnsRemaining: > 0 } && move.Power == 0) return "called_move_taunted";
        return null;
    }

    public static SourceFourActionResult BeginAction(PokemonCatalog catalog, SourceBattleFrame frame,
        long expectedRevision, SourceActionSelection player)
    {
        if (SourceFourActionTurns.ValidateCapability(catalog, frame) is { } invalid) return Fail(frame, invalid);
        if (frame.WildEncounter is null || frame.FourTurn is not null || !frame.Queue.IsEmpty || frame.Outcome != WildBattleOutcome.Ongoing)
            return Fail(frame, "source_wild_selection_phase");
        if (frame.Revision != expectedRevision || player is null || player.Actor.Slot.Value != 0 || !frame.IsCurrent(player.Actor))
            return Fail(frame, "source_wild_selection_identity");
        if (player.Kind != SourceActionKind.Move && frame.Get(new(0)).Registers.Turn?.Lock is not null)
            return Fail(frame, "source_wild_nonmove_locked");
        if (player.Kind == SourceActionKind.Run && ValidatePlayerRun(catalog, frame) is { } runError) return Fail(frame, runError);
        if (ValidatePolicySelection(catalog, frame, player) is { } selectionError) return Fail(frame, selectionError);
        try
        {
            var next = frame; var trace = ImmutableArray.CreateBuilder<RandomTrace>();
            ushort Draw(string purpose, int bound) { var d = Gen3Rng.Next(next.Rng, purpose, bound); next = next with { Rng = d.State }; trace.Add(d.Trace); return d.Value; }
            var enemy = next.Get(new(1)); var body = enemy.Body!; SourceActionSelection chosen;
            if (enemy.Registers.Turn?.Lock is { } locked)
                chosen = new(enemy.Occupant!.Value, SourceActionKind.Move, locked.PpSlot, locked.Target);
            else if (SourceFourActionTurns.RequiresStruggle(catalog, next, enemy.Slot))
                chosen = SourceActionSelection.Struggle(enemy.Occupant!.Value);
            else if (body.EncoreMoveSlot is { } encore)
                chosen = new(enemy.Occupant!.Value, SourceActionKind.Move, encore, new(0));
            else if (next.WildEncounter!.CelioRoamer is not null)
            {
                for (int i = 0; i < 4; i++) Draw($"roamer.ai-setup.slot-{i}", 16);
                bool trapped = body.BindTurnsRemaining > 0 || enemy.Registers.MeanLookSource is not null ||
                    WildBattleEffective.Ability(next.Get(new(0)).Body!) == "ABILITY_SHADOW_TAG" ||
                    WildBattleEffective.Ability(body) != "ABILITY_LEVITATE" && WildBattleEffective.Ability(next.Get(new(0)).Body!) == "ABILITY_ARENA_TRAP";
                if (!trapped) chosen = new(enemy.Occupant!.Value, SourceActionKind.Run);
                else
                {
                    var legal = Enumerable.Range(0, body.Pokemon.Moves.Length).Where(Selectable).ToArray();
                    if (legal.Length == 0) return Fail(frame, "celio_roamer_no_legal_ai_move");
                    int slot = legal[Draw("roamer.ai.choose-best-move", legal.Length) % legal.Length];
                    chosen = new(enemy.Occupant!.Value, SourceActionKind.Move, slot, new(0));
                }
            }
            else
            {
                int slot; do slot = Draw("wild-battle.opponent.choose-slot", 4) & 3; while (!Selectable(slot));
                chosen = new(enemy.Occupant!.Value, SourceActionKind.Move, slot, new(0));
            }
            var result = SourceFourActionTurns.BeginActions(catalog, next, expectedRevision, [player, chosen]);
            return result.Succeeded ? result with { Trace = trace.ToImmutable().AddRange(result.Trace) } : Fail(frame, result.Fault!);
            bool Selectable(int slot) => slot < body.Pokemon.Moves.Length &&
                SourceMoveRestrictionRules.Selectable(catalog, body, slot, SourceMoveRestrictionRules.Opponents(next, enemy.Slot));
        }
        catch (OverflowException) { return Fail(frame, "source_wild_selection_counter_exhausted"); }
        catch (ArgumentException e) { return Fail(frame, e.Message); }
    }

    internal static string? ValidatePolicySelection(PokemonCatalog catalog, SourceBattleFrame frame, SourceActionSelection choice)
    {
        if (choice.Kind is not (SourceActionKind.Run or SourceActionKind.Capture))
            return choice.Capture is null ? null : "source_wild_selection_mixed_payload";
        if (frame.WildEncounter is null || frame.Layout != SourceBattleLayout.Singles || frame.BattleKind != WildBattleCalledBattleKind.Wild)
            return "source_wild_policy_required";
        if (choice.Kind == SourceActionKind.Run)
            return choice.Capture is not null || choice.Actor.Slot.Side == BattleSide.Opponent && frame.WildEncounter.CelioRoamer is null
                ? "source_wild_run_actor" : choice.Actor.Slot.Side == BattleSide.Player ? ValidatePlayerRun(catalog, frame) : null;
        if (choice.Actor.Slot.Side != BattleSide.Player || choice.Capture is not { Context: { } context } capture ||
            context.CaughtSpecies is null || context.CaughtSpecies.Any(s => !catalog.Species.ContainsKey(s)) ||
            !Enum.IsDefined(capture.Ball) || capture.Ball == CaptureBall.Safari ||
            context.Encounter != (frame.WildEncounter.CelioRoamer is null ? CaptureEncounterKind.OrdinaryWild : CaptureEncounterKind.Roamer))
            return "source_capture_context";
        return null;
    }

    private static string? EscapeItem(PokemonCatalog catalog, WildBattleCombatant body) => body.HeldItem != "ITEM_ENIGMA_BERRY" &&
        SourceHeldItemCatalog.Find(catalog, body.HeldItem)?.HoldEffect == "HOLD_EFFECT_CAN_ALWAYS_RUN" ? body.HeldItem : null;

    private static string? ValidatePlayerRun(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        var cell = frame.Get(new(0)); var body = cell.Body!;
        if (EscapeItem(catalog, body) is not null || WildBattleEffective.Ability(body) == "ABILITY_RUN_AWAY") return null;
        if (cell.Registers.MeanLookSource is not null) return "wild_battle_run_mean_look";
        if (body.Rooted) return "wild_battle_run_rooted";
        if (body.BindTurnsRemaining > 0) return "wild_battle_run_binding";
        return SourceSwitchRules.TrappingAbility(catalog, cell.Slot, frame.Slots.Take(frame.BattlerCount).Select(c => c.Slot), s => frame.Get(s).Body!) switch {
            "ABILITY_SHADOW_TAG" => "wild_battle_run_shadow_tag", "ABILITY_ARENA_TRAP" => "wild_battle_run_arena_trap",
            "ABILITY_MAGNET_PULL" => "wild_battle_run_magnet_pull", _ => null };
    }

    internal static SourceFourActionResult Execute(PokemonCatalog catalog, SourceBattleFrame frame, SourceQueuedAction action)
    {
        if (action.Kind == SourceActionKind.Run) return ExecuteRun(catalog, frame, action.Slot.Side == BattleSide.Opponent);
        if (action.Kind != SourceActionKind.Capture || action.Capture is not { } selection) return Fail(frame, "source_wild_action_payload");
        var next = frame; var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
        BeginItem(ref next, events, clearGrudge: true);
        Publish(ref next, events, (n, t) => new SourceBallThrownEvent(n, t, new(0), new(1), selection.Ball));
        if (next.WildEncounter?.TowerBattleKind is { } tower && PokemonTowerBattleContract.BallDodges(tower))
        {
            next = next with { Revision = checked(frame.Revision + 1) };
            Publish(ref next, events, (n, t) => new SourceTowerBallDodgedEvent(n, t, selection.Ball));
            return new(next, events.ToImmutable(), [], null);
        }
        var target = next.Get(new(1)).Body!; var pokemon = target.Pokemon;
        var status = WildBattleSourceFieldEffects.IsValidToxic(target.Status) ? CaptureStatus.BadPoison :
            WildBattleStatus.IsSleeping(target.Status) ? CaptureStatus.Sleep : target.Status switch {
                WildBattleStatus.Freeze => CaptureStatus.Freeze, WildBattleStatus.Poison => CaptureStatus.Poison,
                WildBattleStatus.Burn => CaptureStatus.Burn, WildBattleStatus.Paralysis => CaptureStatus.Paralysis, _ => CaptureStatus.None };
        var context = new CaptureBallContext(pokemon.Level,
            WildBattleEffective.HasType(catalog, target, "TYPE_WATER") || WildBattleEffective.HasType(catalog, target, "TYPE_BUG"),
            selection.Context.IsUnderwater, selection.Context.CaughtSpecies.Contains(pokemon.Species), next.Turn);
        var capture = CaptureRules.Resolve(new(pokemon.Stats.MaxHp, pokemon.CurrentHp, catalog.Species[pokemon.Species].CatchRate,
            selection.Ball, status), next.Rng, context);
        if (capture.Outcome is not (CaptureOutcome.Caught or CaptureOutcome.Escaped)) return Fail(frame, "source_capture_" + capture.Diagnostic);
        next = next with { Rng = capture.Rng, Revision = checked(frame.Revision + 1) };
        Publish(ref next, events, (n, t) => new SourceCaptureResultEvent(n, t, selection.Ball, capture.Outcome,
            capture.Branch!.Value, capture.Calculation!, capture.ChecksPassed, capture.AnimationCode!.Value));
        if (capture.Outcome == CaptureOutcome.Caught)
        {
            next = SourceBattleOutcomeRules.SetOutcome(next, WildBattleOutcome.Caught);
            Publish(ref next, events, (n, t) => new SourceEndedEvent(n, t, WildBattleOutcome.Caught));
        }
        return new(next, events.ToImmutable(), capture.RandomTrace, null);
    }

    private static SourceFourActionResult ExecuteRun(PokemonCatalog catalog, SourceBattleFrame frame, bool opponent)
    {
        var next = frame with { Revision = checked(frame.Revision + 1) }; var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        if (opponent)
        {
            var enemy = frame.Get(new(1)); bool escaped = enemy.Body!.BindTurnsRemaining == 0 && enemy.Registers.MeanLookSource is null;
            Publish(ref next, events, (n, t) => new SourceRoamerRunEvent(n, t, escaped));
            if (escaped)
            {
                next = SourceBattleOutcomeRules.SetOutcome(next, WildBattleOutcome.Escaped, RoamerBattleOutcome.MonFled);
                Publish(ref next, events, (n, t) => new SourceEndedEvent(n, t, WildBattleOutcome.Escaped));
            }
        }
        else
        {
            var player = frame.Get(new(0)).Body!; int p = player.Pokemon.Stats.Speed, o = frame.Get(new(1)).Body!.Pokemon.Stats.Speed;
            int before = frame.RunAttempts; string? item = EscapeItem(catalog, player);
            bool ability = WildBattleEffective.Ability(player) == "ABILITY_RUN_AWAY";
            bool guaranteed = TowerPolicy(frame) is { GuaranteedRun: true };
            bool escaped = item is not null || ability || guaranteed || p >= o;
            int? threshold = null, roll = null;
            if (!escaped)
            {
                threshold = (p * 128 / o + before * 30) & 255;
                var draw = Gen3Rng.Next(next.Rng, "wild-battle.run", 256); next = next with { Rng = draw.State }; trace.Add(draw.Trace);
                roll = draw.Value & 255; escaped = threshold > roll;
            }
            int attempts = item is not null || ability || guaranteed ? before : (before + 1) & 255;
            next = next with { RunAttempts = attempts };
            Publish(ref next, events, (n, t) => new SourceRunAttemptEvent(n, t, before, attempts, p, o, threshold, roll, escaped) { EscapingItem = item });
            if (escaped)
            {
                next = SourceBattleOutcomeRules.SetOutcome(next, WildBattleOutcome.Escaped, RoamerBattleOutcome.Ran);
                Publish(ref next, events, (n, t) => new SourceEndedEvent(n, t, WildBattleOutcome.Escaped));
            }
            else if (frame.FourTurn?.Phase != SourceFourActionPhase.ReplacementsPending) BeginItem(ref next, events, clearGrudge: false);
        }
        return new(next, events.ToImmutable(), trace.ToImmutable(), null);
    }

    public static SourceFourActionResult TryRunAfterFaint(PokemonCatalog catalog, SourceBattleFrame frame, SourceTurnContinuation continuation)
    {
        if (SourceFourActionTurns.ValidateCapability(catalog, frame) is { } invalid) return Fail(frame, invalid);
        if (frame.WildEncounter is null || frame.FourTurn is not { Phase: SourceFourActionPhase.ReplacementsPending } cursor ||
            continuation != new SourceTurnContinuation(frame.Revision, frame.EventSequence, frame.NextAction, cursor.Phase) ||
            frame.PendingSwitches is not [{ Slot.Value: 0, Kind: WildBattleCalledSwitchKind.FaintedReplacement }])
            return Fail(frame, "source_wild_after_faint_run_phase");
        try
        {
            var result = ExecuteRun(catalog, frame, false); var next = result.State;
            if (next.Outcome != WildBattleOutcome.Ongoing) next = next with { PendingSwitches = [], Reservations = [],
                FourTurn = next.FourTurn! with { Phase = SourceFourActionPhase.BattleEnded } };
            return result with { State = next };
        }
        catch (OverflowException) { return Fail(frame, "source_wild_run_counter_exhausted"); }
    }

    private static void BeginItem(ref SourceBattleFrame frame, ImmutableArray<SourceBattleEvent>.Builder events, bool clearGrudge)
    {
        var cell = frame.Get(new(0)); var body = cell.Body!;
        if (body.FuryCutterCounter != 0)
            Publish(ref frame, events, (n, t) => new SourceMtMoonVolatileEvent(n, t, new(0), "fury-cutter", body.FuryCutterCounter, 0, "non-move-action"));
        if (body.DestinyBond)
            Publish(ref frame, events, (n, t) => new SourceTowerVolatileEvent(n, t, new(0), "destiny-bond", false, "non-move-action"));
        var volatileState = body.CalledVolatile;
        if (clearGrudge && volatileState is { Grudge: true })
        {
            volatileState = volatileState with { Grudge = false };
            Publish(ref frame, events, (n, t) => new SourceMtMoonVolatileEvent(n, t, new(0), "grudge", 1, 0, "non-move-action"));
        }
        frame = frame with { Slots = frame.Slots.SetItem(0, cell with { Body = body with { FuryCutterCounter = 0, DestinyBond = false,
            CalledVolatile = volatileState == new WildBattleCalledVolatileState() ? null : volatileState } }) };
    }
    private static void Publish(ref SourceBattleFrame frame, ImmutableArray<SourceBattleEvent>.Builder events,
        Func<long, int, SourceBattleEvent> create)
    { var result = SourceBattleEventHooks.Publish(frame, create); frame = result.State; events.Add(result.Event); }
}
