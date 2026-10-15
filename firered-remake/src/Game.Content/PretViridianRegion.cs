using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Authenticated source increment. Content availability does not certify runtime handlers.</summary>
public sealed class PretViridianRegion
{
    public const string Profile = "firered-viridian-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, string> SourceUiTexts { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretViridianRegion(string directory,ImmutableDictionary<MapId, PretWorldMap> maps,        ImmutableDictionary<string, int> constants,ImmutableDictionary<string, string> sourceUiTexts)
    { GeneratedDirectory = directory; NewMaps = maps; Constants = constants; SourceUiTexts = sourceUiTexts; }

    public static MapId MapIdFromSourceSymbol(string symbol)
    {
        var existing = PretWorldCatalog.Specifications.FirstOrDefault(x => x.SourceMapSymbol == symbol);
        if (existing is not null) return new(existing.StableId);
        return new(symbol switch
        {
            "MAP_ROUTE1" => "map.route-1", "MAP_VIRIDIAN_CITY" => "map.viridian-city",
            "MAP_VIRIDIAN_CITY_MART" => "map.viridian-mart", "MAP_ROUTE2" => "map.route-2",
            "MAP_ROUTE22" => "map.route-22", "MAP_ROUTE21_NORTH" => "map.route-21-north",
            "MAP_VIRIDIAN_CITY_GYM" => "map.viridian-gym", "MAP_VIRIDIAN_CITY_HOUSE" => "map.viridian-house",
            "MAP_VIRIDIAN_CITY_POKEMON_CENTER_1F" => "map.viridian-pokemon-center-1f",
            "MAP_VIRIDIAN_CITY_POKEMON_CENTER_2F" => "map.viridian-pokemon-center-2f",
            "MAP_VIRIDIAN_CITY_SCHOOL" => "map.viridian-school",
            _ => throw new InvalidDataException("Unknown region source map symbol: " + symbol)
        });
    }

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var values = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unsupported connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), values);
    }

    public static PretViridianRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/viridian-region");
        
        static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
        static string SafeFile(string root, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
                throw new InvalidDataException("Unsafe region path: " + path);
            var result = root;
            for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
                if (cursor.LinkTarget is not null) throw new InvalidDataException("Region directory symlink is unsupported.");
            foreach (var segment in path.Split('/'))
            {
                result = Path.Combine(result, segment);
                if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                    throw new InvalidDataException("Region file symlink is unsupported: " + path);
            }
            return result;
        }
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));

        var m = manifest.RootElement;
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            if(runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps,"viridian-region/"+path))continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate region output.");
        }
        using var catalog = JsonDocument.Parse(outputs["viridian-catalog.json"]);
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();

        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Source material remains local; no asset distribution rights are inferred.", "Authenticated Viridian region increment; runtime support is separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            if (spec.GetProperty("role").GetString() != "newMap") continue;
            var relative = spec.GetProperty("directory").GetString()!;
            if(runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!),out var compact) )
            {
                var runtimeMap=PretWorldMap.FromRuntime(compact,metadata,SafeFile(directory,relative),Path.Combine(project,"content/generated/pallet/player.png"),MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id,runtimeMap),"Duplicate runtime map.");continue;
            }
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, Path.Combine(project, "content/generated/pallet/player.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }
        Require(maps.Count == 3, "Region must add exactly three source maps.");

        var constants = PretSharedConstants.ForPackage(directory);
        var uiTexts = ContentJson.ParseStrict<ImmutableDictionary<string,string>>(
            File.ReadAllText(Path.Combine(directory, "ui-texts.json")), ContentJson.CreateOptions());
        return new(directory,maps.ToImmutable(),constants,uiTexts);
    }
}
