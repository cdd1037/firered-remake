using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    // Lifted from EarlyStarterPrimaryMove's source-invocation branches; the resolved
    // target remains a position, including a selected ally, throughout every command.
    internal void SourcePrimaryStatus(PokemonCatalog catalog, BattlerSlot actorSlot, int slot, PokemonMoveDefinition move, string purpose)
    {
        var targetSlot = Invocation!.Target; var actor = Get(actorSlot); var target = Get(targetSlot);
        SpendEarlyPartyPp(actorSlot, targetSlot, slot, move);
        var types = WildBattleEffective.Types(catalog, target); bool Has(string t) => types.Type1 == t || types.Type2 == t;
        void Fail(string reason) => SourceStatRecoveryFail(actorSlot, targetSlot, move, reason);
        string ability = WildBattleEffective.Ability(target);
        if (move.Effect == "EFFECT_POISON" && ability == "ABILITY_IMMUNITY")
        { RecordAbility(targetSlot, ability); Add(n => new SourceAbilityPreventedEvent(n, Turn, actorSlot, targetSlot, move.Id, ability)); return; }
        if (move.Effect == "EFFECT_POISON" && (target.Status == WildBattleStatus.Poison || WildBattleSourceFieldEffects.IsToxic(target.Status)))
        { Fail("already-poisoned"); return; }
        if (move.Effect == "EFFECT_POISON" && (Has("TYPE_POISON") || Has("TYPE_STEEL"))) { Fail("poison-type-immunity"); return; }
        if (move.Effect == "EFFECT_SLEEP" && WildBattleStatus.IsSleeping(target.Status)) { Fail("already-asleep"); return; }
        if (move.Effect == "EFFECT_SLEEP" && ability is "ABILITY_VITAL_SPIRIT" or "ABILITY_INSOMNIA")
        { RecordAbility(targetSlot, ability); Add(n => new SourceAbilityPreventedEvent(n, Turn, actorSlot, targetSlot, move.Id, ability)); return; }
        if (move.Effect == "EFFECT_SLEEP" && UproarPreventsSleep(targetSlot)) { Fail("uproar"); return; }
        if (target.Status != WildBattleStatus.None) { Fail("existing-status"); return; }
        int accuracy = WildBattleDamage.Accuracy(move.Accuracy, actor.AccuracyStage, target.EvasionStage);
        if (WildBattleEffective.Ability(actor) == "ABILITY_COMPOUND_EYES") accuracy = accuracy * 130 / 100;
        if (target.Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED") || BillUnderground(actorSlot, targetSlot, move) || !CalledSureHit(actorSlot, targetSlot) && Draw($"{purpose}.accuracy", 100) % 100 + 1 > accuracy)
        { SourceGenericMiss(actorSlot, targetSlot, move); return; }
        if (SourceCalledSafeguardBlocks(actorSlot, targetSlot, move.Id)) return;
        if (move.Effect == "EFFECT_POISON") { ChangeStatus(targetSlot, WildBattleStatus.Poison, "move"); SynchronizeStatus(catalog, targetSlot, WildBattleStatus.Poison); }
        else ChangeStatus(targetSlot, (uint)((Draw($"{purpose}.sleep-duration", 4) & 3) + 2), "move");
    }
    internal abstract bool UproarPreventsSleep(BattlerSlot slot);
    internal abstract void SynchronizeStatus(PokemonCatalog catalog, BattlerSlot target, uint status);
}
