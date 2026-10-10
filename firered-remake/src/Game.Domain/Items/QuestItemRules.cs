using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Bounded source item storage. These methods do not implement item use, shops or flags.</summary>
public static class QuestItemRules
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string Profile = "firered-oaks-parcel-items-v1";
    public const string TutorialProfile = "firered-old-man-key-items-v1";
    public const string TownMapProfile = "firered-town-map-key-items-v1";
    public const string TownMapSymbol = "ITEM_TOWN_MAP";
    public static ItemId TownMapId { get; } = new("pret:" + TownMapSymbol);
    public const string TeachyTvSymbol = "ITEM_TEACHY_TV";
    public static ItemId TeachyTvId { get; } = new("pret:" + TeachyTvSymbol);
    public const string ParcelSymbol = "ITEM_OAKS_PARCEL";
    public const string PokeBallSymbol = "ITEM_POKE_BALL";
    public static ItemId ParcelId { get; } = new("pret:" + ParcelSymbol);
    public static ItemId PokeBallId { get; } = new("pret:" + PokeBallSymbol);
    public static QuestInventoryLimits SourceLimits { get; } = new(30, 13, 999);

    public static void ValidateCatalog(QuestItemsCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile is not (Profile or TutorialProfile or TownMapProfile) || catalog.CompleteItemCatalog ||
            catalog.Limits != SourceLimits || catalog.Items.IsDefault || catalog.Items.Length != (catalog.Profile==Profile?2:catalog.Profile==TutorialProfile?3:4))
            throw new NotSupportedException("Only the pinned Parcel, tutorial and Town Map item storage profiles are supported.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in catalog.Items)
        {
            if (item is null || item.SourceItemSymbol is null || !seen.Add(item.SourceItemSymbol) ||
                item.Id != new ItemId("pret:" + item.SourceItemSymbol) || item.HoldEffect != "HOLD_EFFECT_NONE" ||
                item.HoldEffectParam != 0 || item.Source is null)
                throw new NotSupportedException("Invalid quest item definition.");
            bool valid = item.SourceItemSymbol switch
            {
                ParcelSymbol => item.Registrability == 0 && item.SourceItemId == 349 && item.Name == "OAK'S PARCEL" && item.Price == 0 &&
                    item.Importance == 2 && item.Pocket == "POCKET_KEY_ITEMS" && item.Type == "ITEM_TYPE_BAG_MENU" &&
                    item.FieldUseFunction == "FieldUseFunc_OakStopsYou" && item.BattleUsage == 0 &&
                    item.BattleUseFunction == "NULL" && item.SecondaryId == 0 &&
                    !item.CanToss && !item.CanSell && !item.CanGive,
                PokeBallSymbol => item.Registrability == 0 && item.SourceItemId == 4 && item.Name == "POKé BALL" && item.Price == 200 &&
                    item.Importance == 0 && item.Pocket == "POCKET_POKE_BALLS" && item.Type == "3" &&
                    item.FieldUseFunction == "NULL" && item.BattleUsage == 2 &&
                    item.BattleUseFunction == "BattleUseFunc_PokeBallEtc" && item.SecondaryId == 3 &&
                    item.CanToss && item.CanSell && item.CanGive,
                TeachyTvSymbol => (catalog.Profile is TutorialProfile or TownMapProfile) && item.SourceItemId == 366 && item.Name == "TEACHY TV" && item.Price == 0 &&
                    item.Importance == 1 && item.Registrability == 1 && item.Pocket == "POCKET_KEY_ITEMS" && item.Type == "ITEM_TYPE_FIELD" &&
                    item.FieldUseFunction == "FieldUseFunc_TeachyTv" && item.BattleUsage == 0 && item.BattleUseFunction == "NULL" &&
                    item.SecondaryId == 0 && !item.CanToss && !item.CanSell && !item.CanGive,
                TownMapSymbol => catalog.Profile == TownMapProfile && item.SourceItemId == 361 && item.Name == "TOWN MAP" && item.Price == 0 &&
                    item.Importance == 1 && item.Registrability == 1 && item.Pocket == "POCKET_KEY_ITEMS" && item.Type == "ITEM_TYPE_BAG_MENU" &&
                    item.FieldUseFunction == "FieldUseFunc_TownMap" && item.BattleUsage == 0 && item.BattleUseFunction == "NULL" &&
                    item.SecondaryId == 0 && !item.CanToss && !item.CanSell && !item.CanGive,
                _ => false
            };
            if (!valid) throw new NotSupportedException("Unsupported quest item metadata.");
        }
    }

    /// <summary>Validates only the additional pockets. The caller also validates ordinary Items/PC.</summary>
    public static string? Validate(QuestItemsCatalog catalog, InventoryState? state)
    {
        ValidateCatalog(catalog);
        if (state is null) return "inventory_missing";
        return ValidatePocket(state.KeyItems, catalog.Limits.KeyItemSlots, catalog.Items.Where(x=>x.Pocket=="POCKET_KEY_ITEMS").Select(x=>x.Id).ToHashSet(), "key_items") ??
            ValidatePocket(state.Balls, catalog.Limits.BallSlots, new HashSet<ItemId>{PokeBallId}, "balls");
    }

    private static string? ValidatePocket(ImmutableArray<InventoryStack>? optional, int capacity, HashSet<ItemId> allowed, string pocket)
    {
        if (optional is null) return null;
        var stacks = optional.Value;
        if (stacks.IsDefault || stacks.Length > capacity) return "inventory_" + pocket + "_capacity";
        var seen = new HashSet<ItemId>();
        foreach (var stack in stacks)
        {
            if (stack is null) return "inventory_stack_missing";
            if (!allowed.Contains(stack.Item)) return "inventory_item_unsupported";
            if (stack.Quantity <= 0 || stack.Quantity > SourceLimits.MaximumStack) return "inventory_stack_quantity";
            if (!seen.Add(stack.Item)) return "inventory_duplicate_stack";
        }
        return null;
    }

    public static int Quantity(QuestItemsCatalog catalog, InventoryState state, string sourceItemSymbol)
    {
        ArgumentNullException.ThrowIfNull(state);
        string? error = Validate(catalog, state);
        if (error is not null) throw new ArgumentException(error, nameof(state));
        var definition = Definition(catalog, sourceItemSymbol) ??
            throw new NotSupportedException("inventory_item_unsupported");
        return OpeningInventoryRules.Quantity(Pocket(state, definition), definition.Id);
    }

    public static bool Check(QuestItemsCatalog catalog, InventoryState state, string sourceItemSymbol, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (Validate(catalog, state) is not null || quantity <= 0 || quantity > catalog.Limits.MaximumStack) return false;
        var definition = Definition(catalog, sourceItemSymbol);
        return definition is not null && OpeningInventoryRules.Quantity(Pocket(state, definition), definition.Id) >= quantity;
    }

    public static InventoryTransactionResult Add(QuestItemsCatalog catalog, InventoryState state,
        string sourceItemSymbol, int quantity = 1) => Change(catalog, state, sourceItemSymbol, quantity, true);

    public static InventoryTransactionResult Remove(QuestItemsCatalog catalog, InventoryState state,
        string sourceItemSymbol, int quantity = 1) => Change(catalog, state, sourceItemSymbol, quantity, false);

    private static InventoryTransactionResult Change(QuestItemsCatalog catalog, InventoryState state,
        string symbol, int quantity, bool adding)
    {
        ArgumentNullException.ThrowIfNull(state);
        InventoryTransactionResult Fail(string error) => new(state, false, error);
        string? error = Validate(catalog, state);
        if (error is not null) return Fail(error);
        var definition = Definition(catalog, symbol);
        if (definition is null) return Fail("inventory_item_unsupported");
        if (quantity <= 0 || quantity > catalog.Limits.MaximumStack) return Fail("inventory_transfer_quantity");
        var stacks = Pocket(state, definition);
        int existing = OpeningInventoryRules.Quantity(stacks, definition.Id);
        if (adding)
        {
            if (existing > catalog.Limits.MaximumStack - quantity) return Fail("inventory_stack_full");
            int capacity = definition.Pocket == "POCKET_KEY_ITEMS" ? catalog.Limits.KeyItemSlots : catalog.Limits.BallSlots;
            if (existing == 0 && stacks.Length >= capacity) return Fail("inventory_destination_full");
        }
        else if (existing < quantity) return Fail("inventory_insufficient_quantity");
        var changed = OpeningInventoryRules.SetQuantity(stacks, definition.Id, existing + (adding ? quantity : -quantity));
        return new(definition.Pocket == "POCKET_KEY_ITEMS" ? state with { KeyItems = changed } :
            state with { Balls = changed }, true, null);
    }

    private static QuestItemDefinition? Definition(QuestItemsCatalog catalog, string symbol)
        => catalog.Items.FirstOrDefault(x => x.SourceItemSymbol == symbol);

    private static ImmutableArray<InventoryStack> Pocket(InventoryState state, QuestItemDefinition item)
        => (item.Pocket == "POCKET_KEY_ITEMS" ? state.KeyItems : state.Balls) ?? [];
}
