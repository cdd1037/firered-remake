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
    private readonly ImmutableDictionary<string, byte[]> outputBytes;
    public ImmutableArray<StorageCustomizationWallpaper> Wallpapers { get; }
    public ImmutableArray<StorageCustomizationCategory> Categories { get; }
    public ImmutableArray<StorageArtAsset> Assets { get; }
    public ImmutableDictionary<string, string> Texts { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
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
    }

    /// <summary>Verifies fixed manifests, every raw source blob and all output bytes before exposing
    /// metadata. Runtime loading needs only these caches, never a reference checkout or network.</summary>
    public static PretStorageCustomization Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        var generated = Path.Combine(root, "content/generated/storage-customization");
        byte[] Read(string directory,string path)
        {
            byte[] bytes = File.ReadAllBytes(SafeFile(directory, path));
            return bytes;
        }
        using var sourceLock = JsonDocument.Parse(Read(root,"scripts/fetch-storage-customization.lock.json"));
        using var manifest = JsonDocument.Parse(Read(generated,"manifest.json"));
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var row in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            files.Add(new(path,$"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        var sourceFiles = files.ToImmutable();
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in manifest.RootElement.GetProperty("outputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, Read(generated,path)), "Duplicate customization output");
        }
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
            Require(PngRgba8.Decode(outputs[asset.TexturePath], asset.Width, asset.Height).Length == asset.Width * asset.Height * 4 , "Customization asset metadata differs");
        return result;
    }

    public StorageCustomizationWallpaper Wallpaper(int id) => Wallpapers.Single(w => w.Id == id);
    public string Text(string symbol) => Texts[symbol];
    public StorageArtAsset Asset(string id) => Assets.Single(a => a.Id == id);
    public byte[] ReadAssetTexture(string id) => [.. outputBytes[Asset(id).TexturePath]];
    public byte[] ReadAssetRgba(string id) => PngRgba8.Decode(outputBytes[Asset(id).TexturePath]);
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
