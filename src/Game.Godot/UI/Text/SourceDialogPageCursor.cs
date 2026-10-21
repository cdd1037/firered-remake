/// <summary>Presentation-only state over engine-produced visual pages. The caller
/// retains source timing, semantic identity, input gates and authoritative commands.</summary>
public sealed class SourceDialogPageCursor
{
    public int Page { get; private set; }
    public int Count { get; private set; }
    public bool IsFinal => Count > 0 && Page + 1 == Count;
    public bool Acknowledged { get; private set; }

    public void Reset(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);
        Page = 0; Count = pageCount; Acknowledged = false;
    }

    public bool TryAdvanceVisual(bool sourcePrinterComplete)
    {
        if (!sourcePrinterComplete || Count == 0 || IsFinal || Acknowledged) return false;
        Page++; return true;
    }

    public bool TryTakeSemanticAcknowledgement(bool sourcePrinterComplete)
    {
        if (!sourcePrinterComplete || !IsFinal || Acknowledged) return false;
        Acknowledged = true; return true;
    }

    public void Clear() { Page = Count = 0; Acknowledged = false; }
}
