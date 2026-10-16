using Game.Content;
namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class IntroMoviePresentationTests
{
    [TestMethod]
    public void EverySceneAndFightBeatRunsInOrderWithoutInput()
    {
        var movie = new IntroMoviePresentation(); var seen = new HashSet<IntroMovieStage>();
        for (int i = 0; i < IntroMoviePresentation.DurationFrames; i++) { seen.Add(movie.Stage); movie.Advance(1d / 60); }
        Assert.IsTrue(movie.Complete);
        Assert.AreEqual(IntroMoviePresentation.DurationFrames, movie.TotalFrames);
        foreach (var stage in Enum.GetValues<IntroMovieStage>().Where(s => s != IntroMovieStage.Complete)) Assert.IsTrue(seen.Contains(stage), stage.ToString());
    }
    [TestMethod]
    public void SplitDeltaAndSingleDeltaAgreeAndSkipRespectsSourceMask()
    {
        var a = new IntroMoviePresentation(); var b = new IntroMoviePresentation();
        Assert.IsFalse(a.Skip(true, true, true));
        a.Advance(4); for (int i = 0; i < 240; i++) b.Advance(1d / 60);
        Assert.AreEqual(a.Stage, b.Stage); Assert.AreEqual(a.Frame, b.Frame);
        Assert.IsFalse(a.Skip(false, false, false)); Assert.IsTrue(a.Skip(false, false, true)); Assert.IsTrue(a.Complete);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => b.Advance(double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => b.Advance(-1));
        Assert.AreEqual(IntroMoviePresentation.SparkleRandom(25), IntroMoviePresentation.SparkleRandom(25));
    }
    [TestMethod]
    public void NidorinoCryFiresAfterNineCrouchUpdatesAndIsConsumedOnlyOnce()
    {
        var movie = new IntroMoviePresentation();
        while (movie.Stage != IntroMovieStage.Cry) movie.Advance(1d / 60);
        Assert.IsFalse(movie.ConsumeNidorinoCry());
        movie.Advance(9d / 60);
        Assert.AreEqual(9, movie.Frame);
        Assert.IsFalse(movie.ConsumeNidorinoCry());
        movie.Advance(1d / 60);
        Assert.AreEqual(IntroMoviePresentation.NidorinoCryFrame, movie.Frame);
        Assert.IsTrue(movie.ConsumeNidorinoCry());
        Assert.IsFalse(movie.ConsumeNidorinoCry());
        movie.Advance(4);
        Assert.IsFalse(movie.ConsumeNidorinoCry());
    }
    [TestMethod]
    public void NidorinoCueSurvivesCrossingItsEntireStageWithoutDuplicating()
    {
        var movie = new IntroMoviePresentation();
        while (movie.Stage != IntroMovieStage.WaitCry) movie.Advance(1d / 60);
        movie.Advance(100d / 60);
        Assert.IsTrue(movie.Stage > IntroMovieStage.Cry);
        Assert.IsTrue(movie.ConsumeNidorinoCry());
        Assert.IsFalse(movie.ConsumeNidorinoCry());
        while (!movie.Complete) { movie.Advance(1d / 60); Assert.IsFalse(movie.ConsumeNidorinoCry()); }
    }
    [TestMethod]
    public void SkippingDoesNotReplayPendingCryAndNewMovieArmsItAgain()
    {
        var movie = new IntroMoviePresentation();
        while (movie.Stage != IntroMovieStage.Cry) movie.Advance(1d / 60);
        movie.Advance(10d / 60);
        Assert.IsTrue(movie.Skip(true, false, false));
        Assert.IsFalse(movie.ConsumeNidorinoCry());
        movie.Advance(20);
        Assert.IsFalse(movie.ConsumeNidorinoCry());
        movie = new IntroMoviePresentation();
        int cues = 0;
        while (!movie.Complete) { movie.Advance(1d / 60); if (movie.ConsumeNidorinoCry()) cues++; }
        Assert.AreEqual(1, cues);
    }
    [TestMethod]
    public void GeneratedOnlyLayersLoadWithoutRawSourceAndRejectTraversal()
    {
        string root = Path.Combine(Path.GetTempPath(), "firered-intro-" + Guid.NewGuid().ToString("N"));
        string relative = "content/generated/ui/intro-movie"; Directory.CreateDirectory(Path.Combine(root, relative));
        try
        {
            foreach (string file in Directory.GetFiles(Path.Combine(TestAssets.Root, relative))) File.Copy(file, Path.Combine(root, relative, Path.GetFileName(file)));
            var package = PretIntroMoviePresentation.Load(root);
            Assert.IsTrue(package.Textures.Length > 0);
            foreach (var texture in package.Textures) CollectionAssert.AreEqual(new byte[] {137,80,78,71,13,10,26,10}, package.ReadTexture(texture.Name).Take(8).ToArray());
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => package.ReadTexture("../raw"));
            File.Delete(Path.Combine(root, relative, "nidorino.png"));
            Assert.ThrowsExactly<InvalidDataException>(() => PretIntroMoviePresentation.Load(root));
        }
        finally { Directory.Delete(root, true); }
    }
}
