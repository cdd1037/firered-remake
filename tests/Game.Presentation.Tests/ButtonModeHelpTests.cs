using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Options;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class ButtonModeHelpTests
{
    [TestMethod]
    public void LEqualsAKeepsShoulderAndMixedOwnersUntilLastRelease()
    {
        var input = new CampaignButtonInput(); input.SetMode(SourceButtonMode.LEqualsA);
        Assert.AreEqual(CampaignButton.A, input.Observe(-1, 13, CampaignButton.A, true).Pressed);
        Assert.AreEqual(CampaignButton.L, input.Observe(-1, 81, CampaignButton.L, true).Pressed);
        Assert.AreEqual(CampaignButton.None, input.Observe(0, 0, CampaignButton.A, true).Pressed);
        Assert.AreEqual(CampaignButton.None, input.Observe(-1, 13, CampaignButton.A, false).Released);
        Assert.AreEqual(CampaignButton.L, input.Observe(-1, 81, CampaignButton.L, false).Released);
        Assert.AreEqual(CampaignButton.A, input.Observe(0, 0, CampaignButton.A, false).Released);
        Assert.AreEqual(CampaignButton.None, input.Held);
    }
    [TestMethod]
    public void LProducesSinglePressAndSingleReleaseWithoutDuplicateRepeat()
    {
        var input = new CampaignButtonInput(); input.SetMode(SourceButtonMode.LEqualsA);
        Assert.AreEqual(CampaignButton.A | CampaignButton.L, input.Observe(2, 9, CampaignButton.L, true).Pressed);
        Assert.AreEqual(default, input.Observe(2, 9, CampaignButton.L, true));
        Assert.AreEqual(CampaignButton.A | CampaignButton.L, input.Observe(2, 9, CampaignButton.L, false).Released);
        Assert.AreEqual(default, input.Observe(2, 9, CampaignButton.L, false));
    }
    [TestMethod]
    public void ModeReconciliationAndDisconnectDoNotLoseOtherOwners()
    {
        var input = new CampaignButtonInput(); input.Observe(-1, 81, CampaignButton.L, true);
        Assert.AreEqual(CampaignButton.A, input.SetMode(SourceButtonMode.LEqualsA).Pressed);
        input.Observe(7, 0, CampaignButton.A, true);
        Assert.AreEqual(CampaignButton.None, input.SetMode(SourceButtonMode.Lr).Released);
        Assert.AreEqual(CampaignButton.A, input.Disconnect(7).Released);
        Assert.AreEqual(CampaignButton.L, input.Held);
        input.Reset(); Assert.AreEqual(CampaignButton.None, input.Held);
        Assert.AreEqual(default, input.Observe(-1, 81, CampaignButton.L, false));
    }
    private static HelpTopic[] Topics() =>
    [ new("TOPIC_FIRST", "First", "Description", Enumerable.Range(0, 25).Select(i => new HelpEntry("entry" + i, "Question " + i, "Answer " + i)).ToImmutableArray()),
      new("TOPIC_SECOND", "Second", "Description", [new("second", "Second question", "Second answer")]),
      new("TOPIC_EXIT", "Exit", "Close Help", []) ];
    [TestMethod]
    public void GreetingWaitsForAAndThreeLevelsRestoreBothCursors()
    {
        var help = new SourceHelpPresentation(); Assert.IsTrue(help.Open(Topics(), false));
        foreach (var ignored in new[] { CampaignButton.B, CampaignButton.L, CampaignButton.R, CampaignButton.Down })
        { Assert.IsFalse(help.Press(ignored)); Assert.AreEqual(SourceHelpLevel.Greeting, help.Level); }
        help.Press(CampaignButton.A); help.Press(CampaignButton.Down); help.Press(CampaignButton.A);
        Assert.AreEqual(SourceHelpLevel.List, help.Level); Assert.AreEqual(2, help.DelayFrames);
        Assert.IsFalse(help.Press(CampaignButton.A)); help.Advance(2 / (16777216d / 280896d));
        help.Press(CampaignButton.A); Assert.AreEqual(SourceHelpLevel.Answer, help.Level);
        help.Press(CampaignButton.B); Assert.AreEqual(SourceHelpLevel.List, help.Level);
        help.Advance(1); help.Press(CampaignButton.B);
        Assert.AreEqual(SourceHelpLevel.Topics, help.Level); Assert.AreEqual(1, help.TopicIndex);
        help.Press(CampaignButton.R); Assert.IsFalse(help.Active);
    }
    [TestMethod]
    public void SevenRowPagingUsesSourceDirectionalMidpointsAndTwoFrameScrollDelay()
    {
        var help = new SourceHelpPresentation(); help.Open(Topics(), true); help.Press(CampaignButton.A); help.Advance(1);
        help.Press(CampaignButton.Right);
        Assert.AreEqual(7, help.EntryIndex); Assert.AreEqual(3, help.Scroll); Assert.AreEqual(2, help.DelayFrames);
        Assert.IsFalse(help.Press(CampaignButton.Down)); help.Advance(1);
        help.Press(CampaignButton.Left); Assert.AreEqual(0, help.EntryIndex); Assert.AreEqual(0, help.Scroll);
        help.Advance(1);
        for (int i = 0; i < 8; i++) { help.Press(CampaignButton.Right); help.Advance(1); }
        Assert.AreEqual(25, help.EntryIndex); Assert.AreEqual(19, help.Scroll);
        help.Press(CampaignButton.A); Assert.AreEqual(SourceHelpLevel.Topics, help.Level);
    }
    [TestMethod]
    public void AnswerBackRestoresEntryAndScrollAndShoulderClosesAnswer()
    {
        var help = new SourceHelpPresentation(); help.Open(Topics(), true); help.Press(CampaignButton.A); help.Advance(1);
        help.Press(CampaignButton.Right); help.Advance(1); int scroll = help.Scroll;
        help.Press(CampaignButton.A); Assert.AreEqual("entry7", help.Entry.Id);
        help.Press(CampaignButton.A); Assert.AreEqual(7, help.EntryIndex); Assert.AreEqual(scroll, help.Scroll);
        help.Advance(1); help.Press(CampaignButton.A); help.Press(CampaignButton.L); Assert.IsFalse(help.Active);
    }
    [TestMethod]
    public void AllImportedAnswersFitPanelWithLongestPlayerNameAndNoGlyphOverlap()
    {
        using var font = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/ui/font/source-font.json")));
        var face = font.RootElement.GetProperty("fonts").EnumerateArray().Single(f => f.GetProperty("id").GetString() == "small");
        var advances = face.GetProperty("glyphs").EnumerateArray().ToDictionary(g => g.GetProperty("character").GetString()!, g => g.GetProperty("advance").GetInt32());
        float Measure(string text) => text.EnumerateRunes().Sum(r => advances.GetValueOrDefault(r.ToString(), 6));
        using var content = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, HelpCatalog.ResourcePath)));
        var texts = content.RootElement.GetProperty("texts");
        string Resolve(string id) => texts.GetProperty(id).GetString()!.Replace("{PLAYER}", "WWWWWWWWWW").Replace("{STR_VAR_1}", "SOMEONE'S");
        int Lines(string text)
        {
            var lines = SourceHelpTextLayout.Wrap(text, 224, Measure);
            Assert.IsTrue(lines.All(l => Measure(l) <= 224), text);
            return lines.Count;
        }
        int checkedAnswers = 0;
        foreach (var topic in content.RootElement.GetProperty("topics").EnumerateObject())
        {
            Assert.IsTrue(112 + Lines(Resolve(topic.Value.GetProperty("description").GetString()!)) * 14 - 1 <= 142);
            foreach (var entry in topic.Value.GetProperty("entries").EnumerateArray())
            {
                int count = Lines(Resolve(entry.GetProperty("question").GetString()!)) + Lines(Resolve(entry.GetProperty("answer").GetString()!));
                Assert.IsTrue(24 + count * 14 + 5 - 1 <= 142, entry.GetProperty("id").GetString());
                checkedAnswers++;
            }
        }
        Assert.IsTrue(checkedAnswers > 100);
        Assert.IsTrue(28 + Lines(Resolve("Help_Text_Greetings")) * 14 - 1 <= 142);
    }
}
