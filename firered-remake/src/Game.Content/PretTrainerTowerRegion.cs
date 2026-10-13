using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Original map identity is stable across Single, Double and Knockout layouts. Mixed selects a format per floor.</summary>
public enum PretTrainerTowerLayoutFormat { Common = -1, Single = 0, Double = 1, Knockout = 2 }

/// <summary>Authenticated Trainer Tower resources; loading these assets does not admit a challenge or alter current composition.</summary>
public sealed class PretTrainerTowerRegion
{
    public const string Profile = "firered-trainer-tower-region-v1";
    public const string FullSourceHash = "35a6334484aa7e743b88a9c5874a955d01a893e300415c210fe229b8bed92a3a";
    public const string ManifestSha256 = "025b657cc68b7771c52593583af4ebb35d1316ef9e6754e86b3de1331dca85ef";
    public const string LockSha256 = "c5a2f22dbc2cbaf77b54d686f006b95d59d713eab622a634018f9481cc0ba8f1";
    public ImmutableDictionary<MapId, PretWorldMap> Maps { get; }
    public ImmutableDictionary<(MapId Map, PretTrainerTowerLayoutFormat Format), PretWorldMap> Layouts { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretTrainerTowerRegion(SilphSourcePackage source,
        ImmutableDictionary<(MapId Map, PretTrainerTowerLayoutFormat Format), PretWorldMap> layouts,
        ImmutableArray<PretSourceScriptFile> scripts, ImmutableDictionary<string, string> texts)
    {
        Layouts = layouts;
        Maps = layouts.Where(p => p.Key.Format is PretTrainerTowerLayoutFormat.Common or PretTrainerTowerLayoutFormat.Single)
            .ToImmutableDictionary(p => p.Key.Map, p => p.Value);
        Scripts = scripts; TextSources = texts; SourceFiles = source.Files; GeneratedDirectory = source.Directory;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); NpcSources = source.Document("npc-source.json");
        Constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        AdditionalSourceHashes = ["trainer-tower-region-manifest:" + ManifestSha256, "trainer-tower-region-lock:" + LockSha256,
            .. source.Files.Select(f => "trainer-tower-region:" + f.Path + ":" + f.Sha256)];
    }

