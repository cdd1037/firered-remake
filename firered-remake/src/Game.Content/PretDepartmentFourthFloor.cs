using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Original 4F Retro Mail definition and full shelf provenance; Mail composition and stone effects stay separate.</summary>
public static class PretDepartmentFourthFloor
{
    public const string Base = "content/generated/department-fourth-floor/";
    public const string ItemsSha256 = "ea45e7040951ccb8e92444093da5b303d4da194cdf365a60f597cf5368de124c";
    public const string SourceInputsSha256 = "e5793e214e54ff546174730caa51e25ed35a66f3427b1411a1cbe26edd64de12";
    public const string ContractSha256 = "b8e4529d0dbe3f73f65a8387e60151bf8355bf91266e3e55be23a57211ac523a";
    public const string LockSha256 = "c5b3ace0c1b5883b351bf54b797de071d4446f343f087e17f33ed1bf000f9e6e";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [ItemsSha256, SourceInputsSha256, ContractSha256, LockSha256];
    private static byte[] Read(string root, string relative, string hash, int? length = null)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, relative));
        if (length is {} size && bytes.Length != size || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("department_fourth_floor_bytes:" + relative);
        return bytes;
    }
    public static NpcTradeItemsDocument LoadDocument(string root)
    {
        var doc = JsonSerializer.Deserialize<NpcTradeItemsDocument>(Read(root, Base + "items.json", ItemsSha256),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("department_fourth_floor_document_missing");
        if (doc.SourceCommit != PokemonFactory.SourceCommit || doc.Profile != "firered-department-fourth-floor-v1" ||
            doc.PreviousItemCount != 130 || doc.ItemCount != 131 || doc.Additions.IsDefault || doc.Additions.Length != 1 ||
            doc.Additions[0].Id != OrdinaryItemInventoryCatalog.RetroMailId ||
            doc.AdditionsSemanticSha256 != OrdinaryItemInventoryCatalog.RetroMailSourceSha256 ||
            doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty || doc.Icons.IsDefault || doc.Icons.Length != 1 ||
            doc.Icons[0].Item != OrdinaryItemInventoryCatalog.RetroMailId || doc.Icons[0].Width != 24 || doc.Icons[0].Height != 24)
            throw new InvalidDataException("department_fourth_floor_scope");
        return doc;
    }
    public static OrdinaryItemInventoryCatalog LoadShop(string root, EarlyShopCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var doc = LoadDocument(root);
        return OrdinaryItemInventoryCatalog.AppendRetroMail(predecessor, doc.Additions);
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
        using var locked = JsonDocument.Parse(Read(root, "scripts/department-fourth-floor.lock.json", LockSha256));
        using var inputs = JsonDocument.Parse(Read(root, Base + "source-input-hashes.json", SourceInputsSha256));
        if (inputs.RootElement.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.RootElement.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("department_fourth_floor_source_identity");
        foreach (var row in inputs.RootElement.GetProperty("inputs").EnumerateArray())
        {
            var data = Read(row.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root,
                row.GetProperty("localPath").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {data.Length}\0"), .. data])) != row.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("department_fourth_floor_source_blob");
        }
        foreach (var row in inputs.RootElement.GetProperty("dependencyInputs").EnumerateArray())
            _ = Read(dependencyRoot ?? root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        foreach (var row in locked.RootElement.GetProperty("files").EnumerateArray())
            _ = Read(root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        _ = Read(root, "scripts/import-department-fourth-floor.py", locked.RootElement.GetProperty("importerSha256").GetString()!);
        _ = LoadDocument(root); _ = LoadSourceContract(root); _ = LoadIconRgba(root);
    }
}
