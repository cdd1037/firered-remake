using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record VermilionGymFieldMoveAsset(string Id, int Width, int Height, int FrameWidth, int FrameHeight, ImmutableArray<int> FrameDurations,
    string SourceImage, string SourcePalette, string? SourceTilemap);
public sealed record VermilionGymFieldMoveCadence(int BannerY, int BannerHeight, int BannerPaletteBank,
    int BackgroundScrollPerTick, int OpeningLeft, int OpeningRight, int OpeningTop, int OpeningBottom,
    int OpenHorizontalStep, int OpenVerticalStep, int OpenTop, int OpenBottom, int CloseVerticalStep,
    int CloseTop, int CloseBottom, int MonStartCenterX, int MonCenterY, int MonStopCenterX, int MonStep,
    int MonHoldTicks, int MonExitXExclusive);

/// <summary>Source Cut summon frames and outdoor BG0 strip. Runtime task/window scheduling belongs to presentation.</summary>
public sealed class PretVermilionGymFieldMoveArt
{
    public const string Profile = "firered-vermilion-gym-field-move-art-v1";
    private readonly ImmutableDictionary<string, byte[]> _outputs;
    public ImmutableArray<VermilionGymFieldMoveAsset> Assets { get; }
    public VermilionGymFieldMoveCadence Cadence { get; }
    private PretVermilionGymFieldMoveArt(ImmutableArray<VermilionGymFieldMoveAsset> assets,
        VermilionGymFieldMoveCadence cadence,
        ImmutableDictionary<string, byte[]> outputs)
    {
        Assets = assets; Cadence = cadence; _outputs = outputs;
    }
    public byte[] ReadPng(string id) => ReadAsset(id);

    private byte[] ReadAsset(string id) => _outputs.TryGetValue(id + ".png", out var bytes)
        ? (byte[])bytes.Clone() : throw new InvalidDataException("Unknown field-move art asset: " + id);

    public static PretVermilionGymFieldMoveArt Load(string root)
    {
        using var manifest = JsonDocument.Parse(Read(root,"content/generated/ui/vermilion-gym-field-move-art/manifest.json"));
        var document = manifest.RootElement;
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in document.GetProperty("outputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, Read(root,"content/generated/ui/vermilion-gym-field-move-art/" + path)), "Duplicate field-move output.");
        }
        var assets = ContentJson.ParseStrict<ImmutableArray<VermilionGymFieldMoveAsset>>(
            document.GetProperty("assets").GetRawText(), ContentJson.CreateOptions());
        Require(assets.Length == 3, "Field-move asset inventory differs.");
        for (int n = 0; n < assets.Length; n++)
        {
            var a = assets[n]; string id = n == 0 ? "red-field-move" : n == 1 ? "green-field-move" : "outdoor-streaks";
            bool player = n < 2;
            Require(a.Id == id && a.Width == (player ? 80 : 256) && a.Height == (player ? 32 : 80) &&
                a.FrameWidth == (player ? 16 : 256) && a.FrameHeight == a.Height &&
                a.FrameDurations.SequenceEqual(player ? new[] { 4,4,4,4,8 } : []) &&
                (outputs[id + ".png"]).Length > 0,
                "Field-move asset geometry/frame contract differs.");
        }
        var cadence = ContentJson.ParseStrict<VermilionGymFieldMoveCadence>(document.GetProperty("cadence").GetRawText(), ContentJson.CreateOptions());
        Require(cadence == new VermilionGymFieldMoveCadence(40,80,15,-16,240,241,80,81,-16,2,40,120,6,80,81,320,80,120,-20,30,-64),
            "Field-move source cadence differs.");
        return new(assets, cadence, outputs.ToImmutable());
    }
    private static byte[] Read(string root,string path)
    {
        Require(!Path.IsPathRooted(path) && !path.Contains('\\') &&
            !path.Split('/').Any(p => p is "" or "." or "..") && !root.Split(Path.DirectorySeparatorChar).Contains(".."), "Unsafe field-move asset path/length.");
        var info = new FileInfo(Path.Combine(Path.GetFullPath(root), path));
        Require(info.LinkTarget is null, "Field-move file symlink rejected.");
        for (var d = info.Directory; d is not null; d = d.Parent) Require(d.LinkTarget is null, "Field-move directory symlink rejected.");
var bytes = File.ReadAllBytes(info.FullName);
return bytes;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
