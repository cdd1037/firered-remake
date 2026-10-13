using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>The original nine-item lobby mart and Metal Coat prize; all 123 predecessor items retain identity.</summary>
public sealed class TrainerTowerShopCatalog
{
    public const string Profile = "firered-trainer-tower-inventory-v1";
    public const string AdditionalItemsSourceSha256 = "79966989eee691e6344fd42705c7e561710282771683554eb6ea542c628158d6";
    public const string LobbyShopSourceSha256 = "a8ab095796325d4c1b647da78ab8e9b1a40cdccc1d68e42fd17bf2f179d5f864";
    public const string LobbyShopId = "TrainerTower_Lobby_Mart_Items";
    public static ItemId MetalCoatId { get; } = new("pret:ITEM_METAL_COAT");
    private static readonly ConditionalWeakTable<EarlyShopCatalog, TrainerTowerShopCatalog> Proofs = new();
    public EarlyShopCatalog Catalog { get; }
    public PowerPlantShopCatalog Predecessor { get; }
    private TrainerTowerShopCatalog(EarlyShopCatalog catalog, PowerPlantShopCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static TrainerTowerShopCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if (For(catalog) is null) throw new NotSupportedException("trainer_tower_shop_authentication"); }
    public static TrainerTowerShopCatalog Authenticate(PowerPlantShopCatalog predecessor, EarlyShopCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(PowerPlantShopCatalog.For(old), predecessor) || candidate.Profile != Profile ||
            old.Items.Length != 123 || candidate.Items.IsDefault || candidate.Items.Length != 124 ||
            candidate.Items.Any(i => i is null || i.EffectBytes.IsDefault) || candidate.Items.Select(i => i.Id).Distinct().Count() != 124 ||
            candidate.Shops.IsDefault || candidate.Shops.Length != old.Shops.Length + 1 ||
            candidate.Shops.Select(s => s.Id).Distinct().Count() != candidate.Shops.Length ||
            candidate with { Profile = old.Profile, Items = old.Items, Shops = old.Shops } != old ||
            old.Items.Where((item, index) => !ReferenceEquals(item, candidate.Items[index])).Any() ||
            old.Shops.Where((shop, index) => !ReferenceEquals(shop, candidate.Shops[index])).Any())
            throw new ArgumentException("trainer_tower_shop_predecessor");
        if (candidate.Items[^1].Id != MetalCoatId ||
            CampaignSourceFingerprint.Compute(candidate.Items.Skip(123).ToArray()) != AdditionalItemsSourceSha256 ||
            CampaignSourceFingerprint.Compute(candidate.Shops[^1]) != LobbyShopSourceSha256 ||
            candidate.Shops[^1].Items.Any(id => !candidate.Items.Any(item => item.Id == id)))
            throw new ArgumentException("trainer_tower_shop_source");
        foreach (var symbol in new[] { "UP_GRADE", "DRAGON_SCALE", "METAL_COAT", "KINGS_ROCK" })
            if (candidate.Items.All(i => i.Id.Value != "pret:ITEM_" + symbol)) throw new ArgumentException("trainer_tower_prize_scope");
        var proof = new TrainerTowerShopCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public string? ValidateInventory(InventoryState? inventory)
    {
        if (inventory is null) return "inventory_missing";
        string? Pocket(ImmutableArray<InventoryStack>? stacks, int capacity, bool canContainMetalCoat)
        {
            if (stacks is not {} values) return null;
            if (values.IsDefault || values.Length > capacity) return "inventory_pocket_capacity";
            if (values.Any(v => v is null)) return "inventory_stack_missing";
            if (values.Any(v => v.Quantity is < 1 or > 999)) return "inventory_stack_quantity";
            if (values.Select(v => v.Item).Distinct().Count() != values.Length) return "inventory_duplicate_stack";
            if (!canContainMetalCoat && values.Any(v => v.Item == MetalCoatId)) return "inventory_item_pocket";
            return null;
        }
        if (Pocket(inventory.KeyItems, Catalog.QuestLimits.KeyItemSlots, false) is {} key) return key;
        if (Pocket(inventory.BagItems, Catalog.Limits.BagItemSlots, true) is {} bag) return bag;
        if (Pocket(inventory.PcItems, Catalog.Limits.PcItemSlots, true) is {} pc) return pc;
        if (Pocket(inventory.Balls, Catalog.QuestLimits.BallSlots, false) is {} ball) return ball;
        if (Pocket(inventory.Berries, SsAnneItemRules.BerrySlots, false) is {} berry) return berry;
        if (Pocket(inventory.Machines, 58, false) is {} machine) return machine;
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
        static ImmutableArray<InventoryStack> Merge(ImmutableArray<InventoryStack> before, ImmutableArray<InventoryStack> after)
        {
            var remaining = after.ToDictionary(s => s.Item); var result = ImmutableArray.CreateBuilder<InventoryStack>();
            foreach (var stack in before)
                if (stack.Item == MetalCoatId) result.Add(stack);
                else if (remaining.Remove(stack.Item, out var updated)) result.Add(updated);
            foreach (var stack in after) if (remaining.ContainsKey(stack.Item)) result.Add(stack);
            return result.ToImmutable();
        }
        var restored = changed with { BagItems = Merge(original.BagItems, changed.BagItems), PcItems = Merge(original.PcItems, changed.PcItems) };
        if (ValidateInventory(restored) is {} resultError) throw new ArgumentException(resultError, nameof(changed));
        return restored;
    }
    private static InventoryState Project(InventoryState inventory) => inventory with {
        BagItems = inventory.BagItems.RemoveAll(s => s.Item == MetalCoatId), PcItems = inventory.PcItems.RemoveAll(s => s.Item == MetalCoatId) };
}
