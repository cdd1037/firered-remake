using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Two original tutor interiors and their unchanged adjacent entrances; teaching admission is separate.</summary>
public sealed class PretOriginalTutorRooms
{
    public const string ResourceId = "firered-original-tutor-rooms-v1";
    public const string ManifestSha256 = "e8722902286244706eaf6ab8a0566ff73eb8db09afe42615721bb3aa88ded62e";
    public const string LockSha256 = "78a61aef6f4b425b43f3389997fd47b85c80a0e89bf7942a93a7dc40d9d58c2c";
    public const string FullSourceHash = "e52729507c966446bf41726d82560adaacb2d92c52e986542196a5eb0ec059bb";
    public const string BodySlamScriptSha256 = "3ace070d6b0f6a231625a273a85760ad0d65dc0e2e26ac8ff6894a8a0bcda878";
    public const string MetronomeScriptSha256 = "53e60afdf97c5beac11106402be91e6a34c81dc06d56ea973d769cd05a349f97";
    public static MapId BodySlamMapId { get; } = new("map.four-island-house1");
    public static MapId MetronomeMapId { get; } = new("map.cinnabar-island-pokemon-lab-research-room");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement MapCatalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretOriginalTutorRooms(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        GeneratedDirectory = source.Directory; MapCatalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); ScriptInventory = source.Document("scripts.json");
        NpcSources = source.Document("npc-source.json");
        AdditionalSourceHashes = ["original-tutor-rooms-manifest:" + ManifestSha256, "original-tutor-rooms-lock:" + LockSha256,
            .. source.Files.Select(f => "original-tutor-rooms:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_FOUR_ISLAND_HOUSE1" => BodySlamMapId,
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_RESEARCH_ROOM" => MetronomeMapId,
        "MAP_FOUR_ISLAND" => new("map.four-island"),
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE" => new("map.cinnabar-island-pokemon-lab-entrance"),
        _ => throw new InvalidDataException("Unknown original tutor room map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need((map.Id == BodySlamMapId || map.Id == MetronomeMapId) &&
            map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Original tutor rooms must retain their disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>Load before campaign compilation. dependencyRoot supplies unchanged adjacent maps and NPC textures.</summary>
    public static PretOriginalTutorRooms Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot, "original-tutor-rooms", ResourceId,
            "scripts/original-tutor-rooms.lock.json", LockSha256, 27112, ManifestSha256, 5207, 88, 24);
        var graphics = new HashSet<string>(["OBJ_EVENT_GFX_FAT_MAN", "OBJ_EVENT_GFX_POKE_MANIAC",
            "OBJ_EVENT_GFX_SCIENTIST", "OBJ_EVENT_GFX_RED_NORMAL"], StringComparer.Ordinal);
        foreach (var texture in source.Document("npc-source.json").GetProperty("graphics").EnumerateArray())
        {
            Need(graphics.Remove(texture.GetProperty("graphicsId").GetString()!) && texture.GetProperty("reuseExisting").GetBoolean(),
                "Original tutor room actors must reuse the original existing resources.");
            _ = SafariSource.Read(dependencyRoot, texture.GetProperty("texturePath").GetString()!,
                texture.GetProperty("sha256").GetString()!, texture.GetProperty("byteLength").GetInt32());
        }
        Need(graphics.Count == 0, "Original tutor room actor resources are incomplete.");
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 2 &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")),
            "Original tutor room catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.",
            "Two original tutor interiors; teaching definitions and current-game admission are separate.", ResourceId, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var outputs = source.Manifest.GetProperty("outputs").EnumerateArray()
            .ToDictionary(p => p.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
            string path = spec.GetProperty("path").GetString()!, hash = spec.GetProperty("sha256").GetString()!;
            Need(path == relative + "/runtime-map.json" && hash == outputs[path].GetProperty("sha256").GetString() &&
                spec.GetProperty("byteLength").GetInt32() == source.Outputs[path].Length,
                "Original tutor room compact map binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, source.Outputs[path].Length, hash,
                relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(),
                spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
            PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id, "Original tutor room source identity differs.");
            foreach (var artifact in runtime.Artifacts)
                Need(outputs[relative + "/" + artifact.Path].GetProperty("sha256").GetString() == artifact.Sha256,
                    "Original tutor room image binding differs.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/city-services/npc/red-normal.png"), MapIdFromSourceSymbol);
            Need(map.FieldWarps.Length == 1 && map.Events.GetProperty("object_events").GetArrayLength() == 2 &&
                map.Events.GetProperty("coord_events").GetArrayLength() == 0 &&
                map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Original tutor room event scope differs.");
            maps.Add(map.Id, map);
        }
        Need(maps.Keys.ToHashSet().SetEquals([BodySlamMapId, MetronomeMapId]), "Original tutor room map collection differs.");
        ValidateRoom(maps[BodySlamMapId], 11, 9, "LAYOUT_HOUSE3", 0, 4, 7, "MAP_FOUR_ISLAND", "2");
        ValidateRoom(maps[MetronomeMapId], 15, 11, "LAYOUT_CINNABAR_ISLAND_POKEMON_LAB_RESEARCH_ROOM", 2,
            7, 9, "MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE", "4");
        ValidateAnchors(source, dependencyRoot, maps);
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 33 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Original tutor room script closure differs.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var entry in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            string path = entry.GetString()!, text = new UTF8Encoding(false, true).GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                string label = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(label.Length > 0, "Missing original tutor room script label.");
                scripts.Add(new(path, text, new(SourceKind.Pret, path, label, "pret/pokefirered", PretWorldMap.SourceCommit, files[path].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants["MOVETUTOR_BODY_SLAM"] == 3 && constants["MOVETUTOR_METRONOME"] == 8,
            "Original tutor room teaching indexes differ.");
        return new(source, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants);
    }

    private static void ValidateRoom(PretWorldMap map, int width, int height, string layout, int backgroundCount,
        int exitX, int exitY, string destination, string destinationIndex)
    {
        Need(map.Width == width && map.Height == height && map.Cells.Length == width * height &&
            map.Events.GetProperty("layout").GetString() == layout && map.Events.GetProperty("bg_events").GetArrayLength() == backgroundCount,
            "Original tutor room geometry or background event count differs.");
        var door = map.Events.GetProperty("warp_events")[0];
        Need(door.GetProperty("x").GetInt32() == exitX && door.GetProperty("y").GetInt32() == exitY &&
            door.GetProperty("elevation").GetInt32() == 3 && door.GetProperty("dest_map").GetString() == destination &&
            door.GetProperty("dest_warp_id").GetString() == destinationIndex, "Original tutor room exit differs.");
    }

    private static void ValidateAnchors(SilphSourcePackage source, string dependencyRoot,
        ImmutableDictionary<MapId, PretWorldMap>.Builder maps)
    {
        var anchors = source.Document("contracts.json").GetProperty("retainedAnchors");
        Need(anchors.GetArrayLength() == 2, "Original tutor room adjacent-map scope differs.");
        foreach (var retained in anchors.EnumerateArray())
        {
            string package = retained.GetProperty("currentPackage").GetString()!;
            _ = SafariSource.Read(dependencyRoot, package + "/manifest.json", retained.GetProperty("currentManifestSha256").GetString()!,
                retained.GetProperty("currentManifestByteLength").GetInt32());
            var resource = retained.GetProperty("currentResource");
            var data = SilphSourcePackage.Json(SafariSource.Read(dependencyRoot, package + "/" + resource.GetProperty("path").GetString(),
                resource.GetProperty("sha256").GetString()!, resource.GetProperty("byteLength").GetInt32()));
            var events = data.GetProperty("events");
            Need(JsonElement.DeepEquals(events, SilphSourcePackage.Json(source.Raw["data/maps/" + retained.GetProperty("sourceName").GetString() + "/map.json"])),
                "Retained adjacent-map events differ from the original source.");
            var entrance = retained.GetProperty("entrances")[0];
            int index = entrance.GetProperty("index").GetInt32();
            var door = events.GetProperty("warp_events")[index];
            Need(JsonElement.DeepEquals(door, entrance.GetProperty("event")), "Original tutor room entrance differs.");
            var room = maps[MapIdFromSourceSymbol(door.GetProperty("dest_map").GetString()!)];
            var exit = room.Events.GetProperty("warp_events")[0];
            Need(door.GetProperty("dest_warp_id").GetString() == "0" &&
                exit.GetProperty("dest_map").GetString() == events.GetProperty("id").GetString() &&
                exit.GetProperty("dest_warp_id").GetString() == index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Original tutor room doors are not reciprocal.");
            Need(retained.GetProperty("geometryChanges").GetArrayLength() == 0 && retained.GetProperty("eventChanges").GetArrayLength() == 0,
                "Original tutor rooms must not rewrite adjacent map geometry or events.");
        }
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
