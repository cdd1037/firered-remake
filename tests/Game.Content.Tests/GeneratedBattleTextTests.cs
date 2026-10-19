using Game.Domain.Events;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedBattleTextTests
{
    [TestMethod]
    public void BattleTemplatesBindLiteralNamesOnceAndKeepSourcePageControls()
    {
        var source = new Dictionary<string, string> {
            ["message"] = "{ACTOR} used {MOVE}!{WAIT_SE}\\nNext\\lLast{PAUSE 60}\\pDone{PAUSE_UNTIL_PRESS}"
        };
        var values = new Dictionary<string, string> {
            ["ACTOR"] = "Literal {MOVE}\\p{WAIT_SE}", ["MOVE"] = "Name {ACTOR}\\n"
        };
        Assert.AreSequenceEqual(new[] {
            "Literal {MOVE}\\p{WAIT_SE} used Name {ACTOR}\\n!\nNext", "Next\nLast", "Done"
        }, SourceBattleText.Pages(source, "message", values));
    }

    [TestMethod]
    [DataRow("{MISSING}")]
    [DataRow("{ACTOR")]
    [DataRow("ACTOR}")]
    [DataRow("{PAUSE 61}")]
    [DataRow("bad\\x")]
    [DataRow("bad\\")]
    public void BattleTemplatesStillRejectUnknownOrMalformedControls(string template)
    {
        Assert.ThrowsExactly<InvalidDataException>(() => SourceBattleText.Pages(
            new Dictionary<string, string> { ["message"] = template }, "message", new Dictionary<string, string>()));
    }

    [TestMethod]
    public void LoadsDialogueWithoutOriginalSources()
    {
        var root = Path.Combine(Path.GetTempPath(), "firered-battle-text-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "content/generated/battle/text.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        try
        {
            File.Copy(Path.Combine(TestAssets.Root, "content/generated/battle/text.json"), output);
            var texts = PretBattleText.Load(root);
            Assert.IsTrue(texts.ContainsKey("sText_AttackerUsedX"));
            Assert.IsTrue(texts.ContainsKey("gText_OakNoRunningFromATrainer"));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
        }
        finally { Directory.Delete(root, true); }
    }
    [TestMethod]
    public void FanfareFallbackAndMenusNeedOnlyGeneratedData()
    {
        var root = Path.Combine(Path.GetTempPath(), "firered-presentation-data-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var relative in new[] { "audio/fanfares.json", "ui/grid-menus.json" })
            {
                var output = Path.Combine(root, "content/generated", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.Copy(Path.Combine(TestAssets.Root, "content/generated", relative), output);
            }
            var cues = PretPresentationCues.Load(root);
            Assert.AreEqual(cues.CounterFor("MUS_LEVEL_UP"), cues.CounterFor("UNKNOWN_CUE"));
            Assert.IsTrue(PretSourceGridMenus.Load(root).Count > 0);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [TestMethod]
    public void EarlyPickupDefinitionsLoadWithoutSourcesOrProofs()
    {
        var root = Path.Combine(Path.GetTempPath(), "firered-pickup-data-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "content/generated/data/items/early-pickups.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        try
        {
            File.Copy(Path.Combine(TestAssets.Root, "content/generated/data/items/early-pickups.json"), output);
            var pickups = PretEarlyPickups.Load(root);
            Assert.AreEqual(8, pickups.Pickups.Length);
            Assert.AreEqual(3, pickups.Pickups.Count(p => p.Kind == "hiddenItem"));
            Assert.IsNotNull(pickups.FindHiddenItem("MAP_PEWTER_CITY", 6, 3));
        }
        finally { Directory.Delete(root, true); }
    }
}
