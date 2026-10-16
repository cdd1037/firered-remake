using System.Collections.Immutable;

namespace Game.Domain.Tutorials;

public enum TeachyTvOrigin { Bag, RegisteredItem }
public enum TeachyTvPhase { Menu, Script, AwaitingDemonstration, ReturningToMenu, Closed }
public enum TeachyTvInstructionKind { Presentation, Confirm, Demonstration, ReturnToMenu }
public enum TeachyTvDemonstrationKind { PokedudeBattle, TmCase, RegisterItem }
public sealed record TeachyTvText(string Symbol, string EncodedText);
public sealed record TeachyTvMenuEntry(string LabelSymbol, int SourceIndex);
public sealed record TeachyTvSourceParty(string Side, int Level, string Species,
    ImmutableArray<string> Moves, string Nature, string Gender, int FixedIv);
public sealed record TeachyTvSourceInput(ImmutableArray<int> CursorPositions, ImmutableArray<int> Delays);
public sealed record TeachyTvSourceVoiceover(string ControllerCommand, string Side,
    string StringId, string Callback, string TextSymbol);
public sealed record TeachyTvBattleDefinition(string SourceBattleType, int SourceBattleTypeValue,
    ImmutableArray<TeachyTvSourceParty> Party, ImmutableArray<TeachyTvSourceInput> ChooseAction,
    ImmutableArray<TeachyTvSourceInput> ChooseMove, ImmutableArray<TeachyTvSourceVoiceover> Voiceovers);
public sealed record TeachyTvLesson(int SourceIndex, string SourceSymbol, string LabelSymbol,
    bool RequiresTmCase, string ScriptSymbol, ImmutableArray<string> Commands,
    string IntroductionText, string ConclusionText, TeachyTvDemonstrationKind DemonstrationKind,
    int ResumeCommandIndex, TeachyTvBattleDefinition? Battle);
public sealed record TeachyTvCatalog(ImmutableArray<TeachyTvLesson> Lessons,
    ImmutableArray<TeachyTvMenuEntry> FullMenu, ImmutableArray<TeachyTvMenuEntry> NoTmCaseMenu,
    ImmutableDictionary<string, TeachyTvText> Texts, string HelloText);
public sealed record TeachyTvPlaybackState(string SessionId, long Revision, TeachyTvOrigin Origin,
    bool HasTmCase, TeachyTvPhase Phase, int ScrollOffset = 0, int SelectedRow = 0,
    int LessonIndex = 0, int CommandIndex = 0)
{
    public string WaitId => SessionId + ":teachy-tv:" + Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string ReturnCallback => Origin == TeachyTvOrigin.Bag ? "CB2_BagMenuFromStartMenu" : "CB2_ReturnToField";
}
public sealed record TeachyTvInstruction(string WaitId, string SourceCommand,
    TeachyTvInstructionKind Kind, string? TextSymbol);
public sealed record TeachyTvDemonstrationRequest(string Ticket, TeachyTvLesson Lesson);
public sealed record TeachyTvDecision(bool Accepted, TeachyTvPlaybackState State, string? Error = null);

/// <summary>
/// Outer source script owner. This class never simulates a battle, spends an item,
/// registers the player's item or mutates party/dex/rewards. External demo owners
/// must restore their temporary party/bag state before returning their ticket.
/// Presentation ACKs acknowledge completed original controller commands; they do
/// not replace the controller's text, animation or input timing.
/// </summary>
public static class TeachyTvPlaybackRules
{
    public const string RulesVersion = "firered-teachy-tv-outer-v1";
    private const string End = "TTVcmd_End";
    private const string Confirm = "TTVcmd_EraseTextWindowIfKeyPressed";
    private const string Demonstration = "TTVcmd_TaskBattleOrFadeByOptionChosen";

    public static TeachyTvPlaybackState Open(string sessionId, TeachyTvOrigin origin, bool ownsTeachyTv, bool hasTmCase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (!ownsTeachyTv) throw new InvalidOperationException("teachy_tv_not_owned");
        if (!Enum.IsDefined(origin)) throw new ArgumentOutOfRangeException(nameof(origin));
        return new(sessionId, 0, origin, hasTmCase, TeachyTvPhase.Menu);
    }

