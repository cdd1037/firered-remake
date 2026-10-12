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

    private int PresentedSpeciesId(SpeciesId species) => _catalog.SaffronGymArt?.Sprites.FirstOrDefault(s => s.SpeciesId == species.Value)?.SourceSpeciesId ?? _catalog.SilphArt?.Sprites.FirstOrDefault(s => s.SpeciesId == species.Value)?.SourceSpeciesId ?? _catalog.PokemonContent!.Catalog.Species[species].SourceSpeciesId;

    private void DisposeSilphPresentation()
    {
        _silphPad = null; _silphDoorTiles.Clear(); _silphTrainerPortraits.Clear(); _silphItemArt = null; _silphInterface = null; _silphScrollUp = null; _silphScrollDown = null;
        foreach (string key in _questPocketTextures.Keys.Where(k => k.StartsWith("silph-", StringComparison.Ordinal)).ToArray())
        { _questPocketTextures[key].Dispose(); _questPocketTextures.Remove(key); }
        Array.Clear(_silphDoorLayers);
    }

    private void LoadSilphPresentation()
    {
        if (!_catalog.SilphEnabled) return;
        SilphSourcePresentation.Authenticate(_root);
        var art = _catalog.SilphArt ?? throw new InvalidDataException("Silph art catalog missing.");
        if (_battleArt.Count != 448 || _storageIcons.Count != 112 || _route22Footprints.Count != 83)
            throw new InvalidDataException("Silph requires complete Surf presentation.");
        foreach (var sprite in art.Sprites)
        {
            using var image = Image.CreateFromData(sprite.Width, sprite.Height, false, Image.Format.Rgba8, art.Files[sprite.RgbaPath]);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"), new(SceneTextures.FromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var icon in art.Icons)
        {
            var rgba = art.Files[icon.RgbaPath].ToArray();
            for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                for (int c = 0; c < 3; c++) { int v = rgba[i + c] >> 3; rgba[i + c] = (byte)((v << 3) | (v >> 2)); }
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), SceneTextures.FromImage(image));
        }
        foreach (var footprint in art.Footprints)
        {
            using var image = Image.CreateFromData(footprint.Width, footprint.Height, false, Image.Format.Rgba8, art.Files[footprint.RgbaPath]);
            _route22Footprints.Add(footprint.SourceSpeciesId, SceneTextures.FromImage(image));
        }
        foreach (int species in new[] { 65, 122, 131 }) _portraits.Add(species, _battleArt[(species, "front", false)].Texture);
        if (_battleArt.Count != 460 || _storageIcons.Count != 115 || _route22Footprints.Count != 86)
            throw new InvalidDataException("Silph species presentation inventory differs.");
        LoadSilphTrainerArt(); LoadSilphItemArt();
        _silphInterface = PretSilphInterface.Load(_root);
        using (var image = Image.CreateFromData(16, 16, false, Image.Format.Rgba8, _silphInterface.Files[_silphInterface.Up.RgbaPath])) _silphScrollUp = SceneTextures.FromImage(image);
        using (var image = Image.CreateFromData(16, 16, false, Image.Format.Rgba8, _silphInterface.Files[_silphInterface.Down.RgbaPath])) _silphScrollDown = SceneTextures.FromImage(image);
        var doors = PretSilphDoorArt.Load(_root);
        foreach (var tile in doors.Tiles) _silphDoorTiles.Add(tile.MetatileId, tile);
        for (int layer = 0; layer < 3; layer++)
        {
            var row = doors.Layers[layer];
            using var image = Image.CreateFromData(row.Width, row.Height, false, Image.Format.Rgba8, doors.Files[row.RgbaPath]);
            _silphDoorLayers[layer] = SceneTextures.FromImage(image);
        }
        Write("silphPresentation", new { ownedSpecies = 113, enemyOnlySpecies = new[] { 65, 122 }, battleSprites = 460,
            icons = 115, footprints = 86, trainerPictures = 5, cardKeyDoorTiles = 16,
            PretSilphArt.ManifestSha256, audibleOutputVerified = false, originalVisualParityVerified = false });
    }
    private void LoadSilphTrainerArt()
    {
        foreach (var row in PretSilphTrainerArt.Load(_root))
        {
            byte[] rgba = row.Rgba.ToArray();
            if (_forestTrainerPortraits.TryGetValue(row.Id, out var old))
            {
                using var current = old.GetImage(); current.Convert(Image.Format.Rgba8);
                if (!current.GetData().SequenceEqual(rgba)) throw new InvalidDataException("Inherited trainer pixels differ: " + row.Id);
            }
            else { using var image = Image.CreateFromData(64, 64, false, Image.Format.Rgba8, rgba); _forestTrainerPortraits.Add(row.Id, (ImageTexture)SceneTextures.FromImage(image)); }
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
    private static byte[] SilphItemDisplayRgba(PretPewterMachineIcon icon)
    {
        byte[] rgba = PretPewterMachines.IconRgba(icon);
        // These three inherited Safari icons use the source-floor RGB555 expansion.
        // Preserve that exact presentation (including transparent RGB) before byte comparison.
        if (icon.Item.Value is "pret:ITEM_MAX_POTION" or "pret:ITEM_MAX_REVIVE" or "pret:ITEM_PROTEIN")
            for (int i = 0; i < rgba.Length; i += 4)
                for (int c = 0; c < 3; c++) rgba[i + c] = (byte)((rgba[i + c] >> 3) * 255 / 31);
        return rgba;
    }
    private void LoadSilphItemArt()
    {
        var items = _catalog.SilphItems ?? throw new InvalidDataException("Silph item metadata missing.");
        _silphItemArt = PretSilphItemArt.Load(_root);
        foreach (var icon in _silphItemArt.Icons)
        {
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, SilphItemDisplayRgba(icon));
            if (_earlyItemIcons.TryGetValue(icon.Item, out var old))
            { using var current = old.GetImage(); current.Convert(Image.Format.Rgba8); if (!current.GetData().SequenceEqual(image.GetData())) throw new InvalidDataException("Inherited item pixels differ: " + icon.Item.Value); }
            else _earlyItemIcons.Add(icon.Item, (ImageTexture)SceneTextures.FromImage(image));
        }
        var document = _questPocketUi ?? throw new InvalidDataException("Silph item pockets missing.");
        var position = document.Assets.Single(a => a.Id == document.Items[0].Icon);
        foreach (var id in new[] { PretSilphItems.CardKeyId, PretSilphItems.MasterBallId, PretSilphItems.UltraBallId })
        {
            if (document.Items.Any(i => i.Id == id.Value)) continue;
            var metadata = items.Items.Single(i => i.Metadata.Item.Id == id).Metadata;
            var icon = _silphItemArt.Icons.Single(i => i.Item == id); string assetId = "silph-" + id.Value[10..].ToLowerInvariant();
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, SilphItemDisplayRgba(icon));
            _questPocketTextures.Add(assetId, ImageTexture.CreateFromImage(image));
            document = document with { Items = document.Items.Add(new(id.Value, metadata.Item.SourceItemSymbol, metadata.Item.Name, metadata.Description, assetId, metadata.Item.FieldUseFunction)),
                Assets = document.Assets.Add(new(assetId, icon.PngPath, icon.Width, icon.Height, position.X, position.Y, icon.PngSha256, icon.RgbaSha256)) };
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
