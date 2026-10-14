using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Independent authenticated extension to the original PC art. All sixteen wallpapers,
/// marking sprites and exact original menu order; existing PretStorageArt evidence stays unchanged.</summary>
public sealed class PretStorageCustomization
{
    public const string Profile = "firered-storage-customization-v1";
    public const string SourceCommit = PretStorageArt.SourceCommit;
    public const string SourceTree = PretStorageArt.SourceTree;
    public const string LockSha256 = "cc22c58ad864ca34097818d26732cb2d64e42411c1baac34001795e8c261777d";
    public const string ManifestSha256 = "44892922d6c7555c11a2bc320d66110f3bf6aa3ccff7792031b5c42d0df17bd2";
    public const string DocumentSha256 = "41a92b4878f59981b4c364b2c419e39f7dc76da88df764cb7d2c2a25ee6f7af6";
    public const string RawManifestSha256 = "27f0553b67094ab3a166ad6a18cef47dca63b88f73b63972fef638dd70a10f6e";
    public const string FullSourceHash = "9175435fb2dc1463b6c0c1a289834ac493b04be7520a132395f50b8a95e3d13e";
    private readonly ImmutableDictionary<string, byte[]> outputBytes;
    public ImmutableArray<StorageCustomizationWallpaper> Wallpapers { get; }
    public ImmutableArray<StorageCustomizationCategory> Categories { get; }
    public ImmutableArray<StorageArtAsset> Assets { get; }
    public ImmutableDictionary<string, string> Texts { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement Document { get; }
    public JsonElement Display => Document.GetProperty("display");

    private PretStorageCustomization(JsonElement document, ImmutableArray<PretWorldSourceFile> sources,
        ImmutableDictionary<string, byte[]> outputs)
    {
        Document = document; SourceFiles = sources; outputBytes = outputs;
        var options = ContentJson.CreateOptions();
        Wallpapers = ContentJson.ParseStrict<ImmutableArray<StorageCustomizationWallpaper>>(document.GetProperty("wallpapers").GetRawText(), options);
        Categories = ContentJson.ParseStrict<ImmutableArray<StorageCustomizationCategory>>(document.GetProperty("categories").GetRawText(), options);
        Assets = ContentJson.ParseStrict<ImmutableArray<StorageArtAsset>>(document.GetProperty("assets").GetRawText(), options);
        Texts = document.GetProperty("texts").EnumerateObject().ToImmutableDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
        AdditionalSourceHashes = ["storage-customization-manifest:" + ManifestSha256,
            "storage-customization-lock:" + LockSha256, .. sources.Select(f => "storage-customization:" + f.Path + ":" + f.Sha256)];
    }

    /// <summary>Verifies fixed manifests, every raw source blob and all output bytes before exposing
    /// metadata. Runtime loading needs only these caches, never a reference checkout or network.</summary>
    public static PretStorageCustomization Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        var raw = Path.Combine(root, "content/raw/storage-customization");
        var generated = Path.Combine(root, "content/generated/storage-customization");
        byte[] Read(string directory, string path, string hash, int? length = null)
        {
            byte[] bytes = File.ReadAllBytes(SafeFile(directory, path));
            Require(Hash(bytes) == hash && (length is null || bytes.Length == length), "Customization file differs: " + path);
            return bytes;
        }
        _ = Read(raw, "manifest.json", RawManifestSha256);
        using var sourceLock = JsonDocument.Parse(Read(root, "scripts/fetch-storage-customization.lock.json", LockSha256));
        using var manifest = JsonDocument.Parse(Read(generated, "manifest.json", ManifestSha256));
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var row in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!, hash = row.GetProperty("sha256").GetString()!;
            string blob = row.GetProperty("gitBlobSha1").GetString()!;
            int length = row.GetProperty("byteLength").GetInt32();
            var bytes = Read(raw, path, hash, length);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob,
                "Customization source Git blob differs: " + path);
            files.Add(new(path, blob, length, hash, $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        var sourceFiles = files.ToImmutable();
        Require(sourceFiles.Length == 52 && Hash(Encoding.UTF8.GetBytes(string.Concat(sourceFiles.OrderBy(f => f.Path, StringComparer.Ordinal)
            .Select(f => f.Path + "\0" + f.Sha256 + "\n")))) == FullSourceHash, "Customization source inventory differs");
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in manifest.RootElement.GetProperty("outputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, Read(generated, path, row.GetProperty("sha256").GetString()!,
                row.GetProperty("byteLength").GetInt32())), "Duplicate customization output");
        }
        Require(outputs.Count == 85 && Hash(outputs["storage-customization.json"]) == DocumentSha256,
            "Customization document inventory differs");
        using var document = JsonDocument.Parse(outputs["storage-customization.json"]);
        var result = new PretStorageCustomization(document.RootElement.Clone(), sourceFiles, outputs.ToImmutable());
        Require(result.Wallpapers.Length == PokemonStorageCustomizationRules.WallpaperCount && result.Categories.Length == 4 &&
            result.Assets.Length == 42 && result.Document.GetProperty("boxNameLength").GetInt32() == PokemonStorageCustomizationRules.BoxNameLength &&
            result.Document.GetProperty("boxNameAlphabet").GetString()!.ToHashSet().SetEquals(PokemonStorageCustomizationRules.BoxNameAlphabet),
            "Customization rules disagree with original source");
        for (int box = 0; box < PokemonStorageState.BoxCount; box++)
            Require(result.Document.GetProperty("defaultNames")[box].GetString() == PokemonStorageCustomizationRules.DefaultBoxName(box) &&
                result.Document.GetProperty("defaultWallpapers")[box].GetInt32() == PokemonStorageCustomizationRules.DefaultWallpaperId(box),
                "Customization source reset defaults differ");
        foreach (var asset in result.Assets)
            Require(outputs[asset.RgbaPath].Length == asset.Width * asset.Height * 4 && Hash(outputs[asset.RgbaPath]) == asset.RgbaSha256 &&
                Hash(outputs[asset.TexturePath]) == asset.Sha256, "Customization asset metadata differs");
        return result;
    }

    public StorageCustomizationWallpaper Wallpaper(int id) => Wallpapers.Single(w => w.Id == id);
    public string Text(string symbol) => Texts[symbol];
    public StorageArtAsset Asset(string id) => Assets.Single(a => a.Id == id);
    public byte[] ReadAssetTexture(string id) => [.. outputBytes[Asset(id).TexturePath]];
    public byte[] ReadAssetRgba(string id) => [.. outputBytes[Asset(id).RgbaPath]];
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Unsafe customization path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Customization directory symlink is unsupported");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Customization file symlink is unsupported: " + path);
        }
        return result;
    }
}

public sealed record StorageCustomizationWallpaper(int Id, string Symbol, string Name, int CategoryId,
    string AssetId, int TitleShadowRgb555, int TitleTextRgb555);
public sealed record StorageCustomizationCategory(int Id, string Name, ImmutableArray<int> WallpaperIds);
