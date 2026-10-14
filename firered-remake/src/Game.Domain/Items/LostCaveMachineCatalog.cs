using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Preserves all 57 obtainable original machines, adding only Murkrow compatibility.</summary>
public sealed class LostCaveMachineCatalog
{
    public const string Profile = "firered-lost-cave-teaching-v1";
    private static readonly ConditionalWeakTable<PewterMachineCatalog, LostCaveMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public PewterMachineCatalog Predecessor { get; }
    public LostCaveOwnedPokemonCatalog Pokemon { get; }
    private LostCaveMachineCatalog(PewterMachineCatalog predecessor, LostCaveOwnedPokemonCatalog pokemon, PewterMachineCatalog catalog)
    { Predecessor = predecessor; Pokemon = pokemon; Catalog = catalog; }
    public static LostCaveMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;

    public static LostCaveMachineCatalog Authenticate(PewterMachineCatalog predecessor, LostCaveOwnedPokemonCatalog pokemon)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(pokemon);
        if (OriginalMachineCatalog.For(predecessor) is not { } original ||
            !ReferenceEquals(LostCaveOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) ||
            !ReferenceEquals(original.Pokemon.SharedBattle, pokemon.Predecessor.Owned) ||
            !ReferenceEquals(OwnedMachineCatalogBinding.For(predecessor), pokemon.Predecessor.Owned) ||
            predecessor.AdditionalMachines is not { IsDefault: false, Length: 56 } inherited)
            throw new ArgumentException("lost_cave_machine_predecessor");
        ImmutableArray<PewterMachineCompatibility> Extend(ImmutableArray<PewterMachineCompatibility> before, Game.Domain.Ids.MoveId move)
        {
            if (before.IsDefault || before.Length != 186 || !before.Select(r => r.Species).ToHashSet().SetEquals(pokemon.Predecessor.Owned.Catalog.Species.Keys))
                throw new ArgumentException("lost_cave_machine_previous_species");
            return before.Add(new(LostCaveOwnedPokemonCatalog.Murkrow, 198,
                LostCaveSpeciesSource.Metadata.MachineMoves.Contains(move), LostCaveSpeciesSource.MachineSource));
        }
        var catalog = predecessor with { Profile = Profile, Compatibility = Extend(predecessor.Compatibility, predecessor.Move),
            AdditionalMachines = inherited.Select(m => m with { Compatibility = Extend(m.Compatibility, m.Move) }).ToImmutableArray() };
        var proof = new LostCaveMachineCatalog(predecessor, pokemon, catalog);
        Proofs.Add(catalog, proof);
        OwnedMachineCatalogBinding.Register(catalog, pokemon.SharedBattle, predecessor);
        DepartmentStoreMachineStock.BindOwnedSuccessor(predecessor, catalog);
        return proof;
    }
}
