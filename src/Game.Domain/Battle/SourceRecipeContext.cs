using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Slot-typed recipes and frame storage form one canonical execution context.</summary>
internal sealed partial class FrameRecipeContext : SourceStatRecipeContext
{
    internal bool SourcePlusMinusBoost(WildBattleCombatant actor)
    {
        string partnerAbility = WildBattleEffective.Ability(actor) switch {
            "ABILITY_PLUS" => "ABILITY_MINUS", "ABILITY_MINUS" => "ABILITY_PLUS", _ => "" };
        // ABILITY_ON_FIELD2 uses FIELD_SPORT's default all-body scan: no side/HP/absence test.
        return partnerAbility.Length != 0 && ActiveSlots.Any(s => WildBattleEffective.Ability(Get(s)) == partnerAbility);
    }
}

internal sealed class SourceRecipeUnsupported(string code) : Exception(code);
