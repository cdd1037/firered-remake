using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>Structural requirements for the source move recipes. Effect support is decided by each recipe.</summary>
public static class SourceMoveDefinitionRules
{
    public const int OrdinaryMoveCount = 354;

    public static bool IsValid(PokemonMoveDefinition? move) =>
        move is not null && move.SourceMoveId is >= 1 and <= OrdinaryMoveCount &&
        move.Pp is >= 1 and <= 40 && move.Power is >= 0 and <= 255 &&
        move.Accuracy is >= 0 and <= 100 && move.SecondaryEffectChance is >= 0 and <= 100 &&
        move.Priority is >= -7 and <= 7 && !move.BattleEffectImplemented &&
        Symbol(move.Id.Value, "pret:MOVE_") && Symbol(move.Type, "TYPE_") &&
        Symbol(move.Effect, "EFFECT_") && Symbol(move.Target, "MOVE_TARGET_") &&
        !move.Flags.IsDefault && move.Flags.Length <= 6 && move.Flags.All(flag => Symbol(flag, "FLAG_"));

    private static bool Symbol(string? value, string prefix) =>
        value is not null && value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length &&
        value[prefix.Length..].All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_');
}
