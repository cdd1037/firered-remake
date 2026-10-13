using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Exact owned118 source extension; it does not admit campaign or combat operations.</summary>
public sealed class SeviiRubyOwnedPokemonCatalog
{
    public const string Profile = "firered-sevii-ruby-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_RAPIDASH"), new("pret:SPECIES_SLUGMA"), new("pret:SPECIES_MAGCARGO")];
    public static SpeciesId Ponyta { get; } = new("pret:SPECIES_PONYTA");
    private static readonly ConditionalWeakTable<PokemonCatalog, SeviiRubyOwnedPokemonCatalog> Proofs = new();
    public CinnabarOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    private SeviiRubyOwnedPokemonCatalog(CinnabarOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static SeviiRubyOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;

    public static SeviiRubyOwnedPokemonCatalog Authenticate(CinnabarOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(CinnabarOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 115 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 118 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => p.Key != Ponyta && (!candidate.Species.TryGetValue(p.Key, out var value) || !ReferenceEquals(p.Value, value))) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("sevii_ruby_owned_scope");
        foreach (var id in AdditionalSpecies.Prepend(Ponyta))
        {
            var expected = ExpectedSpecies(predecessor, id);
            if (!SilphOwnedPokemonCatalog.MatchesSource(candidate.Species[id], expected))
                throw new ArgumentException("sevii_ruby_owned_source");
        }
        var proof = new SeviiRubyOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof); return proof;
    }

    /// <summary>Exact source projection, including only Ponyta40 and Slugma38 level evolution edges.</summary>
    public static PokemonSpeciesDefinition ExpectedSpecies(CinnabarOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(CinnabarOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            (id != Ponyta && !AdditionalSpecies.Contains(id))) throw new ArgumentException("sevii_ruby_owned_species");
        var expected = predecessor.Mechanics.Catalog.Species[id];
        var raw = predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions;
        ImmutableArray<PokemonEvolution> evolutions = [];
        if (id == Ponyta || id == AdditionalSpecies[1])
        {
            var level = id == Ponyta ? 40 : 38;
            var target = id == Ponyta ? AdditionalSpecies[0] : AdditionalSpecies[2];
            if (raw.Length != 1 || raw[0].Method != "EVO_LEVEL" || raw[0].SourceMethodId != 4 ||
                raw[0].Parameter != level.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                raw[0].ParameterValue != level || raw[0].Target != target)
                throw new ArgumentException("sevii_ruby_owned_evolution_source");
            evolutions = [new(level, target, raw[0].Source)];
        }
        else if (!raw.IsEmpty) throw new ArgumentException("sevii_ruby_owned_terminal_evolution");
        return expected with { Evolutions = evolutions };
    }
}
