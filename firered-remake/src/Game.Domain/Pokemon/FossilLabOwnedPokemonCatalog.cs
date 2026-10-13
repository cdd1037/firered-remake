using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>The two Mt. Moon fossil families and their level-40 evolutions over the exact Dojo owner.</summary>
public sealed class FossilLabOwnedPokemonCatalog
{
    public const string Profile = "firered-fossil-lab-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_OMANYTE"), new("pret:SPECIES_OMASTAR"), new("pret:SPECIES_KABUTO"), new("pret:SPECIES_KABUTOPS")];
    private static readonly ConditionalWeakTable<PokemonCatalog, FossilLabOwnedPokemonCatalog> Proofs = new();
    public FightingDojoOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    public OwnedBattlePokemonCatalog SharedBattle => OwnedBattlePokemonCatalog.For(Catalog)!;
    private FossilLabOwnedPokemonCatalog(FightingDojoOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static FossilLabOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static FossilLabOwnedPokemonCatalog Authenticate(FightingDojoOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(FightingDojoOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 154 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 158 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var value) || !ReferenceEquals(p.Value, value)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("fossil_lab_owned_scope");
        foreach (var id in AdditionalSpecies)
            if (!SilphOwnedPokemonCatalog.MatchesSource(candidate.Species[id], ExpectedSpecies(predecessor, id)))
                throw new ArgumentException("fossil_lab_owned_source");
        var proof = new FossilLabOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof);
        OwnedBattlePokemonCatalog.RegisterSuccessor(proof.Catalog, predecessor.SharedBattle, Profile);
        return proof;
    }
    public static PokemonSpeciesDefinition ExpectedSpecies(FightingDojoOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(FightingDojoOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || !AdditionalSpecies.Contains(id))
            throw new ArgumentException("fossil_lab_owned_species");
        var expected = predecessor.Mechanics.Catalog.Species[id];
        var raw = predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions;
        if (expected.Evolutions.IsDefault || !expected.Evolutions.IsEmpty)
            throw new ArgumentException("fossil_lab_canonical_evolution_source");
        var target = id.Value switch {
            "pret:SPECIES_OMANYTE" => AdditionalSpecies[1],
            "pret:SPECIES_KABUTO" => AdditionalSpecies[3], _ => default(SpeciesId) };
        if (id == AdditionalSpecies[0] || id == AdditionalSpecies[2])
        {
            if (raw.IsDefault || raw.Length != 1 || raw[0].Method != "EVO_LEVEL" || raw[0].SourceMethodId != 4 ||
                raw[0].Parameter != "40" || raw[0].ParameterValue != 40 || raw[0].Target != target)
                throw new ArgumentException("fossil_lab_owned_evolution_source");
            return expected with { Evolutions = [new(40, target, raw[0].Source)] };
        }
        if (raw.IsDefault || !raw.IsEmpty) throw new ArgumentException("fossil_lab_owned_terminal_source");
        return expected;
    }
}
