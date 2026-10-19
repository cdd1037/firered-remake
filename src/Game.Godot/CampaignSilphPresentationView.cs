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

    private int PresentedSpeciesId(SpeciesId species) => _catalog.OpponentPokemon?.Catalog.Species.GetValueOrDefault(species)?.SourceSpeciesId
        ?? _catalog.PokemonContent!.Catalog.Species[species].SourceSpeciesId;

    private void DisposeSilphPresentation()
    {
        _silphPad = null; _silphDoorTiles.Clear(); _silphTrainerPortraits.Clear(); _silphItemArt = null; _silphInterface = null; _silphScrollUp = null; _silphScrollDown = null;
        foreach (string key in _questPocketTextures.Keys.Where(k => k.StartsWith("silph-", StringComparison.Ordinal)).ToArray())
        { _questPocketTextures.Remove(key); }
        Array.Clear(_silphDoorLayers);
    }

    private void LoadSilphPresentation()
    {
        var art = _catalog.SilphArt ?? throw new InvalidDataException("Silph art catalog missing.");
        RegisterPokemonArt(art.Sprites, art.Icons, art.Footprints, art.Files);

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
    private void LoadSilphItemArt()
    {
        var items = _catalog.SilphItems ?? throw new InvalidDataException("Silph item metadata missing.");
        _silphItemArt = PretSilphItemArt.Load(_root);
        foreach (var icon in _silphItemArt.Icons)
        {
            if (!_earlyItemIcons.ContainsKey(icon.Item)) _earlyItemIcons.Add(icon.Item, ItemTexture("data/services/silph-item-art.json", icon));
        }
        var document = _questPocketUi ?? throw new InvalidDataException("Silph item pockets missing.");
        var position = document.Assets.Single(a => a.Id == document.Items[0].Icon);
        foreach (var id in new[] { PretSilphItems.CardKeyId, PretSilphItems.MasterBallId, PretSilphItems.UltraBallId })
        {
            if (document.Items.Any(i => i.Id == id.Value)) continue;
            var metadata = items.Items.Single(i => i.Metadata.Item.Id == id).Metadata;
            var icon = _silphItemArt.Icons.Single(i => i.Item == id); string assetId = "silph-" + id.Value[10..].ToLowerInvariant();
            _questPocketTextures.Add(assetId, ItemTexture("data/services/silph-item-art.json", icon));
            document = document with { Items = document.Items.Add(new(id.Value, metadata.Item.SourceItemSymbol, metadata.Item.Name, metadata.Description, assetId, metadata.Item.FieldUseFunction)),
                Assets = document.Assets.Add(new(assetId,icon.PngPath,icon.Width,icon.Height,position.X,position.Y)) };
        }
        _questPocketUi = document;
    }
}
