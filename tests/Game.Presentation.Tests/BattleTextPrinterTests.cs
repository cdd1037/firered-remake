using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class BattleTextPrinterTests
{
    [TestMethod]
    public void battle_pages_start_hidden_and_use_configured_source_cadence()
    {
        foreach (int speed in new[] { 8, 4, 1 })
        {
            var printer = new SourceBattleTextPrinter();
            printer.Reset("AB", speed);
            Assert.AreEqual("", printer.VisibleText);
            Assert.IsFalse(printer.Complete);
            for (int frame = 1; frame <= speed * 2 + 1; frame++)
            {
                printer.Advance(1d / 60, false);
                Assert.AreEqual(frame <= speed ? "A" : "AB", printer.VisibleText);
                Assert.AreEqual(frame == speed * 2 + 1, printer.Complete);
            }
        }
    }

    [TestMethod]
    public void speed_press_is_consumed_once_and_release_restores_cadence()
    {
        var printer = new SourceBattleTextPrinter();
        printer.Reset("ABCDEFGHIJK", 8);
        printer.Advance(1d / 60, false);
        Assert.IsTrue(printer.Press());
        printer.Advance(1d / 120, true);
        Assert.AreEqual("A", printer.VisibleText);
        printer.Advance(1d / 120, true); // First held frame observes one-shot press.
        Assert.AreEqual("A", printer.VisibleText);
        printer.Advance(2d / 60, true);
        Assert.AreEqual("ABC", printer.VisibleText);
        printer.Advance(7d / 60, false);
        Assert.AreEqual("ABC", printer.VisibleText);
        printer.Advance(1d / 60, false);
        Assert.AreEqual("ABCD", printer.VisibleText);
    }

    [TestMethod]
    public void visual_continuation_requires_complete_and_never_acknowledges_semantic_pages()
    {
        var printer = new SourceBattleTextPrinter();
        printer.Reset("一页", 4);
        Assert.ThrowsExactly<InvalidOperationException>(() => printer.ContinuePage("次页"));
        printer.Advance(9d / 60, false);
        Assert.IsTrue(printer.Complete);
        Assert.IsFalse(printer.Press()); // Owner may apply its existing animation/time gate.
        printer.ContinuePage("次页");
        Assert.IsFalse(printer.Complete);
        Assert.AreEqual(0, printer.VisibleCodepoints);
        Assert.IsTrue(printer.Press());
        printer.Advance(1d / 60, false);
        Assert.AreEqual("", printer.VisibleText); // Retains source delay across prompt.
        printer.Reset("新事件", 1);
        Assert.AreEqual("", printer.VisibleText);
        printer.Advance(1d / 60, false);
        Assert.AreEqual("新", printer.VisibleText);
    }

    [TestMethod]
    public void focus_loss_clears_pending_press_and_finished_page_does_not_bank_time()
    {
        var printer = new SourceBattleTextPrinter();
        printer.Reset("AB", 8);
        printer.Advance(1d / 60, false);
        printer.Press();
        printer.ReleaseInput();
        printer.Advance(7d / 60, false);
        Assert.AreEqual("A", printer.VisibleText);
        printer.Advance(9d / 60, false);
        Assert.IsTrue(printer.Complete);
        printer.Advance(100, true);
        printer.ContinuePage("CD");
        printer.Advance(1d / 60, false);
        Assert.AreEqual("", printer.VisibleText);
        Assert.IsFalse(printer.Complete);
    }

    [TestMethod]
    public void same_source_page_continuation_preserves_held_speed_but_new_message_resets_it()
    {
        var printer = new SourceBattleTextPrinter();
        printer.Reset("AB", 8);
        printer.Advance(1d / 60, false);
        printer.Press();
        printer.Advance(3d / 60, true);
        Assert.IsTrue(printer.Complete);
        printer.ContinuePage("CD");
        printer.Advance(1d / 60, true);
        Assert.AreEqual("C", printer.VisibleText);
        printer.Advance(1d / 60, true);
        Assert.AreEqual("CD", printer.VisibleText);
        printer.Reset("EF", 8); // A different source string starts a new printer.
        printer.Advance(2d / 60, true);
        Assert.AreEqual("E", printer.VisibleText);
    }

    // Wiring guards supplement the executable clock tests: these retained legacy
    // consumers must not acquire an unprocessed native printer and deadlock ACK.
    [TestMethod]
    public void legacy_teachy_and_stats_consumers_bypass_native_printer_waits()
    {
        string source = File.ReadAllText(Path.Combine(TestAssets.Root, "src/Game.Godot/CampaignBattleDialogText.cs"));
        string pagination = File.ReadAllText(Path.Combine(TestAssets.Root, "src/Game.Godot/CampaignMessageLayoutView.cs"));
        string view = File.ReadAllText(Path.Combine(TestAssets.Root, "src/Game.Godot/CampaignView.cs"));
        Assert.IsTrue(source.Contains("NativeWildMessageEnabled => TeachyBattle is null", StringComparison.Ordinal),
            "Teachy TV auto-ACK runs before ordinary battle processing and must keep its legacy event renderer.");
        string tutorial = pagination.Split("private bool TryAdvanceBattleVisualMessage()", StringSplitOptions.None)[1]
            .Split("private bool TryAdvanceWildVisualMessage()", StringSplitOptions.None)[0];
        string wild = pagination.Split("private bool TryAdvanceWildVisualMessage()", StringSplitOptions.None)[1]
            .Split("private bool TryAdvanceDoubleVisualMessage()", StringSplitOptions.None)[0];
        Assert.IsTrue(tutorial.Contains("Kind == \"stats\"", StringComparison.Ordinal));
        Assert.IsTrue(wild.Contains("WildStatsPage(battle)", StringComparison.Ordinal));
        Assert.IsTrue(source.Contains("_doubleRewardStats.ContainsKey(_doublePage)", StringComparison.Ordinal));
        Assert.IsTrue(view.Contains("ProcessBattleText(delta)", StringComparison.Ordinal));
        Assert.IsTrue(view.Contains("if(BattleTextInput(key))", StringComparison.Ordinal));
        Assert.IsTrue(view.IndexOf("ProcessBattleText(delta)", StringComparison.Ordinal) <
            view.IndexOf("ProcessDoubleBattle(delta)", StringComparison.Ordinal));
        Assert.IsTrue(view.IndexOf("if(BattleTextInput(key))", StringComparison.Ordinal) <
            view.IndexOf("&&DoubleBattleInput(key)", StringComparison.Ordinal));
        Assert.IsTrue(source.Contains("continuations.Contains(identity.Page)", StringComparison.Ordinal));
        Assert.IsTrue(source.Contains("previous.Pages == identity.Pages && previous.Page + 1 == identity.Page", StringComparison.Ordinal));
        Assert.IsTrue(source.Contains("if (!first) continuations.Add(target.Count)", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("RevealAll()", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Apply(new(\"ack\"))", StringComparison.Ordinal));
    }
}
