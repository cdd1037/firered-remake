using System.Collections.Immutable;
using System.Security.Cryptography;
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
    public const string TrainersSha256 = "ab42c255dbbfa274f9755564003fe30f9a2b86b7d36076d2aea458f444fd0ff5";
    public const string PokemonSha256 = "8b99b80f0b53518468ca6bf863b5e00b4983f250fc7051dc8d84ab67b17db054";
    public const string RulesSha256 = "28231ab8ef38f510bc0f3de0d4bfa4e7019a5e54255258fcf3ccb4a3162a76b6";
    public const string SourceInputsSha256 = "2ed0f1914c50b16202e68b20bfd291d675667296fb62d4dbdfe56c84f73618e6";
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/four-island-rules", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("Four Island source bytes differ: " + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }) ?? throw new InvalidDataException("Missing Four Island source document.");
    }
    public static FourIslandTrainerDocument LoadDocument(string root)
    {
        var document = Read<FourIslandTrainerDocument>(root, "trainers.json", TrainersSha256);
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
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("profile").GetString() != FourIslandOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256)
            throw new InvalidDataException("Four Island owned source differs.");
        return LoadOwned(predecessor);
    }
    public static FourIslandOpponentPokemonCatalog LoadOpponents(SeviiRubyOpponentPokemonCatalog predecessor,
        FourIslandOwnedPokemonCatalog owned) => FourIslandOpponentPokemonCatalog.Authenticate(predecessor, owned);
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root, "rules.json", RulesSha256);
    public static void ValidateSourceEvidence(string root)
    {
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("Four Island source evidence identity");
        foreach (var row in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = row.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(root, path));
            if (bytes.Length != row.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != row.GetProperty("sha256").GetString())
                throw new InvalidDataException("Four Island dependency differs: " + path);
        }
        _ = Read<JsonElement>(root, "pokemon.json", PokemonSha256); _ = LoadRules(root);
    }
}
