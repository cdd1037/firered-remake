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
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<int> InitiallySetFlags { get; }
    public ImmutableArray<PretRocketMetatilePatch> MetatilePatches { get; }
    public ImmutableArray<PretRocketElevatorFloor> ElevatorFloors { get; }
    public ImmutableArray<PretRocketElevatorEntrance> ElevatorEntrances { get; }
    public ImmutableArray<PretRocketDynamicExit> DynamicExits { get; }
    public JsonElement ExternalCeladonEvents { get; }
    public string GeneratedDirectory { get; }


    private PretRocketHideoutRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants,
        JsonElement contract, JsonElement city, Func<string, MapId> resolveMap)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants; ExternalCeladonEvents = city;
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
            var id = resolveMap(f.GetProperty("map").GetString()!); var x = f.GetProperty("x").GetInt32(); var y = f.GetProperty("y").GetInt32();
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



    public static PretRocketHideoutRegion Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot); var directory = Path.Combine(project,"content/generated/maps/regions/rocket-hideout");
        var runtime = world.RuntimeMaps;
        JsonElement Document(string path) { using var d = JsonDocument.Parse(ReadPinned(directory, path)); return d.RootElement.Clone(); }

        var maps = world.MapsInDirectory(directory);

        var constants=PretSharedConstants.ForPackage(directory);
        var contract=Document("rocket-hideout.json");
        using var city=JsonDocument.Parse(world.ReadEvents("data/maps/CeladonCity/map.json"));
        return new(directory,maps,constants,contract,city.RootElement.Clone(),world.Resolve);
    }

    private static byte[] ReadPinned(string root, string path)
    {
        PretRuntimeMapValidation.RequireNoLinks(root, path);
        return File.ReadAllBytes(PretRuntimeMapValidation.SafeFile(root, path));
    }
    private static void Need(bool ok,string message)=>PretRuntimeMapValidation.Require(ok,message);
}
