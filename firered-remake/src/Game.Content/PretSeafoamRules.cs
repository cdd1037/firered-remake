using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.Encounters;

namespace Game.Content;

public sealed record SeafoamTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);
public sealed record SeafoamMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record SeafoamMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount, int MachineCount,
    ImmutableArray<SeafoamMachineRows> Additions, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Thin source-authenticated Articuno ownership, machines and original Route19/20 trainer teams.</summary>
public static class PretSeafoamRules
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/seafoam-rules", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("seafoam_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root,"pokemon.json");
        if (            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 161 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 162 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 190 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 191 ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(["SPECIES_ARTICUNO"]) is false ||
            !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(["SPECIES_ARTICUNO"]) || document.GetProperty("evolutions").GetArrayLength() != 0)
            throw new InvalidDataException("seafoam_pokemon_scope");
        return document;
    }

    public static SeafoamOwnedPokemonCatalog LoadOwned(string root, NpcTradeOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in SeafoamOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, SeafoamOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return SeafoamOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Species = species
        });
    }

    public static SeafoamOpponentPokemonCatalog LoadOpponents(string root, NpcTradeOpponentPokemonCatalog predecessor,
        SeafoamOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return SeafoamOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static SeafoamMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<SeafoamMachinesDocument>(root,"machines.json");
        if (            document.PreviousSpeciesCount != 161 || document.SpeciesCount != 162 || document.MachineCount != 41 ||
            document.Additions.IsDefault || document.Additions.Length != 41 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty )
            throw new InvalidDataException("seafoam_machines_scope");
        return document;
    }

    public static SeafoamMachineCatalog LoadMachines(string root, NpcTradeMachineCatalog predecessor, SeafoamOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog; var additions = document.Additions.ToDictionary(m => m.Item);
        return SeafoamMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = SeafoamMachineCatalog.Profile,Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static SeafoamTrainerDocument LoadDocument(string root)
    {
        var document = Read<SeafoamTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document); return document;
    }
    public static SeafoamTrainerCatalog LoadTrainers(string root) => LoadTrainers(LoadDocument(root));
    private static SeafoamTrainerCatalog LoadTrainers(SeafoamTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty || document.Trainers.Sum(t => t.Party.Length) != 57)
            throw new InvalidDataException("seafoam_trainers_missing");
        var result = new SeafoamTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        SeafoamTrainerPartyFactory.ValidateCatalog(result); return result;
    }
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root,"rules.json");
    public static SeafoamWaterEncounterCatalog LoadWater(SevenIslandWaterEncounterCatalog predecessor) =>
        SeafoamWaterEncounterCatalog.Authenticate(predecessor, SeafoamEncounterTables.Water);
}
