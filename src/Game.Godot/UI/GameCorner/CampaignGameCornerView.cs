using Game.Content;

/// <summary>Game Corner owned species bind inherited battlers and missing original menu art.</summary>
public partial class CampaignView
{
    private GameCornerPresentationData? _gameCornerArt;

    private void LoadGameCornerPresentation()
    {
        if (_catalog.PokemonContent?.Pokemon is not { } owned) return;
        if (_stoneEvolutionArt is null)
            throw new InvalidDataException("Game Corner presentation requires its authenticated Stone Evolution predecessor.");
        var art = PretGameCornerPresentation.Load(_root, owned.Mechanics.Catalog);
        foreach (var entry in art.DexEntries.Values)
            if (!owned.Catalog.Species.Values.Any(species => species.SourceSpeciesId == entry.SourceSpeciesId) ||
                _pokedexMenu is null || !_pokedexMenu.HasSameEntryRules(entry))
                throw new InvalidDataException("Game Corner owned species or source dex binding differs.");

        foreach (var row in art.Manifest.GetProperty("sprites").EnumerateArray())
            RegisterOwnedBattler(row, art.Files, "Game Corner", row.GetProperty("inherited").GetBoolean());
        RegisterOwnedIcons(art.Manifest.GetProperty("pokemonIcons"), art.Files, "Game Corner");
        RegisterOwnedFootprints(art.Manifest.GetProperty("footprints"), art.Files, "Game Corner");
        _gameCornerArt = art;
        _supplementalDex.AddEntries(art.DexEntries.Values, art.Files);

    }

    private void VerifyGameCornerMoveGeometry()
    {
        if (_gameCornerArt is not { } art) return;
        VerifyInheritedMoveGeometry(art.Manifest, "Game Corner");
    }
    private void DisposeGameCornerPresentation() => _gameCornerArt = null;
}
