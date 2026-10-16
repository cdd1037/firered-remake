using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

// Definition checks retained for the compiled early trainer catalogs.
internal static class MtMoonTrainerAi
{
    internal static bool ValidSpecies(PokemonSpeciesDefinition s) => Route3TrainerAi.ValidSpecies(s) || s.SourceSpeciesId switch
    {
        24 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_POISON", "TYPE_POISON", "ABILITY_INTIMIDATE", "ABILITY_SHED_SKIN"),
        35 or 36 or 40 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_NORMAL", "TYPE_NORMAL", "ABILITY_CUTE_CHARM", "ABILITY_NONE"),
        31 or 34 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_POISON", "TYPE_GROUND", "ABILITY_POISON_POINT", "ABILITY_NONE"),
        41 or 42 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_POISON", "TYPE_FLYING", "ABILITY_INNER_FOCUS", "ABILITY_NONE"),
        43 or 69 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_GRASS", "TYPE_POISON", "ABILITY_CHLOROPHYLL", "ABILITY_NONE"),
        46 or 47 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_BUG", "TYPE_GRASS", "ABILITY_EFFECT_SPORE", "ABILITY_NONE"),
        81 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_ELECTRIC", "TYPE_STEEL", "ABILITY_MAGNET_PULL", "ABILITY_STURDY"),
        88 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_POISON", "TYPE_POISON", "ABILITY_STENCH", "ABILITY_STICKY_HOLD"),
        100 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_ELECTRIC", "TYPE_ELECTRIC", "ABILITY_SOUNDPROOF", "ABILITY_STATIC"),
        109 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_POISON", "TYPE_POISON", "ABILITY_LEVITATE", "ABILITY_NONE"),
        _ => false
    };
}
