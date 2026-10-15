using System.Text.Json;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class PresentationResourceInventoryTests
{
    [TestMethod]
    public void PresentationLoadersUseTextureBindingsWithoutImportInventories()
    {
        AssertFiles(PretSeafoamPresentation.Load(TestAssets.Root).Files, "sprites/pokemon/seafoam");
        AssertFiles(PretMemorialPillarPresentation.Load(TestAssets.Root).Files, "sprites/pokemon/memorial-pillar");
        AssertFiles(PretQwilfishPresentation.Load(TestAssets.Root).Files, "sprites/pokemon/qwilfish");
        AssertFiles(PretStoneEvolutionPresentation.Load(TestAssets.Root).Files, "sprites/pokemon/stone-evolution");
        AssertFiles(PretOrdinaryEvolutionPresentation.Load(TestAssets.Root).Files, "sprites/pokemon/ordinary-evolution");
        Assert.AreEqual(4, PretSsAnneBattleArt.Load(TestAssets.Root).Portraits.Length);
        Assert.AreEqual(3, PretVermilionGymFieldMoveArt.Load(TestAssets.Root).Assets.Length);
    }

    private static void AssertFiles(Dictionary<string, byte[]> files, string package)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated", package, "manifest.json")));
        Assert.IsFalse(document.RootElement.TryGetProperty("inputs", out _));
        Assert.IsFalse(document.RootElement.TryGetProperty("inheritedInputs", out _));
        Assert.IsTrue(files.Count > 0);
        foreach (var pair in files)
        {
            Assert.IsTrue(pair.Key.StartsWith("content/generated/", StringComparison.Ordinal));
            Assert.IsTrue(pair.Value.Length > 0);
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(TestAssets.Root, pair.Key)), pair.Value);
        }
    }
}
