using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Authenticated24-machine112-species continuation; preserves every2640 inherited cell.</summary>
public sealed class SurfMachineCatalog
{
    public const string Profile = "firered-surf-teaching-v1";
    private static readonly ConditionalWeakTable<PewterMachineCatalog, SurfMachineCatalog> Authenticated = new();
    public PewterMachineCatalog Catalog { get; }
    public SurfOwnedPokemonCatalog Pokemon { get; }
    private SurfMachineCatalog(PewterMachineCatalog catalog, SurfOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Pokemon = pokemon; }
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    internal static SurfMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Authenticated.TryGetValue(catalog, out var proof) ? proof : null;
    // Exact pinned TMHM_LEARNSET symbols, src/data/pokemon/tmhm_learnsets.h.
    private static readonly ImmutableHashSet<string> PsyduckMachines = new[] {
        "TM01","TM03","TM04","TM06","TM07","TM10","TM13","TM14","TM17","TM18","TM21","TM23",
        "TM27","TM28","TM31","TM32","TM40","TM42","TM43","TM44","TM45","HM03","HM04","HM05","HM06","HM07","HM08"
    }.ToImmutableHashSet(StringComparer.Ordinal);
    public static SurfMachineCatalog Authenticate(SafariMachineCatalog predecessor,
        PewterMachineCatalog candidate, SurfOwnedPokemonCatalog pokemon)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate); ArgumentNullException.ThrowIfNull(pokemon);
        var old = predecessor.Catalog;
        if (!SafariMachineCatalog.IsAuthenticated(old) || !ReferenceEquals(predecessor.Pokemon, pokemon.Predecessor) ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.Profile != Profile || candidate.Compatibility.IsDefault || candidate.AdditionalMachines is not { IsDefault: false, Length: 23 } machines ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("surf_machine_scope");
        void Rows(ItemId machine, ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 110 || rows.IsDefault || rows.Length != 112 ||
                before.Where((cell,index) => !ReferenceEquals(cell, rows[index])).Any())
                throw new ArgumentException("surf_machine_predecessor");
            for (int i = 0; i < 2; i++)
            {
                var id = SurfOwnedPokemonCatalog.AdditionalSpecies[i];
                string symbol = machine.Value[10..];
                bool learns = PsyduckMachines.Contains(symbol) || i == 1 && symbol == "TM15";
                if (rows[110+i] != new PewterMachineCompatibility(id, 54+i, learns))
                    throw new ArgumentException("surf_machine_source");
            }
        }
        Rows(old.Tm39.Item.Id, candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < machines.Length; i++)
        {
            var current = machines[i]; var before = old.AdditionalMachines!.Value[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("surf_machine_definition");
            Rows(before.Machine.Item.Id, current.Compatibility, before.Compatibility);
        }
        var proof = new SurfMachineCatalog(candidate with { }, pokemon);
        Authenticated.Add(proof.Catalog, proof); return proof;
    }
}
