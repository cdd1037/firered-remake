using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Forty-one original machines for the exact 159 owned species. All inherited source cells retain identity.</summary>
public sealed class FossilLabMachineCatalog
{
    public const string Profile = "firered-fossil-lab-teaching-v1";
    private static readonly ConditionalWeakTable<PewterMachineCatalog, FossilLabMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public FightingDojoMachineCatalog Predecessor { get; }
    public FossilLabOwnedPokemonCatalog Pokemon { get; }
    private FossilLabMachineCatalog(PewterMachineCatalog catalog, FightingDojoMachineCatalog predecessor, FossilLabOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static FossilLabMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static FossilLabMachineCatalog Authenticate(FightingDojoMachineCatalog predecessor,
        FossilLabOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(FightingDojoMachineCatalog.For(old), predecessor) || !ReferenceEquals(predecessor.Pokemon, pokemon.Predecessor) ||
            !ReferenceEquals(FossilLabOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 40 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 40 } inherited ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("fossil_lab_machine_predecessor");
        void Rows(ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 154 || rows.IsDefault || rows.Length != 159 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
                throw new ArgumentException("fossil_lab_machine_inherited_rows");
        }
        Rows(candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < inherited.Length; i++)
        {
            var current = machines[i]; var before = inherited[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("fossil_lab_machine_inherited_metadata");
            Rows(current.Compatibility, before.Compatibility);
        }
        var proof = new FossilLabMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof);
        OwnedMachineCatalogBinding.Register(proof.Catalog, pokemon.SharedBattle);
        return proof;
    }
}
