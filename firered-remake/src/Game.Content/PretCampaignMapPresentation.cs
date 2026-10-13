using System.Collections.Frozen;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Resolved content locations for one map; texture ownership remains in Presentation.</summary>
public sealed record PretMapPresentationBinding(string? NpcSourcePath,bool UsesLocalBorder);

public static class PretCampaignMapPresentation
{
    public static FrozenDictionary<MapId,PretMapPresentationBinding> Create(PretCampaignCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var npcs=new Dictionary<MapId,string>();
        var localBorders=new HashSet<MapId>();
        // Source-map overlays have explicit newest-first precedence. Border availability is
        // independent: a higher-priority NPC overlay need not supply its own border.
        void Add(IEnumerable<MapId> maps,string npcSource,bool localBorder=true)
        {
            foreach(var id in maps)
            {
                npcs.TryAdd(id,npcSource);
                if(localBorder)localBorders.Add(id);
            }
        }
        Add(catalog.FightingDojoRegion.NewMaps.Keys,catalog.FightingDojoRegion.NpcSourcePath);
        Add(catalog.FossilLabRegion.NewMaps.Keys,catalog.FossilLabRegion.NpcSourcePath);
        Add(catalog.TrainerTowerRegion.Maps.Keys,catalog.TrainerTowerRegion.NpcSourcePath);
        Add(catalog.PowerPlantRegion.NewMaps.Keys,catalog.PowerPlantRegion.NpcSourcePath);
        Add(catalog.EventIslandRegion.NewMaps.Keys,catalog.EventIslandRegion.NpcSourcePath);
        Add(catalog.SevenIslandRegion.NewMaps.Keys,catalog.SevenIslandRegion.NpcSourcePath);
        Add(catalog.CeruleanCaveRegion.NewMaps.Keys,catalog.CeruleanCaveRegion.NpcSourcePath);
        if(catalog.FiveIslandRegion is {} fiveIsland) Add(fiveIsland.NewMaps.Keys,fiveIsland.NpcSourcePath);
        if(catalog.SixIslandRegion is {} sixIsland) Add(sixIsland.NewMaps.Keys,sixIsland.NpcSourcePath);
        if(catalog.FourIslandRegion is {} fourIsland) Add(fourIsland.NewMaps.Keys,fourIsland.NpcSourcePath);
        if(catalog.SeviiRubyRegion is {} seviiRuby) Add(seviiRuby.NewMaps.Keys,seviiRuby.NpcSourcePath);
        if(catalog.PokemonLeagueRegion is {} pokemonLeague) Add(pokemonLeague.NewMaps.Keys,pokemonLeague.NpcSourcePath,false);
        if(catalog.VictoryRoadRegion is {} victoryRoad) Add(victoryRoad.NewMaps.Keys,victoryRoad.NpcSourcePath);
        if(catalog.ViridianGymRegion is {} viridianGym) Add(viridianGym.NewMaps.Keys,viridianGym.NpcSourcePath);
        if(catalog.FirstSeviiRegion is {} firstSevii) Add(firstSevii.NewMaps.Keys,firstSevii.NpcSourcePath);
        if(catalog.CinnabarRegion is {} cinnabar) Add(cinnabar.NewMaps.Keys,cinnabar.NpcSourcePath);
        if(catalog.SaffronGymRegion is {} saffronGym) Add(saffronGym.NewMaps.Keys,saffronGym.NpcSourcePath);
        if(catalog.SilphRegion is {} silph) Add(silph.NewMaps.Keys,silph.NpcSourcePath);
        if(catalog.SafariRegion is {} safari) Add(safari.NewMaps.Keys,safari.NpcSourcePath);
        if(catalog.FuchsiaGymRegion is {} fuchsiaGym) Add(fuchsiaGym.NewMaps.Keys,fuchsiaGym.NpcSourcePath);
        if(catalog.FuchsiaRouteRegion is {} fuchsiaRoute) Add(fuchsiaRoute.NewMaps.Keys,fuchsiaRoute.NpcSourcePath);
        if(catalog.RegionalServiceMaps is {} regionalServices) Add(catalog.SourceMaps.Keys.Where(catalog.IsRegionalServiceMap),regionalServices.NpcSourcePath);
        if(catalog.SnorlaxRegion is {} snorlax) Add(snorlax.NewMaps.Keys,snorlax.NpcSourcePath);
        if(catalog.PokemonTowerRegion is {} tower) { Add(tower.NewMaps.Keys,tower.NpcSourcePath); Add([PretPokemonTowerRegion.HouseMapId],tower.NpcSourcePath,false); }
        if(catalog.RocketHideoutRegion is {} rocket) Add(rocket.NewMaps.Keys,rocket.NpcSourcePath);
        if(catalog.CeladonGymRegion is {} celadonGym) Add(celadonGym.NewMaps.Keys,celadonGym.NpcSourcePath);
        if(catalog.CeladonApproachRegion is {} approach) Add(approach.NewMaps.Keys,approach.NpcSourcePath);
        if(catalog.RockTunnelRegion is {} rockTunnel) Add(rockTunnel.NewMaps.Keys,rockTunnel.NpcSourcePath);
        if(catalog.Route11Region is {} route11) Add(route11.NewMaps.Keys,route11.NpcSourcePath);
        if(catalog.CityServices is {} city) Add(city.NewMaps.Keys,city.NpcSourcePath);
        if(catalog.VermilionGymRegion is {} gym) Add(gym.NewMaps.Keys,gym.NpcSourcePath);
        if(catalog.SsAnneRegion is {} ship) Add(ship.NewMaps.Keys,ship.NpcSourcePath);
        if(catalog.VermilionRegion is {} vermilion) Add(vermilion.NewMaps.Keys,vermilion.NpcSourcePath);
        if(catalog.BillRegion is {} bill) Add(bill.NewMaps.Keys,bill.NpcSourcePath);
        if(catalog.CeruleanRegion is {} cerulean) Add(cerulean.NewMaps.Keys,cerulean.NpcSourcePath);
        if(catalog.MtMoonRegion is {} moon) Add(moon.NewMaps.Keys,moon.NpcSourcePath);
        if(catalog.Route3Region is {} route3) Add(route3.NewMaps.Keys,route3.NpcSourcePath);
        if(catalog.Route22Region is {} route22) Add(route22.NewMaps.Keys,route22.NpcSourcePath);
        if(catalog.PewterRegion is {} pewter) Add(pewter.NewMaps.Keys,pewter.NpcSourcePath);
        if(catalog.ForestRegion is {} forest) Add(forest.NewMaps.Keys,forest.NpcSourcePath);
        if(catalog.ViridianCenter is {} center) Add([center.Map.Id],center.NpcSourcePath,false);
        if(catalog.ViridianRegion is {} region) Add([new("map.route-1"),new("map.viridian-city"),new("map.viridian-mart")],region.NpcSourcePath,false);
        return catalog.SourceMaps.Keys.ToFrozenDictionary(id=>id,
            id=>new PretMapPresentationBinding(npcs.GetValueOrDefault(id),localBorders.Contains(id)));
    }
}
