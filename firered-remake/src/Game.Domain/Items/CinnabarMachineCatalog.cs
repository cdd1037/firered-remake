using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Exact 31-machine, 115-owned-species successor. All 3164 inherited source cells retain identity.</summary>
public sealed class CinnabarMachineCatalog
{
    public const string Profile = "firered-cinnabar-teaching-v1";
    public const string NewMachinesSourceSha256 = "2394b51643da54842704d51e9a127799b7553dca0b851ea1c6b46c1e00c79e23";
    public const string InheritedAdditionsSourceSha256 = "369f3dbc8fbe74bdddf8c06f673758ff3a0ddeb47e1ecfd903cb2b5faf559866";
    public static ItemId Tm14Id { get; } = new("pret:ITEM_TM14");
    public static ItemId Tm22Id { get; } = new("pret:ITEM_TM22");
    public static ItemId Tm38Id { get; } = new("pret:ITEM_TM38");
    private static readonly ConditionalWeakTable<PewterMachineCatalog, CinnabarMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public SaffronGymMachineCatalog Predecessor { get; }
    public CinnabarOwnedPokemonCatalog Pokemon { get; }
    private CinnabarMachineCatalog(PewterMachineCatalog catalog, SaffronGymMachineCatalog predecessor, CinnabarOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static CinnabarMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;

    public static CinnabarMachineCatalog Authenticate(SaffronGymMachineCatalog predecessor,
        CinnabarOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate); ArgumentNullException.ThrowIfNull(pokemon);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(SaffronGymMachineCatalog.For(old), predecessor) || !ReferenceEquals(predecessor.Pokemon, pokemon.Predecessor) ||
            !ReferenceEquals(CinnabarOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 30 } machines ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("cinnabar_machine_scope");
        var additions = new List<object>();
        void Rows(ItemId item, ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 113 || rows.IsDefault || rows.Length != 115 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
                throw new ArgumentException("cinnabar_machine_inherited_rows");
            additions.Add(new { Item = item, Compatibility = rows.Skip(113).ToArray() });
        }
        Rows(old.Tm39.Item.Id, candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < 27; i++)
        {
            var current = machines[i]; var before = old.AdditionalMachines!.Value[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("cinnabar_machine_predecessor");
            Rows(before.Machine.Item.Id, current.Compatibility, before.Compatibility);
        }
        if (CampaignSourceFingerprint.Compute(additions) != InheritedAdditionsSourceSha256)
            throw new ArgumentException("cinnabar_machine_inherited_source");
        var fresh = machines.Skip(27).ToArray();
        if (fresh.Any(m => m is null || m.Compatibility.IsDefault || m.Compatibility.Length != 115) ||
            CampaignSourceFingerprint.Compute(fresh) != NewMachinesSourceSha256)
            throw new ArgumentException("cinnabar_machine_new_source");
        var proof = new CinnabarMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
}
