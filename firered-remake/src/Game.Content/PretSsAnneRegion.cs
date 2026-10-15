using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>
/// Authenticated S.S. Anne maps, actors, ticket, captain, rival, room healing and departure source contracts. This independent content profile registers
/// no executable handlers and does not certify battles, services, native gameplay or audio.
/// </summary>
public sealed class PretSsAnneRegion
{
    public const string Profile = "firered-ssanne-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Dependencies { get; }
    /// <summary>Declarative source metadata only; this class registers no executable handlers.</summary>
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement Ticket => Dependencies.GetProperty("ticket");
    public JsonElement Captain => Dependencies.GetProperty("captain");
    public JsonElement Healer => Dependencies.GetProperty("healer");
    public JsonElement Rival => Dependencies.GetProperty("rival");
    public JsonElement Departure => Dependencies.GetProperty("departure");
    public JsonElement Moves => Dependencies.GetProperty("moves");
    public JsonElement Species => Dependencies.GetProperty("species");
    public JsonElement Items => Dependencies.GetProperty("items");
    public JsonElement Machines => Dependencies.GetProperty("machines");
    public JsonElement Pickups => Dependencies.GetProperty("pickups");
    public JsonElement ScriptInventory { get; }
    public JsonElement Catalog { get; }
    public JsonElement NpcSources { get; }
    public JsonElement ExternalVermilionEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");

