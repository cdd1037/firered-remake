using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Original Lost Cave/Selphy inventory closure; acquisition remains owned by the existing current commands.</summary>
public static class PretLostCaveItems
{
    public const string Base = "content/generated/lost-cave-items/";
    public const string ItemsSha256 = "c0aaff2bcc856dc2686d26c29c35aa77c0258cbe521baf8b03256f57aca9125d";
    public const string ContractSha256 = "4e00a42cca9f3415dd0a582514d9a843303f457c978f6e59a5ae384da0041583";
    public const string SourceInputsSha256 = "7ccbb4772519d5728b4a26aa01a515208dab0f1d29365d83584195873fe6eff2";
    public const string LockSha256 = "d2fe915d7275c04ef5af978f1cff3299ddae678790896699def389c2f123c3d8";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [ItemsSha256, ContractSha256, SourceInputsSha256, LockSha256];
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static byte[] Read(string root, string relative, string hash, int? length = null)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("lost_cave_item_path");
        var bytes = File.ReadAllBytes(Path.Combine(root, relative));
        if (length is { } size && bytes.Length != size || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("lost_cave_item_bytes:" + relative);
        return bytes;
    }
    public static NpcTradeItemsDocument LoadItemsDocument(string root)
    {
        var doc = JsonSerializer.Deserialize<NpcTradeItemsDocument>(Read(root, Base + "items.json", ItemsSha256), Options)
            ?? throw new InvalidDataException("lost_cave_item_document_missing");
        if (doc.SourceCommit != PokemonFactory.SourceCommit || doc.Profile != OrdinaryItemInventoryCatalog.Profile ||
            doc.PreviousItemCount != 147 || doc.ItemCount != 151 || doc.Additions.IsDefault || doc.Additions.Length != 4 ||
            !doc.Additions.Select(i => i.Id).SequenceEqual(OrdinaryItemInventoryCatalog.LostCaveItems) ||
            doc.AdditionsSemanticSha256 != OrdinaryItemInventoryCatalog.LostCaveItemsSourceSha256 ||
            doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty || doc.Icons.IsDefault || doc.Icons.Length != 4 ||
            !doc.Icons.Select(i => i.Item).SequenceEqual(OrdinaryItemInventoryCatalog.LostCaveItems) ||
            doc.Icons.Any(i => i.Width != 24 || i.Height != 24))
            throw new InvalidDataException("lost_cave_item_scope");
        return doc;
    }
    public static OrdinaryItemInventoryCatalog LoadItems(string root, EarlyShopCatalog predecessor) =>
        OrdinaryItemInventoryCatalog.AppendLostCaveItems(predecessor, LoadItemsDocument(root).Additions);
    public static JsonElement LoadSourceContract(string root) =>
        JsonSerializer.Deserialize<JsonElement>(Read(root, Base + "source-contract.json", ContractSha256));
    public static byte[] LoadIconRgba(string root, ItemId item)
    {
        var icon = LoadItemsDocument(root).Icons.SingleOrDefault(i => i.Item == item)
            ?? throw new ArgumentException("lost_cave_item_icon_id");
        _ = Read(root, icon.PngPath, icon.PngSha256);
        return Read(root, icon.RgbaPath, icon.RgbaSha256, 24 * 24 * 4);
    }
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        using var locked = JsonDocument.Parse(Read(root, "scripts/lost-cave-items.lock.json", LockSha256));
        using var inputs = JsonDocument.Parse(Read(root, Base + "source-input-hashes.json", SourceInputsSha256));
        if (inputs.RootElement.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.RootElement.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("lost_cave_item_source_identity");
        foreach (var row in inputs.RootElement.GetProperty("inputs").EnumerateArray())
        {
            var bytes = Read(row.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root,
                row.GetProperty("localPath").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != row.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("lost_cave_item_source_blob");
        }
        foreach (var row in inputs.RootElement.GetProperty("dependencyInputs").EnumerateArray())
            _ = Read(dependencyRoot ?? root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        foreach (var row in locked.RootElement.GetProperty("files").EnumerateArray())
            _ = Read(root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        _ = Read(root, "scripts/import-lost-cave-items.py", locked.RootElement.GetProperty("importerSha256").GetString()!);
        _ = LoadItemsDocument(root); _ = LoadSourceContract(root);
        foreach (var item in OrdinaryItemInventoryCatalog.LostCaveItems) _ = LoadIconRgba(root, item);
    }
}
