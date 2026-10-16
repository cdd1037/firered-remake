using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

public sealed record SourceTrainerEnemyChoice(BattlerSlot Actor, BattlerSlot ScoredTarget,
    SourceActionSelection Selection, ImmutableArray<int> Scores,
    ImmutableArray<int> SimulatedDamagePercent, ImmutableArray<ForestTrainerAbilityRead> AbilityReads);
public sealed record SourceTrainerMoveResult(SourceBattleFrame State, ImmutableArray<SourceTrainerEnemyChoice> Choices,
    ImmutableArray<RandomTrace> Trace, string? Fault)
{
    public bool Succeeded => Fault is null;
    public MoveId? SourceCurrentMove { get; init; }
    public ImmutableArray<SourceActionSelection> Selections => Choices.Select(c => c.Selection).ToImmutableArray();
}

/// <summary>FireRed enemy controller decisions over actual source positions. Content owns source
/// trainer metadata; the shared action owner executes the returned selections.</summary>
public static partial class SourceFourTrainerAi
{
    public static SourceTrainerMoveResult ChooseMoves(PokemonCatalog catalog, TrainerDefinition trainer,
        SourceBattleFrame frame, long expectedRevision, string? knownPlayerAbility = null, string? knownPlayerHoldEffect = null, MoveId? staleCurrentMove = null)
    {
        if (ValidateMetadata(trainer, frame) is { } error) return new(frame, [], [], error);
        return ChooseCore(catalog, frame, expectedRevision, trainer.AiFlags, knownPlayerAbility, "source-trainer", knownPlayerHoldEffect, staleCurrentMove: staleCurrentMove);
    }

    /// <summary>One CHOOSEMOVE response, for a caller that has already resolved this actor's
    /// switch/item decision. The caller retains the original per-actor controller order.</summary>
    public static SourceTrainerMoveResult ChooseMove(PokemonCatalog catalog, TrainerDefinition trainer,
        SourceBattleFrame frame, long expectedRevision, BattlerSlot actor,
        string? knownPlayerAbility = null, string? knownPlayerHoldEffect = null, MoveId? staleCurrentMove = null)
    {
        if (ValidateMetadata(trainer, frame) is { } error) return new(frame, [], [], error);
        if (actor.Value >= frame.BattlerCount || actor.Side != BattleSide.Opponent) return new(frame, [], [], "source_four_trainer_ai_actor");
        return ChooseCore(catalog, frame, expectedRevision, trainer.AiFlags, knownPlayerAbility, "source-trainer", knownPlayerHoldEffect, actor, staleCurrentMove);
    }

    private static string? ValidateMetadata(TrainerDefinition trainer, SourceBattleFrame frame)
    {
        if (frame is null || trainer is null || trainer.DoubleBattle != (frame.Layout == SourceBattleLayout.PresetDoubles) || trainer.AiFlags is not (1 or 7) ||
            trainer.Party.IsDefaultOrEmpty || trainer.Party.Length < (trainer.DoubleBattle ? 2 : 1) || trainer.Party.Length > 6 ||
            trainer.AiFlagSymbols.IsDefault || !trainer.AiFlagSymbols.Order().SequenceEqual((trainer.AiFlags == 1
                ? new[] { "AI_SCRIPT_CHECK_BAD_MOVE" }
                : new[] { "AI_SCRIPT_CHECK_BAD_MOVE", "AI_SCRIPT_CHECK_VIABILITY", "AI_SCRIPT_TRY_TO_FAINT" }).Order()))
            return "source_four_trainer_ai_metadata";
        if (frame.Layout == SourceBattleLayout.Singles && !SourceTrainerEncounterPolicy.MatchesTrainer(frame, trainer))
            return "source_trainer_ai_roster_proof";
        return null;
    }

    private static BattlerSlot FirstOpponent(SourceBattleFrame frame) => frame.IsSourceAbsent(new(0)) ? new(2) : new(0);

