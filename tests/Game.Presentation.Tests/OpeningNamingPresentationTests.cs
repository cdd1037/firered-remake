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
        Assert.AreEqual(1, grid.Page);
        grid.Activate(); Assert.AreEqual(2, grid.Page); Assert.AreEqual(6, grid.Column);
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
        grid.Select(0, grid.Columns); grid.Activate(); grid.SelectSubmit();
        Assert.AreEqual(OpeningNameAction.Submit, grid.Activate().Action);
        grid.Reset(); Assert.AreEqual(0, grid.Page); Assert.AreEqual(0, grid.Row); Assert.AreEqual(0, grid.Column);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => grid.Select(4, 0));
    }
}
