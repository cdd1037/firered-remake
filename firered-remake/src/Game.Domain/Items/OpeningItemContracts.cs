using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

public sealed record OpeningItemSourceRef(string Path, string Symbol, int StartLine, int EndLine, string Sha256);
public sealed record InventoryStack(ItemId Item, int Quantity);
public sealed record InventoryLimits(int BagItemSlots, int PcItemSlots, int MaximumStack);
public sealed record OpeningPotionDefinition(ItemId Id, int SourceItemId, string Name, int Price,
    string Pocket, string Type, string FieldUseFunction, int BattleUsage, string BattleUseFunction,
    int HealHp, ImmutableArray<int> EffectBytes, OpeningItemSourceRef Source, OpeningItemSourceRef EffectSource);
public sealed record OpeningItemsCatalog(string SourceCommit, string Profile, bool CompleteItemCatalog,
    InventoryLimits Limits, ImmutableArray<InventoryStack> InitialPcItems, OpeningPotionDefinition Potion);

/// <summary>Ordinary Items and PC plus optional, independently validated quest pockets.
/// Null quest pockets preserve legacy saves byte-for-byte; a depleted stack is removed.</summary>
public sealed record InventoryState(ImmutableArray<InventoryStack> BagItems,
    ImmutableArray<InventoryStack> PcItems, InventoryLimits Limits)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<InventoryStack>? KeyItems { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<InventoryStack>? Balls { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<InventoryStack>? Machines { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<InventoryStack>? Berries { get; init; }
}
public sealed record InventoryTransactionResult(InventoryState State, bool Succeeded, string? Error);
public enum PotionUseContext { Field, Battle }
public sealed record PotionUseResult(InventoryState Inventory, PokemonInstance Pokemon, bool Succeeded,
    int HpRestored, int ItemsConsumed, bool ConsumesBattleTurn, string? Error);
public sealed record PartyPotionUseResult(InventoryState Inventory, PartyPokemon Pokemon, bool Succeeded,
    int HpRestored, int ItemsConsumed, bool ConsumesBattleTurn, string? Error);
