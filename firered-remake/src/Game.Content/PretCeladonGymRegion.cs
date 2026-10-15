using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated Celadon Gym source map, trainers, Cut trees and badge/TM19 script contracts.</summary>
public sealed class PretCeladonGymRegion
{
    public const string Profile = "firered-celadon-gym-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement CutTrees => Dependencies.GetProperty("cutTrees");
    public JsonElement BadgeRewards => Dependencies.GetProperty("badgeRewards");
    public JsonElement ScriptInventory { get; }
    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalCeladonEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretCeladonGymRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, JsonElement dependencies, JsonElement catalog, JsonElement scriptsInventory,
        JsonElement npcSources, JsonElement externalCeladonEvents)
    {
        GeneratedDirectory = directory; NewMaps = maps; Scripts = scripts; TextSources = texts;
        Constants = constants; SourceFiles = files; Dependencies = dependencies; Catalog = catalog;
        ScriptInventory = scriptsInventory; NpcSources = npcSources; ExternalCeladonEvents = externalCeladonEvents;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CELADON_CITY_GYM" => new("map.celadon-city-gym"),
        _ => PretCeladonApproachRegion.MapIdFromSourceSymbol(symbol)
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

    public static PretCeladonGymRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/celadon-gym-region");
        var rawDirectory = Path.Combine(project, PretSourcePaths.FireRedRawDirectory);
        byte[] ReadPinned(string root,string path)
        {
            return File.ReadAllBytes(SafeFile(root, path));
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project,"scripts/celadon-gym-region.lock.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Celadon Gym manifest identity differs.");
        var source = sourceLock.RootElement;
        Require(source.GetProperty("schemaVersion").GetInt32() == 1 && source.GetProperty("gameVersion").GetString() == "FIRERED" &&
            source.GetProperty("revision").GetInt32() == 0 && source.GetProperty("maps")[0].GetString() == "CeladonCity_Gym" &&
            !source.GetProperty("completeGame").GetBoolean(), "Celadon Gym source lock identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "celadon-gym-region/" + path)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate Celadon Gym output.");
        }
        using var catalog = JsonDocument.Parse(outputs["celadon-gym-catalog.json"]);
        Require(catalog.RootElement.GetProperty("schemaVersion").GetInt32() == 1 ,
            "Celadon Gym source inventory differs from fixed lock.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        byte[] Raw(string path) => File.ReadAllBytes(SafeFile(rawDirectory, path));
        foreach (var entry in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;

            files.Add(new(path,$"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Celadon Gym source profile; runtime certification is separate.", Profile, inventory);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("directory").GetString()!;
            if (runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!), out var compact))
            {


                var runtimeMap = PretWorldMap.FromRuntime(compact, metadata, SafeFile(directory, relative),
                    SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate Celadon Gym runtime map.");
                continue;
            }
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        Require(            scriptInventory.RootElement.GetProperty("blocks").GetArrayLength() == 81 &&
            scriptInventory.RootElement.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Celadon Gym script reference closure is incomplete.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var property in scriptInventory.RootElement.GetProperty("filePreambles").EnumerateObject())
        {
            var path = property.Name;
            var text = Encoding.UTF8.GetString(Raw(path));
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Require(symbol.Length > 0, "Celadon Gym script has no original source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret,path,symbol,"pret/pokefirered",PretWorldMap.SourceCommit)));
            }
        }
        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_TEMP_12"] == 18 && constants["FLAG_TEMP_13"] == 19 && constants["FLAG_TEMP_14"] == 20 &&
            constants["FLAG_BADGE04_GET"] == 0x823 && constants["FLAG_DEFEATED_ERIKA"] == 0x4B3 &&
            constants["FLAG_GOT_TM19_FROM_ERIKA"] == 0x293 && constants["ITEM_TM19"] == 307,
            "Celadon Gym badge/Cut/reward constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        var dependency = dependencies.RootElement;
        Require(dependency.GetProperty("schemaVersion").GetInt32() == 1 && !dependency.GetProperty("runtimeIntegrated").GetBoolean() &&
            dependency.GetProperty("trainers").GetArrayLength() == 8 && dependency.GetProperty("cutTrees").GetArrayLength() == 3 &&
            dependency.GetProperty("badgeRewards").GetProperty("tm").GetString() == "ITEM_TM19" &&
            dependency.GetProperty("entry").GetProperty("sourceWarpId").GetInt32() == 6 &&
            dependency.GetProperty("entry").GetProperty("destinationWarpId").GetInt32() == 1,
            "Celadon Gym dependency scope differs.");
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var cityEvents = JsonDocument.Parse(Raw("data/maps/CeladonCity/map.json"));
        Require(npcSources.RootElement.GetProperty("schemaVersion").GetInt32() == 1 &&
            npcSources.RootElement.GetProperty("graphics").GetArrayLength() == 7, "Celadon Gym NPC inventory differs.");
        var gymEvents = maps[new MapId("map.celadon-city-gym")].Events;
        var cityEntry = cityEvents.RootElement.GetProperty("warp_events")[6];
        Require(cityEntry.GetProperty("dest_map").GetString() == "MAP_CELADON_CITY_GYM" && cityEntry.GetProperty("dest_warp_id").GetString() == "1" &&
            JsonElement.DeepEquals(cityEntry, dependency.GetProperty("entry").GetProperty("source")) &&
            JsonElement.DeepEquals(gymEvents.GetProperty("warp_events")[1], dependency.GetProperty("entry").GetProperty("destination")),
            "Celadon Gym city entry differs from original source.");
        foreach (var tree in dependency.GetProperty("cutTrees").EnumerateArray())
            Require(JsonElement.DeepEquals(tree.GetProperty("object"), gymEvents.GetProperty("object_events")[tree.GetProperty("localId").GetInt32() - 1]),
                "Celadon Gym Cut object differs from source.");
        foreach (var trainer in dependency.GetProperty("trainers").EnumerateArray())
            Require(JsonElement.DeepEquals(trainer.GetProperty("object"), gymEvents.GetProperty("object_events")[trainer.GetProperty("localId").GetInt32() - 1]),
                "Celadon Gym trainer object differs from source.");
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(),
            inventory, dependencies.RootElement.Clone(), catalog.RootElement.Clone(), scriptInventory.RootElement.Clone(),
            npcSources.RootElement.Clone(), cityEvents.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Celadon Gym path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Celadon Gym directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Celadon Gym file symlink is unsupported: " + path);
        }
        return result;
    }
}
