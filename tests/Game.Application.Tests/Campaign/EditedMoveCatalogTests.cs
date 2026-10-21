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

        // VOLT_TACKLE is unused by the campaign's move definitions, but its Easy
        // Chat word is still a real reference. Reject that dangling reference
        // before making this isolated fixture genuinely unreferenced.
        var dangling = Assert.ThrowsExactly<InvalidDataException>(() => PretCampaignCatalog.LoadCurrent(output.Path));
        Assert.AreEqual("easy_chat_move_name_missing: 10072", dangling.Message);
        const int removedWord = 10072;
        var composerPath = Path.Combine(target, "data/services/mail-composer/catalog.json");
        var composer = JsonNode.Parse(File.ReadAllText(composerPath))!;
        var words = composer["words"]!.AsArray();
        var word = words.Single(row => row!["value"]!.GetValue<int>() == removedWord)!;
        Assert.AreEqual("EC_MOVE2(VOLT_TACKLE)", word["symbol"]!.GetValue<string>());
        Assert.IsTrue(words.Remove(word));
        foreach (var group in composer["groups"]!.AsArray()) RemoveWord(group!["wordValues"]!.AsArray());
        foreach (var bucket in composer["alphabetical"]!.AsArray())
        {
            var alternatives = bucket!["alternatives"]!.AsArray();
            foreach (var alternative in alternatives.ToArray())
            {
                var values = alternative!.AsArray();
                RemoveWord(values);
                if (values.Count == 0) Assert.IsTrue(alternatives.Remove(alternative));
            }
        }
        File.WriteAllText(composerPath, composer.ToJsonString());

        // This checks composition and persistence, not battle execution of edited content.
        var catalog = PretCampaignCatalog.LoadCurrent(output.Path);
        Assert.IsFalse(catalog.MailComposer!.Words.ContainsKey(removedWord));
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

        static void RemoveWord(JsonArray values)
        {
            for (int index = values.Count - 1; index >= 0; index--)
                if (values[index]!.GetValue<int>() == removedWord) values.RemoveAt(index);
        }
    }
}
