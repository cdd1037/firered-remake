using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>The 51 acquired machines retain all metadata and add exactly eight source compatibility rows.</summary>
public sealed class BreedingMachineCatalog
{
    public const string Profile = "firered-breeding-teaching-v1";
    public const string InheritedAdditionsSourceSha256 = "8e5567627b2380640a562ca4755b6592e8312cec58199a6f72c2e666e268a1c4";
    private static readonly ConditionalWeakTable<PewterMachineCatalog, BreedingMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public PewterMachineCatalog Predecessor { get; }
    public BreedingOwnedPokemonCatalog Pokemon { get; }
    private BreedingMachineCatalog(PewterMachineCatalog catalog, PewterMachineCatalog predecessor, BreedingOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static BreedingMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static BreedingMachineCatalog Authenticate(PewterMachineCatalog predecessor,
        BreedingOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor;
        if (FieldFlyMachineCatalog.For(old) is null || !ReferenceEquals(OwnedMachineCatalogBinding.For(old), pokemon.Predecessor.SharedBattle) ||
            !ReferenceEquals(BreedingOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 50 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 50 } inherited ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("breeding_machine_predecessor");
        var additions = new List<(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility)>();
        void Rows(ItemId item, ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 174 || rows.IsDefault || rows.Length != 182 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
                throw new ArgumentException("breeding_machine_inherited_rows");
            if (!rows.Skip(174).Select(c => c.Species).SequenceEqual(BreedingOwnedPokemonCatalog.AdditionalSpecies))
                throw new ArgumentException("breeding_machine_added_order");
            additions.Add((item, rows.Skip(174).ToImmutableArray()));
        }
        Rows(old.Tm39.Item.Id, candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < inherited.Length; i++)
        {
            var current = machines[i]; var before = inherited[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("breeding_machine_inherited_metadata");
            Rows(before.Machine.Item.Id, current.Compatibility, before.Compatibility);
        }
        if (CampaignSourceFingerprint.Compute(additions.OrderBy(p => p.Item.Value, StringComparer.Ordinal)
                .Select(p => new { p.Item, p.Compatibility }).ToArray()) != InheritedAdditionsSourceSha256)
            throw new ArgumentException("breeding_machine_inherited_source");
        var proof = new BreedingMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof);
        OwnedMachineCatalogBinding.Register(proof.Catalog, pokemon.SharedBattle, predecessor);
        DepartmentStoreMachineStock.BindBreedingSuccessor(proof);
        return proof;
    }
}
