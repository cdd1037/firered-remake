using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
namespace Game.Domain.Pokemon;

public sealed record SourceEggSpeciesDefinition(SourceBreedingSpecies Breeding, SpeciesId BaseEggSpecies,
    ImmutableArray<MoveId> EggMoves);
public sealed record SourceEggSpeciesRow(SourceBreedingSpecies Breeding, SpeciesId BaseEggSpecies,
    ImmutableArray<MoveId> EggMoves, ImmutableArray<MoveId> MachineMoves);

/// <summary>Exact owned-species breeding projection from the shared canonical 386 metadata.
/// The original 58 TM/HM inheritance mask does not depend on current machine acquisition.</summary>
public sealed class BreedingSpeciesMetadataCatalog
{
    private static readonly ConditionalWeakTable<PokemonCatalog, BreedingSpeciesMetadataCatalog> Proofs = new();
    public PokemonCatalog Catalog { get; }
    public ImmutableDictionary<SpeciesId, SourceEggSpeciesDefinition> Species { get; }
    public ImmutableArray<MoveId> AllMachineMoves { get; }
    public ImmutableDictionary<SpeciesId, ImmutableHashSet<MoveId>> MachineCompatibility { get; }
    private BreedingSpeciesMetadataCatalog(PokemonCatalog catalog, ImmutableArray<SourceEggSpeciesRow> rows, ImmutableArray<MoveId> moves)
    {
        Catalog = catalog; AllMachineMoves = moves;
        Species = rows.ToImmutableDictionary(r => r.Breeding.Species, r => new SourceEggSpeciesDefinition(r.Breeding, r.BaseEggSpecies, r.EggMoves));
        MachineCompatibility = rows.ToImmutableDictionary(r => r.Breeding.Species, r => r.MachineMoves.ToImmutableHashSet());
    }
    public static BreedingSpeciesMetadataCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static BreedingSpeciesMetadataCatalog Authenticate(BreedingOwnedPokemonCatalog owned,
        ImmutableArray<SourceEggSpeciesRow> rows, ImmutableArray<MoveId> allMachineMoves)
    {
        ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(BreedingOwnedPokemonCatalog.For(owned.Catalog), owned) || rows.IsDefault || rows.Length != 182 ||
            rows.Any(r => r is null || r.Breeding is null || r.EggMoves.IsDefault || r.MachineMoves.IsDefault) ||
            allMachineMoves.IsDefault || allMachineMoves.Length != 58 || allMachineMoves.Distinct().Count() != 58 ||
            !rows.Select(r => r.Breeding.Species).ToHashSet().SetEquals(owned.Catalog.Species.Keys))
            throw new ArgumentException("breeding_metadata_source_or_owner");
        foreach (var row in rows)
            if (!owned.Catalog.Species.ContainsKey(row.BaseEggSpecies) || row.Breeding.EggGroup1 is < 0 or > 15 ||
                row.Breeding.EggGroup2 is < 0 or > 15 || row.Breeding.EggCycles == 0 ||
                row.EggMoves.Concat(row.MachineMoves).Any(m => !owned.Catalog.Moves.ContainsKey(m)) ||
                row.MachineMoves.Any(m => !allMachineMoves.Contains(m)))
                throw new ArgumentException("breeding_metadata_references");
        return Proofs.GetValue(owned.Catalog, _ => new(owned.Catalog, rows, allMachineMoves));
    }
    // Only a sealed, source-checked successor invokes this binding. Existing rows retain
    // their exact immutable definition and compatibility-set references.
    internal static BreedingSpeciesMetadataCatalog RegisterSuccessor(OwnedBattlePokemonCatalog owner,
        BreedingSpeciesMetadataCatalog predecessor, ImmutableArray<SourceEggSpeciesRow> additions)
    {
        if (!ReferenceEquals(OwnedBattlePokemonCatalog.For(owner.Catalog), owner) ||
            !ReferenceEquals(For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(OwnedBattlePokemonCatalog.SourceAncestor(owner, c => ReferenceEquals(c, predecessor.Catalog) ? predecessor : null), predecessor) ||
            !owner.Catalog.Species.Keys.Except(predecessor.Species.Keys).ToHashSet().SetEquals(additions.Select(r => r.Breeding.Species)))
            throw new ArgumentException("breeding_metadata_successor_owner");
        var proof = new BreedingSpeciesMetadataCatalog(owner.Catalog, predecessor, additions);
        return Proofs.GetValue(owner.Catalog, _ => proof);
    }
    private BreedingSpeciesMetadataCatalog(PokemonCatalog catalog, BreedingSpeciesMetadataCatalog predecessor,
        ImmutableArray<SourceEggSpeciesRow> additions)
    {
        Catalog = catalog; AllMachineMoves = predecessor.AllMachineMoves;
        Species = predecessor.Species.AddRange(additions.Select(r => KeyValuePair.Create(r.Breeding.Species,
            new SourceEggSpeciesDefinition(r.Breeding, r.BaseEggSpecies, r.EggMoves))));
        MachineCompatibility = predecessor.MachineCompatibility.AddRange(additions.Select(r =>
            KeyValuePair.Create(r.Breeding.Species, r.MachineMoves.ToImmutableHashSet())));
    }

}
