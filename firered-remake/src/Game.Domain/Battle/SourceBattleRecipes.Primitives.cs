using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;
namespace Game.Domain.Battle;

// Shared source commands; the older historical profiles retain their compatibility helpers.
internal abstract partial class SourceRecipeContext
{
    internal bool EarlyPartyAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose, bool reportMiss, bool? sandstorm = null)
    {
        if (CalledSureHit(actor, target)) return true;
        if (BillUnderground(actor,target,move)) { if(reportMiss)SourceGenericMiss(actor,target,move);return false; }
        int accuracy = WildBattleDamage.Accuracy(move.Accuracy, Get(actor).AccuracyStage, Get(target).Identified ? 6 : Get(target).EvasionStage);
        if (WildBattleEffective.Ability(Get(actor)) == "ABILITY_COMPOUND_EYES") accuracy = accuracy * 130 / 100;
        if ((sandstorm ?? SandstormTurnsRemaining > 0) && !SourceCalledWeatherSuppressed && WildBattleEffective.Ability(Get(target)) == "ABILITY_SAND_VEIL") accuracy = accuracy * 80 / 100;
        // Cmd_accuracycheck applies Hustle by original type category, including
        // physical-type status moves. Always-hit/sure-hit commands bypass this path.
        if (WildBattleEffective.Ability(Get(actor)) == "ABILITY_HUSTLE" &&
            TypeRules.Category(Enum.Parse<ElementType>(move.Type[5..], true)) == DamageCategory.Physical)
            accuracy = accuracy * 80 / 100;
        bool hit = Draw($"{purpose}.accuracy", 100) % 100 + 1 <= accuracy;
        if (!hit && reportMiss) SourceGenericMiss(actor, target, move);
        return hit;
    }
    internal bool CeruleanCritical(WildBattleCombatant actor, WildBattleCombatant target, string effect, string purpose, int denominator) =>
        WildBattleEffective.Ability(target) is not ("ABILITY_SHELL_ARMOR" or "ABILITY_BATTLE_ARMOR") && Draw($"{purpose}.critical", denominator) % denominator == 0;
    internal void ReportPewterEffectiveness(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, MoveId move)
    {
        if (catalog.Moves[move].Type == "TYPE_GROUND" && WildBattleEffective.Ability(Get(target)) == "ABILITY_LEVITATE")
        {
            Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, target, move, "ABILITY_LEVITATE"));
            Add(n => new SourceEffectivenessEvent(n, Turn, actor, target, move, WildBattleEffectiveness.NoEffect)); return;
        }
        ElementType Type(string type) => Enum.Parse<ElementType>(type[5..], true);
        var types = WildBattleEffective.Types(catalog, Get(target)); WildBattleEffectiveness? result = null;
        foreach (var row in TypeRules.OrderedEffects(Type(catalog.Moves[move].Type), Type(types.Type1), Type(types.Type2)))
        {
            if (row.MultiplierTenths == 0) result = WildBattleEffectiveness.NoEffect;
            else if (result != WildBattleEffectiveness.NoEffect) result = row.MultiplierTenths switch {
                5 => result == WildBattleEffectiveness.SuperEffective ? null : WildBattleEffectiveness.NotVeryEffective,
                20 => result == WildBattleEffectiveness.NotVeryEffective ? null : WildBattleEffectiveness.SuperEffective, _ => result };
        }
        if (result is { } effect) Add(n => new SourceEffectivenessEvent(n, Turn, actor, target, move, effect));
    }
    internal void InflictStatus(PokemonCatalog catalog, BattlerSlot target, uint status, bool ability, string? abilityReason = null, bool certain = false)
    {
        if (!ability && SecondaryStatusPrevention(target, status, certain)) return;
        var body = Get(target); string effective = WildBattleEffective.Ability(body);
        if (body.Pokemon.CurrentHp == 0 || body.Status != 0) return;
        if (status == WildBattleStatus.Poison && effective == "ABILITY_IMMUNITY") return;
        if (!ability && (SourceCalledSecondaryBlocked(target) || effective == "ABILITY_SHIELD_DUST" || Safeguard(target))) return;
        var types = WildBattleEffective.Types(catalog, body); bool Has(string type) => types.Type1 == type || types.Type2 == type;
        if (status == WildBattleStatus.Poison && (Has("TYPE_POISON") || Has("TYPE_STEEL")) || status == WildBattleStatus.Burn && Has("TYPE_FIRE")) return;
        if (status == WildBattleStatus.Paralysis && effective == "ABILITY_LIMBER" || status == WildBattleStatus.Burn && effective == "ABILITY_WATER_VEIL") return;
        ChangeStatus(target, status, ability ? abilityReason ?? "static" : "move");
        if (abilityReason != "synchronize") SynchronizeStatus(catalog, target, status);
    }
}
