using System.Collections.Immutable;
using Game.Content;

/// <summary>The source message printer's clear/scroll states; never changes collected facts or selection.</summary>
public sealed class FameCheckerTextPresentation
{
    public const int LineStep = 15; // FONT_NORMAL maxLetterHeight 14 + Parameterized2 lineSpacing 1.
    private readonly SourceTextProgress printer = new();
    private ImmutableArray<FameCheckerTextPage> pages = [];
    private int scrollRemaining;
    public int PageIndex { get; private set; }
    public int ScrollPixels { get; private set; }
    public bool IsScrolling { get; private set; }
    public int PromptFrames { get; private set; }
    public bool WaitingForPrompt => !pages.IsEmpty && printer.Complete && !IsScrolling &&
        pages[PageIndex].EndAction != FameCheckerTextEndAction.End;
    public bool Complete => pages.IsEmpty || printer.Complete && !IsScrolling && !WaitingForPrompt;
    public int VisibleLength => pages.IsEmpty ? 0 : pages[PageIndex].RetainedTextLength + printer.VisibleText.Length;
    public SourceTextProgress Printer => printer;

    public void Reset(ImmutableArray<FameCheckerTextPage> textPages, int configuredSpeed, bool immediate = false)
    {
        pages = textPages; PageIndex = ScrollPixels = scrollRemaining = PromptFrames = 0; IsScrolling = false;
        printer.Reset(pages.IsEmpty ? "" : pages[0].RevealText, immediate ? 1 : configuredSpeed,
            pages.IsEmpty ? [] : pages[0].RepeatControlOffsets);
        if (immediate)
        {
            if (pages.Length != 1 || pages[0].EndAction != FameCheckerTextEndAction.End)
                throw new ArgumentException("Immediate UI text must be one unprompted source page.", nameof(textPages));
            printer.AdvanceFrames(0x400, false, false); // AddTextPrinter speed 0 uses this same bounded loop.
            if (!printer.Complete) throw new InvalidDataException("Immediate source text exceeds the printer bound.");
        }
    }

    /// <returns>True only on the frame accepting a source A/B page prompt (SE_SELECT).</returns>
    public bool AdvanceFrame(bool held, bool pressed)
    {
        if (pages.IsEmpty) return false;
        if (IsScrolling)
        {
            if (scrollRemaining > 0)
            {
                int step = printer.ConfiguredSpeed switch { 8 => 1, 4 => 2, _ => 4 };
                int amount = Math.Min(step, scrollRemaining);
                scrollRemaining -= amount; ScrollPixels += amount;
            }
            else { IsScrolling = false; NextPage(); }
            return false; // RENDER_UPDATE also on the state-transition frame.
        }
        if (WaitingForPrompt)
        {
            PromptFrames++;
            if (!pressed) return false;
            if (pages[PageIndex].EndAction == FameCheckerTextEndAction.Scroll)
            { IsScrolling = true; scrollRemaining = LineStep; }
            else NextPage();
            return true;
        }
        printer.AdvanceFrames(1, held, pressed);
        return false;
    }

    private void NextPage()
    {
        if (PageIndex + 1 >= pages.Length) throw new InvalidDataException("Missing source continuation page.");
        PageIndex++; ScrollPixels = PromptFrames = 0;
        printer.ContinuePage(pages[PageIndex].RevealText, pages[PageIndex].RepeatControlOffsets);
    }
}
