using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretFuchsiaRouteRegion FuchsiaRouteRegion {get;}
    public PretFuchsiaRouteArt? FuchsiaRouteArt {get;}
    public FuchsiaRouteTrainerCatalog? FuchsiaRouteTrainers { get; }
    // These are source-package completeness boundaries, not story locks. Resolve
    // static doors against the authenticated current graph, so newly implemented
    // destinations do not require another historical feature-name exception.
    public bool IsFuchsiaRouteWarpAllowed(MapId map,int sourceWarpId)=>
        FuchsiaRouteRegion is not {} region || !region.NewMaps.ContainsKey(map) ||
        IsCurrentStaticWarpResolved(map, sourceWarpId);

    private static void ValidateFuchsiaRouteComposition(PretFuchsiaRouteRegion region,ImmutableDictionary<MapId,PretWorldMap> maps,CampaignPokemonContent pokemon)
    {
        if(!region.NewMaps.Keys.All(maps.ContainsKey) )throw new InvalidDataException("fuchsia_route_composition_scope");
    }
}
