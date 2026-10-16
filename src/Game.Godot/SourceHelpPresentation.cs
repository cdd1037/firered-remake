using Game.Content;

public enum SourceHelpLevel { Closed, Greeting, Topics, List, Answer }
/// <summary>Source Help hierarchy and seven-row, clamped midpoint scrolling.
/// Context content is snapshotted while the underlying campaign is paused.</summary>
public sealed class SourceHelpPresentation
{
    public SourceHelpLevel Level { get; private set; }
    public IReadOnlyList<HelpTopic> Topics { get; private set; } = [];
    public int TopicIndex { get; private set; }
    public int EntryIndex { get; private set; }
    public int Scroll { get; private set; }
    public int DelayFrames { get; private set; }
    private double _frames;
    public bool Active => Level != SourceHelpLevel.Closed;
    public HelpTopic Topic => Topics[TopicIndex];
    public HelpEntry Entry => Topic.Entries[EntryIndex];
    public int Count => Level == SourceHelpLevel.Topics ? Topics.Count : Topic.Entries.Length + 1;
    public int Selected => Level == SourceHelpLevel.Topics ? TopicIndex : EntryIndex;
    public bool Open(IReadOnlyList<HelpTopic> topics, bool sawIntroduction)
    {
        if (topics.Count == 0) return false;
        Topics = topics; TopicIndex = EntryIndex = Scroll = DelayFrames = 0; _frames = 0;
        Level = sawIntroduction ? SourceHelpLevel.Topics : SourceHelpLevel.Greeting; return true;
    }
    public void Close() { Level = SourceHelpLevel.Closed; DelayFrames = 0; }
    public void Advance(double delta)
    {
        _frames += Math.Max(0, delta) * (16777216d / 280896d);
        int frames = (int)_frames; _frames -= frames; DelayFrames = Math.Max(0, DelayFrames - frames);
    }
    public bool Press(CampaignButton button)
    {
        if (!Active || DelayFrames != 0) return false;
        if (Level == SourceHelpLevel.Greeting)
        {
            if (button != CampaignButton.A) return false;
            Level = SourceHelpLevel.Topics; return true;
        }
        if (button is CampaignButton.L or CampaignButton.R) { Close(); return true; }
        if (Level == SourceHelpLevel.Answer)
        {
            if (button is not (CampaignButton.A or CampaignButton.B)) return false;
            Level = SourceHelpLevel.List; DelayFrames = 2; return true;
        }
        if (button == CampaignButton.B)
        {
            if (Level == SourceHelpLevel.Topics) Close();
            else { Level = SourceHelpLevel.Topics; Scroll = 0; }
            return true;
        }
        if (button == CampaignButton.A)
        {
            if (Level == SourceHelpLevel.Topics)
            {
                if (Topic.Id == "TOPIC_EXIT") Close();
                else { Level = SourceHelpLevel.List; EntryIndex = Scroll = 0; DelayFrames = 2; }
            }
            else if (EntryIndex == Topic.Entries.Length) { Level = SourceHelpLevel.Topics; Scroll = 0; }
            else Level = SourceHelpLevel.Answer;
            return true;
        }
        int step = button switch { CampaignButton.Up => -1, CampaignButton.Down => 1, CampaignButton.Left => -7, CampaignButton.Right => 7, _ => 0 };
        if (step == 0) return false;
        int previous = Selected, selected = Math.Clamp(previous + step, 0, Count - 1);
        if (Level == SourceHelpLevel.Topics) TopicIndex = selected; else EntryIndex = selected;
        int oldScroll = Scroll, cursor = previous - Scroll;
        int shown = Math.Min(7, Count);
        for (int i = previous; i != selected; i += Math.Sign(step))
        {
            if (step > 0)
            { if (Scroll == Count - shown || cursor < shown / 2 + (shown & 1)) cursor++; else Scroll++; }
            else
            { if (Scroll == 0 || cursor > shown - (shown / 2 + (shown & 1)) - 1) cursor--; else Scroll--; }
        }
        if (Level == SourceHelpLevel.List && oldScroll != Scroll) DelayFrames = 2;
        return previous != selected;
    }
}

public static class SourceHelpTextLayout
{
    public static IReadOnlyList<string> Wrap(string text, int width, Func<string, float> measure)
    {
        List<string> lines = [];
        foreach (string paragraph in text.Replace("\r", "").Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' '))
            {
                string next = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && measure(next) > width) { lines.Add(line); line = word; }
                else line = next;
            }
            lines.Add(line);
        }
        return lines;
    }
}
