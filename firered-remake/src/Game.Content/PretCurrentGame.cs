namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    /// <summary>The sole normal-game composition. Regional loaders remain diagnostic/reconstruction inputs.</summary>
    public static PretCampaignCatalog LoadCurrent(string root) => LoadCelioProfile(root);
}
