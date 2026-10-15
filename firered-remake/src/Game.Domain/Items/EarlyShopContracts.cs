using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Availability is data. Only Potion and the three represented status cures execute here.</summary>
public enum EarlyShopUseSupport { SeparateCapture, Potion, StatusCure, DeferredSleep, DeferredEscape, DeferredRepel, FieldRevive, FieldEther, DeferredRareCandy, SeparateEvolution, NoFieldUse, Fossil, FieldRestoreAllPp }
/// <summary>One business item definition with the field-use capability admitted by this inventory view.</summary>
public sealed record EarlyShopItemDefinition(QuestItemDefinition Item, string Description,
    int HealHp, uint CureStatus, ImmutableArray<int> EffectBytes, EarlyShopUseSupport UseSupport)
{
    [JsonIgnore] public ItemId Id => Item.Id;
    [JsonIgnore] public string SourceItemSymbol => Item.SourceItemSymbol;
    [JsonIgnore] public int SourceItemId => Item.SourceItemId;
    [JsonIgnore] public string Name => Item.Name;
    [JsonIgnore] public int Price => Item.Price;
    [JsonIgnore] public string Pocket => Item.Pocket;
    [JsonIgnore] public int Importance => Item.Importance;
    [JsonIgnore] public string Type => Item.Type;
    [JsonIgnore] public string FieldUseFunction => Item.FieldUseFunction;
    [JsonIgnore] public int BattleUsage => Item.BattleUsage;
    [JsonIgnore] public string BattleUseFunction => Item.BattleUseFunction;
    [JsonIgnore] public int HoldEffectParam => Item.HoldEffectParam;
}
public sealed record EarlyShopStock(string Id, ImmutableArray<ItemId> Items);
public sealed record EarlyShopCatalog(InventoryLimits Limits, QuestInventoryLimits QuestLimits, int MaximumTransactionQuantity, int MaximumMoney,
    ImmutableArray<EarlyShopItemDefinition> Items, ImmutableArray<EarlyShopStock> Shops)
{
    [JsonIgnore]
    public InventoryCatalog? Inventory { get; init; }
}
/// <summary>MaximumQuantity matches the source quantity picker (funds/99 for buying).
/// Capacity is checked at commit; MaximumExecutableQuantity additionally exposes that limit.</summary>
public sealed record EarlyShopQuote(bool Succeeded, int MaximumQuantity, int UnitPrice, string? Error)
{
    public int MaximumExecutableQuantity { get; init; }
}
/// <summary>Money and bag commit together. TotalPrice is the source sale price; MoneyDelta can be smaller at the cap.</summary>
public sealed record EarlyShopTransactionResult(InventoryState Inventory, int Money, bool Succeeded,
    int Quantity, int TotalPrice, int MoneyDelta, string? Error);
public sealed record EarlyMedicineUseResult(InventoryState Inventory, PartyPokemon Pokemon, bool Succeeded,
    int HpRestored, uint StatusCured, int ItemsConsumed, bool ConsumesBattleTurn, string? Error);
