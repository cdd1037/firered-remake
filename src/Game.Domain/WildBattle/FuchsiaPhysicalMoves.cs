using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

internal static class FuchsiaPhysicalMoveDefinition
{
    internal static bool Supports(MoveId move) => move.Value is "pret:MOVE_SWORDS_DANCE" or "pret:MOVE_FURY_CUTTER";

    internal static bool ValidDefinition(PokemonMoveDefinition move)
    {
        if (!Supports(move.Id) || move.Flags.IsDefault) return false;
        var expected = move.Id.Value == "pret:MOVE_SWORDS_DANCE"
            ? (14, 30, 0, 0, "TYPE_NORMAL", "EFFECT_ATTACK_UP_2", "MOVE_TARGET_USER", 0, 0)
            : (210, 20, 10, 95, "TYPE_BUG", "EFFECT_FURY_CUTTER", "MOVE_TARGET_SELECTED", 0, 0);
        string[] flags = move.Id.Value == "pret:MOVE_SWORDS_DANCE"
            ? ["FLAG_SNATCH_AFFECTED"]
            : ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"];
        return (move.SourceMoveId, move.Pp, move.Power, move.Accuracy, move.Type, move.Effect,
            move.Target, move.SecondaryEffectChance, move.Priority) == expected &&
            move.Flags.Order(StringComparer.Ordinal).SequenceEqual(flags.Order(StringComparer.Ordinal));
    }
}
