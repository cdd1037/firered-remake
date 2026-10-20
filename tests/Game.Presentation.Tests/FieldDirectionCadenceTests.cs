using Game.Domain.World;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class FieldDirectionCadenceTests
{
    [TestMethod]
    [DataRow(false, 8)]
    [DataRow(true, 1)]
    public void SourceTurnFramesAreNotWalkFrames(bool bicycle, int frames)
    {
        Assert.AreEqual(frames, FieldDirectionCadence.TurnFrames(bicycle));
        var input = new FieldDirectionCadence();
        input.SetHeld(Direction.East, true);
        Assert.IsTrue(input.NeedsTurn(Direction.North));
        input.Accept(frames, false);
        input.Advance((frames - 1) / 60d);
        Assert.IsFalse(input.Ready);
        input.Advance(1 / 60d);
        Assert.IsTrue(input.Ready);
        Assert.IsFalse(input.NeedsTurn(Direction.East));
    }

    [TestMethod]
    [DataRow(false, 32)]
    [DataRow(true, 16)]
    public void SourceBumpsHaveTheirOwnCadence(bool bicycle, int frames) =>
        Assert.AreEqual(frames, FieldDirectionCadence.BumpFrames(bicycle));

    [TestMethod]
    public void MidStepDirectionChangesDoNotRestartTheActiveStep()
    {
        var input = new FieldDirectionCadence();
        input.SetHeld(Direction.East, true); input.Accept(16, true);
        input.Advance(10 / 60d); input.SetHeld(Direction.North, true);
        Assert.AreEqual(6d, input.RemainingFrames, 1e-7);
        input.Advance(6 / 60d);
        Assert.IsTrue(input.Ready);
        Assert.IsFalse(input.NeedsTurn(Direction.East), "continuous movement changes direction without an idle turn");
    }

    [TestMethod]
    public void ReleaseAndRepressRetainsTheActiveAction()
    {
        var input = new FieldDirectionCadence();
        input.SetHeld(Direction.East, true); input.Accept(16, true);
        input.Advance(3 / 60d); input.SetHeld(Direction.East, false);
        input.Advance(4 / 60d); input.SetHeld(Direction.North, true);
        Assert.AreEqual(9d, input.RemainingFrames, 1e-7);
        Assert.IsFalse(input.Ready); Assert.IsFalse(input.NeedsTurn(Direction.East));
    }

    [TestMethod]
    public void AllHeldDirectionsUseSourcePriorityAndReleasesRevealTheNext()
    {
        var input = new FieldDirectionCadence();
        foreach (var d in new[] { Direction.East, Direction.South, Direction.West, Direction.North }) input.SetHeld(d, true);
        foreach (var d in new[] { Direction.North, Direction.South, Direction.West, Direction.East })
        { Assert.AreEqual(d, input.Held); input.SetHeld(d, false); }
        Assert.IsNull(input.Held);
    }

    [TestMethod]
    public void ModalInputClearDoesNotCancelCooldownButTimeStillAdvances()
    {
        var input = new FieldDirectionCadence();
        input.SetHeld(Direction.East, true); input.Accept(32, true); input.ClearHeld();
        Assert.AreEqual(32d, input.RemainingFrames);
        input.Advance(1); input.SetHeld(Direction.North, true);
        Assert.IsTrue(input.Ready); Assert.IsTrue(input.NeedsTurn(Direction.East));
    }

    [TestMethod]
    public void IdleAfterACompletedStepRequiresATurnAgain()
    {
        var input = new FieldDirectionCadence();
        input.SetHeld(Direction.East, true); input.Accept(16, true);
        input.SetHeld(Direction.East, false); input.Advance(16 / 60d);
        input.SetHeld(Direction.North, true);
        Assert.IsTrue(input.NeedsTurn(Direction.East));
    }

    [TestMethod]
    public void MapResetClearsInputAndOldAction()
    {
        var input = new FieldDirectionCadence();
        input.SetHeld(Direction.East, true); input.Accept(16, true); input.Reset();
        Assert.IsTrue(input.Ready); Assert.IsNull(input.Held);
        input.SetHeld(Direction.North, true); Assert.IsTrue(input.NeedsTurn(Direction.East));
    }
    [TestMethod]
    public void FailedApplyClearsIntentWithoutInventingCooldown()
    {
        var input = new FieldDirectionCadence();
        input.SetHeld(Direction.North, true);
        input.Complete(false, 0, false);
        Assert.IsTrue(input.Ready); Assert.IsNull(input.Held); Assert.IsFalse(input.IdlePullFailed);
    }

    [TestMethod]
    public void FailedIdlePullWaitsForNewInputOrMapInsteadOfSpammingCommands()
    {
        var input = new FieldDirectionCadence();
        input.Complete(false, 0, false, idle: true); input.Advance(60);
        Assert.IsTrue(input.IdlePullFailed); Assert.IsTrue(input.Ready);
        input.SetHeld(Direction.North, true); Assert.IsFalse(input.IdlePullFailed);
        input.Complete(false, 0, false, idle: true); input.ResetIdlePullFailure();
        Assert.IsFalse(input.IdlePullFailed);
        input.Complete(false, 0, false, idle: true); input.Reset();
        Assert.IsFalse(input.IdlePullFailed);
    }

    [TestMethod]
    [DataRow(false, Direction.North, Direction.East, false, CyclingRoadAction.Move, 16)]
    [DataRow(true, Direction.North, Direction.East, false, CyclingRoadAction.Turn, 1)]
    [DataRow(false, Direction.South, Direction.South, false, CyclingRoadAction.Move, 4)]
    public void CyclingRoadUsesResolvedActionInsteadOfRequestedTurn(bool slopeState, Direction facing,
        Direction direction, bool brake, CyclingRoadAction expected, int frames)
    {
        var action = CyclingRoadRules.Resolve(slopeState, facing, direction, brake);
        Assert.AreEqual(expected, action.Action);
        var input = new FieldDirectionCadence(); input.SetHeld(direction, true);
        input.Complete(true, FieldDirectionCadence.ActionFrames(true, expected == CyclingRoadAction.Move,
            true, action.Ticks, action.Action), action.Action == CyclingRoadAction.Move);
        Assert.AreEqual((double)frames, input.RemainingFrames);
        // Source slope turns use one frame; blocked slope movement has no bump animation.
        Assert.AreEqual(1, FieldDirectionCadence.ActionFrames(true, false, true, 16, action.Action));
    }

    [TestMethod]
    public void SameFrameEdgesOnlyResolveIntentBeforeOneFieldTick()
    {
        var input = new FieldDirectionCadence();
        input.SetHeld(Direction.South, true); input.SetHeld(Direction.North, true);
        Assert.AreEqual(Direction.North, input.Held); Assert.IsTrue(input.Ready);
        var view = File.ReadAllText(Path.Combine(TestAssets.Root, "src/Game.Godot/CampaignFieldDirectionView.cs"));
        var start = view.IndexOf("private void FieldDirectionInput", StringComparison.Ordinal);
        var end = view.IndexOf("private void ProcessFieldDirection", start, StringComparison.Ordinal);
        Assert.IsFalse(view[start..end].Contains("ProcessFieldDirection();", StringComparison.Ordinal));
    }
}
