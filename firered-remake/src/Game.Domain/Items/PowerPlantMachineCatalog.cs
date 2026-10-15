using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Forty-one original machines for the exact 152 owned species. All inherited source cells retain identity.</summary>
public sealed class PowerPlantMachineCatalog
{
    public const string Profile = "firered-power-plant-teaching-v1";
    public static ItemId Tm17Id { get; } = new("pret:ITEM_TM17");
    public static ItemId Tm25Id { get; } = new("pret:ITEM_TM25");
    private static readonly ConditionalWeakTable<PewterMachineCatalog, PowerPlantMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public EventIslandMachineCatalog Predecessor { get; }
    public PowerPlantOwnedPokemonCatalog Pokemon { get; }
    private PowerPlantMachineCatalog(PewterMachineCatalog catalog, EventIslandMachineCatalog predecessor, PowerPlantOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static PowerPlantMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static PowerPlantMachineCatalog Authenticate(EventIslandMachineCatalog predecessor,
        PowerPlantOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(EventIslandMachineCatalog.For(old), predecessor) || !ReferenceEquals(predecessor.Pokemon, pokemon.Predecessor) ||
            !ReferenceEquals(PowerPlantOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 40 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 38 } inherited ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("power_plant_machine_predecessor");
        void Rows(ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 149 || rows.IsDefault || rows.Length != 152 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
                throw new ArgumentException("power_plant_machine_inherited_rows");
        }
        Rows(candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < inherited.Length; i++)
        {
            var current = machines[i]; var before = inherited[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("power_plant_machine_inherited_metadata");
            Rows(current.Compatibility, before.Compatibility);
        }
        var fresh = machines.Skip(38).ToArray();
        if (fresh.Length != 2 || fresh.Any(m => m is null || m.Machine?.Item is null) || !fresh.Select(m => m.Machine.Item.Id).SequenceEqual([Tm17Id,Tm25Id]) ||
            fresh.Any(m => m is null || m.Compatibility.IsDefault || m.Compatibility.Length != 152 ||
                !m.Compatibility.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys)))
            throw new ArgumentException("power_plant_machine_new_source");
        var proof = new PowerPlantMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
}
