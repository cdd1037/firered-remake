using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record BreedingMetadataDocument(string SourceCommit, string Profile, int SpeciesCount, ImmutableArray<MoveId> AllMachineMoves,
    ImmutableArray<SourceEggSpeciesRow> Species);
public sealed record BreedingMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record BreedingMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount, int MachineCount,
    ImmutableArray<BreedingMachineRows> Additions, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Eight breeding-family owned species, 51 acquired-machine extensions and original egg inheritance metadata.</summary>
public static class PretBreedingSpecies
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/breeding-species", file));
        return JsonSerializer.Deserialize<T>(bytes, SharedMachineCatalog.Options(root, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        })) ?? throw new InvalidDataException("breeding-species_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root,"pokemon.json");
        if (                        !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(BreedingOpponentPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(BreedingOwnedPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) is false ||
            document.GetProperty("evolutions").GetArrayLength() != 9 || document.GetProperty("movesAdded").GetArrayLength() != 0)
            throw new InvalidDataException("breeding-species_pokemon_scope");
        return document;
    }

    public static BreedingOwnedPokemonCatalog LoadOwned(string root, GameCornerOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in BreedingOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, BreedingOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return BreedingOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Species = species
        });
    }

    public static BreedingOpponentPokemonCatalog LoadOpponents(string root, GameCornerOpponentPokemonCatalog predecessor,
        BreedingOwnedPokemonCatalog owned)
    { return BreedingOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static BreedingMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<BreedingMachinesDocument>(root,"machines.json");
        if (            document.Additions.IsDefault || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty )
            throw new InvalidDataException("breeding-species_machines_scope");
        return document;
    }

    public static BreedingMachineCatalog LoadMachines(string root, PewterMachineCatalog predecessor, BreedingOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor; var additions = document.Additions.ToDictionary(m => m.Item);
        return BreedingMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = BreedingMachineCatalog.Profile,Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static BreedingSpeciesMetadataCatalog LoadMetadata(string root, BreedingOwnedPokemonCatalog owned)
    {
        var document = Read<BreedingMetadataDocument>(root,"metadata.json");
        return BreedingSpeciesMetadataCatalog.Authenticate(owned, document.Species, document.AllMachineMoves);
    }
}
