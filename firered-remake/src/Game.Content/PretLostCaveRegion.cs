using System.Buffers.Binary;
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
    public const string FullSourceHash = "56e98ad727f5c7209971176dd492874b5de1dce391e3b12ce92521283d27310a";
    public const string ManifestSha256 = "7e3e6f047396cfdac540701514f68a29a7c70ed96c5529c581be9ba0e2d81f59";
    public const string LockSha256 = "0f44f1ae817a5f454a097ed0d11af16e7e9a7ef37c4350d08f8a687fb48d0e03";
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
    public ImmutableDictionary<string, string> AuthenticatedMapScriptHashes { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
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
        var paths = SourceNames.Select(n => "data/maps/" + n + "/scripts.inc").ToHashSet(StringComparer.Ordinal);
        AuthenticatedMapScriptHashes = source.Files.Where(f => paths.Contains(f.Path))
            .ToImmutableDictionary(f => f.Path, f => f.Sha256, StringComparer.Ordinal);
        Need(AuthenticatedMapScriptHashes.Count == 18, "Lost Cave map-script inventory differs.");
        AdditionalSourceHashes = ["lost-cave-region-manifest:" + ManifestSha256, "lost-cave-region-lock:" + LockSha256,
            .. source.Files.Select(f => "lost-cave-region:" + f.Path + ":" + f.Sha256)];
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
        var source = new SilphSourcePackage(projectRoot, "lost-cave-region", Profile, "scripts/lost-cave-region.lock.json",
            LockSha256, 72705, ManifestSha256, 32725, 232, 178);
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("profile").GetString() == Profile && catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            !catalog.GetProperty("runtimeIntegrated").GetBoolean() && catalog.GetProperty("maps").GetArrayLength() == 18 &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")), "Lost Cave catalog identity differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.",
            "Original Lost Cave and connecting outdoor maps; gameplay and services require separate admission.", Profile, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var outputs = source.Manifest.GetProperty("outputs").EnumerateArray().ToDictionary(e => e.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!, directory = spec.GetProperty("directory").GetString()!;
            string path = spec.GetProperty("path").GetString()!, hash = spec.GetProperty("sha256").GetString()!;
            Need(path == directory + "/runtime-map.json" && hash == outputs[path].GetProperty("sha256").GetString() &&
                spec.GetProperty("byteLength").GetInt32() == source.Outputs[path].Length, "Lost Cave compact map binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, source.Outputs[path].Length, hash,
                directory + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(),
                spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
            PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id && runtime.TechnicalSpawn is null,
                "Lost Cave source map identity differs.");
            foreach (var artifact in runtime.Artifacts)
                Need(outputs[directory + "/" + artifact.Path].GetProperty("sha256").GetString() == artifact.Sha256,
                    "Lost Cave rendered image binding differs.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, directory),
                PretRuntimeMapValidation.SafeFile(source.Directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
            ValidateGrid(source, catalog, map);
            maps.Add(map.Id, map);
        }
        Need(maps.Values.Select(m => m.SourceRef.Path).ToHashSet().SetEquals(SourceNames.Select(n => "data/maps/" + n + "/map.json")),
            "Lost Cave exact eighteen-map cohort differs.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string,string>(StringComparer.Ordinal);
        var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var entry in source.Document("scripts.json").GetProperty("sourcePaths").EnumerateArray())
        {
            string path = entry.GetString()!, text = new UTF8Encoding(false,true).GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path,text);
            else
            {
                string label = Regex.Match(text,@"^(\w+)::?",RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(label.Length > 0, "Lost Cave script lacks a label.");
                scripts.Add(new(path,text,new(SourceKind.Pret,path,label,"pret/pokefirered",PretWorldMap.SourceCommit,files[path].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name,p => p.Value.GetProperty("value").GetInt32(),StringComparer.Ordinal);
        ValidateContracts(source,maps.ToImmutable(),constants);
        return new(source,maps.ToImmutable(),scripts.ToImmutable(),texts.ToImmutable(),constants);
    }

    private static void ValidateGrid(SilphSourcePackage source, JsonElement catalog, PretWorldMap map)
    {
        var layouts = SilphSourcePackage.Json(source.Raw["data/layouts/layouts.json"]);
        var layout = layouts.GetProperty("layouts").EnumerateArray().Single(l => l.TryGetProperty("id",out var id) &&
            id.GetString() == map.Events.GetProperty("layout").GetString());
        Need(map.Width == layout.GetProperty("width").GetInt32() && map.Height == layout.GetProperty("height").GetInt32(),
            "Lost Cave original dimensions differ.");
        foreach (var (cells,path) in new[] { (map.Cells,layout.GetProperty("blockdata_filepath").GetString()!),
            (map.BorderCells,layout.GetProperty("border_filepath").GetString()!) })
        {
            var bytes = source.Raw[path]; Need(bytes.Length == cells.Length * 2,"Lost Cave source block length differs.");
            for (int i=0;i<cells.Length;i++)
            {
                ushort block = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i*2)); int mid = block & 1023;
                string tileset = catalog.GetProperty("tilesets").GetProperty(layout.GetProperty(mid < 640 ? "primary_tileset" : "secondary_tileset").GetString()!).GetString()!;
                uint attr = BinaryPrimitives.ReadUInt32LittleEndian(source.Raw["data/tilesets/"+tileset+"/metatile_attributes.bin"].AsSpan((mid < 640 ? mid : mid-640)*4));
                Need(cells[i].RawBlock == block && cells[i].RawAttributes == attr && cells[i].Collision == ((block >> 10)&3) &&
                    cells[i].Elevation == block >> 12,"Lost Cave original grid/attributes differ.");
            }
        }
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
            map.SourceRef.Sha256 == anchor.GetProperty("sourceRef").GetProperty("sha256").GetString() &&
            map.Events.GetProperty("connections").EnumerateArray().Any(c => JsonElement.DeepEquals(c,anchor.GetProperty("connection"))),
            "Existing Five Island original north connection differs.");
    }

    private static void ValidateContracts(SilphSourcePackage source, ImmutableDictionary<MapId,PretWorldMap> maps,
        ImmutableDictionary<string,int> constants)
    {
        var contract = source.Document("contracts.json"); var declarations = source.Document("declarations.json");
        var topology = contract.GetProperty("mapTopology");
        Need(!contract.GetProperty("runtimeIntegrated").GetBoolean() && maps.Values.Sum(m => m.Cells.Length) == 4794 &&
            maps.Values.Sum(m => m.FieldWarps.Length) == 47 && topology.GetProperty("connections").GetArrayLength() == 3 &&
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
            contract.GetProperty("puzzle").GetProperty("dynamicWarpCount").GetInt32() == 0 &&
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
