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

/// <summary>Thin, source-authenticated Mr. Mime ownership and machine additions; shared raw sources remain in their existing package.</summary>
public static class PretNpcTradeOwnedRules
{
    public const string CatalogSha256 = PokemonSha256;
    public const string PokemonSha256 = "fde280564af61126c9db55c59e497a63115a568c63f0d3115b234439514d99b3";
    public const string MachinesSha256 = "15719f61782ea522d69479c15f6870911f319742cbe50df91b86a1eb382278cd";
    public const string SourceInputsSha256 = "b2923cf0ea6277d9b3c8dfe1bbb6d19aa18034a360ffaf9d17840a0780d1e746";
    public const string LockSha256 = "ec3a8ac32c5993d3a8656d3ee65d00227c5d5e9379197c77aac2ae6d004e3d72";

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
            document.GetProperty("ownedSpeciesCount").GetInt32() != 160 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 190 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 190 ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(["SPECIES_MR_MIME"]) is false ||
            document.GetProperty("opponentSpeciesAdded").GetArrayLength() != 0 || document.GetProperty("evolutions").GetArrayLength() != 0)
            throw new InvalidDataException("npc_trade_owned_pokemon_scope");
        return document;
    }

    public static NpcTradeOwnedPokemonCatalog LoadOwned(string root, FossilLabOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var id = NpcTradeOwnedPokemonCatalog.MrMimeId;
        return NpcTradeOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Species = predecessor.Catalog.Species.Add(id, NpcTradeOwnedPokemonCatalog.ExpectedSpecies(predecessor, id))
        });
    }

    public static NpcTradeOpponentPokemonCatalog LoadOpponents(string root, FossilLabOpponentPokemonCatalog predecessor,
        NpcTradeOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return NpcTradeOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static NpcTradeMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<NpcTradeMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != NpcTradeMachineCatalog.Profile ||
            document.PreviousSpeciesCount != 159 || document.SpeciesCount != 160 || document.MachineCount != 41 ||
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
