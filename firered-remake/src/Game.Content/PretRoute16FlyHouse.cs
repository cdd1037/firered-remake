using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed record PretRoute16FlyTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);

/// <summary>One original house; existing Route16 and the divided gate retain their own identities. Runtime admission is separate.</summary>
public sealed class PretRoute16FlyHouse
{
    public const string ResourceId = "firered-route16-fly-house-v1";
    public const string ManifestSha256 = "d69e9d907b448de13de43a6b09f1d22cc860a6f93ff3f8d962d252a836de4585";
    public const string LockSha256 = "f9f56e2d9f048df41877886595416126304eee3d7b15078666865c175668909f";
    public const string FullSourceHash = "b2a533ebae6f6197f4b214460e45a215cdb827542daa47a1abc0e02fb35a9597";
    public static MapId HouseMapId { get; } = new("map.route-16-house");
    public static MapId RouteMapId { get; } = new("map.route-16");
    public static MapId GateMapId { get; } = new("map.route-16-north-entrance-1f");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public PretRoute16FlyTrainerDocument Trainers { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    private readonly ImmutableDictionary<MapId, ImmutableHashSet<(int X, int Y)>> _upperCells;
    private readonly ImmutableDictionary<MapId, JsonElement> _retained;

    private PretRoute16FlyHouse(Source source, PretWorldMap house, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableDictionary<MapId, JsonElement> retained, PretRoute16FlyTrainerDocument trainers)
    {
        NewMaps = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(HouseMapId, house);
        Scripts = scripts; TextSources = texts; Constants = constants; SourceFiles = source.Files;
        GeneratedDirectory = source.Directory; Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json"); NpcSources = source.Document("npc-source.json");
        Trainers = trainers; _retained = retained;
        _upperCells = Contracts.GetProperty("upperFootAdmissions").EnumerateArray().ToImmutableDictionary(
            r => new MapId(r.GetProperty("map").GetString()!),
            r => r.GetProperty("cells").EnumerateArray().Select(c => (c[0].GetInt32(), c[1].GetInt32())).ToImmutableHashSet());
        Need(_upperCells[RouteMapId].Count == 178 && _upperCells[GateMapId].Count == 44, "Upper passage cell count differs.");
        foreach (var (id, cells) in _upperCells)
        {
            var original = retained[id]; int width = original.GetProperty("width").GetInt32();
            var expected = original.GetProperty("cells").EnumerateArray().Where(c =>
                c.GetProperty("collision").GetInt32() == 0 && c.GetProperty("elevation").GetInt32() == 3 &&
                (id == RouteMapId ? (c.GetProperty("y").GetInt32() is >= 5 and <= 10 && c.GetProperty("x").GetInt32() is >= 2 and <= 41) || (c.GetProperty("x").GetInt32(), c.GetProperty("y").GetInt32()) == (41, 11)
                                 : c.GetProperty("y").GetInt32() is >= 2 and <= 5) &&
                c.GetProperty("behaviorName").GetString() is "MB_NORMAL" or "MB_TALL_GRASS" or "MB_WEST_ARROW_WARP" or "MB_EAST_ARROW_WARP")
                .Select(c => (c.GetProperty("x").GetInt32(), c.GetProperty("y").GetInt32())).ToHashSet();
            Need(cells.SetEquals(expected) && width > 0, "Upper passage must retain exact source collision/elevation.");
        }
        AdditionalSourceHashes = ["route16-fly-house-manifest:" + ManifestSha256, "route16-fly-house-lock:" + LockSha256,
            .. source.Files.Select(f => "route16-fly-house:" + f.Path + ":" + f.Sha256)];
    }

    /// <summary>Additive preflight only. Original Cut, actors, trainer sight, encounters and collision remain mandatory.</summary>
    public bool AllowsAdditionalFootDestination(MapId map, int x, int y) =>
        _upperCells.TryGetValue(map, out var cells) && cells.Contains((x, y));

    /// <summary>Only the original north door chain; lower cycling doors and stairs remain excluded.</summary>
    public bool AllowsWarp(MapId map, int sourceWarpId) => map == HouseMapId ? sourceWarpId is >= 0 and <= 2 :
        map == RouteMapId ? sourceWarpId is >= 0 and <= 2 : map == GateMapId && sourceWarpId is 0 or 1;

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == HouseMapId && map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Unexpected Fly house connection.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }

