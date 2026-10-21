using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{

    // Cmd_attackcanceler calls this AFTER move-blocking abilities and the no-PP guard.
    // The canonical owner handles both single and double battles.
    internal SourceObedienceDecision CheckObedience(PokemonCatalog catalog, string purpose)
    {
        SetPreparedInvocation(Invocation! with { Flags = Invocation!.Flags & ~WildBattleInvocationFlags.AllowNoPp });
        var invocation = Invocation!;
        var actor = invocation.Actor;
        var context = ObedienceContext;
        if (context is null || actor.Side == BattleSide.Opponent || invocation.Flags.HasFlag(WildBattleInvocationFlags.Obeys) || ObedienceMultiTurn(actor))
            return Obeys();
        var mon = Get(actor);
        var original = WildBattleTransformationRules.PersistentPokemon(mon);
        var facts = context.PlayerParty.Single(m => m.Key == ObedienceMember(actor));
        int unusable = 0;
        // Source excludes gCurrMovePos, which remains a real retained slot for forced Struggle.
        int selected = invocation.PpSlot >= 0 ? invocation.PpSlot : invocation.ChosenSlot;
        for (int slot = 0; slot < 4; slot++)
            if (slot == selected || !SourceMoveRestrictionRules.Selectable(catalog, mon, slot,
                ActiveSlots.Where(s => s.Side != actor.Side).Select(Get))) unusable |= 1 << slot;
        var decision = SourceObedienceRules.Decide(new(mon.Pokemon.Level, true,
            SourceObedienceRules.IsOtherTrainer(original.OriginalTrainerId, facts.OriginalTrainerName,
                context.PlayerTrainerId, context.PlayerTrainerName), context.BadgeMask, mon.Status,
            WildBattleEffective.Ability(mon), invocation.CurrentMove, (byte)unusable, ActiveSlots.Any(UproarLocked)),
            (name, bound) => Draw(purpose + ".obedience-" + name, bound));
        if (decision.Outcome == SourceObedienceOutcome.Obey) return Obeys();
        if (invocation.CurrentMove.Value == "pret:MOVE_RAGE") ChangeRage(actor, false, "disobedience");
        Add(n => new SourceObedienceEvent(n, Turn, actor, invocation.CurrentMove, decision));
        if (decision.SourceReturn == 2)
            SetPreparedInvocation(Invocation! with { Flags = Invocation!.Flags | WildBattleInvocationFlags.Obeys });
        switch (decision.Outcome)
        {
            case SourceObedienceOutcome.AlternateMove:
                int slot = decision.MoveSlot!.Value;
                var move = mon.Pokemon.Moves[slot].Move;
                SetPreparedInvocation(Invocation! with { ChosenSlot = slot, PpSlot = slot, ChosenMove = move,
                    CurrentMove = move, CalledMove = move, Flags = Invocation!.Flags | WildBattleInvocationFlags.DisobedientMove });
                break;
            case SourceObedienceOutcome.Nap:
                CancellerCancelMultiTurn(actor, "disobedience-nap");
                ChangeStatus(actor, (uint)((Draw(purpose + ".obedience-sleep-duration", 4) & 3) + 2), "disobedience-nap");
                break;
            case SourceObedienceOutcome.SelfHit:
                SetPreparedInvocation(Invocation! with { Target = actor });
                CancellerCancelMultiTurn(actor, "disobedience-self-hit");
                mon = Get(actor);
                int percent = 100 - Draw(purpose + ".obedience-self-damage", 16) % 16;
                int damage = SourceConfusionDamageWithItems(mon, percent, true, true,
                    catalog.Moves[invocation.CurrentMove].Effect == "EFFECT_EXPLOSION", true, catalog);
                if (SourceHeldDamageEnabled) damage = AdjustHeldDamage(actor, invocation.CurrentMove, damage, purpose, normal2: true);
                HitItemsConfusionDamage(actor, damage);
                int before = mon.Pokemon.CurrentHp, after = Math.Max(0, before - damage);
                Set(actor, Get(actor) with { Pokemon = Get(actor).Pokemon with { CurrentHp = after } });
                Add(n => new SourceConfusionSelfHitEvent(n, Turn, actor, invocation.CurrentMove, before, after, damage, percent));
                SourceDamageResultMessage(actor, actor, catalog.Moves[invocation.CurrentMove]);
                if (after == 0) ForestFaint(actor);
                ForestOutcome(true);
                break;
        }
        return decision;
        SourceObedienceDecision Obeys()
        {
            SetPreparedInvocation(Invocation! with { Flags = (Invocation!.Flags | WildBattleInvocationFlags.Obeys) & ~WildBattleInvocationFlags.AllowNoPp });
            return new(SourceObedienceOutcome.Obey);
        }
    }
}