    public static ImmutableArray<TeachyTvMenuEntry> Menu(TeachyTvCatalog catalog, TeachyTvPlaybackState state) =>
        state.HasTmCase ? catalog.FullMenu : catalog.NoTmCaseMenu;

    public static TeachyTvDecision Navigate(TeachyTvCatalog catalog, TeachyTvPlaybackState state, int absoluteIndex)
    {
        if (state.Phase != TeachyTvPhase.Menu) return No(state, "teachy_tv_not_menu");
        var entries = Menu(catalog, state);
        if (absoluteIndex < 0 || absoluteIndex >= entries.Length) return No(state, "teachy_tv_menu_index");
        int visible = state.HasTmCase ? 6 : 5;
        int offset = state.ScrollOffset, row = state.SelectedRow;
        // ListMenuUpdateSelectedRowIndexAndScrollOffset keeps the cursor around
        // the middle while more rows can scroll, rather than waiting for bottom.
        while (offset + row != absoluteIndex)
        {
            bool down = absoluteIndex > offset + row;
            if (down)
            {
                int middle = (visible + 1) / 2;
                if (offset == entries.Length - visible || row < middle) row++;
                else { offset++; row = middle; }
            }
            else
            {
                int middle = visible - (visible + 1) / 2 - 1;
                if (offset == 0 || row > middle) row--;
                else { offset--; row = middle; }
            }
        }
        return Yes(state with { ScrollOffset = offset, SelectedRow = row });
    }

    public static TeachyTvDecision ConfirmMenu(TeachyTvCatalog catalog, TeachyTvPlaybackState state)
    {
        if (state.Phase != TeachyTvPhase.Menu) return No(state, "teachy_tv_not_menu");
        var menu = Menu(catalog, state);
        int index = state.ScrollOffset + state.SelectedRow;
        if (index < 0 || index >= menu.Length) return No(state, "teachy_tv_menu_index");
        int lesson = menu[index].SourceIndex;
        if (lesson == -2) return Yes(state with { Phase = TeachyTvPhase.Closed });
        if (lesson < 0 || lesson >= catalog.Lessons.Length || catalog.Lessons[lesson].RequiresTmCase && !state.HasTmCase)
            return No(state, "teachy_tv_lesson_unavailable");
        return Yes(state with { Phase = TeachyTvPhase.Script, LessonIndex = lesson, CommandIndex = 0 });
    }

    public static TeachyTvInstruction? Current(TeachyTvCatalog catalog, TeachyTvPlaybackState state)
    {
        if (state.Phase == TeachyTvPhase.ReturningToMenu) return new(state.WaitId, End, TeachyTvInstructionKind.ReturnToMenu, null);
        if (state.Phase != TeachyTvPhase.Script) return null;
        var lesson = catalog.Lessons[state.LessonIndex];
        string command = lesson.Commands[state.CommandIndex];
        var kind = command switch
        {
            Confirm => TeachyTvInstructionKind.Confirm,
            Demonstration => TeachyTvInstructionKind.Demonstration,
            End => TeachyTvInstructionKind.ReturnToMenu,
            _ => TeachyTvInstructionKind.Presentation
        };
        string? text = command switch
        {
            "TTVcmd_NpcMoveAndSetupTextPrinter" => catalog.HelloText,
            "TTVcmd_TextPrinterSwitchStringByOptionChosen" => lesson.IntroductionText,
            "TTVcmd_TextPrinterSwitchStringByOptionChosen2" => lesson.ConclusionText,
            _ => null
        };
        return new(state.WaitId, command, kind, text);
    }

    public static TeachyTvDecision Acknowledge(TeachyTvCatalog catalog, TeachyTvPlaybackState state, string waitId)
    {
        var instruction = Current(catalog, state);
        if (instruction is null || state.WaitId != waitId) return No(state, "teachy_tv_stale_ack");
        if (instruction.Kind is TeachyTvInstructionKind.Confirm or TeachyTvInstructionKind.Demonstration)
            return No(state, "teachy_tv_input_or_demo_required");
        if (instruction.Kind == TeachyTvInstructionKind.ReturnToMenu)
            return Yes(state with { Phase = TeachyTvPhase.Menu, CommandIndex = 0 });
        return Yes(state with { CommandIndex = state.CommandIndex + 1 });
    }

