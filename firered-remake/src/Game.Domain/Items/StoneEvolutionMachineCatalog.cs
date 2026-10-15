using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>The 42 existing machines, with exact seven-species cells appended to the Department shelf owner's rows.</summary>
public sealed class StoneEvolutionMachineCatalog
{
    public const string Profile = "firered-stone-evolution-teaching-v1";
    private static readonly ConditionalWeakTable<PewterMachineCatalog, StoneEvolutionMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public DepartmentStoreMachineStock Predecessor { get; }
    public StoneEvolutionOwnedPokemonCatalog Pokemon { get; }
    private StoneEvolutionMachineCatalog(PewterMachineCatalog catalog, DepartmentStoreMachineStock predecessor, StoneEvolutionOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static StoneEvolutionMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static StoneEvolutionMachineCatalog Authenticate(DepartmentStoreMachineStock predecessor,
        StoneEvolutionOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Machines;
        if (!ReferenceEquals(DepartmentStoreMachineStock.For(old), predecessor) || !ReferenceEquals(SuperRodMachineCatalog.For(predecessor.OriginalMachines)?.Pokemon, pokemon.Predecessor) ||
            !ReferenceEquals(StoneEvolutionOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 41 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 41 } inherited ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("stone_evolution_machine_predecessor");
        void Rows(ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 163 || rows.IsDefault || rows.Length != 170 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
                throw new ArgumentException("stone_evolution_machine_inherited_rows");
            if (!rows.Skip(163).Select(c => c.Species).SequenceEqual(StoneEvolutionOwnedPokemonCatalog.AdditionalSpecies))
                throw new ArgumentException("stone_evolution_machine_added_order");
        }
        Rows(candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < inherited.Length; i++)
        {
            var current = machines[i]; var before = inherited[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("stone_evolution_machine_inherited_metadata");
            Rows(current.Compatibility, before.Compatibility);
        }
        var proof = new StoneEvolutionMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof);
        OwnedMachineCatalogBinding.Register(proof.Catalog, pokemon.SharedBattle);
        DepartmentStoreMachineStock.BindStoneEvolutionSuccessor(proof);
        return proof;
    }
}
