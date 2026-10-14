using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Original ordinary School/House interiors, with source scripts and unchanged exterior entrances.</summary>
public sealed class PretSchoolHouseInteriors
{
    public const string ResourceId = "firered-school-house-interiors-v1";
    public const string ManifestSha256 = "11326c7a3fb8a93ed9f9d3454ec9659736738ba107093e4f84f999a0e9e4acfa";
    public const string LockSha256 = "9e395ff86732c25de3e098392334551b66884d4f76d89c01c7d634abaed6c933";
    public const string FullSourceHash = "b719db92c4dd132901f5b2b4a5bea2681f17f76b617a735bff3e69533bf78987";
    public const string SchoolScriptSha256 = "f4c90c4c73ee3ff8d2697e701f9c756420a04db2a2640a3003ae36e460fc5790";
    public const string HouseScriptSha256 = "876a467f74b6744ae43f087d3602e85f28b85b2ffaf712e6fb92d0034ecd71f4";
    public const string ThreeIslandHouseScriptSha256 = "c32e2476dff0feabf7c2b74b2a8755043749a2a9911c634d1c340f29e3baa424";
    public static MapId SchoolMapId { get; } = new("map.viridian-school");
    public static MapId HouseMapId { get; } = new("map.fuchsia-city-house-1");
    public static MapId ThreeIslandHouseMapId { get; } = new("map.three-island-house2");
    private sealed record RoomSpec(MapId Id, string SourceName, string Layout, int Width, int Height,
        int Actors, int Backgrounds, string Destination, string DestinationIndex, (int X, int Elevation)[] Exits);
    private static readonly RoomSpec[] Rooms = [
        new(SchoolMapId, "ViridianCity_School", "LAYOUT_VIRIDIAN_CITY_SCHOOL", 10, 9, 2, 5,
            "MAP_VIRIDIAN_CITY", "3", [(3,0),(4,0),(5,3)]),
        new(HouseMapId, "FuchsiaCity_House1", "LAYOUT_HOUSE1", 11, 9, 3, 0,
            "MAP_FUCHSIA_CITY", "5", [(3,3),(4,3),(5,3)]),
        new(ThreeIslandHouseMapId, "ThreeIsland_House2", "LAYOUT_HOUSE3", 11, 9, 2, 0,
            "MAP_THREE_ISLAND", "3", [(4,3)])];
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableDictionary<string, string> AuthenticatedMapScriptHashes { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement MapCatalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretSchoolHouseInteriors(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        GeneratedDirectory = source.Directory; MapCatalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); ScriptInventory = source.Document("scripts.json");
        NpcSources = source.Document("npc-source.json");
        var mapScriptPaths = Rooms.Select(r => "data/maps/" + r.SourceName + "/scripts.inc").ToHashSet(StringComparer.Ordinal);
        AuthenticatedMapScriptHashes = source.Files.Where(f => mapScriptPaths.Contains(f.Path))
            .ToImmutableDictionary(f => f.Path, f => f.Sha256, StringComparer.Ordinal);
        Need(AuthenticatedMapScriptHashes.Count == Rooms.Length, "Authenticated room map-script inventory is incomplete.");
        AdditionalSourceHashes = ["school-house-interiors-manifest:" + ManifestSha256, "school-house-interiors-lock:" + LockSha256,
            .. source.Files.Select(f => "school-house-interiors:" + f.Path + ":" + f.Sha256)];
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_VIRIDIAN_CITY_SCHOOL" => SchoolMapId,
        "MAP_FUCHSIA_CITY_HOUSE1" => HouseMapId,
        "MAP_THREE_ISLAND_HOUSE2" => ThreeIslandHouseMapId,
        "MAP_THREE_ISLAND" => new("map.three-island"),
        "MAP_VIRIDIAN_CITY" => new("map.viridian-city"),
        "MAP_FUCHSIA_CITY" => new("map.fuchsia-city"),
        _ => throw new InvalidDataException("Unknown school/house interior map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(Rooms.Any(r => r.Id == map.Id) &&
            map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "School/house interiors must retain their disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>Load before campaign compilation. dependencyRoot supplies unchanged adjacent maps and NPC textures.</summary>
    public static PretSchoolHouseInteriors Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot, "school-house-interiors", ResourceId,
            "scripts/school-house-interiors.lock.json", LockSha256, 35244, ManifestSha256, 6579, 115, 33);
        var graphics = new HashSet<string>(["OBJ_EVENT_GFX_WOMAN_2", "OBJ_EVENT_GFX_LASS",
            "OBJ_EVENT_GFX_OLD_MAN_1", "OBJ_EVENT_GFX_LITTLE_BOY", "OBJ_EVENT_GFX_RED_NORMAL",
            "OBJ_EVENT_GFX_ROCKER", "OBJ_EVENT_GFX_MAN"], StringComparer.Ordinal);
        foreach (var texture in source.Document("npc-source.json").GetProperty("graphics").EnumerateArray())
        {
            Need(graphics.Remove(texture.GetProperty("graphicsId").GetString()!) && texture.GetProperty("reuseExisting").GetBoolean(),
                "School/house interior actors must reuse the original existing resources.");
            _ = SafariSource.Read(dependencyRoot, texture.GetProperty("texturePath").GetString()!,
                texture.GetProperty("sha256").GetString()!, texture.GetProperty("byteLength").GetInt32());
        }
        Need(graphics.Count == 0, "School/house interior actor resources are incomplete.");
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == Rooms.Length &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")),
            "School/house interior catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.",
            "Three source-authenticated ordinary interiors; grid and wall-map runtime admission remain separate.", ResourceId, source.Files);
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
                "School/house interior compact map binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, source.Outputs[path].Length, hash,
                relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(),
                spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
            PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id, "School/house interior source identity differs.");
            foreach (var artifact in runtime.Artifacts)
                Need(outputs[relative + "/" + artifact.Path].GetProperty("sha256").GetString() == artifact.Sha256,
                    "School/house interior image binding differs.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/city-services/npc/red-normal.png"), MapIdFromSourceSymbol);
            Need(map.FieldWarps.Length == Rooms.Single(r => r.Id == map.Id).Exits.Length &&
                map.Events.GetProperty("coord_events").GetArrayLength() == 0 &&
                map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "School/house interior event scope differs.");
            maps.Add(map.Id, map);
        }
        Need(maps.Keys.ToHashSet().SetEquals(Rooms.Select(r => r.Id)), "School/house interior map collection differs.");
        foreach (var room in Rooms) ValidateRoom(maps[room.Id], room);
        ValidateAnchors(source, dependencyRoot, maps);
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 63 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "School/house interior script closure differs.");
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
                Need(label.Length > 0, "Missing school/house interior script label.");
                scripts.Add(new(path, text, new(SourceKind.Pret, path, label, "pret/pokefirered", PretWorldMap.SourceCommit, files[path].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants["LOCALID_SCHOOL_WOMAN"] == 1 && constants["LOCALID_SCHOOL_LASS"] == 2 &&
            constants["MULTICHOICE_TRAINER_SCHOOL_WHITEBOARD"] == 15 && constants["FAMECHECKER_OAK"] == 0 &&
            constants["FAMECHECKER_DAISY"] == 1 && constants["FAMECHECKER_BILL"] == 13 && constants["FAMECHECKER_SABRINA"] == 7,
            "Original school actor/menu and Fame constants differ.");
        return new(source, maps.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable(), constants);
    }

    private static void ValidateRoom(PretWorldMap map, RoomSpec room)
    {
        Need(map.Width == room.Width && map.Height == room.Height && map.Cells.Length == room.Width * room.Height &&
            map.Events.GetProperty("layout").GetString() == room.Layout &&
            map.Events.GetProperty("object_events").GetArrayLength() == room.Actors &&
            map.Events.GetProperty("bg_events").GetArrayLength() == room.Backgrounds,
            "Original interior geometry or event counts differ.");
        for (int i = 0; i < room.Exits.Length; i++)
        {
            var door = map.Events.GetProperty("warp_events")[i];
            Need(door.GetProperty("x").GetInt32() == room.Exits[i].X && door.GetProperty("y").GetInt32() == 7 &&
                door.GetProperty("elevation").GetInt32() == room.Exits[i].Elevation &&
                door.GetProperty("dest_map").GetString() == room.Destination &&
                door.GetProperty("dest_warp_id").GetString() == room.DestinationIndex, "Original interior exit differs.");
        }
    }

    public void ValidateReusedMaps(IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        foreach (var anchor in Contracts.GetProperty("retainedAnchors").EnumerateArray())
        {
            var id = MapIdFromSourceSymbol(anchor.GetProperty("sourceMap").GetString()!);
            Need(existing.TryGetValue(id, out var map) &&
                map.SourceRef.Sha256 == SourceFiles.Single(f => f.Path == "data/maps/" + anchor.GetProperty("sourceName").GetString() + "/map.json").Sha256,
                "Retained original exterior identity differs.");
            var entrance = anchor.GetProperty("entrances")[0];
            Need(JsonElement.DeepEquals(map!.Events.GetProperty("warp_events")[entrance.GetProperty("index").GetInt32()],
                entrance.GetProperty("event")), "Retained original exterior entrance differs.");
        }
    }

    private static void ValidateAnchors(SilphSourcePackage source, string dependencyRoot,
        ImmutableDictionary<MapId, PretWorldMap>.Builder maps)
    {
        var anchors = source.Document("contracts.json").GetProperty("retainedAnchors");
        Need(anchors.GetArrayLength() == Rooms.Length, "School/house interior adjacent-map scope differs.");
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
            Need(JsonElement.DeepEquals(door, entrance.GetProperty("event")), "School/house interior entrance differs.");
            var room = maps[MapIdFromSourceSymbol(door.GetProperty("dest_map").GetString()!)];
            int destinationIndex = int.Parse(door.GetProperty("dest_warp_id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            Need(destinationIndex >= 0 && destinationIndex < room.FieldWarps.Length, "Room entry warp index is outside original exits.");
            var exit = room.Events.GetProperty("warp_events")[destinationIndex];
            Need(exit.GetProperty("dest_map").GetString() == events.GetProperty("id").GetString() &&
                exit.GetProperty("dest_warp_id").GetString() == index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "School/house interior doors are not reciprocal.");
            Need(retained.GetProperty("geometryChanges").GetArrayLength() == 0 && retained.GetProperty("eventChanges").GetArrayLength() == 0,
                "School/house interiors must not rewrite adjacent map geometry or events.");
        }
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
