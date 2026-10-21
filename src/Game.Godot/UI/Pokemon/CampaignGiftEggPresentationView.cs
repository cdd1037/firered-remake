using Game.Content;
using Game.Domain.Pokemon;

/// <summary>GiftEgg owned species bind inherited battlers and missing original menu art.</summary>
public partial class CampaignView
{
    private GiftEggPresentationData? _giftEggArt;

    private void LoadGiftEggPresentation()
    {
        if (_catalog.PokemonContent?.Pokemon is not { } owned) return;
        if (_ordinaryEvolutionArt is null)
            throw new InvalidDataException("GiftEgg presentation requires its authenticated Lost Cave predecessor.");
        var art = PretGiftEggPresentation.Load(_root, owned.Mechanics.Catalog);
        foreach (var entry in art.DexEntries.Values)
            if (!owned.Catalog.Species.Values.Any(species => species.SourceSpeciesId == entry.SourceSpeciesId) ||
                _pokedexMenu is null || !_pokedexMenu.HasSameEntryRules(entry))
                throw new InvalidDataException("GiftEgg owned species or source dex binding differs.");

        foreach (var row in art.Manifest.GetProperty("sprites").EnumerateArray())
            RegisterOwnedBattler(row, art.Files, "GiftEgg", row.GetProperty("inherited").GetBoolean(),
                promotePortrait: row.GetProperty("sourceSpeciesId").GetInt32() is 175);
        RegisterOwnedIcons(art.Manifest.GetProperty("pokemonIcons"), art.Files, "GiftEgg");
        RegisterOwnedFootprints(art.Manifest.GetProperty("footprints"), art.Files, "GiftEgg");
        _giftEggArt = art;

    }

    private void VerifyGiftEggMoveGeometry()
    {
        if (_giftEggArt is not { } art) return;
        VerifyInheritedMoveGeometry(art.Manifest, "GiftEgg");
    }
    private CaptureDexEntry? GiftEggCaptureDexEntry(int species) => _giftEggArt?.DexEntries.GetValueOrDefault(species) is not { } e ? null :
        new(e.SourceSpeciesId, e.NationalNumber, e.Name, e.Category, e.DisplayCategory, e.HeightDecimeters, e.WeightHectograms,
            e.HeightDisplay, e.WeightDisplay, e.Description, e.Footprint!.TexturePath, e.Portrait!.TexturePath, e.HabitatTitle);
    private PokedexMenuPage? GiftEggPokedexPage(int species, PokedexMenuPage page) =>
        _giftEggArt?.DexEntries.GetValueOrDefault(species) is { } entry
            ? page with { Portrait = entry.Portrait, Footprint = page.Caught ? entry.Footprint : null } : null;
    private byte[]? ReadGiftEggDexTexture(PokedexMenuTexture texture) => _giftEggArt is { } art &&
        art.DexEntries.Values.Any(e => e.Portrait == texture || e.Footprint == texture) ? art.Files[texture.TexturePath] : null;
    private void DisposeGiftEggPresentation() => _giftEggArt = null;
}
