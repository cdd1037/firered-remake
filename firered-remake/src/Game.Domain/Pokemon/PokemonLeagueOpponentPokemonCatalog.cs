using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Nine exact trainer-only species, retaining the same owned115 and previous opponent124.</summary>
public sealed class PokemonLeagueOpponentPokemonCatalog
{
    public const string Profile = "firered-pokemon-league-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_MACHAMP"), new("pret:SPECIES_GENGAR"), new("pret:SPECIES_HITMONLEE"), new("pret:SPECIES_HITMONCHAN"), new("pret:SPECIES_RHYDON"), new("pret:SPECIES_JYNX"), new("pret:SPECIES_AERODACTYL"), new("pret:SPECIES_DRAGONAIR"), new("pret:SPECIES_DRAGONITE")];
    private static readonly ConditionalWeakTable<PokemonCatalog, PokemonLeagueOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<CinnabarOwnedPokemonCatalog, PokemonLeagueOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, PokemonLeagueOpponentPokemonCatalog> MechanicsProofs = new();
    public VictoryRoadOpponentPokemonCatalog Predecessor { get; }
    public CinnabarOwnedPokemonCatalog Owned => Predecessor.Owned;
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private PokemonLeagueOpponentPokemonCatalog(VictoryRoadOpponentPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static PokemonLeagueOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static PokemonLeagueOpponentPokemonCatalog? For(CinnabarOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static PokemonLeagueOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static PokemonLeagueOpponentPokemonCatalog Authenticate(VictoryRoadOpponentPokemonCatalog predecessor,
        ImmutableArray<PokemonSpeciesDefinition> opponentSpecies)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(VictoryRoadOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            predecessor.Owned.Catalog.Species.Count != 115 || predecessor.Catalog.Species.Count != 124 ||
            opponentSpecies.IsDefault || opponentSpecies.Length != AdditionalSpecies.Length ||
            !opponentSpecies.Select(s => s?.Id).SequenceEqual(AdditionalSpecies.Select(s => (SpeciesId?)s)) ||
            opponentSpecies.Any(s => !SilphOwnedPokemonCatalog.MatchesSource(s, predecessor.Mechanics.Catalog.Species[s.Id])))
            throw new ArgumentException("pokemon_league_opponent_source");
        var proof = new PokemonLeagueOpponentPokemonCatalog(predecessor, predecessor.Catalog with {
            Species = predecessor.Catalog.Species.AddRange(opponentSpecies.Select(s => KeyValuePair.Create(s.Id, s))) });
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(proof.Owned, _ => proof);
        MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof); return proof;
    }
}
