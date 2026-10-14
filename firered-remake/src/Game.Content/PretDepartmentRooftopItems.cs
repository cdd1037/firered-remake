using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record DepartmentRooftopIconAlias(ItemId Item, ItemId ReusedItem, OpeningItemSourceRef Source, OpeningItemSourceRef ReusedSource);
public sealed record DepartmentRooftopMachinesDocument(string SourceCommit, string Profile, int PreviousMachineCount,
    int MachineCount, int SpeciesCount, ImmutableArray<PewterAdditionalMachine> Additions,
    string AdditionsSemanticSha256, ImmutableArray<DepartmentRooftopIconAlias> IconAliases);

/// <summary>One missing drink and three reward machines; all existing items, owners and icons remain shared.</summary>
public static class PretDepartmentRooftopItems
{
    public const string Base = "content/generated/department-rooftop-items/";
    public const string Profile = "firered-department-rooftop-items-v1";
    public const string ItemsSha256 = "6217d5edc9d495ff2c931e27d9e9594565f19e2e8f262ca1007b78dfacfc21d4";
    public const string MachinesSha256 = "9bbf60e6f2d74f19f7fd9492310e17619c353854f7cbbdb2f411aae8b5542b8a";
    public const string ContractSha256 = "4cf11a2972144e5a992ae2ca0c617d041ff2d46168c024adb39672d27f940263";
    public const string SourceInputsSha256 = "e8f94e8d6201debcc03855f8b7ffc8df6bb1ab5df842d1a05ac4a9b37f63da67";
    public const string LockSha256 = "3eb255668f374f344f11d9c200fb3b3d04d0efcad8bea5ca2fb71207d34e348e";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [ItemsSha256, MachinesSha256, ContractSha256, SourceInputsSha256, LockSha256];
    private static byte[] Read(string root, string relative, string hash, int? length = null)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("department_rooftop_source_path");
        var bytes = File.ReadAllBytes(Path.Combine(root, relative));
        if (length is {} size && bytes.Length != size || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("department_rooftop_bytes:" + relative);
        return bytes;
    }
    private static T Deserialize<T>(byte[] data) => JsonSerializer.Deserialize<T>(data,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) ??
        throw new InvalidDataException("department_rooftop_document_missing");
    public static NpcTradeItemsDocument LoadDocument(string root)
    {
        var doc = Deserialize<NpcTradeItemsDocument>(Read(root, Base + "items.json", ItemsSha256));
        if (doc.SourceCommit != PokemonFactory.SourceCommit || doc.Profile != Profile ||
            doc.PreviousItemCount != 131 || doc.ItemCount != 132 || doc.Additions.IsDefault || doc.Additions.Length != 1 ||
            doc.Additions[0].Id != OrdinaryItemInventoryCatalog.LemonadeId ||
            doc.AdditionsSemanticSha256 != OrdinaryItemInventoryCatalog.LemonadeSourceSha256 ||
            doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty || doc.Icons.IsDefault || doc.Icons.Length != 1 ||
            doc.Icons[0].Item != OrdinaryItemInventoryCatalog.LemonadeId || doc.Icons[0].Width != 24 || doc.Icons[0].Height != 24)
            throw new InvalidDataException("department_rooftop_item_scope");
        return doc;
    }
    public static DepartmentRooftopMachinesDocument LoadMachinesDocument(string root)
    {
        var doc = Deserialize<DepartmentRooftopMachinesDocument>(Read(root, Base + "machines.json", MachinesSha256));
        if (doc.SourceCommit != PokemonFactory.SourceCommit || doc.Profile != Profile || doc.PreviousMachineCount != 42 ||
            doc.MachineCount != 45 || doc.SpeciesCount != 170 || doc.Additions.IsDefault || doc.Additions.Length != 3 ||
            doc.AdditionsSemanticSha256 != DepartmentStoreMachineStock.RooftopRewardsSourceSha256 ||
            doc.IconAliases.IsDefault || doc.IconAliases.Length != 3 ||
            !doc.IconAliases.Select(a => (a.Item.Value, a.ReusedItem.Value)).SequenceEqual(new[] {
                ("pret:ITEM_TM16", "pret:ITEM_TM04"), ("pret:ITEM_TM20", "pret:ITEM_TM05"), ("pret:ITEM_TM33", "pret:ITEM_TM04") }))
            throw new InvalidDataException("department_rooftop_machine_scope");
        return doc;
    }
    public static OrdinaryItemInventoryCatalog LoadShop(string root, EarlyShopCatalog predecessor) =>
        OrdinaryItemInventoryCatalog.AppendLemonade(predecessor, LoadDocument(root).Additions);
    public static DepartmentStoreMachineStock LoadMachines(string root, PewterMachineCatalog predecessor) =>
        DepartmentStoreMachineStock.AppendRooftopRewards(predecessor, LoadMachinesDocument(root).Additions);
    public static JsonElement LoadSourceContract(string root) =>
        Deserialize<JsonElement>(Read(root, Base + "source-contract.json", ContractSha256));
    public static byte[] LoadIconRgba(string root)
    {
        var icon = LoadDocument(root).Icons[0];
        _ = Read(root, icon.PngPath, icon.PngSha256);
        return Read(root, icon.RgbaPath, icon.RgbaSha256, 24 * 24 * 4);
    }
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        using var locked = JsonDocument.Parse(Read(root, "scripts/department-rooftop-items.lock.json", LockSha256));
        using var inputs = JsonDocument.Parse(Read(root, Base + "source-input-hashes.json", SourceInputsSha256));
        if (inputs.RootElement.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.RootElement.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("department_rooftop_source_identity");
        foreach (var row in inputs.RootElement.GetProperty("inputs").EnumerateArray())
        {
            var data = Read(row.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root,
                row.GetProperty("localPath").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {data.Length}\0"), .. data])) != row.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("department_rooftop_source_blob");
        }
        foreach (var row in inputs.RootElement.GetProperty("dependencyInputs").EnumerateArray())
            _ = Read(dependencyRoot ?? root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        foreach (var row in locked.RootElement.GetProperty("files").EnumerateArray())
            _ = Read(root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        _ = Read(root, "scripts/import-department-rooftop-items.py", locked.RootElement.GetProperty("importerSha256").GetString()!);
        _ = LoadDocument(root); _ = LoadMachinesDocument(root); _ = LoadSourceContract(root); _ = LoadIconRgba(root);
    }
}
