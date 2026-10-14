using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Small authenticated ownership delta over shared canonical source mechanics.</summary>
public static class PretGameCornerSpecies
{
    public const string CatalogSha256 = PokemonSha256;
    public const string PokemonSha256 = "6eaabfb58b19e2513f78fbb1f326cbc09f85011db33b26a650c90566901fa9c8";
    public const string SourceInputsSha256 = "4b39e11cef9a7fede55b50aa9cd64eba820b614510e91c34468d5a3d76403535";
    public const string LockSha256 = "7cacdb8cfc72e186f4461e5898903d941d8a341b11e39ee208498e0972eeb412";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [PokemonSha256, SourceInputsSha256, LockSha256];

    private static JsonElement Read(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/game-corner-species", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("game-corner-species_source_bytes:" + file);
        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.Clone();
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree ||
            document.GetProperty("profile").GetString() != GameCornerOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.GetProperty("predecessorCatalogSha256").GetString() != PretStoneEvolutionSpecies.PokemonSha256 ||
            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 170 || document.GetProperty("ownedSpeciesCount").GetInt32() != 174 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 195 || document.GetProperty("opponentSpeciesCount").GetInt32() != 196 ||
            !document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(GameCornerOwnedPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) ||
            !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(GameCornerOpponentPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) ||
            document.GetProperty("trainerOnlySpeciesAdded").GetArrayLength() != 0 ||
            document.GetProperty("movesAdded").GetArrayLength() != 0 || document.GetProperty("evolutions").GetArrayLength() != 2 ||
            document.GetProperty("excludedEvolutions").GetArrayLength() != 1)
            throw new InvalidDataException("game-corner-species_pokemon_scope");
        return document;
    }

    public static GameCornerOwnedPokemonCatalog LoadOwned(string root, StoneEvolutionOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in GameCornerOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, GameCornerOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return GameCornerOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }

    public static GameCornerOpponentPokemonCatalog LoadOpponents(string root, StoneEvolutionOpponentPokemonCatalog predecessor,
        GameCornerOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return GameCornerOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var lockBytes = File.ReadAllBytes(Path.Combine(root, "scripts/game-corner-species.lock.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != LockSha256) throw new InvalidDataException("game-corner-species_lock_bytes");
        var inputs = Read(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("game-corner-species_source_identity");
        foreach (var input in inputs.GetProperty("inputs").EnumerateArray())
        {
            var path = input.GetProperty("localPath").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (!input.GetProperty("shared").GetBoolean() || bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString() ||
                Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != input.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("game-corner-species_source_bytes:" + path);
        }
        foreach (var input in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() || Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString())
                throw new InvalidDataException("game-corner-species_dependency_bytes:" + path);
        }
        using var locked = JsonDocument.Parse(lockBytes);
        var importer = File.ReadAllBytes(Path.Combine(root, "scripts/import-game-corner-species.py"));
        if (Convert.ToHexStringLower(SHA256.HashData(importer)) != locked.RootElement.GetProperty("importerSha256").GetString())
            throw new InvalidDataException("game-corner-species_importer_bytes");
        _ = LoadPokemonDocument(root);
    }
}
