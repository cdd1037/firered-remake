using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

// The receipt is retained unchanged. LearnedMove identifies the slot that paid,
// which need not contain Command.CurrentMove after a source call.
internal sealed record CampaignAnimationCallerPp(SourcePpCommandEvent Command, MoveId LearnedMove, bool NoPpDeduct);

internal sealed record CampaignAnimationInvocation(
    bool IsCalled, BattlerSlot Actor, BattlerSlot Target, MoveId CurrentMove,
    MoveId ChosenMove, MoveId OriginalChosenMove, int ChosenSlot, BattlerSlot PpOwner,
    int PpSlot, MoveId LearnedPpMove, bool NoPpDeduct, int AttackIndex, int UsedIndex,
    int ChainStartIndex, ImmutableArray<CampaignAnimationCallerPp> CallerPpLedger)
{
    // These indexes name real source receipts. Headerless setup has no attack/used
    // page; callers must not dereference its explicit -1 header indexes.
    public int? PpBoundaryIndex { get; init; }
    public int? EarlyLeafPpIndex { get; init; }
    public int PrefixStartIndex { get; init; } = AttackIndex;

    private sealed record Link(int AttackIndex, SourceAttackStringEvent Attack,
        SourceMoveUsedEvent? Used, SourcePpCommandEvent? Pp, SourceSleepTalkEvent? Sleep,
        SourceCallerSelectionEvent Selection, SourceCalledMoveTransitionEvent Transition);

    // Identity only: result eligibility and the leaf PP suffix remain downstream.
    internal static CampaignAnimationInvocation? TryRead(SourceBattleEvent[] events, int usedIndex, int slots,
        IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves)
    {
        if (slots is not (2 or 4) || usedIndex < 1 || usedIndex >= events.Length ||
            events[usedIndex] is not SourceMoveUsedEvent used ||
            events[usedIndex - 1] is not SourceAttackStringEvent attack ||
            !Slot(used.Actor, slots) || !Slot(used.Target, slots) || used.Slot is < 0 or > 3 ||
            attack.Actor != used.Actor || attack.CurrentMove != used.Move || attack.Turn != used.Turn ||
            attack.Sequence >= used.Sequence || Definition(used.Move, moves) is not {} leaf)
            return null;
        // Direct Struggle retains its specialized -1-slot matcher. Its real chosen
        // slot lives in the retained register and is not recoverable from MoveUsed.
        int leafAttack = usedIndex - 1, prefix = leafAttack;
        BattlerSlot chainTarget = used.Target;
        if (prefix > 0 && events[prefix - 1] is SourceMoveTargetResolvedEvent resolved)
        {
            if (leaf.Effect is not ("EFFECT_COUNTER" or "EFFECT_MIRROR_COAT") ||
                resolved.Reason != SourceMoveTargetResolutionReason.Retaliation || resolved.Actor != used.Actor ||
                resolved.CurrentMove != used.Move || resolved.Turn != used.Turn || resolved.Target != used.Target ||
                !Slot(resolved.PriorTarget, slots) || resolved.PriorTarget == resolved.Target ||
                resolved.Target.Side == used.Actor.Side || resolved.Sequence + 1 != attack.Sequence)
                return null;
            chainTarget = resolved.PriorTarget; prefix--;
        }
        SourcePpCommandEvent? early = null;
        bool sunny = false;
        if (leaf.Effect == "EFFECT_SUBSTITUTE" && prefix > 0 && events[prefix - 1] is SourcePpCommandEvent substitutePp)
        { early = substitutePp; prefix--; }
        else if (leaf.Effect == "EFFECT_SOLAR_BEAM" && prefix >= 3 &&
            events[prefix - 1] is SourceMtMoonVolatileEvent { Volatile: "called-charge", Before: 1, After: 0, Reason: "released" } released &&
            events[prefix - 2] is SourcePpCommandEvent solarPp &&
            events[prefix - 3] is SourceMtMoonVolatileEvent { Volatile: "called-charge", Before: 0, After: 1, Reason: "move" } prepared)
        {
            if (prepared.Side != used.Actor || released.Side != used.Actor || prepared.Turn != used.Turn ||
                released.Turn != used.Turn || solarPp.Turn != used.Turn || prepared.Sequence + 1 != solarPp.Sequence ||
                solarPp.Sequence + 1 != released.Sequence || released.Sequence + 1 != attack.Sequence)
                return null;
            early = solarPp; prefix -= 3; sunny = true;
        }
        SourceAbilityActivatedEvent? lightningRod = null;
        if (prefix > 0 && events[prefix - 1] is SourceAbilityActivatedEvent { Ability: "ABILITY_LIGHTNING_ROD" } redirected)
        { lightningRod = redirected; prefix--; }
        var descriptor = ReadChain(events, prefix, usedIndex, used.Actor, chainTarget, used.Move,
            attack.ChosenMove, used.Slot, used.Turn, slots, moves, leafAttack, usedIndex);
        if (descriptor is null) return null;
        descriptor = descriptor with { Target = used.Target };
        if (!descriptor.IsCalled && !sunny) return descriptor;
        if (used.Slot != (leaf.Effect == "EFFECT_SUBSTITUTE" ? descriptor.ChosenSlot : descriptor.PpSlot)) return null;
        if (lightningRod is not null && (lightningRod.Side != used.Target || leaf.Type != "TYPE_ELECTRIC" || used.Target == used.Actor))
            return null;
        if (early is not null)
        {
            if (early.Actor != used.Actor || early.PpOwner != descriptor.PpOwner || early.Target != used.Target ||
                early.CurrentMove != used.Move || early.ChosenMove != descriptor.ChosenMove || early.PpSlot != descriptor.PpSlot ||
                early.CommandCost < 1 || early.CommandCost > (sunny ? 2 : 1) || !Consumption(early, descriptor.NoPpDeduct) ||
                (sunny ? early.After != used.PpBefore : early.Before != used.PpBefore || early.After != used.PpAfter || used.Target != used.Actor))
                return null;
            if (sunny)
                descriptor = descriptor with { NoPpDeduct = true,
                    CallerPpLedger = descriptor.CallerPpLedger.Add(new(early, descriptor.LearnedPpMove, descriptor.NoPpDeduct)) };
            descriptor = descriptor with { EarlyLeafPpIndex = sunny ? leafAttack - 2 : leafAttack - 1 };
        }
        else if (leaf.Effect == "EFFECT_SUBSTITUTE") return null;
        return descriptor with { PrefixStartIndex = prefix };
    }

    internal static CampaignAnimationInvocation? TryReadChargeSetup(SourceBattleEvent[] events, int chargeIndex, int slots,
        IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves)
    {
        if (slots is not (2 or 4) || chargeIndex < 1 || chargeIndex >= events.Length ||
            events[chargeIndex] is not SourceMtMoonVolatileEvent { Volatile: "called-charge", Before: 0, After: 1, Reason: "move" } charge ||
            events[chargeIndex - 1] is not SourcePpCommandEvent pp || !Slot(pp.Actor, slots) || !Slot(pp.Target, slots) ||
            pp.PpSlot is < 0 or > 3 || pp.PpOwner != pp.Actor || charge.Side != pp.Actor || charge.Turn != pp.Turn ||
            pp.Sequence + 1 != charge.Sequence || Definition(pp.CurrentMove, moves) is null)
            return null;
        int ppIndex = chargeIndex - 1;
        var descriptor = ReadChain(events, ppIndex, chargeIndex, pp.Actor, pp.Target, pp.CurrentMove,
            pp.ChosenMove, pp.PpSlot, pp.Turn, slots, moves, -1, -1);
        if (descriptor is null || descriptor.PpOwner != pp.PpOwner || descriptor.PpSlot != pp.PpSlot) return null;
        return descriptor with { PpBoundaryIndex = ppIndex, PrefixStartIndex = ppIndex };
    }

    // Both real entry shapes meet here. chainRight is the first actual leaf entry
    // receipt, immediately after the last transition when the invocation is called.
    // No fabricated attack/MoveUsed row participates in this proof.
    private static CampaignAnimationInvocation? ReadChain(SourceBattleEvent[] events, int chainRight, int entryEnd,
        BattlerSlot actor, BattlerSlot target, MoveId leaf, MoveId chosen, int directSlot, int turn, int slots,
        IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves, int attackIndex, int usedIndex)
    {
        int cursor = chainRight;
        MoveId expectedCurrent = leaf;
        var reversed = new List<Link>();
        while (true)
        {
            int previous = cursor - 1;
            if (previous < 0 || events[previous] is not SourceCalledMoveTransitionEvent transition)
            {
                if (chosen != expectedCurrent || previous >= 0 && events[previous].Turn == turn &&
                    (events[previous] is SourceCallerSelectionEvent or SourceMoveTargetResolvedEvent ||
                     events[previous] is SourceAttackStringEvent orphan && orphan.Actor == actor))
                    return null;
                break;
            }
            if (previous < 1 || events[previous - 1] is not SourceCallerSelectionEvent selection ||
                selection.Result is not {} selected || selected != expectedCurrent ||
                Definition(selection.Caller, moves) is not {} caller || !Caller(caller))
                return null;
            int parentAttack = previous - 2;
            SourceMoveUsedEvent? callerUsed = null;
            SourcePpCommandEvent? callerPp = null;
            SourceSleepTalkEvent? sleep = null;
            if (caller.Effect == "EFFECT_SLEEP_TALK")
            {
                if (parentAttack < 3 || events[parentAttack] is not SourceSleepTalkEvent talk ||
                    events[parentAttack - 1] is not SourcePpCommandEvent pp ||
                    events[parentAttack - 2] is not SourceMoveUsedEvent callerPage)
                    return null;
                sleep = talk; callerPp = pp; callerUsed = callerPage; parentAttack -= 3;
            }
            if (parentAttack < 0 || events[parentAttack] is not SourceAttackStringEvent parent ||
                parent.CurrentMove != selection.Caller || parent.ChosenMove != chosen ||
                parent.Actor != actor || selection.Actor != actor)
                return null;
            reversed.Add(new(parentAttack, parent, callerUsed, callerPp, sleep, selection, transition));
            cursor = parentAttack; expectedCurrent = parent.CurrentMove;
        }
        if (reversed.Count == 0)
            return new(false, actor, target, leaf, chosen, chosen, directSlot, actor, directSlot, leaf,
                false, attackIndex, usedIndex, chainRight, []) { PrefixStartIndex = chainRight };
        for (int i = cursor; i <= entryEnd; i++)
            if (events[i].Turn != turn || i > cursor && events[i].Sequence <= events[i - 1].Sequence)
                return null;
        reversed.Reverse();
        int chosenSlot = reversed[0].Transition.Invocation.ChosenSlot;
        if (chosenSlot is < 0 or > 3) return null;
        int ppSlot = chosenSlot;
        MoveId currentMove = chosen, learned = chosen;
        MoveId? called = null;
        bool suppressed = false;
        BattlerSlot? priorTarget = null;
        ulong priorSelectionCursor = 0;
        var ledger = ImmutableArray.CreateBuilder<CampaignAnimationCallerPp>();
        foreach (var link in reversed)
        {
            var selected = link.Selection.Result!.Value;
            var caller = Definition(link.Attack.CurrentMove, moves)!;
            var selectedDefinition = Definition(selected, moves);
            if (caller.Id != currentMove || selectedDefinition is null ||
                !Selection(caller, selectedDefinition, link.Selection.CandidateSourceMoveIds, slots, moves) ||
                link.Selection.RngCursor < priorSelectionCursor)
                return null;
            if (caller.Effect == "EFFECT_SLEEP_TALK")
            {
                var page = link.Used!; var pp = link.Pp!; var talk = link.Sleep!;
                if (page.Actor != actor || page.Move != caller.Id || page.Slot != ppSlot ||
                    !Slot(page.Target, slots) || priorTarget is {} expected && page.Target != expected ||
                    pp.Actor != actor || pp.PpOwner != actor || pp.Target != page.Target ||
                    pp.ChosenMove != chosen || pp.CurrentMove != caller.Id || pp.PpSlot != ppSlot ||
                    pp.Before != page.PpBefore || pp.After != page.PpAfter || pp.CommandCost is < 1 or > 2 ||
                    !Consumption(pp, suppressed) || pp.RngCursor > link.Selection.RngCursor || talk.Side != actor || talk.CalledMove != selected ||
                    talk.CalledSlot is < 0 or > 3 || talk.CalledSlot == ppSlot)
                    return null;
                ledger.Add(new(pp, learned, suppressed));
                ppSlot = talk.CalledSlot; learned = selected; called = selected; suppressed = true;
            }
            else if (caller.Effect == "EFFECT_ASSIST") called = selected;
            var invocation = link.Transition.Invocation;
            if (invocation.Actor != actor || invocation.PpOwner != actor ||
                invocation.OriginalChosenMove != chosen || invocation.ChosenMove != chosen ||
                invocation.CurrentMove != selected || invocation.CalledMove != called ||
                invocation.ChosenSlot != chosenSlot || invocation.PpSlot != ppSlot ||
                !Slot(invocation.Target, slots) || invocation.CancellerPhase != WildBattleAttackCancellerPhase.ConditionsPassed ||
                invocation.Flags != (WildBattleInvocationFlags.Obeys | (suppressed ? WildBattleInvocationFlags.NoPpDeduct : WildBattleInvocationFlags.None)))
                return null;
            priorTarget = invocation.Target; currentMove = selected; priorSelectionCursor = link.Selection.RngCursor;
        }
        if (currentMove != leaf || priorTarget != target) return null;
        return new(true, actor, target, leaf, chosen, chosen, chosenSlot, actor, ppSlot, learned, suppressed,
            attackIndex, usedIndex, cursor, ledger.ToImmutable()) { PrefixStartIndex = chainRight };
    }

    private static bool Slot(BattlerSlot slot, int slots) => slot.Value >= 0 && slot.Value < slots;
    private static PokemonMoveDefinition? Definition(MoveId move, IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves) =>
        CampaignMoveAnimationPresentation.Definition(move, moves);
    private static bool Caller(PokemonMoveDefinition definition) => definition.Power == 0 && definition.Target == "MOVE_TARGET_DEPENDS" &&
        definition.Effect is "EFFECT_METRONOME" or "EFFECT_SLEEP_TALK" or "EFFECT_ASSIST" or "EFFECT_MIRROR_MOVE" or "EFFECT_NATURE_POWER";
    private static bool Consumption(SourcePpCommandEvent pp, bool suppressed) =>
        pp.Before is >= 0 and <= 255 && pp.After is >= 0 and <= 255 &&
        (suppressed ? pp.After == pp.Before : pp.Before > 0 && pp.After == Math.Max(0, pp.Before - pp.CommandCost));

    private static bool Selection(PokemonMoveDefinition caller, PokemonMoveDefinition selected,
        ImmutableArray<int> candidates, int slots, IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves)
    {
        if (candidates.IsDefault) return false;
        PokemonMoveDefinition? SourceDefinition(int id)
        {
            PokemonMoveDefinition? found = null;
            foreach (var value in moves!.Values)
            {
                if (value.SourceMoveId != id) continue;
                if (found is not null || Definition(value.Id, moves) is null) return null;
                found = value;
            }
            return found;
        }
        bool AssistChoice(PokemonMoveDefinition definition) => WildBattleSourceCallers.IsDirectMetronomeChoice(definition.SourceMoveId) &&
            definition.Effect is not ("EFFECT_MIRROR_MOVE" or "EFFECT_ASSIST");
        return caller.Effect switch
        {
            "EFFECT_METRONOME" => candidates.IsEmpty && WildBattleSourceCallers.IsDirectMetronomeChoice(selected.SourceMoveId),
            "EFFECT_NATURE_POWER" => candidates.IsEmpty &&
                Enumerable.Range((int)WildBattleTerrain.Grass, (int)WildBattleTerrain.Plain - (int)WildBattleTerrain.Grass + 1)
                    .Any(terrain => WildBattleSourceCallers.NaturePower((WildBattleTerrain)terrain) == selected.SourceMoveId),
            "EFFECT_MIRROR_MOVE" => candidates.Length < slots && selected.Flags.Contains("FLAG_MIRROR_MOVE_AFFECTED") &&
                candidates.All(id => SourceDefinition(id) is {} definition && definition.Flags.Contains("FLAG_MIRROR_MOVE_AFFECTED")),
            "EFFECT_ASSIST" => candidates.Length is >= 1 and <= 20 && candidates.Contains(selected.SourceMoveId) &&
                AssistChoice(selected) && candidates.All(id => SourceDefinition(id) is {} definition && AssistChoice(definition)),
            "EFFECT_SLEEP_TALK" => candidates.Length is >= 1 and <= 4 && candidates.Contains(selected.SourceMoveId) &&
                WildBattleSourceCallers.IsSleepTalkChoice(selected) && candidates.All(id => SourceDefinition(id) is {} definition && WildBattleSourceCallers.IsSleepTalkChoice(definition)),
            _ => false
        };
    }
}