    /// <summary>Call before composing. This validates the retained instances without replacing or republishing them.</summary>
    public void ValidateReusedMaps(IReadOnlyDictionary<MapId, PretWorldMap> maps)
    {
        foreach (var row in Contracts.GetProperty("retainedMaps").EnumerateArray())
        {
            var id = new MapId(row.GetProperty("stableId").GetString()!);
            Need(maps.TryGetValue(id, out var map), "Missing retained north-passage map.");
            var original = _retained[id]; var events = original.GetProperty("events");
            Need(map!.Metadata.FullSourceHash == PretSnorlaxRegion.FullSourceHash &&
                map.SourceRef.Commit == PretWorldMap.SourceCommit && map.SourceRef.Symbol == row.GetProperty("sourceMap").GetString() &&
                map.SourceRef.Sha256 == original.GetProperty("sourceRef").GetProperty("sha256").GetString() &&
                map.Width == original.GetProperty("width").GetInt32() && map.Height == original.GetProperty("height").GetInt32() &&
                JsonElement.DeepEquals(map.Events, events), "Retained north-passage identity/events differ.");
            foreach (var c in original.GetProperty("cells").EnumerateArray())
            {
                var cell = map.GetSourceCell(c.GetProperty("x").GetInt32(), c.GetProperty("y").GetInt32());
                Need(cell is not null && cell.RawBlock == c.GetProperty("rawBlock").GetUInt16() &&
                    cell.RawAttributes == c.GetProperty("rawAttributes").GetUInt32(), "Retained north-passage geometry differs.");
            }
        }
    }

