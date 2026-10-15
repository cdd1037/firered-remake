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
    public const int FrameCount = 10;
    private static readonly string[] Kinds = ["frame", "battle-message", "action-menu", "move-menu"];
    private readonly ImmutableDictionary<string, byte[]> outputBytes;
    public ImmutableArray<OptionFrameAsset> Assets { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
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
    }

    /// <summary>Verifies the reviewed lock, both manifests, all source blobs and all texture/RGBA bytes.</summary>
    public static PretOptionFrames Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/option-frames");
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var lockDocument = JsonDocument.Parse(ReadPinned(project,"scripts/fetch-option-frames.lock.json"));
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var l = lockDocument.RootElement;
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("assetCount").GetInt32() == 40 &&
            !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean() &&
            !m.GetProperty("publiclyUploaded").GetBoolean(), "Option-frame manifest identity differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var entry in l.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            var url = $"https://github.com/pret/pokefirered/blob/{SourceCommit}/{path}";
            Require(entry.GetProperty("sourceUrl").GetString() == url, "Option-frame source URL differs.");
            files.Add(new(path,url));
        }
        var inventory = files.ToImmutable();
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in m.GetProperty("outputs").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate option-frame output.");
        }
        var expected = Enumerable.Range(1, FrameCount).SelectMany(i => Kinds.Select(kind => $"type{i}/{kind}"))
            .Select(stem => stem + ".png").Append("option-frames.json");
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected), "Option-frame output inventory differs.");
        using var catalog = JsonDocument.Parse(outputs["option-frames.json"]);
        var c = catalog.RootElement;
        Require(c.GetProperty("schemaVersion").GetInt32() == 1 , "Option-frame catalog identity differs.");
        var assets = ContentJson.ParseStrict<ImmutableArray<OptionFrameAsset>>(c.GetProperty("assets").GetRawText(), ContentJson.CreateOptions());
        Require(assets.Length == 40 && assets.Select(a => (a.FrameIndex, a.Kind)).Distinct().Count() == 40,
            "Option-frame asset inventory differs.");
        foreach (var asset in assets)
        {
            Require(asset.FrameIndex is >= 0 and < FrameCount && Kinds.Contains(asset.Kind, StringComparer.Ordinal),
                "Option-frame index or kind differs.");
            var stem = $"type{asset.FrameIndex + 1}/{asset.Kind}";
            bool frame = asset.Kind == "frame";
            Require(asset.TexturePath == stem + ".png" && asset.TexturePath == stem + ".png" &&
                asset.Width == (frame ? 24 : 240) && asset.Height == (frame ? 24 : 48) &&
                asset.X == 0 && asset.Y == (frame ? 0 : 112) &&
                PngRgba8.Decode(outputs[asset.TexturePath], asset.Width, asset.Height).Length == asset.Width * asset.Height * 4 ,
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
    public byte[] ReadRgba(int frame, string kind) => PngRgba8.Decode(outputBytes[Asset(frame, kind).TexturePath]);
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

public sealed record OptionFrameAsset(int FrameIndex, string Kind, string TexturePath, int Width, int Height, int X, int Y,
    ImmutableArray<string> SourcePaths);