    internal static SourceTrainerMoveResult ChooseCore(PokemonCatalog catalog, SourceBattleFrame frame,
        long expectedRevision, uint aiFlags, string? knownPlayerAbility, string tracePrefix, string? knownPlayerHoldEffect = null, BattlerSlot? onlyActor = null, MoveId? staleCurrentMove = null)
    {
        var currentMove = staleCurrentMove ?? SourceTrainerEncounterPolicy.NoMove;
        SourceTrainerMoveResult Fail(string code) => new(frame, [], [], code) { SourceCurrentMove = staleCurrentMove };
        if (SourceFourActionTurns.ValidateCapability(catalog, frame) is { } capability) return Fail(capability);
        if (frame.Layout == SourceBattleLayout.Singles && SourceTrainerEncounterPolicy.Validate(catalog, frame) is { } encounter) return Fail(encounter);
        if (frame.Revision != expectedRevision || frame.Outcome != WildBattleOutcome.Ongoing || frame.FourTurn is not null ||
            !frame.Queue.IsEmpty || !frame.PendingSwitches.IsEmpty || !frame.Reservations.IsEmpty || frame.DeferredKoVacancyMask != 0)
            return Fail("source_four_trainer_ai_phase");
        if (knownPlayerAbility is not null && catalog.MechanicsData?.Abilities.ContainsKey(knownPlayerAbility) != true)
            return Fail("source_four_trainer_ai_ability_history");
        try
        {
            var rng = frame.Rng; var trace = ImmutableArray.CreateBuilder<RandomTrace>();
            var choices = ImmutableArray.CreateBuilder<SourceTrainerEnemyChoice>();
            foreach (var actor in frame.Slots.Take(frame.BattlerCount).Where(c => c.Slot.Side == BattleSide.Opponent).Select(c => c.Slot))
            {
                if (onlyActor is { } selectedActor && actor != selectedActor || !SourceFourMedicineRules.CanSelect(frame, actor)) continue;
                var cell = frame.Get(actor); var user = cell.Body!;
                if (cell.Registers.Turn?.Lock is { } forced)
                {
                    choices.Add(new(actor, forced.Target, new(cell.Occupant!.Value, SourceActionKind.Move, forced.PpSlot, forced.Target), [], [], []));
                    continue;
                }
                if (frame.Layout == SourceBattleLayout.Singles && WildBattleRules.ForcedMoveSlot(user) is { } lockedSlot)
                {
                    choices.Add(new(actor, actor.Opposite, new(cell.Occupant!.Value, SourceActionKind.Move, lockedSlot, actor.Opposite), [], [], []));
                    continue;
                }
                // Source avoids the controller entirely during MULTIPLETURNS/RECHARGE.
                // Rollout is represented by its actual four-position lock once its recipe is admitted.
                if (SourceFourActionTurns.RequiresStruggle(catalog, frame, actor))
                {
                    // AreAllMovesUnusable runs before CHOOSEMOVE, so it skips AI setup/scoring.
                    // HandleAction_UseMove later discards this target and calls GetMoveTarget.
                    var initialTarget = actor.Opposite;
                    if (frame.Layout == SourceBattleLayout.PresetDoubles)
                    {
                        var draw = Gen3Rng.Next(rng, $"source-battle.slot-{actor.Value}.struggle-selection-target");
                        rng = draw.State; trace.Add(draw.Trace);
                        initialTarget = new BattlerSlot((actor.Value ^ 1) | (draw.Value & 2));
                    }
                    choices.Add(new(actor, initialTarget, SourceActionSelection.Struggle(cell.Occupant!.Value, initialTarget) with {
                        StruggleSelectionPrepared = true }, [], [], []));
                    continue;
                }
                // HandleTurnActionSelectionState checks all limitations before Encore. Encore
                // bypasses CHOOSEMOVE and all its RNG; the action owner calls GetMoveTarget later.
                if (user.EncoreMoveSlot is { } encored)
                {
                    if (encored >= user.Pokemon.Moves.Length || !SourceBattleKernel.SupportsRecipe(catalog.Moves[user.Pokemon.Moves[encored].Move]))
                        return Fail("source_four_trainer_ai_selected_recipe_not_supported");
                    var forcedTarget = FirstOpponent(frame);
                    choices.Add(new(actor, forcedTarget, new(cell.Occupant!.Value, SourceActionKind.Move, encored, forcedTarget), [], [], []));
                    continue;
                }
                var simulated = ImmutableArray.CreateBuilder<int>(4);
                for (int i = 0; i < 4; i++) {
                    var purpose = frame.Layout == SourceBattleLayout.Singles ? $"cerulean-trainer.ai.setup.slot.{i}.damage-percent"
                        : $"{tracePrefix}.ai.battler.{actor.Value}.setup.slot.{i}.damage-percent";
                    var draw = Gen3Rng.Next(rng, purpose, 16);
                    rng = draw.State; trace.Add(draw.Trace); simulated.Add(100 - draw.Trace.Result);
                }
                var target = actor.Opposite;
                if (frame.Layout == SourceBattleLayout.PresetDoubles)
                {
                    var targetDraw = Gen3Rng.Next(rng, $"{tracePrefix}.ai.battler.{actor.Value}.setup.target-flank");
                    rng = targetDraw.State; trace.Add(targetDraw.Trace);
                    target = new BattlerSlot(targetDraw.Value & 2);
                }
                if (frame.IsSourceAbsent(target)) target = new(target.Value ^ 2);
                if (!frame.CanAct(target)) return Fail("source_four_trainer_ai_no_living_target");
                var context = new SourceTrainerAiSlotContext(catalog, frame, actor, target, simulated.MoveToImmutable(), rng, aiFlags, staleCurrentMove: currentMove, knownTargetHoldEffect: knownPlayerHoldEffect);
                var result = CeruleanTrainerAi.Choose(new(user, frame.Get(target).Body!, knownPlayerAbility, aiFlags) {
                    UserHasReserves = context.HasReserves(actor.Side), TargetHasReserves = context.HasReserves(target.Side) }, rng,
                    rain: frame.Field.RainTurnsRemaining > 0,
                    reflect: frame.Sides[0].ReflectTurnsRemaining > 0,
                    slots: context);
                if (!result.Succeeded || result.MoveSlot is not { } chosen) return Fail(result.Diagnostic?.Code ?? "source_four_trainer_ai_choice");
                rng = result.Rng; trace.AddRange(result.Trace);
                currentMove = result.SourceCurrentMove ?? currentMove;
                var move = catalog.Moves[user.Pokemon.Moves[chosen].Move];
                if (!SourceBattleKernel.SupportsRecipe(move)) return Fail("source_four_trainer_ai_selected_recipe_not_supported");
                // OpponentHandleChooseMove rewrites BOTH to the first present player slot,
                // after scoring its random target. USER_OR_SELECTED also chooses the user.
                var selectedTarget = move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? actor
                    : move.Target == "MOVE_TARGET_BOTH" ? FirstOpponent(frame) : target;
                choices.Add(new(actor, target, new(cell.Occupant!.Value, SourceActionKind.Move, chosen, selectedTarget),
                    result.Scores, context.Simulated, result.AbilityReads));
            }
            if (choices.Count == 0) return Fail("source_four_trainer_ai_no_actor");
            return new(frame with { Rng = rng, Revision = checked(frame.Revision + 1) }, choices.ToImmutable(), trace.ToImmutable(), null) { SourceCurrentMove = currentMove };
        }
        catch (OverflowException) { return Fail("source_four_trainer_ai_counter_exhausted"); }
    }

}

