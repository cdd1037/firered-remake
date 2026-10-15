using System.Text.Json;
using Game.Content;
using Game.Domain.Pokemon;
using Godot;

/// <summary>Owned stone targets bind inherited battlers and missing original menu art.</summary>
public partial class CampaignView
{
    private StoneEvolutionPresentationData? _stoneEvolutionArt;

    private void LoadStoneEvolutionPresentation()
    {
        if (OwnedBattlePokemonCatalog.SourceAncestor(OwnedBattlePokemonCatalog.For(_catalog.PokemonContent?.Catalog), StoneEvolutionOwnedPokemonCatalog.For) is not { } owned) return;
        if (!_session.IsCurrentGame || _qwilfishArt is null)
            throw new InvalidDataException("Stone evolution presentation requires the current Qwilfish predecessor.");
        var art = PretStoneEvolutionPresentation.Load(_root);
        foreach (var entry in art.DexEntries.Values)
            if (!owned.Catalog.Species.TryGetValue(new("pret:SPECIES_" + entry.Name), out var species) || species.SourceSpeciesId != entry.SourceSpeciesId ||
                _pokedexMenu is null || entry with { Portrait = null, Footprint = null } != _pokedexMenu.Entries[entry.SourceSpeciesId])
                throw new InvalidDataException("Stone evolution owned species or source dex binding differs.");

        void VerifyDimensions(Texture2D texture, JsonElement row)
        {            if (texture.GetWidth() != row.GetProperty("width").GetInt32() || texture.GetHeight() != row.GetProperty("height").GetInt32())
                throw new InvalidDataException("Stone evolution inherited pixels differ.");
}
        Texture2D Pixels(JsonElement row)
        {
            using var image = NativeImages.LoadPng(art.Files[row.GetProperty("texturePath").GetString()!], row.GetProperty("width").GetInt32(), row.GetProperty("height").GetInt32());
            return SceneTextures.FromImage(image);
        }
        foreach (var row in art.Manifest.GetProperty("inheritedSprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            string side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            if (!_battleArt.TryGetValue((species, side, shiny), out var existing) ||
                existing.Position != new Vector2(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32()))
                throw new InvalidDataException("Stone evolution inherited battler binding differs.");
            VerifyDimensions(existing.Texture, row);
            if (side == "front" && !shiny)
            {
                if (species is 62 or 134 or 136) _portraits.Add(species, existing.Texture);
                else if (!_portraits.TryGetValue(species, out var portrait) || !ReferenceEquals(portrait, existing.Texture))
                    throw new InvalidDataException("Stone evolution inherited portrait binding differs.");
            }
        }
        foreach (var row in art.Manifest.GetProperty("pokemonIcons").EnumerateArray())
        {
            var species = new Game.Domain.Ids.SpeciesId(row.GetProperty("speciesId").GetString()!);
            if (row.GetProperty("inherited").GetBoolean())
            {
                if (!_storageIcons.TryGetValue(species, out var existing)) throw new InvalidDataException("Stone evolution inherited icon missing.");
                VerifyDimensions(existing, row);
            }
            else _storageIcons.Add(species, Pixels(row));
        }
        foreach (var row in art.Manifest.GetProperty("footprints").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            if (row.GetProperty("inherited").GetBoolean())
            {
                if (!_route22Footprints.TryGetValue(species, out var existing)) throw new InvalidDataException("Stone evolution inherited footprint missing.");
                VerifyDimensions(existing, row);
            }
            else _route22Footprints.Add(species, Pixels(row));
        }
        _stoneEvolutionArt = art;
        Write("stoneEvolutionPresentation", new {             nativeWindowVerified = false, physicalAudioVerified = false });
    }

    private void VerifyStoneEvolutionMoveGeometry()
    {
        if (_stoneEvolutionArt is not { } art) return;
        foreach (var row in art.Manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key = (row.GetProperty("species").GetInt32(), row.GetProperty("side").GetString()!);
            if (!_sourceBattlerGeometry.TryGetValue(key, out var actual) || actual.YOffset != row.GetProperty("yOffset").GetInt32() ||
                actual.Elevation != row.GetProperty("elevation").GetInt32() || actual.SourceWidth != row.GetProperty("sourceWidth").GetInt32() ||
                actual.SourceHeight != row.GetProperty("sourceHeight").GetInt32())
                throw new InvalidDataException("Stone evolution inherited move geometry differs.");
        }
    }
    private CaptureDexEntry? StoneEvolutionCaptureDexEntry(int species) => _stoneEvolutionArt?.DexEntries.GetValueOrDefault(species) is not { } e ? null :
        new(e.SourceSpeciesId, e.NationalNumber, e.Name, e.Category, e.DisplayCategory, e.HeightDecimeters, e.WeightHectograms,
            e.HeightDisplay, e.WeightDisplay, e.Description, e.Footprint!.TexturePath, e.Portrait!.TexturePath, e.HabitatTitle);
    private PokedexMenuPage? StoneEvolutionPokedexPage(int species, PokedexMenuPage page) =>
        _stoneEvolutionArt?.DexEntries.GetValueOrDefault(species) is { } entry
            ? page with { Portrait = entry.Portrait, Footprint = page.Caught ? entry.Footprint : null } : null;
    private byte[]? ReadStoneEvolutionDexTexture(PokedexMenuTexture texture) => _stoneEvolutionArt is { } art &&
        art.DexEntries.Values.Any(e => e.Portrait == texture || e.Footprint == texture) ? art.Files[texture.TexturePath] : null;
    private string? StoneEvolutionSpeciesName(int species) => _stoneEvolutionArt?.DexEntries.GetValueOrDefault(species)?.Name;
    private void DisposeStoneEvolutionPresentation() => _stoneEvolutionArt = null;
}
