using Game.Application;
using Game.Content;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class GeneratedOnlyCampaignTests
{
    private static TestAssets.TemporaryDirectory generatedOnly = null!;
    private static PretCampaignCatalog catalog = null!;

    [ClassInitialize]
    public static void PrepareGeneratedOnlyContent(TestContext context)
    {
        generatedOnly = TestAssets.TemporaryOutput();
        try
        {
            var source = Path.Combine(TestAssets.Root, "content/generated");
            var target = Path.Combine(generatedOnly.Path, "content/generated");
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(target, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            Assert.IsFalse(Directory.Exists(Path.Combine(generatedOnly.Path, "content/raw")));
            Assert.IsFalse(Directory.Exists(Path.Combine(generatedOnly.Path, "content/sources")));
            Assert.IsFalse(Directory.Exists(Path.Combine(generatedOnly.Path, "scripts")));
            catalog = PretCampaignCatalog.LoadCurrent(generatedOnly.Path);
        }
        catch
        {
            generatedOnly.Dispose();
            generatedOnly = null!;
            throw;
        }
    }

    [ClassCleanup]
    public static void DeleteGeneratedOnlyContent() => generatedOnly?.Dispose();

    [TestMethod]
    public void NewGameSavesAndContinuesWithoutOriginalSourcesOrCheckpoint()
    {
        Assert.AreSame(catalog.ForestPokemon!.Catalog, catalog.PewterPokemon!.Catalog);
        Assert.AreSame(catalog.ForestPokemon.Catalog, catalog.Route22Pokemon!.Catalog);
        Assert.AreSame(catalog.ForestPokemon.Catalog, catalog.RocketHideoutPokemon!.Catalog);
        Assert.IsTrue(catalog.PokemonContent!.Catalog.Species.Count < catalog.ForestPokemon.Catalog.Species.Count);

        var session = CampaignSession.CreateCurrent(catalog, new(0x11223344));
        Assert.IsNull(session.ValidatePlayerSave(session.State));
        var encoded = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, catalog.ContentVersion);
        var saved = SaveCodec.Decode<CampaignState>(encoded, session.RulesVersion, catalog.ContentVersion, validate: session.ValidatePlayerSave);
        var continued = CampaignSession.RestoreCurrent(catalog, saved.State, saved.Generation);
        Assert.AreEqual(CanonicalJson.Hash(session.State), CanonicalJson.Hash(continued.State));
        var move = continued.Execute("generated-only-smoke", continued.Revision, new("move", Direction.East));
        Assert.AreEqual(CommandStatus.Accepted, move.Status);
        Assert.IsNull(continued.ValidatePlayerSave(continued.State));
    }

    [TestMethod]
    public void ControlledLateFixtureContinuesWithoutOriginalSourcesOrImportTools()
    {
        var session = CampaignSession.CreateCurrent(catalog);
        // Deliberately synthetic new-schema hatch total; original checkpoint is not repaired here.
        var existing = CurrentCampaignFixture.Checkpoint;
        var restored = CampaignSession.RestoreCurrent(catalog, existing.State, existing.Generation);
        Assert.AreEqual(existing.Generation, restored.Revision);
        Assert.AreEqual(CanonicalJson.Hash(existing.State), CanonicalJson.Hash(restored.State));
        Assert.IsNull(restored.ValidatePlayerSave(restored.State));
    }
}
