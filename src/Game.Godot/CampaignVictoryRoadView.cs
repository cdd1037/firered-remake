using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private VictoryRoadPresentationData? _victoryRoadPresentation;
    private readonly Dictionary<int, VictoryRoadTilePresentation> _victoryRoadTiles = [];
    private readonly Dictionary<int, Texture2D> _victoryRoadTileLayers = [];
    private static bool IsVictoryRoadCave(MapId map) => map.Value is "map.victory-road-1f" or "map.victory-road-2f" or "map.victory-road-3f";

    private void LoadVictoryRoadPresentation()
    {
        var art = VictoryRoadPresentationAssets.Load(_root);
        var source = _catalog.VictoryRoadTrainerParties ?? throw new InvalidDataException("Victory Road trainers missing.");
        var opponents = _catalog.OpponentPokemon ?? throw new InvalidDataException("Victory Road opponents missing.");
        foreach (var sprite in art.Sprites)
        {
            if (!opponents.Catalog.Species.TryGetValue(new(sprite.SpeciesId), out var species) || species.SourceSpeciesId != sprite.SourceSpeciesId)
                throw new InvalidDataException("Victory Road sprite species is not an authenticated opponent.");
            using var image = NativeImages.LoadPng(art.Files[sprite.TexturePath], 64, 64);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(SceneTextures.FromImage(image), new(sprite.X, sprite.Y)));
        }
        foreach (var icon in art.PokemonIcons)
        {
            var rgba = NativeImages.ReadPixels(art.Files[icon.TexturePath], 32, 32);
            for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                for (int c = 0; c < 3; c++) { int v = rgba[i + c] >> 3; rgba[i + c] = (byte)((v << 3) | (v >> 2)); }
            using var image = Image.CreateFromData(32, 32, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), SceneTextures.FromImage(image));
        }
        foreach (int species in new[] { 87, 103 }) _portraits.Add(species, _battleArt[(species, "front", false)].Texture);
        foreach (var trainer in source.Trainers.Values)
            if (!art.Trainers.TryGetValue(trainer.Id.Value, out var row) || row.SourceTrainerId != trainer.SourceTrainerId || row.Name != trainer.SourceTrainerName ||
                row.TrainerClass != trainer.TrainerClass || row.TrainerPic != trainer.TrainerPic || row.EncounterMusicGender != trainer.EncounterMusicGender ||
                row.DoubleBattle != trainer.DoubleBattle || art.Portraits.Single(p => p.Id == row.Portrait).SourceTrainerPicId != trainer.SourceTrainerPic)
                throw new InvalidDataException("Victory Road visible trainer differs: " + trainer.Id.Value);
        foreach (var portrait in art.Portraits)
        {
            byte[] png = art.Files[portrait.TexturePath];
            if (portrait.Inherited)
            {
                if (!_forestTrainerPortraits.TryGetValue(portrait.Id, out var old)) throw new InvalidDataException("Inherited Victory Road portrait missing.");
            }
            else
            {
                using var image = NativeImages.LoadPng(png, 64, 64);
                _forestTrainerPortraits.Add(portrait.Id, (ImageTexture)SceneTextures.FromImage(image));
            }
        }
        foreach (var icon in art.Items)
        {
            using var image = NativeImages.LoadPng(art.Files[icon.TexturePath], 24, 24);
            _earlyItemIcons.Add(new(icon.Item), (ImageTexture)SceneTextures.FromImage(image));
        }
        foreach (var tile in art.Tiles) _victoryRoadTiles.Add(tile.MetatileId, tile);
        foreach (var layer in art.Layers)
        {
            using var image = NativeImages.LoadPng(art.Files[layer.TexturePath], layer.Width, layer.Height);
            _victoryRoadTileLayers.Add(layer.Layer, SceneTextures.FromImage(image));
        }
        _victoryRoadPresentation = art;

    }

    private string PresentedBattleSpeciesName(int species) =>
        _pokedexMenu?.Entries.GetValueOrDefault(species)?.Name ?? _catalog.PresentedSpeciesName(species);

    private PokedexMenuTexture? VictoryRoadDexPortrait(int species)
    {
        var sprite = _victoryRoadPresentation?.Sprites.SingleOrDefault(s => s.SourceSpeciesId == species && s.Side == "front" && s.PaletteVariant == "normal");
        return sprite is null ? null : new(sprite.TexturePath,64,64);
    }

    private byte[]? ReadVictoryRoadDexTexture(PokedexMenuTexture texture)
    {
        if (_victoryRoadPresentation is not {} art) return null;
        return art.Sprites.Any(s => s.Side == "front" && s.PaletteVariant == "normal" && texture == VictoryRoadDexPortrait(s.SourceSpeciesId))
            ? art.Files[texture.TexturePath] : null;
    }

    private VictoryRoadTrainerPresentation? VictoryRoadTrainer(string? id)
    {
        if (id is null || _catalog.VictoryRoadTrainerParties?.Trainers.ContainsKey(new(id)) != true) return null;
        return _victoryRoadPresentation?.Trainers.GetValueOrDefault(id) ?? throw new InvalidDataException("Victory Road visible trainer is not authenticated.");
    }
    private VictoryRoadTrainerPresentation? VisibleVictoryRoadTrainer() => VictoryRoadTrainer(_session.State.ForestTrainerBattle?.Trainer.Value);
    private VictoryRoadTrainerPresentation? VictoryRoadDoubleTrainer => VictoryRoadTrainer(_session.State.DoubleBattle?.Trainer.Value);
    private Texture2D? VictoryRoadDoublePortrait => VictoryRoadDoubleTrainer is {} trainer ? _forestTrainerPortraits[trainer.Portrait] : null;

}
