using System.Text.Json;
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
    public void ExistingCheckpointPreservesPayloadRngAndGeneration()
    {
        var session = CampaignSession.CreateCurrent(catalog, new(0x11223344));
        byte[] source = File.ReadAllBytes(TestAssets.Checkpoint);
        using var document = JsonDocument.Parse(source);
        string oldVersion = document.RootElement.GetProperty("ContentVersion").GetString()!;
        string oldRules = document.RootElement.GetProperty("RulesVersion").GetString()!;
        var old = SaveCodec.Decode<CampaignState>(source, oldRules, oldVersion, validate: session.ValidatePlayerSave);
        Assert.IsTrue(oldRules == session.RulesVersion, "checkpoint uses current gameplay rules");
        byte[] converted = SaveCodec.Encode(old.State, old.Generation, oldRules, catalog.ContentVersion);
        var reread = SaveCodec.Decode<CampaignState>(converted, oldRules, catalog.ContentVersion, validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(catalog, reread.State, reread.Generation);
        Assert.IsTrue(CanonicalJson.Hash(restored.State) == CanonicalJson.Hash(old.State) && restored.Revision == old.Generation, "checkpoint Continue preserves entire payload and generation");
        Assert.IsTrue(restored.ValidatePlayerSave(restored.State)is null, "continued checkpoint remains valid");
        using var convertedJson = JsonDocument.Parse(converted);
        Assert.IsTrue(JsonElement.DeepEquals(document.RootElement.GetProperty("Payload"), convertedJson.RootElement.GetProperty("Payload")), "conversion changes no payload fields");
        using var output = TestAssets.TemporaryOutput();
        string destination = Path.Combine(output.Path, "continued-checkpoint.json");
        using (var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
            file.Write(converted);
        Assert.IsTrue(File.ReadAllBytes(TestAssets.Checkpoint).AsSpan().SequenceEqual(source), "source checkpoint preserved");
    }
}
