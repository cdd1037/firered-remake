using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Battle;
using Game.Domain.Randomness;

namespace Game.Domain.WildBattle;

public enum WildBattleAttackCancellerPhase { Start, ConditionsPassed }
[Flags]
public enum WildBattleInvocationFlags
{
    None = 0, AttackStringPrinted = 1, NoPpDeduct = 2, NoAttackString = 4,
    AllowNoPp = 8, Obeys = 16, PpNotAffectedByPressure = 32
}

/// <summary>Action-local source registers. CurrentMove need not occupy any learned slot.</summary>
public sealed record WildBattleMoveInvocation(WildBattleSide Actor, int ChosenSlot, int PpSlot,
    MoveId OriginalChosenMove, MoveId ChosenMove, MoveId CurrentMove, MoveId? CalledMove,
    WildBattleSide Target, WildBattleAttackCancellerPhase CancellerPhase, WildBattleInvocationFlags Flags)
{
    /// <summary>The originally selected actor owns PP even when Snatch changes the script attacker.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? PpOwnerOverride { get; init; }
    [JsonIgnore] public WildBattleSide PpOwner
    {
        get => PpOwnerOverride ?? Actor;
        init => PpOwnerOverride = value == Actor ? null : value;
    }
}

/// <summary>The history registers not representable by the historical chosen/landed fields.</summary>
public sealed record WildBattleSourceMoveHistory
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    public MoveId? LastPrintedMove { get; init; }
    public MoveId? LastResultingMove { get; init; }
    public string? LastHitByType { get; init; }
    public MoveId? LastTakenMove { get; init; }
    public MoveId? LastTakenFromPlayer { get; init; }
    public MoveId? LastTakenFromOpponent { get; init; }
}

/// <summary>A supplied Metronome result, not the random-selection command or an entire turn.</summary>
public sealed record WildBattleCalledMoveAction(WildBattleSide Actor, int ChosenSlot, MoveId CurrentMove,
    string RulesVersion = WildBattleRules.CalledMoveKernelRulesVersion)
{
    public WildBattleCalledActionContext Context { get; init; } = new();
}
public sealed record WildBattleCalledMoveResult(WildBattleResult Battle, WildBattleMoveInvocation? Invocation)
{
    public bool Succeeded => Battle.Succeeded;
    public WildBattleState? State => Battle.State;
    public string? Fault => Battle.Fault;
}
public sealed record WildBattleTargetResolution(WildBattleSide Target, RngState Rng, ImmutableArray<RandomTrace> Trace);
public sealed record WildBattlePpReduction(int Before, int After, int CommandCost, WildBattleInvocationFlags FlagsAfter);
public sealed record WildBattleCalledMoveTransitionEvent(long Sequence, int Turn, WildBattleMoveInvocation Invocation) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleAttackStringEvent(long Sequence, int Turn, WildBattleSide Actor, MoveId ChosenMove, MoveId CurrentMove) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattlePpCommandEvent(long Sequence, int Turn, WildBattleSide Actor, WildBattleSide Target,
    int PpSlot, MoveId ChosenMove, MoveId CurrentMove, int Before, int After, int CommandCost, ulong RngCursor) : WildBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? PpOwnerOverride { get; init; }
    [JsonIgnore] public WildBattleSide PpOwner
    {
        get => PpOwnerOverride ?? Actor;
        init => PpOwnerOverride = value == Actor ? null : value;
    }
}

