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
    public JsonElement Contracts { get; }
    public JsonElement IceFallPairs => Contracts.GetProperty("iceFallPairs");
    public FieldMilestoneCollection StickerMilestones { get; }
    public ImmutableDictionary<string, FieldMilestoneCollection> FieldMilestones { get; }
    public FieldStatFlagUnlocks DollCollection { get; }
    public string GeneratedDirectory { get; }
    private PretFourIslandRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Contracts = source.Document("contracts.json");

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
    public static PretFourIslandRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = FourIslandSource.Region(root);

        var directory = source.Directory;
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "FourIsland target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);

        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
