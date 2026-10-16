using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed class PretBillRegion
{
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    public JsonElement ExternalCeruleanEvents { get; }
    public JsonElement Encounters { get; }
    public JsonElement Animations { get; }

    private PretBillRegion(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        GeneratedDirectory = Path.Combine(project, "content/generated/maps/regions/bill");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
        using var ExternalCeruleanEventsDocument = JsonDocument.Parse(world.ReadEvents("data/maps/CeruleanCity/map.json"));
        ExternalCeruleanEvents = ExternalCeruleanEventsDocument.RootElement.Clone();
        Encounters = PretEarlyFieldData.LandHeaders(project, "MAP_ROUTE24", "MAP_ROUTE25");
        Animations = PretEarlyFieldData.Read(project, "maps/regions/bill/animations.json");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE24" => new("map.route-24"),
        "MAP_ROUTE25" => new("map.route-25"),
        "MAP_ROUTE25_SEA_COTTAGE" => new("map.route-25-sea-cottage"),
        _ => PretCeruleanRegion.MapIdFromSourceSymbol(symbol)
    };



    public static PretBillRegion Load(PretCurrentWorld world) => new(world);
}
