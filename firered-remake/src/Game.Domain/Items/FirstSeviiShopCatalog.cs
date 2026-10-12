using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.Items;
/// <summary>Adds ten original item definitions required by first-Sevii gifts and pickups to the authentic Cinnabar inventory.</summary>
public sealed class FirstSeviiShopCatalog
{
    public const string Profile = "firered-first-sevii-inventory-v1";
    public static ItemId MeteoriteId { get; } = new("pret:ITEM_METEORITE");
    public static ItemId TriPassId { get; } = new("pret:ITEM_TRI_PASS");
    public static ItemId IapapaBerryId { get; } = new("pret:ITEM_IAPAPA_BERRY");
    private static readonly ConditionalWeakTable<EarlyShopCatalog,FirstSeviiShopCatalog> Proofs = new();
    public CinnabarShopCatalog Predecessor { get; }
    public FirstSeviiItemCatalog Source { get; }
    public EarlyShopCatalog Catalog { get; }
    private FirstSeviiShopCatalog(CinnabarShopCatalog previous, FirstSeviiItemCatalog source)
    {
        Predecessor=previous;Source=source;
        Catalog=previous.Catalog with { Profile=Profile, Items=previous.Catalog.Items.AddRange(source.Additions),
            Shops=previous.Catalog.Shops.AddRange(source.Shops) };
    }
    public static FirstSeviiShopCatalog Authenticate(CinnabarShopCatalog previous, FirstSeviiItemCatalog source)
    {
        ArgumentNullException.ThrowIfNull(previous);ArgumentNullException.ThrowIfNull(source);source.Validate();
        if (!ReferenceEquals(CinnabarShopCatalog.For(previous.Catalog),previous) ||
            source.Additions.Any(i=>previous.Catalog.Items.Any(p=>p.Id==i.Id)))
            throw new ArgumentException("first_sevii_shop_predecessor");
        var proof=new FirstSeviiShopCatalog(previous,source);
        var missing=source.Pickups.Select(p=>p.Item).Concat(source.Shops.SelectMany(s=>s.Items))
            .Distinct().Where(id=>!proof.Catalog.Items.Any(i=>i.Id==id)).ToArray();
        if(missing.Length!=0)throw new ArgumentException("first_sevii_inventory_source_closure: "+string.Join(", ",missing.Select(i=>i.Value)));
        Proofs.Add(proof.Catalog,proof);return proof;
    }
    public static FirstSeviiShopCatalog? For(EarlyShopCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog,out var proof)?proof:null;
    public static void ValidateCatalog(EarlyShopCatalog catalog)
    { if(For(catalog) is null)throw new NotSupportedException("first_sevii_shop_authentication"); }
    public string? ValidateInventory(InventoryState? inventory)
    {
        if(inventory is null)return "inventory_missing";
        string? Pocket(ImmutableArray<InventoryStack>? stacks,int capacity,string? pocket)
        {
            if(stacks is not {} values)return null;
            if(values.IsDefault || values.Length>capacity)return "inventory_pocket_capacity";
            if(values.Any(v=>v is null))return "inventory_stack_missing";
            if(values.Any(v=>v.Quantity is <1 or >999))return "inventory_stack_quantity";
            if(values.Select(v=>v.Item).Distinct().Count()!=values.Length)return "inventory_duplicate_stack";
            foreach(var stack in values)
            {
                var addition=Source.Additions.FirstOrDefault(i=>i.Id==stack.Item);
                if(addition is not null&&(pocket is null?addition.Pocket=="POCKET_KEY_ITEMS":addition.Pocket!=pocket))
                    return "inventory_item_pocket";
            }
            return null;
        }
        if(Pocket(inventory.BagItems,Catalog.Limits.BagItemSlots,"POCKET_ITEMS") is {} bag)return bag;
        if(Pocket(inventory.KeyItems,Catalog.QuestLimits.KeyItemSlots,"POCKET_KEY_ITEMS") is {} key)return key;
        if(Pocket(inventory.Berries,SsAnneItemRules.BerrySlots,"POCKET_BERRY_POUCH") is {} berry)return berry;
        if(Pocket(inventory.PcItems,Catalog.Limits.PcItemSlots,null) is {} pc)return pc;
        if(inventory.Berries is {Length:>0} && OpeningInventoryRules.Quantity(inventory.KeyItems??[],SsAnneItemRules.BerryPouchId)!=1)
            return "inventory_berry_pouch_missing";
        return Predecessor.ValidateInventory(Project(inventory));
    }
    public InventoryState InventoryForPredecessor(InventoryState inventory)
    {
        if(ValidateInventory(inventory) is {} error)throw new ArgumentException(error,nameof(inventory));
        return Project(inventory);
    }
    public InventoryState RestoreFromPredecessor(InventoryState original,InventoryState changed)
    {
        _=InventoryForPredecessor(original);
        if(Predecessor.ValidateInventory(changed) is {} error)throw new ArgumentException(error,nameof(changed));
        ImmutableArray<InventoryStack>? Merge(ImmutableArray<InventoryStack>? before,ImmutableArray<InventoryStack>? after,Func<ItemId,bool> protect)
        {
            if(before is not {} values)return after;
            var result=after??[];
            for(int i=0;i<values.Length;i++)if(protect(values[i].Item))result=result.Insert(Math.Min(i,result.Length),values[i]);
            return result.IsEmpty && after is null?null:result;
        }
        var result=changed with {BagItems=Merge(original.BagItems,changed.BagItems,IsAdditional)!.Value,
            KeyItems=Merge(original.KeyItems,changed.KeyItems,IsAdditional),
            Berries=Merge(original.Berries,changed.Berries,IsAdditional),
            PcItems=Merge(original.PcItems,changed.PcItems,IsAdditional)!.Value};
        if(ValidateInventory(result) is {} resultError)throw new ArgumentException(resultError,nameof(changed));
        return result;
    }
    private bool IsAdditional(ItemId id)=>Source.Additions.Any(i=>i.Id==id);
    private InventoryState Project(InventoryState inventory)=>inventory with {
        BagItems=inventory.BagItems.RemoveAll(s=>IsAdditional(s.Item)),
        KeyItems=inventory.KeyItems?.RemoveAll(s=>IsAdditional(s.Item)),
        Berries=inventory.Berries?.RemoveAll(s=>IsAdditional(s.Item)),
        PcItems=inventory.PcItems.RemoveAll(s=>IsAdditional(s.Item))};
}
