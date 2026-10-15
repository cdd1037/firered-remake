using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record DepartmentMachineSourceInput(string Path, string LocalPath);
public sealed record DepartmentMachineDocument(int SchemaVersion, string SourceCommit, string StockId, ImmutableArray<string> StockItems,
    int InheritedMachineCount, int MachineCount, int SpeciesCount,
    ImmutableArray<DepartmentMachineSourceInput> Inputs, PewterAdditionalMachine Tm15, int MovePower,
    int MoveAccuracy, string MoveType, string MoveEffect, string IconSourceItem);

/// <summary>Reuses previously imported sources. No maps, species, graphics, or audio are imported.</summary>
public static class PretDepartmentStoreMachines
{
    public static ItemId ReusedTm15Icon { get; } = new("pret:ITEM_TM05");
    public static DepartmentMachineDocument LoadDocument(string projectRoot)
    {
        var bytes = File.ReadAllBytes(Path.Combine(projectRoot, "content/generated/department-machine-stock/machines.json"));
        var document = JsonSerializer.Deserialize<DepartmentMachineDocument>(bytes,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) ??
            throw new InvalidDataException("department_machine_document_missing");
        if (document.SchemaVersion != 1 ||
            document.StockId != DepartmentStoreMachineStock.ExpectedStockId || document.MovePower != 150 || document.MoveAccuracy != 90 || document.MoveType != "TYPE_NORMAL" || document.MoveEffect != "EFFECT_RECHARGE" || document.IconSourceItem != ReusedTm15Icon.Value)
            throw new InvalidDataException("department_machine_document_scope");
        return document;
    }
    public static DepartmentStoreMachineStock Load(string projectRoot, PretDepartmentStoreContract store,
        PewterMachineCatalog originalMachines, string? sourceRoot = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        var document = LoadDocument(projectRoot);
        var stock = DepartmentStoreMachineStock.Authenticate(originalMachines, document.Tm15);
        var source = store.Stocks.Single(s => s.Id == stock.Id);
        if (source.ClerkScript != DepartmentStoreMachineStock.ExpectedClerkScript ||
            source.Source.Path != DepartmentStoreMachineStock.Source.Path || source.Source.Symbol != DepartmentStoreMachineStock.Source.Symbol ||
            source.Source.LineStart != DepartmentStoreMachineStock.Source.StartLine || source.Source.LineEnd != DepartmentStoreMachineStock.Source.EndLine ||
            !source.Items.Select(i => i.StableId).SequenceEqual(document.StockItems) ||
            !source.Items.Select(i => i.StableId).SequenceEqual(stock.Items.Select(i => i.Value)))
            throw new InvalidDataException("department_machine_original_shelf");
        foreach (var row in source.Items)
        {
            var definition = PewterMachineRules.Definition(stock.Machines, new(row.StableId))!.Machine.Item;
            if (definition.SourceItemId != row.SourceItemId || definition.Name != row.Name || definition.Price != row.Price || definition.Pocket != row.Pocket)
                throw new InvalidDataException("department_machine_original_price");
        }
        return stock;
    }
}
