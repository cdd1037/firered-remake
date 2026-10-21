using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    private static IEnumerable<FieldPickupDefinition> MtMoonPickupDefinitions(PretMtMoonItemsDocument items,EarlyShopCatalog shop,MachineCatalog machines,PretStoryCompilation scripts)
    {
        foreach(var p in items.Pickups.Where(p => p.Kind != "hidden"))
        {
            var metadata=shop.Items.FirstOrDefault(i=>i.Id==p.Item);
            var tm=items.Machines.FirstOrDefault(m=>m.Machine.Item.Id==p.Item);
            int number=metadata?.SourceItemId??tm?.Machine.Item.SourceItemId??throw new InvalidDataException("mtmoon_pickup_item_missing");
            string name=metadata?.Name??tm!.Machine.Item.Name;
            string pocket=metadata?.Pocket??tm!.Machine.Item.Pocket;
            if(!scripts.TryConstant(p.Flag,out int flag))throw new InvalidDataException("mtmoon_pickup_flag_missing: "+p.Flag);
            string map=p.Map switch{"MtMoon_1F"=>"MAP_MT_MOON_1F","MtMoon_B1F"=>"MAP_MT_MOON_B1F","MtMoon_B2F"=>"MAP_MT_MOON_B2F","Route4"=>"MAP_ROUTE4",_=>throw new InvalidDataException("mtmoon_pickup_map_invalid")};
            yield return new("mtmoon:"+p.Flag,map,false,p.LocalObjectId,p.X,p.Y,p.Elevation,p.Script!,p.Item.Value[5..],number,p.Item,name,pocket,p.Quantity,p.Flag,flag);
        }
    }

}
