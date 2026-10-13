using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Thirty-seven original machines for the exact 118 owned species. All inherited source cells retain identity.</summary>
public sealed class SeviiRubyMachineCatalog
{
    public const string Profile = "firered-sevii-ruby-teaching-v1";
    public const string InheritedAdditionsSourceSha256 = "f8dc0ac13ceb6e5fd09d8e76513b32b40ab84270817958b1dacfa6beb817d439";
    public const string Hm06SourceSha256 = "cec0520595825dad4e2be06cfe6b35efeee2c6157a60158780253987c5167811";
    public static ItemId Hm06Id { get; } = new("pret:ITEM_HM06");
    private static readonly ConditionalWeakTable<PewterMachineCatalog, SeviiRubyMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public VictoryRoadMachineCatalog Predecessor { get; }
    public SeviiRubyOwnedPokemonCatalog Pokemon { get; }
    private SeviiRubyMachineCatalog(PewterMachineCatalog catalog, VictoryRoadMachineCatalog predecessor, SeviiRubyOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static SeviiRubyMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PewterMachineCatalog? catalog) => For(catalog) is not null;
    public static SeviiRubyMachineCatalog Authenticate(VictoryRoadMachineCatalog predecessor,
        SeviiRubyOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(candidate);
        var old = predecessor.Catalog;
        if (!ReferenceEquals(VictoryRoadMachineCatalog.For(old), predecessor) || !ReferenceEquals(predecessor.Pokemon, pokemon.Predecessor) ||
            !ReferenceEquals(SeviiRubyOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            !ReferenceEquals(candidate.Tm39, old.Tm39) || !ReferenceEquals(candidate.TmCase, old.TmCase) ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 36 } machines ||
            old.AdditionalMachines is not { IsDefault: false, Length: 35 } inherited ||
            candidate with { Profile = old.Profile, Compatibility = old.Compatibility, AdditionalMachines = old.AdditionalMachines } != old)
            throw new ArgumentException("sevii_ruby_machine_predecessor");
        var additions = new List<(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility)>();
        void Rows(ItemId item, ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 115 || rows.IsDefault || rows.Length != 118 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys))
                throw new ArgumentException("sevii_ruby_machine_inherited_rows");
            additions.Add((item, rows.Skip(115).ToImmutableArray()));
        }
        Rows(old.Tm39.Item.Id, candidate.Compatibility, old.Compatibility);
        for (int i = 0; i < inherited.Length; i++)
        {
            var current = machines[i]; var before = inherited[i];
            if (current is null || !ReferenceEquals(current.Machine, before.Machine) || current with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("sevii_ruby_machine_inherited_metadata");
            Rows(before.Machine.Item.Id, current.Compatibility, before.Compatibility);
        }
        if (CampaignSourceFingerprint.Compute(additions.OrderBy(p => p.Item.Value, StringComparer.Ordinal)
                .Select(p => new { p.Item, p.Compatibility }).ToArray()) != InheritedAdditionsSourceSha256)
            throw new ArgumentException("sevii_ruby_machine_inherited_source");
        var fresh = machines[^1];
        if (fresh is null || fresh.Machine?.Item?.Id != Hm06Id || fresh.Compatibility.IsDefault ||
            fresh.Compatibility.Length != 118 || !fresh.Compatibility.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys) ||
            CampaignSourceFingerprint.Compute(fresh) != Hm06SourceSha256)
            throw new ArgumentException("sevii_ruby_machine_hm06_source");
        var proof = new SeviiRubyMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof); return proof;
    }
}
