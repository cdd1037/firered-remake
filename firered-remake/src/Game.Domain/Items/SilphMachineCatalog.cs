using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Exact 27-machine, 113-species Silph extension; all inherited 2688 cells retain identity.</summary>
public sealed class SilphMachineCatalog
{
    public const string Profile = "firered-silph-teaching-v1";
    public const string NewMachinesSourceSha256 = "c0972d6f358e59bdba9e3dc908ec71ea61cb70c5dde50f2bd2e47fdb06bca624";
    private static readonly ConditionalWeakTable<PewterMachineCatalog, SilphMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public SurfMachineCatalog Predecessor { get; }
    public SilphOwnedPokemonCatalog Pokemon { get; }
    private SilphMachineCatalog(PewterMachineCatalog catalog, SurfMachineCatalog predecessor, SilphOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static SilphMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    private static readonly ImmutableHashSet<string> LaprasMachines = new[] {
        "TM03","TM05","TM06","TM07","TM10","TM13","TM14","TM15","TM17","TM18","TM20","TM21","TM23",
        "TM24","TM25","TM27","TM29","TM32","TM34","TM42","TM43","TM44","TM45","HM03","HM04","HM06","HM07","HM08"
    }.ToImmutableHashSet(StringComparer.Ordinal);
    private static readonly OpeningItemSourceRef LaprasSource = new("src/data/pokemon/tmhm_learnsets.h", "SPECIES_LAPRAS",
        3077, 3104, "92bf603f90469ccae0398902db943770e2cb12cb910883b42a552cff07cff31f");
    public static SilphMachineCatalog Authenticate(SurfMachineCatalog predecessor, PewterMachineCatalog candidate,
        SilphOwnedPokemonCatalog pokemon)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate); ArgumentNullException.ThrowIfNull(pokemon);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(SurfMachineCatalog.For(old), predecessor) || !ReferenceEquals(predecessor.Pokemon, pokemon.Predecessor) ||
            !ReferenceEquals(SilphOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 26 } machines ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("silph_machine_scope");
        void Rows(ItemId item, ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (rows.IsDefault || rows.Length != 113 || before.Length != 112 ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                rows[112] != new PewterMachineCompatibility(SilphOwnedPokemonCatalog.Lapras, 131, LaprasMachines.Contains(item.Value[10..]), LaprasSource))
                throw new ArgumentException("silph_machine_inherited_rows");
        }
        Rows(old.Tm39.Item.Id, candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < 23; i++)
        {
            var current = machines[i]; var before = old.AdditionalMachines!.Value[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("silph_machine_predecessor");
            Rows(before.Machine.Item.Id, current.Compatibility, before.Compatibility);
        }
        var additions = machines.Skip(23).ToArray();
        if (additions.Any(m => m is null || m.Compatibility.IsDefault || m.Compatibility.Length != 113) ||
            CampaignSourceFingerprint.Compute(additions) != NewMachinesSourceSha256)
            throw new ArgumentException("silph_machine_new_source");
        var proof = new SilphMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
}
