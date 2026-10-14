using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Four original normal evolution closures above the exact breeding owner.</summary>
public sealed class OrdinaryEvolutionOwnedPokemonCatalog
{
    public const string Profile = "firered-ordinary-evolution-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_RHYDON"), new("pret:SPECIES_SLOWBRO"), new("pret:SPECIES_CROBAT"), new("pret:SPECIES_BLISSEY")];
    public static ImmutableArray<SpeciesId> UpdatedParents { get; } =
        [new("pret:SPECIES_RHYHORN"), new("pret:SPECIES_SLOWPOKE"), new("pret:SPECIES_GOLBAT"), new("pret:SPECIES_CHANSEY")];
    private static readonly int[] Numbers = [112, 80, 169, 242];
    private static readonly int[] Levels = [42, 37, 0, 0];
    private static readonly ConditionalWeakTable<PokemonCatalog, OrdinaryEvolutionOwnedPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<BreedingOwnedPokemonCatalog, ImmutableDictionary<SpeciesId, PokemonSpeciesDefinition>> Definitions = new();
    public BreedingOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    public OwnedBattlePokemonCatalog SharedBattle => OwnedBattlePokemonCatalog.For(Catalog)!;
    private OrdinaryEvolutionOwnedPokemonCatalog(BreedingOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static OrdinaryEvolutionOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;

    public static OrdinaryEvolutionOwnedPokemonCatalog Authenticate(BreedingOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(BreedingOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 182 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 186 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) || predecessor.Catalog.Moves.Count != 354 ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var value) ||
                !ReferenceEquals(value, UpdatedParents.Contains(p.Key) ? ExpectedSpecies(predecessor, p.Key) : p.Value)))
            throw new ArgumentException("ordinary_evolution_owned_scope");
        foreach (var id in AdditionalSpecies)
            if (!ReferenceEquals(candidate.Species[id], ExpectedSpecies(predecessor, id)))
                throw new ArgumentException("ordinary_evolution_owned_source");
        var proof = new OrdinaryEvolutionOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof);
        OwnedBattlePokemonCatalog.RegisterEvolutionSuccessor(proof.Catalog, predecessor.SharedBattle, Profile);
        return proof;
    }
    public static PokemonSpeciesDefinition ExpectedSpecies(BreedingOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(BreedingOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !AdditionalSpecies.Contains(id) && !UpdatedParents.Contains(id)) throw new ArgumentException("ordinary_evolution_owned_species");
        return Definitions.GetValue(predecessor, CreateDefinitions)[id];
    }
    private static ImmutableDictionary<SpeciesId, PokemonSpeciesDefinition> CreateDefinitions(BreedingOwnedPokemonCatalog predecessor)
    {
        var definitions = ImmutableDictionary.CreateBuilder<SpeciesId, PokemonSpeciesDefinition>();
        var targets = predecessor.Catalog.Species.Keys.Concat(AdditionalSpecies).ToImmutableHashSet();
        for (int i = 0; i < AdditionalSpecies.Length; i++)
        {
            var id = AdditionalSpecies[i]; var parent = UpdatedParents[i];
            var source = predecessor.Mechanics.Catalog.Species[id];
            if (source.SourceSpeciesId != Numbers[i] || source.Evolutions.IsDefault || !source.Evolutions.IsEmpty ||
                !predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions.IsEmpty ||
                !predecessor.Catalog.Species.TryGetValue(parent, out var before) || !before.Evolutions.IsEmpty)
                throw new ArgumentException("ordinary_evolution_terminal_source");
            var edges = SourceLevelEvolutionProjection.Project(predecessor.Mechanics, parent, targets);
            if (edges.Length != 1 || edges[0].Target != id || edges[0].Level != Levels[i] ||
                edges[0].Method != (i < 2 ? PokemonEvolutionMethod.Level : PokemonEvolutionMethod.Friendship))
                throw new ArgumentException("ordinary_evolution_original_parent_edge");
            definitions.Add(id, source);
            definitions.Add(parent, before with { Evolutions = edges });
        }
        return definitions.ToImmutable();
    }
}
