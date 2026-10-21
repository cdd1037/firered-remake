using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Encounters;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public const string CinnabarContentProfile="firered-cinnabar-campaign-v1";
    public PretCinnabarRegion CinnabarRegion{get;}
    public PretCinnabarArt? CinnabarArt{get;}
    public PretCinnabarPresentation? CinnabarPresentation{get;}
    public PretCinnabarItemsDocument? CinnabarItems{get;}
    public CinnabarTrainerCatalog? CinnabarTrainerParties{get;}
    public WaterEncounterCatalog? CinnabarWaterEncounters{get;}


    private static IEnumerable<FieldPickupDefinition> CinnabarPickupDefinitions(PretCinnabarRegion region,EarlyShopCatalog shop,MachineCatalog machines,PretStoryCompilation scripts)
    {
        foreach(var row in region.Contracts.GetProperty("itemPickups").EnumerateArray())
        {
            var item=row.GetProperty("event");var source=row.GetProperty("script");
            string label=source.GetProperty("label").GetString()!;
            var program=scripts.FindProgram(label)??throw new InvalidDataException("cinnabar_pickup_script_missing");
            var assignment=program.Instructions.Single(i=>i.Opcode=="setorcopyvar"&&i.Args[0]=="VAR_0x8000");
            string symbol=assignment.Args[1];var id=new ItemId("pret:"+symbol);
            var ordinary=shop.Items.SingleOrDefault(i=>i.Id==id);
            var machine=MachineRules.Definition(machines,id)?.Machine.Item;
            if(ordinary is null&&machine is null)throw new InvalidDataException("cinnabar_pickup_item_missing");
            var map=region.NewMaps[new(row.GetProperty("map").GetString()!)];int local=row.GetProperty("localId").GetInt32();
            string flag=item.GetProperty("flag").GetString()!;
            if(!scripts.TryConstant(flag,out int flagId)||!System.Text.Json.JsonElement.DeepEquals(item,map.Events.GetProperty("object_events")[local-1]))
                throw new InvalidDataException("cinnabar_pickup_source_mismatch");
            yield return new("cinnabar:"+flag,map.SourceMapSymbol,false,local,item.GetProperty("x").GetInt32(),item.GetProperty("y").GetInt32(),
                item.GetProperty("elevation").GetInt32(),label,symbol,ordinary?.SourceItemId??machine!.SourceItemId,id,
                ordinary?.Name??machine!.Name,ordinary?.Pocket??machine!.Pocket,1,flag,flagId);
        }

    }
}
