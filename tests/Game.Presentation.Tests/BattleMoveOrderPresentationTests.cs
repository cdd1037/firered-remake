using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Presentation;

[TestClass]
public sealed class BattleMoveOrderPresentationTests
{
    [TestMethod]
    [DataRow(0, 1)] [DataRow(1, 0)] [DataRow(2, 0)] [DataRow(3, 0)]
    public void SelectStartsAtSourceDestinationAndCancelRestoresSource(int source, int destination)
    {
        var draft = new BattleMoveOrderSelection(7, "battle:1", 4, 0, 23, source);
        Assert.AreEqual(destination, draft.InitialDestination);
        Assert.AreEqual(source, draft.CancelCursor);
        Assert.IsTrue(draft.Matches(7, "battle:1", 4, 0, 23));
        Assert.IsFalse(draft.Matches(8, "battle:1", 4, 0, 23));
        Assert.IsFalse(draft.Matches(7, "battle:2", 4, 0, 23));
        Assert.IsFalse(draft.Matches(7, "battle:1", 5, 0, 23));
        Assert.IsFalse(draft.Matches(7, "battle:1", 4, 2, 23));
        Assert.IsFalse(draft.Matches(7, "battle:1", 4, 0, 24));
    }

    [TestMethod]
    public void DestinationGridNeverSelectsAnEmptyMove()
    {
        Assert.AreEqual(1, BattleMoveOrderSelection.Navigate(0, 4, true));
        Assert.AreEqual(2, BattleMoveOrderSelection.Navigate(0, 4, false));
        Assert.AreEqual(0, BattleMoveOrderSelection.Navigate(0, 2, false));
        Assert.AreEqual(2, BattleMoveOrderSelection.Navigate(2, 3, true));
        Assert.AreEqual(0, BattleMoveOrderSelection.Navigate(2, 3, false));
    }
    [TestMethod]
    public void PlayTimeRebasesOnlyCurrentSelectDraftAndRetainsBattleRevision()
    {
        var draft = new BattleMoveOrderSelection(7, "battle:1", 4, 2, 23, 1);
        var kept = draft.AfterPlayTimeOnlyCommit(7, 8);
        Assert.IsTrue(kept.Matches(8, "battle:1", 4, 2, 23));
        Assert.AreEqual(draft.SourceSlot, kept.SourceSlot);
        Assert.AreSame(draft, draft.AfterPlayTimeOnlyCommit(8, 9));
        Assert.IsFalse(kept.Matches(8, "battle:1", 5, 2, 23));
    }
}
