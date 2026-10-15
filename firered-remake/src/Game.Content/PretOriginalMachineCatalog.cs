using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record OriginalMachinesDocument(string SourceCommit, string Profile, int PreviousMachineCount, int MachineCount, int SpeciesCount,
    ImmutableArray<PewterAdditionalMachine> Additions);

/// <summary>Authenticated original machine definitions, compatibility, and source acquisition map.</summary>
public static class PretOriginalMachineCatalog
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/original-machine-catalog", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("original_machine_source_missing");
    }
    public static OriginalMachinesDocument LoadDocument(string root)
    {
        var document = Read<OriginalMachinesDocument>(root,"machines.json");
        if (document.PreviousMachineCount != 51 ||
            document.MachineCount != 57 || document.SpeciesCount != 186 ||
            document.Additions.IsDefault || document.Additions.Length != 6)
            throw new InvalidDataException("original_machine_scope");
        return document;
    }
    public static OriginalMachineCatalog Load(string root, OrdinaryEvolutionMachineCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var document = LoadDocument(root);
        return OriginalMachineCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = OriginalMachineCatalog.Profile,AdditionalMachines = predecessor.Catalog.AdditionalMachines!.Value.AddRange(document.Additions)
        });
    }
    public static JsonElement LoadAcquisitions(string root)
    {
        var document = Read<JsonElement>(root,"acquisitions.json");
        return document;
    }
}
