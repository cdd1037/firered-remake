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
        var art = PretQwilfishPresentation.Load(_root);
        foreach (var row in art.Manifest.GetProperty("inheritedSprites").EnumerateArray())
        {
            string side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            if (!_battleArt.TryGetValue((211, side, shiny), out var existing) ||
                existing.Position != new Vector2(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32()))
                throw new InvalidDataException("Qwilfish inherited Tower battler binding differs.");
            using var image = existing.Texture.GetImage(); image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != 64 || image.GetHeight() != 64)
                throw new InvalidDataException("Qwilfish inherited Tower pixels differ.");
            if (side == "front" && !shiny) _portraits.Add(211, existing.Texture);
        }
        Texture2D Pixels(JsonElement row) => PresentationTexture(art.Files, row);
        _storageIcons.Add(new("pret:SPECIES_QWILFISH"), Pixels(art.Manifest.GetProperty("pokemonIcons")[0]));
        _route22Footprints.Add(211, Pixels(art.Manifest.GetProperty("footprints")[0]));
        var entry = art.DexEntries[211];
        if (_pokedexMenu is null || entry with { Portrait = null, Footprint = null } != _pokedexMenu.Entries[211])
            throw new InvalidDataException("Qwilfish shared presentation inventory differs.");
        _qwilfishArt = art;
        _supplementalDex.AddEntries(art.DexEntries.Values, art.Files);

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
    private void DisposeQwilfishPresentation() => _qwilfishArt = null;
}
