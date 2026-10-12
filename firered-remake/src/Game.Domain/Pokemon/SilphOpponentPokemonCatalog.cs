using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Two source trainer-only species. This proof never grants permanent ownership.</summary>
public sealed class SilphOpponentPokemonCatalog
{
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_ALAKAZAM"), new("pret:SPECIES_MR_MIME")];
    private static readonly ConditionalWeakTable<PokemonCatalog, SilphOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<SilphOwnedPokemonCatalog, SilphOpponentPokemonCatalog> Owners = new();
    public SilphOwnedPokemonCatalog Owned { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public ImmutableDictionary<SpeciesId, PokemonSpeciesDefinition> Species => Catalog.Species;
    private SilphOpponentPokemonCatalog(SilphOwnedPokemonCatalog owned, PokemonCatalog catalog)
    { Owned = owned; Catalog = catalog; }
    public static SilphOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SilphOpponentPokemonCatalog? For(SilphOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    public static SilphOpponentPokemonCatalog Authenticate(SilphOwnedPokemonCatalog owned,
        ImmutableArray<PokemonSpeciesDefinition> opponentSpecies)
    {
        ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(SilphOwnedPokemonCatalog.For(owned.Catalog), owned) || opponentSpecies.IsDefault || opponentSpecies.Length != 2 ||
            !opponentSpecies.Select(s => s?.Id).SequenceEqual(AdditionalSpecies.Select(s => (SpeciesId?)s)) ||
            opponentSpecies.Any(s => !SilphOwnedPokemonCatalog.MatchesSource(s, owned.Mechanics.Catalog.Species[s.Id])))
            throw new ArgumentException("silph_opponent_source");
        var proof = new SilphOpponentPokemonCatalog(owned, owned.Catalog with {
            Species = owned.Catalog.Species.AddRange(opponentSpecies.Select(s => KeyValuePair.Create(s.Id, s))) });
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof); return proof;
    }
}
