using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Explicit three-interior successor. Ownership and inherited source mechanics remain Silph113.</summary>
public sealed partial class PretCampaignCatalog
{
    public const string SaffronGymContentProfile="firered-saffron-gym-campaign-v1";
    public PretSaffronGymArt? SaffronGymArt{get;}
    public PretSaffronGymRegion? SaffronGymRegion{get;}
    public ImmutableSortedDictionary<int,string> SaffronGymSpeciesNames{get;}
    private ImmutableArray<SilphWarpPad> SaffronGymWarpPads=>SaffronGymRegion is null?[]:SaffronGymRegion.NewMaps[new("map.saffron-city-gym")].FieldWarps.Where(w=>w.DestinationMapId.Value=="map.saffron-city-gym").Select(w=>new SilphWarpPad(new("map.saffron-city-gym"),w.Index,w.X,w.Y,w.Elevation,w.DestinationMapId,w.DestinationWarpId)).ToImmutableArray();
    public SaffronGymTrainerCatalogContent? SaffronGymTrainers{get;}
    public PretSaffronGymItemsDocument? SaffronGymItems{get;}
    public SaffronGymOpponentPokemonCatalog? SaffronGymOpponents{get;}
    public bool SaffronGymEnabled=>SaffronGymRegion is not null;
    SaffronGymTrainerCatalog? ICampaignCatalog.SaffronGymTrainerParties=>SaffronGymTrainers is {} t?new(t.SourceCommit,t.Profile,t.CompleteSourceRoster,t.Trainers,t.Battles):null;
    public bool SupportsTeleportPads(MapId map)=>SilphEnabled&&SilphRegion!.NewMaps.ContainsKey(map)&&map.Value.StartsWith("map.silph-co-",StringComparison.Ordinal)||
        SaffronGymEnabled&&map.Value=="map.saffron-city-gym"&&SaffronGymRegion!.NewMaps.ContainsKey(map);

    private static void AppendSaffronGymSources(PretSaffronGymRegion region,PretSaffronGymItemsDocument items,List<PretSourceScriptFile> sources,List<string> texts,Dictionary<string,int> constants)
    {
        var paths=sources.ToDictionary(s=>s.Path,StringComparer.Ordinal);
        foreach(var source in region.Scripts)
        {
            if(paths.TryGetValue(source.Path,out var prior))
            {if(prior.Text!=source.Text)throw new InvalidDataException("saffron_gym_source_conflict: "+source.Path);continue;}
            paths.Add(source.Path,source);sources.Add(source);
            if(!texts.Contains(source.Text,StringComparer.Ordinal))texts.Add(source.Text);
        }
        foreach(var text in region.TextSources.Values)if(!texts.Contains(text,StringComparer.Ordinal))texts.Add(text);
        MergeRegionalServiceConstants(constants,region.Constants);
    }
    private static EarlyShopCatalog ComposeSaffronGymShop(EarlyShopCatalog previous,PretSaffronGymRegion region,PretSaffronGymItemsDocument items)
    {
        var candidate=PretSaffronGymItems.Extend(previous,items);
        return SaffronGymShopCatalog.Authenticate(previous,candidate with{Shops=candidate.Shops.Add(PretSaffronGymItems.MartStock(region))}).Catalog;
    }
}
