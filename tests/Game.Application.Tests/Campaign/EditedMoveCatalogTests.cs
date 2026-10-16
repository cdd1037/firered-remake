using System.Text.Json.Nodes;
using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class EditedMoveCatalogTests
{
    [TestMethod]
    public void UnreferencedMoveRemovalDoesNotBlockNewGameOrContinueByHistoricalTotal()
    {
        using var output = TestAssets.TemporaryOutput();
        var source = Path.Combine(TestAssets.Root, "content/generated");
        var target = Path.Combine(output.Path, "content/generated");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }

        var path = Path.Combine(output.Path, PretMetronomeMoveCatalog.CatalogRelativePath);
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        const string removedId = "pret:MOVE_VOLT_TACKLE";
        foreach (var field in new[] { "moves", "moveNames" })
        {
            var rows = json[field]!.AsArray();
            var row = rows.Single(row => row!["id"]!["value"]!.GetValue<string>() == removedId);
            Assert.IsTrue(rows.Remove(row));
        }
        File.WriteAllText(path, json.ToJsonString());

        // This checks composition and persistence, not battle execution of edited content.
        var catalog = PretCampaignCatalog.LoadCurrent(output.Path);
        Assert.IsFalse(catalog.PokemonContent!.Catalog.Moves.ContainsKey(new MoveId(removedId)));
        Assert.AreEqual(json["moves"]!.AsArray().Count, catalog.PokemonContent.Catalog.Moves.Count);
        var session = CampaignSession.CreateCurrent(catalog, new(0x11223344));
        Assert.IsNull(session.ValidatePlayerSave(session.State));
        var encoded = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, catalog.ContentVersion);
        var saved = SaveCodec.Decode<CampaignState>(encoded, session.RulesVersion, catalog.ContentVersion,
            validate: session.ValidatePlayerSave);
        var continued = CampaignSession.RestoreCurrent(catalog, saved.State, saved.Generation);
        Assert.AreEqual(CanonicalJson.Hash(session.State), CanonicalJson.Hash(continued.State));
        Assert.AreEqual(session.Revision, continued.Revision);
        Assert.IsNull(continued.ValidatePlayerSave(continued.State));
    }
}
