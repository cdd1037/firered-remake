using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record TrainerTowerSourceMetadata(string SourceCommit, string SourceTree, string Profile,
    int DeclaredTemplates, int DeclaredTrainers, int DeclaredPoolMembers, int ReferencedTemplates,
    int ReferencedTrainers, int ReferencedPoolMembers, int ActualMemberOccurrences, int UsedSpeciesCount,
    int UsedMovesCount, int UsedHeldItemCount, int PreviousOpponentSpeciesCount, int OpponentSpeciesCount,
    int OwnedSpeciesCount, ImmutableArray<string> OwnedSpeciesAdded, ImmutableArray<string> UsedSpecies,
    ImmutableArray<string> UsedMoves, ImmutableArray<string> UsedHeldItems, ImmutableArray<string> NewOpponentSpecies,
    int CreationRandomDrawsPerMember, string LocalSemanticSha256, ImmutableArray<CanonicalPokemonInput> Inputs,
    ImmutableArray<CanonicalPokemonArtifact> DependencyInputs);
public sealed record TrainerTowerRulesDocument(TrainerTowerSourceMetadata Metadata, TrainerTowerLocalData Local);
public sealed record TrainerTowerOpponentDocument(string SourceCommit, string Profile,
    ImmutableArray<string> OwnedSpeciesAdded, ImmutableArray<PokemonSpeciesDefinition> OpponentSpecies);
public sealed record TrainerTowerPrizeDefinition(TrainerTowerChallengeKind Course, ItemId Item, int SourceItemId, int Quantity);
public sealed record TrainerTowerItemsDocument(string SourceCommit, string Profile, int PreviousItemCount, int ItemCount,
    ImmutableArray<EarlyShopItemDefinition> Additions, EarlyShopStock Shop, string AdditionsSemanticSha256,
    string ShopSemanticSha256, ImmutableArray<TrainerTowerPrizeDefinition> Prizes);

