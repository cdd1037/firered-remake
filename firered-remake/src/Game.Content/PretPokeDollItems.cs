using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Original Poke Doll definition and pixels; complete 4F shelf is evidence, not a shortened live shop.</summary>
public static class PretPokeDollItems
{
    public const string Base = "content/generated/poke-doll-items/";
    public const string ItemsSha256 = "571ea9b9b7a628cb539a27e15e0a7585fb5990b62fec5c9b06d0801cbe8bbed6";
    public const string SourceInputsSha256 = "cc497de2e6542684b540a0c44707e457ed103bd62f6a38cffc81c7942e6f243e";
    public const string ContractSha256 = "400f6d4213604801f0088c5c8e892c8135837b54293ba8b9b2ae911e448a8b0b";
    public const string LockSha256 = "caad213fc4614ac9b0c5371752eebd496870b35679ed2846e9a20c0d7e56f12a";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [ItemsSha256, SourceInputsSha256, ContractSha256, LockSha256];
    private static byte[] Read(string root, string relative, string hash, int? length = null)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, relative));
        if (length is {} size && bytes.Length != size || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("poke_doll_item_bytes:" + relative);
        return bytes;
    }
    public static NpcTradeItemsDocument LoadDocument(string root)
    {
        var doc = JsonSerializer.Deserialize<NpcTradeItemsDocument>(Read(root, Base + "items.json", ItemsSha256),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("poke_doll_item_document_missing");
        if (doc.SourceCommit != PokemonFactory.SourceCommit || doc.Profile != PokeDollInventoryCatalog.Profile ||
            doc.PreviousItemCount != 128 || doc.ItemCount != 129 || doc.Additions.IsDefault || doc.Additions.Length != 1 ||
            doc.Additions[0].Id != PokeDollInventoryCatalog.PokeDollId ||
            doc.AdditionsSemanticSha256 != PokeDollInventoryCatalog.AdditionalItemsSourceSha256 ||
            doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty || doc.Icons.IsDefault || doc.Icons.Length != 1 ||
            doc.Icons[0].Item != PokeDollInventoryCatalog.PokeDollId || doc.Icons[0].Width != 24 || doc.Icons[0].Height != 24)
            throw new InvalidDataException("poke_doll_item_scope");
        return doc;
    }
    public static PokeDollInventoryCatalog LoadShop(string root, SeafoamShopCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var doc = LoadDocument(root);
        return PokeDollInventoryCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = PokeDollInventoryCatalog.Profile, Items = predecessor.Catalog.Items.AddRange(doc.Additions) });
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
        using var locked = JsonDocument.Parse(Read(root, "scripts/poke-doll-items.lock.json", LockSha256));
        using var inputs = JsonDocument.Parse(Read(root, Base + "source-input-hashes.json", SourceInputsSha256));
        if (inputs.RootElement.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.RootElement.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("poke_doll_item_source_identity");
        foreach (var row in inputs.RootElement.GetProperty("inputs").EnumerateArray())
        {
            var data = Read(row.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root,
                row.GetProperty("localPath").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {data.Length}\0"), .. data])) != row.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("poke_doll_item_source_blob");
        }
        foreach (var row in inputs.RootElement.GetProperty("dependencyInputs").EnumerateArray())
            _ = Read(dependencyRoot ?? root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        foreach (var row in locked.RootElement.GetProperty("files").EnumerateArray())
            _ = Read(root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        _ = Read(root, "scripts/import-poke-doll-items.py", locked.RootElement.GetProperty("importerSha256").GetString()!);
        _ = LoadDocument(root); _ = LoadSourceContract(root); _ = LoadIconRgba(root);
    }
}
