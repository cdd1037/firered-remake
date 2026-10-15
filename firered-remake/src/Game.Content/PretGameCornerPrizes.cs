using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record GameCornerMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount, int PreviousMachineCount,
    int MachineCount, ImmutableArray<StoneEvolutionMachineRows> Inherited,
    ImmutableArray<PewterAdditionalMachine> Additions, ImmutableArray<DepartmentRooftopIconAlias> IconAliases,
    ImmutableArray<NpcTradeItemIcon> Icons);

/// <summary>Source-backed content only. The caller owns current composition, receipt generation and coin transactions.</summary>
public static class PretGameCornerPrizes
{
    public const string Base = "content/generated/game-corner-prizes/";
    public const string Profile = "firered-game-corner-prizes-v1";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static byte[] Read(string root,string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("game_corner_prize_path");
        var bytes = File.ReadAllBytes(Path.Combine(root, relative));
        return bytes;
    }
    private static T Deserialize<T>(byte[] bytes) => JsonSerializer.Deserialize<T>(bytes, Options) ??
        throw new InvalidDataException("game_corner_prize_document_missing");
    public static NpcTradeItemsDocument LoadItemsDocument(string root)
    {
        var doc = Deserialize<NpcTradeItemsDocument>(Read(root,Base + "items.json"));
        if (doc.Additions.IsDefault || doc.Additions.Length != 5 ||
            !doc.Additions.Select(i => i.Id).SequenceEqual(OrdinaryItemInventoryCatalog.GameCornerPrizeItems) ||
            doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty || doc.Icons.IsDefault || doc.Icons.Length != 5 ||
            !doc.Icons.Select(i => i.Item).SequenceEqual(OrdinaryItemInventoryCatalog.GameCornerPrizeItems) ||
            doc.Icons.Any(i => i.Width != 24 || i.Height != 24))
            throw new InvalidDataException("game_corner_prize_items_scope");
        return doc;
    }
    public static GameCornerMachinesDocument LoadMachinesDocument(string root)
    {
        var doc = Deserialize<GameCornerMachinesDocument>(Read(root,Base + "machines.json"));
        if (            doc.Inherited.IsDefault || doc.Inherited.Length != 45 ||
            doc.Additions.IsDefault || doc.Additions.Length != 5 ||
            doc.IconAliases.IsDefault || doc.IconAliases.Length != 4 ||
            !doc.IconAliases.Select(a => (a.Item.Value, a.ReusedItem.Value)).SequenceEqual(new[] {
                ("pret:ITEM_TM13", "pret:ITEM_TM14"),("pret:ITEM_TM23", "pret:ITEM_TM47"),("pret:ITEM_TM24", "pret:ITEM_TM25"),("pret:ITEM_TM35", "pret:ITEM_TM38") }) ||
            doc.Icons.IsDefault || doc.Icons.Length != 1 || doc.Icons[0].Item != new ItemId("pret:ITEM_TM30") ||
            doc.Icons[0].Width != 24 || doc.Icons[0].Height != 24)
            throw new InvalidDataException("game_corner_prize_machines_scope");
        return doc;
    }
    public static JsonElement LoadSourceContract(string root)
    {
        var doc = Deserialize<JsonElement>(Read(root,Base + "prizes.json"));
        if (            doc.GetProperty("mapId").GetString() != GameCornerPrizeCatalog.Map || doc.GetProperty("counters").GetArrayLength() != 3 ||
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
            Profile = GameCornerMachineCatalog.Profile,Compatibility = predecessor.Compatibility.AddRange(cells[predecessor.Tm39.Item.Id].Compatibility),AdditionalMachines = predecessor.AdditionalMachines!.Value.Select(m => m with {
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
        return PngRgba8.Decode(Read(root,icon.TexturePath),icon.Width,icon.Height);
    }
}
