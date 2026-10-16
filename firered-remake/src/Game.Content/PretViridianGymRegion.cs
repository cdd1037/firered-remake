using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

/// <summary>One exact Viridian Gym map with the reused city door gate. Application admission remains separate.</summary>
public sealed partial class PretViridianGymRegion
{
    public const string Profile = "firered-viridian-gym-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps => NewMaps;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement EntranceGate => Contracts.GetProperty("entrance");
    public JsonElement TrainerOccurrences => Contracts.GetProperty("trainerOccurrences");
    public JsonElement HiddenItems => Contracts.GetProperty("hiddenItems");
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private PretViridianGymRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        GeneratedDirectory = source.Directory; NewMaps = maps; Constants = constants;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        InitializeField(source);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_VIRIDIAN_CITY_GYM" => new("map.viridian-city-gym"),
        "MAP_VIRIDIAN_CITY" => new("map.viridian-city"),
        _ => PretFirstSeviiRegion.MapIdFromSourceSymbol(symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == new MapId("map.viridian-city-gym") && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Viridian Gym has no edge connections.");
        return new(map.Id, map.Width, map.Height, map.CreateConnectedCells(), []);
    }
    private static SilphSourcePackage ViridianGymSource(string root) => new(root,"maps/regions/viridian-gym");
    public static PretViridianGymRegion Load(string root)
    {
        var source = ViridianGymSource(root);

        var directory = source.Directory;
        var catalog = source.Document("catalog.json");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original Viridian Gym and city-entry source; application admission remains separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!; var relative = spec.GetProperty("mapPath").GetString()!;
            var runtimeEntry = spec;
            Need(runtimeEntry.GetProperty("path").GetString() == relative , "ViridianGym runtime certificate differs.");
            var payload = source.Read(relative);
            var runtime = PretRuntimeMapData.Decode(payload, stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == stableId, "Map symbol differs from its ID.");
            var mapDirectory = Safe(directory, relative);
            var playerTexture = PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png");
            maps.Add(new(stableId), PretWorldMap.FromRuntime(runtime, metadata, mapDirectory, playerTexture, MapIdFromSourceSymbol));
        }
        Need(maps.Count == 1 && maps.Values.Sum(m => m.Width * m.Height) == 480 && maps.Values.Sum(m => m.FieldWarps.Length) == 3,
            "ViridianGym source map collection differs.");
        foreach (var map in maps.Values) foreach (var warp in map.FieldWarps)
            if (maps.TryGetValue(warp.DestinationMapId, out var target)) Need(warp.DestinationWarpId < target.FieldWarps.Length, "ViridianGym target warp index differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
    private static string Safe(string root, string relative) => PretRuntimeMapValidation.SafeFile(root, relative);
}
