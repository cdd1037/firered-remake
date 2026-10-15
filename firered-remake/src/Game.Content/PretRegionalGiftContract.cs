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
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<string> RequiredCommonScripts { get; }
    public ImmutableArray<string> RequiredCommonTexts { get; }
    public MachineItem TeaItem { get; }
    public JsonElement Document { get; }

    private PretRegionalGiftContract(JsonElement document, SharedItemCatalog items)
    {
        Document = document.Clone();
        Constants = document.GetProperty("constants").EnumerateObject().ToImmutableDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);
        RequiredCommonScripts = document.GetProperty("commonScripts").EnumerateArray().Select(e => e.GetString()!).ToImmutableArray();
        RequiredCommonTexts = document.GetProperty("commonTexts").EnumerateArray().Select(e => e.GetString()!).ToImmutableArray();
        var tea = document.GetProperty("tea");
        Require(tea.GetProperty("item").GetString() == "pret:ITEM_TEA", "Tea item identity differs.");
        TeaItem = new(items.Definition(new("pret:ITEM_TEA")), tea.GetProperty("description").GetString()!);

    }

    public static PretRegionalGiftContract Load(string projectRoot, SharedItemCatalog? items = null)
    {
        var root = Path.GetFullPath(projectRoot);
            using var contract = JsonDocument.Parse(ReadPinned(root,"content/generated/data/services/regional-tea-eevee.json"));
        var d = contract.RootElement;
            Require(d.GetProperty("schemaVersion").GetInt32() == 1 && !d.GetProperty("gameplayEnabled").GetBoolean(),
            "Regional gift contract identity differs.");
        return new(d, items ?? SharedItemCatalog.LoadProject(root));

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
