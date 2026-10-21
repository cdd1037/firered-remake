using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;
namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    private static IEnumerable<FieldPickupDefinition> BillPickupDefinitions(PretBillItemsDocument items,MachineCatalog machines,PretStoryCompilation scripts)
    {
        // Visible machine balls use the regional script contract; every hidden event
        // is bound once by PretWorldHiddenPickups from the selected world.
        foreach(var p in items.Pickups.Where(p=>p.Kind=="visible"))
        {
            var item=machines.Machines.Single(m=>m.Machine.Item.Id==p.Item).Machine.Item;
            if(!scripts.TryConstant(p.Flag,out int flag))throw new InvalidDataException("bill_pickup_flag_missing");
            string map=p.Map switch{"Route24"=>"MAP_ROUTE24","Route25"=>"MAP_ROUTE25",_=>throw new InvalidDataException("bill_pickup_map_invalid")};
            yield return new("bill:"+p.Flag,map,false,p.LocalObjectId,p.X,p.Y,p.Elevation,p.Script!,item.SourceItemSymbol,item.SourceItemId,item.Id,item.Name,item.Pocket,p.Quantity,p.Flag,flag);
        }
    }
}
