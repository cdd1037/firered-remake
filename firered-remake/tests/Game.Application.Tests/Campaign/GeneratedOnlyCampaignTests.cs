using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class GeneratedOnlyCampaignTests
{
    [TestMethod]
    public void CurrentGameStartsAndContinuesWithoutOriginalSourcesOrImportTools()
    {
        var root = TestAssets.Output();
        var source = Path.Combine(TestAssets.Root, "content/generated");
        var target = Path.Combine(root, "content/generated");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
        try
        {
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/sources")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "scripts")));
            var catalog = PretCampaignCatalog.LoadCurrent(root);
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
            var checkpoint = File.ReadAllBytes(TestAssets.Checkpoint);
            using var document = JsonDocument.Parse(checkpoint);
            var existing = SaveCodec.Decode<CampaignState>(checkpoint,
                document.RootElement.GetProperty("RulesVersion").GetString()!,
                document.RootElement.GetProperty("ContentVersion").GetString()!, validate: session.ValidatePlayerSave);
            var restored = CampaignSession.RestoreCurrent(catalog, existing.State, existing.Generation);
            Assert.AreEqual(existing.Generation, restored.Revision);
            Assert.AreEqual(CanonicalJson.Hash(existing.State), CanonicalJson.Hash(restored.State));
            Assert.IsNull(restored.ValidatePlayerSave(restored.State));
        }
        finally { Directory.Delete(root, true); }
    }
}
