using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Generated PC wallpapers, marking sprites and source menu labels in their original order.</summary>
public sealed class PretStorageCustomization
{
    public const string Profile = "firered-storage-customization-v1";
    public const string SourceCommit = PretStorageArt.SourceCommit;
    private readonly ImmutableDictionary<string, byte[]> outputBytes;
    public ImmutableArray<StorageCustomizationWallpaper> Wallpapers { get; }
    public ImmutableArray<StorageCustomizationCategory> Categories { get; }
    public ImmutableArray<StorageArtAsset> Assets { get; }
    public ImmutableDictionary<string, string> Texts { get; }
    public JsonElement Document { get; }
    public JsonElement Display => Document.GetProperty("display");

    private PretStorageCustomization(JsonElement document, ImmutableDictionary<string, byte[]> outputs,
        IReadOnlyDictionary<string, string> sourceTexts)
    {
        Document = document; outputBytes = outputs;
        var options = ContentJson.CreateOptions();
        Wallpapers = ContentJson.ParseStrict<ImmutableArray<StorageCustomizationWallpaper>>(document.GetProperty("wallpapers").GetRawText(), options);
        Categories = ContentJson.ParseStrict<ImmutableArray<StorageCustomizationCategory>>(document.GetProperty("categories").GetRawText(), options);
        Assets = ContentJson.ParseStrict<ImmutableArray<StorageArtAsset>>(document.GetProperty("assets").GetRawText(), options);
        Texts = document.GetProperty("textSymbols").EnumerateArray().Select(row => row.GetString()!)
            .ToImmutableDictionary(symbol => symbol, symbol => sourceTexts[symbol], StringComparer.Ordinal);
        Wallpapers = Wallpapers.Select(row => row with { Name = Texts[row.NameTextSymbol] }).ToImmutableArray();
        Categories = Categories.Select(row => row with { Name = Texts[row.NameTextSymbol] }).ToImmutableArray();
    }

    /// <summary>Loads generated artwork and layout, and projects labels from the shared source text bank.</summary>
    public static PretStorageCustomization Load(string projectRoot, IReadOnlyDictionary<string, string>? sourceTexts = null)
    {
        var root = Path.GetFullPath(projectRoot);
        var generated = Path.Combine(root, "content/generated/ui/storage-customization");
        byte[] Read(string directory,string path)
        {
            byte[] bytes = File.ReadAllBytes(GeneratedResourceFile.SafePath(directory, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(Read(generated,"manifest.json"));

        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in manifest.RootElement.GetProperty("outputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, Read(generated,path)), "Duplicate customization output");
        }
        using var document = JsonDocument.Parse(outputs["storage-customization.json"]);
        var result = new PretStorageCustomization(document.RootElement.Clone(), outputs.ToImmutable(), sourceTexts ?? PretBattleText.Load(root));
        Require(result.Wallpapers.Length == PokemonStorageCustomizationRules.WallpaperCount && result.Categories.Length == 4 &&
            result.Assets.Length == 42 && result.Document.GetProperty("boxNameLength").GetInt32() == PokemonStorageCustomizationRules.BoxNameLength &&
            result.Document.GetProperty("boxNameAlphabet").GetString()!.ToHashSet().SetEquals(PokemonStateValidator.NicknameAlphabet),
            "Customization rules disagree with original source");
        for (int box = 0; box < PokemonStorageState.BoxCount; box++)
            Require(result.Document.GetProperty("defaultNames")[box].GetString() == PokemonStorageCustomizationRules.DefaultBoxName(box) &&
                result.Document.GetProperty("defaultWallpapers")[box].GetInt32() == PokemonStorageCustomizationRules.DefaultWallpaperId(box),
                "Customization source reset defaults differ");
        foreach (var asset in result.Assets)
            Require((outputs[asset.TexturePath]).Length > 0, "Customization asset metadata differs");
        return result;
    }

    public StorageCustomizationWallpaper Wallpaper(int id) => Wallpapers.Single(w => w.Id == id);
    public string Text(string symbol) => Texts[symbol];
    public StorageArtAsset Asset(string id) => Assets.Single(a => a.Id == id);
    public byte[] ReadAssetTexture(string id) => [.. outputBytes[Asset(id).TexturePath]];

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}

public sealed record StorageCustomizationWallpaper(int Id, string Symbol, int CategoryId,
    string AssetId, int TitleShadowRgb555, int TitleTextRgb555, string NameTextSymbol)
{
    public string Name { get; init; } = "";
}
public sealed record StorageCustomizationCategory(int Id, ImmutableArray<int> WallpaperIds, string NameTextSymbol)
{
    public string Name { get; init; } = "";
}
