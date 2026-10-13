using System.Runtime.CompilerServices;

namespace Game.Domain.Pokemon;

/// <summary>League133 plus owned118 gives135 unique species. Rapidash is promoted, not counted twice.</summary>
public sealed class SeviiRubyOpponentPokemonCatalog
{
    public const string Profile = "firered-sevii-ruby-opponents-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, SeviiRubyOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<SeviiRubyOwnedPokemonCatalog, SeviiRubyOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, SeviiRubyOpponentPokemonCatalog> MechanicsProofs = new();
    public PokemonLeagueOpponentPokemonCatalog Predecessor { get; }
    public SeviiRubyOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private SeviiRubyOpponentPokemonCatalog(PokemonLeagueOpponentPokemonCatalog predecessor,
        SeviiRubyOwnedPokemonCatalog owned, PokemonCatalog catalog)
    { Predecessor = predecessor; Owned = owned; Catalog = catalog; }
    public static SeviiRubyOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SeviiRubyOpponentPokemonCatalog? For(SeviiRubyOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static SeviiRubyOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SeviiRubyOpponentPokemonCatalog Authenticate(PokemonLeagueOpponentPokemonCatalog predecessor, SeviiRubyOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(PokemonLeagueOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(SeviiRubyOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 133 ||
            !ReferenceEquals(predecessor.Catalog.Moves, owned.Catalog.Moves))
            throw new ArgumentException("sevii_ruby_opponent_predecessor");
        var catalog = predecessor.Catalog with { Species = predecessor.Catalog.Species.SetItems(owned.Catalog.Species) };
        if (catalog.Species.Count != 135) throw new ArgumentException("sevii_ruby_opponent_scope");
        var proof = new SeviiRubyOpponentPokemonCatalog(predecessor, owned, catalog);
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof); return proof;
    }
}
