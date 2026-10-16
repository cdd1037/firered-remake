namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class PokemonLeagueScenePresentationTests
{
    [TestMethod]
    public void CreditsCameraUsesHalfRatePixelsAndStopsAtSourceCommandLength()
    {
        Assert.AreEqual(0,PokemonLeagueScenePresentation.CameraPixels(1,1280));
        Assert.AreEqual(1,PokemonLeagueScenePresentation.CameraPixels(2,1280));
        Assert.AreEqual(1280,PokemonLeagueScenePresentation.CameraPixels(9000,1280));
    }
    [TestMethod]
    public void CreditsRunnerKeepsAuthoredSceneRolesGroundAndEntryMotion()
    {
        Assert.AreEqual((272,80,3,false,false),PokemonLeagueScenePresentation.Runner(0,0));
        Assert.AreEqual((208,80,3,false,false),PokemonLeagueScenePresentation.Runner(0,64));
        Assert.AreEqual((208,160,0,true,true),PokemonLeagueScenePresentation.Runner(3,0));
        Assert.AreEqual((208,80,0,true,true),PokemonLeagueScenePresentation.Runner(3,160));
        Assert.AreEqual(2,PokemonLeagueScenePresentation.Runner(1,200).Ground);
        Assert.IsFalse(PokemonLeagueScenePresentation.Runner(4,200).Rival);
    }
    [TestMethod]
    public void PokemonRevealUsesAuthoredPicturesAndInverseAffineScale()
    {
        Assert.AreEqual(0,PokemonLeagueScenePresentation.ZoomPicture(53));
        Assert.AreEqual(1,PokemonLeagueScenePresentation.ZoomPicture(54));
        Assert.AreEqual(2,PokemonLeagueScenePresentation.ZoomPicture(59));
        Assert.AreEqual(0,PokemonLeagueScenePresentation.CircleAffine(60));
        Assert.AreEqual(16,PokemonLeagueScenePresentation.CircleAffine(61));
        Assert.AreEqual(256,PokemonLeagueScenePresentation.CircleAffine(76));
        Assert.AreEqual(1f,PokemonLeagueScenePresentation.MonFade(0));
        Assert.AreEqual(0f,PokemonLeagueScenePresentation.MonFade(100));
        Assert.AreEqual(1f,PokemonLeagueScenePresentation.MonFade(259));
    }
    [TestMethod]
    public void ConfettiIsBoundedReproducibleAndCatchesUpAcrossDroppedFrames()
    {
        int[] sine=Enumerable.Range(0,256).Select(i=>(int)(Math.Sin(i*Math.PI/128)*256)).ToArray();
        var a=new HallConfettiPresentation(7);var b=new HallConfettiPresentation(7);
        a.AdvanceTo(2,sine);Assert.AreEqual(0,a.Pieces.Count);
        a.AdvanceTo(3,sine);Assert.AreEqual(1,a.Pieces.Count);
        for(int i=0;i<=200;i++)b.AdvanceTo(i,sine);
        a.AdvanceTo(200,sine);
        CollectionAssert.AreEqual(a.Pieces.Select(p=>(p.X,p.Y,p.OffsetX,p.OffsetY,p.Frame)).ToArray(),b.Pieces.Select(p=>(p.X,p.Y,p.OffsetX,p.OffsetY,p.Frame)).ToArray());
        Assert.IsTrue(a.Pieces.Count>0&&a.Pieces.Count<=31);
        a.AdvanceTo(420,sine);Assert.AreEqual(0,a.Pieces.Count);
    }
}
