using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Thirty-nine original machines for the exact 139 owned species. All inherited source cells retain identity.</summary>
public sealed class CeruleanCaveMachineCatalog
{
    public const string Profile = "firered-cerulean-cave-teaching-v1";
    private static readonly ConditionalWeakTable<PewterMachineCatalog, CeruleanCaveMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public CelioMachineCatalog Predecessor { get; }
    public CeruleanCaveOwnedPokemonCatalog Pokemon { get; }
    private CeruleanCaveMachineCatalog(PewterMachineCatalog catalog, CelioMachineCatalog predecessor, CeruleanCaveOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static CeruleanCaveMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static CeruleanCaveMachineCatalog Authenticate(CelioMachineCatalog predecessor,
        CeruleanCaveOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(CelioMachineCatalog.For(old), predecessor) || !ReferenceEquals(predecessor.Pokemon, pokemon.Predecessor) ||
            !ReferenceEquals(CeruleanCaveOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 38 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 38 } inherited ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("cerulean_cave_machine_predecessor");
        void Rows(ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 138 || rows.IsDefault || rows.Length != 139 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
                throw new ArgumentException("cerulean_cave_machine_inherited_rows");
        }
        Rows(candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < inherited.Length; i++)
        {
            var current = machines[i]; var before = inherited[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("cerulean_cave_machine_inherited_metadata");
            Rows(current.Compatibility, before.Compatibility);
        }
        var proof = new CeruleanCaveMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
}
