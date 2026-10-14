using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Domain.Pokemon;

/// <summary>One original capturable species over the current ordinary owner; no new move effects.</summary>
public sealed class LostCaveOwnedPokemonCatalog
{
    public const string Profile = "firered-lost-cave-owned-pokemon-v1";
    public const string OpponentProfile = "firered-lost-cave-opponents-v1";
    public static SpeciesId Murkrow { get; } = new("pret:SPECIES_MURKROW");
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } = [Murkrow];
    private static readonly ConditionalWeakTable<PokemonCatalog, LostCaveOwnedPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<VsSeekerOpponentPokemonCatalog, LostCaveOwnedPokemonCatalog> Parents = new();
    public VsSeekerOpponentPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public PokemonCatalog OpponentCatalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    public OwnedBattlePokemonCatalog SharedBattle => OwnedBattlePokemonCatalog.For(Catalog)!;
    private LostCaveOwnedPokemonCatalog(VsSeekerOpponentPokemonCatalog predecessor)
    {
        Predecessor = predecessor;
        var source = predecessor.Mechanics.Catalog.Species[Murkrow];
        Catalog = predecessor.Owned.Catalog with { Species = predecessor.Owned.Catalog.Species.Add(Murkrow, source) };
        OpponentCatalog = predecessor.Catalog with { Species = predecessor.Catalog.Species.Add(Murkrow, source) };
    }
    public static LostCaveOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static LostCaveOwnedPokemonCatalog Authenticate(VsSeekerOpponentPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(OwnedBattlePokemonCatalog.For(predecessor.Owned.Catalog), predecessor.Owned) ||
            OrdinaryEvolutionOwnedPokemonCatalog.For(predecessor.Owned.Catalog) is null ||
            predecessor.Owned.TrainerOpponentProfile != VsSeekerOpponentPokemonCatalog.Profile ||
            predecessor.Owned.Catalog.Species.Count != 186 || predecessor.Catalog.Species.Count != 209 ||
            predecessor.Catalog.Species.ContainsKey(Murkrow) || predecessor.Owned.Catalog.Species.ContainsKey(Murkrow) ||
            !ReferenceEquals(predecessor.Catalog.Moves, predecessor.Owned.Catalog.Moves) || predecessor.Catalog.Moves.Count != 354)
            throw new ArgumentException("lost_cave_owned_predecessor");
        var source = predecessor.Mechanics.Catalog.Species[Murkrow];
        if (source.SourceSpeciesId != 198 || !source.Evolutions.IsEmpty ||
            !predecessor.Mechanics.SpeciesMechanics[Murkrow].SourceEvolutions.IsEmpty)
            throw new ArgumentException("lost_cave_owned_source");
        if (Parents.TryGetValue(predecessor, out var existing)) return existing;
        var proof = new LostCaveOwnedPokemonCatalog(predecessor);
        OwnedBattlePokemonCatalog.RegisterSuccessor(proof.Catalog, predecessor.Owned, Profile);
        OwnedBattlePokemonCatalog.BindSuccessorOpponents(proof.SharedBattle, proof.OpponentCatalog, OpponentProfile);
        Proofs.Add(proof.Catalog, proof); Parents.Add(predecessor, proof); return proof;
    }
}
