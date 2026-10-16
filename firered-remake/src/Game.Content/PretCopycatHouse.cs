using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The original two-floor Saffron Copycat house; item and tutor admission are separate.</summary>
public sealed class PretCopycatHouse
{
    public const string ResourceId = "firered-copycat-house-v1";
    public static MapId Floor1MapId { get; } = new("map.saffron-city-copycats-house-1f");
    public static MapId Floor2MapId { get; } = new("map.saffron-city-copycats-house-2f");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }

    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretCopycatHouse(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory; Catalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); NpcSources = source.Document("npc-source.json");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SAFFRON_CITY_COPYCATS_HOUSE_1F" => Floor1MapId,
        "MAP_SAFFRON_CITY_COPYCATS_HOUSE_2F" => Floor2MapId,
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        _ => throw new InvalidDataException("Unknown Copycat house map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need((map.Id == Floor1MapId || map.Id == Floor2MapId) &&
            map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Copycat house must retain its original disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.CreateConnectedCells(), []);
    }

    /// <summary>Load before campaign compilation. dependencyRoot supplies unchanged town and actor resources.</summary>
    public static PretCopycatHouse Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"maps/regions/copycat-house");
        var graphics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var texture in source.Document("npc-source.json").GetProperty("graphics").EnumerateArray())
        {
            string graphic = texture.GetProperty("graphicsId").GetString()!;
            Need(graphics.Add(graphic) && graphic is "OBJ_EVENT_GFX_BALDING_MAN" or "OBJ_EVENT_GFX_WOMAN_3" or "OBJ_EVENT_GFX_CHANSEY" or
                    "OBJ_EVENT_GFX_DODUO" or "OBJ_EVENT_GFX_PIKACHU" or "OBJ_EVENT_GFX_JIGGLYPUFF" or
                    "OBJ_EVENT_GFX_LITTLE_GIRL" or "OBJ_EVENT_GFX_RED_NORMAL",
                "Copycat house actors must reuse the original existing resources.");
        }
        Need(graphics.Count == 8, "Copycat house actor collection differs.");
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId ,
            "Copycat house catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original Saffron Copycat house; Mimic and Poke Doll service admission are separate.", ResourceId);

        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!,relative = spec.GetProperty("mapPath").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need((id == Floor1MapId.Value || id == Floor2MapId.Value) && path == relative ,
                "Copycat house compact map binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == id, "Copycat house map identity differs.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
            Need(map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null &&
                map.Events.GetProperty("coord_events").GetArrayLength() == 0, "Copycat house indoor event boundaries differ.");
            maps.Add(map.Id, map);
        }
        var first = maps[Floor1MapId]; var second = maps[Floor2MapId];
        Need(first.Width == 13 && first.Height == 10 && first.FieldWarps.Length == 4 &&
            second.Width == 12 && second.Height == 9 && second.FieldWarps.Length == 1 &&
            first.Events.GetProperty("object_events").GetArrayLength() == 3 &&
            second.Events.GetProperty("object_events").GetArrayLength() == 4 &&
            first.Events.GetProperty("bg_events").GetArrayLength() == 0 && second.Events.GetProperty("bg_events").GetArrayLength() == 3,
            "Copycat house original map/event collection differs.");
        ValidateTopology(source, dependencyRoot, first, second);

        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["MOVETUTOR_MIMIC"] == 7 && constants["ITEM_POKE_DOLL"] == 80 &&
            constants["SPECIES_CHANSEY"] == 113 && constants["SPECIES_DODUO"] == 84,
            "Copycat original tutor/item/cry constants differ.");
        return new(source, maps.ToImmutable(), constants);
    }

    private static void ValidateTopology(SilphSourcePackage source, string dependencyRoot, PretWorldMap first, PretWorldMap second)
    {
        var retained = source.Document("contracts.json").GetProperty("retainedTown");
        string package = retained.GetProperty("currentPackage").GetString()!;
        var town = retained.GetProperty("currentResource");
        var data = SilphSourcePackage.Json(SafariSource.Read(dependencyRoot,package + "/" + town.GetProperty("path").GetString()));
        var entrance = data.GetProperty("events").GetProperty("warp_events")[1];
        Need(Warp(entrance, 22, 14, 0, "MAP_SAFFRON_CITY_COPYCATS_HOUSE_1F", "1"), "Original Saffron entrance differs.");
        var exits = first.Events.GetProperty("warp_events");
        for (int i = 0; i < 3; i++)
            Need(Warp(exits[i], 3 + i, 8, 3, "MAP_SAFFRON_CITY", "1"), "Original three-tile house exit differs.");
        Need(Warp(exits[3], 10, 2, 3, "MAP_SAFFRON_CITY_COPYCATS_HOUSE_2F", "0") &&
            Warp(second.Events.GetProperty("warp_events")[0], 10, 2, 3, "MAP_SAFFRON_CITY_COPYCATS_HOUSE_1F", "3"),
            "Original reciprocal Copycat stairs differ.");
        Need(retained.GetProperty("geometryChanges").GetArrayLength() == 0 && retained.GetProperty("eventChanges").GetArrayLength() == 0,
            "Copycat house must not rewrite its original town entrance.");
    }

    private static bool Warp(JsonElement value, int x, int y, int elevation, string map, string index) =>
        value.GetProperty("x").GetInt32() == x && value.GetProperty("y").GetInt32() == y &&
        value.GetProperty("elevation").GetInt32() == elevation && value.GetProperty("dest_map").GetString() == map &&
        value.GetProperty("dest_warp_id").GetString() == index;

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
