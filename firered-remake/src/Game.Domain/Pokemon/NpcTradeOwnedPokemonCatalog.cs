using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Original Reyley trade ownership over the exact 159-species fossil owner.</summary>
public sealed class NpcTradeOwnedPokemonCatalog
{
    public const string Profile = "firered-npc-trade-owned-pokemon-v1";
    public static SpeciesId MrMimeId { get; } = new("pret:SPECIES_MR_MIME");
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } = [MrMimeId];
    private static readonly ConditionalWeakTable<PokemonCatalog, NpcTradeOwnedPokemonCatalog> Proofs = new();
    public FossilLabOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    public OwnedBattlePokemonCatalog SharedBattle => OwnedBattlePokemonCatalog.For(Catalog)!;
    private NpcTradeOwnedPokemonCatalog(FossilLabOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static NpcTradeOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static NpcTradeOwnedPokemonCatalog Authenticate(FossilLabOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(FossilLabOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 159 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 160 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var value) || !ReferenceEquals(p.Value, value)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("npc_trade_owned_scope");
        if (!ReferenceEquals(candidate.Species[MrMimeId], ExpectedSpecies(predecessor, MrMimeId)))
            throw new ArgumentException("npc_trade_owned_source");
        var proof = new NpcTradeOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof);
        OwnedBattlePokemonCatalog.RegisterSuccessor(proof.Catalog, predecessor.SharedBattle, Profile);
        return proof;
    }
    public static PokemonSpeciesDefinition ExpectedSpecies(FossilLabOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(FossilLabOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || id != MrMimeId)
            throw new ArgumentException("npc_trade_owned_species");
        var expected = predecessor.Mechanics.Catalog.Species[id];
        var raw = predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions;
        if (expected.SourceSpeciesId != 122 || expected.Evolutions.IsDefault || !expected.Evolutions.IsEmpty || raw.IsDefault || !raw.IsEmpty)
            throw new ArgumentException("npc_trade_owned_terminal_source");
        return expected;
    }
}
