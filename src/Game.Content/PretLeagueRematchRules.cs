using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Trainers;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record LeagueRematchTrainerDocument(ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

public sealed record LeagueRematchOpponentDocument(ImmutableArray<string> OwnedSpeciesAdded,
    ImmutableArray<PokemonSpeciesDefinition> OpponentSpecies);

/// <summary>Exact upgraded League source teams and the minimal trainer-only species continuation.</summary>
public static class PretLeagueRematchRules
{

    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/data/regions/league-rematch", file));
        return JsonSerializer.Deserialize<T>(bytes, SharedTrainerCatalog.Options(root, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }))
            ?? throw new InvalidDataException("Missing Pokemon League source document.");
    }

    public static LeagueRematchTrainerDocument LoadDocument(string root)
    {
        var document = Read<LeagueRematchTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document);
        return document;
    }

    public static LeagueRematchTrainerCatalog LoadTrainers(string root) =>
        LoadTrainers(Read<LeagueRematchTrainerDocument>(root,"trainers.json"));

    private static LeagueRematchTrainerCatalog LoadTrainers(LeagueRematchTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty)
            throw new InvalidDataException("Missing Pokemon League trainer or battle records.");
        var result = new LeagueRematchTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        LeagueRematchTrainerPartyFactory.ValidateCatalog(result);
        return result;
    }

}
