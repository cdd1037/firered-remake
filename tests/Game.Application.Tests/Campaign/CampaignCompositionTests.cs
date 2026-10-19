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
    public void CurrentControlledCheckpointLoadsStrictlyAndSourceBytesStayIntact()
    {
        var session = CampaignSession.CreateCurrent(catalog, new(0x11223344));
        byte[] source = File.ReadAllBytes(TestAssets.Checkpoint);
        // The default fixture was explicitly prepared with literal old names and
        // synthetic hatch count 1000. Ordinary tests never convert its schema or
        // invent absent statistics. An override must already be a current save.
        var loaded = SaveCodec.Decode<CampaignState>(source, session.RulesVersion,
            catalog.ContentVersion, validate: session.ValidatePlayerSave);
        Assert.IsTrue(loaded.State.GameStats!.ContainsKey("GAME_STAT_HATCHED_EGGS"));
        var restored = CampaignSession.RestoreCurrent(catalog, loaded.State, loaded.Generation);
        Assert.AreEqual(CanonicalJson.Hash(loaded.State), CanonicalJson.Hash(restored.State));
        CollectionAssert.AreEqual(source, File.ReadAllBytes(TestAssets.Checkpoint));
    }
}
