using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Two exact trainer-only species, retaining the same owned115 and previous opponent122.</summary>
public sealed class VictoryRoadOpponentPokemonCatalog
{
    public const string Profile = "firered-victory-road-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_DEWGONG"), new("pret:SPECIES_EXEGGUTOR")];
    private static readonly ConditionalWeakTable<PokemonCatalog, VictoryRoadOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<CinnabarOwnedPokemonCatalog, VictoryRoadOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, VictoryRoadOpponentPokemonCatalog> MechanicsProofs = new();
    public CinnabarOpponentPokemonCatalog Predecessor { get; }
    public CinnabarOwnedPokemonCatalog Owned => Predecessor.Owned;
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private VictoryRoadOpponentPokemonCatalog(CinnabarOpponentPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static VictoryRoadOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static VictoryRoadOpponentPokemonCatalog? For(CinnabarOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static VictoryRoadOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static VictoryRoadOpponentPokemonCatalog Authenticate(CinnabarOpponentPokemonCatalog predecessor,
        ImmutableArray<PokemonSpeciesDefinition> opponentSpecies)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(CinnabarOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            predecessor.Owned.Catalog.Species.Count != 115 || predecessor.Catalog.Species.Count != 122 ||
            opponentSpecies.IsDefault || opponentSpecies.Length != AdditionalSpecies.Length ||
            !opponentSpecies.Select(s => s?.Id).SequenceEqual(AdditionalSpecies.Select(s => (SpeciesId?)s)) ||
            opponentSpecies.Any(s => !SilphOwnedPokemonCatalog.MatchesSource(s, predecessor.Mechanics.Catalog.Species[s.Id])))
            throw new ArgumentException("victory_road_opponent_source");
        var proof = new VictoryRoadOpponentPokemonCatalog(predecessor, predecessor.Catalog with {
            Species = predecessor.Catalog.Species.AddRange(opponentSpecies.Select(s => KeyValuePair.Create(s.Id, s))) });
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(proof.Owned, _ => proof);
        MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof); return proof;
    }
}
