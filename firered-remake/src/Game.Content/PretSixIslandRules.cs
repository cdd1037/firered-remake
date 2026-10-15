using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record SixIslandTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);

/// <summary>Source-authenticated initial trainer catalog and additive owned/opponent proofs; no runtime route admission.</summary>
public static class PretSixIslandRules
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/six-island-rules", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }) ?? throw new InvalidDataException("Missing Six Island source document.");
    }
    public static SixIslandTrainerDocument LoadDocument(string root)
    {
        var document = Read<SixIslandTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document); return document;
    }
    public static SixIslandTrainerCatalog LoadTrainers(string root) => LoadTrainers(LoadDocument(root));
    private static SixIslandTrainerCatalog LoadTrainers(SixIslandTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty) throw new InvalidDataException("Missing Six Island trainer records.");
        var result = new SixIslandTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        SixIslandTrainerPartyFactory.ValidateCatalog(result); return result;
    }
    public static SixIslandOwnedPokemonCatalog LoadOwned(FourIslandOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in SixIslandOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, SixIslandOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return SixIslandOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static SixIslandOwnedPokemonCatalog LoadOwned(string root, FourIslandOwnedPokemonCatalog predecessor)
    {
        return LoadOwned(predecessor);
    }
    public static SixIslandOpponentPokemonCatalog LoadOpponents(FourIslandOpponentPokemonCatalog predecessor,
        SixIslandOwnedPokemonCatalog owned) => SixIslandOpponentPokemonCatalog.Authenticate(predecessor, owned);
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root,"rules.json");
}
