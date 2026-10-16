using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed class PretRockTunnelRegion
{
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    public ImmutableArray<MapId> ReusedMapIds => [new("map.cerulean-city")];
    public JsonElement ExternalCeruleanEvents { get; }

    private PretRockTunnelRegion(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        GeneratedDirectory = Path.Combine(project, "content/generated/maps/regions/rocktunnel");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
        using var ExternalCeruleanEventsDocument = JsonDocument.Parse(world.ReadEvents("data/maps/CeruleanCity/map.json"));
        ExternalCeruleanEvents = ExternalCeruleanEventsDocument.RootElement.Clone();
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE9" => new("map.route-9"),
        "MAP_ROUTE10" => new("map.route-10"),
        "MAP_ROCK_TUNNEL_1F" => new("map.rock-tunnel-1f"),
        "MAP_ROCK_TUNNEL_B1F" => new("map.rock-tunnel-b1f"),
        "MAP_ROUTE10_POKEMON_CENTER_1F" => new("map.route-10-pokemon-center-1f"),
        "MAP_ROUTE10_POKEMON_CENTER_2F" => new("map.route-10-pokemon-center-2f"),
        "MAP_LAVENDER_TOWN" => new("map.lavender-town"),
        "MAP_LAVENDER_TOWN_HOUSE1" => new("map.lavender-town-house-1"),
        "MAP_LAVENDER_TOWN_HOUSE2" => new("map.lavender-town-house-2"),
        "MAP_LAVENDER_TOWN_MART" => new("map.lavender-town-mart"),
        "MAP_LAVENDER_TOWN_POKEMON_CENTER_1F" => new("map.lavender-town-pokemon-center-1f"),
        "MAP_LAVENDER_TOWN_VOLUNTEER_POKEMON_HOUSE" => new("map.lavender-town-volunteer-pokemon-house"),
        "MAP_POKEMON_TOWER_1F" => new("map.pokemon-tower-1f"),
        "MAP_POWER_PLANT" => new("map.power-plant"),
        "MAP_ROUTE8" => new("map.route-8"),
        _ => PretRoute11Region.MapIdFromSourceSymbol(symbol)
    };



    public static PretRockTunnelRegion Load(PretCurrentWorld world) => new(world);
}
