using System.Collections.Immutable;
using System.Text.Json;
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

    // Raw-checkpoint tests must inspect the actual declared schema without applying
    // either the synthetic hatch statistic or the controlled test conversion.
    public static (LoadedSave<CampaignState> Save, string Rules) DecodeRawCheckpoint(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        string? rules = document.RootElement.GetProperty("RulesVersion").GetString();
        if (rules is not ("firered-current-campaign-v3" or "firered-current-campaign-v4"))
            throw new InvalidDataException("controlled_test_checkpoint_rules_unsupported");
        return (SaveCodec.Decode<CampaignState>(bytes, rules, Catalog.ContentVersion), rules);
    }

    private static LoadedSave<CampaignState> LoadLateGame()
    {
        var validator = CampaignSession.CreateCurrent(Catalog);
        var (loaded, rules) = DecodeRawCheckpoint(File.ReadAllBytes(TestAssets.Checkpoint));
        // Controlled TEST input, not recovery of the legacy player's unknown lifetime total.
        // Keep the on-disk checkpoint intact. Raw-checkpoint validity is tested separately.
        // A fixed synthetic total also leaves room for tests adding authenticated hatch receipts.
        var state = loaded.State.GameStats?.ContainsKey("GAME_STAT_HATCHED_EGGS") == true ? loaded.State :
            loaded.State with { GameStats = (loaded.State.GameStats ??
                ImmutableSortedDictionary<string, int>.Empty).Add("GAME_STAT_HATCHED_EGGS", 1000) };
        // The ignored legacy test input is admitted only through its explicit v3
        // conversion. This validates every discarded Tower receipt, in memory only.
        if (rules == "firered-current-campaign-v3") state = validator.ConvertLegacyTrainerTowerCheckpoint(state);
        var error = validator.ValidatePlayerSave(state);
        if (error is not null) throw new InvalidDataException("controlled_test_checkpoint_invalid: " + error);
        return loaded with { State = state };
    }
}
