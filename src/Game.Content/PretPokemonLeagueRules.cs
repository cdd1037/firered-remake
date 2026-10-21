using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record PokemonLeagueTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles) : RegionalTrainerDocument(Trainers, Battles);

public sealed record PokemonLeagueOpponentDocument(ImmutableArray<string> OwnedSpeciesAdded,
    ImmutableArray<Game.Domain.Pokemon.PokemonSpeciesDefinition> OpponentSpecies);

/// <summary>Regional trainer definitions and battle occurrences.</summary>
public static class PretPokemonLeagueRules
{
    private static readonly RegionalTrainerLoader<PokemonLeagueTrainerDocument, PokemonLeagueTrainerCatalog> Loader = new(
        "content/generated/data/regions/pokemon-league/trainers.json", "Missing Pokemon League source document.",
        document => document.ByTrainer((trainers, battles) => new PokemonLeagueTrainerCatalog(trainers, battles),
            "Missing Pokemon League trainer or battle records."), PokemonLeagueTrainerPartyFactory.ValidateCatalog);

    public static PokemonLeagueTrainerDocument LoadDocument(string root) => Loader.LoadDocument(root);
    public static PokemonLeagueTrainerCatalog LoadTrainers(string root) => Loader.LoadCatalog(root);
}
