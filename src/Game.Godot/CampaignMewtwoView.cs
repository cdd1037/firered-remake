using System.Text.Json;
using Game.Content;
using Godot;

/// <summary>Mewtwo pixels for existing battle, party, storage and Pokédex consumers.</summary>
public partial class CampaignView
{
    private MewtwoPresentationData? _mewtwoArt;
    private readonly Dictionary<int, PokedexMenuEntry> _mewtwoDex = [];
    private JsonElement MewtwoManifest => _mewtwoArt?.Manifest ?? throw new InvalidDataException("Mewtwo presentation missing.");

    private void LoadMewtwoPresentation()
    {
        if (_catalog.PokemonContent?.Catalog.Species.GetValueOrDefault(new("pret:SPECIES_MEWTWO"))?.SourceSpeciesId != 150)
            throw new InvalidDataException("Mewtwo requires its complete current Celio predecessor and owned catalog entry.");
        _mewtwoArt = MewtwoPresentationAssets.Load(_root);
        Texture2D Pixels(JsonElement row, bool replicatePalette = false) => PresentationTexture(_mewtwoArt.Files, row, replicatePalette);
        foreach (var row in MewtwoManifest.GetProperty("sprites").EnumerateArray())
        {
            string side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            var texture = Pixels(row);
            _battleArt.Add((150, side, shiny), new(texture, new(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32())));
            if (side == "front" && !shiny)
            {
                _portraits.Add(150, texture);
                _mewtwoDex.Add(150, (_pokedexMenu ?? throw new InvalidDataException("Mewtwo dex metadata missing.")).Entries[150]
                    with { Portrait = SeviiRubyDexTexture(row) });
            }
        }
        foreach (var row in MewtwoManifest.GetProperty("pokemonIcons").EnumerateArray())
            _storageIcons.Add(new("pret:SPECIES_MEWTWO"), Pixels(row, true));
        foreach (var row in MewtwoManifest.GetProperty("footprints").EnumerateArray())
        {
            _route22Footprints.Add(150, Pixels(row));
            _mewtwoDex[150] = _mewtwoDex[150] with { Footprint = SeviiRubyDexTexture(row) };
        }

    }

    private CaptureDexEntry? MewtwoCaptureDexEntry(int species) => !_mewtwoDex.TryGetValue(species, out var e) ? null :
        new(e.SourceSpeciesId, e.NationalNumber, e.Name, e.Category, e.DisplayCategory, e.HeightDecimeters, e.WeightHectograms,
            e.HeightDisplay, e.WeightDisplay, e.Description, e.Footprint!.TexturePath, e.Portrait!.TexturePath,
            e.HabitatTitle);
    private byte[]? ReadMewtwoDexTexture(PokedexMenuTexture texture) =>
        _mewtwoDex.Values.Any(e => e.Portrait == texture || e.Footprint == texture) ? _mewtwoArt!.Files[texture.TexturePath] : null;

    private void VerifyMewtwoMoveGeometry()
    {
        foreach (var row in MewtwoManifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key = (150, row.GetProperty("side").GetString()!);
            if (!_sourceBattlerGeometry.TryGetValue(key, out var actual) || actual.YOffset != row.GetProperty("yOffset").GetInt32() ||
                actual.Elevation != row.GetProperty("elevation").GetInt32() || actual.SourceWidth != row.GetProperty("sourceWidth").GetInt32() ||
                actual.SourceHeight != row.GetProperty("sourceHeight").GetInt32())
                throw new InvalidDataException("Mewtwo source-move battler geometry differs.");
        }
    }

    private void DisposeMewtwoPresentation()
    {
        _mewtwoArt = null;
        _mewtwoDex.Clear();
    }
}
