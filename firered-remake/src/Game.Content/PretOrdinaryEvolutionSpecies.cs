using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record OrdinaryEvolutionMetadataDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    ImmutableArray<SourceEggSpeciesRow> Additions);
public sealed record OrdinaryEvolutionMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record OrdinaryEvolutionMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount, int MachineCount,
    ImmutableArray<OrdinaryEvolutionMachineRows> Additions, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Four original ordinary evolution targets, existing-machine compatibility and additive breeding metadata.</summary>
public static class PretOrdinaryEvolutionSpecies
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/ordinary-evolution-species", file));
        return JsonSerializer.Deserialize<T>(bytes, SharedMachineCatalog.Options(root, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        })) ?? throw new InvalidDataException("ordinary-evolution-species_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root,"pokemon.json");
        if (            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 182 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 186 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 203 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 204 ||
            !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(OrdinaryEvolutionOpponentPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(OrdinaryEvolutionOwnedPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) is false ||
            document.GetProperty("evolutions").GetArrayLength() != 4 || document.GetProperty("movesAdded").GetArrayLength() != 0)
            throw new InvalidDataException("ordinary-evolution-species_pokemon_scope");
        return document;
    }

    public static OrdinaryEvolutionOwnedPokemonCatalog LoadOwned(string root, BreedingOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in OrdinaryEvolutionOwnedPokemonCatalog.AdditionalSpecies.Concat(OrdinaryEvolutionOwnedPokemonCatalog.UpdatedParents))
            species = species.SetItem(id, OrdinaryEvolutionOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return OrdinaryEvolutionOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Species = species
        });
    }

    public static OrdinaryEvolutionOpponentPokemonCatalog LoadOpponents(string root, BreedingOpponentPokemonCatalog predecessor,
        OrdinaryEvolutionOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return OrdinaryEvolutionOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static OrdinaryEvolutionMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<OrdinaryEvolutionMachinesDocument>(root,"machines.json");
        if (            document.PreviousSpeciesCount != 182 || document.SpeciesCount != 186 || document.MachineCount != 51 ||
            document.Additions.IsDefault || document.Additions.Length != 51 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty )
            throw new InvalidDataException("ordinary-evolution-species_machines_scope");
        return document;
    }

    public static OrdinaryEvolutionMachineCatalog LoadMachines(string root, PewterMachineCatalog predecessor, OrdinaryEvolutionOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor; var additions = document.Additions.ToDictionary(m => m.Item);
        return OrdinaryEvolutionMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = OrdinaryEvolutionMachineCatalog.Profile,Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static BreedingSpeciesMetadataCatalog LoadMetadata(string root, OrdinaryEvolutionOwnedPokemonCatalog owned)
    {
        var document = Read<OrdinaryEvolutionMetadataDocument>(root,"metadata.json");
        if (            document.PreviousSpeciesCount != 182 || document.SpeciesCount != 186 )
            throw new InvalidDataException("ordinary_evolution_metadata_scope");
        return OrdinaryEvolutionSpeciesMetadataCatalog.Authenticate(owned, document.Additions);
    }
}
