using Game.Content;
using Godot;

public partial class CampaignView
{
    private PresentationTextCatalog _saveText = null!;
    private PresentationTextCatalog _inventoryText = null!;

    private void LoadPresentationTexts()
    {
        string directory = OS.GetEnvironment("FIRERED_TEXT_PACK");
        string? overrides = string.IsNullOrEmpty(directory) ? null : System.IO.Path.GetFullPath(directory);
        _saveText = PresentationTextCatalog.Load("save", overrides);
        _inventoryText = PresentationTextCatalog.Load("inventory", overrides);
        FieldMenuText = PresentationTextCatalog.Load("field-menu", overrides);
        OptionsText = PresentationTextCatalog.Load("options", overrides);
        OptionLabels = Enumerable.Range(0, 7).Select(row => CampaignOptionPresentation.Label(row, OptionsText)).ToArray();
    }
}
