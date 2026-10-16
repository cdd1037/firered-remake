using FireRed.Tests;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class InventoryPresentationTests
{
    private static readonly int[] Shift = [0,3,6,8,11,13,14,15,16,15,14,13,11,8,6,3,0];
    private static void Step(StorageHandPresentation hand, int frames) { for(int i=0;i<frames;i++) hand.AdvanceFrame(); }

    [TestMethod]
    [DataRow(0,40,47)][DataRow(49,28,53)][DataRow(50,41,46)][DataRow(57,40,46)][DataRow(-1,27,54)]
    public void DiscOrderMatchesSourceTmAndHmPositions(int index,int x,int y)
        => Assert.AreEqual((x,y),MachineDiscPresentation.Position(index));

    [TestMethod]
    public void DiscLowersBeforeChangingTypeThenRaisesAndHidesForClose()
    {
        var disc=new MachineDiscPresentation();disc.Select(0);
        Assert.AreEqual(0,disc.OffsetY);Assert.IsFalse(disc.Moving);
        disc.Select(50);disc.AdvanceFrame();Assert.AreEqual(10,disc.OffsetY);Assert.AreEqual(0,disc.Displayed);
        disc.AdvanceFrame();Assert.AreEqual(20,disc.OffsetY);Assert.AreEqual(0,disc.Displayed);
        disc.AdvanceFrame();Assert.AreEqual(50,disc.Displayed);Assert.AreEqual(20,disc.OffsetY);
        disc.AdvanceFrame();Assert.AreEqual(10,disc.OffsetY);disc.AdvanceFrame();Assert.AreEqual(0,disc.OffsetY);
        disc.AdvanceFrame();Assert.IsFalse(disc.Moving);
        disc.Select(-1);for(int n=0;n<3;n++)disc.AdvanceFrame();Assert.AreEqual(-1,disc.Displayed);Assert.IsFalse(disc.Moving);
    }

    [TestMethod]
    public void RapidDiscReselectionAndScreenResetNeverExposeWrongDisc()
    {
        var disc=new MachineDiscPresentation();disc.Select(0);disc.Select(10);disc.AdvanceFrame();disc.Select(50);
        disc.AdvanceFrame();Assert.AreEqual(0,disc.Displayed);disc.AdvanceFrame();Assert.AreEqual(50,disc.Displayed);
        disc.Select(-1);disc.AdvanceFrame();Assert.AreEqual(-1,disc.Displayed);
        disc.Reset();disc.Select(49);Assert.AreEqual(49,disc.Displayed);Assert.AreEqual(0,disc.OffsetY);Assert.IsFalse(disc.Moving);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>disc.Select(58));
    }

    [TestMethod]
    public void GrabKeepsIconOnSlotUntilOpenHandContactsThenRaisesFistAndIcon()
    {
        var hand=new StorageHandPresentation();hand.Begin(StorageHandAction.Grab);
        Assert.AreEqual(2,hand.Pose(Shift).HandFrame);Step(hand,9);
        Assert.AreEqual(8,hand.Pose(Shift).HandY);Assert.AreEqual(0,hand.Pose(Shift).MovingY);Assert.IsFalse(hand.Pose(Shift).AtContact);
        hand.AdvanceFrame();Assert.IsTrue(hand.Pose(Shift).AtContact);Assert.AreEqual(3,hand.Pose(Shift).HandFrame);
        Step(hand,8);Assert.AreEqual(0,hand.Pose(Shift).HandY);Assert.AreEqual(-8,hand.Pose(Shift).MovingY);
        Step(hand,2);Assert.IsFalse(hand.Active);
    }

    [TestMethod]
    [DataRow(2)][DataRow(5)]
    public void PlaceLowersHeldIconOpensHandThenLeavesIconOnDestination(int value)
    {
        var action=(StorageHandAction)value;
        var hand=new StorageHandPresentation();hand.Begin(action);
        Assert.AreEqual(-8,hand.Pose(Shift).MovingY);Assert.AreEqual(3,hand.Pose(Shift).HandFrame);
        Step(hand,9);Assert.AreEqual(2,hand.Pose(Shift).HandFrame);Assert.AreEqual(0,hand.Pose(Shift).MovingY);
        Step(hand,8);Assert.AreEqual(0,hand.Pose(Shift).HandY);Assert.AreEqual(0,hand.Pose(Shift).MovingY);
        hand.AdvanceFrame();Assert.AreEqual(0,hand.Pose(Shift).HandFrame);
        hand.AdvanceFrame();Assert.IsFalse(hand.Active);
    }

    [TestMethod]
    public void ShiftUsesSourceSixteenStepArcAndReversesDrawOrderHalfway()
    {
        var hand=new StorageHandPresentation();hand.Begin(StorageHandAction.Shift);
        for(int step=0;step<=16;step++)
        {
            var pose=hand.Pose(Shift);
            Assert.AreEqual(-Shift[step],pose.MovingX);Assert.AreEqual(Shift[step],pose.TargetX);
            Assert.AreEqual(-8+(step+1)/2,pose.MovingY);Assert.AreEqual(-(step+1)/2,pose.TargetY);
            Assert.AreEqual(step>=8,pose.TargetOnTop);hand.AdvanceFrame();
        }
        Assert.AreEqual(3,hand.Pose(Shift).HandFrame);hand.AdvanceFrame();Assert.IsFalse(hand.Active);
    }

    [TestMethod]
    public void ResetAtEveryFrameClearsActionAndAllowsDifferentNextScreenFlow()
    {
        foreach(var action in Enum.GetValues<StorageHandAction>().Where(a=>a!=StorageHandAction.None))
        for(int frame=0;frame<20;frame++)
        {
            var hand=new StorageHandPresentation();hand.Begin(action);Step(hand,frame);hand.Reset();
            Assert.IsFalse(hand.Active);Assert.AreEqual(0,hand.Frame);
            hand.AdvanceFrame();Assert.IsFalse(hand.Active);
            hand.Begin(StorageHandAction.Place);Assert.AreEqual(-8,hand.Pose(Shift).MovingY);
        }
    }

    [TestMethod]
    public void FiveRowSharedHitAreaIncludesCloseForEmptyAndScrollingLists()
    {
        Assert.AreEqual((80,10,152,16),(MachineCaseLayout.ListX,MachineCaseLayout.ListY,MachineCaseLayout.ListWidth,MachineCaseLayout.RowHeight));
        foreach(int count in new[]{0,1,4,5,57})
        for(int cursor=0;cursor<=count;cursor++)
        {
            int first=MachineCaseLayout.KeepCursorVisible(cursor,0,count),visible=MachineCaseLayout.VisibleRows(count,first);
            Assert.IsTrue(visible is >=1 and <=5);Assert.IsTrue(cursor>=first&&cursor<first+visible);
            Assert.IsTrue(MachineCaseLayout.ListY+(visible-1)*16<88);
        }
    }

    [TestMethod]
    public void ScrolledHoverAndClickKeepTheSameVisibleMachineAndPopupOrigin()
    {
        const int count = 9;
        int first = 0;
        for (int cursor = 0; cursor <= count; cursor++)
            first = MachineCaseLayout.KeepCursorVisible(cursor, first, count);
        Assert.AreEqual(5, first); // TM10, TM15, TM17, TM39, CLOSE.
        // Native report: viewport origin (110,86), scale 4, click (661,349).
        double y = (349 - 86) / 4d;
        int row = (int)((y - MachineCaseLayout.ListY) / MachineCaseLayout.RowHeight);
        Assert.AreEqual(3, row);
        int hovered = first + row;
        Assert.AreEqual(8, hovered); // TM39.
        for (int eventIndex = 0; eventIndex < 4; eventIndex++)
        {
            first = MachineCaseLayout.KeepCursorVisible(hovered, first, count);
            Assert.AreEqual(5, first); // Repeated hover, redraw, click, action popup.
            Assert.AreEqual(hovered, first + row);
        }
        Assert.AreEqual(5, MachineCaseLayout.KeepCursorVisible(5, first, count));
        Assert.AreEqual(4, MachineCaseLayout.KeepCursorVisible(4, first, count));
        Assert.AreEqual(0, MachineCaseLayout.KeepCursorVisible(0, first, 0));
        Assert.AreEqual(4, MachineCaseLayout.KeepCursorVisible(8, first, 8)); // Consumed final stack.
        Assert.AreEqual(0, MachineCaseLayout.KeepCursorVisible(0, 0, count)); // Reopen.
    }

    [TestMethod]
    public void GeneratedInventoryPackageHasBothGendersAllDiscTypesAndSourceShiftSamples()
    {
        var assets=InventoryPresentationAssets.Load(TestAssets.Root);
        CollectionAssert.AreEqual(Shift,assets.ShiftOffsets);
        Assert.AreEqual(17,assets.DiscTypes.Length);Assert.AreEqual(32,assets.ArrowOffsets.Length);
        foreach(string id in new[]{"case-male","case-female","case-male-front","case-female-front","hand-single","hand-multi","hand-shadow","discs","hm","number","move-labels","scroll-up","scroll-down"})
            Assert.IsTrue(assets.Assets.Any(a=>a.Id==id));
    }
}
