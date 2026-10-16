using System.Text.Json;
using Game.Content;
using Game.Domain.Pokemon;
using Godot;

/// <summary>GiftEgg owned species bind inherited battlers and missing original menu art.</summary>
public partial class CampaignView
{
    private GiftEggPresentationData? _giftEggArt;

    private void LoadGiftEggPresentation()
    {
        if (_catalog.PokemonContent?.Pokemon is not { } owned) return;
        if (_ordinaryEvolutionArt is null)
            throw new InvalidDataException("GiftEgg presentation requires its authenticated Lost Cave predecessor.");
        var art = PretGiftEggPresentation.Load(_root);
        foreach (var entry in art.DexEntries.Values)
            if (!owned.Catalog.Species.TryGetValue(new("pret:SPECIES_" + entry.Name), out var species) || species.SourceSpeciesId != entry.SourceSpeciesId ||
                _pokedexMenu is null || entry with { Portrait = null, Footprint = null } != _pokedexMenu.Entries[entry.SourceSpeciesId])
                throw new InvalidDataException("GiftEgg owned species or source dex binding differs.");

        void VerifyDimensions(Texture2D texture, JsonElement row)
        {            if (texture.GetWidth() != row.GetProperty("width").GetInt32() || texture.GetHeight() != row.GetProperty("height").GetInt32())
                throw new InvalidDataException("GiftEgg inherited pixels differ.");
}
        Texture2D Pixels(JsonElement row) => PresentationTexture(art.Files, row);
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
                    throw new InvalidDataException("GiftEgg inherited battler binding differs.");
                VerifyDimensions(existing.Texture, row);
                if (side == "front" && !shiny)
                {
                    // Tower opponent art has no owned portrait binding; reuse the exact front texture.
                    if (species == 175) _portraits.Add(species, existing.Texture);
                    else if (!_portraits.TryGetValue(species, out var portrait) || !ReferenceEquals(portrait, existing.Texture))
                        throw new InvalidDataException("GiftEgg inherited portrait binding differs.");
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
                if (!_storageIcons.TryGetValue(species, out var existing)) throw new InvalidDataException("GiftEgg inherited icon missing.");
                VerifyDimensions(existing, row);
            }
            else _storageIcons.Add(species, Pixels(row));
        }
        foreach (var row in art.Manifest.GetProperty("footprints").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            if (row.GetProperty("inherited").GetBoolean())
            {
                if (!_route22Footprints.TryGetValue(species, out var existing)) throw new InvalidDataException("GiftEgg inherited footprint missing.");
                VerifyDimensions(existing, row);
            }
            else _route22Footprints.Add(species, Pixels(row));
        }
        _giftEggArt = art;

    }

    private void VerifyGiftEggMoveGeometry()
    {
        if (_giftEggArt is not { } art) return;
        foreach (var row in art.Manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key = (row.GetProperty("species").GetInt32(), row.GetProperty("side").GetString()!);
            if (!_sourceBattlerGeometry.TryGetValue(key, out var actual) || actual.YOffset != row.GetProperty("yOffset").GetInt32() ||
                actual.Elevation != row.GetProperty("elevation").GetInt32() || actual.SourceWidth != row.GetProperty("sourceWidth").GetInt32() ||
                actual.SourceHeight != row.GetProperty("sourceHeight").GetInt32())
                throw new InvalidDataException("GiftEgg inherited move geometry differs.");
        }
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
