using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretFuchsiaRouteRegion? FuchsiaRouteRegion {get;}
    public PretFuchsiaRoutePokemon? FuchsiaRoutePokemon {get;}
    public PretFuchsiaRouteArt? FuchsiaRouteArt {get;}
    public bool FuchsiaRouteEnabled=>FuchsiaRouteRegion is not null;
    public FuchsiaRouteTrainerCatalog? FuchsiaRouteTrainers=>FuchsiaRoutePokemon?.Trainers;
    // These are source-package completeness boundaries, not story locks. Resolve
    // static doors against the authenticated current graph, so newly implemented
    // destinations do not require another historical feature-name exception.
    public bool IsFuchsiaRouteWarpAllowed(MapId map,int sourceWarpId)=>
        FuchsiaRouteRegion is not {} region || !region.NewMaps.ContainsKey(map) ||
        IsCurrentStaticWarpResolved(map, sourceWarpId);

    private static void ValidateFuchsiaRouteComposition(PretFuchsiaRouteRegion region,ImmutableDictionary<MapId,PretWorldMap> maps,ImmutableDictionary<MapId,PretWorldMap> predecessor,CampaignPokemonContent pokemon)
    {
        if(pokemon.Catalog.Species.Count!=107 || pokemon.Catalog.Moves.Count!=244 ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(region.NewMaps.Keys) )throw new InvalidDataException("fuchsia_route_composition_scope");
    }
}
