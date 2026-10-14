using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>The 41 existing machines, with exact Qwilfish cells appended to the Seafoam owner's rows.</summary>
public sealed class SuperRodMachineCatalog
{
    public const string Profile = "firered-super-rod-teaching-v1";
    public const string InheritedAdditionsSourceSha256 = "b8b8877fd821c811b4000479b96d543275c3854f4cf8dd578417e30f75b647c1";
    private static readonly ConditionalWeakTable<PewterMachineCatalog, SuperRodMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public SeafoamMachineCatalog Predecessor { get; }
    public SuperRodOwnedPokemonCatalog Pokemon { get; }
    private SuperRodMachineCatalog(PewterMachineCatalog catalog, SeafoamMachineCatalog predecessor, SuperRodOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static SuperRodMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static SuperRodMachineCatalog Authenticate(SeafoamMachineCatalog predecessor,
        SuperRodOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(SeafoamMachineCatalog.For(old), predecessor) || !ReferenceEquals(predecessor.Pokemon, pokemon.Predecessor) ||
            !ReferenceEquals(SuperRodOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 40 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 40 } inherited ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("super_rod_machine_predecessor");
        var additions = new List<(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility)>();
        void Rows(ItemId item, ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 162 || rows.IsDefault || rows.Length != 163 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
                throw new ArgumentException("super_rod_machine_inherited_rows");
            additions.Add((item, [rows[162]]));
        }
        Rows(old.Tm39.Item.Id, candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < inherited.Length; i++)
        {
            var current = machines[i]; var before = inherited[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("super_rod_machine_inherited_metadata");
            Rows(before.Machine.Item.Id, current.Compatibility, before.Compatibility);
        }
        if (CampaignSourceFingerprint.Compute(additions.OrderBy(p => p.Item.Value, StringComparer.Ordinal)
                .Select(p => new { p.Item, p.Compatibility }).ToArray()) != InheritedAdditionsSourceSha256)
            throw new ArgumentException("super_rod_machine_inherited_source");
        var proof = new SuperRodMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof);
        OwnedMachineCatalogBinding.Register(proof.Catalog, pokemon.SharedBattle);
        return proof;
    }
}
