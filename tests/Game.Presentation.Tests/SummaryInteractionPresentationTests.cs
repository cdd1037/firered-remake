using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class SummaryInteractionPresentationTests
{
    private static readonly ImmutableArray<PokemonMoveSlot> Moves = [
        new(new("pret:MOVE_TACKLE"), 12, 35), new(new("pret:MOVE_GROWL"), 7, 40),
        new(new("pret:MOVE_LEECH_SEED"), 4, 10), new(new("pret:MOVE_VINE_WHIP"), 2, 10)];

    [TestMethod]
    public void BattlePartyActionsKeepSummaryForEggsAndSingleMemberParty()
    {
        CollectionAssert.AreEqual(new[] { "SUMMARY", "CANCEL" }, SummaryInteractionPresentationAssets.BattlePartyActions(1, false, false));
        CollectionAssert.AreEqual(new[] { "SUMMARY", "CANCEL" }, SummaryInteractionPresentationAssets.BattlePartyActions(6, true, true));
        CollectionAssert.AreEqual(new[] { "SHIFT", "SUMMARY", "CANCEL" }, SummaryInteractionPresentationAssets.BattlePartyActions(6, false, false));
        CollectionAssert.AreEqual(new[] { "SEND OUT", "SUMMARY", "CANCEL" }, SummaryInteractionPresentationAssets.BattlePartyActions(6, false, true));
    }
    [TestMethod]
    public void ReadOnlyModeNeverExposesMoveSwappingEvenWithAnOldSwapCursor()
    {
        var selected = new SummaryMoveSelection(SummaryInteractionMode.ReadOnly, 1, 2);
        Assert.IsFalse(selected.CanReorder); Assert.IsFalse(selected.Swapping);
        Assert.AreEqual("", selected.Control); Assert.AreEqual(5, selected.RowCount(4));
        Assert.AreEqual(Moves[1].Move, selected.SelectedMove(Moves));
        Assert.IsNull((selected with { Cursor = 4 }).SelectedMove(Moves));
        Assert.IsTrue((selected with { Cursor = 4 }).IsCancel(4));
    }
    [TestMethod]
    public void ReplacementFifthRowInspectsNewMoveAndReturnsCancel()
    {
        var candidate = new MoveId("pret:MOVE_RAZOR_LEAF");
        var selected = new SummaryMoveSelection(SummaryInteractionMode.Replace, 4, NewMove: candidate);
        Assert.AreEqual(candidate, selected.SelectedMove(Moves));
        Assert.IsTrue(selected.IsCancel(4)); Assert.IsFalse(selected.CanReorder);
        Assert.AreEqual("PICK", selected.Control); Assert.AreEqual(5, selected.RowCount(4));
        Assert.AreEqual(Moves[0].Move, (selected with { Cursor = 0 }).SelectedMove(Moves));
        Assert.AreEqual(12, Moves[0].CurrentPp);
    }
    [TestMethod]
    public void ReorderModeKeepsTwoStageCancelAndAllSlotMetadata()
    {
        var selected = new SummaryMoveSelection(SummaryInteractionMode.Reorder, 1);
        Assert.AreEqual(5, selected.RowCount(4)); Assert.AreEqual("SWITCH", selected.Control);
        var swapping = selected with { SwapSource = 0 };
        Assert.IsTrue(swapping.Swapping); Assert.AreEqual(4, swapping.RowCount(4)); Assert.AreEqual("PLACE", swapping.Control);
        Assert.IsFalse((swapping with { Cursor = 4 }).IsCancel(4));
        Assert.AreEqual(Moves[1].Move, swapping.SelectedMove(Moves));
        Assert.AreEqual(7, Moves[1].CurrentPp); Assert.AreEqual(40, Moves[1].MaximumPp);
    }
    [TestMethod]
    public void ReminderScrollUsesCandidateIdentityAndFinalCancel()
    {
        ImmutableArray<MoveId> choices = [.. Enumerable.Range(0, 9).Select(i => new MoveId("candidate:" + i))];
        var selected = new SummaryMoveSelection(SummaryInteractionMode.SelectNew, 7, Candidates: choices, FirstRow: 3);
        Assert.AreEqual(choices[7], selected.SelectedMove(Moves)); Assert.AreEqual(10, selected.RowCount(4));
        Assert.IsFalse(selected.IsCancel(4)); Assert.IsFalse(selected.CanReorder);
        selected = selected with { Cursor = 9, FirstRow = 5 };
        Assert.IsTrue(selected.IsCancel(4)); Assert.IsNull(selected.SelectedMove(Moves));
    }
    [TestMethod]
    public void NativeMoveRowsHaveExactHoverAndClickBounds()
    {
        for (int i = 0; i < 5; i++)
        {
            Assert.AreEqual(i, SummaryInteractionPresentationAssets.MoveRowAt(120, 18 + i * 28, 5));
            Assert.AreEqual(i, SummaryInteractionPresentationAssets.MoveRowAt(239, 45 + i * 28, 5));
        }
        Assert.AreEqual(-1, SummaryInteractionPresentationAssets.MoveRowAt(119, 18, 5));
        Assert.AreEqual(-1, SummaryInteractionPresentationAssets.MoveRowAt(240, 18, 5));
        Assert.AreEqual(-1, SummaryInteractionPresentationAssets.MoveRowAt(150, 17, 5));
        Assert.AreEqual(-1, SummaryInteractionPresentationAssets.MoveRowAt(150, 158, 5));
        Assert.AreEqual(-1, SummaryInteractionPresentationAssets.MoveRowAt(150, 130, 4));
    }
    [TestMethod]
    public void SelectionStampRejectsDifferentModalRevisionOrPokemon()
    {
        var stamp = new SummarySelectionStamp("battle:13", 8, 33);
        Assert.IsTrue(stamp.Matches("battle:13", 8, 33));
        Assert.IsFalse(stamp.Matches("battle:14", 8, 33));
        Assert.IsFalse(stamp.Matches("battle:13", 9, 33));
        Assert.IsFalse(stamp.Matches("battle:13", 8, 34));
    }
    [TestMethod]
    public void PlayTimeOnlyCommitsPreserveCurrentBattleAndLearningDraftsButNeverReviveStaleOnes()
    {
        foreach (string context in new[] { "single:wild-battle:1:", "wild:wild-battle:1:pret:MOVE_RAZOR_LEAF", "machine:pret:MOVE_DIG" })
        {
            var stamp = new SummarySelectionStamp(context, 10, 23);
            var afterClock = stamp.AfterPlayTimeOnlyCommit(10, 11).AfterPlayTimeOnlyCommit(11, 12);
            Assert.IsTrue(afterClock.Matches(context, 12, 23));
            Assert.IsFalse(afterClock.Matches(context, 12, 24));
            Assert.IsFalse(afterClock.Matches(context + "changed", 12, 23));
            Assert.AreSame(stamp, stamp.AfterPlayTimeOnlyCommit(11, 12));
            Assert.IsFalse(stamp.AfterPlayTimeOnlyCommit(11, 12).Matches(context, 12, 23));
        }
    }
}
