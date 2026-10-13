using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Retains the current 163 definitions and adds five trainer-only species. Steelix is already inherited.</summary>
public sealed class LeagueRematchOpponentPokemonCatalog
{
    public const string Profile = "firered-league-rematch-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_CROBAT"), new("pret:SPECIES_MISDREAVUS"), new("pret:SPECIES_HERACROSS"),
         new("pret:SPECIES_KINGDRA"), new("pret:SPECIES_TYRANITAR")];
    private static readonly ConditionalWeakTable<PokemonCatalog, LeagueRematchOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<CeruleanCaveOwnedPokemonCatalog, LeagueRematchOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, LeagueRematchOpponentPokemonCatalog> MechanicsProofs = new();
    public CeruleanCaveOpponentPokemonCatalog Predecessor { get; }
    public CeruleanCaveOwnedPokemonCatalog Owned => Predecessor.Owned;
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private LeagueRematchOpponentPokemonCatalog(CeruleanCaveOpponentPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static LeagueRematchOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static LeagueRematchOpponentPokemonCatalog? For(CeruleanCaveOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static LeagueRematchOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;

    public static LeagueRematchOpponentPokemonCatalog Authenticate(CeruleanCaveOpponentPokemonCatalog predecessor,
        ImmutableArray<PokemonSpeciesDefinition> opponentSpecies)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(CeruleanCaveOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            predecessor.Owned.Catalog.Species.Count != 139 || predecessor.Catalog.Species.Count != 163 ||
            opponentSpecies.IsDefault || opponentSpecies.Length != AdditionalSpecies.Length ||
            !opponentSpecies.Select(s => s?.Id).SequenceEqual(AdditionalSpecies.Select(s => (SpeciesId?)s)) ||
            opponentSpecies.Any(s => !SilphOwnedPokemonCatalog.MatchesSource(s, predecessor.Mechanics.Catalog.Species[s.Id])))
            throw new ArgumentException("league_rematch_opponent_source");
        var species = predecessor.Catalog.Species;
        foreach (var id in AdditionalSpecies)
        {
            if (species.ContainsKey(id) || predecessor.Owned.Catalog.Species.ContainsKey(id))
                throw new ArgumentException("league_rematch_opponent_addition_overlap");
            species = species.Add(id, predecessor.Mechanics.Catalog.Species[id]);
        }
        if (species.Count != 168 || !predecessor.Owned.Catalog.Species.Keys.All(species.ContainsKey))
            throw new ArgumentException("league_rematch_opponent_scope");
        var proof = new LeagueRematchOpponentPokemonCatalog(predecessor, predecessor.Catalog with { Species = species });
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(proof.Owned, _ => proof);
        MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof); return proof;
    }
}
