using System.Globalization;
using System.Text;

/// <summary>
/// Presentation-only adapter for one already-expanded Unicode field-dialogue page.
/// Models the pinned ordinary character/newline printer with A/B speed-up enabled.
/// It does not parse original encoded text, escapes, controls, prompts or scrolling.
/// </summary>
public sealed class SourceTextProgress
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    private string _text = string.Empty;
    private int[] _elementOffsets = [];
    private int _nextElement;
    private int[] _repeatControlOffsets = [];
    private int _nextControl;
    private int _visibleLength;

    public string VisibleText => _text[.._visibleLength];
    public int VisibleCodepoints { get; private set; }
    /// <summary>Unicode text elements emitted, excluding LF. Not a UTF-16 length or ROM glyph ID count.</summary>
    public int VisibleGlyphs { get; private set; }
    /// <summary>True after the implicit page-end/EOS is processed, including its cadence wait.</summary>
    public bool Complete { get; private set; } = true;
    public int ConfiguredSpeed { get; private set; } = 4;
    public int DelayCounter { get; private set; }
    public bool HasPrintBeenSpedUp { get; private set; }

    /// <summary>
    /// Start a new page with configured source speed 8 (SLOW), 4 (MID), or 1 (FAST).
    /// Reset draws nothing. Even an empty page processes EOS on the first frame.
    /// Unicode text elements keep surrogate pairs and combining sequences intact.
    /// Only LF is a supported control character; source markup must already be expanded.
    /// </summary>
    public void Reset(string text, int configuredSpeed, IReadOnlyList<int>? repeatControlOffsets = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (configuredSpeed is not (8 or 4 or 1))
            throw new ArgumentOutOfRangeException(nameof(configuredSpeed), "Supported configured source speeds are 8, 4 and 1.");
        StartPage(text, repeatControlOffsets);
        ConfiguredSpeed = configuredSpeed;
        DelayCounter = 0;
        HasPrintBeenSpedUp = false;
        Complete = false;
    }

    /// <summary>Continue after a clear/scroll prompt without resetting source delay or speed-up latch.</summary>
    public void ContinuePage(string text, IReadOnlyList<int>? repeatControlOffsets = null)
    {
        if (!Complete) throw new InvalidOperationException("The current source page is still printing.");
        StartPage(text, repeatControlOffsets);
        Complete = false;
    }

    private void StartPage(string text, IReadOnlyList<int>? repeatControlOffsets)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidatePage(text);
        var offsets = StringInfo.ParseCombiningCharacters(text);
        int[] controls = repeatControlOffsets?.ToArray() ?? [];
        int previous = 0;
        foreach (int offset in controls)
        {
            if (offset < previous || offset > text.Length || (offset != text.Length && Array.BinarySearch(offsets, offset) < 0))
                throw new ArgumentException("Formatting steps must be ordered Unicode element boundaries.", nameof(repeatControlOffsets));
            previous = offset;
        }
        _text = text; _elementOffsets = offsets; _repeatControlOffsets = controls;
        _nextElement = _nextControl = _visibleLength = VisibleGlyphs = VisibleCodepoints = 0;
    }

    /// <summary>
    /// Advance whole source frames. Held applies to every frame; pressed only to the first.
    /// Zero frames has no effect and does not queue a press. Callers retain subframe input.
    /// No callback, gameplay command, page advance or other authoritative mutation occurs.
    /// </summary>
    public void AdvanceFrames(int frames, bool speedButtonHeld, bool speedButtonPressed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        for (int frame = 0; frame < frames && !Complete; frame++)
            AdvanceFrame(speedButtonHeld, speedButtonPressed && frame == 0);
    }

    private void AdvanceFrame(bool held, bool pressed)
    {
        // text_printer.c:AddTextPrinter subtracts one before the scheduled printer runs.
        int textSpeed = ConfiguredSpeed - 1;
        while (true) // text_printer.c:RenderFont immediately retries RENDER_REPEAT.
        {
            // text.c:RenderText preserves the speed-up latch until a new page is created.
            if (held && HasPrintBeenSpedUp)
                DelayCounter = 0;

            if (DelayCounter != 0 && textSpeed != 0)
            {
                DelayCounter--;
                if (pressed)
                {
                    HasPrintBeenSpedUp = true;
                    DelayCounter = 0;
                }
                return; // RENDER_UPDATE, including the frame which first observes A/B.
            }

            DelayCounter = textSpeed;
            if (_nextControl < _repeatControlOffsets.Length && _repeatControlOffsets[_nextControl] == _visibleLength)
            {
                _nextControl++;
                continue; // COLOR/SHADOW/FONT return RENDER_REPEAT, exactly like LF.
            }
            if (_nextElement == _elementOffsets.Length)
            {
                Complete = true; // Implicit EOS: RENDER_FINISH, not another game command.
                return;
            }

            int start = _elementOffsets[_nextElement++];
            _visibleLength = _nextElement < _elementOffsets.Length ? _elementOffsets[_nextElement] : _text.Length;
            foreach (var rune in _text.AsSpan(start, _visibleLength - start).EnumerateRunes()) VisibleCodepoints++;
            if (_text[start] == '\n')
                continue; // RENDER_REPEAT re-enters the delay branch within THIS frame.

            VisibleGlyphs++;
            return; // RENDER_PRINT emits at most one ordinary text element per frame.
        }
    }

    private static void ValidatePage(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char value = text[i];
            if (char.IsControl(value) && value != '\n')
                throw new ArgumentException("Only LF is supported; expand or handle text controls before resetting this page.", nameof(text));
            if (!char.IsSurrogate(value)) continue;
            if (!char.IsHighSurrogate(value) || i + 1 == text.Length || !char.IsLowSurrogate(text[i + 1]))
                throw new ArgumentException("Dialogue text must contain valid Unicode.", nameof(text));
            i++;
        }
    }
}
