using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record GeneratedEggSpeciesRow(SourceBreedingSpecies Breeding, SpeciesId BaseEggSpecies,
    ImmutableArray<MoveId> EggMoves);

/// <summary>Breeding rows reuse the original TM/HM compatibility table, including the eighth HM slot.</summary>
internal static class GeneratedBreedingMetadata
{
    internal static T Read<T>(string root, string relative) => ContentJson.ParseStrict<T>(
        File.ReadAllText(Path.Combine(root, "content/generated", relative)), ContentJson.CreateOptions());

    internal static ImmutableArray<SourceEggSpeciesRow> Resolve(string root,
        ImmutableArray<GeneratedEggSpeciesRow> rows, ImmutableArray<MoveId> allMachineMoves = default)
    {
        if (allMachineMoves.IsDefault)
            allMachineMoves = Read<BreedingMetadataDocument>(root, "breeding-species/metadata.json").AllMachineMoves;
        if (rows.IsDefault || allMachineMoves.Length != 58 || allMachineMoves.Distinct().Count() != 58)
            throw new InvalidDataException("breeding_metadata_machine_order");
        var catalog = SharedMachineCatalog.LoadProject(root);
        var moves = allMachineMoves;
        return rows.Select(row =>
        {
            if (row is null || row.Breeding is null || row.EggMoves.IsDefault)
                throw new InvalidDataException("breeding_metadata_row");
            var compatible = Enumerable.Range(0, 58).Where(index => catalog.Compatibility(
                new ItemId(index < 50 ? $"pret:ITEM_TM{index + 1:00}" : $"pret:ITEM_HM{index - 49:00}"),
                row.Breeding.Species).CanLearn).Select(index => moves[index]).ToImmutableArray();
            return new SourceEggSpeciesRow(row.Breeding, row.BaseEggSpecies, row.EggMoves, compatible);
        }).ToImmutableArray();
    }
}
