using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record ViridianGymTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);
public sealed record ViridianGymMachineDocument(string SourceCommit, string Profile, PewterAdditionalMachine Machine,
    ImmutableArray<OpeningItemSourceRef> RulesSources);

/// <summary>Hash-locked rules from pinned Git blobs; loads no new species or move metadata.</summary>
public static class PretViridianGymRules
{
    public const string TrainersSha256 = "f15a1393f4d13b55c04de2203c23562d3813ca037fb9d5ffecf23340285b0f97";
    public const string MachinesSha256 = "1b41e7eb6de99f158243b8c22da3f136cbd18f98c970eea4af0b21aba0668db4";
    public const string RulesSha256 = "4397b4350f0b4984c70a4dca134fbf4d97705a25c6eba75fa1b67111513203b1";
    public const string SourceInputsSha256 = "6a7482ca29f6b060caa5477fa21627db9db17ffc582433d25e6aca15584a6e11";
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/viridian-gym-rules", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("Viridian Gym source bytes differ: " + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("Missing Viridian Gym source document.");
    }
    public static ViridianGymTrainerDocument LoadDocument(string root)
    {
        var document = Read<ViridianGymTrainerDocument>(root, "trainers.json", TrainersSha256);
        _ = LoadTrainers(document);
        return document;
    }
    public static ViridianGymTrainerCatalog LoadTrainers(string root) =>
        LoadTrainers(Read<ViridianGymTrainerDocument>(root, "trainers.json", TrainersSha256));
    private static ViridianGymTrainerCatalog LoadTrainers(ViridianGymTrainerDocument document)
    {
        var result = new ViridianGymTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        ViridianGymTrainerPartyFactory.ValidateCatalog(result);
        return result;
    }
    public static ViridianGymMachineDocument LoadMachines(string root)
    {
        var document = Read<ViridianGymMachineDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != ViridianGymTrainerPartyFactory.SourceCommit || document.Profile != "firered-viridian-gym-machines-v1" ||
            document.Machine?.Machine?.Item?.Id != ViridianGymMachineCatalog.Tm26Id || document.RulesSources.IsDefaultOrEmpty)
            throw new InvalidDataException("Viridian Gym machine scope differs.");
        return document;
    }
    public static ViridianGymMachineCatalog ExtendMachines(CinnabarMachineCatalog predecessor, string root)
    {
        var source = LoadMachines(root);
        return ViridianGymMachineCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = ViridianGymMachineCatalog.Profile,
            AdditionalMachines = predecessor.Catalog.AdditionalMachines!.Value.Add(source.Machine)
        });
    }
    public static void ValidateSourceEvidence(string root)
    {
        _ = Read<JsonElement>(root, "rules.json", RulesSha256);
        _ = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
    }
}
