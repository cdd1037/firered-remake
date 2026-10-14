using System.Text.Json;
using Game.Content;
using Game.Domain.Pokemon;
using Godot;

/// <summary>Ordinary evolution targets bind exact inherited battlers and missing original menu art.</summary>
public partial class CampaignView
{
    private OrdinaryEvolutionPresentationData? _ordinaryEvolutionArt;

    private void LoadOrdinaryEvolutionPresentation()
    {
        if (OwnedBattlePokemonCatalog.SourceAncestor(OwnedBattlePokemonCatalog.For(_catalog.PokemonContent?.Catalog), OrdinaryEvolutionOwnedPokemonCatalog.For) is not { } owned) return;
        if (!_session.IsCurrentGame || _breedingArt is null ||
            !ReferenceEquals(BreedingOwnedPokemonCatalog.For(owned.Predecessor.Catalog), owned.Predecessor))
            throw new InvalidDataException("Ordinary evolution presentation requires its authenticated Breeding predecessor.");
        var art = PretOrdinaryEvolutionPresentation.Load(_root);
        int battleCount = _battleArt.Count, iconCount = _storageIcons.Count, portraitCount = _portraits.Count, footprintCount = _route22Footprints.Count;
        foreach (var entry in art.DexEntries.Values)
            if (!owned.Catalog.Species.TryGetValue(new("pret:SPECIES_" + entry.Name), out var species) || species.SourceSpeciesId != entry.SourceSpeciesId ||
                _pokedexMenu is null || entry with { Portrait = null, Footprint = null } != _pokedexMenu.Entries[entry.SourceSpeciesId])
                throw new InvalidDataException("Ordinary evolution owned species or source dex binding differs.");

        void VerifyPixels(Texture2D texture, JsonElement row, bool icon = false)
        {
            var rgba = art.Files[row.GetProperty("rgbaPath").GetString()!];
            if (icon && row.GetProperty("sourceSpeciesId").GetInt32() == 80)
            {
                // The existing Saffron Slowbro icon apply source RGB5 expansion at load time.
                rgba = rgba.ToArray();
                for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                    for (int c = 0; c < 3; c++) { int value = rgba[i + c] >> 3; rgba[i + c] = (byte)((value << 3) | (value >> 2)); }
            }
            using var image = texture.GetImage(); image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != row.GetProperty("width").GetInt32() || image.GetHeight() != row.GetProperty("height").GetInt32() ||
                !image.GetData().SequenceEqual(rgba))
                throw new InvalidDataException("Ordinary evolution inherited pixels differ.");
        }
        Texture2D Pixels(JsonElement row)
        {
            using var image = Image.CreateFromData(row.GetProperty("width").GetInt32(), row.GetProperty("height").GetInt32(),
                false, Image.Format.Rgba8, art.Files[row.GetProperty("rgbaPath").GetString()!]);
            return SceneTextures.FromImage(image);
        }
        foreach (var row in art.Manifest.GetProperty("inheritedSprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            string side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            if (!_battleArt.TryGetValue((species, side, shiny), out var existing) ||
                existing.Position != new Vector2(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32()))
                throw new InvalidDataException("Ordinary evolution inherited battler binding differs.");
            VerifyPixels(existing.Texture, row);
            if (side == "front" && !shiny)
            {
                if (species == 242) _portraits.Add(species, existing.Texture);
                else if (!_portraits.TryGetValue(species, out var portrait) || !ReferenceEquals(portrait, existing.Texture))
                    throw new InvalidDataException("Ordinary evolution inherited portrait binding differs.");
            }
        }
        foreach (var row in art.Manifest.GetProperty("pokemonIcons").EnumerateArray())
        {
            var species = new Game.Domain.Ids.SpeciesId(row.GetProperty("speciesId").GetString()!);
            if (row.GetProperty("inherited").GetBoolean())
            {
                if (!_storageIcons.TryGetValue(species, out var existing)) throw new InvalidDataException("Ordinary evolution inherited icon missing.");
                VerifyPixels(existing, row, icon: true);
            }
            else _storageIcons.Add(species, Pixels(row));
        }
        foreach (var row in art.Manifest.GetProperty("footprints").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            if (row.GetProperty("inherited").GetBoolean())
            {
                if (!_route22Footprints.TryGetValue(species, out var existing)) throw new InvalidDataException("Ordinary evolution inherited footprint missing.");
                VerifyPixels(existing, row);
            }
            else _route22Footprints.Add(species, Pixels(row));
        }
        if (_battleArt.Count != battleCount || _storageIcons.Count != iconCount + 2 || _portraits.Count != portraitCount + 1 || _route22Footprints.Count != footprintCount + 3)
            throw new InvalidDataException("Ordinary evolution shared presentation inventory differs.");
        _ordinaryEvolutionArt = art;
        Write("ordinaryEvolutionPresentation", new { PretOrdinaryEvolutionPresentation.ManifestSha256, inheritedBattleSprites = 16,
            newBattleSprites = 0, newIcons = 2, inheritedIcons = 2, newFootprints = 3, inheritedFootprints = 1,
            dexEntries = 4, newPortraitBindings = 1, inheritedNormalCries = 4, newAudioCues = 0, newMoveAnimations = 0,
            nativeWindowVerified = false, physicalAudioVerified = false });
    }

    private void VerifyOrdinaryEvolutionMoveGeometry()
    {
        if (_ordinaryEvolutionArt is not { } art) return;
        foreach (var row in art.Manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key = (row.GetProperty("species").GetInt32(), row.GetProperty("side").GetString()!);
            if (!_sourceBattlerGeometry.TryGetValue(key, out var actual) || actual.YOffset != row.GetProperty("yOffset").GetInt32() ||
                actual.Elevation != row.GetProperty("elevation").GetInt32() || actual.SourceWidth != row.GetProperty("sourceWidth").GetInt32() ||
                actual.SourceHeight != row.GetProperty("sourceHeight").GetInt32())
                throw new InvalidDataException("Ordinary evolution inherited move geometry differs.");
        }
    }
    private CaptureDexEntry? OrdinaryEvolutionCaptureDexEntry(int species) => _ordinaryEvolutionArt?.DexEntries.GetValueOrDefault(species) is not { } e ? null :
        new(e.SourceSpeciesId, e.NationalNumber, e.Name, e.Category, e.DisplayCategory, e.HeightDecimeters, e.WeightHectograms,
            e.HeightDisplay, e.WeightDisplay, e.Description, e.Footprint!.TexturePath, e.Portrait!.TexturePath, e.HabitatTitle, e.Source, e.DescriptionSource);
    private PokedexMenuPage? OrdinaryEvolutionPokedexPage(int species, PokedexMenuPage page) =>
        _ordinaryEvolutionArt?.DexEntries.GetValueOrDefault(species) is { } entry
            ? page with { Portrait = entry.Portrait, Footprint = page.Caught ? entry.Footprint : null } : null;
    private byte[]? ReadOrdinaryEvolutionDexTexture(PokedexMenuTexture texture) => _ordinaryEvolutionArt is { } art &&
        art.DexEntries.Values.Any(e => e.Portrait == texture || e.Footprint == texture) ? art.Files[texture.TexturePath] : null;
    private string? OrdinaryEvolutionSpeciesName(int species) => _ordinaryEvolutionArt?.DexEntries.GetValueOrDefault(species)?.Name;
    private void DisposeOrdinaryEvolutionPresentation() => _ordinaryEvolutionArt = null;
}
