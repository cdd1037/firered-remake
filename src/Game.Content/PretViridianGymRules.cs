using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record ViridianGymTrainerDocument(ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

/// <summary>Hash-locked rules from pinned Git blobs; loads no new species or move metadata.</summary>
public static class PretViridianGymRules
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/data/regions/viridian-gym", file));
        return JsonSerializer.Deserialize<T>(bytes, SharedTrainerCatalog.Options(root, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }))
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
        var result = new ViridianGymTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        ViridianGymTrainerPartyFactory.ValidateCatalog(result);
        return result;
    }
}
