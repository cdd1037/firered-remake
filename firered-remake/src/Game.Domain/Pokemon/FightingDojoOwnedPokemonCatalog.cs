using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
namespace Game.Domain.Pokemon;

/// <summary>The two original Dojo gifts over the exact Power Plant predecessor.</summary>
public sealed class FightingDojoOwnedPokemonCatalog
{
    public const string Profile = "firered-fighting-dojo-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_HITMONLEE"), new("pret:SPECIES_HITMONCHAN")];
    private static readonly ConditionalWeakTable<PokemonCatalog, FightingDojoOwnedPokemonCatalog> Proofs = new();
    public PowerPlantOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    public OwnedBattlePokemonCatalog SharedBattle => OwnedBattlePokemonCatalog.For(Catalog)!;
    private FightingDojoOwnedPokemonCatalog(PowerPlantOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static FightingDojoOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static FightingDojoOwnedPokemonCatalog Authenticate(PowerPlantOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(PowerPlantOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 152 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 154 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var value) || !ReferenceEquals(p.Value, value)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("fighting_dojo_owned_scope");
        foreach (var id in AdditionalSpecies)
            if (!ReferenceEquals(candidate.Species[id], ExpectedSpecies(predecessor, id)))
                throw new ArgumentException("fighting_dojo_owned_source");
        var proof = new FightingDojoOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof);
        OwnedBattlePokemonCatalog.RegisterSuccessor(proof.Catalog, predecessor.SharedBattle, Profile);
        return proof;
    }
    public static PokemonSpeciesDefinition ExpectedSpecies(PowerPlantOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(PowerPlantOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || !AdditionalSpecies.Contains(id))
            throw new ArgumentException("fighting_dojo_owned_species");
        var source = predecessor.Mechanics.Catalog.Species[id];
        if (!predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions.IsEmpty || !source.Evolutions.IsEmpty)
            throw new ArgumentException("fighting_dojo_owned_terminal_source");
        return source;
    }
}