/// <summary>Source commands shared by called-action dispatch. These never register move effects.</summary>
public static class WildBattleSourceCommands
{
    public static bool IsSoundMove(MoveId move) => move.Value is "pret:MOVE_GROWL" or "pret:MOVE_ROAR" or
        "pret:MOVE_SING" or "pret:MOVE_SUPERSONIC" or "pret:MOVE_SCREECH" or "pret:MOVE_SNORE" or
        "pret:MOVE_UPROAR" or "pret:MOVE_METAL_SOUND" or "pret:MOVE_GRASS_WHISTLE" or "pret:MOVE_HYPER_VOICE";
    public static WildBattleTargetResolution ResolveSinglesTarget(WildBattleSide actor, string targetMode,
        RngState rng, string purpose = "wild-battle.called-target")
    {
        if (!Enum.IsDefined(actor)) throw new ArgumentOutOfRangeException(nameof(actor));
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        var target = ResolveSinglesTarget(actor, targetMode, () => {
            var draw = Gen3Rng.Next(rng, purpose, 2); rng = draw.State; trace.Add(draw.Trace); return draw.Value;
        });
        return new(target, rng, trace.ToImmutable());
    }
    internal static WildBattleSide ResolveSinglesTarget(WildBattleSide actor, string targetMode, Func<ushort> draw)
    {
        if (targetMode == "MOVE_TARGET_SELECTED")
        {
            int target; do { target = draw() % 2; } while (target == (int)actor);
            return (WildBattleSide)target;
        }
        return targetMode switch {
            "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" => actor,
            "MOVE_TARGET_DEPENDS" or "MOVE_TARGET_BOTH" or "MOVE_TARGET_FOES_AND_ALLY" or
            "MOVE_TARGET_OPPONENTS_FIELD" or "MOVE_TARGET_RANDOM" => actor == WildBattleSide.Player ? WildBattleSide.Opponent : WildBattleSide.Player,
            _ => throw new ArgumentOutOfRangeException(nameof(targetMode))
        };
    }

    /// <summary>One Cmd_ppreduce. NO_PPDEDUCT is cleared after each command, even when no PP changed.</summary>
    public static WildBattlePpReduction ReducePp(int pp, string targetMode, WildBattleSide actor,
        WildBattleSide target, string targetAbility, int otherSidePressureCount, int otherBattlerPressureCount,
        WildBattleInvocationFlags flags = WildBattleInvocationFlags.None)
    {
        if (!Enum.IsDefined(actor) || !Enum.IsDefined(target)) throw new ArgumentOutOfRangeException(nameof(actor));
        return ReducePp(pp, targetMode, SinglesBattleAdapter.Slot(actor), SinglesBattleAdapter.Slot(target), targetAbility,
            otherSidePressureCount, otherBattlerPressureCount, flags);
    }

    public static WildBattlePpReduction ReducePp(int pp, string targetMode, BattlerSlot actor,
        BattlerSlot target, string targetAbility, int otherSidePressureCount, int otherBattlerPressureCount,
        WildBattleInvocationFlags flags = WildBattleInvocationFlags.None)
    {
        if (pp is < 0 or > 255 || otherSidePressureCount is < 0 or > 2 || otherBattlerPressureCount is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(pp));
        int cost = 1;
        if (!flags.HasFlag(WildBattleInvocationFlags.PpNotAffectedByPressure))
            cost += targetMode switch {
                "MOVE_TARGET_FOES_AND_ALLY" => otherBattlerPressureCount,
                "MOVE_TARGET_BOTH" or "MOVE_TARGET_OPPONENTS_FIELD" => otherSidePressureCount,
                "MOVE_TARGET_SELECTED" or "MOVE_TARGET_DEPENDS" or "MOVE_TARGET_RANDOM" or "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" =>
                    actor != target && targetAbility == "ABILITY_PRESSURE" ? 1 : 0,
                _ => throw new ArgumentOutOfRangeException(nameof(targetMode))
            };
        bool suppressed = (flags & (WildBattleInvocationFlags.NoPpDeduct | WildBattleInvocationFlags.NoAttackString)) != 0;
        return new(pp, suppressed ? pp : Math.Max(0, pp - cost), cost, flags & ~WildBattleInvocationFlags.NoPpDeduct);
    }
}

public static partial class WildBattleRules
{
    public const string CalledMoveKernelRulesVersion = "firered-called-action-v1";
    private static readonly ImmutableHashSet<int> MetronomeForbiddenIds = [118, 165, 166, 102, 68, 243, 182, 197, 203, 194, 214, 168, 266, 289, 270, 343, 271, 264];
    static partial void AddCalledMoveRecipeSupport(PokemonMoveDefinition move, ref bool supported);
    static partial void AddCalledStatRecoverySupport(PokemonMoveDefinition move, ref bool supported);
    static partial void AddCalledScalarDamageSupport(PokemonMoveDefinition move, ref bool supported);
    static partial void DeferCalledScalarProtection(PokemonMoveDefinition move, ref bool deferred);
    public static bool IsCalledMoveEffectSupported(PokemonMoveDefinition move)
    {
        if (!MetronomeSourceMoveDefinition.Matches(move)) return false;
        bool supported = FuchsiaBattleScope.Supports(move.Id) && FuchsiaBattleScope.ValidDefinition(move);
        AddCalledMoveRecipeSupport(move, ref supported);
        AddCalledStatRecoverySupport(move, ref supported);
        AddCalledScalarDamageSupport(move, ref supported);
        AddCalledTurnLockSupport(move, ref supported);
        AddCalledVolatileSupport(move, ref supported);
        AddCalledFieldEffectSupport(move, ref supported);
        AddCalledPartyEffectSupport(move, ref supported);
        AddCalledCallerSupport(move, ref supported);
        AddPreparedMoveSupport(move, ref supported);
        return supported;
    }

