using Game.Content;
using Godot;

/// <summary>Breeding owned species bind inherited battlers and missing original menu art.</summary>
public partial class CampaignView
{
    private BreedingPresentationData? _breedingArt;
    private readonly Dictionary<string, Texture2D> _breedingEggTextures = new(StringComparer.Ordinal);

    private void LoadBreedingPresentation()
    {
        if (_catalog.PokemonContent?.Pokemon is not { } owned) return;
        if (_gameCornerArt is null)
            throw new InvalidDataException("Breeding presentation requires its authenticated Game Corner predecessor.");
        var art = PretBreedingPresentation.Load(_root, owned.Mechanics.Catalog);
        foreach (var entry in art.DexEntries.Values)
            if (!owned.Catalog.Species.Values.Any(species => species.SourceSpeciesId == entry.SourceSpeciesId) ||
                _pokedexMenu is null || !_pokedexMenu.HasSameEntryRules(entry))
                throw new InvalidDataException("Breeding owned species or source dex binding differs.");

        foreach (var row in art.Manifest.GetProperty("sprites").EnumerateArray())
            RegisterOwnedBattler(row, art.Files, "Breeding", row.GetProperty("inherited").GetBoolean(),
                promotePortrait: row.GetProperty("sourceSpeciesId").GetInt32() is 238);
        RegisterOwnedIcons(art.Manifest.GetProperty("pokemonIcons"), art.Files, "Breeding");
        RegisterOwnedFootprints(art.Manifest.GetProperty("footprints"), art.Files, "Breeding");
        foreach (var row in art.Manifest.GetProperty("eggAssets").EnumerateArray())
            _breedingEggTextures.Add(row.GetProperty("id").GetString()!, PresentationTexture(art.Files, row));
        _breedingArt = art;
        _supplementalDex.AddEntries(art.DexEntries.Values, art.Files);

    }

    private void VerifyBreedingMoveGeometry()
    {
        if (_breedingArt is not { } art) return;
        VerifyInheritedMoveGeometry(art.Manifest, "Breeding");
    }
    // Callers must select these assets using the authoritative Egg marker before looking up real species.
    // No species parameter is accepted, so an Egg texture cannot disclose its hidden species or shiny flag.
    private Texture2D BreedingEggShell() => _breedingEggTextures["egg-front"];
    private Texture2D BreedingEggIcon(int frame = 0) => frame is >= 0 and < 2 ? _breedingEggTextures["egg-icon-" + frame]
        : throw new ArgumentOutOfRangeException(nameof(frame));
    private Texture2D BreedingEggHatchFrame(int frame) => frame is >= 0 and < 4 ? _breedingEggTextures["egg-hatch-" + frame]
        : throw new ArgumentOutOfRangeException(nameof(frame));
    private Texture2D BreedingEggShard(int frame) => frame is >= 0 and < 4 ? _breedingEggTextures["egg-shard-" + frame]
        : throw new ArgumentOutOfRangeException(nameof(frame));
    private void DisposeBreedingPresentation() { _breedingArt = null; _breedingEggTextures.Clear(); }
}
