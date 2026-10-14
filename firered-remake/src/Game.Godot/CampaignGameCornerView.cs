using System.Text.Json;
using Game.Content;
using Game.Domain.Pokemon;
using Godot;

/// <summary>Game Corner owned species bind exact inherited battlers and missing original menu art.</summary>
public partial class CampaignView
{
    private GameCornerPresentationData? _gameCornerArt;

    private void LoadGameCornerPresentation()
    {
        if (GameCornerOwnedPokemonCatalog.For(_catalog.PokemonContent?.Catalog) is not { } owned) return;
        if (!_session.IsCurrentGame || _stoneEvolutionArt is null ||
            !ReferenceEquals(StoneEvolutionOwnedPokemonCatalog.For(owned.Predecessor.Catalog), owned.Predecessor))
            throw new InvalidDataException("Game Corner presentation requires its authenticated Stone Evolution predecessor.");
        var art = PretGameCornerPresentation.Load(_root);
        int battleCount = _battleArt.Count, iconCount = _storageIcons.Count, portraitCount = _portraits.Count, footprintCount = _route22Footprints.Count;
        foreach (var entry in art.DexEntries.Values)
            if (!owned.Catalog.Species.TryGetValue(new("pret:SPECIES_" + entry.Name), out var species) || species.SourceSpeciesId != entry.SourceSpeciesId ||
                _pokedexMenu is null || entry with { Portrait = null, Footprint = null } != _pokedexMenu.Entries[entry.SourceSpeciesId])
                throw new InvalidDataException("Game Corner owned species or source dex binding differs.");

        void VerifyPixels(Texture2D texture, JsonElement row, bool icon = false)
        {
            var rgba = art.Files[row.GetProperty("rgbaPath").GetString()!];
            if (icon)
            {
                // The existing Five Island Porygon icon applies source RGB5 expansion at load time.
                rgba = rgba.ToArray();
                for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                    for (int c = 0; c < 3; c++) { int value = rgba[i + c] >> 3; rgba[i + c] = (byte)((value << 3) | (value >> 2)); }
            }
            using var image = texture.GetImage(); image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != row.GetProperty("width").GetInt32() || image.GetHeight() != row.GetProperty("height").GetInt32() ||
                !image.GetData().SequenceEqual(rgba))
                throw new InvalidDataException("Game Corner inherited pixels differ.");
        }
        Texture2D Pixels(JsonElement row)
        {
            using var image = Image.CreateFromData(row.GetProperty("width").GetInt32(), row.GetProperty("height").GetInt32(),
                false, Image.Format.Rgba8, art.Files[row.GetProperty("rgbaPath").GetString()!]);
            return SceneTextures.FromImage(image);
        }
        foreach (var row in art.Manifest.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            string side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            var key = (species, side, shiny);
            var position = new Vector2(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32());
            if (row.GetProperty("inherited").GetBoolean())
            {
                if (!_battleArt.TryGetValue(key, out var existing) || existing.Position != position)
                    throw new InvalidDataException("Game Corner inherited battler binding differs.");
                VerifyPixels(existing.Texture, row);
                if (side == "front" && !shiny && (!_portraits.TryGetValue(species, out var portrait) || !ReferenceEquals(portrait, existing.Texture)))
                    throw new InvalidDataException("Game Corner inherited portrait binding differs.");
            }
            else
            {
                var texture = Pixels(row);
                _battleArt.Add(key, new(texture, position));
                if (side == "front" && !shiny) _portraits.Add(species, texture);
            }
        }
        foreach (var row in art.Manifest.GetProperty("pokemonIcons").EnumerateArray())
        {
            var species = new Game.Domain.Ids.SpeciesId(row.GetProperty("speciesId").GetString()!);
            if (row.GetProperty("inherited").GetBoolean())
            {
                if (!_storageIcons.TryGetValue(species, out var existing)) throw new InvalidDataException("Game Corner inherited icon missing.");
                VerifyPixels(existing, row, icon: row.GetProperty("runtimePaletteExpansion").GetString() != "stored");
            }
            else _storageIcons.Add(species, Pixels(row));
        }
        foreach (var row in art.Manifest.GetProperty("footprints").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            if (row.GetProperty("inherited").GetBoolean())
            {
                if (!_route22Footprints.TryGetValue(species, out var existing)) throw new InvalidDataException("Game Corner inherited footprint missing.");
                VerifyPixels(existing, row);
            }
            else _route22Footprints.Add(species, Pixels(row));
        }
        if (_battleArt.Count != battleCount + 4 || _storageIcons.Count != iconCount + 1 || _portraits.Count != portraitCount + 1 || _route22Footprints.Count != footprintCount + 4)
            throw new InvalidDataException("Game Corner shared presentation inventory differs.");
        _gameCornerArt = art;
        Write("gameCornerPresentation", new { PretGameCornerPresentation.ManifestSha256, inheritedBattleSprites = 12,
            newBattleSprites = 4, newIcons = 1, inheritedIcons = 3, newFootprints = 4, inheritedFootprints = 0,
            dexEntries = 4, inheritedNormalCries = 4, newAudioCues = 1, newMoveAnimations = 0,
            nativeWindowVerified = false, physicalAudioVerified = false });
    }

    private void VerifyGameCornerMoveGeometry()
    {
        if (_gameCornerArt is not { } art) return;
        foreach (var row in art.Manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key = (row.GetProperty("species").GetInt32(), row.GetProperty("side").GetString()!);
            if (!_sourceBattlerGeometry.TryGetValue(key, out var actual) || actual.YOffset != row.GetProperty("yOffset").GetInt32() ||
                actual.Elevation != row.GetProperty("elevation").GetInt32() || actual.SourceWidth != row.GetProperty("sourceWidth").GetInt32() ||
                actual.SourceHeight != row.GetProperty("sourceHeight").GetInt32())
                throw new InvalidDataException("Game Corner inherited move geometry differs.");
        }
    }
    private CaptureDexEntry? GameCornerCaptureDexEntry(int species) => _gameCornerArt?.DexEntries.GetValueOrDefault(species) is not { } e ? null :
        new(e.SourceSpeciesId, e.NationalNumber, e.Name, e.Category, e.DisplayCategory, e.HeightDecimeters, e.WeightHectograms,
            e.HeightDisplay, e.WeightDisplay, e.Description, e.Footprint!.TexturePath, e.Portrait!.TexturePath, e.HabitatTitle, e.Source, e.DescriptionSource);
    private PokedexMenuPage? GameCornerPokedexPage(int species, PokedexMenuPage page) =>
        _gameCornerArt?.DexEntries.GetValueOrDefault(species) is { } entry
            ? page with { Portrait = entry.Portrait, Footprint = page.Caught ? entry.Footprint : null } : null;
    private byte[]? ReadGameCornerDexTexture(PokedexMenuTexture texture) => _gameCornerArt is { } art &&
        art.DexEntries.Values.Any(e => e.Portrait == texture || e.Footprint == texture) ? art.Files[texture.TexturePath] : null;
    private string? GameCornerSpeciesName(int species) => _gameCornerArt?.DexEntries.GetValueOrDefault(species)?.Name;
    private void DisposeGameCornerPresentation() => _gameCornerArt = null;
}
