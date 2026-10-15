using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

// Definition checks retained for the compiled early trainer catalogs.
internal static class Route3TrainerAi
{
    internal static bool ValidSpecies(PokemonSpeciesDefinition species) => Route22TrainerAi.ValidSpecies(species) || species.SourceSpeciesId switch
    {
        23 => (species.Type1, species.Type2, species.Ability1, species.Ability2) == ("TYPE_POISON", "TYPE_POISON", "ABILITY_INTIMIDATE", "ABILITY_SHED_SKIN"),
        29 or 30 or 32 or 33 => (species.Type1, species.Type2, species.Ability1, species.Ability2) == ("TYPE_POISON", "TYPE_POISON", "ABILITY_POISON_POINT", "ABILITY_NONE"),
        39 => (species.Type1, species.Type2, species.Ability1, species.Ability2) == ("TYPE_NORMAL", "TYPE_NORMAL", "ABILITY_CUTE_CHARM", "ABILITY_NONE"),
        129 => (species.Type1, species.Type2, species.Ability1, species.Ability2) == ("TYPE_WATER", "TYPE_WATER", "ABILITY_SWIFT_SWIM", "ABILITY_NONE"),
        130 => (species.Type1, species.Type2, species.Ability1, species.Ability2) == ("TYPE_WATER", "TYPE_FLYING", "ABILITY_INTIMIDATE", "ABILITY_NONE"),
        _ => false
    };
}
