using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record NpcTradeMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record NpcTradeMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    int MachineCount, ImmutableArray<NpcTradeMachineRows> Additions, string AdditionsSemanticSha256, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Thin, source-authenticated Mr. Mime and Jynx ownership and machine additions; shared raw sources remain in their existing package.</summary>
public static class PretNpcTradeOwnedRules
{
    public const string CatalogSha256 = PokemonSha256;
    public const string PokemonSha256 = "9d0dcf21a51db210326cfd511bfbc3a6276752fc412406e1870ace4cdfb368cd";
    public const string MachinesSha256 = "5c4246cb14d9ca57ffb84e180d801e13225be59fce01feafb6bf1c1802ea9e85";
    public const string SourceInputsSha256 = "b2923cf0ea6277d9b3c8dfe1bbb6d19aa18034a360ffaf9d17840a0780d1e746";
    public const string LockSha256 = "f12ac5424f02db66c72b4345e88ebe2f5f0351dd649b6f5f2352a6357699821d";

    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/npc-trade-owned-rules", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("npc_trade_owned_source_bytes:" + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("npc_trade_owned_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree ||
            document.GetProperty("profile").GetString() != NpcTradeOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.GetProperty("predecessorCatalogSha256").GetString() != PretFossilLabRules.PokemonSha256 ||
            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 159 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 161 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 190 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 190 ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(["SPECIES_MR_MIME", "SPECIES_JYNX"]) is false ||
            document.GetProperty("opponentSpeciesAdded").GetArrayLength() != 0 || document.GetProperty("evolutions").GetArrayLength() != 0)
            throw new InvalidDataException("npc_trade_owned_pokemon_scope");
        return document;
    }

    public static NpcTradeOwnedPokemonCatalog LoadOwned(string root, FossilLabOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in NpcTradeOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, NpcTradeOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return NpcTradeOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Species = species
        });
    }

    public static NpcTradeOpponentPokemonCatalog LoadOpponents(string root, FossilLabOpponentPokemonCatalog predecessor,
        NpcTradeOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return NpcTradeOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static NpcTradeMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<NpcTradeMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != NpcTradeMachineCatalog.Profile ||
            document.PreviousSpeciesCount != 159 || document.SpeciesCount != 161 || document.MachineCount != 41 ||
            document.Additions.IsDefault || document.Additions.Length != 41 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty ||
            document.AdditionsSemanticSha256 != NpcTradeMachineCatalog.InheritedAdditionsSourceSha256)
            throw new InvalidDataException("npc_trade_owned_machines_scope");
        return document;
    }

    public static NpcTradeMachineCatalog LoadMachines(string root, FossilLabMachineCatalog predecessor, NpcTradeOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog; var additions = document.Additions.ToDictionary(m => m.Item);
        return NpcTradeMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = NpcTradeMachineCatalog.Profile,
            Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var lockBytes = File.ReadAllBytes(Path.Combine(root, "scripts/npc-trade-owned-rules.lock.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != LockSha256) throw new InvalidDataException("npc_trade_owned_lock_bytes");
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("npc_trade_owned_source_identity");
        foreach (var input in inputs.GetProperty("inputs").EnumerateArray())
        {
            var path = input.GetProperty("localPath").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString() ||
                Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != input.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("npc_trade_owned_source_bytes:" + path);
        }
        foreach (var input in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString())
                throw new InvalidDataException("npc_trade_owned_dependency_bytes:" + path);
        }
        using var locked = JsonDocument.Parse(lockBytes);
        var importer = File.ReadAllBytes(Path.Combine(root, "scripts/import-npc-trade-owned-rules.py"));
        if (Convert.ToHexStringLower(SHA256.HashData(importer)) != locked.RootElement.GetProperty("importerSha256").GetString())
            throw new InvalidDataException("npc_trade_owned_importer_bytes");
        _ = LoadPokemonDocument(root); _ = LoadMachinesDocument(root);
    }
}