    /// <summary>Validates this action contract's mechanical state and independent source history.
    /// Ordinary ResolveMove intentionally rejects these snapshots until a complete turn successor exists.</summary>
    public static string? ValidateCalledMoveActionState(PokemonCatalog catalog, WildBattleState state)
    {
        if (state.RulesVersion != FuchsiaRulesVersion) return "called_move_base_profile";
        if (SourceBattleAdmission.ValidateRulesProfile(catalog, state) is { } profileError) return profileError;
        foreach (var mon in new[] { state.Player, state.Opponent })
            if (mon.SourcePartyStatusOverride is { } partyStatus &&
                (state.CalledParty is null || mon.Pokemon.CurrentHp == 0 || mon.Transform?.PartyStatusOverride is not null ||
                 !(partyStatus == 0 && WildBattleStatus.IsSleeping(mon.Status) ||
                   WildBattleStatus.IsSleeping(partyStatus) && mon.Status == 0)))
                return "source_party_status_override";
        if (ValidateCopiedMoveState(catalog, state) is { } copyError) return copyError;
        if (ValidateCalledDamageState(state) is { } damageError) return damageError;
        if (ValidateCalledFieldState(state) is { } fieldError) return fieldError;
        if (ValidateCalledPartyState(catalog, state) is { } partyError) return partyError;
        if (ValidateCalledVolatileState(catalog, state) is { } volatileError) return volatileError;
        if (ValidateCalledTurnLockState(catalog, state) is { } turnError) return turnError;
        foreach (var mon in new[] { state.Player, state.Opponent })
        {
            if (mon.SourceMoveHistory is not { } h) continue;
            if (h.RulesVersion != CalledMoveKernelRulesVersion) return "called_move_history_version";
            if (mon.LastCalledMove is { } called && h.LastResultingMove != called)
                return "called_move_history_registers";
            foreach (var id in new[] { h.LastPrintedMove, h.LastResultingMove, h.LastTakenMove, h.LastTakenFromPlayer,
                h.LastTakenFromOpponent, mon.LastUsedMove, mon.LastCalledMove, mon.LastLandedMove })
                if (id is { } value && value.Value is not ("pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE") &&
                    (!catalog.Moves.TryGetValue(value, out var move) || !MetronomeSourceMoveDefinition.Matches(move))) return "called_move_history_definition";
            if (h.LastHitByType is { } type && type is not ("TYPE_NORMAL" or "TYPE_FIGHTING" or "TYPE_FLYING" or "TYPE_POISON" or "TYPE_GROUND" or "TYPE_ROCK" or "TYPE_BUG" or "TYPE_GHOST" or "TYPE_STEEL" or "TYPE_MYSTERY" or "TYPE_FIRE" or "TYPE_WATER" or "TYPE_GRASS" or "TYPE_ELECTRIC" or "TYPE_PSYCHIC" or "TYPE_ICE" or "TYPE_DRAGON" or "TYPE_DARK")) return "called_move_history_type";
            foreach (var mirror in new[] { h.LastTakenMove, h.LastTakenFromPlayer, h.LastTakenFromOpponent })
                if (mirror is { } id && id.Value is not ("pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE") && (!catalog.Moves.TryGetValue(id, out var move) || !move.Flags.Contains("FLAG_MIRROR_MOVE_AFFECTED"))) return "called_move_mirror_history";
        }
        WildBattleCombatant Projection(WildBattleCombatant mon) => mon with {
            Status = WildBattleSourceFieldEffects.IsValidToxic(mon.Status) ? WildBattleStatus.Poison : mon.Status,
            CalledDamage = null, CalledVolatile = null, CalledTurnState = null, SourceMoveHistory = null, LastUsedMove = mon.SourceMoveHistory is null ? mon.LastUsedMove : null,
            LastCalledMove = mon.SourceMoveHistory is null ? mon.LastCalledMove : null, LastLandedMove = mon.SourceMoveHistory is null ? mon.LastLandedMove : null,
            BindingMove = mon.BindingMove is { } bind && catalog.Moves.TryGetValue(bind, out var binding) &&
                MetronomeSourceMoveDefinition.Matches(binding) && binding.Effect == "EFFECT_TRAP" ? new("pret:MOVE_BIND") : mon.BindingMove
        };
        return ValidateCore(catalog, CalledPartyValidationProjection(state) with { CalledDelayed = null, CalledField = null, Player = Projection(state.Player), Opponent = Projection(state.Opponent) }, calledAction: true);
    }

