using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed record PretRocketPatchCell(int X, int Y, int MetatileId, int Collision, int Elevation, uint RawAttributes, string AtlasPath, int AtlasX, int AtlasY);
public sealed record PretRocketMetatilePatch(string Label, MapId MapId, ImmutableArray<PretRocketPatchCell> Cells);
public sealed record PretRocketElevatorFloor(int MenuIndex, string Label, int SourceFloorValue, MapId DestinationMapId, int X, int Y, int Elevation, int DestinationWarpId);
public sealed record PretRocketElevatorEntrance(MapId SourceMapId, int SourceWarpId, int ElevatorWarpId, int ReturnX, int ReturnY, int ReturnElevation);
public sealed record PretRocketDynamicExit(int WarpIndex, int X, int Y, int Elevation, int SourceWarpId);

/// <summary>Six authenticated compact maps and source contracts. Application owns flags, spin sequences and saved dynamic destinations.</summary>
public sealed class PretRocketHideoutRegion
{
    public const string Profile = "firered-rocket-hideout-region-v1";
    public const int DynamicWarpId = 127;
    public const int CoordinateWarpId = 255;
    public static MapId DynamicMapId => new("map.dynamic");
    public static MapId ElevatorMapId => new("map.rocket-hideout-elevator");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<int> InitiallySetFlags { get; }
    public ImmutableArray<PretRocketMetatilePatch> MetatilePatches { get; }
    public ImmutableArray<PretRocketElevatorFloor> ElevatorFloors { get; }
    public ImmutableArray<PretRocketElevatorEntrance> ElevatorEntrances { get; }
    public ImmutableArray<PretRocketDynamicExit> DynamicExits { get; }
    public JsonElement SourceContract { get; }
    public JsonElement ExternalCeladonEvents { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string DependencySourcePath => Path.Combine(GeneratedDirectory, "dependencies.json");
    // Rich inspection metadata is authenticated when requested, never preloaded with map cells.
    public JsonElement Dependencies => ReadDocument("dependencies.json");
    public JsonElement Trainers => Dependencies.GetProperty("trainers");
    public JsonElement Pickups => Dependencies.GetProperty("pickups");
    public JsonElement Items => Dependencies.GetProperty("items");
    public JsonElement ScriptInventory => ReadDocument("scripts.json");
    public JsonElement NpcSources => ReadDocument("npc-source.json");
    public JsonElement SpinRoutes => ReadDocument("spin-routes.json");
    private readonly ImmutableHashSet<string> _outputs;

    private PretRocketHideoutRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableArray<PretWorldSourceFile> files, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableHashSet<string> outputs, JsonElement contract, JsonElement city)
    {
        GeneratedDirectory = directory; NewMaps = maps; SourceFiles = files; Scripts = scripts; TextSources = texts;
        Constants = constants; _outputs = outputs; SourceContract = contract; ExternalCeladonEvents = city;
        InitiallySetFlags = [constants["FLAG_HIDE_LIFT_KEY"], constants["FLAG_HIDE_SILPH_SCOPE"]];
        var atlas = contract.GetProperty("patchAtlas").EnumerateArray().ToDictionary(x => x.GetProperty("symbol").GetString()!, StringComparer.Ordinal);
        MetatilePatches = contract.GetProperty("metatilePatches").EnumerateArray().Select(p =>
        {
            var label = p.GetProperty("label").GetString()!;
            var id = label.StartsWith("CeladonCity_", StringComparison.Ordinal) ? new MapId("map.celadon-city-game-corner") :
                label.StartsWith("RocketHideout_B1F_", StringComparison.Ordinal) ? new MapId("map.rocket-hideout-b1f") : new MapId("map.rocket-hideout-b4f");
            var cells = p.GetProperty("cells").EnumerateArray().Select(c =>
            {
                var a = atlas[c.GetProperty("metatile").GetString()!]; var x = c.GetProperty("x").GetInt32(); var y = c.GetProperty("y").GetInt32();
                return new PretRocketPatchCell(x, y, c.GetProperty("metatileId").GetInt32(), c.GetProperty("collision").GetInt32(),
                    maps[id].GetSourceCell(x, y)!.Elevation, a.GetProperty("rawAttributes").GetUInt32(),
                    Path.Combine(directory, a.GetProperty("atlas").GetString()!["assets/".Length..]), a.GetProperty("x").GetInt32(), a.GetProperty("y").GetInt32());
            }).ToImmutableArray();
            return new PretRocketMetatilePatch(label, id, cells);
        }).ToImmutableArray();
        ElevatorFloors = contract.GetProperty("elevator").GetProperty("menu").EnumerateArray().Select(f =>
        {
            var id = MapIdFromSourceSymbol(f.GetProperty("map").GetString()!); var x = f.GetProperty("x").GetInt32(); var y = f.GetProperty("y").GetInt32();
            return new PretRocketElevatorFloor(f.GetProperty("index").GetInt32(), f.GetProperty("label").GetString()!,
                f.GetProperty("value").GetInt32(), id, x, y, maps[id].GetSourceCell(x, y)!.Elevation, CoordinateWarpId);
        }).ToImmutableArray();
        ElevatorEntrances = maps.Values.SelectMany(m => m.Warps.Where(w => w.DestinationMapId == ElevatorMapId)
            .Select(w => new PretRocketElevatorEntrance(m.Id, w.Index, w.DestinationWarpId, w.X, w.Y, m.GetSourceCell(w.X,w.Y)!.Elevation))).ToImmutableArray();
        DynamicExits = maps[ElevatorMapId].Warps.Select(w => new PretRocketDynamicExit(w.Index, w.X, w.Y, w.Elevation, w.DestinationWarpId)).ToImmutableArray();
        Need(MetatilePatches.Length == 6 && ElevatorFloors.Length == 3 && ElevatorEntrances.Length == 7 && DynamicExits.Length == 2,
            "Rocket dynamic/patch contract scope differs.");
    }

