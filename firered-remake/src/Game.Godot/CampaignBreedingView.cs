using System.Text.Json;
using Game.Content;
using Game.Domain.Pokemon;
using Godot;

/// <summary>Breeding owned species bind inherited battlers and missing original menu art.</summary>
public partial class CampaignView
{
    private BreedingPresentationData? _breedingArt;
    private readonly Dictionary<string, Texture2D> _breedingEggTextures = new(StringComparer.Ordinal);

    private void LoadBreedingPresentation()
    {
        if (OwnedBattlePokemonCatalog.SourceAncestor(OwnedBattlePokemonCatalog.For(_catalog.PokemonContent?.Catalog), BreedingOwnedPokemonCatalog.For) is not { } owned) return;
        if (!_session.IsCurrentGame || _gameCornerArt is null ||
            !ReferenceEquals(GameCornerOwnedPokemonCatalog.For(owned.Predecessor.Catalog), owned.Predecessor))
            throw new InvalidDataException("Breeding presentation requires its authenticated Game Corner predecessor.");
        var art = PretBreedingPresentation.Load(_root);
        foreach (var entry in art.DexEntries.Values)
            if (!owned.Catalog.Species.TryGetValue(new("pret:SPECIES_" + entry.Name), out var species) || species.SourceSpeciesId != entry.SourceSpeciesId ||
                _pokedexMenu is null || entry with { Portrait = null, Footprint = null } != _pokedexMenu.Entries[entry.SourceSpeciesId])
                throw new InvalidDataException("Breeding owned species or source dex binding differs.");

        void VerifyDimensions(Texture2D texture, JsonElement row)
        {            if (texture.GetWidth() != row.GetProperty("width").GetInt32() || texture.GetHeight() != row.GetProperty("height").GetInt32())
                throw new InvalidDataException("Breeding inherited pixels differ.");
}
        Texture2D Pixels(JsonElement row)
        {
            using var image = NativeImages.LoadPng(art.Files[row.GetProperty("texturePath").GetString()!], row.GetProperty("width").GetInt32(), row.GetProperty("height").GetInt32());
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
                    throw new InvalidDataException("Breeding inherited battler binding differs.");
                VerifyDimensions(existing.Texture, row);
                if (side == "front" && !shiny)
                {
                    // Tower opponent art has no owned portrait binding; reuse the exact front texture.
                    if (species == 238) _portraits.Add(species, existing.Texture);
                    else if (!_portraits.TryGetValue(species, out var portrait) || !ReferenceEquals(portrait, existing.Texture))
                        throw new InvalidDataException("Breeding inherited portrait binding differs.");
                }
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
                if (!_storageIcons.TryGetValue(species, out var existing)) throw new InvalidDataException("Breeding inherited icon missing.");
                VerifyDimensions(existing, row);
            }
            else _storageIcons.Add(species, Pixels(row));
        }
        foreach (var row in art.Manifest.GetProperty("footprints").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            if (row.GetProperty("inherited").GetBoolean())
            {
                if (!_route22Footprints.TryGetValue(species, out var existing)) throw new InvalidDataException("Breeding inherited footprint missing.");
                VerifyDimensions(existing, row);
            }
            else _route22Footprints.Add(species, Pixels(row));
        }
        foreach (var row in art.Manifest.GetProperty("eggAssets").EnumerateArray())
            _breedingEggTextures.Add(row.GetProperty("id").GetString()!, Pixels(row));
        _breedingArt = art;
        Write("breedingPresentation", new {             nativeWindowVerified = false, physicalAudioVerified = false });
    }

    private void VerifyBreedingMoveGeometry()
    {
        if (_breedingArt is not { } art) return;
        foreach (var row in art.Manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key = (row.GetProperty("species").GetInt32(), row.GetProperty("side").GetString()!);
            if (!_sourceBattlerGeometry.TryGetValue(key, out var actual) || actual.YOffset != row.GetProperty("yOffset").GetInt32() ||
                actual.Elevation != row.GetProperty("elevation").GetInt32() || actual.SourceWidth != row.GetProperty("sourceWidth").GetInt32() ||
                actual.SourceHeight != row.GetProperty("sourceHeight").GetInt32())
                throw new InvalidDataException("Breeding inherited move geometry differs.");
        }
    }
    private CaptureDexEntry? BreedingCaptureDexEntry(int species) => _breedingArt?.DexEntries.GetValueOrDefault(species) is not { } e ? null :
        new(e.SourceSpeciesId, e.NationalNumber, e.Name, e.Category, e.DisplayCategory, e.HeightDecimeters, e.WeightHectograms,
            e.HeightDisplay, e.WeightDisplay, e.Description, e.Footprint!.TexturePath, e.Portrait!.TexturePath, e.HabitatTitle);
    private PokedexMenuPage? BreedingPokedexPage(int species, PokedexMenuPage page) =>
        _breedingArt?.DexEntries.GetValueOrDefault(species) is { } entry
            ? page with { Portrait = entry.Portrait, Footprint = page.Caught ? entry.Footprint : null } : null;
    private byte[]? ReadBreedingDexTexture(PokedexMenuTexture texture) => _breedingArt is { } art &&
        art.DexEntries.Values.Any(e => e.Portrait == texture || e.Footprint == texture) ? art.Files[texture.TexturePath] : null;
    private string? BreedingSpeciesName(int species) => _breedingArt?.DexEntries.GetValueOrDefault(species)?.Name;
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
