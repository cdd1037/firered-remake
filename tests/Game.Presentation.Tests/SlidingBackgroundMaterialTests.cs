using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SlidingBackgroundMaterialTests
{
    [TestMethod]
    public void SlidingMaterialsMatchSourceMapFlipPaletteAndSmallMapWrapVectors()
    {
        using var catalog=new SourceAnimationBackgroundCatalog(Path.Combine(TestAssets.Root,"content/generated/battle/animations/backgrounds"));
        using var vectors=SourceVectorFixture.Read("sliding-background-pixels.json", "python tests/Game.Presentation.Tests/Reference/generate_sliding_background_pixels.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/sliding-background-pixels.json");
        foreach(var row in vectors.RootElement.EnumerateArray())
        {
            var surface=catalog.CreateSurface(0);surface.Apply(catalog.Move(row.GetProperty("id").GetInt32()));
            foreach(var sample in row.GetProperty("samples").EnumerateArray())
            {
                int x=sample[0].GetInt32(),y=sample[1].GetInt32(),bank=sample[2].GetInt32(),index=sample[3].GetInt32(),color=sample[4].GetInt32();
                Assert.AreEqual(new SourceBackgroundPixel(bank,index),surface.Sample(x,y,0));var pixel=new ushort[1];surface.Render(pixel,1,1,x,y,screenSize:0);
                Assert.AreEqual((ushort)(0x8000|(color<0?surface.PaletteColor(0,0):color)),pixel[0]);
            }
        }
        CollectionAssert.AreEqual(catalog.Move(7).Tiles.ToArray(),catalog.Move(18).Tiles.ToArray());CollectionAssert.AreEqual(catalog.Move(7).Tilemap.ToArray(),catalog.Move(18).Tilemap.ToArray());CollectionAssert.AreNotEqual(catalog.Move(7).Palette.ToArray(),catalog.Move(18).Palette.ToArray());
        CollectionAssert.AreEqual(catalog.Move(9).Tiles.ToArray(),catalog.Move(10).Tiles.ToArray());CollectionAssert.AreEqual(catalog.Move(9).Palette.ToArray(),catalog.Move(10).Palette.ToArray());CollectionAssert.AreNotEqual(catalog.Move(9).Tilemap.ToArray(),catalog.Move(10).Tilemap.ToArray());
    }
}
