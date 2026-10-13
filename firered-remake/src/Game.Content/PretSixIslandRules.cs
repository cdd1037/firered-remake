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
    public const string TrainersSha256 = "aeff4df0217ae10670d12101ff0e94b5d98572c4c7165844f522ad35d41cc178";
    public const string PokemonSha256 = "991c172e3d8bcefe39e76d1d595ad390d0a71e7b0886d47e7030369a895c624f";
    public const string RulesSha256 = "cb78c554b72a6dfcbe4cf21e60fa60eee51ee87b62988fdff03de013341106ae";
    public const string SourceInputsSha256 = "ec437b15e7fff2a7bbb142639069746f88e822331da623856858778062a8a795";
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/six-island-rules", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("Six Island source bytes differ: " + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }) ?? throw new InvalidDataException("Missing Six Island source document.");
    }
    public static SixIslandTrainerDocument LoadDocument(string root)
    {
        var document = Read<SixIslandTrainerDocument>(root, "trainers.json", TrainersSha256);
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
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("profile").GetString() != SixIslandOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256)
            throw new InvalidDataException("Six Island owned source differs.");
        return LoadOwned(predecessor);
    }
    public static SixIslandOpponentPokemonCatalog LoadOpponents(FourIslandOpponentPokemonCatalog predecessor,
        SixIslandOwnedPokemonCatalog owned) => SixIslandOpponentPokemonCatalog.Authenticate(predecessor, owned);
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root, "rules.json", RulesSha256);
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("Six Island source evidence identity");
        foreach (var row in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = row.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != row.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != row.GetProperty("sha256").GetString())
                throw new InvalidDataException("Six Island dependency differs: " + path);
        }
        _ = Read<JsonElement>(root, "pokemon.json", PokemonSha256); _ = LoadRules(root);
    }
}
