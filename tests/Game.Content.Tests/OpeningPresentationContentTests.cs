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
        foreach (string name in PretOpeningPresentation.TextureNames)
            CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, opening.ReadTexture(name).Take(8).ToArray());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => opening.ReadTexture("../outside"));
    }

    [TestMethod]
    public void SourceGuideAndCompactAnimationTracksArePresent()
    {
        var opening = PretOpeningPresentation.Load(TestAssets.Root);
        Assert.AreEqual("The various buttons will be explained in\nthe order of their importance.", opening.Texts["gControlsGuide_Text_Intro"]);
        Assert.IsTrue(opening.Texts["gPikachuIntro_Text_Page3"].EndsWith("adventure begin!", StringComparison.Ordinal));
        CollectionAssert.AreEqual(new[] { 3, 6, 6, 6, 6, 6, 6, 6, 6, 6 }, opening.Animations["flames"].Select(f => f.Duration).ToArray());
        Assert.AreEqual(60, opening.Animations["pikachu-body"].Sum(f => f.Duration));
        Assert.AreEqual(15, opening.Animations["pikachu-ears"].Length);
        CollectionAssert.AreEqual(new[] { "GREEN", "GARY", "KAZ", "TORU" }, opening.RivalNames.ToArray());
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