    public PretRocketMetatilePatch Patch(string label) => MetatilePatches.Single(p => p.Label == label);
    public ImmutableArray<PretRocketMetatilePatch> InitialPatches(bool entranceOpened, bool b1GuardDefeated, bool bothB4GuardsDefeated) =>
    [
        Patch("CeladonCity_GameCorner_EventScript_" + (entranceOpened ? "OpenRocketHideout" : "HideRocketHideout")),
        Patch("RocketHideout_B1F_EventScript_" + (b1GuardDefeated ? "RemoveBarrier" : "SetBarrier")),
        Patch("RocketHideout_B4F_EventScript_" + (bothB4GuardsDefeated ? "RemoveBarrier" : "SetBarrier"))
    ];

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROCKET_HIDEOUT_B1F" => new("map.rocket-hideout-b1f"), "MAP_ROCKET_HIDEOUT_B2F" => new("map.rocket-hideout-b2f"),
        "MAP_ROCKET_HIDEOUT_B3F" => new("map.rocket-hideout-b3f"), "MAP_ROCKET_HIDEOUT_B4F" => new("map.rocket-hideout-b4f"),
        "MAP_ROCKET_HIDEOUT_ELEVATOR" => ElevatorMapId, "MAP_DYNAMIC" => DynamicMapId,
        _ => PretCeladonGymRegion.MapIdFromSourceSymbol(symbol)
    };

    internal static int ParseWarpId(string sourceMap, JsonElement warp)
    {
        var value = warp.GetProperty("dest_warp_id").GetString(); var destination = warp.GetProperty("dest_map").GetString();
        if (value == "WARP_ID_DYNAMIC" || destination == "MAP_DYNAMIC")
        {
                Need((sourceMap == "MAP_ROCKET_HIDEOUT_ELEVATOR" ||
    sourceMap == "MAP_CELADON_CITY_DEPARTMENT_STORE_ELEVATOR" ||
    sourceMap == "MAP_SILPH_CO_ELEVATOR" ||
    sourceMap == "MAP_TRAINER_TOWER_ELEVATOR") &&
                 value == "WARP_ID_DYNAMIC" && destination == "MAP_DYNAMIC",
                "Dynamic warp is outside a supported elevator.");
            return DynamicWarpId;
        }
        Need(int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id is >= 0 and <= 255, "Runtime warp index out of bounds.");
        return id;
    }

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        var source = map.Events.GetProperty("connections");
        var connections = source.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty : source.EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch { "up" => Direction.North, "down" => Direction.South,
                "left" => Direction.West, "right" => Direction.East, _ => throw new InvalidDataException("Unsupported connection direction.") },
                c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId,c.Collision,c.Elevation)).ToImmutableArray(),connections);
    }

    public static PretRocketHideoutRegion Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot); var directory = Path.Combine(project,"content/generated/rocket-hideout-region");
        var runtime = runtimeMaps ?? PretRocketHideoutRuntimeMaps.Load(project);
            using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
            Need(m.GetProperty("schemaVersion").GetInt32() == 1 , "Rocket region identity differs.");
        var outputs = m.GetProperty("outputs").EnumerateArray()
            .Select(x => x.GetProperty("path").GetString()!).ToImmutableHashSet(StringComparer.Ordinal);
        byte[] ReadOutput(string path)
        {
            if (!outputs.Contains(path)) throw new InvalidDataException("Missing region output: " + path);
            return ReadPinned(directory, path);
        }
        JsonElement Document(string path) { using var d = JsonDocument.Parse(ReadOutput(path)); return d.RootElement.Clone(); }
            using var sourceLock = JsonDocument.Parse(ReadPinned(project,"scripts/rocket-hideout-region.lock.json"));
        var files = sourceLock.RootElement.GetProperty("files").EnumerateArray().Select(f => new PretWorldSourceFile(f.GetProperty("path").GetString()!,            $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{f.GetProperty("path").GetString()}")).ToImmutableArray();
        var byPath = files.ToDictionary(f=>f.Path,StringComparer.Ordinal);var rawDirectory=Path.Combine(project,PretSourcePaths.FireRedRawDirectory);
            byte[] ReadSource(string path) { var f=byPath[path];return ReadPinned(rawDirectory,path); }
        var metadata = new PretWorldMetadata("pret/pokefirered",PretWorldMap.SourceCommit,"Local authenticated source data.","Source contracts; executable progression is registered by Application.",Profile,files);
        var spec = Document("runtime.json"); var maps=ImmutableDictionary.CreateBuilder<MapId,PretWorldMap>();
        foreach(var s in spec.GetProperty("maps").EnumerateArray())
        {
            var id=new MapId(s.GetProperty("stableId").GetString()!);var compact=runtime.Maps[id];
                Need(                compact.SourceRef.Symbol==s.GetProperty("sourceMapSymbol").GetString() && MapIdFromSourceSymbol(compact.SourceRef.Symbol)==id,"Rocket map identity differs.");
                foreach(var path in compact.ScriptPaths) _ = ReadSource(path);
            var relative=s.GetProperty("directory").GetString()!;
            foreach(var artifact in compact.Artifacts)
            {
                var b=ReadOutput(relative+"/"+artifact.Path);
                    Need(b.Length>24 && BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(16,4))==artifact.Width &&
                    BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(20,4))==artifact.Height,"Rocket PNG differs.");
            }
            maps.Add(id,PretWorldMap.FromRuntime(compact,metadata,PretRuntimeMapValidation.SafeFile(directory,relative),
                PretRuntimeMapValidation.SafeFile(directory,"npc/red-normal.png"),MapIdFromSourceSymbol));
        }
        Need(maps.Count==6 && maps.Values.Sum(x=>x.Width*x.Height)==3179,"Rocket six-map geometry differs.");
        var scripts=ImmutableArray.CreateBuilder<PretSourceScriptFile>();var texts=ImmutableDictionary.CreateBuilder<string,string>(StringComparer.Ordinal);
        foreach(var p in spec.GetProperty("scriptPaths").EnumerateArray())
        {
            var path=p.GetString()!;var text=Encoding.UTF8.GetString(ReadSource(path));
            if(path.EndsWith("/text.inc",StringComparison.Ordinal)||path.StartsWith("data/text/",StringComparison.Ordinal))texts.Add(path,text);
            else {var symbol=Regex.Match(text,@"^(\w+)::?",RegexOptions.Multiline|RegexOptions.CultureInvariant).Groups[1].Value;
                Need(symbol.Length>0,"Rocket source has no original label.");scripts.Add(new(path,text,new(SourceKind.Pret,path,symbol,"pret/pokefirered",PretWorldMap.SourceCommit)));}
        }
        var constants=PretSharedConstants.ForPackage(directory);
        var contract=Document("rocket-hideout.json");
        using var city=JsonDocument.Parse(ReadSource("data/maps/CeladonCity/map.json"));
        Need(JsonElement.DeepEquals(city.RootElement.GetProperty("warp_events")[0],contract.GetProperty("cityEntry").GetProperty("source")) &&
            JsonElement.DeepEquals(runtime.Maps[new("map.celadon-city")].Events,city.RootElement),"Celadon city entry differs from unchanged source.");
        foreach(var e in outputs.Where(e=>e.StartsWith("npc/",StringComparison.Ordinal)||e.StartsWith("patches/",StringComparison.Ordinal)))_ = ReadOutput(e);
        return new(directory,maps.ToImmutable(),files,scripts.ToImmutable(),texts.ToImmutable(),constants,outputs,contract,city.RootElement.Clone());
    }

    private JsonElement ReadDocument(string path)
        { if (!_outputs.Contains(path)) throw new InvalidDataException("Missing region output: " + path);using var document=JsonDocument.Parse(ReadPinned(GeneratedDirectory,path));return document.RootElement.Clone(); }
    private static byte[] ReadPinned(string root, string path)
    {
        PretRocketHideoutRuntimeMaps.RequireNoLinks(root, path);
        return File.ReadAllBytes(PretRuntimeMapValidation.SafeFile(root, path));
    }
    private static void Need(bool ok,string message)=>PretRuntimeMapValidation.Require(ok,message);
}
