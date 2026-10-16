using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>Original Four Island and Icefall Cave maps, including guarded original boundaries. Application admission remains separate.</summary>
public sealed partial class PretFourIslandRegion
{
    public const string Profile = "firered-four-island-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public JsonElement IceFallPairs => Contracts.GetProperty("iceFallPairs");
    public JsonElement IceMetatilePatches => Contracts.GetProperty("iceMetatilePatches");
    public JsonElement WaterfallLanes => Contracts.GetProperty("waterfallLanes");
    public JsonElement SlidingIceCells => Contracts.GetProperty("slidingIceCells");
    public JsonElement ExternalBoundaries => Contracts.GetProperty("externalBoundaries");
    public JsonElement ExcludedEncounterGates => Contracts.GetProperty("excludedEncounterGates");
    public FieldMilestoneCollection StickerMilestones { get; }
    public ImmutableDictionary<string, FieldMilestoneCollection> FieldMilestones { get; }
    public FieldStatFlagUnlocks DollCollection { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretFourIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        var rules = Contracts.GetProperty("statFlagUnlocks").EnumerateArray().Select(r => new FieldStatFlagUnlock(
            r.GetProperty("stat").GetString()!, r.GetProperty("minimum").GetInt32(), r.GetProperty("flag").GetString()!)).ToImmutableArray();
        DollCollection = new(new("map.four-island-loreleis-house"), Contracts.GetProperty("statFlagUnlockSpecial").GetString()!, rules);
        var actors = NewMaps[DollCollection.Map].Events.GetProperty("object_events").EnumerateArray().ToArray();
        Need(rules.All(r => actors.Count(a => a.GetProperty("flag").GetString() == r.Flag) == 1), "Stat unlock must identify one source actor.");
        var stickers = Contracts.GetProperty("stickerMilestones");
        StickerMilestones = new(new(stickers.GetProperty("map").GetString()!), stickers.GetProperty("special").GetString()!,
            stickers.GetProperty("categories").EnumerateArray().Select(row => new FieldMilestone(
                row.GetProperty("stat").GetString()!, row.GetProperty("countVariable").GetString()!,
                row.GetProperty("stateVariable").GetString()!, row.GetProperty("bragFlag").GetInt32(),
                row.GetProperty("clampCount").GetBoolean(), row.GetProperty("stat").GetString() != "GAME_STAT_LINK_BATTLE_WINS",
                row.GetProperty("minimumCounts").EnumerateArray().Select(v => v.GetInt32()).ToImmutableArray())).ToImmutableArray());
        FieldMilestones = ImmutableDictionary<string, FieldMilestoneCollection>.Empty.Add(StickerMilestones.Special, StickerMilestones);
        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FOUR_ISLAND" => new("map.four-island"),
        "MAP_FOUR_ISLAND_HARBOR" => new("map.four-island-harbor"),
        "MAP_FOUR_ISLAND_HOUSE1" => new("map.four-island-house1"),
        "MAP_FOUR_ISLAND_HOUSE2" => new("map.four-island-house2"),
        "MAP_FOUR_ISLAND_ICEFALL_CAVE_1F" => new("map.four-island-icefall-cave-1f"),
        "MAP_FOUR_ISLAND_ICEFALL_CAVE_B1F" => new("map.four-island-icefall-cave-b1f"),
        "MAP_FOUR_ISLAND_ICEFALL_CAVE_BACK" => new("map.four-island-icefall-cave-back"),
        "MAP_FOUR_ISLAND_ICEFALL_CAVE_ENTRANCE" => new("map.four-island-icefall-cave-entrance"),
        "MAP_FOUR_ISLAND_LORELEIS_HOUSE" => new("map.four-island-loreleis-house"),
        "MAP_FOUR_ISLAND_MART" => new("map.four-island-mart"),
        "MAP_FOUR_ISLAND_POKEMON_CENTER_1F" => new("map.four-island-pokemon-center-1f"),
        "MAP_FOUR_ISLAND_POKEMON_CENTER_2F" => new("map.four-island-pokemon-center-2f"),
        "MAP_FOUR_ISLAND_POKEMON_DAY_CARE" => new("map.four-island-pokemon-day-care"),
        _ => PretSeviiRubyRegion.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : connections.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown FourIsland connection direction.") }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.CreateConnectedCells(), edges);
    }
    public static PretFourIslandRegion Load(string root)
    {
        var source = FourIslandSource.Region(root);

        var directory = source.Directory;
        var catalog = source.Document("catalog.json");

        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original Four Island and Icefall Cave source maps; application admission remains separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!;
            var relative = spec.GetProperty("mapPath").GetString()!;
            var runtime = PretRuntimeMapData.Decode(source.Read(relative), stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == stableId, "Map symbol does not match its ID.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, Safe(directory, relative), PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
            maps.Add(new(stableId), map);
        }
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "FourIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
