using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretRegionalGiftSourceFile(string Path, int ByteLength, string GitBlobSha1, string Sha256);

/// <summary>Authenticates Tea, four shared gate scripts, and Eevee gift source. Does not enable maps or effects.</summary>
public sealed class PretRegionalGiftContract
{
    public const string Profile = "firered-regional-tea-eevee-contract-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "ce8b6878310548192e4053b4868932f30945945f1f425a152279563fd4175d0c";
    public const string ContractSha256 = "e27f879bcee3640af45ad618d87ae85c54376f2e827e1b8e1e8a07960cabe81a";
    public const string SourceSetSha256 = "b2f9841d897a86e6d924530bbf485c61c7e02f43ffed82ca7553c25308901080";
    public ImmutableArray<PretRegionalGiftSourceFile> SourceFiles { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<string> RequiredCommonScripts { get; }
    public ImmutableArray<string> RequiredCommonTexts { get; }
    public PewterMachineItem TeaItem { get; }
    public JsonElement Document { get; }

    private PretRegionalGiftContract(JsonElement document, ImmutableArray<PretRegionalGiftSourceFile> files,
        IReadOnlyDictionary<string, byte[]> raw)
    {
        Document = document.Clone(); SourceFiles = files;
        Constants = document.GetProperty("constants").EnumerateObject().ToImmutableDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);
        RequiredCommonScripts = document.GetProperty("commonScripts").EnumerateArray().Select(e => e.GetString()!).ToImmutableArray();
        RequiredCommonTexts = document.GetProperty("commonTexts").EnumerateArray().Select(e => e.GetString()!).ToImmutableArray();
        AdditionalSourceHashes = ["regional-gifts-lock:" + LockSha256, "regional-gifts-contract:" + ContractSha256,
            .. files.Select(f => "regional-gifts-source:" + f.Path + ":" + f.Sha256)];
        const string itemPath = "src/data/items.json";
        var lines = Encoding.UTF8.GetString(raw[itemPath]).Split('\n');
        int idLine = Array.FindIndex(lines, l => l.Contains("\"itemId\": \"ITEM_TEA\"", StringComparison.Ordinal)) + 1;
        Require(idLine > 0, "Tea source declaration is missing.");
        var source = new OpeningItemSourceRef(itemPath, "ITEM_TEA", idLine - 2, idLine + 15,
            files.Single(f => f.Path == itemPath).Sha256);
        var item = document.GetProperty("tea").GetProperty("item");
        Require(item.GetProperty("english").GetString() == "TEA" && item.GetProperty("itemId").GetString() == "ITEM_TEA" &&
            item.GetProperty("pocket").GetString() == "POCKET_KEY_ITEMS" && item.GetProperty("price").GetInt32() == 0,
            "Tea source item identity differs.");
        TeaItem = new(new(new("pret:ITEM_TEA"), "ITEM_TEA", 369, "TEA", 0, 1, 1,
            "POCKET_KEY_ITEMS", "ITEM_TYPE_BAG_MENU", "HOLD_EFFECT_NONE", 0, "FieldUseFunc_OakStopsYou",
            0, "NULL", 0, false, false, false, source),
            item.GetProperty("description_english").GetString()!.Replace("\\n", "\n", StringComparison.Ordinal),
            item.GetProperty("description_english").GetString()!, source);
    }

    public static PretRegionalGiftContract Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        _ = ReadPinned(root, "scripts/regional-tea-eevee.lock.json", LockSha256);
        using var contract = JsonDocument.Parse(ReadPinned(root, "content/generated/regional-tea-eevee/contract.json", ContractSha256, 12145));
        var d = contract.RootElement;
        Require(d.GetProperty("schemaVersion").GetInt32() == 1 && d.GetProperty("profile").GetString() == Profile &&
            d.GetProperty("sourceCommit").GetString() == SourceCommit && d.GetProperty("sourceTree").GetString() == SourceTree &&
            d.GetProperty("sourceSetSha256").GetString() == SourceSetSha256 && !d.GetProperty("gameplayEnabled").GetBoolean(),
            "Regional gift contract identity differs.");
        var files = ImmutableArray.CreateBuilder<PretRegionalGiftSourceFile>();
        var raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in d.GetProperty("sourceFiles").EnumerateArray())
        {
            var f = new PretRegionalGiftSourceFile(entry.GetProperty("path").GetString()!, entry.GetProperty("byteLength").GetInt32(),
                entry.GetProperty("gitBlobSha1").GetString()!, entry.GetProperty("sha256").GetString()!);
            var b = ReadPinned(root, "content/raw/regional-tea-eevee/" + f.Path, f.Sha256, f.ByteLength);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {b.Length}\0"), .. b])) == f.GitBlobSha1 && raw.TryAdd(f.Path, b),
                "Regional gift source Git blob/uniqueness differs: " + f.Path);
            _ = new UTF8Encoding(false, true).GetString(b); files.Add(f);
        }
        Require(files.Count == 37 && Hash(Encoding.UTF8.GetBytes(string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal)
            .Select(f => f.Path + "\0" + f.Sha256 + "\n")))) == SourceSetSha256, "Regional gift source inventory differs.");
        foreach (string gate in new[] { "Route5_SouthEntrance", "Route6_NorthEntrance", "Route7_EastEntrance", "Route8_WestEntrance" })
        {
            using var map = JsonDocument.Parse(raw[$"data/maps/{gate}/map.json"]);
            var triggers = map.RootElement.GetProperty("coord_events").EnumerateArray().ToArray();
            Require(triggers.Length == 3 && triggers.All(t => t.GetProperty("var").GetString() == "VAR_MAP_SCENE_ROUTE5_ROUTE6_ROUTE7_ROUTE8_GATES" &&
                t.GetProperty("var_value").GetString() == "0"), "Shared gate scene differs: " + gate);
        }
        return new(d, files.ToImmutable(), raw);
    }

    private static byte[] ReadPinned(string root, string relative, string sha, int? length = null)
    {
        Require(!Path.IsPathRooted(relative) && !relative.Split('/').Any(p => p is ".." or "." or ""), "Unsafe regional gift path.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        for (FileSystemInfo? info = new FileInfo(path); info is not null; info = info switch
        { FileInfo f => f.Directory, DirectoryInfo d => d.Parent, _ => null })
            Require(info.LinkTarget is null, "Regional gift symlink is unsupported.");
        long size = new FileInfo(path).Length;
        Require(size is > 0 and <= 2 * 1024 * 1024 && (length is null || size == length), "Regional gift source byte length differs.");
        var bytes = File.ReadAllBytes(path);
        Require(bytes.Length == size && Hash(bytes) == sha, "Pinned regional gift bytes differ: " + relative);
        return bytes;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
