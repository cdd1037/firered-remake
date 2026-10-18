using System.Collections.Immutable;

namespace Game.Domain.Pokemon;

public sealed record SourceEasyChatTemplate(string Id, byte Type, int Columns, int Rows, int WordCount,
    int FrameId, string? TitleTextSymbol, ImmutableArray<string> InstructionTextSymbols,
    ImmutableArray<string> ConfirmTextSymbols, ImmutableArray<string> CancelTextSymbols);
public sealed record SourceEasyChatProfileDefinition(byte Type, ImmutableArray<ushort> DefaultWords,
    ImmutableArray<ushort> SpecialPhrase);

public enum SourceEasyChatEditPhase { Edit, ConfirmDone, ConfirmCancel, ConfirmDelete }
public enum SourceEasyChatEditAction { Word, Done, Cancel, Back, DeleteAll, Confirm, Unsupported }
public enum SourceEasyChatEditOutcome { Continue, Commit, Cancel }
public sealed record SourceEasyChatEditState(SourceEasyChatEditPhase Phase, ImmutableArray<ushort> Words);
public sealed record SourceEasyChatEditResult(SourceEasyChatEditState State, SourceEasyChatEditOutcome Outcome,
    string? Error = null)
{
    public bool Succeeded => Error is null;
}

/// <summary>The source Easy Chat word buffer and its three confirmations are common to profile and
/// Mail. This rule never owns saved words, stationery, party slots, or field-script callbacks.</summary>
public static class SourceEasyChatEditRules
{
    public static SourceEasyChatEditResult Apply(SourceEasyChatCatalog catalog, SourceEasyChatSelection selection,
        SourceEasyChatEditState state, int wordCount, SourceEasyChatEditAction action, int? choice = null, int word = 0)
    {
        SourceEasyChatEditResult No(string error) => new(state, SourceEasyChatEditOutcome.Continue, error);
        SourceEasyChatEditResult Phase(SourceEasyChatEditPhase phase) => new(state with { Phase = phase }, SourceEasyChatEditOutcome.Continue);
        if (wordCount is < 1 or > 9 || SourceEasyChatRules.ValidateWords(catalog, state.Words, wordCount) is not null)
            return No("word_context");
        if (state.Phase == SourceEasyChatEditPhase.Edit)
        {
            if (action == SourceEasyChatEditAction.Word)
            {
                if (choice is null || choice < 0 || choice >= wordCount || word is < 0 or > ushort.MaxValue)
                    return No("word_context");
                if (word != SourceEasyChatRules.BlankWord && !SourceEasyChatRules.IsSelectable(catalog, selection, (ushort)word))
                    return No("word_unavailable");
                return new(state with { Words = state.Words.SetItem(choice.Value, (ushort)word) }, SourceEasyChatEditOutcome.Continue);
            }
            return action switch
            {
                SourceEasyChatEditAction.Done => Phase(state.Words.All(w => w == SourceEasyChatRules.BlankWord)
                    ? SourceEasyChatEditPhase.ConfirmCancel : SourceEasyChatEditPhase.ConfirmDone),
                SourceEasyChatEditAction.Cancel or SourceEasyChatEditAction.Back => Phase(SourceEasyChatEditPhase.ConfirmCancel),
                SourceEasyChatEditAction.DeleteAll => Phase(SourceEasyChatEditPhase.ConfirmDelete),
                _ => No("active")
            };
        }
        if (state.Phase is not (SourceEasyChatEditPhase.ConfirmDone or SourceEasyChatEditPhase.ConfirmCancel or SourceEasyChatEditPhase.ConfirmDelete))
            return No("phase");
        if (action == SourceEasyChatEditAction.Back || action == SourceEasyChatEditAction.Confirm && choice == 1)
            return Phase(SourceEasyChatEditPhase.Edit);
        if (action != SourceEasyChatEditAction.Confirm || choice != 0) return No("confirmation_required");
        return state.Phase switch
        {
            SourceEasyChatEditPhase.ConfirmCancel => new(state, SourceEasyChatEditOutcome.Cancel),
            SourceEasyChatEditPhase.ConfirmDone => new(state, SourceEasyChatEditOutcome.Commit),
            _ => new(new(SourceEasyChatEditPhase.Edit, Enumerable.Repeat(SourceEasyChatRules.BlankWord, wordCount).ToImmutableArray()),
                SourceEasyChatEditOutcome.Continue)
        };
    }

    public static string FormatWords(SourceEasyChatCatalog catalog, ImmutableArray<ushort> words, int columns)
    {
        if (columns < 1 || words.IsDefault || words.Length == 0 || words.Length % columns != 0 ||
            words.Any(w => !SourceEasyChatRules.IsKnownWord(catalog, w))) throw new ArgumentException("easy_chat_message_invalid");
        var rows = new List<string>();
        for (int start = 0; start < words.Length; start += columns)
        {
            var text = new System.Text.StringBuilder();
            for (int column = 0; column < columns; column++)
            {
                var word = words[start + column];
                text.Append(SourceEasyChatRules.WordText(catalog, word));
                if (column + 1 < columns && word != SourceEasyChatRules.BlankWord) text.Append(' ');
            }
            rows.Add(text.ToString());
        }
        return string.Join('\n', rows);
    }
}
