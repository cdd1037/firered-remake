using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

// Definition checks retained for the compiled early trainer catalogs.
internal static class ForestTrainerAi
{
    internal static bool ValidSpecies(PokemonSpeciesDefinition species)
    {
        var expected = species.SourceSpeciesId switch
        {
            1 or 2 or 3 => ("TYPE_GRASS", "TYPE_POISON", "ABILITY_OVERGROW", "ABILITY_NONE"),
            4 or 5 => ("TYPE_FIRE", "TYPE_FIRE", "ABILITY_BLAZE", "ABILITY_NONE"),
            6 => ("TYPE_FIRE", "TYPE_FLYING", "ABILITY_BLAZE", "ABILITY_NONE"),
            7 or 8 or 9 => ("TYPE_WATER", "TYPE_WATER", "ABILITY_TORRENT", "ABILITY_NONE"),
            10 => ("TYPE_BUG", "TYPE_BUG", "ABILITY_SHIELD_DUST", "ABILITY_NONE"),
            11 => ("TYPE_BUG", "TYPE_BUG", "ABILITY_SHED_SKIN", "ABILITY_NONE"),
            12 => ("TYPE_BUG", "TYPE_FLYING", "ABILITY_COMPOUND_EYES", "ABILITY_NONE"),
            13 => ("TYPE_BUG", "TYPE_POISON", "ABILITY_SHIELD_DUST", "ABILITY_NONE"),
            14 => ("TYPE_BUG", "TYPE_POISON", "ABILITY_SHED_SKIN", "ABILITY_NONE"),
            15 => ("TYPE_BUG", "TYPE_POISON", "ABILITY_SWARM", "ABILITY_NONE"),
            16 or 17 or 18 => ("TYPE_NORMAL", "TYPE_FLYING", "ABILITY_KEEN_EYE", "ABILITY_NONE"),
            19 or 20 => ("TYPE_NORMAL", "TYPE_NORMAL", "ABILITY_RUN_AWAY", "ABILITY_GUTS"),
            25 or 26 => ("TYPE_ELECTRIC", "TYPE_ELECTRIC", "ABILITY_STATIC", "ABILITY_NONE"),
            _ => default
        };
        return expected != default && (species.Type1, species.Type2, species.Ability1, species.Ability2) == expected;
    }
}
