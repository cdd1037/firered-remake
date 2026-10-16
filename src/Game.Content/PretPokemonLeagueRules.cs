using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Trainers;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record PokemonLeagueTrainerDocument(ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

public sealed record PokemonLeagueOpponentDocument(ImmutableArray<string> OwnedSpeciesAdded,
    ImmutableArray<PokemonSpeciesDefinition> OpponentSpecies);

/// <summary>Exact first League source teams and the minimal trainer-only species continuation.</summary>
public static class PretPokemonLeagueRules
{

    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/data/regions/pokemon-league", file));
        return JsonSerializer.Deserialize<T>(bytes, SharedTrainerCatalog.Options(root, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }))
            ?? throw new InvalidDataException("Missing Pokemon League source document.");
    }

    public static PokemonLeagueTrainerDocument LoadDocument(string root)
    {
        var document = Read<PokemonLeagueTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document);
        return document;
    }

    public static PokemonLeagueTrainerCatalog LoadTrainers(string root) =>
        LoadTrainers(Read<PokemonLeagueTrainerDocument>(root,"trainers.json"));

    private static PokemonLeagueTrainerCatalog LoadTrainers(PokemonLeagueTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty)
            throw new InvalidDataException("Missing Pokemon League trainer or battle records.");
        var result = new PokemonLeagueTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        PokemonLeagueTrainerPartyFactory.ValidateCatalog(result);
        return result;
    }

}
