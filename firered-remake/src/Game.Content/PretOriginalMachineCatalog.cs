using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record OriginalMachinesDocument(string SourceCommit, string SourceTree, string Profile,
    int PreviousMachineCount, int MachineCount, int SpeciesCount, string PredecessorMachinesSha256,
    ImmutableArray<PewterAdditionalMachine> Additions, string AdditionsSemanticSha256);

/// <summary>Authenticated original machine definitions, compatibility, and source acquisition map.</summary>
public static class PretOriginalMachineCatalog
{
    public const string MachinesSha256 = "5db41d3beea3dca70f36433d6f07763becd09b585e1e168d3805eb5fc3a66a70";
    public const string AcquisitionsSha256 = "69ee0497d790e4d585ed0249ea43ea77b73cb7181fcd509d19065e686ecdcb0f";
    public const string SourceInputsSha256 = "8a914f723698b99f421165eb15e263d9ca7c730186e0376b9d2b722b3ba49ec8";
    public const string LockSha256 = "76da96722f78626e8690e760c2c5fe5218a77f70758382437e9e99c64f5d3bf7";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [MachinesSha256, AcquisitionsSha256, SourceInputsSha256, LockSha256];
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/original-machine-catalog", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("original_machine_source_bytes:" + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("original_machine_source_missing");
    }
    public static OriginalMachinesDocument LoadDocument(string root)
    {
        var document = Read<OriginalMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.SourceTree != PretCanonicalPokemonCatalog.SourceTree ||
            document.Profile != OriginalMachineCatalog.Profile || document.PreviousMachineCount != 51 ||
            document.MachineCount != 57 || document.SpeciesCount != 186 ||
            document.PredecessorMachinesSha256 != PretOrdinaryEvolutionSpecies.MachinesSha256 ||
            document.AdditionsSemanticSha256 != OriginalMachineCatalog.AdditionsSourceSha256 ||
            document.Additions.IsDefault || document.Additions.Length != 6)
            throw new InvalidDataException("original_machine_scope");
        return document;
    }
    public static OriginalMachineCatalog Load(string root, OrdinaryEvolutionMachineCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var document = LoadDocument(root);
        return OriginalMachineCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = OriginalMachineCatalog.Profile,
            AdditionalMachines = predecessor.Catalog.AdditionalMachines!.Value.AddRange(document.Additions)
        });
    }
    public static JsonElement LoadAcquisitions(string root)
    {
        var document = Read<JsonElement>(root, "acquisitions.json", AcquisitionsSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree ||
            document.GetProperty("profile").GetString() != OriginalMachineCatalog.Profile)
            throw new InvalidDataException("original_machine_acquisition_scope");
        return document;
    }
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var lockBytes = File.ReadAllBytes(Path.Combine(root, "scripts/original-machine-catalog.lock.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != LockSha256) throw new InvalidDataException("original_machine_lock_bytes");
        using var locked = JsonDocument.Parse(lockBytes);
        var importer = File.ReadAllBytes(Path.Combine(root, "scripts/import-original-machine-catalog.py"));
        if (Convert.ToHexStringLower(SHA256.HashData(importer)) != locked.RootElement.GetProperty("importerSha256").GetString())
            throw new InvalidDataException("original_machine_importer_bytes");
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("original_machine_source_identity");
        foreach (var input in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString())
                throw new InvalidDataException("original_machine_dependency_bytes:" + path);
        }
        _ = LoadDocument(root); _ = LoadAcquisitions(root);
    }
}
