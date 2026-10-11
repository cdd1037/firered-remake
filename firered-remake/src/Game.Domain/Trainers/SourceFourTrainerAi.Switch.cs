using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

public sealed record SourceTrainerSwitchResult(int? PartyIndex, RngState Rng,
    ImmutableArray<RandomTrace> Trace, ImmutableArray<int> Scores, string? Fault)
{
    public bool Succeeded => Fault is null;
}

public static partial class SourceFourTrainerAi
{
    /// <summary>Pure source ShouldSwitch for one actual enemy position, before trainer items.
    /// Earlier selected switches reserve their actual incoming members. The caller owns selection order.</summary>
    public static SourceTrainerSwitchResult DecideSwitch(PokemonCatalog catalog, SourceBattleFrame frame,
        BattlerSlot actor, MoveId staleCurrentMove, ImmutableArray<SourceActionSelection> preparedSwitches = default)
    {
        SourceTrainerSwitchResult Fail(string code) => new(null, frame.Rng, [], [], code);
        if (ValidateSwitchContext(catalog, frame, actor, staleCurrentMove, preparedSwitches) is { } invalid) return Fail(invalid);
        if (!frame.CanAct(actor) || frame.Outcome != WildBattleOutcome.Ongoing || frame.FourTurn is not null ||
            !frame.Queue.IsEmpty || !frame.PendingSwitches.IsEmpty || frame.DeferredKoVacancyMask != 0)
            return Fail("source_four_trainer_switch_phase");
        // These conditions bypass CHOOSEACTION entirely; Encore does not.
        if (frame.Get(actor).Registers.Turn?.Lock is not null) return new(null, frame.Rng, [], [], null);
        var target = FirstOpponent(frame);
        var slots = new SourceTrainerAiSlotContext(catalog, frame, actor, target, [], frame.Rng, 1,
            preparedSwitches, staleCurrentMove);
        if (slots.Reserved(actor) is not null) return Fail("source_four_trainer_switch_already_chosen");
        var user = frame.Get(actor).Body!;
        var result = CeruleanTrainerActionAi.DecideSwitch(catalog,
            new(new("source:four-trainer"), slots.ActualParty, frame.Get(actor).Member!.Value.Index,
                user, frame.Get(target).Body!, user.LastLandedMove), frame.Rng, fuchsiaRules: true, slots: slots);
        return result.Succeeded ? new(result.PartyIndex, result.Rng, result.Trace, [], null) : Fail(result.Diagnostic!.Code);
    }

    /// <summary>OpponentHandleChoosePokemon / GetMostSuitableMonToSwitchInto, including original
    /// random opponent-flank selection. Shared replacement scoring reads these actual positions.</summary>
    public static SourceTrainerSwitchResult ChooseReplacement(PokemonCatalog catalog, SourceBattleFrame frame,
        BattlerSlot actor, MoveId staleCurrentMove, ImmutableArray<SourceActionSelection> preparedSwitches = default)
    {
        if (ValidateSwitchContext(catalog, frame, actor, staleCurrentMove, preparedSwitches) is { } invalid)
            return new(null, frame.Rng, [], [], invalid);
        if (frame.Outcome != WildBattleOutcome.Ongoing || frame.Get(actor).Body!.Pokemon.CurrentHp > 0 &&
            !frame.PendingSwitches.Any(p => p.Slot == actor && p.Kind == WildBattleCalledSwitchKind.BatonPass))
            return new(null, frame.Rng, [], [], "source_four_trainer_replacement_phase");
        return ReplacementCore(new(catalog, frame, actor, FirstOpponent(frame), [], frame.Rng, 1,
            preparedSwitches, staleCurrentMove), forced: true);
    }

