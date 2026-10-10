using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record VermilionGymFieldMoveAsset(string Id, int Width, int Height, int FrameWidth, int FrameHeight,
    ImmutableArray<int> FrameDurations, string SourceImage, string SourcePalette, string? SourceTilemap,
    string RgbaSha256, string PngSha256);
public sealed record VermilionGymFieldMoveCadence(int BannerY, int BannerHeight, int BannerPaletteBank,
    int BackgroundScrollPerTick, int OpeningLeft, int OpeningRight, int OpeningTop, int OpeningBottom,
    int OpenHorizontalStep, int OpenVerticalStep, int OpenTop, int OpenBottom, int CloseVerticalStep,
    int CloseTop, int CloseBottom, int MonStartCenterX, int MonCenterY, int MonStopCenterX, int MonStep,
    int MonHoldTicks, int MonExitXExclusive);

/// <summary>Source Cut summon frames and outdoor BG0 strip. Runtime task/window scheduling belongs to presentation.</summary>
public sealed class PretVermilionGymFieldMoveArt
{
    public const string Profile = "firered-vermilion-gym-field-move-art-v1";
    public const string LockSha256 = "82f5d97a1aa962ce477a7afa19bb82389f0d3d197957783f2254b92cc1ef6670";
    public const string ManifestSha256 = "6bed89e3af667ba3043ee2c485202c99063a1c43ab405abdf84e17670c70770f";
    private readonly ImmutableDictionary<string, byte[]> _outputs;
    public ImmutableArray<VermilionGymFieldMoveAsset> Assets { get; }
    public VermilionGymFieldMoveCadence Cadence { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretVermilionGymFieldMoveArt(ImmutableArray<VermilionGymFieldMoveAsset> assets,
        VermilionGymFieldMoveCadence cadence, ImmutableArray<PretWorldSourceFile> files,
        ImmutableDictionary<string, byte[]> outputs)
    {
        Assets = assets; Cadence = cadence; SourceFiles = files; _outputs = outputs;
        AdditionalSourceHashes = ["vermilion-gym-field-move-art-manifest:" + ManifestSha256,
            "vermilion-gym-field-move-art-lock:" + LockSha256,
            .. files.Select(f => "vermilion-gym-field-move-art:" + f.Path + ":" + f.Sha256)];
    }
    public byte[] ReadPng(string id) => ReadAsset(id, ".png");
    public byte[] ReadRgba(string id) => ReadAsset(id, ".rgba");
    private byte[] ReadAsset(string id, string extension) => _outputs.TryGetValue(id + extension, out var bytes)
        ? (byte[])bytes.Clone() : throw new InvalidDataException("Unknown field-move art asset: " + id);

    public static PretVermilionGymFieldMoveArt Load(string root)
    {
        using var locked = JsonDocument.Parse(Read(root, "scripts/vermilion-gym-assets-field-move.lock.json", LockSha256, 4379));
        using var manifest = JsonDocument.Parse(Read(root, "content/generated/vermilion-gym-field-move-art/manifest.json", ManifestSha256, 8087));
        var document = manifest.RootElement; var sourceLock = locked.RootElement;
        Require(sourceLock.GetProperty("repository").GetString() == "pret/pokefirered" &&
            sourceLock.GetProperty("commit").GetString() == PretVermilionGymAssets.SourceCommit &&
            sourceLock.GetProperty("tree").GetString() == PretVermilionGymAssets.SourceTree &&
            document.GetProperty("schemaVersion").GetInt32() == 1 && document.GetProperty("profile").GetString() == Profile &&
            document.GetProperty("sourceCommit").GetString() == PretVermilionGymAssets.SourceCommit &&
            document.GetProperty("sourceTree").GetString() == PretVermilionGymAssets.SourceTree &&
            document.GetProperty("lockSha256").GetString() == LockSha256 &&
            document.GetProperty("transparentIndex").GetInt32() == 0 &&
            document.GetProperty("paletteExpansion").GetString() == "(rgb5 << 3) | (rgb5 >> 2)" &&
            JsonElement.DeepEquals(document.GetProperty("inputs"), sourceLock.GetProperty("files")) &&
            JsonElement.DeepEquals(document.GetProperty("localInputs"), sourceLock.GetProperty("inputs")),
            "Field-move source identity differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var row in document.GetProperty("inputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!, hash = row.GetProperty("sha256").GetString()!,
                blob = row.GetProperty("gitBlobSha1").GetString()!;
            int length = row.GetProperty("byteLength").GetInt32();
            byte[] bytes = Read(root, "content/raw/vermilion-gym-field-move-art/" + path, hash, length);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob,
                "Field-move original Git blob differs.");
            files.Add(new(path, blob, length, hash, $"https://github.com/pret/pokefirered/blob/{PretVermilionGymAssets.SourceCommit}/{path}"));
        }
        Require(files.Count == 16 && files.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() == 16, "Field-move source inventory differs.");
        foreach (var row in document.GetProperty("localInputs").EnumerateArray())
            _ = Read(root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in document.GetProperty("outputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, Read(root, "content/generated/vermilion-gym-field-move-art/" + path,
                row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32())), "Duplicate field-move output.");
        }
        Require(outputs.Count == 6, "Field-move output inventory differs.");
        var assets = ContentJson.ParseStrict<ImmutableArray<VermilionGymFieldMoveAsset>>(
            document.GetProperty("assets").GetRawText(), ContentJson.CreateOptions());
        Require(assets.Length == 3, "Field-move asset inventory differs.");
        for (int n = 0; n < assets.Length; n++)
        {
            var a = assets[n]; string id = n == 0 ? "red-field-move" : n == 1 ? "green-field-move" : "outdoor-streaks";
            bool player = n < 2;
            Require(a.Id == id && a.Width == (player ? 80 : 256) && a.Height == (player ? 32 : 80) &&
                a.FrameWidth == (player ? 16 : 256) && a.FrameHeight == a.Height &&
                a.FrameDurations.SequenceEqual(player ? new[] { 4, 4, 4, 4, 8 } : []) &&
                outputs[id + ".rgba"].Length == a.Width * a.Height * 4 &&
                Hash(outputs[id + ".rgba"]) == a.RgbaSha256 && Hash(outputs[id + ".png"]) == a.PngSha256,
                "Field-move asset geometry/frame contract differs.");
        }
        var cadence = ContentJson.ParseStrict<VermilionGymFieldMoveCadence>(document.GetProperty("cadence").GetRawText(), ContentJson.CreateOptions());
        Require(cadence == new VermilionGymFieldMoveCadence(40,80,15,-16,240,241,80,81,-16,2,40,120,6,80,81,320,80,120,-20,30,-64),
            "Field-move source cadence differs.");
        return new(assets, cadence, files.ToImmutable(), outputs.ToImmutable());
    }
    private static byte[] Read(string root, string path, string hash, int length)
    {
        Require(length is > 0 and <= 3_000_000 && !Path.IsPathRooted(path) && !path.Contains('\\') &&
            !path.Split('/').Any(p => p is "" or "." or "..") && !root.Split(Path.DirectorySeparatorChar).Contains(".."), "Unsafe field-move asset path/length.");
        var info = new FileInfo(Path.Combine(Path.GetFullPath(root), path));
        Require(info.LinkTarget is null, "Field-move file symlink rejected.");
        for (var d = info.Directory; d is not null; d = d.Parent) Require(d.LinkTarget is null, "Field-move directory symlink rejected.");
        using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        Require(stream.Length == length, "Field-move file length differs.");
        byte[] data = new byte[length]; stream.ReadExactly(data);
        Require(stream.ReadByte() == -1 && Hash(data) == hash, "Field-move file hash differs."); return data;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
