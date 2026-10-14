using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Original Water Labyrinth Togepi and its friendship evolution above the Lost Cave owner.</summary>
public sealed class GiftEggOwnedPokemonCatalog
{
    public const string Profile = "firered-gift-egg-owned-pokemon-v1";
    public const string OpponentProfile = "firered-gift-egg-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_TOGEPI"), new("pret:SPECIES_TOGETIC")];
    private static readonly ConditionalWeakTable<PokemonCatalog, GiftEggOwnedPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<LostCaveOwnedPokemonCatalog, GiftEggOwnedPokemonCatalog> Owners = new();
    public LostCaveOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public PokemonCatalog OpponentCatalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    public OwnedBattlePokemonCatalog SharedBattle => OwnedBattlePokemonCatalog.For(Catalog)!;
    private GiftEggOwnedPokemonCatalog(LostCaveOwnedPokemonCatalog predecessor)
    {
        Predecessor = predecessor;
        var targets = predecessor.Catalog.Species.Keys.Concat(AdditionalSpecies).ToImmutableHashSet();
        var species = predecessor.Catalog.Species;
        foreach (var id in AdditionalSpecies)
        {
            var canonical = predecessor.Mechanics.Catalog.Species[id];
            var evolutions = SourceLevelEvolutionProjection.Project(predecessor.Mechanics, id, targets);
            if (canonical.SourceSpeciesId != (id == AdditionalSpecies[0] ? 175 : 176) || !canonical.Evolutions.IsEmpty ||
                evolutions.Length != (id == AdditionalSpecies[0] ? 1 : 0) ||
                id == AdditionalSpecies[0] && (evolutions[0].Target != AdditionalSpecies[1] ||
                    evolutions[0].Method != PokemonEvolutionMethod.Friendship || evolutions[0].Level != 0))
                throw new ArgumentException("gift_egg_species_source");
            species = species.Add(id, canonical with { Evolutions = evolutions });
        }
        Catalog = predecessor.Catalog with { Species = species };
        OpponentCatalog = predecessor.OpponentCatalog with { Species = predecessor.OpponentCatalog.Species.AddRange(
            AdditionalSpecies.Select(id => KeyValuePair.Create(id, Catalog.Species[id]))) };
    }
    public static GiftEggOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static GiftEggOwnedPokemonCatalog Authenticate(LostCaveOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(LostCaveOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            predecessor.Catalog.Species.Count != 187 || predecessor.OpponentCatalog.Species.Count != 210 ||
            predecessor.Catalog.Moves.Count != 354 || !ReferenceEquals(predecessor.Catalog.Moves, predecessor.OpponentCatalog.Moves) ||
            AdditionalSpecies.Any(id => predecessor.Catalog.Species.ContainsKey(id) || predecessor.OpponentCatalog.Species.ContainsKey(id)))
            throw new ArgumentException("gift_egg_owned_predecessor");
        if (Owners.TryGetValue(predecessor, out var cached)) return cached;
        var proof = new GiftEggOwnedPokemonCatalog(predecessor);
        Proofs.Add(proof.Catalog, proof); Owners.Add(predecessor, proof);
        OwnedBattlePokemonCatalog.RegisterSuccessor(proof.Catalog, predecessor.SharedBattle, Profile);
        OwnedBattlePokemonCatalog.BindSuccessorOpponents(proof.SharedBattle, proof.OpponentCatalog, OpponentProfile);
        return proof;
    }
}
