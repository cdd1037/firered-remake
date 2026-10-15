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
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/viridian-gym-rules", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("Missing Viridian Gym source document.");
    }
    public static ViridianGymTrainerDocument LoadDocument(string root)
    {
        var document = Read<ViridianGymTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document);
        return document;
    }
    public static ViridianGymTrainerCatalog LoadTrainers(string root) =>
        LoadTrainers(Read<ViridianGymTrainerDocument>(root,"trainers.json"));
    private static ViridianGymTrainerCatalog LoadTrainers(ViridianGymTrainerDocument document)
    {
        var result = new ViridianGymTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        ViridianGymTrainerPartyFactory.ValidateCatalog(result);
        return result;
    }
    public static ViridianGymMachineDocument LoadMachines(string root)
    {
        var document = Read<ViridianGymMachineDocument>(root,"machines.json");
        if (            document.Machine?.Machine?.Item?.Id != ViridianGymMachineCatalog.Tm26Id || document.RulesSources.IsDefaultOrEmpty)
            throw new InvalidDataException("Viridian Gym machine scope differs.");
        return document;
    }
    public static ViridianGymMachineCatalog ExtendMachines(CinnabarMachineCatalog predecessor, string root)
    {
        var source = LoadMachines(root);
        return ViridianGymMachineCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = ViridianGymMachineCatalog.Profile,AdditionalMachines = predecessor.Catalog.AdditionalMachines!.Value.Add(source.Machine)
        });
    }
}
