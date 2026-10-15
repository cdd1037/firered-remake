using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

public sealed record SourceBattleItemDefinition(string Symbol, int SourceItemId, string Name, string Pocket,
    int BattleUsage, string BattleUseFunction, ImmutableArray<byte> EffectBytes, bool Reusable)
{
    public string HoldEffect { get; init; } = "HOLD_EFFECT_NONE";
    public int HoldEffectParam { get; init; }
    public ItemId Id => new("pret:" + Symbol);
    public bool IsFixedEffect => !EffectBytes.IsDefaultOrEmpty;
    public bool IsStatBooster => BattleUseFunction == "BattleUseFunc_StatBooster";
    public bool IsPokeFlute => Symbol == "ITEM_POKE_FLUTE";
    public bool IsEscapeItem => BattleUseFunction == "BattleUseFunc_PokeDoll";
}

/// <summary>Immutable source items; field and battle handlers decide which effects they support.</summary>
public sealed class SourceBattleItemCatalog
{
    public static InventoryLimits Limits { get; } = new(42, 30, 999);
    public ImmutableDictionary<ItemId, SourceBattleItemDefinition> Items { get; }
    private SourceBattleItemCatalog(ImmutableArray<SourceBattleItemDefinition> definitions) => Items = definitions.ToImmutableDictionary(d => d.Id);
    public static SourceBattleItemCatalog Authenticate(string sourceCommit, ImmutableArray<SourceBattleItemDefinition> definitions)
    {
        if (sourceCommit != PokemonFactory.SourceCommit || definitions.IsDefaultOrEmpty || definitions.Any(d => d is null || d.EffectBytes.IsDefault))
            throw new ArgumentException("source_item_catalog_identity");
        return new(definitions);
    }
    private static readonly ImmutableHashSet<string> FixedEffectItems =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "ITEM_POTION",
            "ITEM_ANTIDOTE",
            "ITEM_BURN_HEAL",
            "ITEM_ICE_HEAL",
            "ITEM_AWAKENING",
            "ITEM_PARALYZE_HEAL",
            "ITEM_FULL_RESTORE",
            "ITEM_MAX_POTION",
            "ITEM_HYPER_POTION",
            "ITEM_SUPER_POTION",
            "ITEM_FULL_HEAL",
            "ITEM_REVIVE",
            "ITEM_MAX_REVIVE",
            "ITEM_FRESH_WATER",
            "ITEM_SODA_POP",
            "ITEM_LEMONADE",
            "ITEM_MOOMOO_MILK",
            "ITEM_ENERGY_POWDER",
            "ITEM_ENERGY_ROOT",
            "ITEM_HEAL_POWDER",
            "ITEM_REVIVAL_HERB",
            "ITEM_ETHER",
            "ITEM_MAX_ETHER",
            "ITEM_ELIXIR",
            "ITEM_MAX_ELIXIR",
            "ITEM_LAVA_COOKIE",
            "ITEM_BLUE_FLUTE",
            "ITEM_YELLOW_FLUTE",
            "ITEM_RED_FLUTE",
            "ITEM_BERRY_JUICE",
            "ITEM_GUARD_SPEC",
            "ITEM_DIRE_HIT",
            "ITEM_X_ATTACK",
            "ITEM_X_DEFEND",
            "ITEM_X_SPEED",
            "ITEM_X_ACCURACY",
            "ITEM_X_SPECIAL",
            "ITEM_CHERI_BERRY",
            "ITEM_CHESTO_BERRY",
            "ITEM_PECHA_BERRY",
            "ITEM_RAWST_BERRY",
            "ITEM_ASPEAR_BERRY",
            "ITEM_LEPPA_BERRY",
            "ITEM_ORAN_BERRY",
            "ITEM_PERSIM_BERRY",
            "ITEM_LUM_BERRY",
            "ITEM_SITRUS_BERRY");

    internal static bool SupportsFixedEffect(SourceBattleItemDefinition? definition)
    {
        if (definition is null || !FixedEffectItems.Contains(definition.Symbol) ||
            definition.EffectBytes.IsDefault || definition.EffectBytes.Length < 6) return false;
        var effect = definition.EffectBytes;
        int arguments = ((effect[4] & 4) != 0 ? 1 : 0) + ((effect[4] & 8) != 0 ? 1 : 0);
        for (int band = 0; band < 3; band++) if ((effect[5] & (0x20 << band)) != 0) arguments++;
        return effect.Length == 6 + arguments;
    }
    public SourceBattleItemDefinition? Find(ItemId id) => Items.GetValueOrDefault(id);
    internal bool Owns(SourceBattleItemDefinition definition) => Items.TryGetValue(definition.Id, out var exact) && ReferenceEquals(exact, definition);
}

public static class SourceBattleInventoryRules
{
    public static string? Validate(SourceBattleItemCatalog catalog, InventoryState inventory)
    {
        if (catalog is null || inventory is null || inventory.Limits != SourceBattleItemCatalog.Limits) return "source_item_inventory_limits";
        string? Pocket(ImmutableArray<InventoryStack> stacks, string? pocket, int capacity)
        {
            if (stacks.IsDefault || stacks.Length > capacity) return "source_item_inventory_pocket";
            var seen = new HashSet<ItemId>();
            foreach (var stack in stacks)
                if (stack is null || stack.Quantity is < 1 or > 999 || !seen.Add(stack.Item) || catalog.Find(stack.Item) is not { } item ||
                    pocket is not null && item.Pocket != pocket || item.Pocket == "POCKET_KEY_ITEMS" && stack.Quantity != 1)
                    return "source_item_inventory_stack";
            return null;
        }
        return Pocket(inventory.BagItems, "POCKET_ITEMS", 42) ?? Pocket(inventory.PcItems, null, 30) ??
            Pocket(inventory.KeyItems ?? [], "POCKET_KEY_ITEMS", 30) ?? Pocket(inventory.Balls ?? [], "POCKET_POKE_BALLS", 13) ??
            Pocket(inventory.Machines ?? [], "POCKET_TM_CASE", 58) ?? Pocket(inventory.Berries ?? [], "POCKET_BERRY_POUCH", 43);
    }
    public static int Quantity(InventoryState inventory, SourceBattleItemDefinition item) => OpeningInventoryRules.Quantity(Pocket(inventory, item), item.Id);
    internal static ImmutableArray<InventoryStack> Pocket(InventoryState inventory, SourceBattleItemDefinition item) => item.Pocket switch {
        "POCKET_ITEMS" => inventory.BagItems, "POCKET_KEY_ITEMS" => inventory.KeyItems ?? [],
        "POCKET_BERRY_POUCH" => inventory.Berries ?? [], _ => [] };
    internal static InventoryState Consume(InventoryState inventory, SourceBattleItemDefinition item)
    {
        if (item.Reusable) return inventory;
        var pocket = Pocket(inventory, item); int index = Enumerable.Range(0, pocket.Length).Single(i => pocket[i].Item == item.Id);
        var result = pocket[index].Quantity == 1 ? pocket.RemoveAt(index) : pocket.SetItem(index, pocket[index] with { Quantity = pocket[index].Quantity - 1 });
        return item.Pocket switch { "POCKET_ITEMS" => inventory with { BagItems = result }, "POCKET_BERRY_POUCH" => inventory with { Berries = result }, _ => throw new ArgumentException("source_item_consumption_pocket") };
    }
}
