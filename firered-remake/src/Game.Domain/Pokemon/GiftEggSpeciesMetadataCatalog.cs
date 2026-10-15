using System.Collections.Immutable;
namespace Game.Domain.Pokemon;

/// <summary>Two-row source delta over the existing breeding metadata; no full metadata copy.</summary>
public static class GiftEggSpeciesMetadataCatalog
{
    public static BreedingSpeciesMetadataCatalog Authenticate(GiftEggOwnedPokemonCatalog owned,
        ImmutableArray<SourceEggSpeciesRow> additions)
    {
        ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(GiftEggOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            BreedingSpeciesMetadataCatalog.For(owned.Predecessor.Catalog) is not { } predecessor ||
            additions.IsDefault || additions.Length != 2 || additions.Any(r => r is null || r.Breeding is null || r.EggMoves.IsDefault || r.MachineMoves.IsDefault) ||
            !additions.Select(r => r.Breeding.Species).SequenceEqual(GiftEggOwnedPokemonCatalog.AdditionalSpecies))
            throw new ArgumentException("gift_egg_metadata_source_or_owner");
        foreach (var row in additions)
            if (!owned.Catalog.Species.ContainsKey(row.BaseEggSpecies) || row.Breeding.EggGroup1 is < 0 or > 15 ||
                row.Breeding.EggGroup2 is < 0 or > 15 || row.Breeding.EggCycles == 0 ||
                row.EggMoves.Concat(row.MachineMoves).Any(m => !owned.Catalog.Moves.ContainsKey(m)) ||
                row.MachineMoves.Any(m => !predecessor.AllMachineMoves.Contains(m)))
                throw new ArgumentException("gift_egg_metadata_references");
        return BreedingSpeciesMetadataCatalog.RegisterSuccessor(owned.SharedBattle, predecessor, additions);
    }
}
