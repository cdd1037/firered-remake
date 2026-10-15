using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.Items;

/// <summary>Thunder Stone and the two source machines; every inherited item and shop retains identity.</summary>
public sealed class PowerPlantShopCatalog
{
    public const string Profile = "firered-power-plant-inventory-v1";
    public static ItemId ThunderStoneId { get; } = new("pret:ITEM_THUNDER_STONE");
    public static ItemId Tm17Id => PowerPlantMachineCatalog.Tm17Id;
    public static ItemId Tm25Id => PowerPlantMachineCatalog.Tm25Id;
    private static readonly ConditionalWeakTable<EarlyShopCatalog, PowerPlantShopCatalog> Proofs = new();
    public EarlyShopCatalog Catalog { get; }
    public EventIslandShopCatalog Predecessor { get; }
    private PowerPlantShopCatalog(EarlyShopCatalog catalog, EventIslandShopCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static PowerPlantShopCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if (For(catalog) is null) throw new NotSupportedException("power_plant_shop_authentication"); }
    public static PowerPlantShopCatalog Authenticate(EventIslandShopCatalog predecessor, EarlyShopCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(EventIslandShopCatalog.For(old), predecessor) || candidate.Profile != Profile ||
            old.Items.Length != 120 || candidate.Items.IsDefault || candidate.Items.Length != 123 ||
            candidate.Items.Any(i => i is null || i.EffectBytes.IsDefault) || candidate.Items.Select(i => i.Id).Distinct().Count() != 123 ||
            candidate with { Profile = old.Profile, Items = old.Items } != old ||
            old.Items.Where((item, index) => !ReferenceEquals(item, candidate.Items[index])).Any())
            throw new ArgumentException("power_plant_shop_predecessor");
        var fresh = candidate.Items.Skip(120).ToArray();
        if (!fresh.Select(i => i.Id).SequenceEqual([ThunderStoneId,Tm17Id,Tm25Id]))
            throw new ArgumentException("power_plant_shop_source");
        var proof = new PowerPlantShopCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    private static bool Additional(ItemId item) => item == ThunderStoneId || item == Tm17Id || item == Tm25Id;
    public string? ValidateInventory(InventoryState? inventory)
    {
        if (inventory is null) return "inventory_missing";
        string? Pocket(ImmutableArray<InventoryStack>? stacks, int capacity, string pocket)
        {
            if (stacks is not {} values) return null;
            if (values.IsDefault || values.Length > capacity) return "inventory_pocket_capacity";
            if (values.Any(v => v is null)) return "inventory_stack_missing";
            if (values.Any(v => v.Quantity is < 1 or > 999)) return "inventory_stack_quantity";
            if (values.Select(v => v.Item).Distinct().Count() != values.Length) return "inventory_duplicate_stack";
            foreach (var stack in values.Where(v => Additional(v.Item)))
                if (stack.Item == ThunderStoneId ? pocket is not ("POCKET_ITEMS" or "PC") : pocket is not ("POCKET_TM_CASE" or "PC"))
                    return "inventory_item_pocket";
            return null;
        }
        if (Pocket(inventory.KeyItems, Catalog.QuestLimits.KeyItemSlots, "POCKET_KEY_ITEMS") is {} key) return key;
        if (Pocket(inventory.BagItems, Catalog.Limits.BagItemSlots, "POCKET_ITEMS") is {} bag) return bag;
        if (Pocket(inventory.PcItems, Catalog.Limits.PcItemSlots, "PC") is {} pc) return pc;
        if (Pocket(inventory.Balls, Catalog.QuestLimits.BallSlots, "POCKET_POKE_BALLS") is {} ball) return ball;
        if (Pocket(inventory.Berries, SsAnneItemRules.BerrySlots, "POCKET_BERRY_POUCH") is {} berry) return berry;
        if (Pocket(inventory.Machines, 58, "POCKET_TM_CASE") is {} machine) return machine;
        return Predecessor.ValidateInventory(Project(inventory));
    }
    public InventoryState InventoryForPredecessor(InventoryState inventory)
    {
        if (ValidateInventory(inventory) is {} error) throw new ArgumentException(error, nameof(inventory));
        return Project(inventory);
    }
    public InventoryState RestoreFromPredecessor(InventoryState original, InventoryState changed)
    {
        _ = InventoryForPredecessor(original);
        if (Predecessor.ValidateInventory(changed) is {} error) throw new ArgumentException(error, nameof(changed));
        ImmutableArray<InventoryStack>? Merge(ImmutableArray<InventoryStack>? before, ImmutableArray<InventoryStack>? after)
        {
            if (before is not {} values) return after;
            var changedValues = after ?? []; var remaining = changedValues.ToDictionary(s => s.Item);
            var result = ImmutableArray.CreateBuilder<InventoryStack>();
            foreach (var stack in values)
                if (Additional(stack.Item)) result.Add(stack);
                else if (remaining.Remove(stack.Item, out var changedStack)) result.Add(changedStack);
            foreach (var stack in changedValues) if (remaining.ContainsKey(stack.Item)) result.Add(stack);
            return result.Count == 0 && after is null ? null : result.ToImmutable();
        }
        var restored = changed with { BagItems = Merge(original.BagItems, changed.BagItems)!.Value,
            PcItems = Merge(original.PcItems, changed.PcItems)!.Value, Machines = Merge(original.Machines, changed.Machines) };
        if (ValidateInventory(restored) is {} resultError) throw new ArgumentException(resultError, nameof(changed));
        return restored;
    }
    private static InventoryState Project(InventoryState inventory) => inventory with {
        BagItems = inventory.BagItems.RemoveAll(s => Additional(s.Item)), PcItems = inventory.PcItems.RemoveAll(s => Additional(s.Item)),
        Machines = inventory.Machines?.RemoveAll(s => Additional(s.Item)) };
}
