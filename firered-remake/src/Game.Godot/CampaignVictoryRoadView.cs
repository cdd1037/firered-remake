using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private static bool VictoryRoadProfileRequested => PokemonLeagueProfileRequested || OS.GetEnvironment("FIRERED_VICTORY_ROAD") == "1" ||
        OS.GetCmdlineUserArgs().Contains("--story-victory-road");
    private VictoryRoadPresentationData? _victoryRoadPresentation;
    private readonly Dictionary<int, VictoryRoadTilePresentation> _victoryRoadTiles = [];
    private readonly Dictionary<int, Texture2D> _victoryRoadTileLayers = [];
    private static bool IsVictoryRoadCave(MapId map) => map.Value is "map.victory-road-1f" or "map.victory-road-2f" or "map.victory-road-3f";

    private void LoadVictoryRoadPresentation()
    {
        if (!_catalog.VictoryRoadEnabled) return;
        if (!_catalog.LeagueRivalEnabled || _session.RulesVersion != CampaignSession.VictoryRoadRulesVersion && !(_catalog.PokemonLeagueEnabled && _session.RulesVersion == CampaignSession.PokemonLeagueRulesVersion) && !(_catalog.NationalDexEnabled && _catalog.PokemonLeagueEnabled && _session.RulesVersion == CampaignSession.NationalDexRulesVersion) && !SeviiRubyPresentationProfile && !FourIslandPresentationProfile)
            throw new InvalidDataException("Victory Road presentation requires its authenticated successor profile.");
        var art = VictoryRoadPresentationAssets.Load(_root);
        var source = _catalog.VictoryRoadTrainerParties ?? throw new InvalidDataException("Victory Road trainers missing.");
        if (source.SourceCommit != VictoryRoadPresentationAssets.SourceCommit || source.Trainers.Count != art.Trainers.Count ||
            _battleArt.Count != 488 || _storageIcons.Count != 122 || _route22Footprints.Count != 93)
            throw new InvalidDataException("Victory Road must preserve its predecessor species presentation.");
        var opponents = _catalog.VictoryRoadOpponents ?? throw new InvalidDataException("Victory Road opponents missing.");
        if (opponents.Catalog.Species.Count != 124 || opponents.Owned.Catalog.Species.Count != 115)
            throw new InvalidDataException("Victory Road opponent/ownership boundary differs.");
        foreach (var sprite in art.Sprites)
        {
            if (!opponents.Catalog.Species.TryGetValue(new(sprite.SpeciesId), out var species) || species.SourceSpeciesId != sprite.SourceSpeciesId)
                throw new InvalidDataException("Victory Road sprite species is not an authenticated opponent.");
            using var image = Image.CreateFromData(64, 64, false, Image.Format.Rgba8, art.Files[sprite.RgbaPath]);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(SceneTextures.FromImage(image), new(sprite.X, sprite.Y)));
        }
        foreach (var icon in art.PokemonIcons)
        {
            var rgba = art.Files[icon.RgbaPath].ToArray();
            for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                for (int c = 0; c < 3; c++) { int v = rgba[i + c] >> 3; rgba[i + c] = (byte)((v << 3) | (v >> 2)); }
            using var image = Image.CreateFromData(32, 32, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), SceneTextures.FromImage(image));
        }
        foreach (int species in new[] { 87, 103 }) _portraits.Add(species, _battleArt[(species, "front", false)].Texture);
        if (_battleArt.Count != 496 || _storageIcons.Count != 124 || _route22Footprints.Count != 93)
            throw new InvalidDataException("Victory Road opponent presentation inventory differs.");
        foreach (var trainer in source.Trainers.Values)
            if (!art.Trainers.TryGetValue(trainer.Id.Value, out var row) || row.SourceTrainerId != trainer.SourceTrainerId || row.Name != trainer.SourceTrainerName ||
                row.TrainerClass != trainer.TrainerClass || row.TrainerPic != trainer.TrainerPic || row.EncounterMusicGender != trainer.EncounterMusicGender ||
                row.DoubleBattle != trainer.DoubleBattle || art.Portraits.Single(p => p.Id == row.Portrait).SourceTrainerPicId != trainer.SourceTrainerPic)
                throw new InvalidDataException("Victory Road visible trainer differs: " + trainer.Id.Value);
        foreach (var portrait in art.Portraits)
        {
            byte[] rgba = art.Files[portrait.RgbaPath];
            if (portrait.Inherited)
            {
                if (!_forestTrainerPortraits.TryGetValue(portrait.Id, out var old)) throw new InvalidDataException("Inherited Victory Road portrait missing.");
                using var image = old.GetImage(); image.Convert(Image.Format.Rgba8);
                if (!image.GetData().SequenceEqual(rgba)) throw new InvalidDataException("Inherited Victory Road portrait differs.");
            }
            else
            {
                using var image = Image.CreateFromData(64, 64, false, Image.Format.Rgba8, rgba);
                _forestTrainerPortraits.Add(portrait.Id, (ImageTexture)SceneTextures.FromImage(image));
            }
        }
        foreach (var icon in art.Items)
        {
            using var image = Image.CreateFromData(24, 24, false, Image.Format.Rgba8, art.Files[icon.RgbaPath]);
            _earlyItemIcons.Add(new(icon.Item), (ImageTexture)SceneTextures.FromImage(image));
        }
        foreach (var tile in art.Tiles) _victoryRoadTiles.Add(tile.MetatileId, tile);
        foreach (var layer in art.Layers)
        {
            using var image = Image.CreateFromData(layer.Width, layer.Height, false, Image.Format.Rgba8, art.Files[layer.RgbaPath]);
            _victoryRoadTileLayers.Add(layer.Layer, SceneTextures.FromImage(image));
        }
        _victoryRoadPresentation = art;
        Write("victoryRoadPresentation", new { VictoryRoadPresentationAssets.SourceCommit, VictoryRoadPresentationAssets.ManifestSha256,
            trainers = 12, trainerScripts = 13, newPortraits = 1, inheritedPortraits = 6, newSpecies = new[] { 87, 103 },
            battleSprites = 496, icons = 124, footprints = 93, ownedSpecies = 115, newAudioAssets = 0,
            additionalCueIds = new[] { "SE_ICE_BREAK" }, barrierMetatiles = 4, barrierLayers = 3, physicalAudioVerified = false });
    }

    // Names come from the already authenticated 386-entry dex, gated by the bounded opponent catalog.
    private string PresentedBattleSpeciesName(int species) => SixIslandSpeciesName(species) ?? PokemonLeagueSpeciesName(species) ?? (species is 87 or 103 &&
        _catalog.VictoryRoadOpponents?.Catalog.Species.Values.Any(s => s.SourceSpeciesId == species) == true
        ? (_pokedexMenu ?? throw new InvalidDataException("Victory Road species names missing.")).Entries[species].Name
        : _catalog.PresentedSpeciesName(species));

    private PokedexMenuTexture? VictoryRoadDexPortrait(int species)
    {
        var sprite = _victoryRoadPresentation?.Sprites.SingleOrDefault(s => s.SourceSpeciesId == species && s.Side == "front" && s.PaletteVariant == "normal");
        return sprite is null ? null : new(sprite.TexturePath, sprite.Sha256, sprite.RgbaSha256, 64, 64);
    }

    private byte[]? ReadVictoryRoadDexTexture(PokedexMenuTexture texture)
    {
        if (_victoryRoadPresentation is not {} art) return null;
        return art.Sprites.Any(s => s.Side == "front" && s.PaletteVariant == "normal" && texture == VictoryRoadDexPortrait(s.SourceSpeciesId))
            ? art.Files[texture.TexturePath] : null;
    }

    private VictoryRoadTrainerPresentation? VictoryRoadTrainer(string? id)
    {
        if (id is null || !_catalog.VictoryRoadEnabled || _catalog.VictoryRoadTrainerParties?.Trainers.ContainsKey(new(id)) != true) return null;
        return _victoryRoadPresentation?.Trainers.GetValueOrDefault(id) ?? throw new InvalidDataException("Victory Road visible trainer is not authenticated.");
    }
    private VictoryRoadTrainerPresentation? VisibleVictoryRoadTrainer() => VictoryRoadTrainer(_session.State.ForestTrainerBattle?.Trainer.Value);
    private VictoryRoadTrainerPresentation? VictoryRoadDoubleTrainer => VictoryRoadTrainer(_session.State.DoubleBattle?.Trainer.Value);
    private Texture2D? VictoryRoadDoublePortrait => VictoryRoadDoubleTrainer is {} trainer ? _forestTrainerPortraits[trainer.Portrait] : null;

    private bool TryVictoryRoadPatch(Image canvas, FieldMetatileEdit edit, int layer, MapId map)
    {
        if (!_catalog.VictoryRoadEnabled || !IsVictoryRoadCave(map)) return false;
        if (!_victoryRoadTiles.TryGetValue(edit.SourceMetatileId, out var tile)) throw new InvalidDataException("Victory Road metatile pixels missing.");
        // BlitRect replaces alpha as well as RGB, so cleared top/middle pixels cannot retain the old barrier.
        using var atlas = _victoryRoadTileLayers[layer].GetImage();
        canvas.BlitRect(atlas, new(tile.X, tile.Y, 16, 16), new(edit.X * 16, edit.Y * 16));
        return true;
    }
}
