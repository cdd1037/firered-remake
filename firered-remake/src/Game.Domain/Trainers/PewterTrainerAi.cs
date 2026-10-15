using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

// Definition checks retained for the compiled early trainer catalogs.
internal static class PewterTrainerAi
{
    internal static bool ValidSpecies(PokemonSpeciesDefinition s)
    {
        if (ForestTrainerAi.ValidSpecies(s)) return true;
        var expected = s.SourceSpeciesId switch
        {
            74 or 75 or 76 or 95 => ("TYPE_ROCK", "TYPE_GROUND", "ABILITY_ROCK_HEAD", "ABILITY_STURDY"),
            27 or 28 => ("TYPE_GROUND", "TYPE_GROUND", "ABILITY_SAND_VEIL", "ABILITY_NONE"),
            208 => ("TYPE_STEEL", "TYPE_GROUND", "ABILITY_ROCK_HEAD", "ABILITY_STURDY"),
            _ => default
        };
        return expected != default && (s.Type1, s.Type2, s.Ability1, s.Ability2) == expected;
    }

    internal static bool ValidMove(PokemonMoveDefinition m)
    {
        var e = m.Id.Value switch
        {
            "pret:MOVE_TACKLE" => (33, 35, 35, 95, "TYPE_NORMAL", "EFFECT_HIT", "MOVE_TARGET_SELECTED", 0),
            "pret:MOVE_SCRATCH" => (10, 35, 40, 100, "TYPE_NORMAL", "EFFECT_HIT", "MOVE_TARGET_SELECTED", 0),
            "pret:MOVE_DEFENSE_CURL" => (111, 40, 0, 0, "TYPE_NORMAL", "EFFECT_DEFENSE_CURL", "MOVE_TARGET_USER", 0),
            "pret:MOVE_SAND_ATTACK" => (28, 15, 0, 100, "TYPE_GROUND", "EFFECT_ACCURACY_DOWN", "MOVE_TARGET_SELECTED", 0),
            "pret:MOVE_BIND" => (20, 20, 15, 75, "TYPE_NORMAL", "EFFECT_TRAP", "MOVE_TARGET_SELECTED", 100),
            "pret:MOVE_ROCK_TOMB" => (317, 10, 50, 80, "TYPE_ROCK", "EFFECT_SPEED_DOWN_HIT", "MOVE_TARGET_SELECTED", 100),
            _ => default
        };
        string[] flags = m.Id.Value switch
        {
            "pret:MOVE_DEFENSE_CURL" => ["FLAG_SNATCH_AFFECTED"],
            "pret:MOVE_SAND_ATTACK" => ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_ROCK_TOMB" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            _ => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"]
        };
        return e != default && (m.SourceMoveId, m.Pp, m.Power, m.Accuracy, m.Type, m.Effect, m.Target, m.SecondaryEffectChance) == e &&
            m.Priority == 0 && !m.Flags.IsDefault && m.Flags.Length == flags.Length && m.Flags.ToHashSet().SetEquals(flags);
    }
}