    internal static SourceTrainerSwitchResult ReplacementCore(SourceTrainerAiSlotContext slots, bool forced)
    {
        var frame = slots.Frame; var actor = slots.Actor; var rng = frame.Rng;
        SourceTrainerSwitchResult Fail(string code) => new(null, frame.Rng, [], [], code);
        if (slots.Reserved(actor) is { } already) return new(already, rng, [], [], null);
        if (!Enumerable.Range(0, slots.ActualParty.Length).Any(i => slots.SwitchEligible(i)))
            return Fail("source_four_trainer_replacement_no_reserve");
        try
        {
            var draw = Gen3Rng.Next(rng, $"source-trainer.replacement.battler.{actor.Value}.target-flank");
            rng = draw.State; var target = new BattlerSlot(draw.Value & 2);
            if (frame.IsSourceAbsent(target)) target = target.Partner;
            if (frame.Get(target).Body is null || frame.IsSourceAbsent(target)) return Fail("source_four_trainer_replacement_no_target");
            var context = slots.WithTarget(target, rng);
            var result = CeruleanTrainerAiReplacement.Choose(slots.Catalog,
                new(context.ActualParty, frame.Get(actor).Member!.Value.Index, frame.Get(actor).Body!,
                    frame.Get(target).Body!, slots.StaleCurrentMove), fuchsiaRules: true,
                rain: frame.Field.RainTurnsRemaining > 0, reflect: frame.Sides[(int)target.Side].ReflectTurnsRemaining > 0,
                slots: context);
            if (!result.Succeeded) return Fail(result.Diagnostic!.Code);
            int? chosen = result.PartyIndex;
            if (chosen is null)
            {
                // The two original callers differ: forced replacement fallback excludes active
                // members only; voluntary fallback additionally excludes both reservations.
                chosen = Enumerable.Range(0, context.ActualParty.Length).Cast<int?>().FirstOrDefault(i =>
                    context.ActualParty[i!.Value].Pokemon.CurrentHp > 0 &&
                    !frame.Slots.Any(c => c.Slot.Side == actor.Side && c.Member?.Index == i) &&
                    (forced || context.Reserved(new(1)) != i && context.Reserved(new(3)) != i));
            }
            return chosen is null ? Fail("source_four_trainer_replacement_no_candidate")
                : new(chosen, rng, [draw.Trace], result.Scores, null);
        }
        catch (OverflowException) { return Fail("source_four_trainer_replacement_rng_exhausted"); }
    }

    private static string? ValidateSwitchContext(PokemonCatalog catalog, SourceBattleFrame frame,
        BattlerSlot actor, MoveId stale, ImmutableArray<SourceActionSelection> prepared)
    {
        if (SourceFourActionTurns.ValidateCapability(catalog, frame) is { } capability) return capability;
        if (actor.Value is not (1 or 3) || frame.Get(actor).Body is null || frame.Get(actor).Member is null)
            return "source_four_trainer_switch_actor";
        if (stale != ForestTrainerCombatRules.NoMove && stale != WildBattleRules.StruggleMove &&
            (!catalog.Moves.TryGetValue(stale, out var move) || !MetronomeSourceMoveDefinition.Matches(move)))
            return "source_four_trainer_switch_stale_move";
        if (prepared.IsDefault) return null;
        var actors = new HashSet<BattlerSlot>(); var incoming = new HashSet<PartyMemberKey>();
        foreach (var selection in prepared)
        {
            if (selection is null || selection.Kind != SourceActionKind.Switch || !frame.IsCurrent(selection.Actor) ||
                selection.Actor.Slot.Side != BattleSide.Opponent || selection.Incoming is not { Side: BattleSide.Opponent } member ||
                member.Index < 0 || member.Index >= frame.Party(BattleSide.Opponent).Members.Length ||
                frame.Party(BattleSide.Opponent).Members[member.Index] is { IsEgg: true } ||
                frame.Party(BattleSide.Opponent).Members[member.Index].Pokemon.CurrentHp == 0 ||
                frame.Slots.Any(c => c.Member == member) || !actors.Add(selection.Actor.Slot) || !incoming.Add(member))
                return "source_four_trainer_switch_prepared_selection";
        }
        return null;
    }
}
