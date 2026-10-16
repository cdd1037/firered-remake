using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

// Definition checks retained for the compiled early trainer catalogs.
internal static class Route22TrainerAi
{
    internal static bool ValidSpecies(PokemonSpeciesDefinition s) => PewterTrainerAi.ValidSpecies(s) || s.SourceSpeciesId switch
    {
        21 or 22 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_NORMAL", "TYPE_FLYING", "ABILITY_KEEN_EYE", "ABILITY_NONE"),
        56 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_FIGHTING", "TYPE_FIGHTING", "ABILITY_VITAL_SPIRIT", "ABILITY_NONE"),
        _ => false
    };
    internal static bool ValidMove(PokemonMoveDefinition move) =>
        Game.Domain.Battle.SourceBattleKernel.SupportsRecipe(move);
}
