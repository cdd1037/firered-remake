using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Domain;

[TestClass, TestCategory("Game")]
public sealed class SourceEasyChatEditorTests
{
    private static readonly SourceEasyChatCatalog Catalog = new([], new[]
    {
        new SourceEasyChatWord(1, 1, 0, "FIRST", "FIRST", true),
        new SourceEasyChatWord(2, 1, 1, "SECOND", "SECOND", true),
        new SourceEasyChatWord(3, 1, 2, "LOCKED", "LOCKED", false)
    }.ToImmutableDictionary(w => w.Value), [], ImmutableDictionary<string, byte>.Empty, 0, 21, 410, 201);
    private static readonly SourceEasyChatSelection Selection = new(false, false, []);
    private static SourceEasyChatEditResult Apply(SourceEasyChatEditState state, int count, SourceEasyChatEditAction action,
        int? choice = null, int word = 0) => SourceEasyChatEditRules.Apply(Catalog, Selection, state, count, action, choice, word);

    [TestMethod, DataRow(4), DataRow(9)]
    public void SharedConfirmationAndDeleteSemanticsPreserveBufferUntilAccepted(int count)
    {
        var original = new SourceEasyChatEditState(SourceEasyChatEditPhase.Edit, Enumerable.Repeat((ushort)1, count).ToImmutableArray());
        var done = Apply(original, count, SourceEasyChatEditAction.Done);
        Assert.AreEqual(SourceEasyChatEditPhase.ConfirmDone, done.State.Phase);
        Assert.AreEqual(SourceEasyChatEditOutcome.Continue, done.Outcome);
        Assert.AreEqual(original, Apply(done.State, count, SourceEasyChatEditAction.Confirm, 1).State);
        Assert.AreEqual(original, Apply(done.State, count, SourceEasyChatEditAction.Back).State);
        Assert.AreEqual(SourceEasyChatEditOutcome.Commit, Apply(done.State, count, SourceEasyChatEditAction.Confirm, 0).Outcome);
        var delete = Apply(original, count, SourceEasyChatEditAction.DeleteAll);
        Assert.AreEqual(original.Words, delete.State.Words);
        Assert.AreEqual(original, Apply(delete.State, count, SourceEasyChatEditAction.Confirm, 1).State);
        var blank = Apply(delete.State, count, SourceEasyChatEditAction.Confirm, 0);
        Assert.IsTrue(blank.State.Words.All(w => w == ushort.MaxValue));
        Assert.AreEqual(SourceEasyChatEditPhase.Edit, blank.State.Phase);
        var blankDone = Apply(blank.State, count, SourceEasyChatEditAction.Done);
        Assert.AreEqual(SourceEasyChatEditPhase.ConfirmCancel, blankDone.State.Phase);
        Assert.AreEqual(SourceEasyChatEditOutcome.Cancel, Apply(blankDone.State, count, SourceEasyChatEditAction.Confirm, 0).Outcome);
    }

    [TestMethod, DataRow(4), DataRow(9)]
    public void WordValidationRejectsUnavailableWordsBoundsAndWrongPhaseWithoutMutation(int count)
    {
        var state = new SourceEasyChatEditState(SourceEasyChatEditPhase.Edit, Enumerable.Repeat((ushort)1, count).ToImmutableArray());
        foreach (var input in new[] { (-1, 1), (count, 1), (0, -1), (0, 65536), (0, 3), (0, 500) })
        {
            var result = Apply(state, count, SourceEasyChatEditAction.Word, input.Item1, input.Item2);
            Assert.IsFalse(result.Succeeded); Assert.AreSame(state, result.State);
        }
        var blank = Apply(state, count, SourceEasyChatEditAction.Word, count - 1, ushort.MaxValue);
        Assert.IsTrue(blank.Succeeded); Assert.AreEqual(ushort.MaxValue, blank.State.Words[^1]);
        var pending = Apply(state, count, SourceEasyChatEditAction.Cancel).State;
        Assert.AreEqual("confirmation_required", Apply(pending, count, SourceEasyChatEditAction.Word, 0, 2).Error);
        Assert.AreEqual("confirmation_required", Apply(pending, count, SourceEasyChatEditAction.Confirm, 2).Error);
        Assert.AreEqual(SourceEasyChatEditOutcome.Cancel, Apply(pending, count, SourceEasyChatEditAction.Confirm, 0).Outcome);
        Assert.AreEqual("word_context", Apply(state with { Words = [] }, count, SourceEasyChatEditAction.Done).Error);
    }

    [TestMethod]
    public void FourWordValidationAndTwoColumnFormattingKeepSourceBlankSpacing()
    {
        Assert.IsNull(SourceEasyChatRules.ValidateWords(Catalog, [1, 2, ushort.MaxValue, 1], 4, true));
        Assert.AreEqual("FIRST SECOND\nFIRST", SourceEasyChatEditRules.FormatWords(Catalog, [1, 2, ushort.MaxValue, 1], 2));
        Assert.AreEqual("FIRST \nSECOND", SourceEasyChatEditRules.FormatWords(Catalog, [1, ushort.MaxValue, ushort.MaxValue, 2], 2));
        Assert.AreEqual("easy_chat_word_count", SourceEasyChatRules.ValidateWords(Catalog, [], 4));
        Assert.AreEqual("easy_chat_unknown_word", SourceEasyChatRules.ValidateWords(Catalog, [1, 2, 1, 500], 4));
        Assert.AreEqual("easy_chat_message_empty", SourceEasyChatRules.ValidateWords(Catalog, [65535, 65535, 65535, 65535], 4, true));
        Assert.AreEqual("mail_word_count", SourceEasyChatRules.ValidateWords(Catalog, []));
        Assert.AreEqual("mail_message_empty", SourceEasyChatRules.ValidateWords(Catalog, SourceEasyChatRules.BlankWords, true));
    }
}