    /// <summary>Runs the actual shared executor for an explicitly supplied legal Metronome choice.
    /// It performs the caller cancellation, call transition, target draws, called script and history.
    /// Selection RNG, other battlers, end-turn processing and campaign persistence are not this contract.</summary>
    public static WildBattleCalledMoveResult ResolveCalledMoveAction(PokemonCatalog catalog, WildBattleState state,
        WildBattleCalledMoveAction action)
    {
        string? error = action.RulesVersion != CalledMoveKernelRulesVersion ? "called_move_rules_version" :
            ValidateCalledMoveActionState(catalog, state);
        if (error is null && (state.Outcome != WildBattleOutcome.Ongoing || state.AwaitingReplacement || state.CalledParty?.PendingSwitch is not null)) error = "called_move_action_phase";
        if (error is null && !Enum.IsDefined(action.Actor)) error = "called_move_actor";
        if (error is null && (action.Context is null || action.Context.ActionOrderIndex is < 0 or > 1 || action.Context.ActorPartyIndex is < 0 or > 5)) error = "called_action_context";
        var actor = action.Actor == WildBattleSide.Player ? state.Player : state.Opponent;
        if (error is null && (action.ChosenSlot < 0 || action.ChosenSlot >= actor.Pokemon.Moves.Length)) error = "called_move_chosen_slot";
        if (error is null && (ForcedMoveSlot(actor) is not null || actor.CalledTurnState?.Lock is not null || actor.EncoreMoveSlot is { } encore && encore != action.ChosenSlot))
            error = "called_move_action_locked";
        PokemonMoveDefinition? chosen = null, current = null;
        if (error is null && (!catalog.Moves.TryGetValue(actor.Pokemon.Moves[action.ChosenSlot].Move, out chosen) ||
            chosen.Id.Value != "pret:MOVE_METRONOME" || !MetronomeSourceMoveDefinition.Matches(chosen))) error = "called_move_caller";
        if (error is null && (!catalog.Moves.TryGetValue(action.CurrentMove, out current) || !MetronomeSourceMoveDefinition.Matches(current))) error = "called_move_definition";
        if (error is null && MetronomeForbiddenIds.Contains(current!.SourceMoveId)) error = "called_move_metronome_forbidden";
        if (error is null && !IsCalledMoveEffectSupported(current!)) error = "called_move_effect_not_registered";
        if (error is not null) return new(Fail(state, error), null);
        WildBattleMoveInvocation? invocation = null;
        var result = Execute(state, state, context => {
            context.calledActionContext = action.Context!;
            context.ExecuteCalledMoveAction(catalog, action, chosen!, current!);
            context.State = context.State with { Revision = checked(state.Revision + 1) };
            invocation = context.activeInvocation;
        });
        return new(result, result.Succeeded ? invocation : null);
    }

    internal sealed partial class Execution
    {
        internal WildBattleMoveInvocation? activeInvocation;
        // A script may change the effective type without mutating authenticated metadata.
        internal string? invocationEffectiveType;
        // Exact command-level DOESNT_AFFECT_FOE writes. Historical delegates below
        // retain their separately audited event mapping until every script owns flags.
        private bool invocationCommandNoEffect;
        private bool invocationSkipsMoveEnd;
        private bool invocationCancellerPassed;
        partial void ExecuteAdditionalCalledMoveEffect(PokemonCatalog catalog, WildBattleSide side, int slot,
            PokemonMoveDefinition move, string purpose, ref bool handled);
        partial void ExecuteCalledStatRecoveryEffect(PokemonCatalog catalog, WildBattleSide side, int slot,
            PokemonMoveDefinition move, string purpose, ref bool handled);
        partial void ExecuteCalledScalarDamageEffect(PokemonCatalog catalog, WildBattleSide side, int slot,
            PokemonMoveDefinition move, string purpose, ref bool handled);

