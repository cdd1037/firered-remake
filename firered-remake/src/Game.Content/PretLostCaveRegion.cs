using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Eighteen original connected Lost Cave / Resort Gorgeous maps. Loading does not admit gameplay or services.</summary>
public sealed class PretLostCaveRegion
{
    public const string Profile = "firered-lost-cave-region-v1";
    private static readonly string[] SourceNames = ["FiveIsland_WaterLabyrinth", "FiveIsland_ResortGorgeous",
        "FiveIsland_ResortGorgeous_House", "FiveIsland_LostCave_Entrance",
        .. Enumerable.Range(1,14).Select(i => "FiveIsland_LostCave_Room" + i.ToString(CultureInfo.InvariantCulture))];
    public static MapId WaterLabyrinthMapId { get; } = new("map.five-island-water-labyrinth");
    public static MapId ResortGorgeousMapId { get; } = new("map.five-island-resort-gorgeous");
    public static MapId HouseMapId { get; } = new("map.five-island-resort-gorgeous-house");
    public static MapId EntranceMapId { get; } = new("map.five-island-lost-cave-entrance");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement Declarations { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretLostCaveRegion(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string,string> texts,
        ImmutableDictionary<string,int> constants)
    {
        NewMaps = maps; Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        GeneratedDirectory = source.Directory; Catalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json"); ScriptInventory = source.Document("scripts.json");
        Declarations = source.Document("declarations.json"); NpcSources = source.Document("npc-source.json");

    }

    public static MapId MapIdFromSourceSymbol(string symbol)
    {
        if (symbol.StartsWith("MAP_FIVE_ISLAND_LOST_CAVE_ROOM", StringComparison.Ordinal) &&
            int.TryParse(symbol.AsSpan("MAP_FIVE_ISLAND_LOST_CAVE_ROOM".Length), NumberStyles.None,
                CultureInfo.InvariantCulture, out int room) && room is >= 1 and <= 14 &&
            symbol == "MAP_FIVE_ISLAND_LOST_CAVE_ROOM" + room.ToString(CultureInfo.InvariantCulture))
            return new("map.five-island-lost-cave-room" + room.ToString(CultureInfo.InvariantCulture));
        return symbol switch
        {
            "MAP_FIVE_ISLAND_WATER_LABYRINTH" => WaterLabyrinthMapId,
            "MAP_FIVE_ISLAND_RESORT_GORGEOUS" => ResortGorgeousMapId,
            "MAP_FIVE_ISLAND_RESORT_GORGEOUS_HOUSE" => HouseMapId,
            "MAP_FIVE_ISLAND_LOST_CAVE_ENTRANCE" => EntranceMapId,
            "MAP_FIVE_ISLAND" => new("map.five-island"),
            _ => throw new InvalidDataException("Unknown Lost Cave map symbol: " + symbol)
        };
    }

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var source = map.Events.GetProperty("connections");
        var edges = source.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty :
            source.EnumerateArray().Select(c => new ConnectedFieldConnection(
                c.GetProperty("direction").GetString() switch
                {
                    "up" => Direction.North, "down" => Direction.South,
                    _ => throw new InvalidDataException("Lost Cave has only original north/south outdoor connections.")
                }, c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }

    public static PretLostCaveRegion Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot,"lost-cave-region","scripts/lost-cave-region.lock.json");
        var catalog = source.Document("catalog.json");
        Need(            !catalog.GetProperty("runtimeIntegrated").GetBoolean() , "Lost Cave catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original Lost Cave and connecting outdoor maps; gameplay and services require separate admission.", Profile, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!,directory = spec.GetProperty("directory").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need(path == directory + "/runtime-map.json" , "Lost Cave compact map binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Outputs[path], id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.TechnicalSpawn is null,
                "Lost Cave source map identity differs.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, directory),
                PretRuntimeMapValidation.SafeFile(source.Directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            maps.Add(map.Id, map);
        }
        Need(maps.Values.Select(m => m.SourceRef.Path).ToHashSet().SetEquals(SourceNames.Select(n => "data/maps/" + n + "/map.json")),
            "Lost Cave exact eighteen-map cohort differs.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string,string>(StringComparer.Ordinal);
        var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var entry in source.Document("scripts.json").GetProperty("sourcePaths").EnumerateArray())
        {
            string path = entry.GetString()!,text = new UTF8Encoding(false,true).GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path,text);
            else
            {
                string label = Regex.Match(text,@"^(\w+)::?",RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(label.Length > 0, "Lost Cave script lacks a label.");
                scripts.Add(new(path,text,new(SourceKind.Pret,path,label,"pret/pokefirered",PretWorldMap.SourceCommit)));
            }
        }
        var constants = PretSharedConstants.ForPackage(source.Directory);
        ValidateContracts(source,maps.ToImmutable(),constants);
        return new(source,maps.ToImmutable(),scripts.ToImmutable(),texts.ToImmutable(),constants);
    }

    /// <summary>Append this binding to the shared item services only when the Selphy scene/service is admitted.</summary>
    public ImmutableArray<FieldItemServiceDefinition> ServiceDefinitions()
    {
        var binding = Contracts.GetProperty("serviceBindings")[0];
        var e = NewMaps[HouseMapId].Events.GetProperty("object_events")[0];
        string script = binding.GetProperty("scriptId").GetString()!;
        var labels = ScriptInventory.GetProperty("blocks").EnumerateArray().Select(b => b.GetProperty("label").GetString()!).ToHashSet(StringComparer.Ordinal);
        var programs = binding.GetProperty("programIds").EnumerateArray().Select(x => x.GetString()!).ToImmutableArray();
        var items = binding.GetProperty("itemSymbols").EnumerateArray().Select(x => x.GetString()!).ToImmutableArray();
        Need(binding.GetProperty("sourceLocalId").GetInt32() == 1 && binding.GetProperty("mapId").GetString() == HouseMapId.Value &&
            JsonElement.DeepEquals(e,binding.GetProperty("sourceEvent")) && e.GetProperty("script").GetString() == script &&
            programs.Contains(script) && programs.Contains("Std_ObtainItem") && programs.All(labels.Contains) &&
            programs.Distinct(StringComparer.Ordinal).Count() == programs.Length && items.Length == 7 && items.All(Constants.ContainsKey),
            "Selphy shared field-item service binding differs.");
        return [new(binding.GetProperty("id").GetString()!, HouseMapId, script, 1, null, null, programs, items)];
    }

    public void ValidateReusedMaps(IReadOnlyDictionary<MapId,PretWorldMap> existing)
    {
        var anchor = Contracts.GetProperty("retainedAnchor");
        Need(existing.TryGetValue(new("map.five-island"),out var map) &&
            map.Events.GetProperty("connections").EnumerateArray().Any(c => JsonElement.DeepEquals(c,anchor.GetProperty("connection"))),
            "Existing Five Island original north connection differs.");
    }

    private static void ValidateContracts(SilphSourcePackage source, ImmutableDictionary<MapId,PretWorldMap> maps,
        ImmutableDictionary<string,int> constants)
    {
        var contract = source.Document("contracts.json"); var declarations = source.Document("declarations.json");
        var topology = contract.GetProperty("mapTopology");
        Need(!contract.GetProperty("runtimeIntegrated").GetBoolean() && topology.GetProperty("connections").GetArrayLength() == 3 &&
            contract.GetProperty("itemPickups").GetArrayLength() == 5 && contract.GetProperty("hiddenItems").GetArrayLength() == 4 &&
            declarations.GetProperty("trainers").GetArrayLength() == 11 && declarations.GetProperty("wildEncounters").GetArrayLength() == 16 &&
            source.Document("scripts.json").GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Lost Cave complete source cohort differs.");
        foreach (var row in topology.GetProperty("warps").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(row.GetProperty("sourceMap").GetString()!)];
            var original = map.Events.GetProperty("warp_events")[row.GetProperty("sourceIndex").GetInt32()];
            var target = maps[MapIdFromSourceSymbol(original.GetProperty("dest_map").GetString()!)];
            int index = int.Parse(original.GetProperty("dest_warp_id").GetString()!,CultureInfo.InvariantCulture);
            Need(JsonElement.DeepEquals(original,row.GetProperty("source")) &&
                JsonElement.DeepEquals(target.Events.GetProperty("warp_events")[index],row.GetProperty("destination")),
                "Lost Cave original directed/reset destination differs.");
        }
        Need(contract.GetProperty("puzzle").GetProperty("resetWarps").GetArrayLength() == 28 &&
            constants["SHOWN_REQUESTED_MON"] == constants["FLAG_TEMP_2"] &&
            constants["VAR_RESORT_GOREGEOUS_STEP_COUNTER"] == 0x4035 && constants["VAR_RESORT_GORGEOUS_REQUESTED_MON"] == 0x4036 &&
            constants["VAR_RESORT_GORGEOUS_REWARD"] == 0x403B,
            "Lost Cave reset routes or original Selphy variables differ.");
        var house = maps[HouseMapId]; var cave = maps[new("map.five-island-lost-cave-room10")];
        Need(house.Events.GetProperty("object_events")[0].GetProperty("flag").GetString() == "FLAG_HIDE_RESORT_GORGEOUS_INSIDE_SELPHY" &&
            cave.Events.GetProperty("object_events")[0].GetProperty("flag").GetString() == "FLAG_HIDE_LOST_CAVE_SELPHY" &&
            !contract.GetProperty("rescue").GetProperty("runtimeAdmitted").GetBoolean() &&
            !contract.GetProperty("services").GetProperty("runtimeAdmitted").GetBoolean(), "Lost Cave service visibility/admission differs.");
    }
    private static void Need(bool value,string message) => SilphSourcePackage.Need(value,message);
}
