using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Four source machines over Viridian's 32, with exactly the same owned115 catalog.</summary>
public sealed class VictoryRoadMachineCatalog
{
    public const string Profile = "firered-victory-road-teaching-v1";
    public static ImmutableArray<ItemId> AdditionalIds { get; } =
        [new("pret:ITEM_TM02"), new("pret:ITEM_TM07"), new("pret:ITEM_TM37"), new("pret:ITEM_TM50")];
    private static readonly ConditionalWeakTable<PewterMachineCatalog, VictoryRoadMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public ViridianGymMachineCatalog Predecessor { get; }
    public CinnabarOwnedPokemonCatalog Pokemon => Predecessor.Pokemon;
    private VictoryRoadMachineCatalog(PewterMachineCatalog catalog, ViridianGymMachineCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static VictoryRoadMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;

    public static VictoryRoadMachineCatalog Authenticate(ViridianGymMachineCatalog predecessor, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(ViridianGymMachineCatalog.For(old), predecessor) ||
            candidate.Profile != Profile || candidate.AdditionalMachines is not { IsDefault: false, Length: 35 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 31 } inherited ||
            candidate.Compatibility != old.Compatibility || !ReferenceEquals(candidate.Tm39, old.Tm39) ||
            !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate with { Profile = old.Profile, AdditionalMachines = old.AdditionalMachines } != old ||
            inherited.Where((machine, index) => !ReferenceEquals(machine, machines[index])).Any())
            throw new ArgumentException("victory_road_machine_predecessor");
        var fresh = machines.Skip(31).ToArray();
        if (fresh.Any(m => m is null || m.Machine?.Item is null || m.Compatibility.IsDefault ||
                m.Compatibility.Length != 115 || m.Compatibility.Any(c => c is null) ||
                m.Compatibility.Select(c => c.Species).Distinct().Count() != 115 ||
                !m.Compatibility.Select(c => c.Species).ToHashSet().SetEquals(predecessor.Pokemon.Catalog.Species.Keys)) ||
            !fresh.Select(m => m.Machine.Item.Id).SequenceEqual(AdditionalIds))
            throw new ArgumentException("victory_road_machine_source");
        var proof = new VictoryRoadMachineCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
}
