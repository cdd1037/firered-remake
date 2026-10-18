using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class EasyChatProfileContentTests
{
    [TestMethod]
    public void GeneratedTemplatesShareVocabularyAndPreserveSourceProfilePhrases()
    {
        WithComposerCopy(root =>
        {
            var catalog = PretMailComposer.Load(root).Catalog;
            var profile = catalog.Templates["profile"];
            var mail = catalog.Templates["mail"];
            Assert.AreEqual((0, 2, 2, 4, 0), ((int)profile.Type, profile.Columns, profile.Rows, profile.WordCount, profile.FrameId));
            Assert.AreEqual((4, 2, 5, 9, 2), ((int)mail.Type, mail.Columns, mail.Rows, mail.WordCount, mail.FrameId));
            Assert.AreEqual("PROFILE", catalog.Texts[profile.TitleTextSymbol!]);
            CollectionAssert.AreEqual(new[] { "Combine four words or phrases", "and make your profile." },
                profile.InstructionTextSymbols.Select(s => catalog.Texts[s]).ToArray());
            CollectionAssert.AreEqual(new[] { "Your profile", "is as shown. Okay?" },
                profile.ConfirmTextSymbols.Select(s => catalog.Texts[s]).ToArray());
            Assert.AreEqual("Quit editing?", catalog.Texts[profile.CancelTextSymbols.Single()]);
            Assert.AreEqual("gText_StopGivingPkmnMail", mail.CancelTextSymbols.Single());
            Assert.IsNotNull(catalog.ProfileDefinition);
            CollectionAssert.AreEqual(new[] { "I AM", "A", "POKéMON", "FRIEND" },
                catalog.ProfileDefinition.DefaultWords.Select(w => catalog.Words[w].Text).ToArray());
            CollectionAssert.AreEqual(new[] { "MYSTERY", "EVENT", "IS", "EXCITING" },
                catalog.ProfileDefinition.SpecialPhrase.Select(w => catalog.Words[w].Text).ToArray());
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
        });
    }

    [TestMethod]
    [DataRow("word_count")]
    [DataRow("unknown_text")]
    [DataRow("phrase_size")]
    [DataRow("blank_phrase")]
    [DataRow("profile_type")]
    public void MalformedEditorDefinitionsFailAtContentBoundary(string change)
    {
        WithComposerCopy(root =>
        {
            var path = Path.Combine(root, "content/generated/data/services/mail-composer/contracts.json");
            var document = JsonNode.Parse(File.ReadAllText(path))!;
            switch (change)
            {
                case "word_count": document["templates"]![0]!["wordCount"] = 9; break;
                case "unknown_text": document["templates"]![0]!["titleTextSymbol"] = "missing"; break;
                case "phrase_size": document["profileDefinition"]!["defaultWords"]!.AsArray().RemoveAt(0); break;
                case "blank_phrase": document["profileDefinition"]!["specialPhrase"]![0] = ushort.MaxValue; break;
                case "profile_type": document["profileDefinition"]!["type"] = 4; break;
            }
            File.WriteAllText(path, document.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => PretMailComposer.Load(root));
        });
    }

    [TestMethod]
    public void SettledTemplatePixelsReuseSharedMailAssetsWithSourceFrameAndTitleCoordinates()
    {
        var presentation = PretMailPresentation.Load(TestAssets.Root);
        var profile = presentation.Templates["profile"];
        var mail = presentation.Templates["mail"];
        Assert.AreEqual((3, 4, 24, 4, 41, 34), (profile.FrameLeft, profile.FrameTop, profile.FrameWidth, profile.FrameHeight, profile.FieldX, profile.FieldY));
        Assert.AreEqual((3, 0, 24, 10, 41, 2), (mail.FrameLeft, mail.FrameTop, mail.FrameWidth, mail.FrameHeight, mail.FieldX, mail.FieldY));
        Assert.AreEqual((56, 2, 128), (profile.TitleX, profile.TitleY, profile.TitleWidth));
        foreach (string mode in new[] { "fields", "groups", "words" })
        {
            Assert.AreEqual("easy-chat-" + mode, mail.BackgroundIds[mode]);
            var asset = presentation.Assets[profile.BackgroundIds[mode]];
            Assert.AreEqual((240, 160), (asset.Width, asset.Height));
            Assert.IsTrue(asset.PngPath.StartsWith(PretMailPresentation.Base, StringComparison.Ordinal));
            Assert.IsTrue(presentation.PngFiles[asset.Id].Length > 0);
        }
        CollectionAssert.AreEqual(new byte[] { 57, 206, 255 }, presentation.EasyChat.GetProperty("titleRgb").EnumerateArray().Select(c => c.GetByte()).ToArray());
        CollectionAssert.AreEqual(new byte[] { 173, 173, 239 }, presentation.EasyChat.GetProperty("titleShadowRgb").EnumerateArray().Select(c => c.GetByte()).ToArray());
    }

    private static void WithComposerCopy(Action<string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "firered-profile-content-" + Guid.NewGuid().ToString("N"));
        string output = Path.Combine(root, "content/generated/data/services/mail-composer");
        Directory.CreateDirectory(output);
        try
        {
            foreach (string name in new[] { "catalog.json", "contracts.json" })
                File.Copy(Path.Combine(TestAssets.Root, "content/generated/data/services/mail-composer", name), Path.Combine(output, name));
            check(root);
        }
        finally { Directory.Delete(root, true); }
    }
}
