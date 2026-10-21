using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Domain.Items;
using Game.Domain.World;
namespace Game.Content;
public sealed partial class PretCampaignCatalog
{
 private static IEnumerable<FieldPickupDefinition> SsAnnePickupDefinitions(PretSsAnneItemsDocument items,MachineCatalog machines,EarlyShopCatalog shop,PretStoryCompilation scripts)
 {
  foreach(var p in items.Pickups.Where(p => p.Kind != "hidden"))
  {
   var item=items.Items.Single(i=>i.Metadata.Item.Id==p.Item).Metadata.Item;
   if(!scripts.TryConstant(p.Flag,out int flag))throw new InvalidDataException("ssanne_pickup_flag_missing");
   string symbol="MAP_"+Regex.Replace(p.Map,"([a-z])([A-Z])","$1_$2").ToUpperInvariant();

   yield return new("ssanne:"+p.Flag,symbol,false,p.LocalObjectId,p.X,p.Y,p.Elevation,p.Script!,item.SourceItemSymbol,item.SourceItemId,item.Id,item.Name,item.Pocket,p.Quantity,p.Flag,flag);
  }
 }
}
