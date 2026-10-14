using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Seven breeding babies and Tyrogue's Hitmontop branch over the exact 174-species owner.</summary>
public sealed class BreedingOwnedPokemonCatalog
{
    public const string Profile = "firered-breeding-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_PICHU"), new("pret:SPECIES_CLEFFA"), new("pret:SPECIES_IGGLYBUFF"), new("pret:SPECIES_TYROGUE"),
         new("pret:SPECIES_SMOOCHUM"), new("pret:SPECIES_ELEKID"), new("pret:SPECIES_WYNAUT"), new("pret:SPECIES_HITMONTOP")];
    private static readonly int[] Numbers = [172, 173, 174, 236, 238, 239, 360, 237];
    private static readonly ConditionalWeakTable<PokemonCatalog, BreedingOwnedPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<GameCornerOwnedPokemonCatalog, ImmutableDictionary<SpeciesId, PokemonSpeciesDefinition>> Definitions = new();
    public GameCornerOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    public OwnedBattlePokemonCatalog SharedBattle => OwnedBattlePokemonCatalog.For(Catalog)!;

    private BreedingOwnedPokemonCatalog(GameCornerOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }

    public static BreedingOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;

    public static BreedingOwnedPokemonCatalog Authenticate(GameCornerOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(GameCornerOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 174 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 182 ||
            predecessor.Catalog.Moves.Count != 354 || !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(pair => !candidate.Species.TryGetValue(pair.Key, out var value) || !ReferenceEquals(pair.Value, value)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("breeding_owned_scope");
        foreach (var id in AdditionalSpecies)
            if (!ReferenceEquals(candidate.Species[id], ExpectedSpecies(predecessor, id)))
                throw new ArgumentException("breeding_owned_source");
        var proof = new BreedingOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof);
        OwnedBattlePokemonCatalog.RegisterSuccessor(proof.Catalog, predecessor.SharedBattle, Profile);
        return proof;
    }

    public static PokemonSpeciesDefinition ExpectedSpecies(GameCornerOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(GameCornerOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || !AdditionalSpecies.Contains(id))
            throw new ArgumentException("breeding_owned_species");
        return Definitions.GetValue(predecessor, CreateDefinitions)[id];
    }

    private static ImmutableDictionary<SpeciesId, PokemonSpeciesDefinition> CreateDefinitions(GameCornerOwnedPokemonCatalog predecessor)
    {
        var definitions = ImmutableDictionary.CreateBuilder<SpeciesId, PokemonSpeciesDefinition>();
        for (int index = 0; index < AdditionalSpecies.Length; index++)
        {
            var id = AdditionalSpecies[index];
            var source = predecessor.Mechanics.Catalog.Species[id];
            var raw = predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions;
            if (source.SourceSpeciesId != Numbers[index] || source.Evolutions.IsDefault || !source.Evolutions.IsEmpty || raw.IsDefault)
                throw new ArgumentException("breeding_owned_canonical_source");
            var targets = predecessor.Catalog.Species.Keys.Concat(AdditionalSpecies).ToImmutableHashSet();
            var evolutions = SourceLevelEvolutionProjection.Project(predecessor.Mechanics, id, targets);
            if (evolutions.Length != raw.Length) throw new ArgumentException("breeding_owned_evolution_projection");
            definitions.Add(id, source with { Evolutions = evolutions });
        }
        return definitions.ToImmutable();
    }
}
