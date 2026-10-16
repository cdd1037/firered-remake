using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Pokemon;

public sealed record SourceEggSpeciesDefinition(SourceBreedingSpecies Breeding, SpeciesId BaseEggSpecies,
    ImmutableArray<MoveId> EggMoves);
public sealed record SourceEggSpeciesRow(SourceBreedingSpecies Breeding, SpeciesId BaseEggSpecies,
    ImmutableArray<MoveId> EggMoves, ImmutableArray<MoveId> MachineMoves);

/// <summary>Breeding rules for the explicitly admitted species. Machine inheritance is independent of acquisition.</summary>
public sealed class PokemonBreedingData
{
    public ImmutableDictionary<SpeciesId, SourceEggSpeciesDefinition> Species { get; }
    public ImmutableArray<MoveId> AllMachineMoves { get; }
    public ImmutableDictionary<SpeciesId, ImmutableHashSet<MoveId>> MachineCompatibility { get; }
    private PokemonBreedingData(ImmutableArray<SourceEggSpeciesRow> rows, ImmutableArray<MoveId> moves)
    {
        AllMachineMoves = moves;
        Species = rows.ToImmutableDictionary(r => r.Breeding.Species, r => new SourceEggSpeciesDefinition(r.Breeding, r.BaseEggSpecies, r.EggMoves));
        MachineCompatibility = rows.ToImmutableDictionary(r => r.Breeding.Species, r => r.MachineMoves.ToImmutableHashSet());
    }
    public static PokemonBreedingData Create(PokemonCatalog catalog,
        ImmutableArray<SourceEggSpeciesRow> rows, ImmutableArray<MoveId> allMachineMoves)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (rows.IsDefaultOrEmpty || rows.Any(r => r is null || r.Breeding is null || r.EggMoves.IsDefault || r.MachineMoves.IsDefault) ||
            allMachineMoves.IsDefaultOrEmpty || allMachineMoves.Distinct().Count() != allMachineMoves.Length ||
            !rows.Select(r => r.Breeding.Species).ToHashSet().SetEquals(catalog.Species.Keys) ||
            rows.Select(r => r.Breeding.Species).Distinct().Count() != rows.Length)
            throw new ArgumentException("breeding_metadata_scope");
        foreach (var row in rows)
            if (!catalog.Species.ContainsKey(row.BaseEggSpecies) || row.Breeding.EggGroup1 is < 0 or > 15 ||
                row.Breeding.EggGroup2 is < 0 or > 15 || row.Breeding.EggCycles == 0 ||
                row.EggMoves.Concat(row.MachineMoves).Any(m => !catalog.Moves.ContainsKey(m)) ||
                row.MachineMoves.Any(m => !allMachineMoves.Contains(m)))
                throw new ArgumentException("breeding_metadata_references");
        return new(rows, allMachineMoves);
    }
}
