using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Eighteen original connected Lost Cave / Resort Gorgeous maps. Loading does not admit gameplay or services.</summary>
public sealed class PretLostCaveRegion
{
    public const string Profile = "firered-lost-cave-region-v1";
    private static readonly string[] SourceNames = ["FiveIsland_WaterLabyrinth", "FiveIsland_ResortGorgeous",
        "FiveIsland_ResortGorgeous_House", "FiveIsland_LostCave_Entrance",
        .. Enumerable.Range(1,14).Select(i => "FiveIsland_LostCave_Room" + i.ToString(CultureInfo.InvariantCulture))];
    public static MapId WaterLabyrinthMapId { get; } = new("map.five-island-water-labyrinth");
    public static MapId ResortGorgeousMapId { get; } = new("map.five-island-resort-gorgeous");
    public static MapId HouseMapId { get; } = new("map.five-island-resort-gorgeous-house");
    public static MapId EntranceMapId { get; } = new("map.five-island-lost-cave-entrance");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement Items { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretLostCaveRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string,int> constants)
    {
        NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory; Catalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); Items = source.Document("items.json"); NpcSources = source.Document("npc-source.json");

    }

    public static MapId MapIdFromSourceSymbol(string symbol)
    {
        if (symbol.StartsWith("MAP_FIVE_ISLAND_LOST_CAVE_ROOM", StringComparison.Ordinal) &&
            int.TryParse(symbol.AsSpan("MAP_FIVE_ISLAND_LOST_CAVE_ROOM".Length), NumberStyles.None,
                CultureInfo.InvariantCulture, out int room) && room is >= 1 and <= 14 &&
            symbol == "MAP_FIVE_ISLAND_LOST_CAVE_ROOM" + room.ToString(CultureInfo.InvariantCulture))
            return new("map.five-island-lost-cave-room" + room.ToString(CultureInfo.InvariantCulture));
        return symbol switch
        {
            "MAP_FIVE_ISLAND_WATER_LABYRINTH" => WaterLabyrinthMapId,
            "MAP_FIVE_ISLAND_RESORT_GORGEOUS" => ResortGorgeousMapId,
            "MAP_FIVE_ISLAND_RESORT_GORGEOUS_HOUSE" => HouseMapId,
            "MAP_FIVE_ISLAND_LOST_CAVE_ENTRANCE" => EntranceMapId,
            "MAP_FIVE_ISLAND" => new("map.five-island"),
            _ => throw new InvalidDataException("Unknown Lost Cave map symbol: " + symbol)
        };
    }

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var source = map.Events.GetProperty("connections");
        var edges = source.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty :
            source.EnumerateArray().Select(c => new ConnectedFieldConnection(
                c.GetProperty("direction").GetString() switch
                {
                    "up" => Direction.North, "down" => Direction.South,
                    _ => throw new InvalidDataException("Lost Cave has only original north/south outdoor connections.")
                }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }

    public static PretLostCaveRegion Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot,"maps/regions/lost-cave");
        var catalog = source.Document("catalog.json");

        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original Lost Cave and connecting outdoor maps; gameplay and services require separate admission.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!,directory = spec.GetProperty("mapPath").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need(path == directory , "Lost Cave compact map binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == id && runtime.TechnicalSpawn is null,
                "Lost Cave source map identity differs.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, directory),
                PretOverworldTextures.Resolve(source.Directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
            maps.Add(map.Id, map);
        }
        Need(maps.Values.Select(m => m.SourceMapName).ToHashSet().SetEquals(SourceNames),
            "Lost Cave exact eighteen-map cohort differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source,maps.ToImmutable(),constants);
    }

    /// <summary>Append this binding to the shared item services only when the Selphy scene/service is admitted.</summary>
    public ImmutableArray<FieldItemServiceDefinition> ServiceDefinitions()
    {
        var binding = Contracts.GetProperty("serviceBindings")[0];
        var e = NewMaps[HouseMapId].Events.GetProperty("object_events")[0];
        string script = binding.GetProperty("scriptId").GetString()!;
        var programs = binding.GetProperty("programIds").EnumerateArray().Select(x => x.GetString()!).ToImmutableArray();
        var items = binding.GetProperty("itemSymbols").EnumerateArray().Select(x => x.GetString()!).ToImmutableArray();
        Need(binding.GetProperty("sourceLocalId").GetInt32() == 1 && binding.GetProperty("mapId").GetString() == HouseMapId.Value &&
            JsonElement.DeepEquals(e,binding.GetProperty("sourceEvent")) && e.GetProperty("script").GetString() == script &&
            programs.Contains(script) && programs.Contains("Std_ObtainItem") &&
            programs.Distinct(StringComparer.Ordinal).Count() == programs.Length && items.Length == 7 && items.All(Constants.ContainsKey),
            "Selphy shared field-item service binding differs.");
        return [new(binding.GetProperty("id").GetString()!, HouseMapId, script, 1, null, null, programs, items)];
    }

    public void ValidateReusedMaps(IReadOnlyDictionary<MapId,PretWorldMap> existing)
    {
        var anchor = Contracts.GetProperty("retainedAnchor");
        Need(existing.TryGetValue(new("map.five-island"),out var map) &&
            map.Events.GetProperty("connections").EnumerateArray().Any(c => JsonElement.DeepEquals(c,anchor.GetProperty("connection"))),
            "Existing Five Island original north connection differs.");
    }

    private static void Need(bool value,string message) => SilphSourcePackage.Need(value,message);
}
