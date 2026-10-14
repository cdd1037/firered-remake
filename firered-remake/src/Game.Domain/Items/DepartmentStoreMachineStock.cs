using System.Collections.Immutable;
using System.Runtime.CompilerServices;
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
    public const string Tm15SourceSha256 = "94fd64f9d5f13c89be211e690e1d789aab6ce04ae2c25f4caec3c32a35c45a06";
    public const string RooftopRewardsSourceSha256 = "f30cb3edf4d701eb5abac98ede000f71ec9a5eb02a08c465fb0ba4b24e7b27d9";
    public static ItemId Tm15Id { get; } = new("pret:ITEM_TM15");
    public static OpeningItemSourceRef Source { get; } = new(ExpectedSourcePath, ExpectedStockId, 54, 63,
        "3c3838130444cd6c1bbae4b21cdf7869d5d0fa585c8a5ded6343ba98b3fbf17e");
    private static readonly (string Item, int Id, int Price, string Move, int MoveId, int Pp)[] Expected = [
        ("TM05", 293, 1000, "ROAR", 46, 20), ("TM15", 303, 7500, "HYPER_BEAM", 63, 5),
        ("TM28", 316, 2000, "DIG", 91, 10), ("TM31", 319, 3000, "BRICK_BREAK", 280, 15),
        ("TM43", 331, 3000, "SECRET_POWER", 290, 20), ("TM45", 333, 3000, "ATTRACT", 213, 15)
    ];
    private static readonly ConditionalWeakTable<PewterMachineCatalog, DepartmentStoreMachineStock> Proofs = new();
    public static DepartmentStoreMachineStock? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static PewterMachineCatalog BaseCatalog(PewterMachineCatalog catalog) => For(catalog)?.OriginalMachines ?? catalog;
    public override string Id => ExpectedStockId;
    public override ImmutableArray<ItemId> Items { get; } = Expected.Select(row => new ItemId("pret:ITEM_" + row.Item)).ToImmutableArray();
    public override PewterMachineCatalog Machines { get; }
    public PewterMachineCatalog OriginalMachines { get; }
    private PewterMachineCatalog? RooftopPredecessor { get; init; }
    public static PewterMachineCatalog RooftopBaseCatalog(PewterMachineCatalog catalog) => For(catalog)?.RooftopPredecessor ?? catalog;
    public static bool HasRooftopRewards(PewterMachineCatalog? catalog) => For(catalog)?.RooftopPredecessor is not null;
    private readonly OwnedBattlePokemonCatalog _owner;
    private DepartmentStoreMachineStock(PewterMachineCatalog original, PewterMachineCatalog machines, OwnedBattlePokemonCatalog owner)
    { OriginalMachines = original; Machines = machines; _owner = owner; }

    public static DepartmentStoreMachineStock Authenticate(PewterMachineCatalog original, PewterAdditionalMachine tm15)
    {
        ArgumentNullException.ThrowIfNull(original); ArgumentNullException.ThrowIfNull(tm15);
        PewterMachineRules.ValidateCatalog(original);
        var owner = OwnedMachineCatalogBinding.For(original) ?? throw new ArgumentException("machine_stock_current_owner");
        if (CampaignSourceFingerprint.Compute(tm15) != Tm15SourceSha256 ||
            tm15.Compatibility.Length != owner.Catalog.Species.Count ||
            !tm15.Compatibility.Select(row => row.Species).ToHashSet().SetEquals(owner.Catalog.Species.Keys) ||
            tm15.Compatibility.Any(row => owner.Catalog.Species[row.Species].SourceSpeciesId != row.SourceSpeciesId) ||
            !PokemonMoveRules.IsLearningSupported(owner.Catalog, tm15.Move))
            throw new ArgumentException("department_machine_tm15_source");
        if (For(original) is { } admitted) { admitted.Validate(); return admitted; }
        // Compatibility and source move execution already belong to this exact current Pokemon owner.
        // Inherited machine definitions and all inventory limits retain their original references/values.
        var existing = PewterMachineRules.Definition(original, Tm15Id);
        if (existing is not null && CampaignSourceFingerprint.Compute(existing) != Tm15SourceSha256)
            throw new ArgumentException("department_machine_existing_tm15");
        var machines = existing is not null ? original : original with {
            AdditionalMachines = (original.AdditionalMachines ?? []).Add(tm15)
        };
        if (!ReferenceEquals(machines, original)) OwnedMachineCatalogBinding.Register(machines, owner);
        var stock = new DepartmentStoreMachineStock(original, machines, owner);
        stock.Validate(); Proofs.Add(machines, stock); return stock;
    }

    // Only the sealed seven-species successor may reuse this exact source shelf.
    internal static void BindStoneEvolutionSuccessor(StoneEvolutionMachineCatalog successor)
    {
        if (!ReferenceEquals(StoneEvolutionMachineCatalog.For(successor.Catalog), successor) ||
            !ReferenceEquals(For(successor.Predecessor.Machines), successor.Predecessor))
            throw new ArgumentException("department_machine_successor_proof");
        var stock = new DepartmentStoreMachineStock(successor.Predecessor.OriginalMachines,
            successor.Catalog, successor.Pokemon.SharedBattle);
        stock.Validate(); Proofs.Add(stock.Machines, stock);
    }

    /// <summary>Add the three original rooftop rewards to the exact current 170-species owner.</summary>
    public static DepartmentStoreMachineStock AppendRooftopRewards(PewterMachineCatalog original,
        ImmutableArray<PewterAdditionalMachine> additions)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (For(original) is not { } before || before.RooftopPredecessor is not null ||
            StoneEvolutionMachineCatalog.For(original) is not { } stone ||
            !ReferenceEquals(OwnedMachineCatalogBinding.For(original), stone.Pokemon.SharedBattle) ||
            additions.IsDefault || additions.Length != 3 || additions.Any(m => m is null) ||
            !additions.Select(m => m.Machine.Item.Id.Value).SequenceEqual(new[] { "pret:ITEM_TM16", "pret:ITEM_TM20", "pret:ITEM_TM33" }) ||
            CampaignSourceFingerprint.Compute(additions.ToArray()) != RooftopRewardsSourceSha256)
            throw new ArgumentException("department_rooftop_machine_source");
        before.Validate();
        foreach (var machine in additions)
            if (PewterMachineRules.Definition(original, machine.Machine.Item.Id) is not null ||
                machine.Compatibility.Length != before._owner.Catalog.Species.Count ||
                !machine.Compatibility.Select(c => c.Species).ToHashSet().SetEquals(before._owner.Catalog.Species.Keys) ||
                machine.Compatibility.Any(c => before._owner.Catalog.Species[c.Species].SourceSpeciesId != c.SourceSpeciesId) ||
                !PokemonMoveRules.IsLearningSupported(before._owner.Catalog, machine.Move))
                throw new ArgumentException("department_rooftop_machine_owner");
        var machines = original with { AdditionalMachines = original.AdditionalMachines!.Value.AddRange(additions) };
        OwnedMachineCatalogBinding.Register(machines, before._owner);
        var stock = new DepartmentStoreMachineStock(before.OriginalMachines, machines, before._owner)
        { RooftopPredecessor = original };
        stock.Validate(); Proofs.Add(machines, stock); return stock;
    }

    // The sealed successor retains both the original six-item shelf and its rooftop predecessor proof.
    internal static void BindGameCornerSuccessor(GameCornerMachineCatalog successor)
    {
        if (!ReferenceEquals(GameCornerMachineCatalog.For(successor.Catalog), successor) ||
            !ReferenceEquals(For(successor.Predecessor.Machines), successor.Predecessor) ||
            successor.Predecessor.RooftopPredecessor is null)
            throw new ArgumentException("department_game_corner_successor_proof");
        var stock = new DepartmentStoreMachineStock(successor.Predecessor.OriginalMachines,
            successor.Catalog, successor.Pokemon.SharedBattle)
        { RooftopPredecessor = successor.Predecessor.RooftopPredecessor };
        stock.Validate(); Proofs.Add(stock.Machines, stock);
    }

    internal static void BindFieldFlySuccessor(FieldFlyMachineCatalog successor)
    {
        if (!ReferenceEquals(FieldFlyMachineCatalog.For(successor.Catalog), successor) ||
            For(successor.Predecessor) is not { } before || !ReferenceEquals(before._owner, successor.Owner))
            throw new ArgumentException("department_field_fly_successor_proof");
        var stock = new DepartmentStoreMachineStock(before.OriginalMachines, successor.Catalog, before._owner)
        { RooftopPredecessor = before.RooftopPredecessor };
        stock.Validate(); Proofs.Add(stock.Machines, stock);
    }

    internal static void BindBreedingSuccessor(BreedingMachineCatalog successor)
    {
        if (!ReferenceEquals(BreedingMachineCatalog.For(successor.Catalog), successor) ||
            For(successor.Predecessor) is not { } before ||
            !ReferenceEquals(before._owner, successor.Pokemon.Predecessor.SharedBattle))
            throw new ArgumentException("department_breeding_successor_proof");
        var stock = new DepartmentStoreMachineStock(before.OriginalMachines, successor.Catalog, successor.Pokemon.SharedBattle)
        { RooftopPredecessor = before.RooftopPredecessor };
        stock.Validate(); Proofs.Add(stock.Machines, stock);
    }

    internal static void BindOrdinaryEvolutionSuccessor(OrdinaryEvolutionMachineCatalog successor)
    {
        if (!ReferenceEquals(OrdinaryEvolutionMachineCatalog.For(successor.Catalog), successor) ||
            For(successor.Predecessor) is not { } before ||
            !ReferenceEquals(before._owner, successor.Pokemon.Predecessor.SharedBattle))
            throw new ArgumentException("department_ordinary_evolution_successor_proof");
        var stock = new DepartmentStoreMachineStock(before.OriginalMachines, successor.Catalog, successor.Pokemon.SharedBattle)
        { RooftopPredecessor = before.RooftopPredecessor };
        stock.Validate(); Proofs.Add(stock.Machines, stock);
    }

    internal static void BindOriginalMachineSuccessor(OriginalMachineCatalog successor)
    {
        if (!ReferenceEquals(OriginalMachineCatalog.For(successor.Catalog), successor) ||
            For(successor.Predecessor.Catalog) is not { } before ||
            !ReferenceEquals(before._owner, successor.Pokemon.SharedBattle))
            throw new ArgumentException("department_original_machine_successor_proof");
        var stock = new DepartmentStoreMachineStock(before.OriginalMachines, successor.Catalog, before._owner)
        { RooftopPredecessor = before.RooftopPredecessor };
        stock.Validate(); Proofs.Add(stock.Machines, stock);
    }

    public bool IsClerk(string mapId, string sourcePath, string script, string stockId) =>
        mapId == ExpectedMapId && sourcePath == ExpectedSourcePath && script == ExpectedClerkScript && stockId == Id;

    public override void Validate()
    {
        if (!ReferenceEquals(OwnedMachineCatalogBinding.For(Machines), _owner))
            throw new NotSupportedException("department_machine_owner");
        foreach (var row in Expected)
        {
            var value = PewterMachineRules.Definition(Machines, new("pret:ITEM_" + row.Item));
            var item = value?.Machine.Item;
            if (item is null || item.SourceItemSymbol != "ITEM_" + row.Item || item.SourceItemId != row.Id ||
                item.Name != row.Item || item.Price != row.Price || item.Pocket != "POCKET_TM_CASE" ||
                item.Importance != 0 || !item.CanSell || value!.Move != new MoveId("pret:MOVE_" + row.Move) ||
                value.SourceMoveId != row.MoveId || value.MovePp != row.Pp)
                throw new NotSupportedException("department_machine_stock_definition");
        }
    }
}
