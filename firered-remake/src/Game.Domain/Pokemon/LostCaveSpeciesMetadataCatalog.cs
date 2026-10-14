namespace Game.Domain.Pokemon;

/// <summary>One exact source metadata row, preserving all 186 prior definitions and 58 inheritance moves.</summary>
public static class LostCaveSpeciesMetadataCatalog
{
    public static BreedingSpeciesMetadataCatalog Authenticate(LostCaveOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(LostCaveOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            BreedingSpeciesMetadataCatalog.For(owned.Predecessor.Owned.Catalog) is not { } previous ||
            previous.Species.Count != 186 || previous.AllMachineMoves.Length != 58)
            throw new ArgumentException("lost_cave_metadata_owner");
        var row = LostCaveSpeciesSource.Metadata;
        if (row.EggMoves.Concat(row.MachineMoves).Any(m => !owned.Catalog.Moves.ContainsKey(m)) ||
            row.MachineMoves.Any(m => !previous.AllMachineMoves.Contains(m)))
            throw new ArgumentException("lost_cave_metadata_source");
        return BreedingSpeciesMetadataCatalog.RegisterSuccessor(owned.SharedBattle, previous, [row]);
    }
}
