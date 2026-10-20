using System.Text;

/// <summary>Renderer admission is narrower than Domain Unicode name validity.
/// Check exact scalars without truncating, normalizing, or replacing the stored name.</summary>
public static class SourceNameGlyphs
{
    public static bool CanRender(string text, Func<int, bool> supports)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(supports);
        for (int index = 0; index < text.Length;)
        {
            if (!Rune.TryGetRuneAt(text, index, out var rune) || !supports(rune.Value)) return false;
            index += rune.Utf16SequenceLength;
        }
        return true;
    }
}
