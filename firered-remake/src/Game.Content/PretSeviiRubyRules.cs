using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record SeviiRubyTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);

/// <summary>Source-authenticated initial trainer catalog and additive owned/opponent proofs; no runtime route admission.</summary>
public static class PretSeviiRubyRules
{
    public const string TrainersSha256 = "7ebbcb6c8fe122e6f4285032b03e3b4103fbf92b4d3380e7f4c51a0aafb44f5a";
    public const string PokemonSha256 = "6e5e7fb4d3954cbbc8ed244f71d28bf0c3e3b43306b788ef8b309c5955a96b46";
    public const string RulesSha256 = "cce6265420c96f366baa322143c51f1125347bfbfe18d60f7a0c511773298197";
    public const string SourceInputsSha256 = "8af6ccc76bbfae80b9c5fde91848782205fec385c3fb353d345b7f9c931dd690";
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/sevii-ruby-rules", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("Sevii Ruby source bytes differ: " + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }) ?? throw new InvalidDataException("Missing Sevii Ruby source document.");
    }
    public static SeviiRubyTrainerDocument LoadDocument(string root)
    {
        var document = Read<SeviiRubyTrainerDocument>(root, "trainers.json", TrainersSha256);
        _ = LoadTrainers(document); return document;
    }
    public static SeviiRubyTrainerCatalog LoadTrainers(string root) => LoadTrainers(LoadDocument(root));
    private static SeviiRubyTrainerCatalog LoadTrainers(SeviiRubyTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty) throw new InvalidDataException("Missing Ruby trainer records.");
        var result = new SeviiRubyTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        SeviiRubyTrainerPartyFactory.ValidateCatalog(result); return result;
    }
    public static SeviiRubyOwnedPokemonCatalog LoadOwned(CinnabarOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species.SetItem(SeviiRubyOwnedPokemonCatalog.Ponyta,
            SeviiRubyOwnedPokemonCatalog.ExpectedSpecies(predecessor, SeviiRubyOwnedPokemonCatalog.Ponyta));
        foreach (var id in SeviiRubyOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, SeviiRubyOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return SeviiRubyOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static SeviiRubyOwnedPokemonCatalog LoadOwned(string root, CinnabarOwnedPokemonCatalog predecessor)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("profile").GetString() != SeviiRubyOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256)
            throw new InvalidDataException("Ruby owned source differs.");
        return LoadOwned(predecessor);
    }
    public static SeviiRubyOpponentPokemonCatalog LoadOpponents(PokemonLeagueOpponentPokemonCatalog predecessor,
        SeviiRubyOwnedPokemonCatalog owned) => SeviiRubyOpponentPokemonCatalog.Authenticate(predecessor, owned);
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root, "rules.json", RulesSha256);
    public static void ValidateSourceEvidence(string root)
    {
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        foreach (var row in inputs.GetProperty("files").EnumerateArray())
        {
            var path = row.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(root, "content/raw/sevii-ruby-rules", path));
            if (bytes.Length != row.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != row.GetProperty("sha256").GetString())
                throw new InvalidDataException("Ruby raw source differs: " + path);
        }
        _ = Read<JsonElement>(root, "pokemon.json", PokemonSha256); _ = LoadRules(root);
    }
}
