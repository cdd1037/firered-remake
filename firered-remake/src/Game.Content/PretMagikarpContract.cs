using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record PretMagikarpSourceFile(string Path, string SourceDirectory);

/// <summary>
/// Authenticated evidence for the Route4 Magikarp sale. Existing Route3 source scripts remain
/// authoritative; this loader neither replaces those scripts nor enables gifting by itself.
/// </summary>
public sealed class PretMagikarpContract
{
    public const string Profile = "firered-route4-magikarp-source-contract-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public ImmutableArray<string> RequiredCommonScriptLabels { get; }
    public ImmutableArray<string> RequiredCommonTextLabels { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Document { get; }

    private PretMagikarpContract(JsonElement document)
    {
        Document = document.Clone();
        RequiredCommonScriptLabels = document.GetProperty("commonScripts").EnumerateArray().Select(e => e.GetString()!).ToImmutableArray();
        RequiredCommonTextLabels = document.GetProperty("commonTexts").EnumerateArray().Select(e => e.GetString()!).ToImmutableArray();
        Constants = document.GetProperty("constants").EnumerateObject().ToImmutableDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);
    }

    public static PretMagikarpContract Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        const string generated = "content/generated/magikarp-gift/";
            using var manifest = JsonDocument.Parse(ReadPinned(root,generated + "manifest.json"));
        var m = manifest.RootElement;
            Require(m.GetProperty("schemaVersion").GetInt32() == 1 &&
            m.GetProperty("scriptBlockCount").GetInt32() == 35 && m.GetProperty("sourceFunctionCount").GetInt32() == 27,
            "Magikarp source manifest scope differs.");
            var contractBytes = ReadPinned(root,generated + "magikarp-contract.json");
        using var document = JsonDocument.Parse(contractBytes);
        var d = document.RootElement;
            Require(            !d.GetProperty("gameplayEnabled").GetBoolean(), "Magikarp contract identity differs.");
        return new(d);

    }

    private static byte[] ReadPinned(string root, string relative)
    {
        Require(!Path.IsPathRooted(relative) && !relative.Split('/').Any(p => p is ".." or "." or ""), "Unsafe Magikarp source path.");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        for (FileSystemInfo? info = new FileInfo(full); info is not null; info = info switch
        {
            FileInfo file => file.Directory,
            DirectoryInfo directory => directory.Parent,
            _ => null
        })
            Require(info.LinkTarget is null, "Magikarp source/output symlink is unsupported: " + info.FullName);
        return File.ReadAllBytes(full);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
