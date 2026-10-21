using Game.Content;

/// <summary>Ordinary evolution targets bind inherited battlers and missing original menu art.</summary>
public partial class CampaignView
{
    private OrdinaryEvolutionPresentationData? _ordinaryEvolutionArt;

    private void LoadOrdinaryEvolutionPresentation()
    {
        if (_catalog.PokemonContent?.Pokemon is not { } owned) return;
        if (_breedingArt is null)
            throw new InvalidDataException("Ordinary evolution presentation requires its authenticated Breeding predecessor.");
        var art = PretOrdinaryEvolutionPresentation.Load(_root, owned.Mechanics.Catalog);
        foreach (var entry in art.DexEntries.Values)
            if (!owned.Catalog.Species.Values.Any(species => species.SourceSpeciesId == entry.SourceSpeciesId) ||
                _pokedexMenu is null || !_pokedexMenu.HasSameEntryRules(entry))
                throw new InvalidDataException("Ordinary evolution owned species or source dex binding differs.");

        foreach (var row in art.Manifest.GetProperty("inheritedSprites").EnumerateArray())
            RegisterOwnedBattler(row, art.Files, "Ordinary evolution", inherited: true,
                promotePortrait: row.GetProperty("sourceSpeciesId").GetInt32() is 242);
        RegisterOwnedIcons(art.Manifest.GetProperty("pokemonIcons"), art.Files, "Ordinary evolution");
        RegisterOwnedFootprints(art.Manifest.GetProperty("footprints"), art.Files, "Ordinary evolution");
        _ordinaryEvolutionArt = art;
        _supplementalDex.AddEntries(art.DexEntries.Values, art.Files);

    }

    private void VerifyOrdinaryEvolutionMoveGeometry()
    {
        if (_ordinaryEvolutionArt is not { } art) return;
        VerifyInheritedMoveGeometry(art.Manifest, "Ordinary evolution");
    }
    private void DisposeOrdinaryEvolutionPresentation() => _ordinaryEvolutionArt = null;
}
