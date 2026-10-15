using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>Generated image descriptors for the ten USER FRAME variants.</summary>
public sealed class PretOptionFrames
{
    public const int FrameCount = 10;
    private static readonly string[] Kinds = ["frame", "battle-message", "action-menu", "move-menu"];
    public ImmutableArray<OptionFrameAsset> Assets { get; }
    public JsonElement Display { get; }
    public string GeneratedDirectory { get; }

    private PretOptionFrames(string directory, ImmutableArray<OptionFrameAsset> assets, JsonElement display)
    {
        GeneratedDirectory = directory;
        Assets = assets;
        Display = display;
    }

    /// <summary>Reads metadata only; Godot owns native image decoding and texture caching.</summary>
    public static PretOptionFrames Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/option-frames");
        using var catalog = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "option-frames.json")));
        var c = catalog.RootElement;
        Require(c.GetProperty("schemaVersion").GetInt32() == 1, "Unsupported option-frame catalog schema.");
        var assets = ContentJson.ParseStrict<ImmutableArray<OptionFrameAsset>>(c.GetProperty("assets").GetRawText(), ContentJson.CreateOptions());
        Require(assets.Length == 40 && assets.Select(a => (a.FrameIndex, a.Kind)).Distinct().Count() == 40,
            "Option-frame asset inventory differs.");
        foreach (var asset in assets)
        {
            Require(asset.FrameIndex is >= 0 and < FrameCount && Kinds.Contains(asset.Kind, StringComparer.Ordinal),
                "Option-frame index or kind differs.");
            bool shared = asset.Kind == "battle-message" || asset.FrameIndex == 0;
            var texturePath = shared
                ? $"content/generated/lab-battle-ui/{(asset.Kind == "frame" ? "frame1" : asset.Kind)}.png"
                : "content/generated/option-frames/frames.png";
            bool frame = asset.Kind == "frame";
            Require(asset.TexturePath == texturePath &&
                asset.Width == (frame ? 24 : 240) && asset.Height == (frame ? 24 : 48) &&
                asset.X == 0 && asset.Y == (frame ? 0 : 112), "Option-frame texture metadata differs.");
            Require(shared ? asset.Region is null : asset.Region is { X: >= 1, Y: >= 1 } r &&
                r.Width == asset.Width && r.Height == asset.Height &&
                (long)r.X + r.Width < 1024 && (long)r.Y + r.Height < 1024,
                "Option-frame atlas region differs.");
        }
        foreach (var path in assets.Select(a => a.TexturePath).Distinct(StringComparer.Ordinal))
            Require(File.Exists(Path.Combine(project, path)), "Missing option-frame texture: " + path);
        var display = c.GetProperty("display");
        Require(display.GetProperty("logicalWidth").GetInt32() == 240 && display.GetProperty("logicalHeight").GetInt32() == 160 &&
            display.GetProperty("frameIndexMinimum").GetInt32() == 0 && display.GetProperty("frameIndexMaximum").GetInt32() == 9 &&
            !display.GetProperty("battleMessageUsesUserFrame").GetBoolean(), "Option-frame display contract differs.");
        return new(directory, assets, display.Clone());
    }

    public OptionFrameAsset Asset(int frame, string kind)
    {
        if (frame is < 0 or >= FrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        if (!Kinds.Contains(kind, StringComparer.Ordinal)) throw new ArgumentException("Unsupported option-frame asset kind.", nameof(kind));
        return Assets.Single(a => a.FrameIndex == frame && a.Kind == kind);
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}

public sealed record OptionFrameAsset(int FrameIndex, string Kind, string TexturePath, int Width, int Height, int X, int Y,
    ImageRegion? Region = null);
