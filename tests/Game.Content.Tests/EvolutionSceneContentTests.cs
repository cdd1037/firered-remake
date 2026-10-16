using Game.Content;
namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class EvolutionSceneContentTests
{
    [TestMethod]
    public void GeneratedSceneIncludesEveryPaletteLayerAndHatchBackground()
    {
        var scene=PretEvolutionScene.Load(TestAssets.Root);
        Assert.AreEqual(50,scene.Document.PaletteCount);
        Assert.AreEqual(4,scene.Document.Assets.Length);
        Assert.AreEqual(10,scene.Document.AtlasColumns);
        Assert.AreEqual(4,scene.Document.PaletteControls.Length);
        Assert.IsTrue(scene.Texture("hatch").Length>33);
        Assert.IsTrue(scene.Texture("sparkle").Length>33);
        for(int layer=0;layer<2;layer++)Assert.IsTrue(scene.Texture($"background-{layer}").Length>33);
    }
    [TestMethod]
    public void PaletteCycleHonorsSourcePostIncrementDelayAndReturnsCopies()
    {
        var scene=PretEvolutionScene.Load(TestAssets.Root);
        Assert.AreEqual(0,scene.PaletteAt(0));Assert.AreEqual(0,scene.PaletteAt(21));
        Assert.AreEqual(0,scene.PaletteAt(29));Assert.AreEqual(1,scene.PaletteAt(37));
        for(int frame=0;frame<1800;frame++)Assert.IsTrue(scene.PaletteAt(frame)>=0 && scene.PaletteAt(frame)<50);
        var bytes=scene.Texture("hatch");bytes[0]=0;
        Assert.AreEqual((byte)137,scene.Texture("hatch")[0]);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>scene.PaletteAt(-1));
    }
}
