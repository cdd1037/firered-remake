using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record TrainerTowerSourceMetadata(string SourceCommit, string Profile, int DeclaredTemplates, int DeclaredTrainers, int DeclaredPoolMembers,
    int ReferencedTemplates, int ReferencedTrainers, int ReferencedPoolMembers, int ActualMemberOccurrences,
    int UsedSpeciesCount, int UsedMovesCount, int UsedHeldItemCount, int PreviousOpponentSpeciesCount,
    int OpponentSpeciesCount, int OwnedSpeciesCount, ImmutableArray<string> OwnedSpeciesAdded,
    ImmutableArray<string> UsedSpecies, ImmutableArray<string> UsedMoves,
    ImmutableArray<string> UsedHeldItems, ImmutableArray<string> NewOpponentSpecies,
    int CreationRandomDrawsPerMember, ImmutableArray<CanonicalPokemonInput> Inputs,
    ImmutableArray<CanonicalPokemonArtifact> DependencyInputs);
public sealed record TrainerTowerRulesDocument(TrainerTowerSourceMetadata Metadata, TrainerTowerLocalData Local);
public sealed record TrainerTowerOpponentDocument(string SourceCommit, string Profile,
    ImmutableArray<string> OwnedSpeciesAdded, ImmutableArray<PokemonSpeciesDefinition> OpponentSpecies);
public sealed record TrainerTowerPrizeDefinition(TrainerTowerChallengeKind Course, ItemId Item, int SourceItemId, int Quantity);
public sealed record TrainerTowerItemsDocument(string SourceCommit, string Profile, int PreviousItemCount, int ItemCount,
    ImmutableArray<EarlyShopItemDefinition> Additions, EarlyShopStock Shop,
    ImmutableArray<TrainerTowerPrizeDefinition> Prizes);

/// <summary>Loads the local Trainer Tower courses, opponents and prizes.</summary>
public static class PretTrainerTowerRules
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private static byte[] Read(string root,string path)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, path));
        return bytes;
    }
    private static T Document<T>(string root,string name) =>
        JsonSerializer.Deserialize<T>(Read(root,"content/generated/trainer-tower-rules/" + name), Options)
            ?? throw new InvalidDataException("trainer_tower_source_missing:" + name);
    public static TrainerTowerRulesDocument LoadDocument(string root)
    {
        var document = Document<TrainerTowerRulesDocument>(root,"catalog.json");
        if (document.Metadata is null)
            throw new InvalidDataException("trainer_tower_source_missing:metadata");
        _ = TrainerTowerCatalog.Authenticate(document.Local);
        return document;
    }
    public static TrainerTowerCatalog Load(string root) => TrainerTowerCatalog.Authenticate(LoadDocument(root).Local);
    public static JsonElement LoadCreationVectors(string root) => Document<JsonElement>(root,"creation-vectors.json");
    public static TrainerTowerItemsDocument LoadItemsDocument(string root)
    {
        var d = Document<TrainerTowerItemsDocument>(root,"items.json");
        var courses = Load(root).Courses;
        if (d.Additions.IsDefaultOrEmpty || d.Prizes.IsDefault || d.Prizes.Length != courses.Length ||
            d.Prizes.Where((p, i) => (int)p.Course != i || p.Item.Value != "pret:" + courses[i].PrizeItem ||
                p.SourceItemId != courses[i].SourcePrizeItemId || p.Quantity != 1).Any())
            throw new InvalidDataException("trainer_tower_items_scope");
        return d;
    }

}
