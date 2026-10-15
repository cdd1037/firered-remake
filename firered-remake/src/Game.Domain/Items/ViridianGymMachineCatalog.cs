using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Exactly one new machine and no new species. All 31 inherited machine definitions and
/// all 3565 inherited compatibility cells retain their original object identity.</summary>
public sealed class ViridianGymMachineCatalog
{
    public const string Profile = "firered-viridian-gym-teaching-v1";
    public static ItemId Tm26Id { get; } = new("pret:ITEM_TM26");
    private static readonly ConditionalWeakTable<PewterMachineCatalog, ViridianGymMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public CinnabarMachineCatalog Predecessor { get; }
    public CinnabarOwnedPokemonCatalog Pokemon => Predecessor.Pokemon;
    private ViridianGymMachineCatalog(PewterMachineCatalog catalog, CinnabarMachineCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static ViridianGymMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;

    public static ViridianGymMachineCatalog Authenticate(CinnabarMachineCatalog predecessor, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(CinnabarMachineCatalog.For(old), predecessor) ||
            candidate.Profile != Profile || candidate.AdditionalMachines is not { IsDefault: false, Length: 31 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 30 } inherited ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.Compatibility != old.Compatibility ||
            candidate with { Profile = old.Profile, AdditionalMachines = old.AdditionalMachines } != old ||
            inherited.Where((machine, index) => !ReferenceEquals(machine, machines[index])).Any())
            throw new ArgumentException("viridian_gym_machine_predecessor");
        var fresh = machines[30];
        if (fresh is null || fresh.Compatibility.IsDefault || fresh.Compatibility.Length != 115 || fresh.Compatibility.Any(c => c is null) ||
            fresh.Machine?.Item?.Id != Tm26Id || fresh.Move.Value != "pret:MOVE_EARTHQUAKE" ||
            fresh.SourceMoveId != 89 || fresh.MovePp != 10 ||
            !fresh.Compatibility.Select(c => c.Species).ToHashSet().SetEquals(predecessor.Pokemon.Catalog.Species.Keys))
            throw new ArgumentException("viridian_gym_machine_source");
        var proof = new ViridianGymMachineCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof);
        return proof;
    }
}
