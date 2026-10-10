namespace Game.Domain.Ids;

public static class StableId
{
    public static string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
            value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':' or '/')))
            throw new ArgumentException("Stable IDs need 1–128 ASCII letters, digits, '-', '_', '.', ':', or '/'.", nameof(value));
        return value;
    }
    public static bool IsValid(string? value)
    {
        try { Validate(value!); return true; } catch (ArgumentException) { return false; }
    }
}

public readonly record struct MapId
{
    public string Value { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public MapId(string value) => Value = StableId.Validate(value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct SpeciesId
{
    public string Value { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public SpeciesId(string value) => Value = StableId.Validate(value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct MoveId
{
    public string Value { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public MoveId(string value) => Value = StableId.Validate(value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct ItemId
{
    public string Value { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public ItemId(string value) => Value = StableId.Validate(value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct ScriptId
{
    public string Value { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public ScriptId(string value) => Value = StableId.Validate(value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct TrainerId
{
    public string Value { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public TrainerId(string value) => Value = StableId.Validate(value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct TextId
{
    public string Value { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public TextId(string value) => Value = StableId.Validate(value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct FlagId
{
    public string Value { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public FlagId(string value) => Value = StableId.Validate(value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct CombatantId
{
    public string Value { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public CombatantId(string value) => Value = StableId.Validate(value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct BattleId
{
    public string Value { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public BattleId(string value) => Value = StableId.Validate(value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct CommandId
{
    public string Value { get; }
    [System.Text.Json.Serialization.JsonConstructor]
    public CommandId(string value) => Value = StableId.Validate(value);
    public override string ToString() => Value ?? string.Empty;
}

