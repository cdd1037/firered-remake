using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Exact 28-machine, 113-owned-species continuation. TM04 adds 113 source
/// cells; all 3051 inherited cells and machine definitions retain object identity.</summary>
public sealed class SaffronGymMachineCatalog
{
    public const string Profile = "firered-saffron-gym-teaching-v1";
    public static ItemId Tm04Id => SaffronGymShopCatalog.Tm04Id;
    private static readonly ConditionalWeakTable<PewterMachineCatalog, SaffronGymMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public SilphMachineCatalog Predecessor { get; }
    public SilphOwnedPokemonCatalog Pokemon => Predecessor.Pokemon;
    private SaffronGymMachineCatalog(PewterMachineCatalog catalog, SilphMachineCatalog predecessor)
    { Catalog = catalog; Predecessor = predecessor; }
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static SaffronGymMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SaffronGymMachineCatalog Authenticate(SilphMachineCatalog predecessor, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(SilphMachineCatalog.For(old), predecessor) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 27 } machines ||
            candidate with { Profile = old.Profile, AdditionalMachines = old.AdditionalMachines } != old ||
            machines.Take(26).Where((machine, index) => !ReferenceEquals(machine, old.AdditionalMachines!.Value[index])).Any())
            throw new ArgumentException("saffron_gym_machine_scope");
        var additions = machines.Skip(26).ToArray();
        if (additions[0] is null || additions[0].Compatibility.IsDefault || additions[0].Compatibility.Length != 113)
            throw new ArgumentException("saffron_gym_machine_new_source");
        var proof = new SaffronGymMachineCatalog(candidate with { }, predecessor);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
}
