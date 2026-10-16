using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>Gen III status1. Sleep counters 1–5 are opt-in; freeze is Route11 opt-in; toxic remains unsupported.</summary>
public static class WildBattleStatus
{
    public const uint None = 0, Poison = 0x08, Burn = 0x10, Freeze = 0x20, Paralysis = 0x40;
    public static bool IsSleeping(uint status) => status is >= 1 and <= 5;
    public static bool IsSupported(uint status, bool earlyStarterRules = false, bool route11Rules = false) =>
        status is None or Poison or Burn or Paralysis || earlyStarterRules && IsSleeping(status) || route11Rules && status == Freeze;
}

/// <summary>Closed move/ability surface for the forest profile. Imported metadata alone never enables an effect.</summary>
internal static class ForestBattleScope
{
    private sealed record Move(int Id, int Pp, int Power, int Accuracy, string Type, string Effect,
        string Target = "MOVE_TARGET_SELECTED", int Chance = 0, int Priority = 0, bool Contact = false);

    private static readonly IReadOnlyDictionary<string, Move> Moves = new Dictionary<string, Move>(StringComparer.Ordinal)
    {
        ["pret:MOVE_TACKLE"] = new(33, 35, 35, 95, "TYPE_NORMAL", "EFFECT_HIT", Contact: true),
        ["pret:MOVE_SCRATCH"] = new(10, 35, 40, 100, "TYPE_NORMAL", "EFFECT_HIT", Contact: true),
        ["pret:MOVE_GROWL"] = new(45, 40, 0, 100, "TYPE_NORMAL", "EFFECT_ATTACK_DOWN", "MOVE_TARGET_BOTH"),
        ["pret:MOVE_TAIL_WHIP"] = new(39, 30, 0, 100, "TYPE_NORMAL", "EFFECT_DEFENSE_DOWN", "MOVE_TARGET_BOTH"),
        ["pret:MOVE_SAND_ATTACK"] = new(28, 15, 0, 100, "TYPE_GROUND", "EFFECT_ACCURACY_DOWN"),
        ["pret:MOVE_STRING_SHOT"] = new(81, 40, 0, 95, "TYPE_BUG", "EFFECT_SPEED_DOWN", "MOVE_TARGET_BOTH"),
        ["pret:MOVE_HARDEN"] = new(106, 30, 0, 0, "TYPE_NORMAL", "EFFECT_DEFENSE_UP", "MOVE_TARGET_USER"),
        ["pret:MOVE_POISON_STING"] = new(40, 35, 15, 100, "TYPE_POISON", "EFFECT_POISON_HIT", Chance: 30),
        ["pret:MOVE_THUNDER_SHOCK"] = new(84, 30, 40, 100, "TYPE_ELECTRIC", "EFFECT_PARALYZE_HIT", Chance: 10),
        ["pret:MOVE_EMBER"] = new(52, 25, 40, 100, "TYPE_FIRE", "EFFECT_BURN_HIT", Chance: 10),
        ["pret:MOVE_BUBBLE"] = new(145, 30, 20, 100, "TYPE_WATER", "EFFECT_SPEED_DOWN_HIT", "MOVE_TARGET_BOTH", 10),
        ["pret:MOVE_VINE_WHIP"] = new(22, 10, 35, 100, "TYPE_GRASS", "EFFECT_HIT", Contact: true),
        ["pret:MOVE_GUST"] = new(16, 35, 40, 100, "TYPE_FLYING", "EFFECT_GUST"),
        ["pret:MOVE_QUICK_ATTACK"] = new(98, 30, 40, 100, "TYPE_NORMAL", "EFFECT_QUICK_ATTACK", Priority: 1, Contact: true)
    };

    internal static bool Supports(MoveId move) => Moves.ContainsKey(move.Value);
    internal static bool SupportsOpponent(int species) => species is 10 or 11 or 13 or 14 or 16 or 19 or 25;
    internal static bool SupportsAbility(string ability) => ability is "ABILITY_OVERGROW" or "ABILITY_BLAZE"
        or "ABILITY_TORRENT" or "ABILITY_KEEN_EYE" or "ABILITY_RUN_AWAY" or "ABILITY_GUTS"
        or "ABILITY_SHIELD_DUST" or "ABILITY_SHED_SKIN" or "ABILITY_STATIC"
        or "ABILITY_COMPOUND_EYES" or "ABILITY_SWARM";
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (!Moves.TryGetValue(m.Id.Value, out var e)) return false;
        return (m.SourceMoveId, m.Pp, m.Power, m.Accuracy, m.Type, m.Effect, m.Target, m.SecondaryEffectChance, m.Priority)
            == (e.Id, e.Pp, e.Power, e.Accuracy, e.Type, e.Effect, e.Target, e.Chance, e.Priority)
            && !m.Flags.IsDefault && m.Flags.Contains("FLAG_MAKES_CONTACT") == e.Contact;
    }
}
