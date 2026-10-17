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
        CollectionAssert.AreEqual(new[] { 0, 12, 24, 56, 68, 80, 92, 123 }, opening.NamingColumnOffsets[0].ToArray());
        CollectionAssert.AreEqual(new[] { 0, 22, 44, 66, 88, 110 }, opening.NamingColumnOffsets[2].ToArray());
        CollectionAssert.AreEqual(new[] { 3, 0, 4, 0 }, opening.Animations["naming-player"].Select(f => f.Frame).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 3, 0, 4 }, opening.Animations["naming-rival"].Select(f => f.Frame).ToArray());
        CollectionAssert.AreEqual(new[] { "GREEN", "GARY", "KAZ", "TORU" }, opening.RivalNames.ToArray());
    }

    [TestMethod]
    public void SharedNamingTemplatesPreserveSourceLimitsTitlesIconsAndGeometry()
    {
        var opening = PretOpeningPresentation.Load(TestAssets.Root);
        var templates = opening.NamingTemplates;
        Assert.AreEqual(new NamingScreenTemplate(false, 7, 1, false, 0, "YOUR NAME?", false, 56, 37), templates["NAMING_SCREEN_PLAYER"]);
        Assert.AreEqual(new NamingScreenTemplate(false, 8, 2, false, 0, "BOX NAME?", false, 56, 41), templates["NAMING_SCREEN_BOX"]);
        Assert.AreEqual(new NamingScreenTemplate(false, 10, 3, true, 0, "'s nickname?", true, 56, 40), templates["NAMING_SCREEN_CAUGHT_MON"]);
        Assert.AreEqual(templates["NAMING_SCREEN_CAUGHT_MON"], templates["NAMING_SCREEN_NICKNAME"]);
        Assert.AreEqual(new NamingScreenTemplate(false, 7, 4, false, 0, "RIVAL's NAME?", false, 56, 37), templates["NAMING_SCREEN_RIVAL"]);
        Assert.AreEqual(new NamingScreenLayout(240, 73, 33, 49, 8, 6, 168, 49), opening.NamingLayout);
        Assert.AreEqual(98, opening.NamingLayout.EntryX(templates["NAMING_SCREEN_PLAYER"].MaxChars));
        Assert.AreEqual(94, opening.NamingLayout.EntryX(templates["NAMING_SCREEN_BOX"].MaxChars));
        Assert.AreEqual(86, opening.NamingLayout.EntryX(templates["NAMING_SCREEN_NICKNAME"].MaxChars));
        CollectionAssert.AreEqual(new[] { new OpeningAnimationFrame(0, 2), new OpeningAnimationFrame(1, 2) }, opening.Animations["naming-pc"].ToArray());
        byte[] pc = opening.ReadTexture("naming-pc-icon");
        Assert.AreEqual(16, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(pc.AsSpan(16, 4)));
        Assert.AreEqual(48, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(pc.AsSpan(20, 4)));
    }

    [TestMethod]
    public void GeneratedOnlyNamingContractRejectsMissingAndOutOfRangeMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "firered-naming-" + Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(root, "content/generated/ui/opening");
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "opening.json");
            var source = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/ui/opening/opening.json")))!;
            File.WriteAllText(path, source.ToJsonString());
            Assert.AreEqual(5, PretOpeningPresentation.Load(root).NamingTemplates.Count);
            void Reject(Action<JsonNode> mutate)
            {
                var json = source.DeepClone(); mutate(json);
                File.WriteAllText(path, json.ToJsonString());
                Assert.ThrowsExactly<InvalidDataException>(() => PretOpeningPresentation.Load(root));
            }
            Reject(json => json.AsObject().Remove("namingTemplates"));
            Reject(json => json["namingTemplates"]!.AsObject().Remove("NAMING_SCREEN_NICKNAME"));
            Reject(json => json["namingTemplates"]!["NAMING_SCREEN_PLAYER"]!.AsObject().Remove("copyExistingString"));
            Reject(json => json["namingTemplates"]!["NAMING_SCREEN_PLAYER"]!["maxChars"] = 11);
            Reject(json => json["namingTemplates"]!["NAMING_SCREEN_BOX"]!["maxChars"] = 9);
            Reject(json => json["namingTemplates"]!["NAMING_SCREEN_PLAYER"]!["initialPage"] = 3);
            Reject(json => json["namingTemplates"]!["NAMING_SCREEN_PLAYER"]!["iconFunction"] = 0);
            Reject(json => json["namingTemplates"]!["NAMING_SCREEN_BOX"]!["addGenderIcon"] = true);
            Reject(json => json["namingTemplates"]!["NAMING_SCREEN_NICKNAME"]!["title"] = "different title");
            Reject(json => json["namingTemplates"]!["NAMING_SCREEN_PLAYER"]!["title"] = "");
            Reject(json => json["namingTemplates"]!["NAMING_SCREEN_RIVAL"]!["iconX"] = 240);
            Reject(json => json.AsObject().Remove("namingLayout"));
            Reject(json => json["namingLayout"]!["entryCellWidth"] = 32);
            Reject(json => json["animations"]!["naming-pc"]![0]!["frame"] = 2);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
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
            json["namingColumnOffsets"]![0]![1] = 0;
            File.WriteAllText(pathToJson, json.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => PretOpeningPresentation.Load(root));
            json["namingColumnOffsets"]![0]![1] = 12;
            json["keyboard"]![0]![0] = "";
            File.WriteAllText(pathToJson, json.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => PretOpeningPresentation.Load(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
