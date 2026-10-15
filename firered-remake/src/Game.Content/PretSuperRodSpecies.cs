using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SuperRodMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record SuperRodMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount, int MachineCount,
    ImmutableArray<SuperRodMachineRows> Additions, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Source-authenticated Qwilfish ownership and exactly 41 inherited machine rows.</summary>
public static class PretSuperRodSpecies
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/super-rod-species", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("super-rod-species_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root,"pokemon.json");
        if (            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 162 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 163 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 191 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 192 ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(["SPECIES_QWILFISH"]) is false ||
            !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(["SPECIES_QWILFISH"]) || document.GetProperty("evolutions").GetArrayLength() != 0)
            throw new InvalidDataException("super-rod-species_pokemon_scope");
        return document;
    }

    public static SuperRodOwnedPokemonCatalog LoadOwned(string root, SeafoamOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in SuperRodOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, SuperRodOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return SuperRodOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Species = species
        });
    }

    public static SuperRodOpponentPokemonCatalog LoadOpponents(string root, SeafoamOpponentPokemonCatalog predecessor,
        SuperRodOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return SuperRodOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static SuperRodMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<SuperRodMachinesDocument>(root,"machines.json");
        if (            document.PreviousSpeciesCount != 162 || document.SpeciesCount != 163 || document.MachineCount != 41 ||
            document.Additions.IsDefault || document.Additions.Length != 41 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty )
            throw new InvalidDataException("super-rod-species_machines_scope");
        return document;
    }

    public static SuperRodMachineCatalog LoadMachines(string root, SeafoamMachineCatalog predecessor, SuperRodOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog; var additions = document.Additions.ToDictionary(m => m.Item);
        return SuperRodMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = SuperRodMachineCatalog.Profile,Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }
}