        private bool DeferInvocationProtection(PokemonMoveDefinition? move)
        {
            if (activeInvocation is null || move is null) return false;
            bool deferred = move.Effect == "EFFECT_FOCUS_PUNCH" || HasCalledPartyRecipe(move.Effect) || move.Effect is "EFFECT_EXPLOSION" or "EFFECT_SPIT_UP" or "EFFECT_MIMIC" or "EFFECT_SKETCH" || HasCalledTurnLockRecipe(move.Effect);
            DeferCalledScalarProtection(move, ref deferred);
            return deferred;
        }

        private bool InvocationSoundproof(WildBattleSide side, PokemonMoveDefinition move)
        {
            // battle_util.c sSoundMovesTable is independent of effect aliases.
            if (!WildBattleSourceCommands.IsSoundMove(move.Id)) return false;
            var target = activeInvocation!.Target;
            if (WildBattleEffective.Ability(Get(target)) != "ABILITY_SOUNDPROOF") return false;
            if (Get(side).CalledTurnState?.Lock is { Kind: not WildBattleCalledLockKind.Recharge })
                activeInvocation = activeInvocation with { Flags = activeInvocation.Flags | WildBattleInvocationFlags.NoPpDeduct };
            SpendInvocationPp(side, target, move);
            RecordAbility?.Invoke(target, "ABILITY_SOUNDPROOF");
            Add(n => new WildBattleAbilityPreventedEvent(n, State.Turn, side, target, move.Id, "ABILITY_SOUNDPROOF"));
            return true;
        }

        private bool PrintInvocationAttackString()
        {
            var invocation = activeInvocation!;
            if ((invocation.Flags & (WildBattleInvocationFlags.AttackStringPrinted | WildBattleInvocationFlags.NoAttackString)) != 0) return false;
            Add(n => new WildBattleAttackStringEvent(n, State.Turn, invocation.Actor, invocation.ChosenMove, invocation.CurrentMove));
            activeInvocation = invocation with { Flags = invocation.Flags | WildBattleInvocationFlags.AttackStringPrinted };
            return true;
        }
        private void SpendInvocationPp(WildBattleSide side, WildBattleSide targetSide, PokemonMoveDefinition move, bool printAttackString = true)
        {
            var invocation = activeInvocation! with { Target = targetSide };
            activeInvocation = invocation;
            bool printed = printAttackString && PrintInvocationAttackString(); invocation = activeInvocation!;
            var actor = Get(invocation.PpOwner); int slot = invocation.PpSlot;
            bool realSlot = slot >= 0 && slot < actor.Pokemon.Moves.Length;
            if (!realSlot && (invocation.Flags & (WildBattleInvocationFlags.NoPpDeduct | WildBattleInvocationFlags.NoAttackString)) == 0)
                throw new UnsupportedAction("called_pp_slot");
            int pp = realSlot ? actor.Pokemon.Moves[slot].CurrentPp : 0;
            int otherPressure = WildBattleEffective.Ability(Get(Other(side))) == "ABILITY_PRESSURE" ? 1 : 0;
            var reduction = WildBattleSourceCommands.ReducePp(pp, move.Target, side, targetSide,
                WildBattleEffective.Ability(Get(targetSide)), otherPressure, otherPressure, invocation.Flags);
            if (realSlot) Set(invocation.PpOwner, actor with { Pokemon = actor.Pokemon with { Moves = actor.Pokemon.Moves.SetItem(slot,
                actor.Pokemon.Moves[slot] with { CurrentPp = reduction.After }) } });
            activeInvocation = invocation with { Flags = reduction.FlagsAfter };
            if (printed) Add(n => new WildBattleMoveUsedEvent(n, State.Turn, side, targetSide, slot, move.Id, pp, reduction.After));
            Add(n => new WildBattlePpCommandEvent(n, State.Turn, side, targetSide, slot, invocation.ChosenMove, move.Id, pp, reduction.After, reduction.CommandCost, State.Rng.Cursor) { PpOwner = invocation.PpOwner });
        }

