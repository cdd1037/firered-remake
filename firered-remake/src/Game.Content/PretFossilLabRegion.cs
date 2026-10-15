using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Authenticated original Cinnabar fossil lab resources. This is not a game profile or runtime admission.</summary>
public sealed class PretFossilLabRegion
{
    public const string ResourceId = "firered-fossil-lab-region-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }

    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    /// <summary>Contains authenticated project-relative bindings to existing NPC textures.</summary>
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretFossilLabRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Constants = constants;
        SourceFiles = ImmutableArray<PretWorldSourceFile>.Empty; GeneratedDirectory = source.Directory;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE" => new("map.cinnabar-island-pokemon-lab-entrance"),
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_EXPERIMENT_ROOM" => new("map.cinnabar-island-pokemon-lab-experiment-room"),
        "MAP_CINNABAR_ISLAND" => new("map.cinnabar-island"),
        // These are original, preserved warp destinations. Resolving their IDs
        // does not add them to NewMaps or admit traversal into missing rooms.
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_LOUNGE" => new("map.cinnabar-island-pokemon-lab-lounge"),
        "MAP_CINNABAR_ISLAND_POKEMON_LAB_RESEARCH_ROOM" => new("map.cinnabar-island-pokemon-lab-research-room"),
        _ => throw new InvalidDataException("Unknown Cinnabar fossil lab map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need((map.Id == new MapId("map.cinnabar-island-pokemon-lab-entrance") ||
            map.Id == new MapId("map.cinnabar-island-pokemon-lab-experiment-room")) &&
            map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null,
            "Cinnabar fossil lab must retain its original disconnected indoor geometry.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    public static PretFossilLabRegion Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot,"fossil-lab-region");
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId ,
            "Cinnabar fossil lab catalog source identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original Cinnabar fossil lab resources; current-game admission is separate.", ResourceId, ImmutableArray<PretWorldSourceFile>.Empty);

        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!,relative = spec.GetProperty("directory").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need(path == relative + "/runtime-map.json" ,
                "Cinnabar fossil lab compact resource binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.Width == (id.EndsWith("-entrance", StringComparison.Ordinal) ? 28 : 15) && runtime.Height == 11,
                "Cinnabar fossil lab map identity/dimensions differ.");
            maps.Add(new(id), PretWorldMap.FromRuntime(runtime, metadata,
                PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(projectRoot, "content/generated/shared-overworld/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(maps.Keys.ToHashSet().SetEquals(new[] { new MapId("map.cinnabar-island-pokemon-lab-entrance"),new MapId("map.cinnabar-island-pokemon-lab-experiment-room") }) ,
            "Cinnabar fossil lab map/warp collection differs.");

        var files = ImmutableArray<PretWorldSourceFile>.Empty.ToDictionary(f => f.Path, StringComparer.Ordinal);
        
        var constants = PretSharedConstants.ForPackage(source.Directory);
        Need(constants["HELIX_FOSSIL"] == 1 && constants["DOME_FOSSIL"] == 2 && constants["OLD_AMBER"] == 3 &&
            constants["VAR_MAP_SCENE_CINNABAR_ISLAND_POKEMON_LAB_EXPERIMENT_ROOM_WHICH_FOSSIL"] == 0x4069 &&
            constants["VAR_MAP_SCENE_CINNABAR_ISLAND_POKEMON_LAB_EXPERIMENT_ROOM_REVIVE_STATE"] == 0x406A &&
            constants["FLAG_REVIVED_DOME"] == 0x2EC && constants["FLAG_REVIVED_HELIX"] == 0x2ED && constants["FLAG_REVIVED_AMBER"] == 0x2EE &&
            constants["SPECIES_OMANYTE"] == 138 && constants["SPECIES_KABUTO"] == 140 && constants["SPECIES_AERODACTYL"] == 142,
            "Fossil selection, state and gift constants differ.");
        var entranceEvents = maps[new("map.cinnabar-island-pokemon-lab-entrance")].Events;
        var experimentEvents = maps[new("map.cinnabar-island-pokemon-lab-experiment-room")].Events;
        Need(entranceEvents.GetProperty("object_events").GetArrayLength() == 1 &&
            entranceEvents.GetProperty("coord_events").GetArrayLength() == 0 && entranceEvents.GetProperty("bg_events").GetArrayLength() == 4 &&
            experimentEvents.GetProperty("object_events").GetArrayLength() == 2 &&
            experimentEvents.GetProperty("coord_events").GetArrayLength() == 0 && experimentEvents.GetProperty("bg_events").GetArrayLength() == 0,
            "Fossil lab original event cohort differs.");
        var contracts = source.Document("contracts.json");
        Need(contracts.GetProperty("unsupportedWarpDestinations").GetArrayLength() == 2 &&
            contracts.GetProperty("unsupportedNpcEntries").GetArrayLength() == 1 &&
            contracts.GetProperty("unsupportedNpcEntries")[0].GetProperty("script").GetString() ==
                "CinnabarIsland_PokemonLab_ExperimentRoom_EventScript_Garett" &&
            !contracts.GetProperty("revivalState").GetProperty("timerBased").GetBoolean(),
            "Fossil lab explicit unsupported boundaries or readiness contract differ.");
        return new(source, maps.ToImmutable(), constants);
    }
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