/// <summary>Opaque validated explicit source positions for the shared scorer. It never creates a singles battle.</summary>
public sealed class SourceTrainerAiSlotContext
{
    internal bool Doubles => Frame.Layout == SourceBattleLayout.PresetDoubles;
    internal PokemonCatalog Catalog { get; }
    internal SourceBattleFrame Frame { get; }
    internal BattlerSlot Actor { get; }
    internal BattlerSlot Target { get; }
    internal ImmutableArray<SourceActionSelection> Prepared { get; }
    internal MoveId StaleCurrentMove { get; }
    internal string? KnownTargetHoldEffect { get; }
    private readonly uint aiFlags;
    private readonly RngState rng;
    internal ImmutableArray<int> Simulated { get; }
    internal SourceTrainerAiSlotContext(PokemonCatalog catalog, SourceBattleFrame frame, BattlerSlot actor,
        BattlerSlot target, ImmutableArray<int> simulated, RngState rng, uint aiFlags,
        ImmutableArray<SourceActionSelection> prepared = default, MoveId staleCurrentMove = default, string? knownTargetHoldEffect = null)
    { Catalog = catalog; Frame = frame; Actor = actor; Target = target; Simulated = simulated; this.rng = rng; this.aiFlags = aiFlags; Prepared = prepared.IsDefault ? [] : prepared; StaleCurrentMove = staleCurrentMove; KnownTargetHoldEffect = knownTargetHoldEffect; }
    internal string? Validate(CeruleanTrainerAiInput input, RngState candidateRng) =>
        input.User != Frame.Get(Actor).Body || input.Target != Frame.Get(Target).Body ||
        input.AiFlags != aiFlags || candidateRng != rng ? "source_four_trainer_ai_slot_context" : null;
    internal bool Matches(WildBattleCombatant user, WildBattleCombatant target) =>
        user == Frame.Get(Actor).Body && target == Frame.Get(Target).Body;
    internal int? Reserved(BattlerSlot slot) => Prepared.FirstOrDefault(a => a.Actor.Slot == slot)?.Incoming?.Index
        ?? Frame.Reservations.FirstOrDefault(r => r.Outgoing.Slot == slot)?.Incoming.Index;
    internal BattlerSlot SwitchPartner => Frame.IsSourceAbsent(Actor.Partner) ? Actor : Actor.Partner;
    internal bool SwitchEligible(int index, BattlerSlot? first = null)
    {
        var party = Frame.Party(Actor.Side).Members;
        var a = first ?? Actor; var b = SwitchPartner;
        return index >= 0 && index < party.Length && !party[index].IsEgg && party[index].Pokemon.CurrentHp > 0 &&
            index != Frame.Get(a).Member?.Index && index != Frame.Get(b).Member?.Index &&
            index != Reserved(a) && index != Reserved(b);
    }
    internal ImmutableArray<WildBattleCombatant> ActualParty => Frame.Party(Actor.Side).Members.Select((m, i) =>
        Frame.Slots.FirstOrDefault(c => c.Slot.Side == Actor.Side && c.Member?.Index == i)?.Body
            ?? new WildBattleCombatant(m.Pokemon, HeldItem: m.HeldItem, Status: m.Status) { KnockedOffItem = m.KnockedOffItem }).ToImmutableArray();
    internal SourceTrainerAiSlotContext WithTarget(BattlerSlot target, RngState current) =>
        new(Catalog, Frame with { Rng = current }, Actor, target, [], current, aiFlags, Prepared, StaleCurrentMove, KnownTargetHoldEffect);
    internal bool ShouldProcess(int slot) => slot < Frame.Get(Actor).Body!.Pokemon.Moves.Length &&
        Frame.Get(Actor).Body!.Pokemon.Moves[slot].CurrentPp > 0;
    internal bool IsUsable(int slot) => SourceMoveRestrictionRules.Selectable(Catalog, Frame.Get(Actor).Body!, slot,
        SourceMoveRestrictionRules.Opponents(Frame, Actor));
    internal bool HasReserves(BattleSide side) => Frame.Party(side).Members.Select((m, i) => (m, i)).Any(x =>
        !x.m.IsEgg && x.m.Pokemon.CurrentHp > 0 && !Frame.Slots.Any(c => c.Slot.Side == side && c.Member?.Index == x.i));
}
