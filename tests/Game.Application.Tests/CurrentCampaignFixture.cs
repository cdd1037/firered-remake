using Game.Application;
using Game.Content;

namespace FireRed.Tests.Application;

/// <summary>
/// One immutable current-content catalog and an already prepared current-schema
/// fixture per test process. The default local fixture has explicit literal legacy
/// names and synthetic hatch count 1000; ordinary tests never convert or repair it.
/// </summary>
internal static class CurrentCampaignFixture
{
    private static readonly Lazy<PretCampaignCatalog> Current = new(
        () => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
    private static readonly Lazy<LoadedSave<CampaignState>> LateGame = new(LoadLateGame);

    public static PretCampaignCatalog Catalog => Current.Value;
    public static LoadedSave<CampaignState> Checkpoint => LateGame.Value;

    private static LoadedSave<CampaignState> LoadLateGame()
    {
        var validator = CampaignSession.CreateCurrent(Catalog);
        return SaveCodec.Decode<CampaignState>(File.ReadAllBytes(TestAssets.Checkpoint),
            validator.RulesVersion, Catalog.ContentVersion, TestAssets.Checkpoint, validator.ValidatePlayerSave);
    }
}
