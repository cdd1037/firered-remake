using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record GiftEggMetadataDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    ImmutableArray<SourceEggSpeciesRow> Additions);
public sealed record GiftEggMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record GiftEggMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount, int MachineCount,
    ImmutableArray<GiftEggMachineRows> Additions, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Original gift Togepi and Togetic, existing-machine compatibility and additive breeding metadata.</summary>
public static class PretGiftEggSpecies
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/gift-egg-species", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("gift-egg-species_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root,"pokemon.json");
        if (                        !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(GiftEggOwnedPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(GiftEggOwnedPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) is false ||
            document.GetProperty("evolutions").GetArrayLength() != 1 || document.GetProperty("movesAdded").GetArrayLength() != 0)
            throw new InvalidDataException("gift-egg-species_pokemon_scope");
        return document;
    }

    public static GiftEggOwnedPokemonCatalog LoadOwned(string root, LostCaveOwnedPokemonCatalog predecessor)
    {
        return GiftEggOwnedPokemonCatalog.Authenticate(predecessor);
    }

    public static GiftEggMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<GiftEggMachinesDocument>(root,"machines.json");
        if (            document.Additions.IsDefault || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty )
            throw new InvalidDataException("gift-egg-species_machines_scope");
        return document;
    }

    public static GiftEggMachineCatalog LoadMachines(string root, PewterMachineCatalog predecessor, GiftEggOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor; var additions = document.Additions.ToDictionary(m => m.Item);
        return GiftEggMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = GiftEggMachineCatalog.Profile,Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static BreedingSpeciesMetadataCatalog LoadMetadata(string root, GiftEggOwnedPokemonCatalog owned)
    {
        var document = Read<GiftEggMetadataDocument>(root,"metadata.json");
        return GiftEggSpeciesMetadataCatalog.Authenticate(owned, document.Additions);
    }
}
