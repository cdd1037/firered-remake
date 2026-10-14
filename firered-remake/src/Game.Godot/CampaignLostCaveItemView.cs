using Game.Content;
using Godot;

public partial class CampaignView
{
    private void LoadLostCaveItemIcons()
    {
        if (!_catalog.LostCaveEnabled) return;
        foreach (var icon in PretLostCaveItems.LoadItemsDocument(_root).Icons)
        {
            if (_catalog.EarlyShop!.Items.All(item => item.Id != icon.Item))
                throw new InvalidDataException("Lost Cave icon lacks its authenticated inventory item.");
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8,
                PretLostCaveItems.LoadIconRgba(_root, icon.Item));
            _earlyItemIcons.Add(icon.Item, ImageTexture.CreateFromImage(image));
        }
    }
}
