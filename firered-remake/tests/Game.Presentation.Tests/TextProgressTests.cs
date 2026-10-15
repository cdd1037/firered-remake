using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Presentation;
[TestClass, TestCategory("Game")]
public sealed class TextProgressTests
{
    private void Reject<T>(Action action)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }

        throw new InvalidDataException("Expected rejection: " + typeof(T).Name);
    }

    private object Snapshot(SourceTextProgress p) => new
    {
        p.VisibleText,
        p.VisibleGlyphs,
        p.Complete,
        p.ConfiguredSpeed,
        p.DelayCounter,
        p.HasPrintBeenSpedUp
    };
    private string SnapshotText(SourceTextProgress p) => JsonSerializer.Serialize(Snapshot(p));
    [TestMethod]
    public void ordinary_pages_use_literal_source_frame_cadence()
    {
        foreach (int speed in new[]
        {
            1,
            4,
            8
        }

        )
        {
            var p = new SourceTextProgress();
            p.Reset("AB", speed);
            for (int frame = 1; frame <= speed * 2 + 1; frame++)
            {
                p.AdvanceFrames(1, false, false);
                string expected = frame <= speed ? "A" : "AB";
                Assert.IsTrue(p.VisibleText == expected, "literal cadence " + speed + "/" + frame);
                Assert.IsTrue(p.Complete == (frame == speed * 2 + 1), "literal EOS " + speed + "/" + frame);
            }
        }
    }

    [TestMethod]
    public void empty_page_processes_EOS_on_first_frame()
    {
        var p = new SourceTextProgress();
        p.Reset("", 4);
        Assert.IsTrue(!p.Complete, "Reset must await the first frame");
        p.AdvanceFrames(1, false, false);
        Assert.IsTrue(p.Complete && p.VisibleGlyphs == 0, "Empty EOS");
    }

    [TestMethod]
    public void unicode_text_elements_remain_whole()
    {
        var p = new SourceTextProgress();
        p.Reset("ée\u0301𠀀👩‍🔬", 1);
        string[] expected = ["é", "ée\u0301", "ée\u0301𠀀", "ée\u0301𠀀👩‍🔬"];
        for (int i = 0; i < expected.Length; i++)
        {
            p.AdvanceFrames(1, false, false);
            Assert.IsTrue(p.VisibleText == expected[i] && p.VisibleGlyphs == i + 1 && !p.Complete, "Unicode element was split or counted as code units.");
        }

        p.AdvanceFrames(1, false, false);
        Assert.IsTrue(p.Complete && p.VisibleGlyphs == 4, "Unicode EOS differs.");
    }

    [TestMethod]
    public void newline_only_pages_finish_without_emitting_a_glyph()
    {
        foreach (int speed in new[]
        {
            8,
            4,
            1
        }

        )
        {
            var p = new SourceTextProgress();
            p.Reset("\n", speed);
            p.AdvanceFrames(speed - 1, false, false);
            Assert.IsTrue(!p.Complete && p.VisibleGlyphs == 0, "LF-only page finished before its source EOS frame.");
            p.AdvanceFrames(1, false, false);
            Assert.IsTrue(p.Complete && p.VisibleText == "\n" && p.VisibleGlyphs == 0, "LF-only page acquired a glyph or missed EOS.");
        }
    }

    [TestMethod]
    public void frame_batches_match_single_frame_input_semantics()
    {
        var batch = new SourceTextProgress();
        var single = new SourceTextProgress();
        batch.Reset("ABCDEFG", 4);
        single.Reset("ABCDEFG", 4);
        batch.AdvanceFrames(1, false, false);
        single.AdvanceFrames(1, false, false);
        batch.AdvanceFrames(3, true, true);
        single.AdvanceFrames(1, true, true);
        single.AdvanceFrames(2, true, false);
        Assert.IsTrue(SnapshotText(batch) == SnapshotText(single) && batch.VisibleText == "ABC", "Pressed must apply to the first frame only.");
        batch.AdvanceFrames(4, false, false);
        single.AdvanceFrames(4, false, false);
        Assert.IsTrue(SnapshotText(batch) == SnapshotText(single) && batch.VisibleText == "ABCD", "Released batch must restore configured cadence.");
        batch.AdvanceFrames(100, true, false);
        Assert.IsTrue(batch.Complete && batch.VisibleText == "ABCDEFG" && batch.VisibleGlyphs == 7, "Hold must reuse the page's speed-up latch.");
    }

    [TestMethod]
    public void zero_frames_do_not_queue_input_or_change_state()
    {
        var p = new SourceTextProgress();
        p.Reset("AB", 4);
        p.AdvanceFrames(1, false, false);
        string before = SnapshotText(p);
        p.AdvanceFrames(0, true, true);
        Assert.IsTrue(SnapshotText(p) == before, "Zero-frame update mutated presentation.");
        p.AdvanceFrames(1, true, false);
        Assert.IsTrue(p.VisibleText == "A" && p.DelayCounter == 2 && !p.HasPrintBeenSpedUp, "Zero-frame input leaked into the next frame.");
    }

    [TestMethod]
    public void invalid_speed_frame_and_text_inputs_fail_atomically()
    {
        var p = new SourceTextProgress();
        p.Reset("AB", 4);
        p.AdvanceFrames(1, false, false);
        string before = SnapshotText(p);
        foreach (int speed in new[]
        {
            int.MinValue,
            -1,
            0,
            2,
            3,
            5,
            7,
            9,
            255,
            int.MaxValue
        }

        )
            Reject<ArgumentOutOfRangeException>(() => p.Reset("NEW", speed));
        Reject<ArgumentOutOfRangeException>(() => p.AdvanceFrames(-1, true, true));
        Reject<ArgumentNullException>(() => p.Reset(null !, 4));
        foreach (string text in new[]
        {
            "A\r\nB",
            "A\tB",
            "A\0B",
            "\u001b",
            "\ud800",
            "\udc00",
            "\ud800A"
        }

        )
            Reject<ArgumentException>(() => p.Reset(text, 4));
        Assert.IsTrue(SnapshotText(p) == before, "Rejected input partially reset or advanced the printer.");
    }

    [TestMethod]
    public void reset_clears_old_page_counters_latch_and_text()
    {
        var p = new SourceTextProgress();
        p.Reset("OLD", 8);
        p.AdvanceFrames(1, false, false);
        p.AdvanceFrames(1, true, true);
        Assert.IsTrue(p.HasPrintBeenSpedUp, "Setup did not arm speed-up.");
        p.Reset("N", 4);
        Assert.IsTrue(p.VisibleText == "" && p.VisibleGlyphs == 0 && !p.Complete && p.DelayCounter == 0 && !p.HasPrintBeenSpedUp && p.ConfiguredSpeed == 4, "Reset leaked prior page state.");
        p.AdvanceFrames(1, true, false);
        p.AdvanceFrames(1, true, false);
        Assert.IsTrue(p.VisibleText == "N" && p.DelayCounter == 2 && !p.Complete, "Held from a previous page must not silently arm speed-up.");
    }

    [TestMethod]
    public void completion_is_stable_and_instances_are_independent()
    {
        var first = new SourceTextProgress();
        var second = new SourceTextProgress();
        Assert.IsTrue(first.Complete && first.VisibleText == "" && first.VisibleGlyphs == 0, "Unstarted presentation must be inert.");
        first.Reset("A", 1);
        second.Reset("B", 8);
        first.AdvanceFrames(2, false, false);
        string before = SnapshotText(first);
        first.AdvanceFrames(int.MaxValue, true, true);
        Assert.IsTrue(SnapshotText(first) == before, "Completed presentation mutated or reprinted.");
        Assert.IsTrue(!second.Complete && second.VisibleText == "" && second.DelayCounter == 0, "Other instance was changed.");
    }

    [TestMethod]
    public void expanded_input_contract_does_not_pretend_to_parse_rom_controls()
    {
        const string text = "{COLOR RED}\\p$";
        var p = new SourceTextProgress();
        p.Reset(text, 1);
        p.AdvanceFrames(text.Length + 1, false, false);
        Assert.IsTrue(p.Complete && p.VisibleText == text && p.VisibleGlyphs == text.Length, "Bounded Unicode adapter unexpectedly parsed source markup.");
    }
}
