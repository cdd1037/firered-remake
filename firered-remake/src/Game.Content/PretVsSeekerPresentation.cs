using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Content;

public sealed record VsSeekerPresentationAsset(string Id, string Path, int Width, int Height, int FrameWidth, int FrameHeight, int FrameCount,
    string SourcePath, ImmutableArray<int> SourceFrames, bool Reused);

/// <summary>Authenticated, bounded presentation resources. Runtime shares existing field-move pixels,
/// trainer sheets and original synth pack; only missing scanner/icon pixels are added.</summary>
public sealed class PretVsSeekerPresentation
{
    public const string Base = "content/generated/vs-seeker-presentation/";
    public const string Profile = "firered-vs-seeker-presentation-v1";
    private readonly ImmutableDictionary<string, byte[]> _textures;
    private readonly byte[] _itemIcon;
    public ImmutableArray<VsSeekerPresentationAsset> Assets { get; }
    /// <summary>Exact EarlyShopItemDefinition JSON, including original QuestItemDefinition SourceMetadata.
    /// Generic item use stays NoFieldUse; the application key-item owner handles the original field callback.</summary>
    public JsonElement ItemDefinition { get; }
    /// <summary>Original script encoding, including literal \n / \p / \l and the terminal '$'.</summary>
    public ImmutableDictionary<string, string> EncodedTexts { get; }
    public JsonElement TextSources { get; }
    private PretVsSeekerPresentation(ImmutableArray<VsSeekerPresentationAsset> assets,
        ImmutableDictionary<string, byte[]> textures, byte[] itemIcon, JsonElement item,
        ImmutableDictionary<string, string> texts, JsonElement textSources)
    { Assets = assets; _textures = textures; _itemIcon = itemIcon; ItemDefinition = item; EncodedTexts = texts; TextSources = textSources; }
    public VsSeekerPresentationAsset Asset(string id) => Assets.Single(a => a.Id == id);
    public byte[] ReadPng(string id) => _textures.TryGetValue(id, out var data) ? (byte[])data.Clone()
        : throw new ArgumentException("Unknown Vs Seeker asset.", nameof(id));
    public byte[] ReadItemIconRgba() => (byte[])_itemIcon.Clone();

    public static PretVsSeekerPresentation Load(string root, string? dependencyRoot = null)
    {
        using var doc = JsonDocument.Parse(Read(root,Base + "manifest.json"));
        var json = doc.RootElement;
        if (json.GetProperty("schemaVersion").GetInt32() != 1 )
            throw new InvalidDataException("Vs Seeker source identity differs.");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        var assets = json.GetProperty("assets").Deserialize<ImmutableArray<VsSeekerPresentationAsset>>(options);
        if (assets.IsDefault || assets.Length != 8 || assets.Count(a => a.Reused) != 2 ||
            assets.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != 8)
            throw new InvalidDataException("Vs Seeker asset inventory differs.");
        var textures = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var asset in assets)
        {
            if (asset.Width != asset.FrameWidth * asset.FrameCount || asset.Height != asset.FrameHeight)
                throw new InvalidDataException("Vs Seeker frame geometry differs.");
            textures.Add(asset.Id, Read(asset.Reused ? dependencyRoot ?? root : root,asset.Path));
        }
        byte[] iconRgba = PngRgba8.Decode(Read(root,Base + "item-vs-seeker.png"));
        var texts = json.GetProperty("texts").EnumerateArray().ToImmutableDictionary(
            e => e.GetProperty("symbol").GetString()!, e => e.GetProperty("encodedText").GetString()!, StringComparer.Ordinal);
        if (texts.Count != 6 || texts.Values.Any(t => !t.EndsWith('$')))
            throw new InvalidDataException("Vs Seeker original text inventory differs.");
        return new(assets, textures.ToImmutable(), iconRgba, json.GetProperty("itemDefinition").Clone(),
            texts, json.GetProperty("texts").Clone());
    }
    private static byte[] Read(string root,string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains('\\') ||
            relative.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Vs Seeker asset path/length.");
        var info = new FileInfo(Path.Combine(Path.GetFullPath(root), relative));
        if (info.LinkTarget is not null) throw new InvalidDataException("Vs Seeker asset symlink rejected.");
        for (var d = info.Directory; d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("Vs Seeker directory symlink rejected.");
var bytes = File.ReadAllBytes(info.FullName);
return bytes;
    }
}
