using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Domain.Items;

/// <summary>Original 5F counters, sharing authenticated ordinary item definitions and transactions.</summary>
public sealed record DepartmentStoreCounterStock(string SourceCommit, string MapId,
    string ClerkScript, ImmutableArray<DepartmentStoreOrdinaryItem> Entries, OpeningItemSourceRef Source) : AuthenticatedShopStock
{
    public const string ExpectedMapId = "map.celadon-city-department-store-5f";
    public const string ExpectedSourcePath = "data/maps/CeladonCity_DepartmentStore_5F/scripts.inc";
    public const string XItemsId = "CeladonCity_DepartmentStore_5F_XItems";
    public const string VitaminsId = "CeladonCity_DepartmentStore_5F_Vitamins";
    private sealed record Counter(string Id, string Script, int Actor, int Y, string Movement, int MartLine,
        int FirstLine, int LastLine, ImmutableArray<DepartmentStoreOrdinaryItem> Items);
    private static DepartmentStoreOrdinaryItem Item(string symbol, int id, string name, int price) =>
        new(new("pret:" + symbol), id, name, price, "POCKET_ITEMS");
    private static readonly ImmutableArray<Counter> Counters = [
        new(XItemsId, "CeladonCity_DepartmentStore_5F_EventScript_ClerkXItems", 3, 7, "MOVEMENT_TYPE_FACE_DOWN", 22, 28, 39, [
            Item("ITEM_X_ATTACK", 75, "X ATTACK", 500), Item("ITEM_X_DEFEND", 76, "X DEFEND", 550),
            Item("ITEM_X_SPEED", 77, "X SPEED", 350), Item("ITEM_X_SPECIAL", 79, "X SPECIAL", 350),
            Item("ITEM_X_ACCURACY", 78, "X ACCURACY", 950), Item("ITEM_GUARD_SPEC", 73, "GUARD SPEC.", 700),
            Item("ITEM_DIRE_HIT", 74, "DIRE HIT", 650)]),
        new(VitaminsId, "CeladonCity_DepartmentStore_5F_EventScript_ClerkVitamins", 4, 6, "MOVEMENT_TYPE_FACE_RIGHT", 46, 52, 61, [
            Item("ITEM_HP_UP", 63, "HP UP", 9800), Item("ITEM_PROTEIN", 64, "PROTEIN", 9800),
            Item("ITEM_IRON", 65, "IRON", 9800), Item("ITEM_CALCIUM", 67, "CALCIUM", 9800),
            Item("ITEM_ZINC", 70, "ZINC", 9800), Item("ITEM_CARBOS", 66, "CARBOS", 9800)])
    ];
    public override string Id => Source.Symbol;
    public override ImmutableArray<ItemId> Items => Entries.Select(i => i.Id).ToImmutableArray();
    public static bool IsStockId(string id) => Counters.Any(c => c.Id == id);
    public static string? StockForClerk(string map, string script) => map == ExpectedMapId
        ? Counters.FirstOrDefault(c => c.Script == script)?.Id : null;
    public bool IsClerk(string map, string path, string script, int line) => map == MapId &&
        path == ExpectedSourcePath && script == ClerkScript && Counters.Any(c => c.Id == Id && c.MartLine == line);
    public bool IsSourceActor(FieldActorDefinition actor) => Counters.Any(c => c.Id == Id &&
        actor.SourceLocalId == c.Actor && actor.ScriptId == c.Script && actor.GraphicsId == "OBJ_EVENT_GFX_CLERK" &&
        actor.X == 1 && actor.Y == c.Y && actor.Elevation == 3 && actor.MovementType == c.Movement &&
        actor.HideFlag == "0" && !actor.IsClone && actor.TrainerType == "TRAINER_TYPE_NONE");

    public override void Validate(EarlyShopCatalog catalog)
    {
        EarlyShopInventoryRules.ValidateCatalog(catalog);
        var expected = Counters.SingleOrDefault(c => c.Id == Id);
        if (expected is null || SourceCommit != OpeningInventoryRules.SourceCommit ||
            MapId != ExpectedMapId ||
            ClerkScript != expected.Script || Source != new OpeningItemSourceRef(ExpectedSourcePath, expected.Id,
                expected.FirstLine, expected.LastLine) || Entries.IsDefault || !Entries.SequenceEqual(expected.Items))
            throw new NotSupportedException("department_counter_source_stock");
        foreach (var entry in Entries)
        {
            var item = EarlyShopInventoryRules.Find(catalog, entry.Id);
            if (item is null || item.SourceItemSymbol != entry.Id.Value[5..] || item.SourceItemId != entry.SourceItemId ||
                item.Name != entry.Name || item.Price != entry.Price || item.Pocket != entry.Pocket || item.Importance != 0)
                throw new NotSupportedException("department_counter_item_metadata");
        }
    }
}
