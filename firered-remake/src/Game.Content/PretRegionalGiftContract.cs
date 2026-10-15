using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretRegionalGiftSourceFile(string Path);

/// <summary>Authenticates Tea, four shared gate scripts, and Eevee gift source. Does not enable maps or effects.</summary>
public sealed class PretRegionalGiftContract
{
    public const string Profile = "firered-regional-tea-eevee-contract-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public ImmutableArray<PretRegionalGiftSourceFile> SourceFiles { get; }
    public ImmutableDictionary<string, int> Constants { get; }
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
        const string itemPath = "src/data/items.json";
        var lines = Encoding.UTF8.GetString(raw[itemPath]).Split('\n');
        int idLine = Array.FindIndex(lines, l => l.Contains("\"itemId\": \"ITEM_TEA\"", StringComparison.Ordinal)) + 1;
        Require(idLine > 0, "Tea source declaration is missing.");
        var source = new OpeningItemSourceRef(itemPath,"ITEM_TEA",idLine - 2,idLine + 15);
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
            using var contract = JsonDocument.Parse(ReadPinned(root,"content/generated/regional-tea-eevee/contract.json"));
        var d = contract.RootElement;
            Require(d.GetProperty("schemaVersion").GetInt32() == 1 && !d.GetProperty("gameplayEnabled").GetBoolean(),
            "Regional gift contract identity differs.");
        var files = ImmutableArray.CreateBuilder<PretRegionalGiftSourceFile>();
        var raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in d.GetProperty("sourceFiles").EnumerateArray())
        {
            var f = new PretRegionalGiftSourceFile(entry.GetProperty("path").GetString()!);
                var b = ReadPinned(root,PretSourcePaths.FireRedRawDirectory + "/" + f.Path);
                Require(raw.TryAdd(f.Path, b),
                "Regional gift source Git blob/uniqueness differs: " + f.Path);
            _ = new UTF8Encoding(false, true).GetString(b); files.Add(f);
        }
            foreach (string gate in new[] { "Route5_SouthEntrance","Route6_NorthEntrance","Route7_EastEntrance","Route8_WestEntrance" })
        {
            using var map = JsonDocument.Parse(raw[$"data/maps/{gate}/map.json"]);
            var triggers = map.RootElement.GetProperty("coord_events").EnumerateArray().ToArray();
            Require(triggers.Length == 3 && triggers.All(t => t.GetProperty("var").GetString() == "VAR_MAP_SCENE_ROUTE5_ROUTE6_ROUTE7_ROUTE8_GATES" &&
                t.GetProperty("var_value").GetString() == "0"), "Shared gate scene differs: " + gate);
        }
        return new(d, files.ToImmutable(), raw);
    }

    private static byte[] ReadPinned(string root, string relative)
    {
        Require(!Path.IsPathRooted(relative) && !relative.Split('/').Any(p => p is ".." or "." or ""), "Unsafe regional gift path.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        for (FileSystemInfo? info = new FileInfo(path); info is not null; info = info switch
        {
            FileInfo f => f.Directory,
            DirectoryInfo d => d.Parent,
            _ => null
        })
            Require(info.LinkTarget is null, "Regional gift symlink is unsupported.");
        return File.ReadAllBytes(path);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
