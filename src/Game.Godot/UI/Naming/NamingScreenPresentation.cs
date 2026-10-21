using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Pokemon;

public enum NamingScreenFlow { Player, Rival, Capture, Gift, Hatch, NameRater, Box, PartyNickname }
public enum NamingEscapeBehavior { ReturnToEntry, KeepOriginal, Stay, HostQuit }

/// <summary>Only caller identity and existing transaction behavior vary between
/// naming flows. Source templates own their visual metadata and character limits.</summary>
public sealed record NamingScreenContext(NamingScreenFlow Flow, string TargetIdentity, string OriginalName,
    string SpeciesName = "", PokemonGender Gender = PokemonGender.Genderless, int PlayerGender = 0)
{
    public string TemplateSymbol => Flow switch
    {
        NamingScreenFlow.Player => "NAMING_SCREEN_PLAYER",
        NamingScreenFlow.Rival => "NAMING_SCREEN_RIVAL",
        NamingScreenFlow.Box => "NAMING_SCREEN_BOX",
        NamingScreenFlow.Capture => "NAMING_SCREEN_CAUGHT_MON",
        NamingScreenFlow.Gift or NamingScreenFlow.Hatch or NamingScreenFlow.NameRater or NamingScreenFlow.PartyNickname => "NAMING_SCREEN_NICKNAME",
        _ => throw new ArgumentOutOfRangeException(nameof(Flow))
    };
    public bool IsOpening => Flow is NamingScreenFlow.Player or NamingScreenFlow.Rival;
    public string InitialText => IsOpening ? OriginalName : "";
    public NamingEscapeBehavior EscapeBehavior => Flow switch
    {
        NamingScreenFlow.Player or NamingScreenFlow.Rival => NamingEscapeBehavior.ReturnToEntry,
        NamingScreenFlow.Gift or NamingScreenFlow.Hatch => NamingEscapeBehavior.KeepOriginal,
        NamingScreenFlow.NameRater or NamingScreenFlow.Box => NamingEscapeBehavior.Stay,
        _ => NamingEscapeBehavior.HostQuit
    };
}

public enum NamingScreenAction { Insert, ChangePage, Delete, Submit, None }
public readonly record struct NamingScreenKey(NamingScreenAction Action, char Character = ' ');
public readonly record struct NamingScreenLayer(int Page, int OffsetY);

/// <summary>Source keyboard navigation and page animation. The native editor
/// continues owning text, selection, IME, caret and validation.</summary>
public sealed class NamingScreenPresentation(PretOpeningPresentation source)
{
    public NamingScreenContext? Context { get; private set; }
    public NamingScreenTemplate Template => source.NamingTemplates[Context?.TemplateSymbol
        ?? throw new InvalidOperationException("Naming screen has no context.")];
    public int MaxChars => Template.MaxChars;
    public int EntryX => source.NamingLayout.EntryX(MaxChars);
    public string Title => (Template.PrefixSpeciesName ? Context!.SpeciesName : "") + Template.Title;
    // An unchanged Application synchronization must not reset the editor, focus,
    // keyboard page or animation. The view initializes its editor only on true.
    public bool Open(NamingScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.IsDefined(context.Flow) || string.IsNullOrEmpty(context.TargetIdentity))
            throw new ArgumentException("Supported naming flow and target identity required.", nameof(context));
        if (Context == context) return false;
        Context = context; Reset(); return true;
    }
    public void Close() { Context = null; Reset(); }
    public int Page { get; private set; }
    public int Row { get; private set; }
    public int Column { get; private set; }
    public int VisualFrame { get; private set; }
    public int SwapFrame { get; private set; }
    public bool Swapping { get; private set; }
    public int Columns => source.Keyboard[Page][0].Length;
    public int IncomingPage => (Page + 1) % source.Keyboard.Length;
    public bool OnAction => Column == Columns;
    public int CursorX => 38 + source.NamingColumnOffsets[Page][Math.Min(Column, Columns - 1)];
    public int PageButtonPage => (Page + (Swapping && SwapFrame >= 8 ? 2 : 1)) % source.Keyboard.Length;
    public int PageButtonOffsetY => !Swapping || SwapFrame >= 12 ? 0 : SwapFrame < 8 ? SwapFrame : SwapFrame - 12;
    public bool PageButtonTextVisible => !Swapping || SwapFrame != 8;
    public int ArrowOffsetX => new[] { 0, -4, -2, -1 }[((VisualFrame + 7) / 8) & 3];
    public int UnderscoreOffsetY => new[] { 2, 3, 2, 1 }[(VisualFrame / 9) & 3];
    public void Reset()
    { Row = Column = VisualFrame = SwapFrame = 0; Page = Context is null ? 0 : Template.InitialPage; Swapping = false; }
    public bool SelectPoint(float x, float y)
    {
        if (Swapping) return false;
        if (x >= 184 && x < 224 && y >= 72 && y < 152)
            Select(y < 104 ? 0 : y < 128 ? 1 : 2, Columns);
        else if (x >= 24 && x < 176 && y >= 80 && y < 144)
        {
            int column = Enumerable.Range(0, Columns).MinBy(i => Math.Abs(x - 38 - source.NamingColumnOffsets[Page][i]));
            Select((int)((y - 80) / 16), column);
        }
        else return false;
        return true;
    }
    public void AdvanceFrames(int frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        VisualFrame += frames;
        if (!Swapping) return;
        SwapFrame = Math.Min(33, SwapFrame + frames);
        if (SwapFrame != 33) return;
        bool onAction = OnAction;
        Page = IncomingPage; Column = onAction ? Columns : Math.Min(Column, Columns - 1);
        Swapping = false;
    }
    public ImmutableArray<NamingScreenLayer> Layers(ImmutableArray<int> sine)
    {
        if (sine.Length != 320) throw new ArgumentException("Native sine table required.", nameof(sine));
        if (!Swapping) return [new(Page, 0)];
        int angle = Math.Min(128, SwapFrame * 4);
        var incoming = new NamingScreenLayer(IncomingPage, -(sine[angle] * 40 >> 8));
        var outgoing = new NamingScreenLayer(Page, -(sine[(angle + 128) & 255] * 40 >> 8));
        // Source swaps BG priorities at angle 64, before completing the second half.
        return SwapFrame < 16 ? [incoming, outgoing] : [outgoing, incoming];
    }
    public void Select(int row, int column)
    {
        if (column < 0 || column > Columns || row < 0 || row >= (column == Columns ? 3 : 4)) throw new ArgumentOutOfRangeException(nameof(row));
        if (Swapping) return;
        Row = row; Column = column;
    }
    public void Move(int dx, int dy)
    {
        if (Swapping) return;
        Column = (Column + Math.Sign(dx) + Columns + 1) % (Columns + 1);
        int rows = OnAction ? 3 : 4;
        Row = (Math.Min(Row, rows - 1) + Math.Sign(dy) + rows) % rows;
    }
    public void SelectSubmit() => Select(2, Columns);
    public NamingScreenKey Activate()
    {
        if (Swapping) return new(NamingScreenAction.None);
        if (!OnAction) return new(NamingScreenAction.Insert, source.Keyboard[Page][Row][Column]);
        if (Row == 1) return new(NamingScreenAction.Delete);
        if (Row == 2) return new(NamingScreenAction.Submit);
        SwapFrame = 0; Swapping = true;
        return new(NamingScreenAction.ChangePage);
    }
}
