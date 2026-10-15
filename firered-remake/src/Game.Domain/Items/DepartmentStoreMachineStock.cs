using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>The original six-TM shelf. It adds one source item to the existing owner, without a regional catalog chain.</summary>
public sealed class DepartmentStoreMachineStock : AuthenticatedMachineShopStock
{
    public const string ExpectedMapId = "map.celadon-city-department-store-2f";
    public const string ExpectedStockId = "CeladonCity_DepartmentStore_2F_TMs";
    public const string ExpectedClerkScript = "CeladonCity_DepartmentStore_2F_EventScript_ClerkTMs";
    public const string ExpectedSourcePath = "data/maps/CeladonCity_DepartmentStore_2F/scripts.inc";
    public static ItemId Tm15Id { get; } = new("pret:ITEM_TM15");
    public static OpeningItemSourceRef Source { get; } = new(ExpectedSourcePath, ExpectedStockId, 54, 63);
    private static readonly (string Item, int Id, int Price, string Move, int MoveId, int Pp)[] Expected = [
        ("TM05", 293, 1000, "ROAR", 46, 20), ("TM15", 303, 7500, "HYPER_BEAM", 63, 5),
        ("TM28", 316, 2000, "DIG", 91, 10), ("TM31", 319, 3000, "BRICK_BREAK", 280, 15),
        ("TM43", 331, 3000, "SECRET_POWER", 290, 20), ("TM45", 333, 3000, "ATTRACT", 213, 15)
    ];
    public override string Id => ExpectedStockId;
    public override ImmutableArray<ItemId> Items { get; } = Expected.Select(row => new ItemId("pret:ITEM_" + row.Item)).ToImmutableArray();
    public override MachineCatalog Machines { get; }
    public DepartmentStoreMachineStock(MachineCatalog machines)
    { Machines = machines ?? throw new ArgumentNullException(nameof(machines)); Validate(); }

    public bool IsClerk(string mapId, string sourcePath, string script, string stockId) =>
        mapId == ExpectedMapId && sourcePath == ExpectedSourcePath && script == ExpectedClerkScript && stockId == Id;

    public override void Validate()
    {
        MachineRules.ValidateCatalog(Machines);
        foreach (var row in Expected)
        {
            var value = MachineRules.Definition(Machines, new("pret:ITEM_" + row.Item));
            var item = value?.Machine.Item;
            if (item is null || item.SourceItemSymbol != "ITEM_" + row.Item || item.SourceItemId != row.Id ||
                item.Name != row.Item || item.Price != row.Price || item.Pocket != "POCKET_TM_CASE" ||
                item.Importance != 0 || !item.CanSell || value!.Move != new MoveId("pret:MOVE_" + row.Move) ||
                value.SourceMoveId != row.MoveId || value.MovePp != row.Pp)
                throw new NotSupportedException("department_machine_stock_definition");
        }
    }
}
