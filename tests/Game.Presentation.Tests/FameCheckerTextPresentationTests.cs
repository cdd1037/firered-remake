using Game.Content;
using Game.Domain.Events;

namespace FireRed.Tests.Presentation;

[TestClass]
public sealed class FameCheckerTextPresentationTests
{
    private static System.Collections.Immutable.ImmutableArray<FameCheckerTextPage> Pages(string text) =>
        FameCheckerTextPages.Parse(new FameCheckerText("test", "test", 1, text, []), "RED", "BLUE");

    [TestMethod]
    public void ParserPreservesZeroWidthFormattingAndLiteralExpandedNames()
    {
        var pages = Pages("{COLOR BLUE}{SHADOW LIGHT_BLUE}A\\nB\\l{FONT_FEMALE}{PLAYER}\\pC$");
        CollectionAssert.AreEqual(new[] { 0, 0 }, pages[0].RepeatControlOffsets.ToArray());
        CollectionAssert.AreEqual(new[] { 0 }, pages[1].RepeatControlOffsets.ToArray());
        Assert.AreEqual("B\nRED", pages[1].Text);
        Assert.AreEqual(2, pages[1].RetainedTextLength);
        Assert.AreEqual(FameCheckerTextFont.Female, pages[1].Lines[1].Runs[0].Style.Font);
        Assert.AreEqual(FameCheckerTextColor.Blue, pages[2].Lines[0].Runs[0].Style.Foreground);
    }

    [TestMethod]
    public void ImmediateMenuDescriptionDoesNotDelayCancelInput()
    {
        var p = new FameCheckerTextPresentation();
        p.Reset(Pages("Closed.$"),8,immediate:true);
        Assert.IsTrue(p.Complete);
        Assert.AreEqual(7,p.VisibleLength);
    }

    [TestMethod]
    public void FormattingUsesSourceRenderRepeatCadenceInsteadOfSilentRemoval()
    {
        // text.c RenderText: each FC command sets delay=3 then RENDER_REPEAT consumes one delay
        // in the same RenderFont call. Literal independent source trace: A at 7, EOS at 11.
        var p = new FameCheckerTextPresentation();
        p.Reset(Pages("{COLOR BLUE}{SHADOW LIGHT_BLUE}A$"), 4);
        for (int frame = 1; frame <= 11; frame++)
        {
            p.AdvanceFrame(false, false);
            Assert.AreEqual(frame < 7 ? 0 : 1, p.VisibleLength, $"frame {frame}");
            Assert.AreEqual(frame == 11, p.Complete, $"EOS frame {frame}");
        }
    }

    [TestMethod]
    [DataRow(8, 1, 15)]
    [DataRow(4, 2, 8)]
    [DataRow(1, 4, 4)]
    public void ScrollKeepsOldWindowUntilSourcePixelsAndTransitionFrameFinish(int speed, int step, int ticks)
    {
        var p = new FameCheckerTextPresentation();
        p.Reset(Pages("A\\nB\\lC$"), speed);
        for (int guard = 0; !p.WaitingForPrompt && guard < 100; guard++) p.AdvanceFrame(false, false);
        Assert.IsTrue(p.WaitingForPrompt);
        Assert.IsFalse(p.AdvanceFrame(true, false)); // Holding never acknowledges a new prompt.
        Assert.IsTrue(p.AdvanceFrame(true, true));
        Assert.AreEqual(0, p.ScrollPixels);
        for (int tick = 1; tick <= ticks; tick++)
        {
            p.AdvanceFrame(false, false);
            Assert.AreEqual(Math.Min(15, tick * step), p.ScrollPixels);
            Assert.AreEqual(0, p.PageIndex);
        }
        p.AdvanceFrame(false, false);
        Assert.AreEqual(1, p.PageIndex);
        Assert.AreEqual(2, p.VisibleLength); // B + LF, not a new C glyph yet.
        Assert.IsFalse(p.IsScrolling);
        for (int i = 0; i < speed - 1; i++)
        {
            p.AdvanceFrame(false, false);
            Assert.AreEqual(2, p.VisibleLength);
        }
        p.AdvanceFrame(false, false);
        Assert.AreEqual(3, p.VisibleLength);
    }

    [TestMethod]
    public void ClearPreservesSourceDelayAndSpeedUpLatchAcrossPrompt()
    {
        var p = new FameCheckerTextPresentation();
        p.Reset(Pages("AB\\pCD$"), 4);
        p.AdvanceFrame(false, false);
        p.AdvanceFrame(true, true); // Delay branch latches speed-up, emits nothing.
        p.AdvanceFrame(true, false);
        p.AdvanceFrame(true, false);
        Assert.IsTrue(p.WaitingForPrompt);
        Assert.IsTrue(p.Printer.HasPrintBeenSpedUp);
        Assert.IsTrue(p.AdvanceFrame(true, true));
        Assert.AreEqual(1, p.PageIndex);
        Assert.AreEqual(0, p.VisibleLength);
        Assert.IsTrue(p.Printer.HasPrintBeenSpedUp);
        p.AdvanceFrame(true, false);
        Assert.AreEqual(1, p.VisibleLength);
        p.Reset(Pages("X$"), 4);
        Assert.IsFalse(p.Printer.HasPrintBeenSpedUp); // A new fact starts a new printer.
    }

    [TestMethod]
    public void AllImportedEnglishFactAndPickBodiesUseTheSameBoundedDecoder()
    {
        var catalog = PretFameChecker.Load(TestAssets.Root).Catalog;
        foreach (var person in catalog.Persons)
        foreach (var text in person.Facts.Select(f => f.Text).Concat(new[] { person.NameText, person.CompletionText }))
        {
            var pages = FameCheckerTextPages.Parse(text, "RED", "BLUE");
            Assert.IsFalse(pages.IsEmpty);
            var p = new FameCheckerTextPresentation(); p.Reset(pages, 1);
            int prompts = 0, guard = 0;
            while (!p.Complete && guard++ < 20000)
                if (p.AdvanceFrame(false, p.WaitingForPrompt)) prompts++;
            Assert.IsTrue(p.Complete, text.Symbol);
            Assert.AreEqual(pages.Length - 1, prompts, text.Symbol);
        }
    }
}
