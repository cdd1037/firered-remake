using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record FourIslandTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);

/// <summary>Source-authenticated initial trainer catalog and additive owned/opponent proofs; no runtime route admission.</summary>
public static class PretFourIslandRules
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/four-island-rules", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }) ?? throw new InvalidDataException("Missing Four Island source document.");
    }
    public static FourIslandTrainerDocument LoadDocument(string root)
    {
        var document = Read<FourIslandTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document); return document;
    }
    public static FourIslandTrainerCatalog LoadTrainers(string root) => LoadTrainers(LoadDocument(root));
    private static FourIslandTrainerCatalog LoadTrainers(FourIslandTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty) throw new InvalidDataException("Missing Four Island trainer records.");
        var result = new FourIslandTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        FourIslandTrainerPartyFactory.ValidateCatalog(result); return result;
    }
    public static FourIslandOwnedPokemonCatalog LoadOwned(SeviiRubyOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in FourIslandOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, FourIslandOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return FourIslandOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static FourIslandOwnedPokemonCatalog LoadOwned(string root, SeviiRubyOwnedPokemonCatalog predecessor)
    {
        return LoadOwned(predecessor);
    }
    public static FourIslandOpponentPokemonCatalog LoadOpponents(SeviiRubyOpponentPokemonCatalog predecessor,
        FourIslandOwnedPokemonCatalog owned) => FourIslandOpponentPokemonCatalog.Authenticate(predecessor, owned);
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root,"rules.json");
}
