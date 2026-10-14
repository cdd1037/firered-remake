using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record GiftEggMetadataDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    ImmutableArray<SourceEggSpeciesRow> Additions, string SemanticSha256);
public sealed record GiftEggMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record GiftEggMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    int MachineCount, ImmutableArray<GiftEggMachineRows> Additions, string AdditionsSemanticSha256, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Original gift Togepi and Togetic, existing-machine compatibility and additive breeding metadata.</summary>
public static class PretGiftEggSpecies
{
    public const string CatalogSha256 = PokemonSha256;
    public const string PokemonSha256 = "bb0487c6a2984b1baeb75f9e764e50ca46964ca8c3d6c2b655428866b71b08e6";
    public const string MachinesSha256 = "8b4377ab237f2c1ba6662c3a6ca1fa33212c7eeffa7150325163ca81b33b3544";
    public const string SourceInputsSha256 = "b7d9e98699dfdbf378d35bb384ffa55863f40584bc863eac942dc5f2dd60e3e2";
    public const string LockSha256 = "a4194f11b1b8db705914695d73cddf883be553389f557eeedb5467f7ad2f6f3c";

    public const string MetadataSha256 = "0a4bf59b2cba8faa43d2de7aed412818bc1c107d7948b4268ffac78b1c86e3e5";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [PokemonSha256, MachinesSha256, MetadataSha256, SourceInputsSha256, LockSha256];
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/gift-egg-species", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("gift-egg-species_source_bytes:" + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("gift-egg-species_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree ||
            document.GetProperty("profile").GetString() != GiftEggOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 187 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 189 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 210 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 212 ||
            !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(GiftEggOwnedPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(GiftEggOwnedPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) is false ||
            document.GetProperty("evolutions").GetArrayLength() != 1 || document.GetProperty("movesAdded").GetArrayLength() != 0)
            throw new InvalidDataException("gift-egg-species_pokemon_scope");
        return document;
    }

    public static GiftEggOwnedPokemonCatalog LoadOwned(string root, LostCaveOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root);
        return GiftEggOwnedPokemonCatalog.Authenticate(predecessor);
    }

    public static GiftEggMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<GiftEggMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != GiftEggMachineCatalog.Profile ||
            document.PreviousSpeciesCount != 187 || document.SpeciesCount != 189 || document.MachineCount != 57 ||
            document.Additions.IsDefault || document.Additions.Length != 57 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty ||
            document.AdditionsSemanticSha256 != GiftEggMachineCatalog.InheritedAdditionsSourceSha256)
            throw new InvalidDataException("gift-egg-species_machines_scope");
        return document;
    }

    public static GiftEggMachineCatalog LoadMachines(string root, PewterMachineCatalog predecessor, GiftEggOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor; var additions = document.Additions.ToDictionary(m => m.Item);
        return GiftEggMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = GiftEggMachineCatalog.Profile,
            Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static BreedingSpeciesMetadataCatalog LoadMetadata(string root, GiftEggOwnedPokemonCatalog owned)
    {
        var document = Read<GiftEggMetadataDocument>(root, "metadata.json", MetadataSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != "firered-gift-egg-metadata-v1" ||
            document.PreviousSpeciesCount != 187 || document.SpeciesCount != 189 || document.SemanticSha256 != GiftEggSpeciesMetadataCatalog.SemanticSha256)
            throw new InvalidDataException("gift_egg_metadata_scope");
        return GiftEggSpeciesMetadataCatalog.Authenticate(owned, document.Additions);
    }

    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var lockBytes = File.ReadAllBytes(Path.Combine(root, "scripts/gift-egg-species.lock.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != LockSha256) throw new InvalidDataException("gift-egg-species_lock_bytes");
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("gift-egg-species_source_identity");
        foreach (var input in inputs.GetProperty("inputs").EnumerateArray())
        {
            var path = input.GetProperty("localPath").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(input.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString() ||
                Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != input.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("gift-egg-species_source_bytes:" + path);
        }
        foreach (var input in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString())
                throw new InvalidDataException("gift-egg-species_dependency_bytes:" + path);
        }
        using var locked = JsonDocument.Parse(lockBytes);
        var importer = File.ReadAllBytes(Path.Combine(root, "scripts/import-gift-egg-species.py"));
        if (Convert.ToHexStringLower(SHA256.HashData(importer)) != locked.RootElement.GetProperty("importerSha256").GetString())
            throw new InvalidDataException("gift-egg-species_importer_bytes");
        _ = LoadPokemonDocument(root); _ = LoadMachinesDocument(root);
    }
}
