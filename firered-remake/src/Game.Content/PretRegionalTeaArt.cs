using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record RegionalTeaArtDocument(int SchemaVersion, string Profile, string SourceCommit,
    string LockSha256, ImmutableArray<PretRegionalGiftSourceFile> Inputs, QuestItemUiEntry Item,
    QuestItemUiAsset Asset, string RgbaPath, ImmutableArray<string> Limitations);

/// <summary>Original Tea pixels and source item metadata. Loading admits no field-use or inventory action.</summary>
public sealed class PretRegionalTeaArt
{
    public const string Profile = "firered-regional-tea-art-v1";
    public const string LockSha256 = "ade0658bf9e37e19575a954c0e70254769ab1f2e3e8a1ebcc2f985a3f15c95ac";
    public const string DocumentSha256 = "bc61a34cea38055beb8108a36b6ef0a68cd0a419d55c0d6bb8b2e020176993af";
    public const string GeneratedDirectory = "content/generated/regional-tea-art/";
    public RegionalTeaArtDocument Document { get; }
    private readonly byte[] _rgba;
    private PretRegionalTeaArt(RegionalTeaArtDocument document, byte[] rgba) { Document = document; _rgba = rgba; }
    public byte[] ReadRgba() => [.. _rgba];

    public static PretRegionalTeaArt Load(string projectRoot, PewterMachineItem tea)
    {
        var root = Path.GetFullPath(projectRoot);
        var lockBytes = Read(root, "scripts/regional-tea-art.lock.json", LockSha256);
        using var sourceLock = JsonDocument.Parse(lockBytes);
        var lockRoot = sourceLock.RootElement;
        Require(lockRoot.GetProperty("schemaVersion").GetInt32() == 1 && lockRoot.GetProperty("profile").GetString() == Profile &&
            lockRoot.GetProperty("commit").GetString() == PretRegionalGiftContract.SourceCommit &&
            lockRoot.GetProperty("tree").GetString() == PretRegionalGiftContract.SourceTree &&
            lockRoot.GetProperty("repository").GetString() == "pret/pokefirered", "Tea art source identity differs.");
        var document = ContentJson.ParseStrict<RegionalTeaArtDocument>(Encoding.UTF8.GetString(
            Read(root, GeneratedDirectory + "tea-art.json", DocumentSha256)), ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.Profile == Profile && document.SourceCommit == PretRegionalGiftContract.SourceCommit &&
            document.LockSha256 == LockSha256 && document.Inputs.Length == 6 && !document.Limitations.IsDefaultOrEmpty,
            "Tea art scope differs.");
        var lockRows = ContentJson.ParseStrict<ImmutableArray<PretRegionalGiftSourceFile>>(
            lockRoot.GetProperty("files").GetRawText(), ContentJson.CreateOptions());
        Require(document.Inputs.SequenceEqual(lockRows), "Tea art source inventory differs.");
        foreach (var entry in document.Inputs)
        {
            var bytes = Read(root, "content/raw/regional-tea-art/" + entry.Path, entry.Sha256, entry.ByteLength);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) == entry.GitBlobSha1,
                "Tea art Git blob differs: " + entry.Path);
        }
        var item = tea.Item;
        var ui = document.Item;
        Require(item.Id.Value == "pret:ITEM_TEA" && item.SourceItemId == 369 && item.SourceItemSymbol == "ITEM_TEA" &&
            item.Pocket == "POCKET_KEY_ITEMS" && item.Price == 0 && !item.CanToss && !item.CanSell && !item.CanGive &&
            item.FieldUseFunction == "FieldUseFunc_OakStopsYou" && ui.Id == item.Id.Value && ui.SourceSymbol == item.SourceItemSymbol &&
            ui.Name == item.Name && ui.Description == tea.Description && ui.FieldUseFunction == item.FieldUseFunction,
            "Tea presentation differs from the authoritative key item.");
        var asset = document.Asset;
        Require(asset.Id == "regional-tea" && ui.Icon == asset.Id && asset.TexturePath == "regional-tea.png" &&
            document.RgbaPath == "regional-tea.rgba" && (asset.Width, asset.Height, asset.X, asset.Y) == (24, 24, 8, 124),
            "Tea icon layout differs.");
        _ = Read(root, GeneratedDirectory + asset.TexturePath, asset.Sha256);
        var rgba = Read(root, GeneratedDirectory + document.RgbaPath, asset.RgbaSha256, 24 * 24 * 4);
        return new(document, rgba);
    }

    private static byte[] Read(string root, string relative, string hash, int? size = null)
    {
        Require(!Path.IsPathRooted(relative) && !relative.Contains('\\') && !relative.Split('/').Any(p => p is ".." or "." or ""), "Unsafe Tea art path.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        for (FileSystemInfo? current = new FileInfo(path); current is not null; current = current switch
            { FileInfo file => file.Directory, DirectoryInfo directory => directory.Parent, _ => null })
            Require(current.LinkTarget is null, "Tea art symlink rejected.");
        long length = new FileInfo(path).Length;
        Require(length is > 0 and <= 2 * 1024 * 1024 && (size is null || length == size), "Tea art byte length differs.");
        var bytes = File.ReadAllBytes(path);
        Require(bytes.Length == length && Convert.ToHexStringLower(SHA256.HashData(bytes)) == hash, "Tea art bytes differ: " + relative);
        return bytes;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
