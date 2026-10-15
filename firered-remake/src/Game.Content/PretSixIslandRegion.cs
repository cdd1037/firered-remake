using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Twelve exact Six Island and Dotted Hole maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretSixIslandRegion
{
    public const string Profile = "firered-six-island-region-v1";
    // Authenticated against LAYOUT_SIX_ISLAND_GREEN_PATH below. This preserves the
    // unloaded northern boundary without hiding WaterPath's later west connections.
    private const int GreenPathConnectionHeight = 20;
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public JsonElement FallWarps => Contracts.GetProperty("fallWarps");
    public JsonElement DottedHoleDoor => Contracts.GetProperty("dottedHoleDoor");
    public JsonElement SapphireTheft => Contracts.GetProperty("sapphireTheft");
    public JsonElement DirectionalBraille => Contracts.GetProperty("directionalBraille");
    public JsonElement SapphireBraille => Contracts.GetProperty("sapphireBraille");
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public JsonElement ExcludedEncounterGates => Contracts.GetProperty("excludedEncounterGates");
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretSixIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants; 
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SIX_ISLAND" => new("map.six-island"),
        "MAP_SIX_ISLAND_DOTTED_HOLE_1F" => new("map.six-island-dotted-hole-1f"),
        "MAP_SIX_ISLAND_DOTTED_HOLE_B1F" => new("map.six-island-dotted-hole-b1f"),
        "MAP_SIX_ISLAND_DOTTED_HOLE_B2F" => new("map.six-island-dotted-hole-b2f"),
        "MAP_SIX_ISLAND_DOTTED_HOLE_B3F" => new("map.six-island-dotted-hole-b3f"),
        "MAP_SIX_ISLAND_DOTTED_HOLE_B4F" => new("map.six-island-dotted-hole-b4f"),
        "MAP_SIX_ISLAND_DOTTED_HOLE_SAPPHIRE_ROOM" => new("map.six-island-dotted-hole-sapphire-room"),
        "MAP_SIX_ISLAND_GREEN_PATH" => new("map.six-island-green-path"),
        "MAP_SIX_ISLAND_HARBOR" => new("map.six-island-harbor"),
        "MAP_SIX_ISLAND_HOUSE" => new("map.six-island-house"),
        "MAP_SIX_ISLAND_MART" => new("map.six-island-mart"),
        "MAP_SIX_ISLAND_POKEMON_CENTER_1F" => new("map.six-island-pokemon-center-1f"),
        "MAP_SIX_ISLAND_POKEMON_CENTER_2F" => new("map.six-island-pokemon-center-2f"),
        "MAP_SIX_ISLAND_RUIN_VALLEY" => new("map.six-island-ruin-valley"),
        "MAP_SIX_ISLAND_WATER_PATH" => new("map.six-island-water-path"),
        "MAP_SIX_ISLAND_WATER_PATH_HOUSE1" => new("map.six-island-water-path-house1"),
        "MAP_SIX_ISLAND_WATER_PATH_HOUSE2" => new("map.six-island-water-path-house2"),
        _ => PretFourIslandRegion.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown SixIsland connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!),
                map.Id.Value == "map.six-island-water-path" && c.GetProperty("map").GetString() == "MAP_SIX_ISLAND_GREEN_PATH" &&
                    c.GetProperty("direction").GetString() == "left" ? GreenPathConnectionHeight : null)).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }
    public static PretSixIslandRegion Load(string root)
    {
        var source = SixIslandSource.Region(root);
        var files = ImmutableArray<PretWorldSourceFile>.Empty;
        var directory = source.Directory;
        var catalog = source.Document("catalog.json");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original Six Island and Dotted Hole source maps; application admission remains separate.", Profile, files);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!; var relative = spec.GetProperty("directory").GetString()!;
            var runtimeEntry = spec;
            Need(runtimeEntry.GetProperty("path").GetString() == relative + "/runtime-map.json" , "SixIsland runtime certificate differs.");
            var payload = source.Read(relative + "/runtime-map.json");
            var runtime = PretRuntimeMapData.Decode(payload, stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == stableId, "Map symbol differs from its ID.");
            var mapDirectory = Safe(directory, relative);
            var playerTexture = PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png");
            maps.Add(new(stableId), PretWorldMap.FromRuntime(runtime, metadata, mapDirectory, playerTexture, MapIdFromSourceSymbol));
        }
        Need(maps.Count == 12 && maps.Values.Sum(m => m.Width * m.Height) == 6498 && maps.Values.Sum(m => m.FieldWarps.Length) == 37,
            "SixIsland source map collection differs.");
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "SixIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
