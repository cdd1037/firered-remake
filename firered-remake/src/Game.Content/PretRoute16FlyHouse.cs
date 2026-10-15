using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed record PretRoute16FlyTrainerDocument(ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

/// <summary>One original house; existing Route16 and the divided gate retain their own identities. Runtime admission is separate.</summary>
public sealed class PretRoute16FlyHouse
{
    public const string ResourceId = "firered-route16-fly-house-v1";
    public static MapId HouseMapId { get; } = new("map.route-16-house");
    public static MapId RouteMapId { get; } = new("map.route-16");
    public static MapId GateMapId { get; } = new("map.route-16-north-entrance-1f");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public PretRoute16FlyTrainerDocument Trainers { get; }
    public JsonElement Contracts { get; }

    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private readonly ImmutableDictionary<MapId, ImmutableHashSet<(int X, int Y)>> _upperCells;
    private readonly ImmutableDictionary<MapId, PretRuntimeMapData> _retained;

    private PretRoute16FlyHouse(Source source, PretWorldMap house, ImmutableDictionary<string, int> constants,
        ImmutableDictionary<MapId, PretRuntimeMapData> retained, PretRoute16FlyTrainerDocument trainers)
    {
        NewMaps = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house); Constants = constants;
        GeneratedDirectory = source.Directory; Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        Trainers = trainers; _retained = retained;
        _upperCells = Contracts.GetProperty("upperFootAdmissions").EnumerateArray().ToImmutableDictionary(
            r => new MapId(r.GetProperty("map").GetString()!),
            r => r.GetProperty("cells").EnumerateArray().Select(c => (c[0].GetInt32(), c[1].GetInt32())).ToImmutableHashSet());
        Need(_upperCells[RouteMapId].Count == 178 && _upperCells[GateMapId].Count == 44, "Upper passage cell count differs.");
        foreach (var (id, cells) in _upperCells)
        {
            var original = retained[id]; int width = original.Width;
            var expected = original.ExpandCells().Where(c => c.Collision == 0 && c.Elevation == 3 &&
                (id == RouteMapId ? (c.Y is >= 5 and <= 10 && c.X is >= 2 and <= 41) || (c.X, c.Y) == (41, 11)
                                 : c.Y is >= 2 and <= 5) &&
                c.BehaviorName is "MB_NORMAL" or "MB_TALL_GRASS" or "MB_WEST_ARROW_WARP" or "MB_EAST_ARROW_WARP")
                .Select(c => (c.X, c.Y)).ToHashSet();
            Need(cells.SetEquals(expected) && width > 0, "Upper passage must retain exact source collision/elevation.");
        }
    }

    /// <summary>Additive preflight only. Original Cut, actors, trainer sight, encounters and collision remain mandatory.</summary>
    public bool AllowsAdditionalFootDestination(MapId map, int x, int y) =>
        _upperCells.TryGetValue(map, out var cells) && cells.Contains((x, y));

    /// <summary>Only the original north door chain; lower cycling doors and stairs remain excluded.</summary>
    public bool AllowsWarp(MapId map, int sourceWarpId) => map == HouseMapId ? sourceWarpId is >= 0 and <= 2 :
        map == RouteMapId ? sourceWarpId is >= 0 and <= 2 : map == GateMapId && sourceWarpId is 0 or 1;

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == HouseMapId && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Unexpected Fly house connection.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>Call before composing. This validates the retained instances without replacing or republishing them.</summary>
    public void ValidateReusedMaps(IReadOnlyDictionary<MapId, PretWorldMap> maps)
    {
        foreach (var row in Contracts.GetProperty("retainedMaps").EnumerateArray())
        {
            var id = new MapId(row.GetProperty("stableId").GetString()!);
            Need(maps.TryGetValue(id, out var map), "Missing retained north-passage map.");
            var original = _retained[id]; var events = original.Events;
            Need(map!.SourceMapSymbol == row.GetProperty("sourceMap").GetString() &&
                map.Width == original.Width && map.Height == original.Height &&
                JsonElement.DeepEquals(map.Events, events), "Retained north-passage identity/events differ.");
            foreach (var c in original.ExpandCells())
            {
                var cell = map.GetSourceCell(c.X, c.Y);
                Need(cell is not null && cell.RawBlock == c.RawBlock &&
                    cell.RawAttributes == c.RawAttributes, "Retained north-passage geometry differs.");
            }
        }
    }

    public static PretRoute16FlyHouse Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot; var source = new Source(projectRoot);
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("maps").GetArrayLength() == 1 , "Fly house catalog differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "One house; north path admission remains separate.", ResourceId);
        var spec = catalog.GetProperty("maps")[0]; string path = spec.GetProperty("path").GetString()!;
        Need(path == "route-16-house.json" && spec.GetProperty("stableId").GetString() == HouseMapId.Value ,
            "Fly house compact map identity differs.");
        var house = LoadHouse(source, metadata, spec, dependencyRoot);
        Need(house.Width == 11 && house.Height == 9 && house.FieldWarps.Length == 3 &&
            house.Events.GetProperty("object_events").GetArrayLength() == 2 && house.Events.GetProperty("coord_events").GetArrayLength() == 0 &&
            house.Events.GetProperty("bg_events").GetArrayLength() == 0 && house.Events.GetProperty("layout").GetString() == "LAYOUT_HOUSE1",
            "Original Fly house geometry/event cohort differs.");
        var retained = ImmutableDictionary.CreateBuilder<MapId, PretRuntimeMapData>();
        foreach (var anchor in source.Document("contracts.json").GetProperty("retainedMaps").EnumerateArray())
        {
            var original = PretRuntimeMapData.Decode(SafariSource.Read(dependencyRoot, anchor.GetProperty("resourcePath").GetString()!),
                anchor.GetProperty("stableId").GetString()!, PretSnorlaxRegion.MapIdFromSourceSymbol);
            retained.Add(new(anchor.GetProperty("stableId").GetString()!), original);
        }
        Need(retained.Keys.ToHashSet().SetEquals([RouteMapId, GateMapId]), "North-passage retained map cohort differs.");
        ValidateTopology(source.Document("contracts.json"), house, retained);
        var npcs = source.Document("npc-source.json").GetProperty("graphics");
        Need(npcs.GetArrayLength() == 3, "Fly house actor textures differ.");

        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["ITEM_HM02"] == 340 && constants["FLAG_GOT_HM02"] == 0x238, "Original HM02 gift constants differ.");
        var trainers = ContentJson.ParseStrict<PretRoute16FlyTrainerDocument>(Encoding.UTF8.GetString(source.Read("trainers.json")), SharedTrainerCatalog.Options(dependencyRoot));
        Game.Domain.Trainers.Route16FlyTrainerPartyFactory.ValidateCatalog(new(
            trainers.Trainers.ToImmutableDictionary(trainer => trainer.Id),
            trainers.Battles.ToImmutableDictionary(battle => battle.ScriptEntry, StringComparer.Ordinal)));
        return new(source, house, constants, retained.ToImmutable(), trainers);
    }

    // This house reuses the regional source-symbol mapping for its exterior warp.
    private static PretWorldMap LoadHouse(Source source, PretWorldMetadata metadata, JsonElement spec, string dependencyRoot)
    {
        var runtime = PretRuntimeMapData.Decode(source.Read("route-16-house.json"), HouseMapId.Value, PretSnorlaxRegion.MapIdFromSourceSymbol);
        Need((runtime.Width, runtime.Height, runtime.BorderWidth, runtime.BorderHeight) == (11, 9, 2, 2),
            "Fly house geometry differs.");
        return PretWorldMap.FromRuntime(runtime, metadata, Path.Combine(source.Directory, "route-16-house"),
            PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/sprites/overworld/red-normal.png"), PretSnorlaxRegion.MapIdFromSourceSymbol);
    }

    private static void ValidateTopology(JsonElement contract, PretWorldMap house, ImmutableDictionary<MapId, PretRuntimeMapData>.Builder retained)
    {
        JsonElement Events(string symbol) => symbol == "MAP_ROUTE16_HOUSE" ? house.Events : retained[PretSnorlaxRegion.MapIdFromSourceSymbol(symbol)].Events;
        Need(contract.GetProperty("topology").GetArrayLength() == 8, "Fly house source topology scope differs.");
        foreach (var edge in contract.GetProperty("topology").EnumerateArray())
        {
            string symbol = edge.GetProperty("sourceMap").GetString()!; int index = edge.GetProperty("sourceIndex").GetInt32();
            var source = Events(symbol).GetProperty("warp_events")[index];
            string destination = source.GetProperty("dest_map").GetString()!; int target = int.Parse(source.GetProperty("dest_warp_id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            var back = Events(destination).GetProperty("warp_events")[target];
            Need(JsonElement.DeepEquals(source, edge.GetProperty("source")) && JsonElement.DeepEquals(back, edge.GetProperty("destination")) &&
                back.GetProperty("dest_map").GetString() == symbol && (back.GetProperty("dest_warp_id").GetString() == index.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                    symbol == "MAP_ROUTE16_HOUSE" && target == 0 && back.GetProperty("dest_warp_id").GetString() == "1"), "Original north-passage warp link differs.");
        }
    }

    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);

    // Load only generated map and service data.
    private sealed class Source
    {
        internal string Directory { get; }
        internal Source(string root)
        {
            Directory = Path.GetFullPath(Path.Combine(root, "content/generated/maps/regions/route16-fly-house"));
        }
        internal byte[] Read(string path) => SafariSource.Read(Directory, path);
        internal JsonElement Document(string path) => SilphSourcePackage.Json(Read(path));
    }
}
