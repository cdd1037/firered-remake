using Game.Content;

/// <summary>Owned stone targets bind inherited battlers and missing original menu art.</summary>
public partial class CampaignView
{
    private StoneEvolutionPresentationData? _stoneEvolutionArt;

    private void LoadStoneEvolutionPresentation()
    {
        if (_catalog.PokemonContent?.Pokemon is not { } owned) return;
        if (_qwilfishArt is null)
            throw new InvalidDataException("Stone evolution presentation requires the current Qwilfish predecessor.");
        var art = PretStoneEvolutionPresentation.Load(_root, owned.Mechanics.Catalog);
        foreach (var entry in art.DexEntries.Values)
            if (!owned.Catalog.Species.Values.Any(species => species.SourceSpeciesId == entry.SourceSpeciesId) ||
                _pokedexMenu is null || !_pokedexMenu.HasSameEntryRules(entry))
                throw new InvalidDataException("Stone evolution owned species or source dex binding differs.");

        foreach (var row in art.Manifest.GetProperty("inheritedSprites").EnumerateArray())
            RegisterOwnedBattler(row, art.Files, "Stone evolution", inherited: true,
                promotePortrait: row.GetProperty("sourceSpeciesId").GetInt32() is 62 or 134 or 136);
        RegisterOwnedIcons(art.Manifest.GetProperty("pokemonIcons"), art.Files, "Stone evolution");
        RegisterOwnedFootprints(art.Manifest.GetProperty("footprints"), art.Files, "Stone evolution");
        _stoneEvolutionArt = art;
        _supplementalDex.AddEntries(art.DexEntries.Values, art.Files);

    }

    private void VerifyStoneEvolutionMoveGeometry()
    {
        if (_stoneEvolutionArt is not { } art) return;
        VerifyInheritedMoveGeometry(art.Manifest, "Stone evolution");
    }
    private void DisposeStoneEvolutionPresentation() => _stoneEvolutionArt = null;
}
