/// <summary>
/// Immutable visual layout of ONE already-bound source page in a two-row window.
/// A caller owns the visual cursor separately from its semantic/source page index.
/// No control parsing, input, timing, command dispatch, or source-page flattening.
/// </summary>
public sealed class SourceMessageLayout
{
    private readonly string[] _pages;

    public string SourceText { get; }
    public int PageCount => _pages.Length;

    private SourceMessageLayout(string sourceText, string[] pages)
    {
        SourceText = sourceText;
        _pages = pages;
    }

    public static SourceMessageLayout Create(string sourceText, int width, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ValidateDisplayText(sourceText);
        var lines = SourceHelpTextLayout.WrapPreservingText(sourceText, width, measure);
        string[] pages = new string[(lines.Count + 1) / 2];
        for (int i = 0; i < pages.Length; i++)
            pages[i] = i * 2 + 1 < lines.Count ? lines[i * 2] + "\n" + lines[i * 2 + 1] : lines[i * 2];
        return new(sourceText, pages);
    }

    /// <summary>The full original string is retained for synchronization. Equal text
    /// at a different semantic page still requires the caller to reset its cursor.</summary>
    public bool Matches(string sourceText) => string.Equals(SourceText, sourceText, StringComparison.Ordinal);

    public SourceMessageSample Sample(int visualPage)
    {
        if ((uint)visualPage >= (uint)_pages.Length)
            throw new ArgumentOutOfRangeException(nameof(visualPage));
        return new(_pages[visualPage], visualPage, _pages.Length);
    }

    /// <summary>Choice/quantity prompts cannot gain hidden ACK pages. A failed fit
    /// returns no partial text; the caller must retain its existing explicit owner.</summary>
    public bool TryGetSingleWindow(out string text)
    {
        text = _pages.Length == 1 ? _pages[0] : "";
        return _pages.Length == 1;
    }

    private static void ValidateDisplayText(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsControl(text[i]) && text[i] != '\n')
                throw new ArgumentException("Only LF is supported; expand source controls before layout.", nameof(text));
            if (!char.IsSurrogate(text[i])) continue;
            if (!char.IsHighSurrogate(text[i]) || i + 1 == text.Length || !char.IsLowSurrogate(text[i + 1]))
                throw new ArgumentException("Display text must contain valid Unicode.", nameof(text));
            i++;
        }
    }
}

public readonly record struct SourceMessageSample(string Text, int VisualPage, int PageCount)
{
    public bool IsFinal => VisualPage + 1 == PageCount;
}
