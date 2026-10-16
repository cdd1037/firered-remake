using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>Explicit closure for the ordinary first-badge party through level 20. Frozen profiles remain independent.</summary>
internal static class EarlyPartyBattleScope
{
    internal static bool Supports(MoveId move) => EarlyStarterBattleScope.Supports(move) || move.Value is
        "pret:MOVE_CONFUSION" or "pret:MOVE_STUN_SPORE" or "pret:MOVE_SUPERSONIC" or "pret:MOVE_THUNDER_WAVE" or
        "pret:MOVE_FURY_ATTACK" or "pret:MOVE_FOCUS_ENERGY" or "pret:MOVE_TWINEEDLE" or "pret:MOVE_RAZOR_LEAF" or
        "pret:MOVE_HYPER_FANG" or "pret:MOVE_BITE" or "pret:MOVE_DOUBLE_TEAM" or "pret:MOVE_SMOKESCREEN" or
        "pret:MOVE_SCARY_FACE" or "pret:MOVE_SLAM" or "pret:MOVE_WHIRLWIND";

    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (EarlyStarterBattleScope.Supports(m.Id)) return EarlyStarterBattleScope.ValidDefinition(m);
        var expected = m.Id.Value switch
        {
            "pret:MOVE_CONFUSION" => (93, 25, 50, 100, "TYPE_PSYCHIC", "EFFECT_CONFUSE_HIT", "MOVE_TARGET_SELECTED", 10, 0),
            "pret:MOVE_STUN_SPORE" => (78, 30, 0, 75, "TYPE_GRASS", "EFFECT_PARALYZE", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_SUPERSONIC" => (48, 20, 0, 55, "TYPE_NORMAL", "EFFECT_CONFUSE", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_THUNDER_WAVE" => (86, 20, 0, 100, "TYPE_ELECTRIC", "EFFECT_PARALYZE", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_FURY_ATTACK" => (31, 20, 15, 85, "TYPE_NORMAL", "EFFECT_MULTI_HIT", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_FOCUS_ENERGY" => (116, 30, 0, 0, "TYPE_NORMAL", "EFFECT_FOCUS_ENERGY", "MOVE_TARGET_USER", 0, 0),
            "pret:MOVE_TWINEEDLE" => (41, 20, 25, 100, "TYPE_BUG", "EFFECT_TWINEEDLE", "MOVE_TARGET_SELECTED", 20, 0),
            "pret:MOVE_RAZOR_LEAF" => (75, 25, 55, 95, "TYPE_GRASS", "EFFECT_HIGH_CRITICAL", "MOVE_TARGET_BOTH", 0, 0),
            "pret:MOVE_HYPER_FANG" => (158, 15, 80, 90, "TYPE_NORMAL", "EFFECT_FLINCH_HIT", "MOVE_TARGET_SELECTED", 10, 0),
            "pret:MOVE_BITE" => (44, 25, 60, 100, "TYPE_DARK", "EFFECT_FLINCH_HIT", "MOVE_TARGET_SELECTED", 30, 0),
            "pret:MOVE_DOUBLE_TEAM" => (104, 15, 0, 0, "TYPE_NORMAL", "EFFECT_EVASION_UP", "MOVE_TARGET_USER", 0, 0),
            "pret:MOVE_SMOKESCREEN" => (108, 20, 0, 100, "TYPE_NORMAL", "EFFECT_ACCURACY_DOWN", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_SCARY_FACE" => (184, 10, 0, 90, "TYPE_NORMAL", "EFFECT_SPEED_DOWN_2", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_SLAM" => (21, 20, 80, 75, "TYPE_NORMAL", "EFFECT_HIT", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_WHIRLWIND" => (18, 20, 0, 100, "TYPE_NORMAL", "EFFECT_ROAR", "MOVE_TARGET_SELECTED", 0, -6),
            _ => default
        };
        string[] flags = m.Id.Value switch
        {
            "pret:MOVE_FOCUS_ENERGY" or "pret:MOVE_DOUBLE_TEAM" => ["FLAG_SNATCH_AFFECTED"],
            "pret:MOVE_STUN_SPORE" or "pret:MOVE_SUPERSONIC" or "pret:MOVE_THUNDER_WAVE" or "pret:MOVE_SMOKESCREEN" or "pret:MOVE_SCARY_FACE" =>
                ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_FURY_ATTACK" or "pret:MOVE_SLAM" =>
                ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_RAZOR_LEAF" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_HYPER_FANG" or "pret:MOVE_BITE" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            _ => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"]
        };
        return expected.Item1 != 0 && (m.SourceMoveId, m.Pp, m.Power, m.Accuracy, m.Type, m.Effect,
            m.Target, m.SecondaryEffectChance, m.Priority) == expected && !m.Flags.IsDefault &&
            m.Flags.Length == flags.Length && flags.All(m.Flags.Contains);
    }
}
