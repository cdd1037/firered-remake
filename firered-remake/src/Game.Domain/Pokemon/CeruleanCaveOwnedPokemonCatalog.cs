using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>The source-required Mewtwo extension over Celio; inherited species and all moves retain identity.</summary>
public sealed class CeruleanCaveOwnedPokemonCatalog
{
    public const string Profile = "firered-cerulean-cave-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_MEWTWO")];
    private static readonly ConditionalWeakTable<PokemonCatalog, CeruleanCaveOwnedPokemonCatalog> Proofs = new();
    public CelioOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    public OwnedBattlePokemonCatalog SharedBattle => OwnedBattlePokemonCatalog.For(Catalog)!;
    private CeruleanCaveOwnedPokemonCatalog(CelioOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static CeruleanCaveOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static CeruleanCaveOwnedPokemonCatalog Authenticate(CelioOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(CelioOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 138 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 139 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var value) || !ReferenceEquals(p.Value, value)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("cerulean_cave_owned_scope");
        foreach (var id in AdditionalSpecies)
            if (!ReferenceEquals(candidate.Species[id], ExpectedSpecies(predecessor, id)))
                throw new ArgumentException("cerulean_cave_owned_source");
        var proof = new CeruleanCaveOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public static PokemonSpeciesDefinition ExpectedSpecies(CelioOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(CelioOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || !AdditionalSpecies.Contains(id))
            throw new ArgumentException("cerulean_cave_owned_species");
        var expected = predecessor.Mechanics.Catalog.Species[id];
        if (!predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions.IsEmpty || !expected.Evolutions.IsEmpty)
            throw new ArgumentException("cerulean_cave_owned_terminal_evolution");
        return expected;
    }
}
