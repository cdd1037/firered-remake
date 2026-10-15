using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>The original 50 TMs and seven obtainable FireRed HMs, over the exact 186 owned species.</summary>
public sealed class OriginalMachineCatalog
{
    public const string Profile = "firered-original-machine-catalog-v1";
    public static ImmutableArray<ItemId> AdditionalItems { get; } = [
        new("pret:ITEM_TM10"), new("pret:ITEM_TM18"), new("pret:ITEM_TM27"),
        new("pret:ITEM_TM29"), new("pret:ITEM_TM42"), new("pret:ITEM_TM48")];
    private static readonly ConditionalWeakTable<PewterMachineCatalog, OriginalMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public OrdinaryEvolutionMachineCatalog Predecessor { get; }
    public OrdinaryEvolutionOwnedPokemonCatalog Pokemon => Predecessor.Pokemon;
    private OriginalMachineCatalog(PewterMachineCatalog catalog, OrdinaryEvolutionMachineCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static OriginalMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;

    public static OriginalMachineCatalog Authenticate(OrdinaryEvolutionMachineCatalog predecessor, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog; var pokemon = predecessor.Pokemon;
        if (!ReferenceEquals(OrdinaryEvolutionMachineCatalog.For(old), predecessor) ||
            !ReferenceEquals(OrdinaryEvolutionOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) ||
            !ReferenceEquals(OwnedMachineCatalogBinding.For(old), pokemon.SharedBattle) ||
            candidate.Profile != Profile || old.AdditionalMachines is not { IsDefault: false, Length: 50 } inherited ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 56 } machines ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate with { Profile = old.Profile, AdditionalMachines = old.AdditionalMachines } != old ||
            inherited.Where((machine, i) => !ReferenceEquals(machine, machines[i])).Any())
            throw new ArgumentException("original_machine_predecessor");
        var added = machines.Skip(inherited.Length).ToImmutableArray();
        if (added.Any(m => m is null || m.Machine?.Item is null) || !added.Select(m => m.Machine.Item.Id).SequenceEqual(AdditionalItems))
            throw new ArgumentException("original_machine_additions_source");
        foreach (var machine in added)
            if (PewterMachineRules.Definition(old, machine.Machine.Item.Id) is not null ||
                machine.Compatibility.IsDefault || machine.Compatibility.Length != 186 ||
                !machine.Compatibility.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys) ||
                machine.Compatibility.Any(c => pokemon.Catalog.Species[c.Species].SourceSpeciesId != c.SourceSpeciesId) ||
                !PokemonMoveRules.IsLearningSupported(pokemon.Catalog, machine.Move))
                throw new ArgumentException("original_machine_compatibility_owner");
        var expected = Enumerable.Range(1, 50).Select(i => $"pret:ITEM_TM{i:00}")
            .Concat(Enumerable.Range(1, 7).Select(i => $"pret:ITEM_HM{i:00}")).ToHashSet();
        if (!machines.Select(m => m.Machine.Item.Id.Value).Append(candidate.Tm39.Item.Id.Value).ToHashSet().SetEquals(expected))
            throw new ArgumentException("original_machine_complete_set");
        var proof = new OriginalMachineCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof);
        OwnedMachineCatalogBinding.Register(proof.Catalog, pokemon.SharedBattle, old);
        DepartmentStoreMachineStock.BindOriginalMachineSuccessor(proof);
        return proof;
    }
}