        internal void ExecuteCalledMoveAction(PokemonCatalog catalog, WildBattleCalledMoveAction action,
            PokemonMoveDefinition chosen, PokemonMoveDefinition current)
        {
            var side = action.Actor;
            SourceCalledPreparePartyExecution(side);
            invocationEffectiveType = null;
            invocationCommandNoEffect = false;
            invocationSkipsMoveEnd = false;
            var originalActorLastUsedMove = Get(side).LastUsedMove;
            BeginPreparedMoveInvocation(originalActorLastUsedMove);
            activeInvocation = new(side, action.ChosenSlot, action.ChosenSlot, chosen.Id, chosen.Id, chosen.Id,
                null, chosen.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? side : Other(side),
                WildBattleAttackCancellerPhase.Start, WildBattleInvocationFlags.None);
            int eventStart = Events.Count;
            if (RockTunnel) rockTunnelPreviousMove = Get(side).SourceMoveHistory?.LastResultingMove ?? Get(side).LastCalledMove ?? Get(side).LastUsedMove;
            earlyPartyActed.Add(side);
            ForestMove(catalog, side, action.ChosenSlot, chosen, callerOnly: true);
            if (invocationCancellerPassed)
            {
                PrintInvocationAttackString();
                // Cmd_metronome clears only ATTACKSTRING_PRINTED. Chosen move and PP position survive.
                // Metronome writes gCurrentMove directly, not gCalledMove. Other
                // caller commands may use the distinct CalledMove register later.
                activeInvocation = activeInvocation! with { CurrentMove = current.Id,
                    Flags = activeInvocation!.Flags & ~WildBattleInvocationFlags.AttackStringPrinted };
                var target = WildBattleSourceCommands.ResolveSinglesTarget(side, current.Target,
                    () => Draw($"wild-battle.{side.ToString().ToLowerInvariant()}.called-target", 2));
                activeInvocation = activeInvocation with { Target = target };
                Add(n => new WildBattleCalledMoveTransitionEvent(n, State.Turn, activeInvocation));
                ForestMove(catalog, side, action.ChosenSlot, current);
            }
            if (invocationSkipsMoveEnd)
            {
                // Source end2 skips MOVEEND registers/items/abilities. Only undo the
                // historical MoveUsed-event helper's premature LastUsedMove write.
                Set(side, Get(side) with { LastUsedMove = originalActorLastUsedMove });
                return;
            }
            RecordCalledTurnInability(eventStart, side);
            if (Route11) Route11DefrostOnFireHit(catalog, side, eventStart);
            if (Vermilion && State.CalledParty?.PendingSwitch is not { Kind: WildBattleCalledSwitchKind.BatonPass }) { VermilionImmunityAbilities(); SourceCalledApplyChangedItems(); VermilionMoveEndBerries(); }
            if (State.CalledParty?.PendingSwitch is { Kind: WildBattleCalledSwitchKind.BatonPass })
                Set(side, Get(side) with { LastUsedMove = originalActorLastUsedMove });
            else FinishCalledMoveHistory(catalog, eventStart);
            SourceCalledSynchronizeParties();
        }

        private void FinishCalledMoveHistory(PokemonCatalog catalog, int eventStart)
        {
            RecordCalledDamageHistory(catalog, eventStart);
            SourceCalledMoveEndSubstitute();
            // Snatch's swap marker is reversed at MOVEEND_UPDATE_LAST_MOVES, after effects.
            FinishPreparedMoveRegisters();
            var invocation = activeInvocation!;
            var emitted = Events.Skip(eventStart).ToArray();
            if (emitted.OfType<WildBattleTransformedEvent>().Any())
                activeInvocation = invocation = invocation with { ChosenMove = new("pret:MOVE_UNAVAILABLE") };
            var targetSide = emitted.OfType<WildBattleConfusionSelfHitEvent>().Any() ? invocation.Actor : invocation.Target;
            activeInvocation = invocation = invocation with { Target = targetSide };
            SourceMoveHistoryReducer.Apply(catalog, State.Turn, SinglesBattleAdapter.ImportInvocation(invocation),
                emitted.Select(SourceBattleEventAdapter.Import).ToImmutableArray(), invocationCommandNoEffect,
                slot => {
                    var side = SinglesBattleAdapter.Side(slot);
                    int index = State.CalledParty is { } party ? WildBattleSourcePartyEffects.ActiveIndex(party, side) : 0;
                    return SinglesBattleAdapter.ImportSlot(slot, new(slot.Side, index), 1, Get(side));
                },
                (slot, cell) => Set(SinglesBattleAdapter.Side(slot), SinglesBattleAdapter.ExportBody(cell.Body!, cell.Registers)),
                invocationEffectiveType, recordDamage: false);
            SourceTraceForecastFaintedActions();

        }
    }
}
