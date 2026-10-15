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
public sealed record NpcTradeMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount, int MachineCount,
    ImmutableArray<NpcTradeMachineRows> Additions, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Thin, source-authenticated Mr. Mime and Jynx ownership and machine additions; shared raw sources remain in their existing package.</summary>
public static class PretNpcTradeOwnedRules
{

    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/npc-trade-owned-rules", file));
        return JsonSerializer.Deserialize<T>(bytes, SharedMachineCatalog.Options(root, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        })) ?? throw new InvalidDataException("npc_trade_owned_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root,"pokemon.json");
        if (            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 159 ||
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
        var document = Read<NpcTradeMachinesDocument>(root,"machines.json");
        if (            document.PreviousSpeciesCount != 159 || document.SpeciesCount != 161 || document.MachineCount != 41 ||
            document.Additions.IsDefault || document.Additions.Length != 41 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty )
            throw new InvalidDataException("npc_trade_owned_machines_scope");
        return document;
    }

    public static NpcTradeMachineCatalog LoadMachines(string root, FossilLabMachineCatalog predecessor, NpcTradeOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog; var additions = document.Additions.ToDictionary(m => m.Item);
        return NpcTradeMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = NpcTradeMachineCatalog.Profile,Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }
}
