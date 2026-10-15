using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>
/// Authenticated Routes 24/25 and Sea Cottage, including Bill scene source and animation art. This independent content profile registers
/// no executable handlers and does not certify battles, services, native gameplay or audio.
/// </summary>
public sealed class PretBillRegion
{
    public const string Profile = "firered-bill-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement CeruleanNorthConnection => Dependencies.GetProperty("ceruleanNorthConnection");
    public JsonElement BillPcPreviews => Dependencies.GetProperty("billPcPreviews");
    public JsonElement Encounters { get; }
    public JsonElement Animations { get; }

    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalCeruleanEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretBillRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        JsonElement dependencies, JsonElement catalog, JsonElement npcSources, JsonElement externalCeruleanEvents, JsonElement encounters, JsonElement animations)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; Dependencies = dependencies; Catalog = catalog;
        Encounters = encounters; Animations = animations;
        NpcSources = npcSources; ExternalCeruleanEvents = externalCeruleanEvents;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE24" => new("map.route-24"),
        "MAP_ROUTE25" => new("map.route-25"),
        "MAP_ROUTE25_SEA_COTTAGE" => new("map.route-25-sea-cottage"),
        _ => PretCeruleanRegion.MapIdFromSourceSymbol(symbol)
    };

    public static Game.Domain.World.ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var connections = map.Events.GetProperty("connections");
        var values = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<Game.Domain.World.ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new Game.Domain.World.ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Game.Domain.World.Direction.North, "down" => Game.Domain.World.Direction.South,
                "left" => Game.Domain.World.Direction.West, "right" => Game.Domain.World.Direction.East,
                _ => throw new InvalidDataException("Unsupported connection direction.")
            }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new Game.Domain.World.ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), values);
    }

    public static PretBillRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/bill");

        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));

        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1, "Bill manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "scripts.json") continue;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "maps/regions/bill/" + path)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate bill output.");
        }
        using var catalog = JsonDocument.Parse(outputs["bill-catalog.json"]);

        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Routes 24/25 and Sea Cottage; runtime certification is separate.", Profile);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("mapPath").GetString()!;
            if (runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!), out var compact))
            {

                var runtimeMap = PretWorldMap.FromRuntime(compact, metadata, SafeFile(directory, relative),
                    PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate Bill runtime map.");
                continue;
            }
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_GOT_SS_TICKET"] > 0 && constants["TRAINER_TEAM_ROCKET_GRUNT_6"] == 356 &&
            constants["LOCALID_BILL_HUMAN"] == 1 && constants["LOCALID_BILL_CLEFAIRY"] == 2,
            "Bill progression/local actor constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(dependencies.RootElement.GetProperty("trainers").GetArrayLength() == 19 &&
            dependencies.RootElement.GetProperty("billPcPreviews").GetArrayLength() == 4 &&
            !dependencies.RootElement.GetProperty("ceruleanNorthConnection").GetProperty("enabled").GetBoolean(),
            "Bill dependency scope differs.");
        using var encounters = JsonDocument.Parse(outputs["encounters.json"]);
        using var animations = JsonDocument.Parse(outputs["animations.json"]);
        Require(encounters.RootElement.GetProperty("gameVersion").GetString() == "FIRERED" &&
            animations.RootElement.GetProperty("metatiles").GetArrayLength() == 10 ,
            "Bill encounter or animation scope differs.");
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var cityEvents = JsonDocument.Parse(GeneratedRegionEvents.Read(project, "data/maps/CeruleanCity/map.json"));
        return new(directory, maps.ToImmutable(), constants.ToImmutable(), dependencies.RootElement.Clone(), catalog.RootElement.Clone(), npcSources.RootElement.Clone(), cityEvents.RootElement.Clone(), encounters.RootElement.Clone(), animations.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe bill path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Bill directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Bill file symlink is unsupported: " + path);
        }
        return result;
    }
}