    /// <summary>format is the floor's resolved format (0..2), never the Mixed course value 3.</summary>
    public PretWorldMap GetMap(MapId map, int format) => GetMap(map, (PretTrainerTowerLayoutFormat)format);
    public PretWorldMap GetMap(MapId map, PretTrainerTowerLayoutFormat format)
    {
        Need(Enum.IsDefined(format), "Mixed is a course, not a Trainer Tower layout format.");
        if (Layouts.TryGetValue((map, PretTrainerTowerLayoutFormat.Common), out var common)) return common;
        return Layouts.TryGetValue((map, format), out var floor) ? floor : throw new InvalidDataException("Unknown Trainer Tower map/format.");
    }
    public PretWorldMap GetMap(MapId map, TrainerTowerBattleFormat format) => GetMap(map, (int)format);
    /// <summary>Original dynamic object variable slots mapped to source graphics symbols.</summary>
    public ImmutableDictionary<int, string> NpcGraphics(TrainerTowerBattleFormat format, TrainerTowerFloorTemplate template)
    {
        Need(Enum.IsDefined(format) && template.Format == format && !template.Trainers.IsDefaultOrEmpty, "Tower NPC template format differs.");
        var data = Contracts.GetProperty("dynamicGraphics");
        string Single(string facility) => data.GetProperty("single").EnumerateArray()
            .Where(r => r.GetProperty("facilityClass").GetString() == facility)
            .Select(r => r.GetProperty("graphicsId").GetString()!).FirstOrDefault() ?? data.GetProperty("default").GetString()!;
        var result = ImmutableDictionary.CreateBuilder<int, string>();
        if (format == TrainerTowerBattleFormat.Single) result.Add(1, Single(template.Trainers[0].FacilityClass));
        else if (format == TrainerTowerBattleFormat.Double)
        {
            var row = data.GetProperty("double").EnumerateArray().FirstOrDefault(r => r.GetProperty("facilityClass").GetString() == template.Trainers[0].FacilityClass);
            result.Add(0, row.ValueKind == JsonValueKind.Undefined ? data.GetProperty("default").GetString()! : row.GetProperty("graphicsId1").GetString()!);
            result.Add(3, row.ValueKind == JsonValueKind.Undefined ? data.GetProperty("default").GetString()! : row.GetProperty("graphicsId2").GetString()!);
        }
        else
        {
            Need(template.Trainers.Length == 3, "Knockout requires all three original trainer graphics.");
            int[] slots = [2, 0, 1];
            for (int i = 0; i < 3; i++) result.Add(slots[i], Single(template.Trainers[i].FacilityClass));
        }
        return result.ToImmutable();
    }
    public int GraphicsIdValue(string symbol) => Constants.TryGetValue(symbol, out int value) && symbol.StartsWith("OBJ_EVENT_GFX_", StringComparison.Ordinal)
        ? value : throw new InvalidDataException("Unknown Tower graphics symbol: " + symbol);
    public ImmutableDictionary<string, int> NpcGraphicsValues(TrainerTowerBattleFormat format, TrainerTowerFloorTemplate template) =>
        NpcGraphics(format, template).ToImmutableDictionary(p => "VAR_OBJ_GFX_ID_" + p.Key, p => GraphicsIdValue(p.Value), StringComparer.Ordinal);
    /// <summary>Retains the original numeric lookup comparison against converted trainer class, including its source table quirk.</summary>
    public string EncounterMusic(TrainerTowerFloorTemplate template, int trainerIndex)
    {
        Need(trainerIndex >= 0 && trainerIndex < template.Trainers.Length, "Tower music trainer index is outside the floor template.");
        var data = Contracts.GetProperty("encounterMusic"); int trainerClass = template.Trainers[trainerIndex].SourceTrainerClass;
        var match = data.GetProperty("lookup").EnumerateArray().FirstOrDefault(r => r.GetProperty("comparisonValue").GetInt32() == trainerClass);
        int index = match.ValueKind == JsonValueKind.Undefined ? data.GetProperty("defaultMusicIndex").GetInt32() : match.GetProperty("musicIndex").GetInt32();
        return data.GetProperty("songs").EnumerateArray().Single(r => r.GetProperty("musicIndex").GetInt32() == index).GetProperty("song").GetString()!;
    }
    public string LayoutId(MapId map, PretTrainerTowerLayoutFormat format)
    {
        var selected = GetMap(map, format);
        return Catalog.GetProperty("maps").EnumerateArray().Single(s =>
            s.GetProperty("directory").GetString() == Path.GetRelativePath(GeneratedDirectory, selected.DirectoryPath).Replace('\\', '/'))
            .GetProperty("layoutId").GetString()!;
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_TRAINER_TOWER_LOBBY" => new("map.trainer-tower-lobby"),
        "MAP_TRAINER_TOWER_ROOF" => new("map.trainer-tower-roof"),
        "MAP_TRAINER_TOWER_ELEVATOR" => new("map.trainer-tower-elevator"),
        "MAP_SEVEN_ISLAND_TRAINER_TOWER" => new("map.seven-island-trainer-tower"),
        "MAP_DYNAMIC" => PretRocketHideoutRegion.DynamicMapId,
        _ when Regex.IsMatch(symbol, "^MAP_TRAINER_TOWER_[1-8]F$", RegexOptions.CultureInvariant) => new("map.trainer-tower-" + symbol[^2..].ToLowerInvariant()),
        _ => throw new InvalidDataException("Unknown Trainer Tower map symbol: " + symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Tower indoor connection scope differs.");
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    public static PretTrainerTowerRegion Load(string projectRoot)
    {
        var source = Source(projectRoot);
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")), "Tower source catalog binding differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.", "Trainer Tower resources; runtime admission is separate.", Profile, source.Files);
        var raw = source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var originalLayouts = SilphSourcePackage.Json(raw["data/layouts/layouts.json"]).GetProperty("layouts").EnumerateArray()
            .Where(l => l.TryGetProperty("id", out _)).ToDictionary(l => l.GetProperty("id").GetString()!, StringComparer.Ordinal);
        var layouts = ImmutableDictionary.CreateBuilder<(MapId Map, PretTrainerTowerLayoutFormat Format), PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string relative = spec.GetProperty("directory").GetString()!, path = spec.GetProperty("path").GetString()!;
            string id = spec.GetProperty("stableId").GetString()!, hash = spec.GetProperty("sha256").GetString()!;
            Need(path == relative + "/runtime-map.json", "Tower map path differs.");
            var runtime = PretRuntimeMapData.Decode(source.Outputs[path], new(id, path, spec.GetProperty("byteLength").GetInt32(), hash,
                relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(), spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
            PretRuntimeMapIntegration.ValidateSource(runtime, metadata, raw, spec);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id, "Tower source map identity differs.");
            var format = Enum.Parse<PretTrainerTowerLayoutFormat>(spec.GetProperty("format").GetString()!);
            string originalId = runtime.Events.GetProperty("layout").GetString()!;
            string expectedId = originalId + (format == PretTrainerTowerLayoutFormat.Double ? "_DOUBLES" : format == PretTrainerTowerLayoutFormat.Knockout ? "_KNOCKOUT" : "");
            Need(spec.GetProperty("layoutId").GetString() == expectedId, "Tower map/format layout binding differs.");
            var layout = originalLayouts[expectedId];
            Need(runtime.Width == layout.GetProperty("width").GetInt32() && runtime.Height == layout.GetProperty("height").GetInt32() &&
                runtime.BorderWidth == layout.GetProperty("border_width").GetInt32() && runtime.BorderHeight == layout.GetProperty("border_height").GetInt32(), "Tower layout dimensions differ.");
            ValidateCells(runtime.Blocks, runtime.ExpandCells(), raw[layout.GetProperty("blockdata_filepath").GetString()!], raw);
            ValidateCells(runtime.BorderBlocks, runtime.ExpandBorderCells(), raw[layout.GetProperty("border_filepath").GetString()!], raw);
            foreach (var artifact in runtime.Artifacts)
                Need(SafariSource.Hash(source.Outputs[relative + "/" + artifact.Path]) == artifact.Sha256, "Tower image binding differs.");
            layouts.Add((new(id), format), PretWorldMap.FromRuntime(runtime, metadata, Path.Combine(source.Directory, relative),
                Path.Combine(source.Directory, "npc/red-normal.png"), MapIdFromSourceSymbol));
        }
        Need(layouts.Count == 27 && layouts.Keys.Select(k => k.Map).Distinct().Count() == 11, "Tower map/layout count differs.");
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 294 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0, "Tower script scope differs.");
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
                Need(label.Length > 0, "Missing Tower script label.");
                scripts.Add(new(path, text, new(SourceKind.Pret, path, label, "pret/pokefirered", PretWorldMap.SourceCommit, files[path].Sha256)));
            }
        }
        return new(source, layouts.ToImmutable(), scripts.ToImmutable(), texts.ToImmutable());
    }
    /// <summary>Authenticates all resource bytes without requiring current map composition.</summary>
    public static void ValidateEvidence(string projectRoot) => _ = Source(projectRoot);
    private static SilphSourcePackage Source(string root) => new(root, "trainer-tower-region", Profile,
        "scripts/trainer-tower-region.lock.json", LockSha256, 65322, ManifestSha256, 50683, 214, 286);
    private static void ValidateCells(ImmutableArray<ushort> blocks, ImmutableArray<PretWorldCell> cells, byte[] source, Dictionary<string, byte[]> raw)
    {
        Need(source.Length == blocks.Length * 2 && cells.Length == blocks.Length, "Tower grid length differs.");
        for (int i = 0; i < blocks.Length; i++)
        {
            Need(blocks[i] == BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(i * 2)), "Tower raw grid differs.");
            var cell = cells[i]; int local = cell.MetatileId < 640 ? cell.MetatileId : cell.MetatileId - 640;
            Need(cell.RawAttributes == BinaryPrimitives.ReadUInt32LittleEndian(raw[cell.SourceRef.Path].AsSpan(local * 4)), "Tower attributes differ.");
        }
    }
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
