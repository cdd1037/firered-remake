namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SummaryPageTransitionTests
{
    private static SummaryPageKey Page(string owner = "field", long instance = 7, int page = 0, bool details = false)
        => new(owner, instance, page, details);

    private static void Draw(SummaryPageTransition view, SummaryPageKey page, double now)
    {
        view.BeginFrame();
        view.Observe(page, now);
        view.Present();
    }

    [TestMethod]
    [DataRow(119)]
    [DataRow(120)]
    [DataRow(500)]
    [DataRow(5000)]
    public void RenderStallDoesNotRestartEntryFadeOrPortrait(int stall)
    {
        var view = new SummaryPageTransition();
        Draw(view, Page(), 100);
        Draw(view, Page(), 500);
        Assert.AreEqual(0f, view.EntryAlpha(500));
        Draw(view, Page(), 500 + (ulong)stall);
        Assert.AreEqual(100d, view.EntryStart);
        Assert.AreEqual(100d, view.PortraitStart);
        Assert.AreEqual(0f, view.EntryAlpha(500 + (ulong)stall));
        Assert.AreEqual((int)((400UL + (ulong)stall) * 60 / 1000), view.PortraitFrame(500 + (ulong)stall));
        Assert.IsNull(view.Outgoing);
    }

    [TestMethod]
    public void OneDifferentScreenDrawEndsTheVisitEvenWhenReopenedQuickly()
    {
        var view = new SummaryPageTransition();
        Draw(view, Page(), 100);
        Draw(view, Page(page: 1), 400);
        Assert.IsTrue(view.IsSliding(400));
        view.BeginFrame(); // A party page is drawn, not Summary.
        Draw(view, Page(), 417);
        Assert.AreEqual(417d, view.EntryStart);
        Assert.AreEqual(417d, view.PortraitStart);
        Assert.AreEqual(1f, view.EntryAlpha(417));
        Assert.IsNull(view.Outgoing);
    }

    [TestMethod]
    [DataRow("field")]
    [DataRow("pc")]
    [DataRow("battle:single:42")]
    [DataRow("daycare")]
    [DataRow("learning:machine:pret:MOVE_TACKLE")]
    public void EverySummaryOwnerUsesOneVisitAndFourSourcePixelSteps(string owner)
    {
        var view = new SummaryPageTransition();
        var first = Page(owner: owner);
        Draw(view, first, 100);
        Draw(view, first with { Page = 1 }, 500);
        Assert.AreEqual(first, view.Outgoing);
        Assert.AreEqual(1, view.Direction);
        foreach (var (elapsed, offset, locked) in new[] { (0UL, 60, true), (17UL, 120, true), (34UL, 180, true), (50UL, 240, false) })
        {
            // No draw is required to finish the animation or unlock the next edge.
            Assert.AreEqual(offset, view.Offset(500 + elapsed));
            Assert.AreEqual(locked, view.IsSliding(500 + elapsed));
        }
        Assert.AreEqual(100d, view.EntryStart);
        Assert.AreEqual(100d, view.PortraitStart);
    }

    [TestMethod]
    public void ProcessAndInputObservationDoNotRestartAnInFlightPage()
    {
        var view = new SummaryPageTransition();
        Draw(view, Page(), 100);
        var right = Page(page: 1);
        view.Observe(right, 500); // The accepted input is seen before drawing.
        for (double now = 500; now < 550; now++)
        {
            view.Observe(right, now);
            Assert.AreEqual(500d, view.SlideStart);
            Assert.IsTrue(view.IsSliding(now), "Repeated/held edges remain inhibited only while the slide is moving.");
        }
        Assert.IsFalse(view.IsSliding(550), "The first edge at completion cannot wait for a redraw.");
        view.Observe(Page(), 550);
        Assert.AreEqual(-1, view.Direction);
        Assert.AreEqual(right, view.Outgoing);
        Assert.AreEqual(550d, view.SlideStart);
    }

    [TestMethod]
    public void EntryPaletteFadeInhibitsInputBeforeFirstDrawWithoutAddingADrawDelay()
    {
        var view = new SummaryPageTransition();
        view.Observe(Page(), 100);
        Assert.IsTrue(view.IsEntering(100));
        Assert.IsTrue(view.IsEntering(366));
        Assert.IsFalse(view.IsEntering(367));
        Assert.IsNull(view.Outgoing);
        view.Observe(Page(page: 1), 367);
        Assert.IsTrue(view.IsSliding(367));
        view.Reset();
        Assert.IsFalse(view.IsEntering(0));
    }

    [TestMethod]
    public void DifferentPokemonDiscardsOldSlideWithoutRestartingWholeScreenFade()
    {
        var view = new SummaryPageTransition();
        Draw(view, Page(), 100);
        Draw(view, Page(page: 1), 500);
        Draw(view, Page(instance: 8, page: 1), 517);
        Assert.AreEqual(100d, view.EntryStart);
        Assert.AreEqual(517d, view.PortraitStart);
        Assert.IsNull(view.Outgoing);
        Assert.IsFalse(view.IsSliding(517));
    }

    [TestMethod]
    public void ContextReplacementCannotReuseAnOutgoingPageEvenForSamePokemon()
    {
        var view = new SummaryPageTransition();
        Draw(view, Page(), 100);
        Draw(view, Page(page: 1), 500);
        Draw(view, Page(owner: "pc", page: 1), 517);
        Assert.AreEqual(517d, view.EntryStart);
        Assert.AreEqual(517d, view.PortraitStart);
        Assert.IsNull(view.Outgoing);
    }

    [TestMethod]
    public void MoveDetailsEnterAndBackRetainOppositeSourceSlideDirections()
    {
        var view = new SummaryPageTransition();
        Draw(view, Page(page: 2), 100);
        Draw(view, Page(page: 2, details: true), 500);
        Assert.AreEqual(1, view.Direction);
        Assert.AreEqual(Page(page: 2), view.Outgoing);
        Draw(view, Page(page: 2), 550);
        Assert.AreEqual(-1, view.Direction);
        Assert.AreEqual(Page(page: 2, details: true), view.Outgoing);
    }

    [TestMethod]
    public void HelpSuspendsFramesAndClockWhileExitResetStartsFreshVisit()
    {
        var view = new SummaryPageTransition();
        Draw(view, Page(), 100);
        Draw(view, Page(page: 1), 500);
        // Help deliberately invokes neither BeginFrame nor the Summary process clock.
        Assert.AreEqual(60, view.Offset(500));
        Draw(view, Page(page: 1), 500);
        Assert.AreEqual(100d, view.EntryStart);
        Assert.AreEqual(60, view.Offset(500));
        view.Reset();
        Assert.IsNull(view.Current);
        Assert.IsNull(view.Outgoing);
        Draw(view, Page(), 0);
        Assert.AreEqual(1f, view.EntryAlpha(0));
        Assert.IsFalse(view.IsSliding(0));
    }

    [TestMethod]
    [DataRow(0d)]
    [DataRow(0.314159d)]
    [DataRow(123.456789d)]
    [DataRow(12345.67891d)]
    public void RealSixtiethSecondTicksVisitEverySlideStepFromFractionalStart(double seconds)
    {
        var view = new SummaryPageTransition();
        Draw(view, Page(), seconds * 1000);
        seconds += .5;
        Draw(view, Page(page: 1), seconds * 1000);
        for (int frame = 0; frame <= 4; frame++)
        {
            view.Observe(Page(page: 1), seconds * 1000);
            Assert.AreEqual(Math.Min(frame + 1, 4) * 60, view.Offset(seconds * 1000), $"Source frame {frame}");
            Assert.AreEqual(frame < 3, view.IsSliding(seconds * 1000));
            seconds += 1d / 60;
        }
    }

    [TestMethod]
    [DataRow(0d)]
    [DataRow(0.314159d)]
    [DataRow(123.456789d)]
    public void EntryExitAndPortraitShareExactSourceFramesWithoutPrematureSixteenMsAdvance(double seconds)
    {
        var view = new SummaryPageTransition();
        double start = seconds * 1000;
        Draw(view, Page(), start);
        Assert.AreEqual(0, SummaryPageTransition.ElapsedFrames(start + 16, start));
        Assert.AreEqual(0, view.PortraitFrame(start + 16));
        Assert.IsTrue(view.IsEntering(start + 266));
        for (int frame = 0; frame <= 16; frame++)
        {
            double now = seconds * 1000;
            Assert.AreEqual(frame, SummaryPageTransition.ElapsedFrames(now, start), "Also drives exit completion.");
            Assert.AreEqual(frame, view.PortraitFrame(now));
            Assert.AreEqual(frame < 16, view.IsEntering(now));
            Assert.AreEqual(Math.Max(0, 1 - frame / 16f), view.EntryAlpha(now), 0.00001f);
            seconds += 1d / 60;
        }
    }

    [TestMethod]
    [DataRow(16d, 0)]
    [DataRow(32d, 1)]
    [DataRow(48d, 2)]
    [DataRow(64d, 3)]
    public void RealMillisecondIntervalsAreFlooredRatherThanRoundedToSourceFrames(double elapsed, int frames)
    {
        const double start = 314.159;
        Assert.AreEqual(frames, SummaryPageTransition.ElapsedFrames(start + elapsed, start));
    }
}
