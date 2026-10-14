using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasAdditionalPrimaryStatusRecipe(string effect) => effect is
        "EFFECT_PARALYZE" or "EFFECT_TOXIC" or "EFFECT_WILL_O_WISP";
    internal virtual bool PrimaryParalysisChecksLimber => true;
    internal virtual void PreparePrimaryToxicStorage() { }
    internal virtual bool PrimaryStatusAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose)
    {
        if (SourceGenericAccuracy(actor, target, move, purpose)) return true;
        SourceCalledProtectionFailure(actor, target, move.Id, "accuracy"); return false;
    }
    internal void ExecuteAdditionalPrimaryStatus(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move, string purpose, ref bool handled)
    {
        if (!HasAdditionalPrimaryStatusRecipe(move.Effect)) return;
        if (Invocation is null || !MetronomeSourceMoveDefinition.Matches(move))
            throw new SourceRecipeUnsupported("primary_status_source_definition");
        handled = true;
        var target = Invocation.Target;
        SpendEarlyPartyPp(actor, target, slot, move);
        ApplyAdditionalPrimaryStatus(catalog, actor, target, move, purpose);
    }
    internal void ApplyAdditionalPrimaryStatus(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose)
    {
        var body = Get(target); var types = WildBattleEffective.Types(catalog, body);
        bool Has(string t) => types.Type1 == t || types.Type2 == t;
        void Fail(string why) => SourceCalledProtectionFailure(actor, target, move.Id, why);
        void Prevent(string ability) { RecordAbility(target, ability); Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, target, move.Id, ability)); }
        string ability = WildBattleEffective.Ability(body);
        uint status;
        if (move.Effect == "EFFECT_PARALYZE")
        {
            if (PrimaryParalysisChecksLimber && ability == "ABILITY_LIMBER") { Prevent(ability); return; }
            ElementType Type(string symbol) => Enum.Parse<ElementType>(symbol[5..], true);
            if (TypeRules.OrderedEffects(Type(move.Type), Type(types.Type1), Type(types.Type2)).Any(x => x.MultiplierTenths == 0))
            { Fail("type-immunity"); return; }
            if (body.Status != 0) { Fail(body.Status == WildBattleStatus.Paralysis ? "already-paralyzed" : "existing-status"); return; }
            status = WildBattleStatus.Paralysis;
        }
        else if (move.Effect == "EFFECT_TOXIC")
        {
            if (ability == "ABILITY_IMMUNITY") { Prevent(ability); return; }
            if (body.Status == WildBattleStatus.Poison || WildBattleSourceFieldEffects.IsToxic(body.Status)) { Fail("already-poisoned"); return; }
            if (body.Status != 0) { Fail("existing-status"); return; }
            if (Has("TYPE_POISON") || Has("TYPE_STEEL"))
            { Add(n => new SourceEffectivenessEvent(n, Turn, actor, target, move.Id, WildBattleEffectiveness.NoEffect)); return; }
            status = WildBattleSourceFieldEffects.ToxicPoison;
        }
        else if (move.Effect == "EFFECT_WILL_O_WISP")
        {
            if (body.Status == WildBattleStatus.Burn) { Fail("already-burned"); return; }
            if (Has("TYPE_FIRE")) { Fail("fire-type-immunity"); return; }
            if (ability == "ABILITY_WATER_VEIL") { Prevent(ability); return; }
            if (body.Status != 0) { Fail("existing-status"); return; }
            status = WildBattleStatus.Burn;
        }
        else throw new SourceRecipeUnsupported("primary_status_result_not_registered");
        if (!PrimaryStatusAccuracy(actor, target, move, purpose)) return;
        if (move.Effect == "EFFECT_WILL_O_WISP" && SsAnneFlashFire(actor, target, move)) return;
        if (SourceCalledSafeguardBlocks(actor, target, move.Id)) return;
        if (status == WildBattleSourceFieldEffects.ToxicPoison)
        {
            if (body.Pokemon.CurrentHp == 0) return;
            PreparePrimaryToxicStorage();
        }
        if (PokedudeBattle && target.Side == BattleSide.Opponent) return;
        ChangeStatus(target, status, "move");
        SynchronizeStatus(catalog, target, status == WildBattleSourceFieldEffects.ToxicPoison ? WildBattleStatus.Poison : status);
    }
}
