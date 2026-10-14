using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record DepartmentMachineSourceInput(string Path, string LocalPath, int ByteLength, string Sha256);
public sealed record DepartmentMachineDocument(int SchemaVersion, string SourceCommit, string ContractSha256,
    string StockId, ImmutableArray<string> StockItems, int InheritedMachineCount, int MachineCount, int SpeciesCount,
    ImmutableArray<DepartmentMachineSourceInput> Inputs, string Tm15SemanticSha256, PewterAdditionalMachine Tm15,
    int MovePower, int MoveAccuracy, string MoveType, string MoveEffect, string IconSourceItem);

/// <summary>Reuses previously imported sources. No maps, species, graphics, or audio are imported.</summary>
public static class PretDepartmentStoreMachines
{
    public const string GeneratedSha256 = "4b6d7d5d11c453a081c7fafef86e296938a12fe4b8df77e1513479be5701d48a";
    public static ItemId ReusedTm15Icon { get; } = new("pret:ITEM_TM05");
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = ["department-machine-stock:" + GeneratedSha256];
    public static DepartmentMachineDocument LoadDocument(string projectRoot)
    {
        var bytes = File.ReadAllBytes(Path.Combine(projectRoot, "content/generated/department-machine-stock/machines.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != GeneratedSha256)
            throw new InvalidDataException("department_machine_source_bytes");
        var document = JsonSerializer.Deserialize<DepartmentMachineDocument>(bytes,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) ??
            throw new InvalidDataException("department_machine_document_missing");
        if (document.SchemaVersion != 1 || document.SourceCommit != OpeningInventoryRules.SourceCommit ||
            document.StockId != DepartmentStoreMachineStock.ExpectedStockId || document.InheritedMachineCount != 41 ||
            document.MachineCount != 42 || document.SpeciesCount != 163 || document.Tm15SemanticSha256 != DepartmentStoreMachineStock.Tm15SourceSha256 ||
            document.MovePower != 150 || document.MoveAccuracy != 90 || document.MoveType != "TYPE_NORMAL" || document.MoveEffect != "EFFECT_RECHARGE" || document.IconSourceItem != ReusedTm15Icon.Value)
            throw new InvalidDataException("department_machine_document_scope");
        var contract = File.ReadAllBytes(Path.Combine(projectRoot, "scripts/department-machine-stock.contract.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(contract)) != document.ContractSha256)
            throw new InvalidDataException("department_machine_source_contract");
        return document;
    }
    public static void ValidateSourceEvidence(string projectRoot, DepartmentMachineDocument document)
    {
        foreach (var input in document.Inputs)
        {
            if (Path.IsPathRooted(input.LocalPath) || input.LocalPath.Split('/').Any(part => part is "" or "." or ".."))
                throw new InvalidDataException("department_machine_source_path");
            var bytes = File.ReadAllBytes(Path.Combine(projectRoot, input.LocalPath));
            if (bytes.Length != input.ByteLength || Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.Sha256)
                throw new InvalidDataException("department_machine_source_input:" + input.Path);
        }
    }
    public static DepartmentStoreMachineStock Load(string projectRoot, PretDepartmentStoreContract store,
        PewterMachineCatalog originalMachines, string? sourceRoot = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        var document = LoadDocument(projectRoot);
        ValidateSourceEvidence(sourceRoot ?? projectRoot, document);
        var stock = DepartmentStoreMachineStock.Authenticate(originalMachines, document.Tm15);
        var source = store.Stocks.Single(s => s.Id == stock.Id);
        if (source.ClerkScript != DepartmentStoreMachineStock.ExpectedClerkScript ||
            source.Source.Path != DepartmentStoreMachineStock.Source.Path || source.Source.Symbol != DepartmentStoreMachineStock.Source.Symbol ||
            source.Source.LineStart != DepartmentStoreMachineStock.Source.StartLine || source.Source.LineEnd != DepartmentStoreMachineStock.Source.EndLine ||
            source.Source.Sha256 != DepartmentStoreMachineStock.Source.Sha256 ||
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
