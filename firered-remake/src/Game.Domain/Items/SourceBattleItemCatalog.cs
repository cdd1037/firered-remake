using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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

/// <summary>A separate, exact source item proof; never widens any historical shop/catalog profile.</summary>
public sealed class SourceBattleItemCatalog
{
    public const string SemanticSha256 = "ebebc7d3e8b6074a20d71a43f7c58509f8289a01573333022484a4e0bab48a63";
    public static InventoryLimits Limits { get; } = new(42, 30, 999);
    public ImmutableDictionary<ItemId, SourceBattleItemDefinition> Items { get; }
    private SourceBattleItemCatalog(ImmutableArray<SourceBattleItemDefinition> definitions) => Items = definitions.ToImmutableDictionary(d => d.Id);
    public static SourceBattleItemCatalog Authenticate(string sourceCommit, ImmutableArray<SourceBattleItemDefinition> definitions)
    {
        if (sourceCommit != PokemonFactory.SourceCommit || definitions.IsDefaultOrEmpty || definitions.Any(d => d is null || d.EffectBytes.IsDefault))
            throw new ArgumentException("source_item_catalog_identity");
        var text = new StringBuilder(sourceCommit).Append('\n');
        foreach (var d in definitions.OrderBy(d => d.SourceItemId))
            text.Append(CanonicalRow(d)).Append('\n');
        if (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))) != SemanticSha256)
            throw new ArgumentException("source_item_catalog_fingerprint");
        return new(definitions);
    }
    internal static string CanonicalRow(SourceBattleItemDefinition d) => string.Join('|',d.Symbol,
        d.SourceItemId.ToString(CultureInfo.InvariantCulture),d.Name,d.Pocket,d.BattleUsage.ToString(CultureInfo.InvariantCulture),
        d.BattleUseFunction,string.Join(',',d.EffectBytes),d.Reusable?"1":"0",d.HoldEffect,d.HoldEffectParam.ToString(CultureInfo.InvariantCulture));
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
