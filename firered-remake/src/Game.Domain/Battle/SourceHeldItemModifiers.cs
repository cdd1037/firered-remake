using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal static class SourceHeldItemModifiers
{
    internal static int Accuracy(PokemonCatalog? catalog, WildBattleCombatant target, int accuracy) =>
        SourceHeldItemCatalog.Find(catalog, target.HeldItem) is { HoldEffect: "HOLD_EFFECT_EVASION_UP" } item
            ? accuracy * (100 - item.HoldEffectParam) / 100 : accuracy;
    internal static int CriticalBonus(PokemonCatalog? catalog, WildBattleCombatant actor) =>
        SourceHeldItemCatalog.Find(catalog, actor.HeldItem)?.HoldEffect switch {
            "HOLD_EFFECT_SCOPE_LENS" => 1,
            "HOLD_EFFECT_LUCKY_PUNCH" when actor.Pokemon.Species.Value == "pret:SPECIES_CHANSEY" => 2,
            "HOLD_EFFECT_STICK" when actor.Pokemon.Species.Value == "pret:SPECIES_FARFETCHD" => 2, _ => 0 };
    // CalculateBaseDamage stores each modified battle stat back into u16, before stages.
    internal static int TypeAttack(PokemonCatalog? catalog, WildBattleCombatant actor, ElementType type, int attack)
    {
        var item = SourceHeldItemCatalog.Find(catalog, actor.HeldItem);
        if (item is null || !item.HoldEffect.StartsWith("HOLD_EFFECT_", StringComparison.Ordinal) ||
            !item.HoldEffect.EndsWith("_POWER", StringComparison.Ordinal)) return attack;
        string symbol = item.HoldEffect[12..^6];
        return Enum.TryParse<ElementType>(symbol, true, out var boosted) && boosted == type
            ? unchecked((ushort)(attack * (100 + item.HoldEffectParam) / 100)) : attack;
    }
    internal static int SpecialAttack(PokemonCatalog? catalog, WildBattleCombatant actor, int attack) =>
        SourceHeldItemCatalog.Find(catalog, actor.HeldItem)?.HoldEffect switch {
            "HOLD_EFFECT_SOUL_DEW" when actor.Pokemon.Species.Value is "pret:SPECIES_LATIAS" or "pret:SPECIES_LATIOS" => unchecked((ushort)(attack * 150 / 100)),
            "HOLD_EFFECT_DEEP_SEA_TOOTH" when actor.Pokemon.Species.Value == "pret:SPECIES_CLAMPERL" => unchecked((ushort)(attack * 2)),
            // Gen III Light Ball affects Special Attack only. Transformed species is the battle species.
            "HOLD_EFFECT_LIGHT_BALL" when actor.Pokemon.Species.Value == "pret:SPECIES_PIKACHU" => unchecked((ushort)(attack * 2)),
            _ => attack };
    internal static int SpecialDefense(PokemonCatalog? catalog, WildBattleCombatant target, int defense) =>
        SourceHeldItemCatalog.Find(catalog, target.HeldItem)?.HoldEffect switch {
            "HOLD_EFFECT_SOUL_DEW" when target.Pokemon.Species.Value is "pret:SPECIES_LATIAS" or "pret:SPECIES_LATIOS" => unchecked((ushort)(defense * 150 / 100)),
            "HOLD_EFFECT_DEEP_SEA_SCALE" when target.Pokemon.Species.Value == "pret:SPECIES_CLAMPERL" => unchecked((ushort)(defense * 2)),
            _ => defense };
}
