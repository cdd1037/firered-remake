using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Authenticated original Saffron Fighting Dojo resources. This is not a game profile or runtime admission.</summary>
public sealed class PretFightingDojoRegion
{
    public const string ResourceId = "firered-fighting-dojo-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }

    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    /// <summary>Contains authenticated project-relative bindings to existing NPC textures.</summary>
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretFightingDojoRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Constants = constants; GeneratedDirectory = source.Directory;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SAFFRON_CITY_DOJO" => new("map.saffron-city-dojo"),
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        _ => throw new InvalidDataException("Unknown Saffron Fighting Dojo map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == new MapId("map.saffron-city-dojo") && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Saffron Fighting Dojo must retain its original disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    public static PretFightingDojoRegion Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot,"maps/regions/fighting-dojo");
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId ,
            "Saffron Fighting Dojo catalog source identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original Saffron Fighting Dojo resources; current-game admission is separate.", ResourceId);

        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!,relative = spec.GetProperty("mapPath").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need(path == relative ,
                "Saffron Fighting Dojo compact resource binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceMapSymbol).Value == id && runtime.Width == 13 && runtime.Height == 16,
                "Saffron Fighting Dojo map identity/dimensions differ.");
            maps.Add(new(id), PretWorldMap.FromRuntime(runtime, metadata,
                PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(projectRoot, "content/generated/sprites/overworld/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(maps.ContainsKey(new("map.saffron-city-dojo")) && maps.Values.Single().FieldWarps.Length == 3,
            "Saffron Fighting Dojo map/warp collection differs.");

        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["LOCALID_KARATE_MASTER"] == 5 && constants["FLAG_GOT_HITMON_FROM_DOJO"] == 0x278 &&
            constants["FLAG_HIDE_DOJO_HITMONLEE_BALL"] == 0x60 && constants["FLAG_HIDE_DOJO_HITMONCHAN_BALL"] == 0x61 &&
            constants["SPECIES_HITMONLEE"] == 106 && constants["SPECIES_HITMONCHAN"] == 107,
            "Dojo actor and gift constants differ.");
        var events = maps.Values.Single().Events;
        Need(events.GetProperty("object_events").GetArrayLength() == 7 && events.GetProperty("coord_events").GetArrayLength() == 2 &&
            events.GetProperty("bg_events").GetArrayLength() == 4, "Dojo original event cohort differs.");
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