/// <summary>Byte-locked local Trainer Tower resources; no external e-Reader profile is admitted.</summary>
public static class PretTrainerTowerRules
{
    public const string CatalogSha256 = "b3c2a390ccf0a427742e410d12f1c5aa5ac0cb77358d3b4254da0df35e0ff72b";
    public const string PokemonSha256 = "47671c6470abf4304cd3b7219577e742b889a00c77b546178d8b80ff8fc49143";
    public const string CreationVectorsSha256 = "2902b09f01da759b6c16abd44baf32d854b3867c2f43d52f7b9dc93900f57f58";
    public const string ItemsSha256 = "2c2c9bcc8f4bb34fc2505794f07c7baeb3094efd10b1836e055e3bf1c3839129";
    public const string LockSha256 = "32ede3d2f71d87c8251e28ca38d4d428e2fa0a201e2f6bcf373b9985061e8b70";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private static byte[] Read(string root, string path, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, path));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("trainer_tower_source_bytes:" + path);
        return bytes;
    }
    private static T Document<T>(string root, string name, string hash) =>
        JsonSerializer.Deserialize<T>(Read(root, "content/generated/trainer-tower-rules/" + name, hash), Options)
            ?? throw new InvalidDataException("trainer_tower_source_missing:" + name);
    public static TrainerTowerRulesDocument LoadDocument(string root)
    {
        var document = Document<TrainerTowerRulesDocument>(root, "catalog.json", CatalogSha256);
        var d = document.Metadata;
        if (d.SourceCommit != PokemonFactory.SourceCommit || d.SourceTree != PretCanonicalPokemonCatalog.SourceTree ||
            d.Profile != TrainerTowerCatalog.Profile || d.DeclaredTemplates != 32 || d.DeclaredTrainers != 60 || d.DeclaredPoolMembers != 360 ||
            d.ReferencedTemplates != 28 || d.ReferencedTrainers != 53 || d.ReferencedPoolMembers != 318 || d.ActualMemberOccurrences != 74 ||
            d.UsedSpeciesCount != 63 || d.UsedMovesCount != 114 || d.UsedHeldItemCount != 23 || d.PreviousOpponentSpeciesCount != 186 ||
            d.OpponentSpeciesCount != 204 || d.OwnedSpeciesCount != 152 || d.OwnedSpeciesAdded.IsDefault || !d.OwnedSpeciesAdded.IsEmpty ||
            d.CreationRandomDrawsPerMember != 0 || d.LocalSemanticSha256 != TrainerTowerCatalog.LocalSemanticSha256 ||
            d.UsedSpecies.IsDefault || d.UsedSpecies.Length != 63 || d.UsedMoves.IsDefault || d.UsedMoves.Length != 114 ||
            d.UsedHeldItems.IsDefault || d.UsedHeldItems.Length != 23 || d.NewOpponentSpecies.IsDefault ||
            !d.NewOpponentSpecies.Select(s => "pret:" + s).SequenceEqual(TrainerTowerOpponentPokemonCatalog.AdditionalSpecies.Select(s => s.Value)) ||
            d.Inputs.IsDefaultOrEmpty || d.DependencyInputs.IsDefault || d.DependencyInputs.Length != 10)
            throw new InvalidDataException("trainer_tower_source_scope");
        _ = TrainerTowerCatalog.Authenticate(document.Local);
        return document;
    }
    public static TrainerTowerCatalog Load(string root) => TrainerTowerCatalog.Authenticate(LoadDocument(root).Local);
    public static TrainerTowerOpponentPokemonCatalog LoadOpponents(string root, PowerPlantOpponentPokemonCatalog predecessor)
    {
        _ = LoadDocument(root);
        var document = Document<TrainerTowerOpponentDocument>(root, "pokemon.json", PokemonSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != TrainerTowerOpponentPokemonCatalog.Profile ||
            document.OwnedSpeciesAdded.IsDefault || !document.OwnedSpeciesAdded.IsEmpty)
            throw new InvalidDataException("trainer_tower_opponent_source");
        return TrainerTowerOpponentPokemonCatalog.Authenticate(predecessor, document.OpponentSpecies);
    }
    public static JsonElement LoadCreationVectors(string root) => Document<JsonElement>(root, "creation-vectors.json", CreationVectorsSha256);
    public static TrainerTowerItemsDocument LoadItemsDocument(string root)
    {
        var d = Document<TrainerTowerItemsDocument>(root, "items.json", ItemsSha256);
        var courses = Load(root).Courses;
        if (d.SourceCommit != PokemonFactory.SourceCommit || d.Profile != TrainerTowerShopCatalog.Profile ||
            d.PreviousItemCount != 123 || d.ItemCount != 124 || d.Additions.IsDefault || d.Additions.Length != 1 ||
            d.AdditionsSemanticSha256 != TrainerTowerShopCatalog.AdditionalItemsSourceSha256 ||
            d.ShopSemanticSha256 != TrainerTowerShopCatalog.LobbyShopSourceSha256 || d.Prizes.IsDefault || d.Prizes.Length != 4 ||
            d.Prizes.Where((p, i) => (int)p.Course != i || p.Item.Value != "pret:" + courses[i].PrizeItem ||
                p.SourceItemId != courses[i].SourcePrizeItemId || p.Quantity != 1).Any())
            throw new InvalidDataException("trainer_tower_items_scope");
        return d;
    }
    public static TrainerTowerShopCatalog LoadShop(string root, PowerPlantShopCatalog predecessor)
    {
        var d = LoadItemsDocument(root);
        return TrainerTowerShopCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = TrainerTowerShopCatalog.Profile, Items = predecessor.Catalog.Items.AddRange(d.Additions),
            Shops = predecessor.Catalog.Shops.Add(d.Shop) });
    }
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        _ = Read(root, "scripts/trainer-tower-rules.lock.json", LockSha256);
        var document = LoadDocument(root);
        _ = Document<TrainerTowerOpponentDocument>(root, "pokemon.json", PokemonSha256);
        _ = LoadCreationVectors(root);
        _ = LoadItemsDocument(root);
        foreach (var i in document.Metadata.Inputs)
        {
            var bytes = Read(root, "content/raw/trainer-tower-rules/" + i.Path, i.Sha256);
            if (bytes.Length != i.ByteLength || Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != i.GitBlobSha1)
                throw new InvalidDataException("trainer_tower_git_blob:" + i.Path);
        }
        foreach (var i in document.Metadata.DependencyInputs)
            if (Read(dependencyRoot ?? root, i.Path, i.Sha256).Length != i.ByteLength)
                throw new InvalidDataException("trainer_tower_dependency:" + i.Path);
    }
}
