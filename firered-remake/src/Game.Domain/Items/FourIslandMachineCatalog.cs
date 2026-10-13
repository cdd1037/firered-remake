using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Thirty-eight original machines for the exact 125 owned species. All inherited source cells retain identity.</summary>
public sealed class FourIslandMachineCatalog
{
    public const string Profile = "firered-four-island-teaching-v1";
    public const string InheritedAdditionsSourceSha256 = "a25475715a1f328900b60731b0df7ee3877e033460001740c450861414b89773";
    public const string Hm07SourceSha256 = "ad37907a62cc6166187d1f3938d022f933508f7d49b1a8859a23db162bf15ed2";
    public static ItemId Hm07Id { get; } = new("pret:ITEM_HM07");
    private static readonly ConditionalWeakTable<PewterMachineCatalog, FourIslandMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public SeviiRubyMachineCatalog Predecessor { get; }
    public FourIslandOwnedPokemonCatalog Pokemon { get; }
    private FourIslandMachineCatalog(PewterMachineCatalog catalog, SeviiRubyMachineCatalog predecessor, FourIslandOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static FourIslandMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static FourIslandMachineCatalog Authenticate(SeviiRubyMachineCatalog predecessor,
        FourIslandOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(SeviiRubyMachineCatalog.For(old), predecessor) || !ReferenceEquals(predecessor.Pokemon, pokemon.Predecessor) ||
            !ReferenceEquals(FourIslandOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 37 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 36 } inherited ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("four_island_machine_predecessor");
        var additions = new List<(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility)>();
        void Rows(ItemId item, ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 118 || rows.IsDefault || rows.Length != 125 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
                throw new ArgumentException("four_island_machine_inherited_rows");
            additions.Add((item, rows.Skip(118).ToImmutableArray()));
        }
        Rows(old.Tm39.Item.Id, candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < inherited.Length; i++)
        {
            var current = machines[i]; var before = inherited[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("four_island_machine_inherited_metadata");
            Rows(before.Machine.Item.Id, current.Compatibility, before.Compatibility);
        }
        if (CampaignSourceFingerprint.Compute(additions.OrderBy(p => p.Item.Value, StringComparer.Ordinal)
                .Select(p => new { p.Item, p.Compatibility }).ToArray()) != InheritedAdditionsSourceSha256)
            throw new ArgumentException("four_island_machine_inherited_source");
        var fresh = machines[^1];
        if (fresh is null || fresh.Machine?.Item?.Id != Hm07Id || fresh.Compatibility.IsDefault ||
            fresh.Compatibility.Length != 125 || !fresh.Compatibility.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys) ||
            CampaignSourceFingerprint.Compute(fresh) != Hm07SourceSha256)
            throw new ArgumentException("four_island_machine_hm07_source");
        var proof = new FourIslandMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
}
