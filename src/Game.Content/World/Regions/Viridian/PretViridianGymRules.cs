using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record ViridianGymTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles) : RegionalTrainerDocument(Trainers, Battles);

/// <summary>Regional trainer definitions and battle occurrences.</summary>
public static class PretViridianGymRules
{
    private static readonly RegionalTrainerLoader<ViridianGymTrainerDocument, OrdinaryTrainerCatalog> Loader = new(
        "content/generated/data/regions/viridian-gym/trainers.json", "Missing Viridian Gym source document.",
        document => new(document.Trainers.Distinct().ToImmutableDictionary(t => t.Id), document.Battles, "viridian-gym-trainer"));

    public static ViridianGymTrainerDocument LoadDocument(string root) => Loader.LoadDocument(root, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    public static OrdinaryTrainerCatalog LoadTrainers(string root) => Loader.LoadCatalog(root, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
