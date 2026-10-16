using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasUtilityHpRecipe(string effect) => effect is
        "EFFECT_PAIN_SPLIT" or "EFFECT_MEMENTO" or "EFFECT_SPLASH";

    internal void ExecuteUtilityHp(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move)
    {
        SpendEarlyPartyPp(actor, target, slot, move);
        if (move.Effect == "EFFECT_SPLASH")
        {
            SourceStatRecoveryFail(actor, actor, move, "nothing-happened");
            return;
        }
        if (move.Effect == "EFFECT_PAIN_SPLIT")
        {
            // NO_ACC_CALC_CHECK_LOCK_ON precedes painsplitdmgcalc's Substitute test.
            if (!OneHitNoAccuracy(actor, target, move))
            { SourceStatRecoveryFail(actor, target, move, "pain-split-unreachable"); return; }
            if (SubstitutePresent(target))
            { SourceStatRecoveryFail(actor, target, move, "substitute"); return; }
            int average = (Get(actor).Pokemon.CurrentHp + Get(target).Pokemon.CurrentHp) / 2;
            SetHitItemReceipt(target, HitItemReceipt(target) with { Damage = 0xffff });
            int actorLost = Math.Max(0, Get(actor).Pokemon.CurrentHp - average);
            var receipt = HitItemReceipt(actor);
            if (receipt.Damage == 0 && actorLost > 0)
                SetHitItemReceipt(actor, receipt with { Damage = actorLost });
            HitItemsCommandDamage(Get(target).Pokemon.CurrentHp - average);
            // This normal-type move is explicitly excluded from physical damage receipts.
            foreach (var recipient in new[] { actor, target })
            {
                var mon = Get(recipient); int before = mon.Pokemon.CurrentHp;
                int after = Math.Min(average, mon.Pokemon.Stats.MaxHp);
                Set(recipient, mon with { Pokemon = mon.Pokemon with { CurrentHp = after } });
                if (after >= before)
                    Add(n => new SourceRecoveredEvent(n, Turn, recipient, move.Id, before, after, after - before));
                else
                    Add(n => new SourceHpChangedEvent(n, Turn, actor, recipient, move.Id, before, after, before - after, false, 100));
            }
            return;
        }
        if (move.Effect != "EFFECT_MEMENTO") throw new SourceRecipeUnsupported("source_utility_hp_effect");
        var foe = Get(target);
        if (!foe.Protected && foe.AttackStage == 0 && foe.SpecialAttackStageDelta == -6)
        { SourceStatRecoveryFail(actor, target, move, "stats-at-minimum"); return; }
        var user = Get(actor); int hp = user.Pokemon.CurrentHp;
        HitItemsCommandDamage(hp, noEffect: foe.Protected);
        // setatkhptozero is not datahpupdate: no recoil or retaliation damage receipt.
        Set(actor, user with { Pokemon = user.Pokemon with { CurrentHp = 0 } });
        Add(n => new SourceHpChangedEvent(n, Turn, actor, actor, move.Id, hp, 0, hp, false, 100));
        if (foe.Protected) SourceStatRecoveryFail(actor, target, move, "protected");
        else if (SubstitutePresent(target)) SourceStatRecoveryFail(actor, target, move, "substitute");
        else
        {
            bool shown = false;
            SourceChangeStat(actor, target, WildBattleStat.Attack, -2, move.Id, ref shown);
            SourceChangeStat(actor, target, WildBattleStat.SpecialAttack, -2, move.Id, ref shown);
        }
        ForestFaint(actor);
        ForestOutcome(true);
    }
}
