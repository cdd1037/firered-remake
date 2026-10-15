using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Thirty-nine original machines for the exact 135 owned species. All inherited source cells retain identity.</summary>
public sealed class FiveIslandMachineCatalog
{
    public const string Profile = "firered-five-island-teaching-v1";
    public static ItemId Tm36Id { get; } = new("pret:ITEM_TM36");
    private static readonly ConditionalWeakTable<PewterMachineCatalog, FiveIslandMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public SixIslandMachineCatalog Predecessor { get; }
    public FiveIslandOwnedPokemonCatalog Pokemon { get; }
    private FiveIslandMachineCatalog(PewterMachineCatalog catalog, SixIslandMachineCatalog predecessor, FiveIslandOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static FiveIslandMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static FiveIslandMachineCatalog Authenticate(SixIslandMachineCatalog predecessor,
        FiveIslandOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(SixIslandMachineCatalog.For(old), predecessor) || !ReferenceEquals(predecessor.Pokemon, pokemon.Predecessor) ||
            !ReferenceEquals(FiveIslandOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 38 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 37 } inherited ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("five_island_machine_predecessor");
        void Rows(ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 132 || rows.IsDefault || rows.Length != 135 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
                throw new ArgumentException("five_island_machine_inherited_rows");
        }
        Rows(candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < inherited.Length; i++)
        {
            var current = machines[i]; var before = inherited[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("five_island_machine_inherited_metadata");
            Rows(current.Compatibility, before.Compatibility);
        }
        var fresh = machines[^1];
        if (fresh is null || fresh.Machine?.Item?.Id != Tm36Id || fresh.Compatibility.IsDefault ||
            fresh.Compatibility.Length != 135 || !fresh.Compatibility.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
            throw new ArgumentException("five_island_machine_tm36_source");
        var proof = new FiveIslandMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
}
