using Game.Application;
using Game.Content;
using Godot;

/// <summary>Source Tea and Eevee presentation for the five admitted Celadon condominium maps.</summary>
public partial class CampaignView
{
    private void ExtendRegionalServicesQuestPocket(ref QuestItemUiDocument document)
    {
        var tea = _catalog.TeaItem ?? throw new InvalidDataException("Regional Tea metadata missing.");
        var source = PretRegionalTeaArt.Load(_root, tea);
        var asset = source.Document.Asset;
        string firstIcon = document.Items[0].Icon;
        var position = document.Assets.Single(a => a.Id == firstIcon);
        if (document.Items.Any(i => i.Id == tea.Item.Id.Value)) throw new InvalidDataException("Tea pocket entry is duplicated.");
        using var image = NativeImages.LoadPng(source.ReadPng(), asset.Width, asset.Height);
        // Key-item textures retain the existing pocket loader's ownership and cleanup path.
        _questPocketTextures.Add(asset.Id, SceneTextures.FromImage(image));
        document = document with
        {
            Items = document.Items.Add(source.Document.Item),
            Assets = document.Assets.Add(asset with { X = position.X, Y = position.Y })
        };
        // The existing Key Items Use/Cancel popup shows gText_OakForbidsUseOfItemHere.
        // Tea has no new field command and never gains Give, Toss or Sell actions.
    }

    private void LoadRegionalServicesPresentation()
    {
        var art = _catalog.EeveeArt ?? throw new InvalidDataException("Regional Eevee art catalog missing.");
        foreach (var sprite in art.Sprites)
        {
            using var image = NativeImages.LoadPng(art.Files[sprite.TexturePath], sprite.Width, sprite.Height);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(SceneTextures.FromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var icon in art.Icons)
        {
            var rgba = NativeImages.ReadPixels(art.Files[icon.TexturePath], icon.Width, icon.Height);
            for (int i = 0; i < rgba.Length; i += 4)
                if (rgba[i + 3] != 0)
                    for (int c = 0; c < 3; c++) { int value = rgba[i + c] >> 3; rgba[i + c] = (byte)((value << 3) | (value >> 2)); }
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), SceneTextures.FromImage(image));
        }
        foreach (var footprint in art.Footprints)
        {
            using var image = NativeImages.LoadPng(art.Files[footprint.TexturePath], footprint.Width, footprint.Height);
            _route22Footprints.Add(footprint.SourceSpeciesId, SceneTextures.FromImage(image));
        }
        // Reuse the same owned front texture for a source mon-picture request.
        _portraits.Add(133, _battleArt[(133, "front", false)].Texture);

    }

    private bool RegionalServicesActorHasPixels(CampaignActor actor) =>
        !PretCampaignCatalog.RegionalServiceMapIds.Contains(_session.State.World.Player.MapId) ||
        actor.MovementType != "MOVEMENT_TYPE_INVISIBLE";
}
