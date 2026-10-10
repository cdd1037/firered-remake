using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>Authenticated original ten USER FRAME variants. The caller selects source-proven windows only.</summary>
public sealed class PretOptionFrames
{
    public const string Profile = "firered-option-frames-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "bb167f16ab480a5aaf85b58849a11fab619a55ab32f53a625111d809a96b276a";
    public const string ManifestSha256 = "39559efc646909935ca70f391460b6aae8fcb5867f7f5b1ad64577e41c90fa6e";
    public const string RawManifestSha256 = "a041d5779296ef1ad49386ebe6521a91c387c6d3b34df405e13275f2a443d22d";
    public const string CatalogSha256 = "33c7019352500d66be9e17cceee0bbdde520da4303b3fe189bfd21105d4bece1";
    public const string FullSourceHash = "8d61326f390264312ae6120cde0cddeee9abc0795d9023391adbaa176c3fca9b";
    public const int FrameCount = 10;
    private static readonly string[] Kinds = ["frame", "battle-message", "action-menu", "move-menu"];
    private readonly ImmutableDictionary<string, byte[]> outputBytes;
    public ImmutableArray<OptionFrameAsset> Assets { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement Display { get; }
    public string GeneratedDirectory { get; }
    public string CatalogSourcePath => Path.Combine(GeneratedDirectory, "option-frames.json");
    public string ManifestSourcePath => Path.Combine(GeneratedDirectory, "manifest.json");

    private PretOptionFrames(string directory, ImmutableArray<OptionFrameAsset> assets,
        ImmutableArray<PretWorldSourceFile> files, JsonElement display, ImmutableDictionary<string, byte[]> outputs)
    {
        GeneratedDirectory = directory;
        Assets = assets;
        SourceFiles = files;
        Display = display;
        outputBytes = outputs;
        AdditionalSourceHashes = ["option-frames-manifest:" + ManifestSha256, "option-frames-lock:" + LockSha256,
            .. files.Select(f => "option-frames:" + f.Path + ":" + f.Sha256)];
    }

    /// <summary>Verifies the reviewed lock, both manifests, all source blobs and all texture/RGBA bytes.</summary>
    public static PretOptionFrames Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var raw = Path.Combine(project, "content/raw/option-frames");
        var directory = Path.Combine(project, "content/generated/option-frames");
        byte[] ReadPinned(string root, string path, string hash, int? length = null)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            Require(Hash(bytes) == hash && (length is null || bytes.Length == length), "Pinned option-frame file differs: " + path);
            return bytes;
        }
        _ = ReadPinned(raw, "manifest.json", RawManifestSha256);
        using var lockDocument = JsonDocument.Parse(ReadPinned(project, "scripts/fetch-option-frames.lock.json", LockSha256));
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        var l = lockDocument.RootElement;
        var m = manifest.RootElement;
        Require(l.GetProperty("schemaVersion").GetInt32() == 1 && l.GetProperty("profile").GetString() == Profile &&
            l.GetProperty("repository").GetString() == "pret/pokefirered" && l.GetProperty("commit").GetString() == SourceCommit &&
            l.GetProperty("tree").GetString() == SourceTree && l.GetProperty("gameEdition").GetString() == "FIRERED" &&
            l.GetProperty("romRevision").GetInt32() == 0, "Option-frame source identity differs.");
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == SourceCommit && m.GetProperty("tree").GetString() == SourceTree &&
            m.GetProperty("lockSha256").GetString() == LockSha256 && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("inputCount").GetInt32() == 24 && m.GetProperty("assetCount").GetInt32() == 40 &&
            !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean() &&
            !m.GetProperty("publiclyUploaded").GetBoolean(), "Option-frame manifest identity differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var entry in l.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            var hash = entry.GetProperty("sha256").GetString()!;
            var blob = entry.GetProperty("gitBlobSha1").GetString()!;
            var length = entry.GetProperty("byteLength").GetInt32();
            var bytes = ReadPinned(raw, path, hash, length);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob,
                "Option-frame Git blob identity differs: " + path);
            var url = $"https://github.com/pret/pokefirered/blob/{SourceCommit}/{path}";
            Require(entry.GetProperty("sourceUrl").GetString() == url, "Option-frame source URL differs.");
            files.Add(new(path, blob, length, hash, url));
        }
        var inventory = files.ToImmutable();
        Require(inventory.Length == 24 && inventory.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() == 24,
            "Option-frame source inventory differs.");
        Require(Hash(Encoding.UTF8.GetBytes(string.Concat(inventory.OrderBy(f => f.Path, StringComparer.Ordinal)
            .Select(f => f.Path + "\0" + f.Sha256 + "\n")))) == FullSourceHash, "Option-frame source-set hash differs.");
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in m.GetProperty("outputs").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, entry.GetProperty("sha256").GetString()!,
                entry.GetProperty("byteLength").GetInt32())), "Duplicate option-frame output.");
        }
        var expected = Enumerable.Range(1, FrameCount).SelectMany(i => Kinds.Select(kind => $"type{i}/{kind}"))
            .SelectMany(stem => new[] { stem + ".png", stem + ".rgba" }).Append("option-frames.json");
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected), "Option-frame output inventory differs.");
        Require(Hash(outputs["option-frames.json"]) == CatalogSha256, "Option-frame catalog differs.");
        using var catalog = JsonDocument.Parse(outputs["option-frames.json"]);
        var c = catalog.RootElement;
        Require(c.GetProperty("schemaVersion").GetInt32() == 1 && c.GetProperty("profile").GetString() == Profile &&
            c.GetProperty("sourceCommit").GetString() == SourceCommit && c.GetProperty("tree").GetString() == SourceTree &&
            c.GetProperty("fullSourceHash").GetString() == FullSourceHash, "Option-frame catalog identity differs.");
        var assets = ContentJson.ParseStrict<ImmutableArray<OptionFrameAsset>>(c.GetProperty("assets").GetRawText(), ContentJson.CreateOptions());
        Require(assets.Length == 40 && assets.Select(a => (a.FrameIndex, a.Kind)).Distinct().Count() == 40,
            "Option-frame asset inventory differs.");
        foreach (var asset in assets)
        {
            Require(asset.FrameIndex is >= 0 and < FrameCount && Kinds.Contains(asset.Kind, StringComparer.Ordinal),
                "Option-frame index or kind differs.");
            var stem = $"type{asset.FrameIndex + 1}/{asset.Kind}";
            bool frame = asset.Kind == "frame";
            Require(asset.TexturePath == stem + ".png" && asset.RgbaPath == stem + ".rgba" &&
                asset.Width == (frame ? 24 : 240) && asset.Height == (frame ? 24 : 48) &&
                asset.X == 0 && asset.Y == (frame ? 0 : 112) &&
                outputs[asset.RgbaPath].Length == asset.Width * asset.Height * 4 &&
                Hash(outputs[asset.RgbaPath]) == asset.RgbaSha256 && Hash(outputs[asset.TexturePath]) == asset.Sha256,
                "Option-frame texture metadata differs.");
            Require(asset.SourcePaths.Length == (frame ? 1 : 4) &&
                asset.SourcePaths[0] == $"graphics/text_window/type{asset.FrameIndex + 1}.png" &&
                asset.SourcePaths.All(p => inventory.Any(f => f.Path == p)), "Option-frame source reference differs.");
        }
        var display = c.GetProperty("display");
        Require(display.GetProperty("logicalWidth").GetInt32() == 240 && display.GetProperty("logicalHeight").GetInt32() == 160 &&
            display.GetProperty("frameIndexMinimum").GetInt32() == 0 && display.GetProperty("frameIndexMaximum").GetInt32() == 9 &&
            !display.GetProperty("battleMessageUsesUserFrame").GetBoolean(), "Option-frame display contract differs.");
        return new(directory, assets, inventory, display.Clone(), outputs.ToImmutable());
    }

    public OptionFrameAsset Asset(int frame, string kind)
    {
        if (frame is < 0 or >= FrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        if (!Kinds.Contains(kind, StringComparer.Ordinal)) throw new ArgumentException("Unsupported option-frame asset kind.", nameof(kind));
        return Assets.Single(a => a.FrameIndex == frame && a.Kind == kind);
    }
    public byte[] ReadTexture(int frame, string kind) => [.. outputBytes[Asset(frame, kind).TexturePath]];
    public byte[] ReadRgba(int frame, string kind) => [.. outputBytes[Asset(frame, kind).RgbaPath]];
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Unsafe option-frame path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Option-frame directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Option-frame file symlink is unsupported: " + path);
        }
        return result;
    }
}

public sealed record OptionFrameAsset(int FrameIndex, string Kind, string TexturePath, string RgbaPath,
    int Width, int Height, int X, int Y, string Sha256, string RgbaSha256, ImmutableArray<string> SourcePaths);
