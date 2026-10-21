using System.Text.Json;
using Game.Content;
using Game.Domain.Pokemon;
using Game.Presentation.BattleAnimations;
using Godot;

/// <summary>Opponent-only birds share ordinary battlers, Transform geometry and seen-Dex rendering.</summary>
public partial class CampaignView
{
    private MemorialPillarPresentationData? _memorialPillarArt;

    private void LoadMemorialPillarPresentation()
    {
        if (_catalog.OpponentPokemon is not { } opponents) return;
        if (_ordinaryEvolutionArt is null)
            throw new InvalidDataException("Memorial Pillar presentation requires current game assets.");
        var art = PretMemorialPillarPresentation.Load(_root);
        foreach (var entry in art.DexEntries.Values)
        {
            var id = new Game.Domain.Ids.SpeciesId("pret:SPECIES_" + entry.Name);
            if (!opponents.Catalog.Species.TryGetValue(id, out var species) || species.SourceSpeciesId != entry.SourceSpeciesId ||
                _pokedexMenu is null ||
                !_pokedexMenu.HasSameEntryRules(entry))
                throw new InvalidDataException("Memorial Pillar opponent species or seen-Dex binding differs.");
        }
        foreach (var row in art.Manifest.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            string side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            using var image = NativeImages.LoadPng(art.Files[row.GetProperty("texturePath").GetString()!], 64, 64);
            var texture = SceneTextures.FromImage(image);
            _battleArt.Add((species, side, shiny), new(texture, new(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32())));
            if (side == "front" && !shiny) _portraits.Add(species, texture);
        }
        _memorialPillarArt = art;

    }

    private void AddMemorialPillarMoveGeometry()
    {
        if (_memorialPillarArt is not { } art) return;
        foreach (var row in art.Manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            int species = row.GetProperty("species").GetInt32(); string side = row.GetProperty("side").GetString()!;
            var geometry = new AnimationBattler(new(side == "back" ? 0 : 1), "", row.GetProperty("yOffset").GetInt32(),
                row.GetProperty("elevation").GetInt32(), row.GetProperty("sourceWidth").GetInt32(), row.GetProperty("sourceHeight").GetInt32());
            if (_sourceBattlerGeometry.TryGetValue((species, side), out var old))
            {
                if (old.YOffset != geometry.YOffset || old.Elevation != geometry.Elevation || old.SourceWidth != geometry.SourceWidth || old.SourceHeight != geometry.SourceHeight)
                    throw new InvalidDataException("Memorial Pillar inherited geometry differs.");
            }
            else _sourceBattlerGeometry.Add((species, side), geometry);
        }
    }
    private PokedexMenuPage? MemorialPillarPokedexPage(int species, PokedexMenuPage page) =>
        _memorialPillarArt?.DexEntries.GetValueOrDefault(species) is { } entry ? page with { Portrait = entry.Portrait, Footprint = null } : null;
    private byte[]? ReadMemorialPillarDexTexture(PokedexMenuTexture texture) => _memorialPillarArt is { } art &&
        art.DexEntries.Values.Any(e => e.Portrait == texture) ? art.Files[texture.TexturePath] : null;
    private void DisposeMemorialPillarPresentation() => _memorialPillarArt = null;
}
