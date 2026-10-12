using Game.Content;
using Godot;

public partial class CampaignView
{
    /// <summary>Called by LoadCinnabarPresentation after inherited bag/quest/TM Case initialization.</summary>
    private void LoadCinnabarItemArt()
    {
        if (!_catalog.CinnabarEnabled) return;
        var items = _catalog.CinnabarItems ?? throw new InvalidDataException("Cinnabar item metadata missing.");
        var art = PretCinnabarItemArt.LoadProject(_root);
        foreach (var icon in art.Icons)
        {
            _ = items.Items.Single(i => i.Metadata.Item.Id == icon.Item);
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, PretPewterMachines.IconRgba(icon));
            if (_earlyItemIcons.TryGetValue(icon.Item, out var old))
            {
                using var current = old.GetImage(); current.Convert(Image.Format.Rgba8);
                if (!current.GetData().SequenceEqual(image.GetData())) throw new InvalidDataException("Cinnabar item pixels differ: " + icon.Item.Value);
            }
            else _earlyItemIcons.Add(icon.Item, (ImageTexture)SceneTextures.FromImage(image));
        }
        var document = _questPocketUi ?? throw new InvalidDataException("Cinnabar key-item pocket missing.");
        var key = items.Items.Single(i => i.Metadata.Item.Id == PretCinnabarItems.SecretKeyId).Metadata;
        const string assetId = "cinnabar-secret-key";
        if (document.Items.Any(i => i.Id == key.Item.Id.Value))
        {
            var existing = document.Items.Single(i => i.Id == key.Item.Id.Value);
            if (existing.Name != key.Item.Name || existing.Description != key.Description || existing.Icon != assetId ||
                existing.FieldUseFunction != key.Item.FieldUseFunction || !_questPocketTextures.ContainsKey(assetId))
                throw new InvalidDataException("Cinnabar Secret Key presentation differs.");
            return;
        }
        var position = document.Assets.Single(a => a.Id == document.Items[0].Icon);
        var keyIcon = art.Icons.Single(i => i.Item == key.Item.Id);
        using (var image = Image.CreateFromData(keyIcon.Width, keyIcon.Height, false, Image.Format.Rgba8, PretPewterMachines.IconRgba(keyIcon)))
            _questPocketTextures.Add(assetId, ImageTexture.CreateFromImage(image));
        _questPocketUi = document with
        {
            Items = document.Items.Add(new(key.Item.Id.Value, key.Item.SourceItemSymbol, key.Item.Name, key.Description, assetId, key.Item.FieldUseFunction)),
            Assets = document.Assets.Add(new(assetId, keyIcon.PngPath, keyIcon.Width, keyIcon.Height, position.X, position.Y, keyIcon.PngSha256, keyIcon.RgbaSha256))
        };
        Write("cinnabarItemPresentation", new { icons = art.Icons.Length, secretKeyPocket = true,
            machines = new[] { "TM14", "TM22", "TM38" }, PretCinnabarItemArt.ManifestSha256 });
    }
}
