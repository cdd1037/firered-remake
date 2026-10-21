using Game.Domain.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SourceTextInputTests
{
    private static bool Held(CampaignButtonInput buttons) =>
        (buttons.Held & (CampaignButton.A | CampaignButton.B)) != 0;

    [TestMethod]
    [DataRow(8)]
    [DataRow(4)]
    [DataRow(1)]
    public void TapIsOneEdgeAndReleaseRestoresConfiguredCadence(int speed)
    {
        var printer = new SourceTextProgress(); var input = new SourceTextInput();
        printer.Reset("ABCDE", speed);
        input.Advance(printer, 1, false);
        input.Press(); input.Advance(printer, 1, true);
        input.Advance(printer, 1, false);
        int before = printer.VisibleGlyphs;
        input.Advance(printer, 1, false);
        Assert.AreEqual(before + (speed == 1 ? 1 : 0), printer.VisibleGlyphs);
        Assert.AreEqual(speed > 1, printer.HasPrintBeenSpedUp);
    }

    [TestMethod]
    public void SubframeTapSurvivesReleaseButIsNotRepeatedAcrossBatch()
    {
        var printer = new SourceTextProgress(); var input = new SourceTextInput();
        printer.Reset("ABCD", 4); input.Advance(printer, 1, false);
        input.Press(); input.Advance(printer, 0, true);
        input.Advance(printer, 4, false);
        Assert.AreEqual("AB", printer.VisibleText);
        Assert.AreEqual(1, printer.DelayCounter);
        input.Advance(printer, 1, false);
        Assert.AreEqual("AB", printer.VisibleText);
        input.Advance(printer, 1, false);
        Assert.AreEqual("ABC", printer.VisibleText);
    }

    [TestMethod]
    public void NewPrinterDoesNotTreatAlreadyHeldOrFirstGlyphEdgeAsSpeedup()
    {
        foreach (bool firstFrameEdge in new[] { false, true })
        {
            var printer = new SourceTextProgress(); var input = new SourceTextInput();
            printer.Reset("ABC", 4);
            if (firstFrameEdge) input.Press();
            input.Advance(printer, 4, true);
            Assert.AreEqual("A", printer.VisibleText);
            Assert.IsFalse(printer.HasPrintBeenSpedUp);
        }
    }

    [TestMethod]
    public void PartialABReleaseAndMappedAOwnersKeepPhysicalHold()
    {
        var buttons = new CampaignButtonInput(); buttons.SetMode(SourceButtonMode.LEqualsA);
        var printer = new SourceTextProgress(); var input = new SourceTextInput();
        printer.Reset("ABCDEFG", 4); input.Advance(printer, 1, false);
        buttons.Observe(-1, 13, CampaignButton.A, true); input.Press();
        input.Advance(printer, 1, Held(buttons));
        buttons.Observe(-1, 8, CampaignButton.B, true); input.Press();
        buttons.Observe(-1, 13, CampaignButton.A, false);
        input.Advance(printer, 1, Held(buttons)); Assert.AreEqual("AB", printer.VisibleText);
        buttons.Observe(0, 9, CampaignButton.L, true); input.Press();
        buttons.Observe(-1, 8, CampaignButton.B, false);
        input.Advance(printer, 1, Held(buttons)); Assert.AreEqual("ABC", printer.VisibleText);
        Assert.AreEqual(CampaignButton.None, buttons.Observe(1, 0, CampaignButton.A, true).Pressed);
        buttons.Disconnect(0);
        input.Advance(printer, 1, Held(buttons)); Assert.AreEqual("ABCD", printer.VisibleText);
        buttons.Disconnect(1);
        input.Advance(printer, 1, Held(buttons)); Assert.AreEqual("ABCD", printer.VisibleText);
    }

    [TestMethod]
    public void VisualAndSourceContinuationsKeepLatchAndHeldWithoutReplayingAckEdge()
    {
        var buttons = new CampaignButtonInput(); var printer = new SourceTextProgress();
        var input = new SourceTextInput();
        printer.Reset("AB", 4); input.Advance(printer, 1, false);
        buttons.Observe(-1, 13, CampaignButton.A, true); input.Press();
        input.Advance(printer, 10, Held(buttons)); Assert.IsTrue(printer.Complete);
        foreach (string page in new[] { "visual", "source" })
        {
            printer.ContinuePage(page); input.Clear();
            input.Advance(printer, 1, Held(buttons));
            Assert.AreEqual(page[..1], printer.VisibleText);
            Assert.IsTrue(printer.HasPrintBeenSpedUp);
            input.Advance(printer, 20, Held(buttons));
        }
        printer.Reset("NEW", 4); input.Clear(); input.Advance(printer, 4, Held(buttons));
        Assert.AreEqual("N", printer.VisibleText); Assert.IsFalse(printer.HasPrintBeenSpedUp);
    }

    [TestMethod]
    public void ClearingPendingEdgeDoesNotManufactureSpeedupForHeldButton()
    {
        var printer = new SourceTextProgress(); var input = new SourceTextInput();
        printer.Reset("ABC", 4); input.Advance(printer, 1, false);
        input.Press(); input.Clear(); input.Advance(printer, 2, true);
        Assert.IsFalse(printer.HasPrintBeenSpedUp); Assert.AreEqual("A", printer.VisibleText);
    }

    // These routing assertions complement the executable input tests: a correct
    // printer alone did not catch the former field/Oak host-wiring regressions.
    [TestMethod]
    public void FieldAndOakConsumersUseSharedHeldSampleAndOneShotInput()
    {
        string Read(string name) => File.ReadAllText(Path.Combine(TestAssets.Root, "src/Game.Godot", name));
        string field = Read("UI/Text/CampaignFieldTextProgress.cs"), oak = Read("UI/Opening/CampaignOpeningView.cs");
        StringAssert.Contains(field, "_fieldTextInput.Advance(_fieldTextProgress, frames, TextSpeedButtonHeld)");
        StringAssert.Contains(field, "previous.Pages == dialogue.Pages");
        StringAssert.Contains(field, "dialogue.Page == previous.Page + 1");
        StringAssert.Contains(field, "if (continuation) _fieldTextProgress.ContinuePage");
        StringAssert.Contains(oak, "_openingTextInput.Advance(_openingPrinter, 1, TextSpeedButtonHeld)");
        StringAssert.Contains(oak, "ResetOpeningPrinter(_openingPages[_openingPage].Message == previousMessage)");
        StringAssert.Contains(Read("UI/Opening/CampaignPlayerEntryView.cs"), "if(key is Key.Enter or Key.Backspace && _entryActionFrame");
        StringAssert.Contains(Read("UI/Opening/CampaignPlayerEntryView.cs"), "if (_entryMode != \"oak-speech\") _entryActionFrame");
        Assert.IsFalse(oak.Contains("_openingSpeedUp", StringComparison.Ordinal));
        Assert.IsFalse(field.Contains("_fieldTextHeld", StringComparison.Ordinal));
    }
}
