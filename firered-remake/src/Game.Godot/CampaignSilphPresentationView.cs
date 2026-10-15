using Game.Content;
using Game.Domain.Ids;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private readonly Dictionary<string, string> _silphTrainerPortraits = new(StringComparer.Ordinal);
    private readonly Dictionary<int, SilphDoorMetatile> _silphDoorTiles = [];
    private readonly Texture2D[] _silphDoorLayers = new Texture2D[3];
    private PretSilphItemArt? _silphItemArt;
    private PretSilphInterface? _silphInterface;
    private Texture2D? _silphScrollUp, _silphScrollDown;

    private int PresentedSpeciesId(SpeciesId species) => _catalog.FiveIslandOpponents?.Catalog.Species.GetValueOrDefault(species)?.SourceSpeciesId ?? _catalog.SixIslandOpponents?.Catalog.Species.GetValueOrDefault(species)?.SourceSpeciesId ?? _catalog.PokemonLeagueOpponents?.Catalog.Species.GetValueOrDefault(species)?.SourceSpeciesId ?? _catalog.VictoryRoadOpponents?.Catalog.Species.GetValueOrDefault(species)?.SourceSpeciesId ?? _catalog.CinnabarArt?.Sprites.FirstOrDefault(s => s.SpeciesId == species.Value)?.SourceSpeciesId ?? _catalog.SaffronGymArt?.Sprites.FirstOrDefault(s => s.SpeciesId == species.Value)?.SourceSpeciesId ?? _catalog.SilphArt?.Sprites.FirstOrDefault(s => s.SpeciesId == species.Value)?.SourceSpeciesId ?? _catalog.PokemonContent!.Catalog.Species[species].SourceSpeciesId;

    private void DisposeSilphPresentation()
    {
        _silphPad = null; _silphDoorTiles.Clear(); _silphTrainerPortraits.Clear(); _silphItemArt = null; _silphInterface = null; _silphScrollUp = null; _silphScrollDown = null;
        foreach (string key in _questPocketTextures.Keys.Where(k => k.StartsWith("silph-", StringComparison.Ordinal)).ToArray())
        { _questPocketTextures.Remove(key); }
        Array.Clear(_silphDoorLayers);
    }

    private void LoadSilphPresentation()
    {
        if (!_catalog.SilphEnabled) return;
        var art = _catalog.SilphArt ?? throw new InvalidDataException("Silph art catalog missing.");
        foreach (var sprite in art.Sprites)
        {
            using var image = NativeImages.LoadPng(art.Files[sprite.TexturePath], sprite.Width, sprite.Height);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"), new(SceneTextures.FromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var icon in art.Icons)
        {
            var rgba = NativeImages.ReadPixels(art.Files[icon.TexturePath], icon.Width, icon.Height);
            for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                for (int c = 0; c < 3; c++) { int v = rgba[i + c] >> 3; rgba[i + c] = (byte)((v << 3) | (v >> 2)); }
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), SceneTextures.FromImage(image));
        }
        foreach (var footprint in art.Footprints)
        {
            using var image = NativeImages.LoadPng(art.Files[footprint.TexturePath], footprint.Width, footprint.Height);
            _route22Footprints.Add(footprint.SourceSpeciesId, SceneTextures.FromImage(image));
        }
        foreach (int species in new[] { 65, 122, 131 }) _portraits.Add(species, _battleArt[(species, "front", false)].Texture);
        LoadSilphTrainerArt(); LoadSilphItemArt();
        _silphInterface = PretSilphInterface.Load(_root);
        using (var image = NativeImages.LoadPng(_silphInterface.Files[_silphInterface.Up.TexturePath], 16, 16)) _silphScrollUp = SceneTextures.FromImage(image);
        using (var image = NativeImages.LoadPng(_silphInterface.Files[_silphInterface.Down.TexturePath], 16, 16)) _silphScrollDown = SceneTextures.FromImage(image);
        var doors = PretSilphDoorArt.Load(_root);
        foreach (var tile in doors.Tiles) _silphDoorTiles.Add(tile.MetatileId, tile);
        for (int layer = 0; layer < 3; layer++)
        {
            var row = doors.Layers[layer];
            using var image = NativeImages.LoadPng(doors.Files[row.TexturePath], row.Width, row.Height);
            _silphDoorLayers[layer] = SceneTextures.FromImage(image);
        }
        Write("silphPresentation", new { enemyOnlySpecies = new[] { 65, 122 }, audibleOutputVerified = false, originalVisualParityVerified = false });
    }
    private void LoadSilphTrainerArt()
    {
        foreach (var row in PretSilphTrainerArt.Load(_root))
        {
            byte[] png = row.Png;
            if(!(_forestTrainerPortraits.TryGetValue(row.Id, out var old))){ using var image = NativeImages.LoadPng(png, 64, 64); _forestTrainerPortraits.Add(row.Id, (ImageTexture)SceneTextures.FromImage(image)); }
        }
        _silphTrainerPortraits.Add("TRAINER_PIC_ROCKET_GRUNT_M", "rocket-grunt-m-front");
        _silphTrainerPortraits.Add("TRAINER_PIC_ROCKET_GRUNT_F", "rocket-grunt-f-front");
        _silphTrainerPortraits.Add("TRAINER_PIC_SCIENTIST", "scientist-front");
        _silphTrainerPortraits.Add("TRAINER_PIC_RIVAL_LATE", "rival-late-front");
        _silphTrainerPortraits.Add("TRAINER_PIC_LEADER_GIOVANNI", "giovanni-front");
        foreach (var trainer in _catalog.SilphTrainers!.Trainers.Values)
            if (!_silphTrainerPortraits.ContainsKey(trainer.TrainerPic) && !(trainer.TrainerPic == "TRAINER_PIC_JUGGLER" && trainer.TrainerClass == "TRAINER_CLASS_JUGGLER" && _forestTrainerPortraits.ContainsKey("juggler-front"))) throw new InvalidDataException("Silph trainer portrait missing: " + trainer.TrainerPic);
    }
    private string? VisibleSilphTrainerPortrait() => _session.State.ForestTrainerBattle is { } battle &&
        _catalog.SilphTrainers?.Trainers.TryGetValue(battle.Trainer, out var trainer) == true ? trainer.TrainerPic == "TRAINER_PIC_JUGGLER" ? "juggler-front" : _silphTrainerPortraits[trainer.TrainerPic] : null;
    private string? VisibleSilphTrainerClass() => _session.State.ForestTrainerBattle is { } battle &&
        _catalog.SilphTrainers?.Trainers.TryGetValue(battle.Trainer, out var trainer) == true ? trainer.TrainerClass switch
        { "TRAINER_CLASS_TEAM_ROCKET" => "TEAM ROCKET", "TRAINER_CLASS_SCIENTIST" => "SCIENTIST", "TRAINER_CLASS_RIVAL_LATE" => "RIVAL", "TRAINER_CLASS_BOSS" => "BOSS", "TRAINER_CLASS_JUGGLER" => "JUGGLER", _ => throw new InvalidDataException("Unsupported Silph trainer class.") } : null;
    private void LoadSilphItemArt()
    {
        var items = _catalog.SilphItems ?? throw new InvalidDataException("Silph item metadata missing.");
        _silphItemArt = PretSilphItemArt.Load(_root);
        foreach (var icon in _silphItemArt.Icons)
        {
            if (!_earlyItemIcons.ContainsKey(icon.Item)) _earlyItemIcons.Add(icon.Item, ItemTexture("silph-item-art", icon));
        }
        var document = _questPocketUi ?? throw new InvalidDataException("Silph item pockets missing.");
        var position = document.Assets.Single(a => a.Id == document.Items[0].Icon);
        foreach (var id in new[] { PretSilphItems.CardKeyId, PretSilphItems.MasterBallId, PretSilphItems.UltraBallId })
        {
            if (document.Items.Any(i => i.Id == id.Value)) continue;
            var metadata = items.Items.Single(i => i.Metadata.Item.Id == id).Metadata;
            var icon = _silphItemArt.Icons.Single(i => i.Item == id); string assetId = "silph-" + id.Value[10..].ToLowerInvariant();
            _questPocketTextures.Add(assetId, ItemTexture("silph-item-art", icon));
            document = document with { Items = document.Items.Add(new(id.Value, metadata.Item.SourceItemSymbol, metadata.Item.Name, metadata.Description, assetId, metadata.Item.FieldUseFunction)),
                Assets = document.Assets.Add(new(assetId,icon.PngPath,icon.Width,icon.Height,position.X,position.Y)) };
        }
        _questPocketUi = document;
    }
    private bool TrySilphPatch(Image canvas, FieldMetatileEdit edit, int layer, MapId mapId)
    {
        if (!_catalog.SilphEnabled || _catalog.SilphRegion?.NewMaps.ContainsKey(mapId) != true) return false;
        if (!_silphDoorTiles.TryGetValue(edit.SourceMetatileId, out var tile)) throw new InvalidDataException("Silph metatile art missing: " + edit.SourceMetatileId);
        using var atlas = _silphDoorLayers[layer].GetImage(); canvas.BlitRect(atlas, new(tile.X, tile.Y, tile.Width, tile.Height), new(edit.X * 16, edit.Y * 16)); return true;
    }
}
