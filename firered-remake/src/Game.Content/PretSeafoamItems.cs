using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Original Water Stone metadata and icon. Original evolution effect is retained; actual use remains explicitly unsupported.</summary>
public static class PretSeafoamItems
{
    public const string Base = "content/generated/seafoam-items/";
    public const string ItemsSha256 = "f603788e614d2744f8bac0613f8d178650b2e8fcd3b98575a14fae805eb47aff";
    public const string SourceInputsSha256 = "4ef16f32e5b196eed43d19099c367fccb6d87259b9a14c0108ec977cab845828";
    public const string LockSha256 = "1462ce454219a5366e47bc0e4bc8671cdf2ca662d42f032f8c127e6aa1bf1582";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [ItemsSha256, SourceInputsSha256, LockSha256];
    private static byte[] Read(string root, string relative, string hash, int? length = null)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, relative));
        if (length is { } size && bytes.Length != size || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("seafoam_item_bytes:" + relative);
        return bytes;
    }
    public static NpcTradeItemsDocument LoadDocument(string root)
    {
        var doc = JsonSerializer.Deserialize<NpcTradeItemsDocument>(Read(root, Base + "items.json", ItemsSha256),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("seafoam_item_document_missing");
        if (doc.SourceCommit != PokemonFactory.SourceCommit || doc.Profile != SeafoamShopCatalog.Profile ||
            doc.PreviousItemCount != 127 || doc.ItemCount != 128 || doc.Additions.IsDefault || doc.Additions.Length != 1 ||
            doc.Additions[0].Id != SeafoamShopCatalog.WaterStoneId || doc.Additions[0].UseSupport != EarlyShopUseSupport.NoFieldUse ||
            doc.Additions[0].FieldUseFunction != "FieldUseFunc_EvoItem" || !doc.Additions[0].EffectBytes.SequenceEqual([0,0,0,0,128,0]) ||
            doc.AdditionsSemanticSha256 != SeafoamShopCatalog.AdditionalItemsSourceSha256 || doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty ||
            doc.Icons.IsDefault || doc.Icons.Length != 1 || doc.Icons[0].Item != SeafoamShopCatalog.WaterStoneId || doc.Icons[0].Width != 24 || doc.Icons[0].Height != 24)
            throw new InvalidDataException("seafoam_item_scope");
        return doc;
    }
    public static SeafoamShopCatalog LoadShop(string root, NpcTradeShopCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var document = LoadDocument(root);
        return SeafoamShopCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = SeafoamShopCatalog.Profile, Items = predecessor.Catalog.Items.AddRange(document.Additions) });
    }
    public static byte[] LoadIconRgba(string root)
    {
        var icon = LoadDocument(root).Icons[0];
        _ = Read(root, icon.PngPath, icon.PngSha256);
        return Read(root, icon.RgbaPath, icon.RgbaSha256, 24 * 24 * 4);
    }
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        using var locked = JsonDocument.Parse(Read(root, "scripts/seafoam-items.lock.json", LockSha256));
        using var inputs = JsonDocument.Parse(Read(root, Base + "source-input-hashes.json", SourceInputsSha256));
        if (inputs.RootElement.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.RootElement.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("seafoam_item_source_identity");
        foreach (var row in inputs.RootElement.GetProperty("inputs").EnumerateArray())
        {
            var data = Read(row.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root,
                row.GetProperty("localPath").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {data.Length}\0"), .. data])) != row.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("seafoam_item_source_blob");
        }
        foreach (var row in inputs.RootElement.GetProperty("dependencyInputs").EnumerateArray())
            _ = Read(dependencyRoot ?? root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        _ = Read(root, "scripts/import-seafoam-items.py", locked.RootElement.GetProperty("importerSha256").GetString()!);
        _ = LoadDocument(root); _ = LoadIconRgba(root);
    }
}
