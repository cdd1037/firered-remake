using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Events;

/// <summary>Bounded source Teachy TV two-line printer, sharing the established source glyph cadence.
/// The authenticated bodies contain only newline/clear/scroll controls. No battle text is synthesized.</summary>
public sealed class TeachyTvTextPresentation
{
    private readonly SourceTextProgress _printer = new();
    private ImmutableArray<FameCheckerTextPage> _pages = [];
    private int _index, _speed = 4, _scrollPixels, _promptFrames;
    private bool _scrolling, _held, _pressed;
    public FameCheckerTextPage? Page => _pages.IsEmpty ? null : _pages[_index];
    public int VisibleLength => Page is {} p ? p.RetainedTextLength + _printer.VisibleText.Length : 0;
    public int ScrollPixels => _scrollPixels;
    public int PromptFrames => _promptFrames;
    public bool AtPrompt => !_scrolling && Page is {} p && _printer.Complete && p.EndAction != FameCheckerTextEndAction.End;
    public bool Complete => _pages.IsEmpty || (!_scrolling && _index == _pages.Length - 1 && _printer.Complete);

    public void Clear() { _pages = []; _index = _scrollPixels = _promptFrames = 0; _scrolling = _held = _pressed = false; }
    public void Begin(string symbol, string encoded, int configuredSpeed)
    {
        if (encoded.Any(c => c is '{' or '}')) throw new InvalidDataException("Unsupported Teachy TV text control: " + symbol);
        Clear(); _speed = configuredSpeed;
        _pages = FameCheckerTextPages.Parse(new FameCheckerText(symbol, "src/data/text/teachy_tv.h", 0,
            encoded.EndsWith('$') ? encoded : encoded + "$", []), "RED", "BLUE");
        ResetPage();
    }
    public void ReleaseInput() { _held = _pressed = false; }

    public void Confirm(bool pressed)
    {
        _held = pressed;
        if (!pressed) return;
        _pressed = true;
        if (!AtPrompt) return;
        if (Page!.EndAction == FameCheckerTextEndAction.Scroll) { _scrolling = true; _scrollPixels = 0; }
        else NextPage();
        _pressed = false;
    }
    public void AdvanceFrame()
    {
        if (_pages.IsEmpty) return;
        if (_scrolling)
        {
            if (_scrollPixels == 15) { _scrolling = false; NextPage(); }
            else _scrollPixels = Math.Min(15, _scrollPixels + (_speed == 8 ? 1 : _speed == 4 ? 2 : 4));
            return;
        }
        _printer.AdvanceFrames(1, _held, _pressed); _pressed = false;
        if (AtPrompt) _promptFrames++; else _promptFrames = 0;
    }
    private void NextPage()
    {
        if (_index + 1 >= _pages.Length) throw new InvalidDataException("Teachy TV prompt has no continuation.");
        _index++; _scrollPixels = _promptFrames = 0; ResetPage();
    }
    private void ResetPage() { _pressed = false; _printer.Reset(_pages[_index].RevealText, _speed); }
}
