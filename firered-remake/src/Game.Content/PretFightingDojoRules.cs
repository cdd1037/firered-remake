using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;
namespace Game.Content;

public sealed record FightingDojoTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);
public sealed record FightingDojoMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record FightingDojoMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount, int MachineCount,
    ImmutableArray<FightingDojoMachineRows> Additions, ImmutableArray<ItemId> ItemsAdded);
/// <summary>Bounded Dojo ownership, opponent, trainer and machine catalogs over authenticated Power Plant resources.</summary>
public static class PretFightingDojoRules
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/fighting-dojo-rules", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }) ?? throw new InvalidDataException("fighting_dojo_source_missing");
    }
    public static FightingDojoTrainerDocument LoadDocument(string root)
    {
        var document = Read<FightingDojoTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document); return document;
    }
    public static FightingDojoTrainerCatalog LoadTrainers(string root) => LoadTrainers(LoadDocument(root));
    private static FightingDojoTrainerCatalog LoadTrainers(FightingDojoTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty) throw new InvalidDataException("fighting_dojo_trainers_missing");
        var result = new FightingDojoTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        FightingDojoTrainerPartyFactory.ValidateCatalog(result); return result;
    }
    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root,"pokemon.json");
        return document;
    }
    public static FightingDojoOwnedPokemonCatalog LoadOwned(string root, PowerPlantOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in FightingDojoOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, FightingDojoOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return FightingDojoOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static FightingDojoOpponentPokemonCatalog LoadOpponents(string root, PowerPlantOpponentPokemonCatalog predecessor,
        FightingDojoOwnedPokemonCatalog owned)
    { return FightingDojoOpponentPokemonCatalog.Authenticate(predecessor, owned); }
    public static FightingDojoMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<FightingDojoMachinesDocument>(root,"machines.json");
        if (            document.Additions.IsDefault || document.Additions.Length != 41 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty )
            throw new InvalidDataException("fighting_dojo_machines_scope");
        return document;
    }
    public static FightingDojoMachineCatalog LoadMachines(string root, PowerPlantMachineCatalog predecessor, FightingDojoOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog; var additions = document.Additions.ToDictionary(m => m.Item);
        return FightingDojoMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = FightingDojoMachineCatalog.Profile,Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray() });
    }
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root,"rules.json");
}
