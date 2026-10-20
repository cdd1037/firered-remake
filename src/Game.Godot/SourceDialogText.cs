using System.Text;
using Godot;

/// <summary>
/// RichTextLabel owns shaping, wrapping,
/// measuring, reveal clipping and glyph rendering. This class projects its line
/// ranges into visual pages; it never parses source commands or emits gameplay ACKs.
/// Add it to the scene tree before Bind. The host owns its presentation layer.
/// </summary>
public partial class SourceDialogText : Control
{
    private readonly RichTextLabel _text = new()
    {
        BbcodeEnabled = false,
        Threaded = false,
        FitContent = false,
        ScrollActive = false,
        ClipContents = true,
        AutowrapTrimFlags = 0,
        VisibleCharactersBehavior = TextServer.VisibleCharactersBehavior.CharsAfterShaping,
        MouseFilter = MouseFilterEnum.Ignore,
        FocusMode = FocusModeEnum.None,
        TextureFilter = TextureFilterEnum.Nearest,
    };
    private readonly SourceDialogPageCursor _cursor = new();
    private record struct PageSlice(int FirstLine, int LastLine, int Start, int End, int Offset);
    private PageSlice[] _pages = [];
    private int[] _utf16Offsets = [0];
    private string _boundText = "";
    private int _revealed;

    public int VisualPage => _cursor.Page;
    public int PageCount => _cursor.Count;
    public bool IsFinal => _cursor.IsFinal;
    public bool RevealComplete => _pages.Length > 0 && _revealed == Page.End - Page.Start;
    public int PageCodepointCount => Page.End - Page.Start;
    public string CurrentVisualText => _boundText[_utf16Offsets[Page.Start].._utf16Offsets[Page.End]];
    public Vector2 ContinuationPosition => new(_text.GetLineWidth(Page.LastLine),
        _text.GetLineOffset(Page.LastLine) - Page.Offset);
    private PageSlice Page => _pages[_cursor.Page];

    public SourceDialogText()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        TextureFilter = TextureFilterEnum.Nearest;
        _text.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _text.AddThemeConstantOverride("outline_size", 0);
        _text.AddThemeConstantOverride("shadow_outline_size", 0);
        _text.AddThemeConstantOverride("paragraph_separation", 0);
        // Existing SourceGameFont textures already contain foreground and shadow.
        _text.AddThemeColorOverride("default_color", Colors.White);
        _text.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        AddChild(_text);
    }

    public void Bind(string text, Font font, int fontSize,
        int lineStep, int width, int rows = 2, bool wrap = true)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineStep);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        _boundText = text;
        List<int> offsets = [0];
        int utf16 = 0;
        foreach (var rune in text.EnumerateRunes()) { utf16 += rune.Utf16SequenceLength; offsets.Add(utf16); }
        _utf16Offsets = offsets.ToArray();
        Size = new(width, rows * lineStep);
        _text.Size = new(width, rows * lineStep);
        _text.AddThemeFontOverride("normal_font", font);
        _text.AddThemeFontSizeOverride("normal_font_size", fontSize);
        _text.AddThemeConstantOverride("line_separation", lineStep - (int)font.GetHeight(fontSize));
        _text.AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
        _text.VisibleCharacters = -1;
        _text.Text = text;
        int count = _text.GetLineCount(), total = _text.GetTotalCharacterCount();
        if (count < 1 || total != _utf16Offsets.Length - 1)
            throw new InvalidOperationException("Native text shaping did not produce complete plain text.");
        List<PageSlice> pages = [];
        for (int first = 0; first < count; first += rows)
        {
            int last = Math.Min(count - 1, first + rows - 1);
            int start = Math.Min(total, _text.GetLineRange(first).X);
            int end = first + rows < count ? Math.Min(total, _text.GetLineRange(first + rows).X) : total;
            pages.Add(new(first, last, start, end, (int)MathF.Round(_text.GetLineOffset(first))));
        }
        _pages = pages.ToArray();
        _text.Size = new(width, Math.Max(rows * lineStep, _text.GetContentHeight() + lineStep));
        _cursor.Reset(_pages.Length);
        BeginPage();
    }

    public void RevealCodepoints(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > PageCodepointCount) throw new ArgumentOutOfRangeException(nameof(count));
        _revealed = count;
        _text.VisibleCharacters = Page.Start + count;
    }

    public void RevealAll() => RevealCodepoints(PageCodepointCount);

    public bool TryAdvanceVisual(bool sourcePrinterComplete)
    {
        if (!_cursor.TryAdvanceVisual(sourcePrinterComplete)) return false;
        BeginPage(); return true;
    }

    public bool TryTakeSemanticAcknowledgement(bool sourcePrinterComplete) =>
        _cursor.TryTakeSemanticAcknowledgement(sourcePrinterComplete);

    public void Clear()
    {
        _boundText = ""; _pages = [];
        _utf16Offsets = [0]; _text.Text = ""; _cursor.Clear(); Visible = false;
    }

    private void BeginPage()
    {
        _text.Position = new(0, -Page.Offset);
        RevealCodepoints(0);
    }
}
