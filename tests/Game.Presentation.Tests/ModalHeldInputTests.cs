using System.Collections.Immutable;
using Game.Domain.Tutorials;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class ModalHeldInputTests
{
    [TestMethod]
    public void LosingFocusStopsTeachyMenuRepeatWithoutChangingSelection()
    {
        var entries = Enumerable.Range(0, 7).Select(i => new TeachyTvMenuEntry("row" + i, i)).ToImmutableArray();
        var catalog = new TeachyTvCatalog([], entries, entries, ImmutableDictionary<string, TeachyTvText>.Empty, "hello");
        var state = TeachyTvPlaybackRules.Open("focus-test", TeachyTvOrigin.Bag, true, true);
        var view = new TeachyTvPresentation(); view.Synchronize(catalog, state, 4); view.AdvanceFrames(16);
        view.Input(TeachyTvButton.Down, true);
        Assert.IsTrue(view.TryTakeRequest(out var request));
        Assert.AreEqual(TeachyTvPresentationAction.Navigate, request!.Action);
        state = TeachyTvPlaybackRules.Navigate(catalog, state, request.Value).State;
        view.Synchronize(catalog, state, 4);
        view.ReleaseInput(); view.AdvanceFrames(60);
        Assert.IsFalse(view.TryTakeRequest(out _));
        Assert.AreEqual(1, view.State!.ScrollOffset + view.State.SelectedRow);
        view.Input(TeachyTvButton.Down, true);
        Assert.IsTrue(view.TryTakeRequest(out request)); Assert.AreEqual(2, request!.Value);
    }

    [TestMethod]
    public void LosingFocusClearsHeldAndQueuedTextPressWithoutRestartingText()
    {
        var released = new TeachyTvTextPresentation(); var control = new TeachyTvTextPresentation();
        const string text = "This is a long line of tutorial narration.$";
        released.Begin("test", text, 4); control.Begin("test", text, 4);
        for (int i = 0; i < 3; i++) { released.AdvanceFrame(); control.AdvanceFrame(); }
        int before = released.VisibleLength;
        released.Confirm(true); released.ReleaseInput();
        Assert.AreEqual(before, released.VisibleLength);
        for (int i = 0; i < 12; i++) { released.AdvanceFrame(); control.AdvanceFrame(); }
        Assert.AreEqual(control.VisibleLength, released.VisibleLength);
        Assert.IsFalse(released.Complete);
    }
}
