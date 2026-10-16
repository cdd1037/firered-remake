using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public const string SilphContentProfile="firered-silph-campaign-v1";
    public PretSilphRegion? SilphRegion { get; }
    public PretSilphPokemon? SilphPokemon { get; }
    public PretSilphItemsDocument? SilphItems { get; }
    public SilphTrainerCatalogContent? SilphTrainers { get; }
    public GamePokemonCatalog? SilphOwnership { get; }
    public GameOpponentPokemonCatalog? SilphOpponents { get; }
    public PretSilphArt? SilphArt { get; }
    public bool SilphEnabled=>SilphRegion is not null;
    SilphTrainerCatalog? ICampaignCatalog.SilphTrainerParties=>SilphTrainers is {} t?new(t.Trainers,t.Battles):null;
    public ImmutableArray<SilphWarpPad> SilphWarpPads=>(SilphRegion?.WarpPads??[]).AddRange(SaffronGymWarpPads);
    public ImmutableArray<SilphDoorDefinition> SilphDoors=>SilphRegion?.Doors??[];
    public ImmutableArray<CampaignSilphElevatorFloor> SilphElevatorFloors=>SilphRegion?.ElevatorFloors??[];
    public ImmutableArray<CampaignSilphElevatorEntrance> SilphElevatorEntrances=>SilphRegion?.ElevatorEntrances??[];
    public ImmutableArray<CampaignSilphDynamicExit> SilphDynamicExits=>SilphRegion?.DynamicExits??[];
    public string PresentedSpeciesName(int species)=>PokemonContent?.SpeciesNames.GetValueOrDefault(species)
        ??SilphPokemon?.AdditionalSpeciesNames.GetValueOrDefault(species)
        ??SaffronGymSpeciesNames.GetValueOrDefault(species)
        ??SourceSpeciesName(species)
        ??throw new InvalidDataException("presentation_species_not_admitted");

    private static IEnumerable<FieldPickupDefinition> SilphPickupDefinitions(PretSilphRegion region,EarlyShopCatalog shop,MachineCatalog machines,PretSilphItemsDocument items,PretStoryCompilation scripts)
    {
        foreach(var row in region.Contracts.GetProperty("pickups").EnumerateArray())
        {
            string symbol=row.GetProperty("item").GetString()!;var id=new ItemId("pret:"+symbol);
            var ordinary=shop.Items.SingleOrDefault(i=>i.Id==id);
            var source=MachineRules.Definition(machines,id)?.Machine.Item??items.Items.Single(i=>i.Metadata.Item.Id==id).Metadata.Item;
            var map=region.NewMaps[new(row.GetProperty("map").GetString()!)];
            int local=row.GetProperty("localId").GetInt32(),x=row.GetProperty("x").GetInt32(),y=row.GetProperty("y").GetInt32();
            var actor=map.Events.GetProperty("object_events")[local-1];
            string flag=row.GetProperty("flag").GetString()!,label=row.GetProperty("source").GetProperty("label").GetString()!;
            if(actor.GetProperty("script").GetString()!=label||actor.GetProperty("flag").GetString()!=flag||
                actor.GetProperty("x").GetInt32()!=x||actor.GetProperty("y").GetInt32()!=y||!scripts.TryConstant(flag,out int sourceFlag))
                throw new InvalidDataException("silph_pickup_source_mismatch");
            yield return new("silph:"+flag,map.SourceMapSymbol,false,local,x,y,actor.GetProperty("elevation").GetInt32(),label,symbol,
                ordinary?.SourceItemId??source.SourceItemId,id,ordinary?.Name??source.Name,ordinary?.Pocket??source.Pocket,1,flag,sourceFlag);
        }
        foreach(var row in region.Contracts.GetProperty("hiddenItems").EnumerateArray())
        {
            var d=row.GetProperty("declaration");var item=shop.Items.Single(i=>i.Id.Value=="pret:"+d.GetProperty("item").GetString());
            var map=region.NewMaps[new(row.GetProperty("map").GetString()!)];string flag=d.GetProperty("flag").GetString()!;
            if(!scripts.TryConstant(flag,out int sourceFlag)||d.GetProperty("underfoot").GetBoolean())throw new InvalidDataException("silph_hidden_source");
            yield return new("silph:"+flag,map.SourceMapSymbol,true,null,d.GetProperty("x").GetInt32(),d.GetProperty("y").GetInt32(),
                d.GetProperty("elevation").GetInt32(),"EventScript_HiddenItemScript",item.SourceItemSymbol,item.SourceItemId,item.Id,item.Name,item.Pocket,
                d.GetProperty("quantity").GetInt32(),flag,sourceFlag);
        }
    }
}
