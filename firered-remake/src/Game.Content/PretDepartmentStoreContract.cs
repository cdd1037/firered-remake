using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Game.Content;

public sealed record PretDepartmentStoreSourceFile(string Path, int ByteLength, string GitBlobSha1, string Sha256);
public sealed record PretDepartmentStoreSourceReference(string Path, string Symbol, int LineStart, int LineEnd, string Sha256);
public sealed record PretDepartmentStoreItem(string StableId, int SourceItemId, string Name, int Price, string Pocket, JsonElement Metadata);
public sealed record PretDepartmentStoreStock(string Id, string ClerkScript, string ProposedAdmission,
    ImmutableArray<PretDepartmentStoreItem> Items, PretDepartmentStoreSourceReference Source);
public sealed record PretDepartmentStoreFloor(int MenuIndex, string Label, int SourceFloorValue,
    string DestinationMapId, int DestinationWarpId, int X, int Y);
public sealed record PretDepartmentStoreEntrance(string SourceMapId, int SourceWarpIndex, int X, int Y,
    int WarpEventElevation, int ElevatorWarpIndex);
public sealed record PretDepartmentStoreBoundary(string MapId, string Script, string Service, string Reason);

/// <summary>Authenticates the next bounded service source. Loading neither admits maps nor enables transactions.</summary>
public sealed class PretDepartmentStoreContract
{
    public const string Profile = "firered-department-store-contract-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "a964111fc4a38298550cfddbc22c6093644a7c7e43cc2707b18319a54ef5c3c8";
    public const string ContractSha256 = "8dc7477a49cabd7e1d21955cdb2cc2aaabc31891ca76a23f3b7c101a60493f84";
    public const string SourceSetSha256 = "e5b91c9df1e63e3f4b274221009318fc5e05f46c17314b4dffe224ad89de7354";
    public const string ElevatorMapId = "map.celadon-city-department-store-elevator";
    public const string OrdinaryMapId = "map.celadon-city-department-store-2f";
    public const string OrdinaryStockId = "CeladonCity_DepartmentStore_2F_Items";
    public const string OrdinaryClerkScript = "CeladonCity_DepartmentStore_2F_EventScript_ClerkItems";
    public const string OrdinarySourcePath = "data/maps/CeladonCity_DepartmentStore_2F/scripts.inc";
    public const int ExitChoice = 5;
    public const int CancelChoice = 127;
    public JsonElement Document { get; }
    public ImmutableArray<PretDepartmentStoreSourceFile> SourceFiles { get; }
    public ImmutableDictionary<string, JsonElement> MapEvents { get; }
    public ImmutableArray<PretDepartmentStoreStock> Stocks { get; }
    public ImmutableArray<PretDepartmentStoreFloor> ElevatorFloors { get; }
    public ImmutableArray<PretDepartmentStoreEntrance> ElevatorEntrances { get; }
    public ImmutableArray<PretDepartmentStoreBoundary> DeferredServices { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public PretDepartmentStoreStock OrdinaryStock => Stocks.Single(s => s.Id == OrdinaryStockId);
    private readonly ImmutableArray<int> _shakeTable;

    private PretDepartmentStoreContract(JsonElement document, ImmutableArray<PretDepartmentStoreSourceFile> files)
    {
        Document = document.Clone(); SourceFiles = files;
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
        AdditionalSourceHashes = ["department-store-lock:" + LockSha256, "department-store-contract:" + ContractSha256,
            .. files.Select(f => "department-store-source:" + f.Path + ":" + f.Sha256)];
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
        using var sourceLock = JsonDocument.Parse(ReadPinned(root, "scripts/department-store.lock.json", LockSha256));
        using var contract = JsonDocument.Parse(ReadPinned(root, "content/generated/department-store/contract.json", ContractSha256, 80138));
        var d = contract.RootElement; var l = sourceLock.RootElement;
        Need(Int(d, "schemaVersion") == 1 && String(d, "profile") == Profile && String(d, "sourceCommit") == SourceCommit &&
             String(d, "sourceTree") == SourceTree && String(d, "sourceSetSha256") == SourceSetSha256 &&
             !d.GetProperty("runtimeIntegrated").GetBoolean() && !d.GetProperty("completeDepartmentStore").GetBoolean() &&
             JsonElement.DeepEquals(d.GetProperty("sourceFiles"), l.GetProperty("files")), "Department store contract identity differs.");
        var files = ImmutableArray.CreateBuilder<PretDepartmentStoreSourceFile>();
        var raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in d.GetProperty("sourceFiles").EnumerateArray())
        {
            var f = new PretDepartmentStoreSourceFile(String(row, "path"), Int(row, "byteLength"), String(row, "gitBlobSha1"), String(row, "sha256"));
            byte[] b = ReadPinned(root, "content/raw/department-store/" + f.Path, f.Sha256, f.ByteLength);
            Need(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {b.Length}\0"), .. b])) == f.GitBlobSha1 &&
                 raw.TryAdd(f.Path, b), "Department store Git blob/source uniqueness differs: " + f.Path);
            _ = new UTF8Encoding(false, true).GetString(b); files.Add(f);
        }
        Need(files.Count == 36 && Hash(Encoding.UTF8.GetBytes(string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal)
            .Select(f => f.Path + "\0" + f.Sha256 + "\n")))) == SourceSetSha256, "Department store source inventory differs.");
        var result = new PretDepartmentStoreContract(d, files.ToImmutable());
        foreach (var map in d.GetProperty("maps").EnumerateArray())
        {
            using var original = JsonDocument.Parse(raw[String(map, "sourcePath")]);
            result.ValidateMapEvents(String(map, "stableId"), original.RootElement);
        }
        using var sourceItems = JsonDocument.Parse(raw["src/data/items.json"]);
        // Source item holes deliberately repeat ITEM_NONE; only these stocked, real symbols are unique.
        var stockedSymbols = result.Stocks.SelectMany(s => s.Items).Select(i => i.StableId[5..]).ToHashSet(StringComparer.Ordinal);
        var metadata = sourceItems.RootElement.GetProperty("items").EnumerateArray().Where(i => stockedSymbols.Contains(String(i, "itemId")))
            .ToDictionary(i => String(i, "itemId"), StringComparer.Ordinal);
        foreach (var stock in result.Stocks)
        {
            string[] lines = Encoding.UTF8.GetString(raw[stock.Source.Path]).Split('\n');
            Need(stock.Source.LineStart > 0 && stock.Source.LineEnd <= lines.Length &&
                 lines[stock.Source.LineStart - 1] == stock.Source.Symbol + "::", "Stock source label differs.");
            string body = string.Join('\n', lines.Skip(stock.Source.LineStart).Take(stock.Source.LineEnd - stock.Source.LineStart));
            string[] symbols = Regex.Matches(body, @"\.2byte (ITEM_\w+)").Select(m => m.Groups[1].Value).ToArray();
            Need(symbols.SequenceEqual(stock.Items.Select(i => i.StableId[5..]).Append("ITEM_NONE")), "Stock source inventory differs.");
            foreach (var item in stock.Items)
                Need(item.Price > 0 && JsonElement.DeepEquals(item.Metadata, metadata[item.StableId[5..]]), "Stock metadata differs.");
        }
        Need(result.MapEvents.Count == 7 && result.Stocks.Length == 5 && result.ElevatorFloors.Length == 5 &&
             result.ElevatorEntrances.Length == 5 && result.DeferredServices.Length == 7, "Department store scope differs.");
        return result;
    }

    private static PretDepartmentStoreSourceReference Reference(JsonElement r) =>
        new(String(r, "path"), String(r, "symbol"), Int(r, "lineStart"), Int(r, "lineEnd"), String(r, "sha256"));
    private static string String(JsonElement e, string name) => e.GetProperty(name).GetString() ?? throw new InvalidDataException("Missing department store string.");
    private static int Int(JsonElement e, string name) => e.GetProperty(name).GetInt32();
    private static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Need(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static byte[] ReadPinned(string root, string relative, string sha, int? length = null)
    {
        Need(!Path.IsPathRooted(relative) && !relative.Split('/').Any(p => p is "" or "." or ".."), "Unsafe department store path.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        for (FileSystemInfo? info = new FileInfo(path); info is not null; info = info switch
        { FileInfo f => f.Directory, DirectoryInfo d => d.Parent, _ => null })
            Need(info.LinkTarget is null, "Department store symlink is unsupported.");
        long size = new FileInfo(path).Length;
        Need(size is > 0 and <= 2 * 1024 * 1024 && (length is null || size == length), "Department store byte length differs.");
        byte[] bytes = File.ReadAllBytes(path);
        Need(bytes.Length == size && Hash(bytes) == sha, "Pinned department store bytes differ: " + relative);
        return bytes;
    }
}
