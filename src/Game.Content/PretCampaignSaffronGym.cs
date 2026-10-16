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
    public GameOpponentPokemonCatalog? SaffronGymOpponents{get;}
    public bool SaffronGymEnabled=>SaffronGymRegion is not null;
    SaffronGymTrainerCatalog? ICampaignCatalog.SaffronGymTrainerParties=>SaffronGymTrainers is {} t?new(t.Trainers,t.Battles):null;
    public bool SupportsTeleportPads(MapId map)=>SilphEnabled&&SilphRegion!.NewMaps.ContainsKey(map)&&map.Value.StartsWith("map.silph-co-",StringComparison.Ordinal)||
        SaffronGymEnabled&&map.Value=="map.saffron-city-gym"&&SaffronGymRegion!.NewMaps.ContainsKey(map);

}