    private PretSsAnneRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, JsonElement dependencies, JsonElement catalog, JsonElement scriptsInventory,
        JsonElement npcSources, JsonElement externalVermilionEvents)
    {
        GeneratedDirectory = directory; NewMaps = maps; Scripts = scripts; TextSources = texts;
        Constants = constants; SourceFiles = files; Dependencies = dependencies; Catalog = catalog;
        ScriptInventory = scriptsInventory; NpcSources = npcSources; ExternalVermilionEvents = externalVermilionEvents;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SSANNE_1F_CORRIDOR" => new("map.ssanne-1f-corridor"),
        "MAP_SSANNE_1F_ROOM1" => new("map.ssanne-1f-room-1"),
        "MAP_SSANNE_1F_ROOM2" => new("map.ssanne-1f-room-2"),
        "MAP_SSANNE_1F_ROOM3" => new("map.ssanne-1f-room-3"),
        "MAP_SSANNE_1F_ROOM4" => new("map.ssanne-1f-room-4"),
        "MAP_SSANNE_1F_ROOM5" => new("map.ssanne-1f-room-5"),
        "MAP_SSANNE_1F_ROOM6" => new("map.ssanne-1f-room-6"),
        "MAP_SSANNE_1F_ROOM7" => new("map.ssanne-1f-room-7"),
        "MAP_SSANNE_2F_CORRIDOR" => new("map.ssanne-2f-corridor"),
        "MAP_SSANNE_2F_ROOM1" => new("map.ssanne-2f-room-1"),
        "MAP_SSANNE_2F_ROOM2" => new("map.ssanne-2f-room-2"),
        "MAP_SSANNE_2F_ROOM3" => new("map.ssanne-2f-room-3"),
        "MAP_SSANNE_2F_ROOM4" => new("map.ssanne-2f-room-4"),
        "MAP_SSANNE_2F_ROOM5" => new("map.ssanne-2f-room-5"),
        "MAP_SSANNE_2F_ROOM6" => new("map.ssanne-2f-room-6"),
        "MAP_SSANNE_3F_CORRIDOR" => new("map.ssanne-3f-corridor"),
        "MAP_SSANNE_B1F_CORRIDOR" => new("map.ssanne-b1f-corridor"),
        "MAP_SSANNE_B1F_ROOM1" => new("map.ssanne-b1f-room-1"),
        "MAP_SSANNE_B1F_ROOM2" => new("map.ssanne-b1f-room-2"),
        "MAP_SSANNE_B1F_ROOM3" => new("map.ssanne-b1f-room-3"),
        "MAP_SSANNE_B1F_ROOM4" => new("map.ssanne-b1f-room-4"),
        "MAP_SSANNE_B1F_ROOM5" => new("map.ssanne-b1f-room-5"),
        "MAP_SSANNE_CAPTAINS_OFFICE" => new("map.ssanne-captains-office"),
        "MAP_SSANNE_DECK" => new("map.ssanne-deck"),
        "MAP_SSANNE_EXTERIOR" => new("map.ssanne-exterior"),
        "MAP_SSANNE_KITCHEN" => new("map.ssanne-kitchen"),
        _ => PretVermilionRegion.MapIdFromSourceSymbol(symbol)
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

    public static PretSsAnneRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/ssanne-region");
        var rawDirectory = Path.Combine(project, PretSourcePaths.FireRedRawDirectory);
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project,"scripts/ssanne-region.lock.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "S.S. Anne manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps, "ssanne-region/" + path)) continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate S.S. Anne output.");
        }
        using var catalog = JsonDocument.Parse(outputs["ssanne-catalog.json"]);
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            var bytes = ReadPinned(rawDirectory,path);
            Require(raw.TryAdd(path, bytes), "Duplicate S.S. Anne source.");
            files.Add(new(path,$"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated S.S. Anne source profile; runtime certification is separate.", Profile, inventory);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.RootElement.GetProperty("maps").EnumerateArray())
        {
            var relative = spec.GetProperty("directory").GetString()!;
            if (runtimeMaps is not null && runtimeMaps.Maps.TryGetValue(new MapId(spec.GetProperty("stableId").GetString()!), out var compact) )
            {
                var runtimeMap = PretWorldMap.FromRuntime(compact, metadata, SafeFile(directory, relative),
                    SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
                Require(maps.TryAdd(runtimeMap.Id, runtimeMap), "Duplicate S.S. Anne runtime map.");
                continue;
            }
            var map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            Require(MapIdFromSourceSymbol(map.SourceMapSymbol) == map.Id, "Map stable identity differs.");
            Require(maps.TryAdd(map.Id, map), "Duplicate region map.");
        }
        Require(maps.Count == 26 && maps.Values.Sum(x => x.Width*x.Height) == 5450, "Incomplete S.S. Anne map set.");
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        Require(scriptInventory.RootElement.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "S.S. Anne script reference closure is incomplete.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var property in scriptInventory.RootElement.GetProperty("filePreambles").EnumerateObject())
        {
            var path = property.Name;
            var text = Encoding.UTF8.GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Require(symbol.Length > 0, "S.S. Anne script has no original source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret,path,symbol,"pret/pokefirered",PretWorldMap.SourceCommit)));
            }
        }
        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_GOT_SS_TICKET"] > 0 && constants["FLAG_GOT_HM01"] > 0 &&
            constants["LOCALID_SS_ANNE_CAPTAIN"] == 1, "S.S. Anne progression/local actor constants differ.");
        using var dependencies = JsonDocument.Parse(outputs["dependencies.json"]);
        Require(!dependencies.RootElement.GetProperty("runtimeIntegrated").GetBoolean() &&
            dependencies.RootElement.GetProperty("trainers").GetArrayLength() == 19 &&
            dependencies.RootElement.GetProperty("pickups").GetArrayLength() == 12 &&
            dependencies.RootElement.GetProperty("captain").GetProperty("item").GetString() == "ITEM_HM01" &&
            dependencies.RootElement.GetProperty("departure").GetProperty("afterCityScene").GetInt32() == 3,
            "S.S. Anne dependency scope differs.");
        using var npcSources = JsonDocument.Parse(outputs["npc-source.json"]);
        using var cityEvents = JsonDocument.Parse(raw["data/maps/VermilionCity/map.json"]);
        return new(directory, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(),
            inventory, dependencies.RootElement.Clone(), catalog.RootElement.Clone(), scriptInventory.RootElement.Clone(),
            npcSources.RootElement.Clone(), cityEvents.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe S.S. Anne path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("S.S. Anne directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("S.S. Anne file symlink is unsupported: " + path);
        }
        return result;
    }
}
