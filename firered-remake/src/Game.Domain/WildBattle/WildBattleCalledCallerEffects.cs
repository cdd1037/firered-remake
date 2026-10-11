using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

/// <summary>An ordinary learned selection in the source action contract, including genuine callers.
/// The move recipe is shared with supplied called results; this is not a whole-turn scheduler.</summary>
public sealed record WildBattleSourceMoveAction(WildBattleSide Actor, int ChosenSlot,
    string RulesVersion = WildBattleRules.CalledMoveKernelRulesVersion)
{
    public WildBattleCalledActionContext Context { get; init; } = new();
}

/// <summary>gProtectStructs damage registers belong to the turn, not last landed move history.</summary>
public sealed record WildBattleCalledDamageState
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    public int Turn { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PhysicalDamage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? PhysicalSource { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int SpecialDamage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? SpecialSource { get; init; }
}

public sealed record WildBattleCallerSelectionEvent(long Sequence, int Turn, WildBattleSide Actor,
    MoveId Caller, MoveId? Result, ImmutableArray<int> CandidateSourceMoveIds, ulong RngCursor) : WildBattleEvent(Sequence, Turn);

public static partial class WildBattleRules
{
    private static bool HasCalledCallerRecipe(string effect) => effect is "EFFECT_METRONOME" or
        "EFFECT_MIRROR_MOVE" or "EFFECT_NATURE_POWER" or "EFFECT_ASSIST" or "EFFECT_SLEEP_TALK" or "EFFECT_COUNTER" or "EFFECT_MIRROR_COAT" or "EFFECT_MIMIC" or "EFFECT_SKETCH";
    private static void AddCalledCallerSupport(PokemonMoveDefinition move, ref bool supported)
    { if (MetronomeSourceMoveDefinition.Matches(move) && HasCalledCallerRecipe(move.Effect)) supported = true; }

    private static string? ValidateCalledDamageState(WildBattleState state)
    {
        foreach (var mon in new[] { state.Player, state.Opponent })
        {
            if (mon.CalledDamage is not { } damage) continue;
            if (damage.RulesVersion != CalledMoveKernelRulesVersion) return "called_damage_version";
            if (damage.Turn < 0 || damage.Turn > state.Turn || damage.PhysicalDamage is < 0 or > 65535 ||
                damage.SpecialDamage is < 0 or > 65535 || damage.PhysicalSource is { } p && !Enum.IsDefined(p) ||
                damage.SpecialSource is { } s && !Enum.IsDefined(s) ||
                damage.PhysicalDamage != 0 && damage.PhysicalSource is null ||
                damage.SpecialDamage != 0 && damage.SpecialSource is null) return "called_damage_registers";
        }
        return null;
    }

    public static WildBattleCalledMoveResult ResolveSourceMoveAction(PokemonCatalog catalog, WildBattleState state,
        WildBattleSourceMoveAction action)
    {
        string? error = action.RulesVersion != CalledMoveKernelRulesVersion ? "called_move_rules_version" :
            ValidateCalledMoveActionState(catalog, state);
        if (error is null && (state.Outcome != WildBattleOutcome.Ongoing || state.AwaitingReplacement || state.CalledParty?.PendingSwitch is not null)) error = "called_move_action_phase";
        if (error is null && !Enum.IsDefined(action.Actor)) error = "called_move_actor";
        var actor = action.Actor == WildBattleSide.Player ? state.Player : state.Opponent;
        if (error is null && (action.ChosenSlot < 0 || action.ChosenSlot >= actor.Pokemon.Moves.Length)) error = "called_move_chosen_slot";
        if (error is null && (ForcedMoveSlot(actor) is not null || actor.CalledTurnState?.Lock is not null ||
            actor.EncoreMoveSlot is { } encore && encore != action.ChosenSlot)) error = "called_move_action_locked";
        if (error is null && (action.Context is null || action.Context.ActionOrderIndex is < 0 or > 1 ||
            action.Context.ActorPartyIndex is < 0 or > 5)) error = "called_action_context";
        PokemonMoveDefinition? chosen = null;
        if (error is null && (!catalog.Moves.TryGetValue(actor.Pokemon.Moves[action.ChosenSlot].Move, out chosen) ||
            !MetronomeSourceMoveDefinition.Matches(chosen))) error = "called_move_definition";
        if (error is null && !IsCalledMoveEffectSupported(chosen!)) error = "called_move_effect_not_registered";
        if (error is not null) return new(Fail(state, error), null);
        WildBattleMoveInvocation? invocation = null;
        var result = Execute(state, state, context => {
            context.ExecuteSourceMoveAction(catalog, action, chosen!);
            context.SourceHeldStandaloneActionFinished();
            context.State = context.State with { Revision = checked(state.Revision + 1) };
            invocation = context.activeInvocation;
        });
        return new(result, result.Succeeded ? invocation : null);
    }

    /// <summary>Source selection restrictions before actions begin. The execution entry separately runs
    /// cancellation, because Taunt/Disable/Imprison may change after a valid selection.</summary>
    public static string? ValidateSourceMoveSelection(PokemonCatalog catalog, WildBattleState state,
        WildBattleSide side, int slot)
    {
        if (ValidateCalledMoveActionState(catalog, state) is { } stateError) return stateError;
        if (!Enum.IsDefined(side)) return "called_move_actor";
        if (state.Outcome != WildBattleOutcome.Ongoing || state.AwaitingReplacement || state.CalledParty?.PendingSwitch is not null)
            return "called_move_action_phase";
        var actor = side == WildBattleSide.Player ? state.Player : state.Opponent;
        var target = side == WildBattleSide.Player ? state.Opponent : state.Player;
        if (actor.CalledTurnState?.Lock is { } locked) return slot == locked.PpSlot ? null : "called_move_action_locked";
        if (ForcedMoveSlot(actor) is { } forced) return slot == forced ? null : "called_move_action_locked";
        bool Available(int index) => SourceBattleAdmission.IsCanonicalSourceState(catalog, state)
            ? SourceMoveRestrictionRules.Selectable(catalog, actor, index, [target])
            : IsCalledMoveSelectable(catalog, actor, index) && !IsImprisoned(actor, target, actor.Pokemon.Moves[index].Move);
        bool exhausted = !Enumerable.Range(0, actor.Pokemon.Moves.Length).Any(Available);
        if (exhausted) return slot == StruggleSlot ? null : "called_move_struggle_required";
        if (slot == StruggleSlot) return "called_move_struggle_not_required";
        if (slot < 0 || slot >= actor.Pokemon.Moves.Length) return "called_move_chosen_slot";
        var move = catalog.Moves[actor.Pokemon.Moves[slot].Move];
        if (!MetronomeSourceMoveDefinition.Matches(move) || !IsCalledMoveEffectSupported(move)) return "called_move_effect_not_registered";
        if (!SourceBattleHeldItems.ChoiceAllows(actor, move.Id)) return "called_move_choice_locked";
        if (actor.Pokemon.Moves[slot].CurrentPp == 0) return "called_move_no_pp";
        if (actor.DisableTurnsRemaining > 0 && actor.DisabledMove == move.Id) return "called_move_disabled";
        if (actor.EncoreTurnsRemaining > 0 && actor.EncoreMoveSlot != slot) return "called_move_encore";
        if (IsImprisoned(actor, target, move.Id)) return "called_move_imprisoned";
        if (actor.CalledVolatile is { Tormented: true } && actor.LastUsedMove == move.Id && move.Id != StruggleMove) return "called_move_tormented";
        if (actor.CalledVolatile is { TauntTurnsRemaining: > 0 } && move.Power == 0) return "called_move_taunted";
        return null;
    }

    /// <summary>The source ProtectStruct reset, separate from residual effects and turn increment.</summary>
    public static WildBattleResult ResolveCalledDamageTurnReset(PokemonCatalog catalog, WildBattleState state)
    {
        if (ValidateCalledMoveActionState(catalog, state) is { } error) return Fail(state, error);
        return Execute(state, state, context => {
            context.SourceCalledResetDamageTurn();
            context.State = context.State with { Revision = checked(state.Revision + 1) };
        });
    }

    internal sealed partial class Execution
    {
        internal void SourceCalledResetDamageTurn() => State = State with {
            Player = State.Player with { CalledDamage = null }, Opponent = State.Opponent with { CalledDamage = null } };

        internal void ExecuteSourceMoveAction(PokemonCatalog catalog, WildBattleSourceMoveAction action,
            PokemonMoveDefinition chosen)
        {
            // Integration binds the B07 action context and B08 actual party index here.
            calledActionContext = action.Context with { ActorPartyIndex = State.CalledParty is { } party
                ? WildBattleSourcePartyEffects.ActiveIndex(party, action.Actor) : action.Context.ActorPartyIndex };
            var side = action.Actor;
            SourceCalledPreparePartyExecution(side);
            invocationEffectiveType = null;
            invocationCommandNoEffect = false;
            invocationSkipsMoveEnd = false;
            var originalActorLastUsedMove = Get(side).LastUsedMove;
            BeginPreparedMoveInvocation(catalog, originalActorLastUsedMove);
            activeInvocation = new(side, action.ChosenSlot, action.ChosenSlot, chosen.Id, chosen.Id, chosen.Id,
                null, chosen.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? side : Other(side),
                WildBattleAttackCancellerPhase.Start, WildBattleInvocationFlags.None);
            int eventStart = Events.Count;
            if (RockTunnel) rockTunnelPreviousMove = Get(side).SourceMoveHistory?.LastResultingMove ?? Get(side).LastCalledMove ?? Get(side).LastUsedMove;
            earlyPartyActed.Add(side);
            ForestMove(catalog, side, action.ChosenSlot, chosen);
            if (invocationSkipsMoveEnd)
            { Set(side, Get(side) with { LastUsedMove = originalActorLastUsedMove }); return; }
            RecordCalledTurnInability(eventStart, side);
            if (State.CalledParty?.PendingSwitch?.Kind == WildBattleCalledSwitchKind.BatonPass)
            { Set(side, Get(side) with { LastUsedMove = originalActorLastUsedMove }); return; }
            CompleteCalledInvocationMoveEnd(catalog, eventStart, side);
            SourceCalledSynchronizeParties();
        }

        /// <summary>Source noValidMoves action. The turn adapter must establish selection exhaustion first.
        /// StruggleSlot is used only for this genuine forced action, never for Mirror-called Struggle.</summary>
        internal void ExecuteSourceForcedStruggle(PokemonCatalog catalog, WildBattleSide side,
            WildBattleCalledActionContext context)
        {
            calledActionContext = context;
            SourceCalledPreparePartyExecution(side);
            var current = catalog.Moves[StruggleMove];
            if (!MetronomeSourceMoveDefinition.Matches(current)) throw new UnsupportedAction("called_struggle_definition");
            int chosenPosition = side == WildBattleSide.Player ? State.ForestPlayerLastMoveSlot : State.ForestOpponentLastMoveSlot;
            invocationEffectiveType = null;
            invocationCommandNoEffect = false;
            invocationSkipsMoveEnd = false;
            var originalActorLastUsedMove = Get(side).LastUsedMove;
            BeginPreparedMoveInvocation(catalog, originalActorLastUsedMove);
            activeInvocation = new(side, chosenPosition, StruggleSlot, current.Id, current.Id, current.Id, null,
                Other(side), WildBattleAttackCancellerPhase.Start, WildBattleInvocationFlags.NoPpDeduct);
            int eventStart = Events.Count;
            if (RockTunnel) rockTunnelPreviousMove = Get(side).SourceMoveHistory?.LastResultingMove ?? Get(side).LastCalledMove ?? Get(side).LastUsedMove;
            earlyPartyActed.Add(side);
            // ForestMove's source forced-Struggle branch clears noValidMoves and consumes GetMoveTarget
            // before cancellation; suppliedDefinition preserves the authenticated current move tuple.
            ForestMove(catalog, side, StruggleSlot, current);
            if (invocationSkipsMoveEnd)
            { Set(side, Get(side) with { LastUsedMove = originalActorLastUsedMove }); return; }
            RecordCalledTurnInability(eventStart, side);
            CompleteCalledInvocationMoveEnd(catalog, eventStart, side);
            SourceCalledSynchronizeParties();
        }

        private PokemonMoveDefinition CalledDefinition(PokemonCatalog catalog, int sourceId)
        {
            var matches = catalog.Moves.Values.Where(m => m.SourceMoveId == sourceId && MetronomeSourceMoveDefinition.Matches(m)).ToArray();
            if (matches.Length != 1) throw new UnsupportedAction("called_move_definition_" + sourceId);
            if (!IsCalledMoveEffectSupported(matches[0]))
                throw new UnsupportedAction("called_move_effect_not_registered_" + matches[0].Id.Value);
            return matches[0];
        }

        private void ExecuteCalledCallerEffect(PokemonCatalog catalog, WildBattleSide side, int slot,
            PokemonMoveDefinition move, string purpose, ref bool handled)
        {
            if (!HasCalledCallerRecipe(move.Effect)) return;
            if (activeInvocation is null || !MetronomeSourceMoveDefinition.Matches(move)) throw new UnsupportedAction("called_caller_source_definition");
            handled = true;
            if (move.Effect is "EFFECT_MIMIC" or "EFFECT_SKETCH")
            { CalledCopyMove(catalog, side, move); return; }
            if (move.Effect == "EFFECT_SLEEP_TALK")
            { CalledSleepTalk(catalog, side, move, purpose); return; }
            if (move.Effect is "EFFECT_COUNTER" or "EFFECT_MIRROR_COAT")
            { CalledCounter(catalog, side, slot, move, purpose); return; }
            PrintInvocationAttackString();
            WildBattleSourceCallSelection selection;
            bool writesCalledMove = false;
            switch (move.Effect)
            {
                case "EFFECT_METRONOME": selection = WildBattleSourceCallers.Metronome(State.Rng); break;
                case "EFFECT_NATURE_POWER": selection = new(WildBattleSourceCallers.NaturePower(State.Terrain ?? throw new UnsupportedAction("called_nature_power_terrain")), State.Rng, [], []); break;
                case "EFFECT_MIRROR_MOVE":
                    int Id(MoveId? id) => id is null || id.Value.Value == "pret:MOVE_NONE" ? 0 :
                        id.Value.Value == "pret:MOVE_UNAVAILABLE" ? 65535 : catalog.Moves[id.Value].SourceMoveId;
                    var history = Get(side).SourceMoveHistory;
                    selection = WildBattleSourceCallers.MirrorMove((int)side, Id(history?.LastTakenMove),
                        [Id(history?.LastTakenFromPlayer), Id(history?.LastTakenFromOpponent)], State.Rng);
                    break;
                case "EFFECT_ASSIST":
                    if (State.CalledParty is not { } party) throw new UnsupportedAction("called_assist_party_required");
                    var members = WildBattleSourcePartyEffects.Party(party, side).Select(member => new WildBattleAssistMember(member.IsEgg,
                        member.Pokemon.Moves.Select(m => catalog.Moves[m.Move].SourceMoveId).ToImmutableArray())).ToImmutableArray();
                    selection = WildBattleSourceCallers.Assist(members, WildBattleSourcePartyEffects.ActiveIndex(party, side), State.Rng);
                    writesCalledMove = true;
                    break;
                default: throw new UnsupportedAction("called_caller_effect");
            }
            State = State with { Rng = selection.Rng };
            Trace.AddRange(selection.Trace);
            PokemonMoveDefinition? current = selection.SourceMoveId is { } sourceId ? CalledDefinition(catalog, sourceId) : null;
            Add(n => new WildBattleCallerSelectionEvent(n, State.Turn, side, move.Id, current?.Id, selection.Candidates, State.Rng.Cursor));
            if (current is null)
            {
                if (move.Effect == "EFFECT_MIRROR_MOVE") activeInvocation = activeInvocation with {
                    Flags = activeInvocation.Flags | WildBattleInvocationFlags.PpNotAffectedByPressure };
                SpendInvocationPp(side, activeInvocation.Target, move);
                Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, activeInvocation.Target, move.Id,
                    move.Effect == "EFFECT_MIRROR_MOVE" ? "mirror-move-no-history" : "assist-no-candidates"));
                return;
            }
            // All four source commands clear ATTACKSTRING_PRINTED. Only Assist writes gCalledMove;
            // jumptocalledmove TRUE preserves gChosenMove and gCurrMovePos at every nesting level.
            activeInvocation = activeInvocation with { CurrentMove = current.Id,
                CalledMove = writesCalledMove ? current.Id : activeInvocation.CalledMove,
                Flags = activeInvocation.Flags & ~WildBattleInvocationFlags.AttackStringPrinted };
            var target = WildBattleSourceCommands.ResolveSinglesTarget(side, current.Target,
                () => Draw($"wild-battle.{side.ToString().ToLowerInvariant()}.called-target", 2));
            activeInvocation = activeInvocation with { Target = target };
            Add(n => new WildBattleCalledMoveTransitionEvent(n, State.Turn, activeInvocation));
            // Source legality and history, not a depth cap or filtered/redrawn result, determine the call chain.
            ForestMove(catalog, side, slot, current);
        }

        private void CalledSleepTalk(PokemonCatalog catalog, WildBattleSide side, PokemonMoveDefinition move, string purpose)
        {
            SpendInvocationPp(side, activeInvocation!.Target, move);
            if (!WildBattleStatus.IsSleeping(Get(side).Status))
            { Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, activeInvocation.Target, move.Id, "user-awake")); return; }
            activeInvocation = activeInvocation with { Flags = activeInvocation.Flags | WildBattleInvocationFlags.NoPpDeduct };
            var actor = Get(side); var foe = Get(Other(side));
            bool Candidate(int index) => SnorlaxSleepTalkCandidate(catalog, actor, foe, index) &&
                !CalledVolatileMoveLimited(catalog, actor, index);
            var slots = Enumerable.Range(0, 4).Where(Candidate).ToArray();
            if (slots.Length == 0)
            { Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, activeInvocation.Target, move.Id, "no-callable-move")); return; }
            int selected; do { selected = Draw(purpose + ".sleep-talk-slot", 4) & 3; } while (!Candidate(selected));
            var current = CalledDefinition(catalog, catalog.Moves[actor.Pokemon.Moves[selected].Move].SourceMoveId);
            Add(n => new WildBattleSleepTalkEvent(n, State.Turn, side, selected, current.Id));
            Add(n => new WildBattleCallerSelectionEvent(n, State.Turn, side, move.Id, current.Id,
                slots.Select(i => catalog.Moves[actor.Pokemon.Moves[i].Move].SourceMoveId).ToImmutableArray(), State.Rng.Cursor));
            // Unlike Metronome/Mirror/Nature/Assist, this source command explicitly changes gCurrMovePos.
            activeInvocation = activeInvocation with { CurrentMove = current.Id, CalledMove = current.Id, PpSlot = selected,
                Flags = activeInvocation.Flags & ~WildBattleInvocationFlags.AttackStringPrinted };
            var target = WildBattleSourceCommands.ResolveSinglesTarget(side, current.Target,
                () => Draw(purpose + ".called-target", 2));
            activeInvocation = activeInvocation with { Target = target };
            Add(n => new WildBattleCalledMoveTransitionEvent(n, State.Turn, activeInvocation));
            ForestMove(catalog, side, selected, current);
        }

        private void CalledCounter(PokemonCatalog catalog, WildBattleSide side, int slot, PokemonMoveDefinition move, string purpose)
        {
            var memory = Get(side).CalledDamage;
            bool physical = move.Effect == "EFFECT_COUNTER";
            if (memory is not null && memory.Turn != State.Turn) memory = null;
            int damage = physical ? memory?.PhysicalDamage ?? 0 : memory?.SpecialDamage ?? 0;
            WildBattleSide? source = physical ? memory?.PhysicalSource : memory?.SpecialSource;
            if (damage == 0 || source is null || source == side || Get(source.Value).Pokemon.CurrentHp == 0)
            {
                activeInvocation = activeInvocation! with { Flags = activeInvocation!.Flags | WildBattleInvocationFlags.PpNotAffectedByPressure };
                SpendInvocationPp(side, activeInvocation.Target, move);
                Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, activeInvocation.Target, move.Id,
                    physical ? "counter-no-physical-damage" : "mirror-coat-no-special-damage"));
                return;
            }
            var target = source.Value;
            activeInvocation = activeInvocation! with { Target = target };
            var type = ScalarType(move.Type);
            bool hit = ScalarAccuracy(side, target, move, purpose, type);
            SpendInvocationPp(side, target, move);
            if (!hit) { SourceGenericMiss(side, target, move); return; }
            // typecalc2 retains only immunity; resistance, weakness and STAB do not scale doubled damage.
            bool immune = ScalarTypeResult(catalog, target, type).Immune;
            int adjusted = ScalarAdjust(target, move, checked(damage * 2), purpose);
            int lost = SourceGenericApplyDamage(side, target, move, immune ? 0 : adjusted, false, 100);
            ScalarReportType(catalog, side, target, move, type, true);
            SourceGenericSecondary(catalog, side, target, move, purpose, immune ? 0 : adjusted);
            ScalarFinish(catalog, side, target, move, purpose, lost, type);
        }

        private void RecordCalledDamageHistory(PokemonCatalog catalog, int eventStart)
        {
            var invocation = activeInvocation!;
            // SwitchInClearSetData resets current to NONE; any entry Spikes damage is passive.
            if (invocation.CurrentMove.Value == "pret:MOVE_NONE") return;
            var current = catalog.Moves[invocation.CurrentMove];
            if (current.Effect == "EFFECT_PAIN_SPLIT") return;
            var events = Events.Skip(eventStart).ToArray();
            bool noEffect = invocationCommandNoEffect || events.OfType<WildBattleEffectivenessEvent>()
                .Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect && e.Move == current.Id);
            // datahpupdate treats Hidden Power's F_DYNAMIC_TYPE_1 as its original Normal metadata.
            string type = current.Effect == "EFFECT_HIDDEN_POWER" ? current.Type : invocationEffectiveType ?? current.Type;
            foreach (var hit in events.OfType<WildBattleHpChangedEvent>())
            {
                // Substitute, recoil, confusion and direct self-sacrifice have distinct source handling.
                if (hit.Actor == hit.Target || hit.Move != current.Id || noEffect) continue;
                RecordCalledDamage(hit.Target, hit.Actor, Math.Max(0, hit.HpBefore - hit.HpAfter), type);
            }
        }
        private void RecordCalledDamage(WildBattleSide target, WildBattleSide source, int hpDealt, string sourceType)
        {
            var prior = Get(target).CalledDamage;
            var memory = prior is not null && prior.Turn == State.Turn ? prior : new WildBattleCalledDamageState { Turn = State.Turn };
            memory = TypeRules.Category(ScalarType(sourceType)) == DamageCategory.Physical
                ? memory with { PhysicalDamage = hpDealt, PhysicalSource = source }
                : memory with { SpecialDamage = hpDealt, SpecialSource = source };
            Set(target, Get(target) with { CalledDamage = memory });
        }
    }
}
