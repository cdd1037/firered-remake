using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record GameCornerMachineRows(ItemId Item, ImmutableArray<MachineCompatibility> Compatibility);
public sealed record GameCornerMachinesDocument(int PreviousSpeciesCount, int SpeciesCount, int PreviousMachineCount,
    int MachineCount, ImmutableArray<GameCornerMachineRows> Inherited,
    ImmutableArray<MachineDefinition> Additions, ImmutableArray<DepartmentRooftopIconAlias> IconAliases,
    ImmutableArray<NpcTradeItemIcon> Icons);

/// <summary>Source-backed content only. The caller owns current composition, receipt generation and coin transactions.</summary>
public static class PretGameCornerPrizes
{
    public const string Base = "content/generated/data/services/game-corner-prizes/";
    public const string Profile = "firered-game-corner-prizes-v1";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static byte[] Read(string root,string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("game_corner_prize_path");
        var bytes = File.ReadAllBytes(Path.Combine(root, relative));
        return bytes;
    }
    private static T Deserialize<T>(string root, byte[] bytes, SharedMachineCatalog? shared = null) => JsonSerializer.Deserialize<T>(bytes, (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions(Options)) ??
        throw new InvalidDataException("game_corner_prize_document_missing");
    public static NpcTradeItemsDocument LoadItemsDocument(string root, SharedMachineCatalog? shared = null)
    {
        var doc = Deserialize<NpcTradeItemsDocument>(root, Read(root,Base + "items.json"), shared);
        if (doc.Additions.IsDefault || doc.Additions.Length != 5 ||
            !doc.Additions.Select(i => i.Id).SequenceEqual(ItemIds.GameCornerPrizeItems) ||
            doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty || doc.Icons.IsDefault || doc.Icons.Length != 5 ||
            !doc.Icons.Select(i => i.Item).SequenceEqual(ItemIds.GameCornerPrizeItems) ||
            doc.Icons.Any(i => i.Width != 24 || i.Height != 24))
            throw new InvalidDataException("game_corner_prize_items_scope");
        return doc;
    }
    public static GameCornerMachinesDocument LoadMachinesDocument(string root, SharedMachineCatalog? shared = null)
    {
        var doc = Deserialize<GameCornerMachinesDocument>(root, Read(root,Base + "machines.json"), shared);
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
    public static JsonElement LoadSourceContract(string root, SharedMachineCatalog? shared = null)
    {
        var doc = Deserialize<JsonElement>(root, Read(root,Base + "prizes.json"), shared);
        if (            doc.GetProperty("mapId").GetString() != GameCornerPrizeCatalog.Map || doc.GetProperty("counters").GetArrayLength() != 3 ||
            !doc.GetProperty("itemCapacityCheckedBeforeDeduction").GetBoolean() || !doc.GetProperty("monDeductedOnlyAfterReceipt").GetBoolean())
            throw new InvalidDataException("game_corner_prize_contract");
        return doc;
    }

    public static GameCornerPrizeCatalog LoadCatalog(string root, GamePokemonCatalog owned, EarlyShopCatalog items, MachineCatalog machines,
        SharedMachineCatalog? shared = null, IReadOnlyDictionary<string, string>? sourceTexts = null)
    {
        var rows = LoadSourceContract(root, shared).GetProperty("counters");
        var counters = rows.Deserialize<ImmutableArray<GameCornerPrizeCounter>>(Options);
        sourceTexts ??= PretBattleText.Load(root);
        counters = counters.Select((counter, index) =>
        {
            var symbols = rows[index].GetProperty("menuTextSymbols").EnumerateArray().Select(row => row.GetString()!).ToArray();
            if (counter.Prizes.IsDefault || symbols.Length != counter.Prizes.Length + 1) throw new InvalidDataException("game_corner_prize_menu_texts");
            return counter with { MenuLabels = symbols.Select(symbol => sourceTexts[symbol]).ToImmutableArray() };
        }).ToImmutableArray();
        return GameCornerPrizeCatalog.Authenticate(owned, items, machines, counters);
    }
}
