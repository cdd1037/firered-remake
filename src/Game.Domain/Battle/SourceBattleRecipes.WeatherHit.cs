using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal static bool HasWeatherHitRecipe(string effect) => effect is
        "EFFECT_GUST" or "EFFECT_TWISTER" or "EFFECT_THUNDER" or "EFFECT_SKY_UPPERCUT";

    // All four source prefixes enter BattleScript_EffectHit. SourceChargeRules
    // supplies the Air exception and Gust/Twister multiplier for the current target.
    // The action owner repeats Twister's script for the other opponent, retaining
    // one PP debit and independently resetting damage, accuracy and effect commands.
    internal void ExecuteWeatherHit(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move, string purpose)
    {
        if (!HasWeatherHitRecipe(move.Effect) || !SourceMoveDefinitionRules.IsValid(move))
            throw new SourceRecipeUnsupported("source_weather_hit_definition");
        SourceGenericHit(catalog, actor, target, slot, move, purpose);
    }
}
