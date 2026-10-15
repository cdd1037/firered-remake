using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record StoneEvolutionMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record StoneEvolutionMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount, int MachineCount,
    ImmutableArray<StoneEvolutionMachineRows> Additions, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Source-authenticated seven-target ownership and exactly 42 inherited machine rows.</summary>
public static class PretStoneEvolutionSpecies
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/stone-evolution-species", file));
        return JsonSerializer.Deserialize<T>(bytes, SharedMachineCatalog.Options(root, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        })) ?? throw new InvalidDataException("stone-evolution-species_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root,"pokemon.json");
        if (            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 163 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 170 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 192 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 195 ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(StoneEvolutionOwnedPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) is false ||
            !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(StoneEvolutionOpponentPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) || document.GetProperty("evolutions").GetArrayLength() != 0)
            throw new InvalidDataException("stone-evolution-species_pokemon_scope");
        return document;
    }

    public static StoneEvolutionOwnedPokemonCatalog LoadOwned(string root, SuperRodOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in StoneEvolutionOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, StoneEvolutionOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return StoneEvolutionOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Species = species
        });
    }

    public static StoneEvolutionOpponentPokemonCatalog LoadOpponents(string root, SuperRodOpponentPokemonCatalog predecessor,
        StoneEvolutionOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return StoneEvolutionOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static StoneEvolutionMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<StoneEvolutionMachinesDocument>(root,"machines.json");
        if (            document.PreviousSpeciesCount != 163 || document.SpeciesCount != 170 || document.MachineCount != 42 ||
            document.Additions.IsDefault || document.Additions.Length != 42 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty )
            throw new InvalidDataException("stone-evolution-species_machines_scope");
        return document;
    }

    public static StoneEvolutionMachineCatalog LoadMachines(string root, DepartmentStoreMachineStock predecessor, StoneEvolutionOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Machines; var additions = document.Additions.ToDictionary(m => m.Item);
        return StoneEvolutionMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = StoneEvolutionMachineCatalog.Profile,Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }
}
