using Game.Content;
using Godot;

public partial class CampaignView
{
    private void LoadLostCaveItemIcons()
    {
        foreach (var icon in PretLostCaveItems.LoadItemsDocument(_root).Icons)
        {
            if (_catalog.EarlyShop!.Items.All(item => item.Id != icon.Item))
                throw new InvalidDataException("Lost Cave icon lacks its authenticated inventory item.");
            _earlyItemIcons.Add(icon.Item, ItemTexture(icon));
        }
    }
}
