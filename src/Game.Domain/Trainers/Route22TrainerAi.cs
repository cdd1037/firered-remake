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
    internal static bool ValidMove(PokemonMoveDefinition m)
    {
        if (m.Id.Value is not ("pret:MOVE_TACKLE" or "pret:MOVE_SCRATCH" or "pret:MOVE_GROWL" or "pret:MOVE_TAIL_WHIP" or "pret:MOVE_SAND_ATTACK") ||
            !ForestBattleScope.ValidDefinition(m)) return false;
        string[] flags = m.Power > 1
            ? ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"]
            : ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"];
        return m.Flags.Length == flags.Length && m.Flags.ToHashSet().SetEquals(flags);
    }
}
