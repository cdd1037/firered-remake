using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>The 57 acquired machines retain all metadata and add exactly two source compatibility rows.</summary>
public sealed class GiftEggMachineCatalog
{
    public const string Profile = "firered-gift-egg-teaching-v1";
    private static readonly ConditionalWeakTable<PewterMachineCatalog, GiftEggMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public PewterMachineCatalog Predecessor { get; }
    public GiftEggOwnedPokemonCatalog Pokemon { get; }
    private GiftEggMachineCatalog(PewterMachineCatalog catalog, PewterMachineCatalog predecessor, GiftEggOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static GiftEggMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static GiftEggMachineCatalog Authenticate(PewterMachineCatalog predecessor,
        GiftEggOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor;
        if (LostCaveMachineCatalog.For(old) is null || !ReferenceEquals(OwnedMachineCatalogBinding.For(old), pokemon.Predecessor.SharedBattle) ||
            !ReferenceEquals(GiftEggOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 56 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 56 } inherited ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("gift_egg_machine_predecessor");
        void Rows(ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 187 || rows.IsDefault || rows.Length != 189 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
                throw new ArgumentException("gift_egg_machine_inherited_rows");
            if (!rows.Skip(187).Select(c => c.Species).SequenceEqual(GiftEggOwnedPokemonCatalog.AdditionalSpecies))
                throw new ArgumentException("gift_egg_machine_added_order");
        }
        Rows(candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < inherited.Length; i++)
        {
            var current = machines[i]; var before = inherited[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("gift_egg_machine_inherited_metadata");
            Rows(current.Compatibility, before.Compatibility);
        }
        var proof = new GiftEggMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof);
        OwnedMachineCatalogBinding.Register(proof.Catalog, pokemon.SharedBattle, predecessor);
        DepartmentStoreMachineStock.BindOwnedSuccessor(predecessor, proof.Catalog);
        return proof;
    }
}