    public static PretRoute16FlyHouse Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot; var source = new Source(projectRoot, dependencyRoot);
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash && catalog.GetProperty("maps").GetArrayLength() == 1 &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")), "Fly house catalog differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.", "One house; north path admission remains separate.", ResourceId, source.Files);
        var spec = catalog.GetProperty("maps")[0]; string path = spec.GetProperty("path").GetString()!;
        var bytes = source.Outputs[path];
        Need(path == "route-16-house/runtime-map.json" && spec.GetProperty("stableId").GetString() == HouseMapId.Value &&
            spec.GetProperty("sha256").GetString() == SafariSource.Hash(bytes) && spec.GetProperty("byteLength").GetInt32() == bytes.Length,
            "Fly house compact map identity differs.");
        var house = LoadHouse(source, metadata, spec, dependencyRoot);
        Need(house.Width == 11 && house.Height == 9 && house.FieldWarps.Length == 3 &&
            house.Events.GetProperty("object_events").GetArrayLength() == 2 && house.Events.GetProperty("coord_events").GetArrayLength() == 0 &&
            house.Events.GetProperty("bg_events").GetArrayLength() == 0 && house.Events.GetProperty("layout").GetString() == "LAYOUT_HOUSE1",
            "Original Fly house geometry/event cohort differs.");
        ValidateHouseCells(house, source.Raw);
        var retained = ImmutableDictionary.CreateBuilder<MapId, JsonElement>();
        foreach (var anchor in source.Document("contracts.json").GetProperty("retainedMaps").EnumerateArray())
        {
            _ = SafariSource.Read(dependencyRoot, anchor.GetProperty("manifestPath").GetString()!, anchor.GetProperty("manifestSha256").GetString()!, anchor.GetProperty("manifestByteLength").GetInt32());
            var original = SilphSourcePackage.Json(SafariSource.Read(dependencyRoot, anchor.GetProperty("resourcePath").GetString()!,
                anchor.GetProperty("resourceSha256").GetString()!, anchor.GetProperty("resourceByteLength").GetInt32()));
            Need(JsonElement.DeepEquals(original.GetProperty("events"), SilphSourcePackage.Json(source.Raw["data/maps/" + anchor.GetProperty("sourceName").GetString() + "/map.json"])), "Original retained map events differ.");
            retained.Add(new(anchor.GetProperty("stableId").GetString()!), original);
        }
        Need(retained.Keys.ToHashSet().SetEquals([RouteMapId, GateMapId]), "North-passage retained map cohort differs.");
        ValidateTopology(source.Document("contracts.json"), house, retained);
        var npcs = source.Document("npc-source.json").GetProperty("graphics");
        Need(npcs.GetArrayLength() == 3, "Fly house actor textures differ.");
        foreach (var texture in npcs.EnumerateArray())
        {
            bool reused = texture.GetProperty("reuseExisting").GetBoolean();
            Need(reused == (texture.GetProperty("graphicsId").GetString() != "OBJ_EVENT_GFX_FEAROW"), "Fly house resource reuse differs.");
            _ = SafariSource.Read(reused ? dependencyRoot : source.Directory, texture.GetProperty("texturePath").GetString()!,
                texture.GetProperty("sha256").GetString()!, texture.GetProperty("byteLength").GetInt32());
        }
        var inventory = source.Document("scripts.json");
        Need(inventory.GetProperty("blocks").GetArrayLength() == 40 && inventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0, "Fly house script closure differs.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>(); var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var entry in inventory.GetProperty("sourcePaths").EnumerateArray())
        {
            string sourcePath = entry.GetString()!, text = new UTF8Encoding(false, true).GetString(source.Raw[sourcePath]);
            if (sourcePath.EndsWith("/text.inc", StringComparison.Ordinal) || sourcePath.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(sourcePath, text);
            else
            {
                string label = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Need(label.Length > 0, "Fly house source label missing.");
                scripts.Add(new(sourcePath, text, new(SourceKind.Pret, sourcePath, label, "pret/pokefirered", PretWorldMap.SourceCommit, files[sourcePath].Sha256)));
            }
        }
        var constants = source.Document("constants.json").GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetProperty("value").GetInt32(), StringComparer.Ordinal);
        Need(constants["ITEM_HM02"] == 340 && constants["FLAG_GOT_HM02"] == 0x238, "Original HM02 gift constants differ.");
        var trainers = SafariSource.Parse<PretRoute16FlyTrainerDocument>(source.Outputs["trainers.json"]);
        Need(trainers.SourceCommit == PretWorldMap.SourceCommit && trainers.Profile == ResourceId && !trainers.CompleteSourceRoster &&
            trainers.Trainers.Length == 1 && trainers.Battles.Length == 2, "Upper path trainer scope differs.");
        var trainer = trainers.Trainers[0];
        Need(trainer.Id.Value == "pret:TRAINER_YOUNG_COUPLE_LEA_JED" && trainer.SourceTrainerId == 489 && trainer.DoubleBattle &&
            trainer.AiFlags == 1 && trainer.PartyFlags == 0 && trainer.Party.Length == 2 && trainer.Party.All(m => m.Level == 29 && m.SourceIv == 0 && m.HeldItem == "ITEM_NONE") &&
            trainer.Party.Select(m => m.Species.Value).SequenceEqual(["pret:SPECIES_RAPIDASH", "pret:SPECIES_NINETALES"]), "Original Lea/Jed preset party differs.");
        Need(trainers.Battles.All(b => b.Trainer == trainer.Id && b.Mode == 4 && b.BattleTypeFlags == 9 && b.EntryScript == "EventScript_TryDoDoubleTrainerBattle" &&
            b.Callback == "CB2_EndTrainerBattle" && b.NotEnoughMonsText is not null) && trainers.Battles.Select(b => b.ScriptEntry).ToHashSet()
            .SetEquals(["Route16_EventScript_Lea", "Route16_EventScript_Jed"]), "Original shared-double interactions differ.");
        return new(source, house, scripts.ToImmutable(), texts.ToImmutable(), constants, retained.ToImmutable(), trainers);
    }

