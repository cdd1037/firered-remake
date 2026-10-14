using System.Collections.Immutable;

namespace Game.Domain.Pokemon;

/// <summary>
/// A fixed source box. Null is an empty slot; entries are canonical healthy party projections of
/// BoxPokemon, not historical party snapshots. See PokemonStorageRules for the conversion contract.
/// </summary>
public sealed record PokemonStorageBox(ImmutableArray<PartyPokemon?> Slots)
{
    // Null retains the source reset value for this box index; old saves remain byte-identical.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? WallpaperId { get; init; }
}

public sealed record PokemonStorageState(int CurrentBox, ImmutableArray<PokemonStorageBox> Boxes)
{
    public const int BoxCount = 14;
    public const int SlotsPerBox = 30;
    public const int Capacity = BoxCount * SlotsPerBox;

    public static PokemonStorageState Empty { get; } = new(0,
        Enumerable.Range(0, BoxCount).Select(_ =>
            new PokemonStorageBox(Enumerable.Repeat<PartyPokemon?>(null, SlotsPerBox).ToImmutableArray()))
            .ToImmutableArray());
}

/// <summary>Both coordinates are zero-based; display labels add one.</summary>
public sealed record PokemonStorageAddress(int BoxIndex, int SlotIndex);

/// <summary>Stable code and optional exact location; PokemonError preserves the shared validator code.</summary>
public sealed record PokemonStorageDiagnostic(string Code, int? PartyIndex = null,
    int? BoxIndex = null, int? SlotIndex = null, string? PokemonError = null);

public enum PokemonStorageOutcome
{
    Rejected = 0,
    ReceivedInParty,
    ReceivedInBox,
    Deposited,
    Withdrawn,
    BoxSelected
}

/// <summary>On rejection Party and Storage are the exact original inputs, with no partial mutation.</summary>
public sealed record PokemonStorageResult(PokemonStorageOutcome Outcome, ImmutableArray<PartyPokemon> Party,
    PokemonStorageState Storage, PokemonStorageAddress? Address, PokemonStorageDiagnostic? Diagnostic)
{
    public bool Succeeded => Outcome != PokemonStorageOutcome.Rejected;
}
