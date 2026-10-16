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

    public string GeneratedDirectory { get; }
    private readonly ImmutableDictionary<MapId, ImmutableHashSet<(int X, int Y)>> _upperCells;

    private PretRoute16FlyHouse(Source source, PretWorldMap house, ImmutableDictionary<string, int> constants,
        ImmutableDictionary<MapId, PretRuntimeMapData> retained, PretRoute16FlyTrainerDocument trainers)
    {
        NewMaps = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house); Constants = constants;
        GeneratedDirectory = source.Directory;

        Trainers = trainers;
        _upperCells = source.Document("passage.json").GetProperty("upperFootAdmissions").EnumerateArray().ToImmutableDictionary(
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



    public static PretRoute16FlyHouse Load(PretCurrentWorld world, string? dependencyRoot = null)
    {
        var projectRoot = world.ProjectRoot;
        dependencyRoot ??= projectRoot; var source = new Source(projectRoot);
        var house = world.Maps[HouseMapId];
        Need(house.Width == 11 && house.Height == 9 && house.FieldWarps.Length == 3 &&
            house.Events.GetProperty("object_events").GetArrayLength() == 2 && house.Events.GetProperty("coord_events").GetArrayLength() == 0 &&
            house.Events.GetProperty("bg_events").GetArrayLength() == 0 && house.Events.GetProperty("layout").GetString() == "LAYOUT_HOUSE1",
            "Original Fly house geometry/event cohort differs.");
        var retained = new[] { RouteMapId, GateMapId }.ToImmutableDictionary(id => id, id => world.RuntimeMaps.Maps[id]);

        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["ITEM_HM02"] == 340 && constants["FLAG_GOT_HM02"] == 0x238, "Original HM02 gift constants differ.");
        var trainers = ContentJson.ParseStrict<PretRoute16FlyTrainerDocument>(Encoding.UTF8.GetString(source.Read("trainers.json")), SharedTrainerCatalog.Options(dependencyRoot));
        Game.Domain.Trainers.Route16FlyTrainerPartyFactory.ValidateCatalog(new(
            trainers.Trainers.ToImmutableDictionary(trainer => trainer.Id),
            trainers.Battles.ToImmutableDictionary(battle => battle.ScriptEntry, StringComparer.Ordinal)));
        return new(source, house, constants, retained, trainers);
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
