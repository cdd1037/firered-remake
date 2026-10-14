using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record GameCornerMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    int PreviousMachineCount, int MachineCount, ImmutableArray<StoneEvolutionMachineRows> Inherited, string InheritedSemanticSha256,
    ImmutableArray<PewterAdditionalMachine> Additions, string AdditionsSemanticSha256,
    ImmutableArray<DepartmentRooftopIconAlias> IconAliases, ImmutableArray<NpcTradeItemIcon> Icons);

/// <summary>Source-backed content only. The caller owns current composition, receipt generation and coin transactions.</summary>
public static class PretGameCornerPrizes
{
    public const string Base = "content/generated/game-corner-prizes/";
    public const string Profile = "firered-game-corner-prizes-v1";
    public const string ItemsSha256 = "5bd1eed67c18d170f6374fd6fe274ab16f13b11f681d6f3cbff1688932e32358";
    public const string MachinesSha256 = "c30c6f9ef42bef171aa4b618438c4646c32a99816ccd2893f4d12fcde118e2bf";
    public const string PrizesSha256 = "24b7846094b584fdccbfeaad1b053822a1823c34ea77b4fd50d3c3f54b2f28dc";
    public const string SourceInputsSha256 = "c5928a910cc205c043d14e0f42b918bb8520ea57acf683dcdfbdcc0e3538a623";
    public const string LockSha256 = "6d0fb6009e3c0631d6f0944fe00af178e26f6eea18ec3917a06d7f57dd71bad7";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [ItemsSha256, MachinesSha256, PrizesSha256, SourceInputsSha256, LockSha256];
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static byte[] Read(string root, string relative, string hash, int? length = null)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("game_corner_prize_path");
        var bytes = File.ReadAllBytes(Path.Combine(root, relative));
        if (length is { } size && bytes.Length != size || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("game_corner_prize_bytes:" + relative);
        return bytes;
    }
    private static T Deserialize<T>(byte[] bytes) => JsonSerializer.Deserialize<T>(bytes, Options) ??
        throw new InvalidDataException("game_corner_prize_document_missing");
    public static NpcTradeItemsDocument LoadItemsDocument(string root)
    {
        var doc = Deserialize<NpcTradeItemsDocument>(Read(root, Base + "items.json", ItemsSha256));
        if (doc.SourceCommit != PokemonFactory.SourceCommit || doc.Profile != Profile || doc.PreviousItemCount != 132 || doc.ItemCount != 137 ||
            doc.Additions.IsDefault || doc.Additions.Length != 5 ||
            !doc.Additions.Select(i => i.Id).SequenceEqual(OrdinaryItemInventoryCatalog.GameCornerPrizeItems) ||
            doc.AdditionsSemanticSha256 != OrdinaryItemInventoryCatalog.GameCornerPrizesSourceSha256 ||
            doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty || doc.Icons.IsDefault || doc.Icons.Length != 5 ||
            !doc.Icons.Select(i => i.Item).SequenceEqual(OrdinaryItemInventoryCatalog.GameCornerPrizeItems) ||
            doc.Icons.Any(i => i.Width != 24 || i.Height != 24))
            throw new InvalidDataException("game_corner_prize_items_scope");
        return doc;
    }
    public static GameCornerMachinesDocument LoadMachinesDocument(string root)
    {
        var doc = Deserialize<GameCornerMachinesDocument>(Read(root, Base + "machines.json", MachinesSha256));
        if (doc.SourceCommit != PokemonFactory.SourceCommit || doc.Profile != GameCornerMachineCatalog.Profile ||
            doc.PreviousSpeciesCount != 170 || doc.SpeciesCount != 174 || doc.PreviousMachineCount != 45 || doc.MachineCount != 50 ||
            doc.Inherited.IsDefault || doc.Inherited.Length != 45 || doc.InheritedSemanticSha256 != GameCornerMachineCatalog.InheritedSourceSha256 ||
            doc.Additions.IsDefault || doc.Additions.Length != 5 || doc.AdditionsSemanticSha256 != GameCornerMachineCatalog.AdditionsSourceSha256 ||
            doc.IconAliases.IsDefault || doc.IconAliases.Length != 4 ||
            !doc.IconAliases.Select(a => (a.Item.Value, a.ReusedItem.Value)).SequenceEqual(new[] {
                ("pret:ITEM_TM13", "pret:ITEM_TM14"), ("pret:ITEM_TM23", "pret:ITEM_TM47"),
                ("pret:ITEM_TM24", "pret:ITEM_TM25"), ("pret:ITEM_TM35", "pret:ITEM_TM38") }) ||
            doc.Icons.IsDefault || doc.Icons.Length != 1 || doc.Icons[0].Item != new ItemId("pret:ITEM_TM30") ||
            doc.Icons[0].Width != 24 || doc.Icons[0].Height != 24)
            throw new InvalidDataException("game_corner_prize_machines_scope");
        return doc;
    }
    public static JsonElement LoadSourceContract(string root)
    {
        var doc = Deserialize<JsonElement>(Read(root, Base + "prizes.json", PrizesSha256));
        if (doc.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            doc.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree || doc.GetProperty("profile").GetString() != Profile ||
            doc.GetProperty("mapId").GetString() != GameCornerPrizeCatalog.Map || doc.GetProperty("counters").GetArrayLength() != 3 ||
            !doc.GetProperty("itemCapacityCheckedBeforeDeduction").GetBoolean() || !doc.GetProperty("monDeductedOnlyAfterReceipt").GetBoolean())
            throw new InvalidDataException("game_corner_prize_contract");
        return doc;
    }
    public static OrdinaryItemInventoryCatalog LoadItems(string root, EarlyShopCatalog predecessor) =>
        OrdinaryItemInventoryCatalog.AppendGameCornerPrizes(predecessor, LoadItemsDocument(root).Additions);
    public static GameCornerMachineCatalog LoadMachines(string root, PewterMachineCatalog predecessor, GameCornerOwnedPokemonCatalog owned)
    {
        var doc = LoadMachinesDocument(root); var cells = doc.Inherited.ToDictionary(r => r.Item);
        return GameCornerMachineCatalog.Authenticate(predecessor, owned, predecessor with {
            Profile = GameCornerMachineCatalog.Profile,
            Compatibility = predecessor.Compatibility.AddRange(cells[predecessor.Tm39.Item.Id].Compatibility),
            AdditionalMachines = predecessor.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(cells[m.Machine.Item.Id].Compatibility)
            }).Concat(doc.Additions).ToImmutableArray()
        });
    }
    public static GameCornerPrizeCatalog LoadCatalog(string root, PokemonCatalog owned, EarlyShopCatalog items, PewterMachineCatalog machines) =>
        GameCornerPrizeCatalog.Authenticate(owned, items, machines,
            LoadSourceContract(root).GetProperty("counters").Deserialize<ImmutableArray<GameCornerPrizeCounter>>(Options));
    public static byte[] LoadIconRgba(string root, ItemId item)
    {
        var icon = LoadItemsDocument(root).Icons.Concat(LoadMachinesDocument(root).Icons).SingleOrDefault(i => i.Item == item)
            ?? throw new ArgumentException("game_corner_prize_icon_id");
        _ = Read(root, icon.PngPath, icon.PngSha256);
        return Read(root, icon.RgbaPath, icon.RgbaSha256, 24 * 24 * 4);
    }
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        using var locked = JsonDocument.Parse(Read(root, "scripts/game-corner-prizes.lock.json", LockSha256));
        using var inputs = JsonDocument.Parse(Read(root, Base + "source-input-hashes.json", SourceInputsSha256));
        if (inputs.RootElement.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.RootElement.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("game_corner_prize_source_identity");
        foreach (var row in inputs.RootElement.GetProperty("inputs").EnumerateArray())
        {
            var bytes = Read(row.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root,
                row.GetProperty("localPath").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != row.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("game_corner_prize_source_blob");
        }
        foreach (var row in inputs.RootElement.GetProperty("dependencyInputs").EnumerateArray())
            _ = Read(dependencyRoot ?? root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        foreach (var row in locked.RootElement.GetProperty("files").EnumerateArray())
            _ = Read(root, row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        _ = Read(root, "scripts/import-game-corner-prizes.py", locked.RootElement.GetProperty("importerSha256").GetString()!);
        _ = LoadItemsDocument(root); _ = LoadMachinesDocument(root); _ = LoadSourceContract(root);
        foreach (var item in OrdinaryItemInventoryCatalog.GameCornerPrizeItems.Add(new("pret:ITEM_TM30"))) _ = LoadIconRgba(root, item);
    }
}
