using Game.Application;
using Game.Content;

namespace FireRed.Tests.Application;

/// <summary>
/// Read-only current content and the optional late-game input are fixed for one test process.
/// Every test creates its own session; state changes use the immutable save's with-expressions.
/// File mutation/codec tests load their own inputs directly instead of using this fixture.
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
            validator.RulesVersion, Catalog.ContentVersion, validate: validator.ValidatePlayerSave);
    }
}
