using System.Collections.Immutable;
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
        var loaded = SaveCodec.Decode<CampaignState>(File.ReadAllBytes(TestAssets.Checkpoint),
            validator.RulesVersion, Catalog.ContentVersion);
        // Controlled TEST input, not recovery of the legacy player's unknown lifetime total.
        // Keep the on-disk checkpoint intact. Raw-checkpoint validity is tested separately.
        // A fixed synthetic total also leaves room for tests adding authenticated hatch receipts.
        var state = loaded.State.GameStats?.ContainsKey("GAME_STAT_HATCHED_EGGS") == true ? loaded.State :
            loaded.State with { GameStats = (loaded.State.GameStats ??
                ImmutableSortedDictionary<string, int>.Empty).Add("GAME_STAT_HATCHED_EGGS", 1000) };
        var error = validator.ValidatePlayerSave(state);
        if (error is not null) throw new InvalidDataException("controlled_test_checkpoint_invalid: " + error);
        return loaded with { State = state };
    }
}
