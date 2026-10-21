using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

public sealed record CampaignProfileEditor(string Ticket, long Revision, SourceEasyChatEditState Edit,
    ImmutableArray<ushort> OriginalWords, string NativeWaitId, StoryFrame NativeFrame, long NativeRevision)
{
    public string WaitId => Ticket + ":" + Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed partial class CampaignSession
{
    private const string ProfileSetFlag = "FLAG_SYS_SET_TRAINER_CARD_PROFILE";
    public ImmutableArray<ushort> ProfileWords => SavedProfileWords(State);

    private ImmutableArray<ushort> SavedProfileWords(CampaignState state) => state.ProfileWords ??
        _content.MailComposer?.ProfileDefinition?.DefaultWords ?? throw new InvalidDataException("profile_content_missing");

    private string? ValidateEasyChatProfile(CampaignState state)
    {
        if (_content.MailComposer is not { ProfileDefinition: { } definition } catalog)
            return state.ProfileWords is null ? null : "save_profile_content_missing";
        // Before this feature existed, an omitted profile was the source's NewGameInitData default.
        // Once the source flag is set, an omitted custom profile is no longer reconstructible.
        if (state.ProfileWords is null)
            return state.Script.Flags.GetValueOrDefault(ProfileSetFlag) ? "save_profile_words_missing" : null;
        if (SourceEasyChatRules.ValidateWords(catalog, state.ProfileWords.Value, 4, true) is { } invalid)
            return invalid.Replace("easy_chat_", "save_profile_", StringComparison.Ordinal);
        return !state.Script.Flags.GetValueOrDefault(ProfileSetFlag) && !state.ProfileWords.Value.SequenceEqual(definition.DefaultWords)
            ? "save_profile_flag_missing" : null;
    }

    private StoryInstruction? EasyChatNativeInstruction(CampaignState state)
    {
        if (state.Script.Completed || state.Script.Wait?.Kind != StoryWaitKind.NativeEffect || state.Script.Stack.IsDefaultOrEmpty) return null;
        var frame = state.Script.Stack[^1]; var program = _content.FindProgram(frame.ProgramId);
        int index = frame.InstructionIndex - 1;
        return program is not null && index >= 0 && index < program.Instructions.Length ? program.Instructions[index] : null;
    }

    private bool IsProfileCallback(CampaignState state, CampaignProfileEditor editor) =>
        state.Script.Wait?.CorrelationId == editor.NativeWaitId && state.Script.Revision == editor.NativeRevision &&
        !state.Script.Stack.IsDefaultOrEmpty && state.Script.Stack[^1] == editor.NativeFrame &&
        EasyChatNativeInstruction(state) is { Op: StoryOp.Special, Opcode: "special" } instruction &&
        instruction.Args.SequenceEqual(new[] { "ShowEasyChatScreen" }) &&
        state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) == _content.MailComposer?.ProfileDefinition?.Type &&
        SavedProfileWords(state).SequenceEqual(editor.OriginalWords);

    private Decision<CampaignState> DecideProfileEditor(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.ProfileEditor is not { } editor || _content.MailComposer is not { ProfileDefinition: { } definition } catalog)
            return No("profile_composer_context");
        if (!IsProfileCallback(state, editor)) return No("profile_composer_callback_context");
        if (command.Text != editor.WaitId) return No("profile_composer_stale_input");
        var result = SourceEasyChatEditRules.Apply(catalog, EasyChatSelection(state), editor.Edit, 4,
            EasyChatAction(command.Kind, "profile_compose_"), command.Choice, command.Quantity);
        if (!result.Succeeded) return No("profile_composer_" + result.Error);
        if (result.Outcome == SourceEasyChatEditOutcome.Continue)
            return new(CommandStatus.Accepted, state with { ProfileEditor = editor with { Edit = result.State, Revision = editor.Revision + 1 } });
        bool commit = result.Outcome == SourceEasyChatEditOutcome.Commit;
        int changed = commit && !editor.OriginalWords.SequenceEqual(editor.Edit.Words) ? 1 : 0;
        // Task_RunEasyChat performs both effects on every normal exit, including confirmed cancellation.
        // The phrase comparison intentionally reads the transient buffer, not the committed profile.
        int phraseDifferent = editor.Edit.Words.SequenceEqual(definition.SpecialPhrase) ? 0 : 1;
        var next = state with { ProfileEditor = null, ProfileWords = commit ? editor.Edit.Words : editor.OriginalWords,
            Dialogue = null, Motions = [], PresentationTicks = 0, PresentationEffects = [],
            Script = state.Script with { Flags = state.Script.Flags.SetItem(ProfileSetFlag, true),
                Variables = state.Script.Variables.SetItem("VAR_RESULT", changed).SetItem("VAR_0x8004", phraseDifferent) } };
        return new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, editor.NativeWaitId, _content))));
    }

    private bool TryApplyEasyChatProfileEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode != "special" || effect.Args.Length != 1 ||
            effect.Args[0] is not ("ShowEasyChatScreen" or "ShowEasyChatMessage")) return false;
        if (EasyChatNativeInstruction(state) is not { Op: StoryOp.Special, Opcode: "special" } instruction ||
            instruction.Source != effect.Source || !instruction.Args.SequenceEqual(effect.Args))
            throw new InvalidDataException("profile_native_instruction_context");
        if (_content.MailComposer is not { ProfileDefinition: { } definition } catalog ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != definition.Type)
            throw new InvalidDataException("easy_chat_type_not_supported");
        if (ValidateEasyChatProfile(state) is { } error) throw new InvalidDataException(error);
        var words = SavedProfileWords(state);
        if (effect.Args[0] == "ShowEasyChatMessage")
        {
            next = state with { Dialogue = new("gStringVar4", [SourceEasyChatEditRules.FormatWords(catalog, words, 2)], 0, false, false) };
            return true;
        }
        if (state.ProfileEditor is not null || state.MailComposer is not null || state.MailInteraction is not null)
            throw new InvalidDataException("easy_chat_editor_active");
        next = state with { ProfileEditor = new("profile:" + (Revision + 1), 0,
            new(SourceEasyChatEditPhase.Edit, words), words, state.Script.Wait!.CorrelationId,
            state.Script.Stack[^1], state.Script.Revision), Dialogue = null, Motions = [], PresentationTicks = 0,
            PresentationEffects = [] };
        return true;
    }
}
