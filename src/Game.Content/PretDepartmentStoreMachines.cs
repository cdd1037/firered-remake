using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record DepartmentMachineDocument(int SchemaVersion, string StockId, ImmutableArray<string> StockItems,
    int InheritedMachineCount, int MachineCount, int SpeciesCount,
    MachineDefinition Tm15, int MovePower,
    int MoveAccuracy, string MoveType, string MoveEffect, string IconSourceItem);

/// <summary>Reuses previously imported sources. No maps, species, graphics, or audio are imported.</summary>
public static class PretDepartmentStoreMachines
{
    public static ItemId ReusedTm15Icon { get; } = new("pret:ITEM_TM05");
    public static DepartmentMachineDocument LoadDocument(string projectRoot, SharedMachineCatalog? shared = null)
    {
        var bytes = File.ReadAllBytes(Path.Combine(projectRoot, "content/generated/data/machine-learning/department-machine-stock.json"));
        var document = JsonSerializer.Deserialize<DepartmentMachineDocument>(bytes,
            (shared ?? SharedMachineCatalog.LoadProject(projectRoot)).CreateJsonOptions(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })) ??
            throw new InvalidDataException("department_machine_document_missing");
        if (document.SchemaVersion != 1 ||
            document.StockId != DepartmentStoreMachineStock.ExpectedStockId || document.MovePower != 150 || document.MoveAccuracy != 90 || document.MoveType != "TYPE_NORMAL" || document.MoveEffect != "EFFECT_RECHARGE" || document.IconSourceItem != ReusedTm15Icon.Value)
            throw new InvalidDataException("department_machine_document_scope");
        return document;
    }
    public static DepartmentStoreMachineStock Load(string projectRoot, PretDepartmentStoreContract store,
        MachineCatalog originalMachines, string? sourceRoot = null, SharedMachineCatalog? shared = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        var document = LoadDocument(projectRoot, shared);
        var stock = new DepartmentStoreMachineStock(originalMachines);
        var source = store.Stocks.Single(s => s.Id == stock.Id);
        if (source.ClerkScript != DepartmentStoreMachineStock.ExpectedClerkScript ||
            source.Source.Path != DepartmentStoreMachineStock.Source.Path || source.Source.Symbol != DepartmentStoreMachineStock.Source.Symbol ||
            source.Source.LineStart != DepartmentStoreMachineStock.Source.StartLine || source.Source.LineEnd != DepartmentStoreMachineStock.Source.EndLine ||
            !source.Items.Select(i => i.StableId).SequenceEqual(document.StockItems) ||
            !source.Items.Select(i => i.StableId).SequenceEqual(stock.Items.Select(i => i.Value)))
            throw new InvalidDataException("department_machine_original_shelf");
        foreach (var row in source.Items)
        {
            var definition = MachineRules.Definition(stock.Machines, new(row.StableId))!.Machine.Item;
            if (definition.SourceItemId != row.SourceItemId || definition.Name != row.Name || definition.Price != row.Price || definition.Pocket != row.Pocket)
                throw new InvalidDataException("department_machine_original_price");
        }
        return stock;
    }
}
