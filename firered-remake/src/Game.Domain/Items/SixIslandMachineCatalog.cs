using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Thirty-eight original machines for the exact 132 owned species. All inherited source cells retain identity.</summary>
public sealed class SixIslandMachineCatalog
{
    public const string Profile = "firered-six-island-teaching-v1";
    public const string InheritedAdditionsSourceSha256 = "39e365934481eaa27f533de3dc0ddb43c909bc5b0c37e6afa38713b0612841b8";
    private static readonly ConditionalWeakTable<PewterMachineCatalog, SixIslandMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public FourIslandMachineCatalog Predecessor { get; }
    public SixIslandOwnedPokemonCatalog Pokemon { get; }
    private SixIslandMachineCatalog(PewterMachineCatalog catalog, FourIslandMachineCatalog predecessor, SixIslandOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static SixIslandMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static SixIslandMachineCatalog Authenticate(FourIslandMachineCatalog predecessor,
        SixIslandOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(FourIslandMachineCatalog.For(old), predecessor) || !ReferenceEquals(predecessor.Pokemon, pokemon.Predecessor) ||
            !ReferenceEquals(SixIslandOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 37 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 37 } inherited ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("six_island_machine_predecessor");
        var additions = new List<(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility)>();
        void Rows(ItemId item, ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 125 || rows.IsDefault || rows.Length != 132 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
                throw new ArgumentException("six_island_machine_inherited_rows");
            additions.Add((item, rows.Skip(125).ToImmutableArray()));
        }
        Rows(old.Tm39.Item.Id, candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < inherited.Length; i++)
        {
            var current = machines[i]; var before = inherited[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("six_island_machine_inherited_metadata");
            Rows(before.Machine.Item.Id, current.Compatibility, before.Compatibility);
        }
        if (CampaignSourceFingerprint.Compute(additions.OrderBy(p => p.Item.Value, StringComparer.Ordinal)
                .Select(p => new { p.Item, p.Compatibility }).ToArray()) != InheritedAdditionsSourceSha256)
            throw new ArgumentException("six_island_machine_inherited_source");
        var proof = new SixIslandMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
}
