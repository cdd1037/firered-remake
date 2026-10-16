using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class PokedexVisualTests
{
    [TestMethod]
    public void NativeVisualAtlasHasAllHabitatsTypesAndBothPaletteModes()
    {
        var menu=PretPokedexMenu.Load(TestAssets.Root);
        var sprites=menu.Document.Visuals.Sprites;
        foreach(var category in menu.Document.Habitats)Assert.IsTrue(sprites.ContainsKey("category/"+category.Id));
        foreach(var type in menu.Entries.Values.SelectMany(e=>new[]{e.Type1,e.Type2}).Distinct())
        {Assert.AreEqual(32,sprites[type].Width);Assert.AreEqual(12,sprites[type].Height);}
        foreach(var mode in new[]{"kanto","national"})
        {Assert.AreEqual(64,sprites[mode+"/circle/0"].Width);Assert.AreEqual(40,sprites[mode+"/mini"].Height);Assert.AreEqual(24,sprites[mode+"/key/START_BUTTON"].Width);}
        CollectionAssert.AreEqual(Enumerable.Range(0,30).ToArray(),menu.Document.Visuals.PageTurnColumns[9].ToArray());
    }
    [TestMethod]
    public void ZoomFramesStayInNativeWindowAndEndAtFullDataFrame()
    {
        var menu=PretPokedexMenu.Load(TestAssets.Root);
        foreach(var slot in menu.Document.HabitatLayouts.SelectMany(s=>s))
        {
            for(int scale=0;scale<=6;scale++)
            {
                var f=PokedexZoomGeometry.Frame(slot,scale);
                Assert.IsTrue(f.Left>=0&&f.Left+f.Width+2<=30);
                Assert.IsTrue(f.Top>=2&&f.Top+f.Height+2<=18);
                Assert.IsTrue(f.Divider>f.Top&&f.Divider<f.Top+f.Height+1);
            }
            Assert.AreEqual((0,2,28,14,11),PokedexZoomGeometry.Frame(slot,6));
        }
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>PokedexZoomGeometry.Frame(new(0,0,0,0),7));
    }
    [TestMethod]
    public void PageTurnFadesOldLeftAndNewRightWithoutChangingSimulation()
    {
        var visuals=PretPokedexMenu.Load(TestAssets.Root).Document.Visuals;
        Assert.AreEqual(2,visuals.FadeStep);Assert.AreEqual(23,visuals.FadeFrames);
        Assert.AreEqual(new PokedexTurnFrame(false,false,9,0),PokedexTurnGeometry.Frame(visuals,false,0));
        Assert.AreEqual(new PokedexTurnFrame(false,false,9,16),PokedexTurnGeometry.Frame(visuals,false,16));
        Assert.AreEqual(new PokedexTurnFrame(false,true,0,0),PokedexTurnGeometry.Frame(visuals,false,23));
        Assert.AreEqual(new PokedexTurnFrame(false,false,0,0),PokedexTurnGeometry.Frame(visuals,true,9));
        Assert.AreEqual(new PokedexTurnFrame(false,true,9,16),PokedexTurnGeometry.Frame(visuals,true,10));
        Assert.AreEqual(new PokedexTurnFrame(false,true,9,0),PokedexTurnGeometry.Frame(visuals,true,26));
        Assert.IsTrue(PokedexTurnGeometry.Frame(visuals,false,33).Complete);
        Assert.IsTrue(PokedexTurnGeometry.Frame(visuals,true,33).Complete);
    }
}
