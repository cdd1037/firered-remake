using System.Text.Json;
using Game.Content;
using Godot;

/// <summary>Owned Qwilfish uses the exact existing Tower battlers and ordinary shared menus.</summary>
public partial class CampaignView
{
    private QwilfishPresentationData? _qwilfishArt;

    private void LoadQwilfishPresentation()
    {
        if (_catalog.PokemonContent?.Catalog.Species.ContainsKey(new("pret:SPECIES_QWILFISH")) != true) return;
        if (!_session.IsCurrentGame || !_catalog.TrainerTowerEnabled || !_catalog.SeafoamEnabled || _route22Footprints.Count != 137)
            throw new InvalidDataException("Qwilfish ownership requires the current Seafoam/Tower presentation predecessor.");
        var art = PretQwilfishPresentation.Load(_root);
        int battleCount = _battleArt.Count;
        foreach (var row in art.Manifest.GetProperty("inheritedSprites").EnumerateArray())
        {
            string side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            if (!_battleArt.TryGetValue((211, side, shiny), out var existing) ||
                existing.Position != new Vector2(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32()))
                throw new InvalidDataException("Qwilfish inherited Tower battler binding differs.");
            using var image = existing.Texture.GetImage(); image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != 64 || image.GetHeight() != 64 ||
                !image.GetData().SequenceEqual(art.Files[row.GetProperty("rgbaPath").GetString()!]))
                throw new InvalidDataException("Qwilfish inherited Tower pixels differ.");
            if (side == "front" && !shiny) _portraits.Add(211, existing.Texture);
        }
        Texture2D Pixels(JsonElement row)
        {
            using var image = Image.CreateFromData(row.GetProperty("width").GetInt32(), row.GetProperty("height").GetInt32(),
                false, Image.Format.Rgba8, art.Files[row.GetProperty("rgbaPath").GetString()!]);
            return SceneTextures.FromImage(image);
        }
        _storageIcons.Add(new("pret:SPECIES_QWILFISH"), Pixels(art.Manifest.GetProperty("pokemonIcons")[0]));
        _route22Footprints.Add(211, Pixels(art.Manifest.GetProperty("footprints")[0]));
        var entry = art.DexEntries[211];
        if (_pokedexMenu is null || entry with { Portrait = null, Footprint = null } != _pokedexMenu.Entries[211] ||
            _route22Footprints.Count != 138 || _battleArt.Count != battleCount)
            throw new InvalidDataException("Qwilfish shared presentation inventory differs.");
        _qwilfishArt = art;
        Write("qwilfishPresentation", new { PretQwilfishPresentation.ManifestSha256, inheritedBattleSprites = 4,
            newBattleSprites = 0, newIcons = 1, newFootprints = 1, dexEntries = 1, inheritedNormalCry = true,
            newMoveAnimations = 0, nativeWindowVerified = false, physicalAudioVerified = false });
    }

    private void VerifyQwilfishMoveGeometry()
    {
        if (_qwilfishArt is not { } art) return;
        foreach (var row in art.Manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key = (211, row.GetProperty("side").GetString()!);
            if (!_sourceBattlerGeometry.TryGetValue(key, out var actual) || actual.YOffset != row.GetProperty("yOffset").GetInt32() ||
                actual.Elevation != row.GetProperty("elevation").GetInt32() || actual.SourceWidth != row.GetProperty("sourceWidth").GetInt32() ||
                actual.SourceHeight != row.GetProperty("sourceHeight").GetInt32())
                throw new InvalidDataException("Qwilfish inherited Tower move geometry differs.");
        }
    }
    private CaptureDexEntry? QwilfishCaptureDexEntry(int species) => _qwilfishArt?.DexEntries.GetValueOrDefault(species) is not { } e ? null :
        new(e.SourceSpeciesId, e.NationalNumber, e.Name, e.Category, e.DisplayCategory, e.HeightDecimeters, e.WeightHectograms,
            e.HeightDisplay, e.WeightDisplay, e.Description, e.Footprint!.TexturePath, e.Portrait!.TexturePath, e.HabitatTitle, e.Source, e.DescriptionSource);
    private PokedexMenuPage? QwilfishPokedexPage(int species, PokedexMenuPage page) =>
        _qwilfishArt?.DexEntries.GetValueOrDefault(species) is { } entry
            ? page with { Portrait = entry.Portrait, Footprint = page.Caught ? entry.Footprint : null } : null;
    private byte[]? ReadQwilfishDexTexture(PokedexMenuTexture texture) => _qwilfishArt is { } art &&
        art.DexEntries.Values.Any(e => e.Portrait == texture || e.Footprint == texture) ? art.Files[texture.TexturePath] : null;
    private string? QwilfishSpeciesName(int species) => _qwilfishArt?.DexEntries.GetValueOrDefault(species)?.Name;
    private void DisposeQwilfishPresentation() => _qwilfishArt = null;
}
