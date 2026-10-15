using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

public sealed record PretDepartmentStoreSourceReference(string Path, string Symbol, int LineStart, int LineEnd);
public sealed record PretDepartmentStoreItem(string StableId, int SourceItemId, string Name, int Price, string Pocket, JsonElement Metadata);
public sealed record PretDepartmentStoreStock(string Id, string ClerkScript, string ProposedAdmission,
    ImmutableArray<PretDepartmentStoreItem> Items, PretDepartmentStoreSourceReference Source);
public sealed record PretDepartmentStoreFloor(int MenuIndex, string Label, int SourceFloorValue,
    string DestinationMapId, int DestinationWarpId, int X, int Y);
public sealed record PretDepartmentStoreEntrance(string SourceMapId, int SourceWarpIndex, int X, int Y,
    int WarpEventElevation, int ElevatorWarpIndex);
public sealed record PretDepartmentStoreBoundary(string MapId, string Script, string Service, string Reason);

/// <summary>Loads department store stocks, elevator routes and service declarations.</summary>
public sealed class PretDepartmentStoreContract
{
    public const string Profile = "firered-department-store-contract-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string ElevatorMapId = "map.celadon-city-department-store-elevator";
    public const string OrdinaryMapId = "map.celadon-city-department-store-2f";
    public const string OrdinaryStockId = "CeladonCity_DepartmentStore_2F_Items";
    public const string OrdinaryClerkScript = "CeladonCity_DepartmentStore_2F_EventScript_ClerkItems";
    public const string OrdinarySourcePath = "data/maps/CeladonCity_DepartmentStore_2F/scripts.inc";
    public const int ExitChoice = 5;
    public const int CancelChoice = 127;
    public JsonElement Document { get; }
    public ImmutableDictionary<string, JsonElement> MapEvents { get; }
    public ImmutableArray<PretDepartmentStoreStock> Stocks { get; }
    public ImmutableArray<PretDepartmentStoreFloor> ElevatorFloors { get; }
    public ImmutableArray<PretDepartmentStoreEntrance> ElevatorEntrances { get; }
    public ImmutableArray<PretDepartmentStoreBoundary> DeferredServices { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public PretDepartmentStoreStock OrdinaryStock => Stocks.Single(s => s.Id == OrdinaryStockId);
    private readonly ImmutableArray<int> _shakeTable;

    private PretDepartmentStoreContract(JsonElement document)
    {
        Document = document.Clone();
        MapEvents = document.GetProperty("maps").EnumerateArray().ToImmutableDictionary(
            m => String(m, "stableId"), m => m.GetProperty("events").Clone(), StringComparer.Ordinal);
        Stocks = document.GetProperty("stocks").EnumerateArray().Select(s => new PretDepartmentStoreStock(
            String(s, "id"), String(s, "clerkScript"), String(s, "proposedAdmission"),
            s.GetProperty("items").EnumerateArray().Select(i =>
            {
                var m = i.GetProperty("metadata");
                return new PretDepartmentStoreItem(String(i, "stableId"), Int(i, "sourceItemId"),
                    String(m, "english"), Int(m, "price"), String(m, "pocket"), m.Clone());
            }).ToImmutableArray(), Reference(s.GetProperty("source")))).ToImmutableArray();
        var elevator = document.GetProperty("elevator");
        ElevatorFloors = elevator.GetProperty("floors").EnumerateArray().Select(f => new PretDepartmentStoreFloor(
            Int(f, "menuIndex"), String(f, "label"), Int(f, "sourceFloorValue"), String(f, "destinationMapId"),
            Int(f, "destinationWarpId"), Int(f, "x"), Int(f, "y"))).ToImmutableArray();
        ElevatorEntrances = elevator.GetProperty("entrances").EnumerateArray().Select(e => new PretDepartmentStoreEntrance(
            String(e, "sourceMapId"), Int(e, "sourceWarpIndex"), Int(e, "x"), Int(e, "y"), Int(e, "elevation"),
            int.Parse(String(e, "dest_warp_id"), System.Globalization.CultureInfo.InvariantCulture))).ToImmutableArray();
        DeferredServices = document.GetProperty("deferredServices").EnumerateArray().Select(e => new PretDepartmentStoreBoundary(
            String(e, "mapId"), String(e, "script"), String(e, "service"), String(e, "reason"))).ToImmutableArray();
        Constants = document.GetProperty("constants").EnumerateObject().ToImmutableDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);
        _shakeTable = elevator.GetProperty("shakeTable").EnumerateArray().Select(x => x.GetInt32()).ToImmutableArray();
    }

    public bool IsOrdinaryClerk(string mapId, string scriptPath, string scriptSymbol, string stockId) =>
        mapId == OrdinaryMapId && scriptPath == OrdinarySourcePath && scriptSymbol == OrdinaryClerkScript && stockId == OrdinaryStockId;

    /// <summary>Data verification only. The integrator still checks loaded maps/cells and movement admission.</summary>
    public void ValidateMapEvents(string stableId, JsonElement events)
    {
        Need(MapEvents.TryGetValue(stableId, out var expected) && JsonElement.DeepEquals(expected, events),
            "Department store event source differs: " + stableId);
    }

    /// <summary>The source skips animation for the same floor; four-floor travel is 38*3 frames.</summary>
    public int ElevatorShakeFrames(int fromSourceFloorValue, int toSourceFloorValue)
    {
        Need(ElevatorFloors.Any(f => f.SourceFloorValue == fromSourceFloorValue) &&
             ElevatorFloors.Any(f => f.SourceFloorValue == toSourceFloorValue), "Unsupported department store floor.");
        int distance = Math.Abs(fromSourceFloorValue - toSourceFloorValue);
        return distance == 0 ? 0 : checked(_shakeTable[distance] * 3);
    }

    public static PretDepartmentStoreContract Load(string projectRoot)
    {
        string root = Path.GetFullPath(projectRoot);
        using var contract = JsonDocument.Parse(ReadPinned(root, "content/generated/data/services/department-store.json"));
        var document = contract.RootElement;
        Need(Int(document, "schemaVersion") == 1, "Unsupported department store schema.");
        var result = new PretDepartmentStoreContract(document);
        Need(result.Stocks.SelectMany(stock => stock.Items).All(item => item.Price > 0), "Department store item price is invalid.");
        return result;
    }

    private static PretDepartmentStoreSourceReference Reference(JsonElement r) =>
        new(String(r, "path"),String(r, "symbol"),Int(r, "lineStart"),Int(r, "lineEnd"));
    private static string String(JsonElement e, string name) => e.GetProperty(name).GetString() ?? throw new InvalidDataException("Missing department store string.");
    private static int Int(JsonElement e, string name) => e.GetProperty(name).GetInt32();
    private static void Need(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static byte[] ReadPinned(string root, string relative)
    {
        Need(!Path.IsPathRooted(relative) && !relative.Split('/').Any(p => p is "" or "." or ".."), "Unsafe department store path.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        for (FileSystemInfo? info = new FileInfo(path); info is not null; info = info switch
        { FileInfo f => f.Directory, DirectoryInfo d => d.Parent, _ => null })
            Need(info.LinkTarget is null, "Department store symlink is unsupported.");
        byte[] bytes = File.ReadAllBytes(path);
        return bytes;
    }
}
