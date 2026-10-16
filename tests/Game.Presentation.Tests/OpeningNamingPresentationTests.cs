using Game.Content;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class OpeningNamingPresentationTests
{
    [TestMethod]
    public void NavigationWrapsAndPageChangesKeepCursorOnValidAction()
    {
        var grid = new OpeningNamingPresentation(PretOpeningPresentation.Load(TestAssets.Root));
        Assert.AreEqual(new OpeningNameKey(OpeningNameAction.Insert, 'A'), grid.Activate());
        grid.Move(-1, 0); Assert.IsTrue(grid.OnAction);
        Assert.AreEqual(OpeningNameAction.ChangePage, grid.Activate().Action);
        Assert.IsTrue(grid.Swapping); grid.AdvanceFrames(33); Assert.AreEqual(1, grid.Page);
        grid.Activate(); grid.AdvanceFrames(33); Assert.AreEqual(2, grid.Page); Assert.AreEqual(6, grid.Column);
        grid.Move(0, -1); Assert.AreEqual(2, grid.Row);
        Assert.AreEqual(OpeningNameAction.Submit, grid.Activate().Action);
        grid.Move(0, -1); Assert.AreEqual(OpeningNameAction.Delete, grid.Activate().Action);
        grid.Move(1, 0); Assert.AreEqual(0, grid.Column);
        Assert.AreEqual('5', grid.Activate().Character);
    }

    [TestMethod]
    public void FourthCharacterRowClampsWhenMovingOntoButtonsAndResetRestoresUppercase()
    {
        var grid = new OpeningNamingPresentation(PretOpeningPresentation.Load(TestAssets.Root));
        grid.Select(3, 7); grid.Move(1, 0); Assert.AreEqual(2, grid.Row);
        Assert.AreEqual(OpeningNameAction.Submit, grid.Activate().Action);
        grid.Select(0, grid.Columns); grid.Activate(); grid.AdvanceFrames(33); grid.SelectSubmit();
        Assert.AreEqual(OpeningNameAction.Submit, grid.Activate().Action);
        grid.Reset(); Assert.AreEqual(0, grid.Page); Assert.AreEqual(0, grid.Row); Assert.AreEqual(0, grid.Column);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => grid.Select(4, 0));
    }

    [TestMethod]
    public void PageSwapUsesNativeSinePriorityChangeAndRejectsRepeatedActivation()
    {
        var source = PretOpeningPresentation.Load(TestAssets.Root);
        var sine = CaptureBallPresentationAssets.Load(TestAssets.Root).Sine;
        var grid = new OpeningNamingPresentation(source);
        grid.Select(0, grid.Columns); grid.Activate();
        CollectionAssert.AreEqual(new[] { new OpeningNamingLayer(1, 0), new OpeningNamingLayer(0, 0) }, grid.Layers(sine).ToArray());
        Assert.AreEqual(OpeningNameAction.None, grid.Activate().Action);
        grid.Move(1, 1); Assert.AreEqual(8, grid.Column); Assert.AreEqual(0, grid.Row);
        grid.AdvanceFrames(8); Assert.IsFalse(grid.PageButtonTextVisible); Assert.AreEqual(2, grid.PageButtonPage);
        grid.AdvanceFrames(8);
        CollectionAssert.AreEqual(new[] { new OpeningNamingLayer(0, 40), new OpeningNamingLayer(1, -40) }, grid.Layers(sine).ToArray());
        Assert.AreEqual(0, grid.Page); Assert.IsTrue(grid.Swapping);
        grid.AdvanceFrames(16); Assert.IsTrue(grid.Swapping);
        grid.AdvanceFrames(1); Assert.IsFalse(grid.Swapping); Assert.AreEqual(1, grid.Page);
        CollectionAssert.AreEqual(new[] { new OpeningNamingLayer(1, 0) }, grid.Layers(sine).ToArray());
        Assert.ThrowsExactly<ArgumentException>(() => grid.Layers([]));
    }
    [TestMethod]
    public void PageSwapAndDecorationClockAreChunkIndependentAndResettable()
    {
        var source = PretOpeningPresentation.Load(TestAssets.Root);
        var a = new OpeningNamingPresentation(source); var b = new OpeningNamingPresentation(source);
        foreach (var grid in new[] { a, b }) { grid.Select(0, grid.Columns); grid.Activate(); }
        a.AdvanceFrames(20); for (int i = 0; i < 20; i++) b.AdvanceFrames(1);
        Assert.AreEqual(a.PageButtonOffsetY, b.PageButtonOffsetY); Assert.AreEqual(a.ArrowOffsetX, b.ArrowOffsetX);
        Assert.AreEqual(a.UnderscoreOffsetY, b.UnderscoreOffsetY);
        a.Reset(); Assert.IsFalse(a.Swapping); Assert.AreEqual(0, a.VisualFrame); Assert.AreEqual(0, a.Page);
        a.Select(0, 7); Assert.AreEqual(161, a.CursorX);
    }
    [TestMethod]
    public void TrainerPortraitFadeHonorsUnequalSourceStepsPlatformToggleAndHold()
    {
        var fade = new OpeningPortraitTransition("red", "rival", 40);
        Assert.AreEqual(1f, fade.Alpha); Assert.IsTrue(fade.PlatformVisible);
        fade.AdvanceFrames(2); Assert.AreEqual(1f, fade.Alpha);
        fade.AdvanceFrames(1); Assert.AreEqual(15f / 16, fade.Alpha);
        fade.AdvanceFrames(21); Assert.AreEqual(.5f, fade.Alpha); Assert.IsFalse(fade.PlatformVisible);
        fade.AdvanceFrames(25); Assert.AreEqual("rival", fade.Portrait); Assert.AreEqual(0f, fade.Alpha);
        fade.AdvanceFrames(40); Assert.AreEqual(0f, fade.Alpha);
        fade.AdvanceFrames(3); Assert.AreEqual(.125f, fade.Alpha);
        fade.AdvanceFrames(9); Assert.AreEqual(.5f, fade.Alpha); Assert.IsTrue(fade.PlatformVisible);
        fade.AdvanceFrames(12); Assert.AreEqual(1f, fade.Alpha); Assert.IsFalse(fade.Complete);
        fade.AdvanceFrames(1); Assert.IsTrue(fade.Complete);
        var hide = new OpeningPortraitTransition("oak", null, 48); hide.AdvanceFrames(96); Assert.IsFalse(hide.Complete);
        hide.AdvanceFrames(1); Assert.IsTrue(hide.Complete);
    }
}
