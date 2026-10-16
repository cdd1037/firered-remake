using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Source-selected ordinary interiors. Geometry, actors and reciprocal doors are catalog data.</summary>
public sealed class PretSchoolHouseInteriors
{
    public const string ResourceId = "firered-school-house-interiors-v1";
    public static MapId SchoolMapId { get; } = new("map.viridian-school");
    public static MapId HouseMapId { get; } = new("map.fuchsia-city-house-1");
    public static MapId ThreeIslandHouseMapId { get; } = new("map.three-island-house2");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement MapCatalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private readonly ImmutableDictionary<string, MapId> _symbols;

    private PretSchoolHouseInteriors(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, MapId> symbols)
    {
        NewMaps = maps; _symbols = symbols;
        Constants = PretSharedConstants.ForPackage(source.Directory);
        GeneratedDirectory = source.Directory; MapCatalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); NpcSources = source.Document("npc-source.json");
    }

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Ordinary interiors must retain disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height, map.CreateConnectedCells(), []);
    }

    public static PretSchoolHouseInteriors Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot, "maps/regions/school-house-interiors");
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("resourceId").GetString() == ResourceId, "Ordinary interior catalog identity differs.");
        var symbols = catalog.GetProperty("mapSymbols").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => new MapId(p.Value.GetString()!), StringComparer.Ordinal);
        MapId Resolve(string symbol) => symbols.TryGetValue(symbol, out var id) ? id :
            throw new InvalidDataException("Unknown ordinary interior map symbol: " + symbol);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,
            "Local source evidence; redistribution rights are not inferred.", "Source-authenticated ordinary interiors.", ResourceId);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        var graphics = source.Document("npc-source.json").GetProperty("graphics").EnumerateArray()
            .Select(g => g.GetProperty("graphicsId").GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = entry.GetProperty("stableId").GetString()!, path = entry.GetProperty("mapPath").GetString()!;
            var runtime = PretRuntimeMapData.Decode(source.Read(path), id, Resolve);
            var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, path),
                PretOverworldTextures.Resolve(source.Directory, "sprites/overworld/red-normal.png"), Resolve);
            Need(Resolve(runtime.SourceMapSymbol) == map.Id && map.FieldWarps.Length > 0 &&
                map.Events.GetProperty("coord_events").GetArrayLength() == 0 &&
                map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
                "Ordinary interior identity or event scope differs.");
            foreach (var actor in map.Events.GetProperty("object_events").EnumerateArray())
                Need(graphics.Contains(actor.GetProperty("graphics_id").GetString()!), "Missing ordinary interior actor graphics.");
            maps.Add(map.Id, map);
        }
        Need(maps.Count > 0, "Ordinary interior catalog is empty.");
        var result = new PretSchoolHouseInteriors(source, maps.ToImmutable(), symbols);
        // Validate retained door declarations against their independently owned generated resources.
        foreach (var anchor in result.Contracts.GetProperty("retainedAnchors").EnumerateArray())
        {
            Need(anchor.GetProperty("currentPackage").GetString() == "content/generated", "Exterior root differs.");
            string relative = anchor.GetProperty("currentResource").GetProperty("path").GetString()!;
            var data = SilphSourcePackage.Json(SafariSource.Read(Path.Combine(dependencyRoot, "content/generated"), relative));
            result.ValidateAnchor(anchor, data.GetProperty("events"));
        }
        var doors = result.Contracts.GetProperty("retainedAnchors").EnumerateArray()
            .SelectMany(anchor => anchor.GetProperty("entrances").EnumerateArray().Select(entrance =>
                (Exterior: anchor.GetProperty("sourceMap").GetString()!, Index: entrance.GetProperty("index").GetInt32(),
                 Room: entrance.GetProperty("event").GetProperty("dest_map").GetString()!)))
            .ToHashSet();
        foreach (var room in result.NewMaps.Values)
            foreach (var exit in room.Events.GetProperty("warp_events").EnumerateArray())
                Need(doors.Contains((exit.GetProperty("dest_map").GetString()!,
                    int.Parse(exit.GetProperty("dest_warp_id").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                    room.SourceMapSymbol)), "Interior exit has no reciprocal exterior contract.");
        return result;
    }

    public void ValidateReusedMaps(IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        foreach (var anchor in Contracts.GetProperty("retainedAnchors").EnumerateArray())
        {
            var id = _symbols[anchor.GetProperty("sourceMap").GetString()!];
            Need(existing.TryGetValue(id, out var map), "Ordinary interior exterior is not mounted.");
            ValidateAnchor(anchor, map!.Events);
        }
    }

    private void ValidateAnchor(JsonElement anchor, JsonElement exterior)
    {
        Need(exterior.GetProperty("id").GetString() == anchor.GetProperty("sourceMap").GetString(), "Exterior identity differs.");
        foreach (var entry in anchor.GetProperty("entrances").EnumerateArray())
        {
            int index = entry.GetProperty("index").GetInt32();
            var entrance = exterior.GetProperty("warp_events")[index];
            Need(JsonElement.DeepEquals(entrance, entry.GetProperty("event")), "Exterior entrance differs.");
            var room = NewMaps[_symbols[entrance.GetProperty("dest_map").GetString()!]];
            int destination = int.Parse(entrance.GetProperty("dest_warp_id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            Need(destination >= 0 && destination < room.FieldWarps.Length, "Interior entrance index is invalid.");
            var exit = room.Events.GetProperty("warp_events")[destination];
            Need(exit.GetProperty("dest_map").GetString() == exterior.GetProperty("id").GetString() &&
                 exit.GetProperty("dest_warp_id").GetString() == index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                 "Interior door is not reciprocal.");
        }
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
