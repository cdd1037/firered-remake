using Game.Application;
using Game.Content;
using Game.Domain.World;
using FireRed.Tests;

namespace FireRed.Tests.Application.Campaign;
[TestClass, TestCategory("Game")]
public sealed class CampaignCompositionTests
{
    private Game.Domain.World.ICampaignCatalog catalog = null !;
    [TestInitialize]
    public void SetUp() => catalog = CurrentCampaignFixture.Catalog;
    [TestMethod]
    public void DefaultStartupSelectsCurrentGame()
    {
        var startup = GameStartupSelection.Resolve([], new Dictionary<string, string?> { { "FIRERED_PROJECT_ROOT", TestAssets.Root } });
        Assert.IsTrue(startup.Mode == GameStartupMode.Current, "default selects current game");
    }

    [TestMethod]
    public void NewGameContinuesAndAcceptsMovement()
    {
        var session = CampaignSession.CreateCurrent(catalog, new(0x11223344));
        Assert.IsTrue(catalog.ContentVersion == "firered-current-content-v4", "explicit content compatibility version");
        Assert.IsTrue(catalog.Maps.ContainsKey(new("map.saffron-city")) && catalog.Maps.ContainsKey(PretLostCaveRegion.EntranceMapId), "expanded regions loaded");
        Assert.IsTrue(catalog.PokemonContent!.Catalog.Moves.Count == 354, "all move definitions loaded");
        Assert.IsTrue(session.ValidatePlayerSave(session.State)is null, "new game is saveable");
        var encoded = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, catalog.ContentVersion);
        var saved = SaveCodec.Decode<CampaignState>(encoded, session.RulesVersion, catalog.ContentVersion, validate: session.ValidatePlayerSave);
        var continued = CampaignSession.RestoreCurrent(catalog, saved.State, saved.Generation);
        Assert.IsTrue(continued.Revision == session.Revision && CanonicalJson.Hash(continued.State) == CanonicalJson.Hash(session.State), "new game Continue preserves state and RNG");
        var before = continued.State;
        var move = continued.Execute("composition-smoke", continued.Revision, new("move", Direction.East));
        Assert.IsTrue(move.Status == CommandStatus.Accepted, "public movement command accepted after Continue");
        Assert.IsTrue(continued.ValidatePlayerSave(continued.State)is null, "movement result remains saveable");
    }

    [TestMethod]
    public void RawCheckpointIsNeverAutomaticallyUpgradedAndSourceBytesStayIntact()
    {
        var session = CampaignSession.CreateCurrent(catalog, new(0x11223344));
        byte[] source = File.ReadAllBytes(TestAssets.Checkpoint);
        var (loaded, rules) = CurrentCampaignFixture.DecodeRawCheckpoint(source);
        if (rules != session.RulesVersion)
        {
            var result = PlayerSaveOperations.Load(() => SaveCodec.Decode<CampaignState>(source,
                session.RulesVersion, catalog.ContentVersion, validate: session.ValidatePlayerSave));
            Assert.AreEqual(SaveOperationStatus.IncompatibleVersion, result.Result.Status);
            Assert.AreEqual(SaveFileOutcome.Preserved, result.Result.Files);
            Assert.IsNull(result.Saved);
            if (loaded.State.GameStats?.ContainsKey("GAME_STAT_HATCHED_EGGS") != true)
                Assert.AreEqual("save_hatched_egg_count_missing_migration_required",
                    session.ValidateLegacyTrainerTowerCheckpoint(loaded.State));
        }
        else if (loaded.State.GameStats?.ContainsKey("GAME_STAT_HATCHED_EGGS") == true)
        {
            var restored = CampaignSession.RestoreCurrent(catalog, loaded.State, loaded.Generation);
            Assert.AreEqual(CanonicalJson.Hash(loaded.State), CanonicalJson.Hash(restored.State));
        }
        else
        {
            var result = PlayerSaveOperations.Load(() => SaveCodec.Decode<CampaignState>(source,
                session.RulesVersion, catalog.ContentVersion, validate: session.ValidatePlayerSave));
            Assert.AreEqual(SaveOperationStatus.MigrationRequired, result.Result.Status);
            Assert.AreEqual(SaveFileOutcome.Preserved, result.Result.Files);
            Assert.IsNull(result.Saved);
        }
        Assert.IsTrue(File.ReadAllBytes(TestAssets.Checkpoint).AsSpan().SequenceEqual(source));
    }
}
