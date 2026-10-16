using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>Opt-in starter closure layered over the frozen first-badge battle profile.</summary>
internal static class EarlyStarterBattleScope
{
    internal static bool Supports(MoveId move) => PewterBattleScope.Supports(move) ||
        move.Value is "pret:MOVE_WATER_GUN" or "pret:MOVE_WITHDRAW" or "pret:MOVE_LEECH_SEED" or "pret:MOVE_POISON_POWDER" or "pret:MOVE_SLEEP_POWDER";

    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (PewterBattleScope.Supports(m.Id)) return PewterBattleScope.ValidDefinition(m);
        var expected = m.Id.Value switch
        {
            "pret:MOVE_WATER_GUN" => (55, 25, 40, 100, "TYPE_WATER", "EFFECT_HIT", "MOVE_TARGET_SELECTED"),
            "pret:MOVE_WITHDRAW" => (110, 40, 0, 0, "TYPE_WATER", "EFFECT_DEFENSE_UP", "MOVE_TARGET_USER"),
            "pret:MOVE_LEECH_SEED" => (73, 10, 0, 90, "TYPE_GRASS", "EFFECT_LEECH_SEED", "MOVE_TARGET_SELECTED"),
            "pret:MOVE_POISON_POWDER" => (77, 35, 0, 75, "TYPE_POISON", "EFFECT_POISON", "MOVE_TARGET_SELECTED"),
            "pret:MOVE_SLEEP_POWDER" => (79, 15, 0, 75, "TYPE_GRASS", "EFFECT_SLEEP", "MOVE_TARGET_SELECTED"),
            _ => default
        };
        string[] flags = m.Id.Value == "pret:MOVE_WATER_GUN"
            ? ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"]
            : m.Id.Value == "pret:MOVE_WITHDRAW" ? ["FLAG_SNATCH_AFFECTED"]
            : ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"];
        return expected.Item1 != 0 && m.Priority == 0 && m.SecondaryEffectChance == 0 &&
            (m.SourceMoveId, m.Pp, m.Power, m.Accuracy, m.Type, m.Effect, m.Target) == expected &&
            !m.Flags.IsDefault && m.Flags.Length == flags.Length && flags.All(m.Flags.Contains);
    }
}
