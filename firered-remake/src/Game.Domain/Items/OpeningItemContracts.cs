using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

public sealed record OpeningItemSourceRef(string Path, string Symbol, int StartLine, int EndLine);
public sealed record InventoryStack(ItemId Item, int Quantity);
public sealed record InventoryLimits(int BagItemSlots, int PcItemSlots, int MaximumStack);
public sealed record OpeningPotionDefinition(QuestItemDefinition Item, int HealHp, ImmutableArray<int> EffectBytes)
{
    [JsonIgnore] public ItemId Id => Item.Id;
    [JsonIgnore] public int SourceItemId => Item.SourceItemId;
    [JsonIgnore] public string Name => Item.Name;
    [JsonIgnore] public int Price => Item.Price;
    [JsonIgnore] public string Pocket => Item.Pocket;
    [JsonIgnore] public string Type => Item.Type;
    [JsonIgnore] public string FieldUseFunction => Item.FieldUseFunction;
    [JsonIgnore] public int BattleUsage => Item.BattleUsage;
    [JsonIgnore] public string BattleUseFunction => Item.BattleUseFunction;
}
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
