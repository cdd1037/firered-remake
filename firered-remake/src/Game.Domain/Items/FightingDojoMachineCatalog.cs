using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Forty-one original machines for the exact 154 owned species. All inherited source cells retain identity.</summary>
public sealed class FightingDojoMachineCatalog
{
    public const string Profile = "firered-fighting-dojo-teaching-v1";
    private static readonly ConditionalWeakTable<PewterMachineCatalog, FightingDojoMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public PowerPlantMachineCatalog Predecessor { get; }
    public FightingDojoOwnedPokemonCatalog Pokemon { get; }
    private FightingDojoMachineCatalog(PewterMachineCatalog catalog, PowerPlantMachineCatalog predecessor, FightingDojoOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static FightingDojoMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static FightingDojoMachineCatalog Authenticate(PowerPlantMachineCatalog predecessor,
        FightingDojoOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(PowerPlantMachineCatalog.For(old), predecessor) || !ReferenceEquals(predecessor.Pokemon, pokemon.Predecessor) ||
            !ReferenceEquals(FightingDojoOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 40 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 40 } inherited ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("fighting_dojo_machine_predecessor");
        void Rows(ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 152 || rows.IsDefault || rows.Length != 154 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
                throw new ArgumentException("fighting_dojo_machine_inherited_rows");
        }
        Rows(candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < inherited.Length; i++)
        {
            var current = machines[i]; var before = inherited[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("fighting_dojo_machine_inherited_metadata");
            Rows(current.Compatibility, before.Compatibility);
        }
        var proof = new FightingDojoMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof);
        OwnedMachineCatalogBinding.Register(proof.Catalog, pokemon.SharedBattle);
        return proof;
    }
}
