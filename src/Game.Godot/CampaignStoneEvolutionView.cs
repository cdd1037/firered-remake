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
        if (_catalog.PokemonContent?.Pokemon is not { } owned) return;
        if (_qwilfishArt is null)
            throw new InvalidDataException("Stone evolution presentation requires the current Qwilfish predecessor.");
        var art = PretStoneEvolutionPresentation.Load(_root, owned.Mechanics.Catalog);
        foreach (var entry in art.DexEntries.Values)
            if (!owned.Catalog.Species.Values.Any(species => species.SourceSpeciesId == entry.SourceSpeciesId) ||
                _pokedexMenu is null || entry with { Portrait = null, Footprint = null } != _pokedexMenu.Entries[entry.SourceSpeciesId])
                throw new InvalidDataException("Stone evolution owned species or source dex binding differs.");

        void VerifyDimensions(Texture2D texture, JsonElement row)
        {            if (texture.GetWidth() != row.GetProperty("width").GetInt32() || texture.GetHeight() != row.GetProperty("height").GetInt32())
                throw new InvalidDataException("Stone evolution inherited pixels differ.");
}
        Texture2D Pixels(JsonElement row) => PresentationTexture(art.Files, row);
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
        _supplementalDex.AddEntries(art.DexEntries.Values, art.Files);

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
    private void DisposeStoneEvolutionPresentation() => _stoneEvolutionArt = null;
}
