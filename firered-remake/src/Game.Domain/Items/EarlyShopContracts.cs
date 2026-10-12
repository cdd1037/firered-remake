using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Availability is data. Only Potion and the three represented status cures execute here.</summary>
public enum EarlyShopUseSupport { SeparateCapture, Potion, StatusCure, DeferredSleep, DeferredEscape, DeferredRepel, FieldRevive, FieldEther, DeferredRareCandy, SeparateEvolution, NoFieldUse, Fossil, FieldRestoreAllPp }
public sealed record EarlyShopItemDefinition(ItemId Id, string SourceItemSymbol, int SourceItemId,
    string Name, string Description, int Price, string Pocket, int Importance, string Type,
    string FieldUseFunction, int BattleUsage, string BattleUseFunction, int HoldEffectParam,
    int HealHp, uint CureStatus, ImmutableArray<int> EffectBytes, EarlyShopUseSupport UseSupport,
    OpeningItemSourceRef Source, OpeningItemSourceRef? EffectSource)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public QuestItemDefinition? SourceMetadata { get; init; }
}
public sealed record EarlyShopStock(string Id, ImmutableArray<ItemId> Items, OpeningItemSourceRef Source);
public sealed record EarlyShopCatalog(string SourceCommit, string Profile, bool CompleteItemCatalog,
    InventoryLimits Limits, QuestInventoryLimits QuestLimits, int MaximumTransactionQuantity, int MaximumMoney,
    ImmutableArray<EarlyShopItemDefinition> Items, ImmutableArray<EarlyShopStock> Shops);
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
