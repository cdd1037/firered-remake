using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedBattleTextTests
{
    [TestMethod]
    public void LoadsDialogueWithoutOriginalSources()
    {
        var root = Path.Combine(Path.GetTempPath(), "firered-battle-text-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "content/generated/battle-text");
        Directory.CreateDirectory(output);
        try
        {
            File.Copy(Path.Combine(TestAssets.Root, "content/generated/battle-text/catalog.json"), Path.Combine(output, "catalog.json"));
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
            foreach (var relative in new[] { "presentation-cues/fanfares.json", "source-grid-menus/manifest.json" })
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
        var output = Path.Combine(root, "content/generated/early-pickups");
        Directory.CreateDirectory(output);
        try
        {
            File.Copy(Path.Combine(TestAssets.Root, "content/generated/early-pickups/early-pickups.json"), Path.Combine(output, "early-pickups.json"));
            var pickups = PretEarlyPickups.Load(root);
            Assert.AreEqual(8, pickups.Pickups.Length);
            Assert.AreEqual(3, pickups.Pickups.Count(p => p.Kind == "hiddenItem"));
            Assert.IsNotNull(pickups.FindHiddenItem("MAP_PEWTER_CITY", 6, 3));
        }
        finally { Directory.Delete(root, true); }
    }
}
