using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated source catalog only. Loading never enables a service or modifies global campaign composition.</summary>
public sealed class PretFuchsiaRouteRegion
{
    public const string Profile = "firered-fuchsia-route-region-import-v1";
    public static readonly MapId CenterMapId = new("map.fuchsia-city-pokemon-center-1f");
    public const string HealingLocation = "HEAL_LOCATION_FUCHSIA_CITY";
    /// <summary>Six complete authenticated maps. Loading alone admits no field action or service.</summary>
    public ImmutableDictionary<MapId, PretWorldMap> SourceMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps => SourceMaps;
    public ImmutableArray<MapId> ReusedMapIds => [new("map.route-12")];
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Catalog { get; }

    public JsonElement ObjectTemplates { get; }
    public JsonElement NpcSources { get; }
    public JsonElement Availability { get; }
    public ImmutableDictionary<MapId, JsonElement> ReusedEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretFuchsiaRouteRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants, ImmutableArray<PretWorldSourceFile> sources,
        JsonElement catalog, JsonElement objects, JsonElement npcs,
        JsonElement availability, ImmutableDictionary<MapId, JsonElement> reused)
    {
        GeneratedDirectory = directory; SourceMaps = maps; Constants = constants; SourceFiles = sources;
        Catalog = catalog; ObjectTemplates = objects;
        NpcSources = npcs; Availability = availability; ReusedEvents = reused;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE13" => new("map.route-13"),
        "MAP_ROUTE14" => new("map.route-14"),
        "MAP_ROUTE15" => new("map.route-15"),
        "MAP_ROUTE15_WEST_ENTRANCE_1F" => new("map.route-15-west-entrance-1f"),
        "MAP_ROUTE15_WEST_ENTRANCE_2F" => new("map.route-15-west-entrance-2f"),
        "MAP_FUCHSIA_CITY" => new("map.fuchsia-city"),
        "MAP_FUCHSIA_CITY_POKEMON_CENTER_1F" => new("map.fuchsia-city-pokemon-center-1f"),
        "MAP_FUCHSIA_CITY_POKEMON_CENTER_2F" => new("map.fuchsia-city-pokemon-center-2f"),
        "MAP_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE" => new("map.fuchsia-city-safari-zone-entrance"),
        "MAP_FUCHSIA_CITY_WARDENS_HOUSE" => new("map.fuchsia-city-wardens-house"),
        "MAP_FUCHSIA_CITY_MART" => new("map.fuchsia-city-mart"),
        "MAP_FUCHSIA_CITY_SAFARI_ZONE_OFFICE" => new("map.fuchsia-city-safari-zone-office"),
        "MAP_FUCHSIA_CITY_GYM" => new("map.fuchsia-city-gym"),
        "MAP_FUCHSIA_CITY_HOUSE1" => new("map.fuchsia-city-house-1"),
        "MAP_FUCHSIA_CITY_HOUSE2" => new("map.fuchsia-city-house-2"),
        "MAP_FUCHSIA_CITY_HOUSE3" => new("map.fuchsia-city-house-3"),
        "MAP_ROUTE18" => new("map.route-18"),
        "MAP_ROUTE19" => new("map.route-19"),
        _ => PretSnorlaxRegion.MapIdFromSourceSymbol(symbol)
    };

    /// <summary>Keep source connections, including unavailable neighbors, so callers can reject before mutation.</summary>
    public static Game.Domain.World.ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<Game.Domain.World.ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new Game.Domain.World.ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Game.Domain.World.Direction.North, "down" => Game.Domain.World.Direction.South,
                "left" => Game.Domain.World.Direction.West, "right" => Game.Domain.World.Direction.East,
                _ => throw new InvalidDataException("Unknown Fuchsia route connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new Game.Domain.World.ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }

    /// <summary>Only a source-package boundary check; not an admission of interactions, trainer approaches or encounters.</summary>
    public bool ContainsDestination(MapId destination) => SourceMaps.ContainsKey(destination) || ReusedMapIds.Contains(destination);
    public bool AllowsNewMapWarp(MapId source, int index)
    {
        if (!SourceMaps.TryGetValue(source, out var map) || index < 0 || index >= map.FieldWarps.Length) return false;
        return ContainsDestination(map.FieldWarps[index].DestinationMapId);
    }
    public JsonElement Route12Events => ReusedEvents[new("map.route-12")];

    /// <summary>Checks the existing Route12 instance before a later composition; never replaces them.</summary>
    public void ValidateReusedMaps(IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        foreach (var id in ReusedMapIds)
        {
            Need(existing.ContainsKey(id), "Existing Route12 is missing: " + id.Value);
        }
    }

    /// <summary>Maps retain packed blocks and expand source cells only on demand.</summary>
    public static PretFuchsiaRouteRegion Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/fuchsia-route-region");
        
        byte[] Pinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(Safe(root, path));
            return bytes;
        }
        using var manifestDocument = JsonDocument.Parse(Pinned(directory,"manifest.json"));
        var manifest = manifestDocument.RootElement;
        Need(manifest.GetProperty("schemaVersion").GetInt32() == 1 && manifest.GetProperty("unresolvedLabelCount").GetInt32() == 0,
            "Fuchsia route manifest scope differs.");
        var outputEntries = manifest.GetProperty("outputs").EnumerateArray().ToDictionary(x => x.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (path, entry) in outputEntries)
        {
            if (path == "scripts.json") continue;
            _ = Safe(directory, path);
            outputs.Add(path, Pinned(directory,path));
        }
        JsonElement Document(string path) { using var doc = JsonDocument.Parse(outputs[path]); return doc.RootElement.Clone(); }
        var catalog = Document("fuchsia-route-region-catalog.json");
        Need(            !catalog.GetProperty("runtimeIntegrated").GetBoolean(), "Fuchsia source inventory/availability differs.");
        
        var sources = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        
        var files = sources.ToImmutable(); var byPath = files.ToDictionary(s => s.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Six source maps; unchanged Route12 reuse; services require explicit admission.", Profile, files);
        var runtimeCatalog = Document("runtime-catalog.json");
        Need(runtimeCatalog.GetProperty("converterVersion").GetString() == PretRuntimeMapCatalog.ConverterVersion , "Fuchsia runtime converter differs.");
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            var stableId = spec.GetProperty("stableId").GetString()!;
            var relative = spec.GetProperty("directory").GetString()!;
            var runtime = PretRuntimeMapData.Decode(outputs[relative + "/runtime-map.json"], stableId, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == stableId, "Map symbol does not match its ID.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, Safe(directory, relative), PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
            maps.Add(new(stableId), map);
        }
        foreach (var map in maps.Values)
            foreach (var warp in map.FieldWarps)
                if (maps.TryGetValue(warp.DestinationMapId, out var target))
                    Need(warp.DestinationWarpId < target.Events.GetProperty("warp_events").GetArrayLength(), "Loaded warp index differs.");
        
        var constants = PretSharedConstants.ForPackage(directory);
        Need(constants[HealingLocation] == 8 && constants["LOCALID_FUCHSIA_NURSE"] == 1 && constants["LOCALID_ROUTE14_BORDER_TREE"] == 13 && constants["SPECIES_KABUTO"] == 140 && constants["SPECIES_OMANYTE"] == 138, "Fuchsia source constants differ.");
        var reused = ImmutableDictionary.CreateBuilder<MapId, JsonElement>();
        foreach (var spec in catalog.GetProperty("reusedMaps").EnumerateArray())
        {
            Need(!spec.GetProperty("regenerate").GetBoolean(), "Reused source ownership differs.");
            var path = spec.GetProperty("sourceRef").GetProperty("path").GetString()!;
            using var events = JsonDocument.Parse(GeneratedRegionEvents.Read(project, path)); reused.Add(new(spec.GetProperty("stableId").GetString()!), events.RootElement.Clone());
        }
        Need(reused.Count == 1 && !maps.Keys.Any(reused.ContainsKey), "Reused maps must not be regenerated.");
        var availability = Document("availability.json");
        Need(!availability.GetProperty("runtimeIntegrated").GetBoolean() && !availability.GetProperty("completeGame").GetBoolean() && availability.GetProperty("enabledServices").GetArrayLength() == 0, "Source import cannot enable services.");
        var center = maps[CenterMapId];
        var nurse = center.Events.GetProperty("object_events")[0];
        Need(nurse.GetProperty("local_id").GetString() == "LOCALID_FUCHSIA_NURSE" && nurse.GetProperty("x").GetInt32() == 7 &&
            nurse.GetProperty("y").GetInt32() == 2 && nurse.GetProperty("script").GetString() == "FuchsiaCity_PokemonCenter_1F_EventScript_Nurse",
            "Fuchsia nurse declaration differs.");
        var fuchsia = maps[new("map.fuchsia-city")];
        Need(fuchsia.Warps[6].DestinationMapId == CenterMapId && fuchsia.Warps[6].DestinationWarpId == 1 &&
            fuchsia.Warps[6].X == 25 && fuchsia.Warps[6].Y == 31 && center.Warps[1].DestinationWarpId == 6,
            "Fuchsia Center entrance differs.");
        Need(center.GetSourceCell(6, 8)!.BehaviorId == 0 && center.GetSourceCell(7, 8)!.BehaviorId == 0x65 &&
            center.GetSourceCell(8, 8)!.BehaviorId == 0, "Center extra warp records must not become wider exits.");
        var clone = maps[new("map.route-15")].Events.GetProperty("object_events")[13];
        var borderTree = maps[new("map.route-14")].Events.GetProperty("object_events")[12];
        Need(clone.GetProperty("type").GetString() == "clone" && clone.GetProperty("x").GetInt32() == 73 && clone.GetProperty("y").GetInt32() == 7 &&
            clone.GetProperty("target_map").GetString() == "MAP_ROUTE14" && clone.GetProperty("target_local_id").GetString() == "LOCALID_ROUTE14_BORDER_TREE" &&
            borderTree.GetProperty("local_id").GetString() == "LOCALID_ROUTE14_BORDER_TREE" && borderTree.GetProperty("x").GetInt32() == 1 && borderTree.GetProperty("y").GetInt32() == 47,
            "Route15 cross-map tree clone differs.");
        var npcs = Document("npc-source.json");
        var dynamic = npcs.GetProperty("dynamicGraphics");
        Need(dynamic.GetArrayLength() == 1 && dynamic[0].GetProperty("sourceGraphics").GetString() == "OBJ_EVENT_GFX_VAR_0" &&
            dynamic[0].GetProperty("whenSet").GetString() == "OBJ_EVENT_GFX_OMANYTE" && dynamic[0].GetProperty("whenUnset").GetString() == "OBJ_EVENT_GFX_KABUTO" &&
            fuchsia.Events.GetProperty("object_events")[8].GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_VAR_0",
            "Fuchsia fossil graphics branch differs.");
        return new(directory, maps.ToImmutable(), constants, files, catalog,
            Document("objects.json"), npcs, availability, reused.ToImmutable());
    }
    private static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string Safe(string root, string path)
    {
        var result = PretRuntimeMapValidation.SafeFile(root, path);
        for (var cursor = new DirectoryInfo(Path.GetFullPath(root)); cursor is not null; cursor = cursor.Parent)
            Need(cursor.LinkTarget is null, "Fuchsia source directory symlink rejected.");
        var current = Path.GetFullPath(root);
        foreach (var segment in path.Split('/'))
        {
            current = Path.Combine(current, segment);
            Need(new FileInfo(current).LinkTarget is null && new DirectoryInfo(current).LinkTarget is null, "Fuchsia source file symlink rejected.");
        }
        return result;
    }
}
