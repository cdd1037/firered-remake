using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record FossilLabMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record FossilLabMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    int MachineCount, ImmutableArray<FossilLabMachineRows> Additions, string AdditionsSemanticSha256, ImmutableArray<ItemId> ItemsAdded);
public sealed record FossilLabShopDocument(string SourceCommit, string Profile, int PreviousItemCount, int ItemCount,
    ImmutableArray<EarlyShopItemDefinition> Additions, string AdditionsSemanticSha256, ImmutableArray<EarlyShopStock> ShopsAdded);

/// <summary>Authenticated three-fossil catalogs and level-five creation evidence over the exact Dojo resources.</summary>
public static class PretFossilLabRules
{
    public const string CatalogSha256 = PokemonSha256;
    public const string ShopSha256 = "3dcbece9abab82ebb607dacd950bbb4ee451bcfea9159c88909d65d241b8840b";
    public const string LockSha256 = "671828fc22bfae36fe3f2318b83a0ae1324cb40ab886e6909643d27d1c9af0dc";
    public const string PokemonSha256 = "3b1fd31e40b0f2b87ef92f392a80552e2b3c5e403308903c53a02982106a3940";
    public const string MachinesSha256 = "c333902b0b61882e21f029ac174f476006259491c6284757726fdc97425f79f3";
    public const string RulesSha256 = "341bf2676f3b42cd6e9cb73288ff498a157ddf43a7c1c59bbf218e9121ffc0bf";
    public const string SourceInputsSha256 = "c5a502b598b541e15dfc6623de69c55e8ef4bda20b52bbce5c4956b793167477";

    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/fossil-lab-rules", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("fossil_lab_source_bytes:" + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("fossil_lab_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree ||
            document.GetProperty("profile").GetString() != FossilLabOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 154 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 159 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 186 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 190 ||
            document.GetProperty("evolutions").GetArrayLength() != 2)
            throw new InvalidDataException("fossil_lab_pokemon_scope");
        return document;
    }

    public static FossilLabOwnedPokemonCatalog LoadOwned(string root, FightingDojoOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in FossilLabOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, FossilLabOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return FossilLabOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }

    public static FossilLabOpponentPokemonCatalog LoadOpponents(string root, FightingDojoOpponentPokemonCatalog predecessor,
        FossilLabOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return FossilLabOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static FossilLabMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<FossilLabMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != FossilLabMachineCatalog.Profile ||
            document.PreviousSpeciesCount != 154 || document.SpeciesCount != 159 || document.MachineCount != 41 ||
            document.Additions.IsDefault || document.Additions.Length != 41 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty ||
            document.AdditionsSemanticSha256 != FossilLabMachineCatalog.InheritedAdditionsSourceSha256)
            throw new InvalidDataException("fossil_lab_machines_scope");
        return document;
    }

    public static FossilLabMachineCatalog LoadMachines(string root, FightingDojoMachineCatalog predecessor, FossilLabOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog; var additions = document.Additions.ToDictionary(m => m.Item);
        return FossilLabMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = FossilLabMachineCatalog.Profile,
            Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static JsonElement LoadRules(string root) => Read<JsonElement>(root, "rules.json", RulesSha256);

    public static FossilLabShopDocument LoadShopDocument(string root)
    {
        var document = Read<FossilLabShopDocument>(root, "shop.json", ShopSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != FossilLabShopCatalog.Profile ||
            document.PreviousItemCount != 124 || document.ItemCount != 125 ||
            document.Additions.IsDefault || document.Additions.Length != 1 || document.Additions[0].Id != FossilLabShopCatalog.OldAmberId ||
            document.AdditionsSemanticSha256 != FossilLabShopCatalog.AdditionalItemsSourceSha256 ||
            document.ShopsAdded.IsDefault || !document.ShopsAdded.IsEmpty)
            throw new InvalidDataException("fossil_lab_shop_scope");
        return document;
    }

    public static FossilLabShopCatalog LoadShop(string root, TrainerTowerShopCatalog predecessor)
    {
        var document = LoadShopDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        return FossilLabShopCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = FossilLabShopCatalog.Profile, Items = predecessor.Catalog.Items.AddRange(document.Additions)
        });
    }

    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var lockBytes = File.ReadAllBytes(Path.Combine(root, "scripts/fossil-lab-rules.lock.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != LockSha256) throw new InvalidDataException("fossil_lab_lock_bytes");
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("fossil_lab_source_identity");
        foreach (var input in inputs.GetProperty("inputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(root, "content/raw/fossil-lab-rules", path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString() ||
                Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != input.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("fossil_lab_source_bytes:" + path);
        }
        foreach (var input in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString())
                throw new InvalidDataException("fossil_lab_dependency_bytes:" + path);
        }
        using var locked = JsonDocument.Parse(lockBytes);
        var importer = File.ReadAllBytes(Path.Combine(root, "scripts/import-fossil-lab-rules.py"));
        if (Convert.ToHexStringLower(SHA256.HashData(importer)) != locked.RootElement.GetProperty("importerSha256").GetString())
            throw new InvalidDataException("fossil_lab_importer_bytes");
        _ = LoadPokemonDocument(root); _ = LoadMachinesDocument(root); _ = LoadRules(root); _ = LoadShopDocument(root);
    }
}
