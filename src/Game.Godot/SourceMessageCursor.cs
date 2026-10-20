/// <summary>Presentation-only cursor over one fully expanded semantic page. The owner
/// supplies identity (including its source page and font), and remains responsible for
/// timing, accepted input and authoritative acknowledgement. English stays unchanged.</summary>
public sealed class SourceMessageCursor
{
    private object? _identity;
    private string? _sourceText;
    private int _width, _visualPage;
    private SourceMessageLayout? _layout;
    public SourceMessageSample Sample => _sourceText is null
        ? throw new InvalidOperationException("No semantic message is synchronized.")
        : _layout?.Sample(_visualPage) ?? new(_sourceText, 0, 1);

    public bool Synchronize(object identity, string sourceText, int width, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        if (Equals(_identity, identity) && _sourceText == sourceText && _width == width) return false;
        var layout = SourceHelpTextLayout.ContainsCjk(sourceText)
            ? SourceMessageLayout.Create(sourceText, width, measure) : null;
        _identity = identity; _sourceText = sourceText; _width = width; _visualPage = 0; _layout = layout;
        return true;
    }

    public bool TryAdvance()
    {
        if (_sourceText is null || Sample.IsFinal) return false;
        _visualPage++; return true;
    }

    public void Clear() { _identity = null; _sourceText = null; _layout = null; _width = _visualPage = 0; }
}
