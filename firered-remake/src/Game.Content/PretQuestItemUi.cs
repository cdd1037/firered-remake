using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Items;

namespace Game.Content;

public sealed record QuestItemUiAsset(string Id, string TexturePath, int Width, int Height,
    int X, int Y, string Sha256, string RgbaSha256);
public sealed record QuestItemUiTextSource(string Path, string Symbol, string? Field,
    int StartLine, ImmutableArray<string> TerminalControls);
public sealed record QuestItemUiEntry(string Id, string SourceSymbol, string Name, string Description,
    string Icon, string FieldUseFunction);
public sealed record QuestItemUiDocument(int SchemaVersion, string Profile, string SourceCommit,
    string FullSourceHash, ImmutableArray<PretQuestItemInput> Inputs, ImmutableArray<string> Pockets,
    ImmutableDictionary<string, string> Texts, ImmutableArray<QuestItemUiTextSource> TextSources,
    ImmutableArray<QuestItemUiEntry> Items, ImmutableArray<QuestItemUiAsset> Assets,
    ImmutableArray<string> Limitations);

/// <summary>Only local, fixed Parcel/Poke Ball text and static male pocket art; no inventory actions.</summary>
public static class PretQuestItemUi
{
    public const string Profile = "firered-quest-item-ui-static-v1";
    public const string GeneratedSha256 = "80b2ca79e738d57fede3426b52b6e208fec031c970219cf6d2d39b3408f47b83";
    public const string SourceManifestSha256 = "8cc32d516018f8c6b1b847f886f4123b55321920874522ca5f54953a35cec4b2";

    public static QuestItemUiDocument Load(string json)
    {
        if (json is null || Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))) != GeneratedSha256)
            throw new InvalidDataException("Quest item UI catalog hash differs; run the verified importer.");
        var doc = ContentJson.ParseStrict<QuestItemUiDocument>(json, ContentJson.CreateOptions());
        if (doc.SchemaVersion != 1 || doc.Profile != Profile || doc.SourceCommit != QuestItemRules.SourceCommit
            || doc.FullSourceHash != SourceManifestSha256 || doc.Inputs.Length != 18
            || !doc.Pockets.SequenceEqual(new[] { "POCKET_ITEMS", "POCKET_KEY_ITEMS", "POCKET_POKE_BALLS" })
            || doc.Items.Length != 2 || doc.Assets.Length != 5 || doc.Texts.Count != 4 || doc.TextSources.Length != 8)
            throw new InvalidDataException("Quest item UI profile is incomplete.");
        return doc;
    }

    public static void ValidateCatalog(QuestItemUiDocument doc, QuestItemsCatalog catalog)
    {
        QuestItemRules.ValidateCatalog(catalog);
        foreach (var ui in doc.Items)
        {
            var item = catalog.Items.Single(x => x.Id.Value == ui.Id);
            if (item.SourceItemSymbol != ui.SourceSymbol || item.Name != ui.Name || item.FieldUseFunction != ui.FieldUseFunction)
                throw new InvalidDataException("Quest item UI differs from the authoritative item catalog.");
        }
    }

    public static byte[] ReadTexture(string directory, QuestItemUiAsset asset)
    {
        if (asset.TexturePath != asset.Id + ".png" || asset.TexturePath.Contains('/') || asset.TexturePath.Contains('\\')
            || asset.TexturePath.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("Unsafe quest item UI texture path.");
        var path = Path.Combine(directory, asset.TexturePath);
        if (new FileInfo(path).LinkTarget is not null) throw new InvalidDataException("Quest item UI texture symlink rejected.");
        var bytes = File.ReadAllBytes(path);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != asset.Sha256)
            throw new InvalidDataException("Quest item UI texture hash differs: " + asset.Id);
        return bytes;
    }
}
