using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record PretMagikarpSourceFile(string Path, string RawModule, int ByteLength, string GitBlobSha1, string Sha256);

/// <summary>
/// Authenticated evidence for the Route4 Magikarp sale. Existing Route3 source scripts remain
/// authoritative; this loader neither replaces those scripts nor enables gifting by itself.
/// </summary>
public sealed class PretMagikarpContract
{
    public const string Profile = "firered-route4-magikarp-source-contract-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string SourceSetSha256 = "3e3f276a42b85736fc7490f18e47023f860cb776f08ffabab48336f2cd28babd";
    public const string RegionLockSha256 = "38f2849ba187f843875322fd0760c2410fbac1fd410ac04f347ccb9368fdd6f4";
    public const string ManifestSha256 = "ec75e1b2b59c110fdc0ef1903f816bd320d0a57f6850abe37143c00b3f5a2845";
    public const string ContractSha256 = "73d7351cf4d124329ba70defb9951b2a6c7af59e7f68e33699bf015207749e59";
    public const string CommonScriptSha256 = "a448dc774545bed9b620c3a9b628af1d6f075fe6b9f65557a4f7dd7cdd5f4a3a";

    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PretMagikarpSourceFile> SourceFiles { get; }
    public ImmutableArray<string> RequiredCommonScriptLabels { get; }
    public ImmutableArray<string> RequiredCommonTextLabels { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Document { get; }

    private PretMagikarpContract(JsonElement document, ImmutableArray<PretMagikarpSourceFile> sources)
    {
        Document = document.Clone();
        SourceFiles = sources;
        RequiredCommonScriptLabels = document.GetProperty("commonScripts").EnumerateArray().Select(e => e.GetString()!).ToImmutableArray();
        RequiredCommonTextLabels = document.GetProperty("commonTexts").EnumerateArray().Select(e => e.GetString()!).ToImmutableArray();
        Constants = document.GetProperty("constants").EnumerateObject().ToImmutableDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);
        AdditionalSourceHashes = ["magikarp-contract-profile:" + Profile,
            "magikarp-contract-manifest:" + ManifestSha256, "magikarp-contract:" + ContractSha256,
            "magikarp-common-script-evidence:" + CommonScriptSha256,
            .. sources.Select(s => "magikarp-source:" + s.Path + ":" + s.Sha256)];
    }

    public static PretMagikarpContract Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        _ = ReadPinned(root, "scripts/route3-region.lock.json", RegionLockSha256);
        const string generated = "content/generated/magikarp-gift/";
        using var manifest = JsonDocument.Parse(ReadPinned(root, generated + "manifest.json", ManifestSha256, 808));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == SourceCommit && m.GetProperty("sourceTree").GetString() == SourceTree &&
            m.GetProperty("sourceSetSha256").GetString() == SourceSetSha256 &&
            m.GetProperty("reusedRegionSourceCount").GetInt32() == 24 && m.GetProperty("newSourceCount").GetInt32() == 5 &&
            m.GetProperty("scriptBlockCount").GetInt32() == 35 && m.GetProperty("sourceFunctionCount").GetInt32() == 27,
            "Magikarp source manifest scope differs.");
        var contractBytes = ReadPinned(root, generated + "magikarp-contract.json", ContractSha256, 59616);
        _ = ReadPinned(root, generated + "common-script-dependencies.inc", CommonScriptSha256, 2127);
        using var document = JsonDocument.Parse(contractBytes);
        var d = document.RootElement;
        Require(d.GetProperty("profile").GetString() == Profile && d.GetProperty("sourceCommit").GetString() == SourceCommit &&
            d.GetProperty("sourceTree").GetString() == SourceTree && d.GetProperty("sourceSetSha256").GetString() == SourceSetSha256 &&
            !d.GetProperty("gameplayEnabled").GetBoolean(), "Magikarp contract identity differs.");
        var sources = ImmutableArray.CreateBuilder<PretMagikarpSourceFile>();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in d.GetProperty("sourceFiles").EnumerateArray())
        {
            var source = new PretMagikarpSourceFile(entry.GetProperty("path").GetString()!, entry.GetProperty("rawModule").GetString()!,
                entry.GetProperty("byteLength").GetInt32(), entry.GetProperty("gitBlobSha1").GetString()!, entry.GetProperty("sha256").GetString()!);
            Require(source.RawModule is "route3-region" or "magikarp-gift" && paths.Add(source.Path), "Magikarp source path differs.");
            var bytes = ReadPinned(root, "content/raw/" + source.RawModule + "/" + source.Path, source.Sha256, source.ByteLength);
            var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == source.GitBlobSha1,
                "Magikarp source Git blob differs: " + source.Path);
            _ = new UTF8Encoding(false, true).GetString(bytes);
            sources.Add(source);
        }
        Require(sources.Count == 29 && sources.Count(s => s.RawModule == "magikarp-gift") == 5, "Magikarp source inventory is incomplete.");
        var material = string.Concat(sources.OrderBy(s => s.Path, StringComparer.Ordinal).Select(s => s.Path + "\0" + s.Sha256 + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(material)) == SourceSetSha256, "Magikarp source-set hash differs.");
        return new(d, sources.ToImmutable());
    }

    private static byte[] ReadPinned(string root, string relative, string sha256, int? length = null)
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
        var fileLength = new FileInfo(full).Length;
        Require(fileLength is > 0 and <= 2 * 1024 * 1024 && (length is null || fileLength == length), "Magikarp source/output byte length differs: " + relative);
        var bytes = File.ReadAllBytes(full);
        Require(bytes.Length == fileLength && Hash(bytes) == sha256, "Pinned Magikarp bytes differ: " + relative);
        return bytes;
    }

    private static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
