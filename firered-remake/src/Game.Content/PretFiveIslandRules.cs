using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record FiveIslandTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);

/// <summary>Source-authenticated initial trainer catalog and additive owned/opponent proofs; no runtime route admission.</summary>
public static class PretFiveIslandRules
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/five-island-rules", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }) ?? throw new InvalidDataException("Missing Five Island source document.");
    }
    public static FiveIslandTrainerDocument LoadDocument(string root)
    {
        var document = Read<FiveIslandTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document); return document;
    }
    public static FiveIslandTrainerCatalog LoadTrainers(string root) => LoadTrainers(LoadDocument(root));
    private static FiveIslandTrainerCatalog LoadTrainers(FiveIslandTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty) throw new InvalidDataException("Missing Five Island trainer records.");
        var result = new FiveIslandTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        FiveIslandTrainerPartyFactory.ValidateCatalog(result); return result;
    }
    public static FiveIslandOwnedPokemonCatalog LoadOwned(SixIslandOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in FiveIslandOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, FiveIslandOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return FiveIslandOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static FiveIslandOwnedPokemonCatalog LoadOwned(string root, SixIslandOwnedPokemonCatalog predecessor)
    {
        return LoadOwned(predecessor);
    }
    public static FiveIslandOpponentPokemonCatalog LoadOpponents(SixIslandOpponentPokemonCatalog predecessor,
        FiveIslandOwnedPokemonCatalog owned) => FiveIslandOpponentPokemonCatalog.Authenticate(predecessor, owned);
    public static JsonElement LoadEncounterDeclarations(string root) => Read<JsonElement>(root,"encounters.json");
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root,"rules.json");
}
