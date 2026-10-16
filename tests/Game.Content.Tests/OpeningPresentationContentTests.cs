using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class OpeningPresentationContentTests
{
    [TestMethod]
    public void SourcePagesAndKeyboardPreserveDialogueAndSupportedCharacters()
    {
        var opening = PretOpeningPresentation.Load(TestAssets.Root);
        Assert.AreEqual(4, opening.Pages("WelcomeToTheWorld", "RED", "BLUE").Length);
        Assert.AreEqual("Hello, there!\nGlad to meet you!", opening.Pages("WelcomeToTheWorld", "RED", "BLUE")[0]);
        Assert.AreEqual("Right…\nSo your name is LEAF.", opening.Pages("SoYourNameIsPlayer", "LEAF", "BLUE")[0]);
        Assert.AreEqual("…Er, was it GREEN?", opening.Pages("ConfirmRivalName", "RED", "GREEN")[0]);
        Assert.AreEqual("RED!", opening.Pages("LetsGo", "RED", "BLUE")[0]);
        Assert.AreEqual("ABCDEF .", opening.Keyboard[0][0]);
        Assert.AreEqual("abcdef .", opening.Keyboard[1][0]);
        Assert.AreEqual("!?♂♀/-", opening.Keyboard[2][2]);
        foreach (string name in new[] { "title", "background", "oak", "red", "leaf", "rival", "platform", "nidoran-f" })
            CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, opening.ReadTexture(name).Take(8).ToArray());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => opening.ReadTexture("../outside"));
    }

    [TestMethod]
    public void IsolatedGeneratedPackageLoadsAndRejectsIncompleteKeyboard()
    {
        string root = Path.Combine(Path.GetTempPath(), "firered-opening-" + Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(root, "content/generated/ui/opening");
        try
        {
            Directory.CreateDirectory(directory);
            foreach (string path in Directory.GetFiles(Path.Combine(TestAssets.Root, "content/generated/ui/opening"))) File.Copy(path, Path.Combine(directory, Path.GetFileName(path)));
            Assert.AreEqual(3, PretOpeningPresentation.Load(root).Keyboard.Length);
            string pathToJson = Path.Combine(directory, "opening.json");
            var json = JsonNode.Parse(File.ReadAllText(pathToJson))!;
            json["keyboard"]![0]![0] = "";
            File.WriteAllText(pathToJson, json.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => PretOpeningPresentation.Load(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