    // The existing regional-service loader uses this authenticated expansion path
    // for new profiles, avoiding edits to the global compact-resolver registry.
    private static PretWorldMap LoadHouse(Source source, PretWorldMetadata metadata, JsonElement spec, string dependencyRoot)
    {
        var data = PretRuntimeMapValidation.Decode<PretRuntimeMapDocument>(source.Outputs["route-16-house/runtime-map.json"]);
        var raw = source.Raw; var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        Need(data.SchemaVersion == 1 && data.ConverterVersion == PretRuntimeMapCatalog.ConverterVersion && data.SourceCommit == PretWorldMap.SourceCommit &&
            data.FullSourceHash == FullSourceHash && data.SourceWorldSha256 == spec.GetProperty("sourceWorldSha256").GetString() &&
            data.StableId == HouseMapId.Value && data.TechnicalSpawn is null && data.TileSize == 16 &&
            (data.Width, data.Height, data.BorderWidth, data.BorderHeight) == (11, 9, 2, 2), "Fly house compact header differs.");
        Need(data.SourceRef.Kind == SourceKind.Pret && data.SourceRef.Repository == "pret/pokefirered" && data.SourceRef.Commit == PretWorldMap.SourceCommit &&
            data.SourceRef.Path == "data/maps/Route16_House/map.json" && data.SourceRef.Symbol == "MAP_ROUTE16_HOUSE" &&
            data.SourceRef.Sha256 == files[data.SourceRef.Path].Sha256 &&
            JsonElement.DeepEquals(data.Events, SilphSourcePackage.Json(raw[data.SourceRef.Path])), "Fly house original event source differs.");
        var behaviors = data.Behaviors.ToDictionary(b => b.Id, b => b.Name); var metatiles = data.Metatiles.ToDictionary(r => checked((int)r[0]));
        var originalNames = Regex.Matches(Encoding.UTF8.GetString(raw["include/constants/metatile_behaviors.h"]),
            @"^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .ToDictionary(m => Convert.ToInt32(m.Groups[2].Value, 16), m => m.Groups[1].Value);
        ImmutableArray<PretWorldCell> Cells(string encoded, string path, int width, int height)
        {
            var blocks = Convert.FromBase64String(encoded);
            Need(Convert.ToBase64String(blocks) == encoded && blocks.AsSpan().SequenceEqual(raw[path]) && blocks.Length == width * height * 2, "Fly house source blocks differ.");
            var cells = ImmutableArray.CreateBuilder<PretWorldCell>();
            for (int i = 0; i < width * height; i++)
            {
                ushort block = BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan(i * 2)); var (mid, collision, elevation) = PretWorldDecoder.DecodeBlock(block);
                var row = metatiles[mid]; Need(row.Length == 3 && row[2] < data.AttributeSources.Length, "Fly house metatile row invalid.");
                var reference = data.AttributeSources[(int)row[2]]; int local = mid < 640 ? mid : mid - 640;
                string expected = "data/tilesets/" + (mid < 640 ? "primary/building" : "secondary/generic_building_1") + "/metatile_attributes.bin";
                Need(reference.Path == expected && reference.Sha256 == files[expected].Sha256, "Fly house attribute source differs.");
                uint attribute = BinaryPrimitives.ReadUInt32LittleEndian(raw[expected].AsSpan(local * 4)); var a = PretWorldDecoder.DecodeAttributes(attribute);
                Need(row[1] == attribute && a.LayerType <= 2 && behaviors[a.BehaviorId] == originalNames.GetValueOrDefault(a.BehaviorId, $"UNKNOWN_0x{a.BehaviorId:X3}"), "Fly house source attributes differ.");
                cells.Add(new(i % width, i / width, block, mid, collision, elevation, attribute, a.BehaviorId, behaviors[a.BehaviorId], a.Terrain,
                    a.Attribute2, a.Attribute3, a.EncounterType, a.Attribute5, a.LayerType, a.Attribute7, PretWorldDecoder.Movement(a.BehaviorId),
                    new(SourceKind.Pret, expected, $"metatile_{local:x3}", "pret/pokefirered", PretWorldMap.SourceCommit, reference.Sha256)));
            }
            return cells.ToImmutable();
        }
        var cells = Cells(data.Blocks, "data/layouts/House1/map.bin", 11, 9); var border = Cells(data.BorderBlocks, "data/layouts/House1/border.bin", 2, 2);
        Need(metatiles.Keys.ToHashSet().SetEquals(cells.Concat(border).Select(c => c.MetatileId)) &&
            behaviors.Keys.ToHashSet().SetEquals(cells.Concat(border).Select(c => c.BehaviorId)), "Fly house compact tables differ.");
        var embedded = ImmutableArray.CreateBuilder<PretWorldEmbeddedSource>();
        foreach (var script in data.Scripts)
        {
            Need(script.Path.StartsWith("data/maps/Route16_House/", StringComparison.Ordinal) && script.Sha256 == files[script.Path].Sha256 &&
                Encoding.UTF8.GetBytes(script.Text).AsSpan().SequenceEqual(raw[script.Path]), "Fly house embedded script differs.");
            embedded.Add(new(script.Path, script.Sha256, Convert.ToBase64String(raw[script.Path])));
        }
        Need(data.Scripts.Any(s => s.Path == "data/maps/Route16_House/scripts.inc") && data.TexturePath == "map.png" && data.Artifacts.Length == 8, "Fly house script/artifact scope differs.");
        foreach (var artifact in data.Artifacts)
            Need(SafariSource.Hash(source.Outputs["route-16-house/" + artifact.Path]) == artifact.Sha256 &&
                (artifact.Path.StartsWith("border", StringComparison.Ordinal) ? (artifact.Width, artifact.Height) == (32, 32) : (artifact.Width, artifact.Height) == (176, 144)), "Fly house image differs.");
        var world = new PretWorldDocument(1, HouseMapId.Value, data.SourceRef, metadata, 11, 9, 16, 2, 2,
            data.TexturePath, data.Artifacts, cells, border, embedded.ToImmutable(), null, data.Events.Clone());
        return PretWorldMap.FromAuthenticatedExtension(world, Path.Combine(source.Directory, "route-16-house"),
            PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/city-services/npc/red-normal.png"), PretSnorlaxRegion.MapIdFromSourceSymbol);
    }

    private static void ValidateHouseCells(PretWorldMap map, Dictionary<string, byte[]> raw)
    {
        foreach (var (cells, path) in new[] { (map.Cells, "data/layouts/House1/map.bin"), (map.BorderCells, "data/layouts/House1/border.bin") })
        {
            Need(raw[path].Length == cells.Length * 2, "Fly house block count differs.");
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i]; ushort block = BinaryPrimitives.ReadUInt16LittleEndian(raw[path].AsSpan(i * 2));
                string set = cell.MetatileId < 640 ? "primary/building" : "secondary/generic_building_1";
                int offset = (cell.MetatileId < 640 ? cell.MetatileId : cell.MetatileId - 640) * 4;
                Need(cell.RawBlock == block && (cell.MetatileId, cell.Collision, cell.Elevation) == PretWorldDecoder.DecodeBlock(block) &&
                    cell.RawAttributes == BinaryPrimitives.ReadUInt32LittleEndian(raw["data/tilesets/" + set + "/metatile_attributes.bin"].AsSpan(offset)),
                    "Fly house original collision/metatile differs.");
            }
        }
    }

    private static void ValidateTopology(JsonElement contract, PretWorldMap house, ImmutableDictionary<MapId, JsonElement>.Builder retained)
    {
        JsonElement Events(string symbol) => symbol == "MAP_ROUTE16_HOUSE" ? house.Events : retained[PretSnorlaxRegion.MapIdFromSourceSymbol(symbol)].GetProperty("events");
        Need(contract.GetProperty("topology").GetArrayLength() == 8 && !contract.GetProperty("runtimeIntegrated").GetBoolean(), "Fly house source topology scope differs.");
        foreach (var edge in contract.GetProperty("topology").EnumerateArray())
        {
            string symbol = edge.GetProperty("sourceMap").GetString()!; int index = edge.GetProperty("sourceIndex").GetInt32();
            var source = Events(symbol).GetProperty("warp_events")[index];
            string destination = source.GetProperty("dest_map").GetString()!; int target = int.Parse(source.GetProperty("dest_warp_id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            var back = Events(destination).GetProperty("warp_events")[target];
            Need(JsonElement.DeepEquals(source, edge.GetProperty("source")) && JsonElement.DeepEquals(back, edge.GetProperty("destination")) &&
                back.GetProperty("dest_map").GetString() == symbol && (back.GetProperty("dest_warp_id").GetString() == index.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                    symbol == "MAP_ROUTE16_HOUSE" && target == 0 && back.GetProperty("dest_warp_id").GetString() == "1"), "Original north-passage warp link differs.");
        }
    }

    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);

    // Sources already owned by an earlier package are authenticated in place.
    // Exactly three source blobs are new, so no tileset/resource tree is copied.
    private sealed class Source
    {
        internal string Directory { get; }
        internal JsonElement Lock { get; }
        internal Dictionary<string, byte[]> Raw { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, byte[]> Outputs { get; } = new(StringComparer.Ordinal);
        internal ImmutableArray<PretWorldSourceFile> Files { get; }
        internal Source(string root, string dependency)
        {
            Directory = Path.GetFullPath(Path.Combine(root, "content/generated/route16-fly-house"));
            Lock = SilphSourcePackage.Json(SafariSource.Read(root, "scripts/route16-fly-house.lock.json", LockSha256, 34223));
            var manifest = SilphSourcePackage.Json(SafariSource.Read(root, "content/generated/route16-fly-house/manifest.json", ManifestSha256, 3608));
            Need(Lock.GetProperty("commit").GetString() == PretWorldMap.SourceCommit && Lock.GetProperty("tree").GetString() == SilphSourcePackage.SourceTree &&
                Lock.GetProperty("profile").GetString() == ResourceId && manifest.GetProperty("profile").GetString() == ResourceId &&
                manifest.GetProperty("lockSha256").GetString() == LockSha256 && manifest.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
                Lock.GetProperty("files").GetArrayLength() == 80 && manifest.GetProperty("outputs").GetArrayLength() == 16,
                "Fly house source package identity differs.");
            var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>(); int own = 0;
            foreach (var row in Lock.GetProperty("files").EnumerateArray())
            {
                string path = row.GetProperty("path").GetString()!, hash = row.GetProperty("sha256").GetString()!, blob = row.GetProperty("gitBlobSha1").GetString()!;
                string storage = row.GetProperty("storagePath").GetString()!; bool reused = row.GetProperty("reuseExisting").GetBoolean();
                Need(storage.StartsWith("content/raw/", StringComparison.Ordinal) && storage.EndsWith("/" + path, StringComparison.Ordinal) &&
                    (reused || storage == "content/raw/route16-fly-house/" + path), "Fly house source storage binding differs.");
                int length = row.GetProperty("byteLength").GetInt32(); var bytes = SafariSource.Read(reused ? dependency : root, storage, hash, length);
                Need(SafariSource.GitBlob(bytes) == blob, "Fly house source Git blob differs."); Raw.Add(path, bytes);
                if (!reused) own++;
                files.Add(new(path, blob, length, hash, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
            }
            Files = files.ToImmutable(); Need(own == 3, "Fly house duplicated source inputs.");
            Need(SafariSource.Hash(Encoding.UTF8.GetBytes(string.Concat(Files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n")))) == FullSourceHash,
                "Fly house full source hash differs.");
            foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
            {
                string path = row.GetProperty("path").GetString()!;
                Outputs.Add(path, SafariSource.Read(Directory, path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32()));
            }
        }
        internal JsonElement Document(string path) => SilphSourcePackage.Json(Outputs[path]);
    }
}
