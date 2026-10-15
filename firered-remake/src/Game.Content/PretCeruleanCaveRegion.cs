using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated resources for three cave maps. This is not a game profile or runtime admission.</summary>
public sealed class PretCeruleanCaveRegion
{
    public const string ResourceId = "firered-cerulean-cave-resources-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }

    public JsonElement NpcSources { get; }
    public JsonElement Encounters { get; }
    public JsonElement Items { get; }
    public JsonElement Reachability { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretCeruleanCaveRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Constants = constants;
        SourceFiles = ImmutableArray<PretWorldSourceFile>.Empty; GeneratedDirectory = source.Directory;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        Encounters = source.Document("encounters.json"); Items = source.Document("items.json");
        Reachability = source.Document("reachability.json");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CERULEAN_CAVE_1F" => new("map.cerulean-cave-1f"),
        "MAP_CERULEAN_CAVE_2F" => new("map.cerulean-cave-2f"),
        "MAP_CERULEAN_CAVE_B1F" => new("map.cerulean-cave-b1f"),
        "MAP_CERULEAN_CITY" => new("map.cerulean-city"),
        _ => throw new InvalidDataException("Unknown Cerulean Cave map symbol: " + symbol)
    };

    public static PretCeruleanCaveRegion Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot,"cerulean-cave");
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId ,
            "Cerulean Cave catalog source identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original cave resources; current-game admission is separate.", ResourceId, ImmutableArray<PretWorldSourceFile>.Empty);

        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!,relative = spec.GetProperty("directory").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need(path == relative + "/runtime-map.json" ,
                "Cerulean Cave compact resource binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == 40 && runtime.Height == 23,
                "Cerulean Cave map identity/dimensions differ.");
            maps.Add(new(id), PretWorldMap.FromRuntime(runtime, metadata,
                PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretOverworldTextures.Resolve(source.Directory, "../shared-overworld/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(maps.Keys.ToHashSet().SetEquals(new[] { new MapId("map.cerulean-cave-1f"),new MapId("map.cerulean-cave-2f"),new MapId("map.cerulean-cave-b1f") }) , "Cerulean Cave map/warp collection differs.");

        var files = ImmutableArray<PretWorldSourceFile>.Empty.ToDictionary(f => f.Path, StringComparer.Ordinal);
        
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