    public static TeachyTvDecision ConfirmText(TeachyTvCatalog catalog, TeachyTvPlaybackState state, string waitId)
    {
        if (state.WaitId != waitId || Current(catalog, state)?.Kind != TeachyTvInstructionKind.Confirm)
            return No(state, "teachy_tv_confirm_not_pending");
        return Yes(state with { CommandIndex = state.CommandIndex + 1 });
    }

    public static (TeachyTvDecision Decision, TeachyTvDemonstrationRequest? Request) BeginDemonstration(
        TeachyTvCatalog catalog, TeachyTvPlaybackState state, string waitId)
    {
        if (state.WaitId != waitId || Current(catalog, state)?.Kind != TeachyTvInstructionKind.Demonstration)
            return (No(state, "teachy_tv_demo_not_pending"), null);
        var next = Yes(state with { Phase = TeachyTvPhase.AwaitingDemonstration });
        return (next, new(next.State.WaitId, catalog.Lessons[state.LessonIndex]));
    }

    /// <summary>Internal completion from the real demo owner, after restoration.
    /// Battle outcome 3 always skips the conclusion; bag/TM/party cancellation uses interrupted.
    /// Normal callbacks resume at source index 12 (battle) or 9 (field demonstrations).</summary>
    public static TeachyTvDecision CompleteDemonstration(TeachyTvCatalog catalog, TeachyTvPlaybackState state,
        string ticket, bool interrupted, int? sourceBattleOutcome = null)
    {
        if (state.Phase != TeachyTvPhase.AwaitingDemonstration || state.WaitId != ticket)
            return No(state, "teachy_tv_stale_demo");
        var lesson = catalog.Lessons[state.LessonIndex];
        if (lesson.DemonstrationKind == TeachyTvDemonstrationKind.PokedudeBattle)
        {
            // Cancelling from a nested bag/party menu directly resumes the menu without
            // assigning gBattleOutcome. Battle-screen B sets B_OUTCOME_DREW instead.
            if (!interrupted && sourceBattleOutcome is not (>= 1 and <= 10)) return No(state, "teachy_tv_battle_outcome_required");
            interrupted |= sourceBattleOutcome == 3;
        }
        else if (sourceBattleOutcome is not null) return No(state, "teachy_tv_unexpected_battle_outcome");
        return Yes(state with { Phase = interrupted ? TeachyTvPhase.Menu : TeachyTvPhase.Script,
            CommandIndex = interrupted ? 0 : lesson.ResumeCommandIndex });
    }

    /// <summary>B is checked before the outer script command, including its A/B confirm.
    /// External demos must receive B themselves, perform restoration, then complete.</summary>
    public static TeachyTvDecision Cancel(TeachyTvPlaybackState state) => state.Phase switch
    {
        TeachyTvPhase.Menu => Yes(state with { Phase = TeachyTvPhase.Closed }),
        TeachyTvPhase.Script => Yes(state with { Phase = TeachyTvPhase.ReturningToMenu, CommandIndex = 0 }),
        TeachyTvPhase.AwaitingDemonstration => No(state, "teachy_tv_external_cleanup_required"),
        _ => No(state, "teachy_tv_cancel_not_pending")
    };

    public static TeachyTvDecision SelectButton(TeachyTvPlaybackState state) =>
        state.Phase == TeachyTvPhase.Menu && state.Origin == TeachyTvOrigin.RegisteredItem
            ? Yes(state with { Phase = TeachyTvPhase.Closed }) : No(state, "teachy_tv_select_ignored");

    private static TeachyTvDecision Yes(TeachyTvPlaybackState state) => new(true, state with { Revision = checked(state.Revision + 1) });
    private static TeachyTvDecision No(TeachyTvPlaybackState state, string error) => new(false, state, error);
}
