using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>One HM02 addition over the exact current 174-species owner; teaching remains in PewterMachineRules.</summary>
public sealed class FieldFlyMachineCatalog
{
    public const string Hm02SourceSha256 = "0b93a610c9f9a4ec5721d6f6aa24a7df00f87587a4b71d9e489c13e9f1ccb2d8";
    public static ItemId Hm02Id { get; } = new("pret:ITEM_HM02");
    private static readonly ConditionalWeakTable<PewterMachineCatalog, FieldFlyMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public PewterMachineCatalog Predecessor { get; }
    internal OwnedBattlePokemonCatalog Owner { get; }
    private FieldFlyMachineCatalog(PewterMachineCatalog catalog, PewterMachineCatalog predecessor, OwnedBattlePokemonCatalog owner)
    { Catalog = catalog; Predecessor = predecessor; Owner = owner; }
    public static FieldFlyMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static PewterMachineCatalog BaseCatalog(PewterMachineCatalog catalog) => For(catalog)?.Predecessor ?? catalog;
    public static FieldFlyMachineCatalog Authenticate(PewterMachineCatalog predecessor, PewterAdditionalMachine hm02)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(hm02);
        if (GameCornerMachineCatalog.For(predecessor) is not { } prior ||
            OwnedMachineCatalogBinding.For(predecessor) is not { } owner || !ReferenceEquals(prior.Pokemon.SharedBattle, owner) ||
            predecessor.AdditionalMachines is not { IsDefault: false, Length: 49 } before || owner.Catalog.Species.Count != 174 ||
            CampaignSourceFingerprint.Compute(hm02) != Hm02SourceSha256 ||
            hm02.Compatibility.Length != 174 || !hm02.Compatibility.Select(row => row.Species).ToHashSet().SetEquals(owner.Catalog.Species.Keys) ||
            hm02.Compatibility.Any(row => owner.Catalog.Species[row.Species].SourceSpeciesId != row.SourceSpeciesId) ||
            !PokemonMoveRules.IsLearningSupported(owner.Catalog, hm02.Move) || PewterMachineRules.Definition(predecessor, Hm02Id) is not null)
            throw new ArgumentException("field_fly_machine_source_or_owner");
        PewterMachineRules.ValidateCatalog(predecessor);
        var candidate = predecessor with { AdditionalMachines = before.Add(hm02) };
        var proof = new FieldFlyMachineCatalog(candidate, predecessor, owner);
        OwnedMachineCatalogBinding.Register(candidate, owner); Proofs.Add(candidate, proof);
        DepartmentStoreMachineStock.BindFieldFlySuccessor(proof);
        return proof;
    }
}
