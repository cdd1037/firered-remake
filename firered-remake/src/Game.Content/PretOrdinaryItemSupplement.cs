using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Original ordinary item definitions and pixels. Effect availability remains explicit.</summary>
public static class PretOrdinaryItemSupplement
{
    public const string Base = "content/generated/ordinary-item-supplement/";
    public const string ItemsSha256 = "cafeee34bd8d22354aa1904f67205804dae8e4fd46b673f16b0b2e1c451deba5";
    public const string SourceInputsSha256 = "f9b0148355277ae08fa01e7940e8d1858b811700b83f794833c013c8b4e97ace";
    public const string ContractSha256 = "866bd4fc3251a1594b2d9d81978db5af218b331081bee693696002136ac7c9e3";
    public const string LockSha256 = "65d9f51c2cf115a9bc805f429b77e4f9df84d144a3adc585c17a5ae8355bedb1";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [ItemsSha256, SourceInputsSha256, ContractSha256, LockSha256];
    private static byte[] Read(string root, string relative, string hash, int? length = null)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, relative));
        if (length is {} size && bytes.Length != size || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("ordinary_item_bytes:" + relative);
        return bytes;
    }
    public static NpcTradeItemsDocument LoadDocument(string root)
    {
        var doc = JsonSerializer.Deserialize<NpcTradeItemsDocument>(Read(root, Base + "items.json", ItemsSha256),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("ordinary_item_document_missing");
        if (doc.SourceCommit != PokemonFactory.SourceCommit || doc.Profile != OrdinaryItemInventoryCatalog.Profile ||
            doc.PreviousItemCount != 129 || doc.ItemCount != 130 || doc.Additions.IsDefault || doc.Additions.Length != 1 ||
            doc.Additions[0].Id != OrdinaryItemInventoryCatalog.PpMaxId ||
            doc.AdditionsSemanticSha256 != OrdinaryItemInventoryCatalog.AdditionalItemsSourceSha256 ||
            doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty || doc.Icons.IsDefault || doc.Icons.Length != 1 ||
            doc.Icons[0].Item != OrdinaryItemInventoryCatalog.PpMaxId || doc.Icons[0].Width != 24 || doc.Icons[0].Height != 24)
            throw new InvalidDataException("ordinary_item_scope");
        return doc;
    }
    public static OrdinaryItemInventoryCatalog LoadShop(string root, EarlyShopCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var doc = LoadDocument(root);
        return OrdinaryItemInventoryCatalog.Authenticate(predecessor, doc.Additions);
    }
    public static JsonElement LoadSourceContract(string root) =>
        JsonSerializer.Deserialize<JsonElement>(Read(root, Base + "source-contract.json", ContractSha256));
    public static byte[] LoadIconRgba(string root)
    {
        var icon = LoadDocument(root).Icons[0];
        _ = Read(root, icon.PngPath, icon.PngSha256);
        return Read(root, icon.RgbaPath, icon.RgbaSha256, 24 * 24 * 4);
    }
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        using var locked = JsonDocument.Parse(Read(root, "scripts/ordinary-item-supplement.lock.json", LockSha256));
        using var inputs = JsonDocument.Parse(Read(root, Base + "source-input-hashes.json", SourceInputsSha256));
        if (inputs.RootElement.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.RootElement.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("ordinary_item_source_identity");
        foreach (var row in inputs.RootElement.GetProperty("inputs").EnumerateArray())
        {
            var data = Read(row.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root,
                row.GetProperty("localPath").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {data.Length}\0"), .. data])) != row.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("ordinary_item_source_blob");
        }
        foreach (var row in inputs.RootElement.GetProperty("dependencyInputs").EnumerateArray())
            _ = Read(dependencyRoot ?? root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        foreach (var row in locked.RootElement.GetProperty("files").EnumerateArray())
            _ = Read(root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        _ = Read(root, "scripts/import-ordinary-item-supplement.py", locked.RootElement.GetProperty("importerSha256").GetString()!);
        _ = LoadDocument(root); _ = LoadSourceContract(root); _ = LoadIconRgba(root);
    }
}
