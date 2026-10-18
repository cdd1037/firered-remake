using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class EasyChatProfileTests
{
    private const string Flag = "FLAG_SYS_SET_TRAINER_CARD_PROFILE";
    private static PretCampaignCatalog Content => CurrentCampaignFixture.Catalog;
    private static SourceEasyChatCatalog Words => Content.MailComposer!;
    private static ImmutableArray<ushort> Defaults => Words.ProfileDefinition!.DefaultWords;
    private static void Set(CampaignSession session, CampaignState state) => typeof(CampaignSession)
        .GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, Content.ContentVersion, session.Revision));
    private static CampaignSession Visit(CampaignState? source = null)
    {
        var session = CampaignSession.CreateCurrent(Content); var state = source ?? CurrentCampaignFixture.Checkpoint.State;
        var map = Content.MapFromSourceSymbol("MAP_PEWTER_CITY_POKEMON_CENTER_1F");
        var actor = Content.MapDefinition(map).Actors.Single(a => a.ScriptId == "MysteryEventClub_EventScript_Woman");
        state = state with { World = state.World with { Player = new(map, actor.X, actor.Y + 1, 3, Direction.North) },
            Script = StoryState.Empty with { Flags = state.Script.Flags, Variables = state.Script.Variables,
                Revision = state.Script.Revision, EffectSequence = state.Script.EffectSequence },
            Dialogue = null, ItemMenu = null, MonPicture = null, Motions = [], PresentationTicks = 0, PresentationEffects = [],
            EntryPrograms = [], ArrivalFacing = null, IsSurfing = false, IsCycling = false, BicycleSlope = false, MapEdits = null };
        state = (CampaignState)typeof(CampaignSession).GetMethod("EnterMap", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(session, [state, null, Game.Domain.Roaming.RoamerTransition.Warp])!;
        Set(session, (CampaignState)typeof(CampaignSession).GetMethod("Pump", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [state])!);
        AdvanceUntil(session, s => s.Script.Completed && s.Dialogue is null); return session;
    }
    private static void Send(CampaignSession session, CampaignCommand command)
    {
        long revision = session.Revision;
        var result = session.Execute("profile:" + revision, revision, command);
        Assert.AreEqual(CommandStatus.Accepted, result.Status, command.Kind + ": " + result.Error);
        var stale = session.Execute("profile:" + revision, revision, command);
        Assert.AreEqual(CommandStatus.Rejected, stale.Status); Assert.AreEqual("stale_revision", stale.Error);
    }
    private static void Reject(CampaignSession session, CampaignCommand command, string error)
    {
        var state = session.State; long revision = session.Revision;
        var result = session.Execute("profile-reject:" + revision, revision, command);
        Assert.AreEqual(CommandStatus.Rejected, result.Status, command.Kind + ": " + result.Error);
        Assert.AreEqual(error, result.Error); Assert.AreSame(state, session.State); Assert.AreEqual(revision, session.Revision);
    }
    private static void AdvanceUntil(CampaignSession session, Func<CampaignState, bool> predicate)
    {
        for (int n = 0; n < 100; n++)
        {
            if (predicate(session.State)) return;
            Assert.IsNull(session.State.ProfileEditor, "The native editor may only resume through its own commands.");
            Assert.AreNotEqual(StoryWaitKind.MenuChoice, session.State.Script.Wait?.Kind, "A source menu needs an explicit choice.");
            Send(session, new("ack"));
        }
        Assert.Fail("Source profile script did not reach the expected wait.");
    }
    private static void Menu(CampaignSession session)
    {
        Send(session, new("interact"));
        AdvanceUntil(session, s => s.Script.Wait?.Kind == StoryWaitKind.MenuChoice);
        Assert.AreEqual("MULTICHOICE_YES_NO_INFO_2", StoryMenuRules.Pending(session.State.Script, Content)!.Definition.Id);
    }
    private static void Open(CampaignSession session)
    {
        Menu(session); Send(session, new("ack", Choice: 0)); AdvanceUntil(session, s => s.ProfileEditor is not null);
        Assert.AreEqual(StoryWaitKind.NativeEffect, session.State.Script.Wait?.Kind);
        Assert.IsTrue(session.State.ScreenIsBlack, "The source field fade remains black behind the native editor.");
        Assert.AreEqual(CampaignEasyChatContext.Profile, session.EasyChatEditor!.Context);
        Assert.AreEqual("profile", session.EasyChatEditor!.TemplateId); Assert.AreEqual(4, session.EasyChatEditor!.Words.Length);
    }
    private static void Compose(CampaignSession session, string action, int? choice = null, int word = 1) =>
        Send(session, new("profile_compose_" + action, Choice: choice, Quantity: word, Text: session.EasyChatEditor!.WaitId));
    private static void Finish(CampaignSession session) => AdvanceUntil(session, s => s.Script.Completed && s.Dialogue is null);
    private static ushort DifferentWord(CampaignSession session) => Words.Words.Values.First(w => w.Value != session.ProfileWords[0] &&
        SourceEasyChatRules.IsSelectable(Words, session.EasyChatSelectionFacts, w.Value)).Value;
    private static void AssertSavedMessageThenDelay(CampaignSession session, string nextText)
    {
        AdvanceUntil(session, s => s.Dialogue?.TextSymbol == "gStringVar4");
        string savedText = SourceEasyChatEditRules.FormatWords(Words, session.ProfileWords, 2);
        Assert.AreEqual(savedText, session.VisibleText);
        Assert.AreEqual(StoryWaitKind.MessagePrinting, session.State.Script.Wait?.Kind);
        Assert.IsFalse(session.State.ScreenIsBlack);
        Send(session, new("ack"));
        Assert.AreEqual(StoryWaitKind.Presentation, session.State.Script.Wait?.Kind);
        Assert.AreEqual(80, session.State.PresentationTicks);
        Assert.AreEqual("gStringVar4", session.State.Dialogue!.TextSymbol);
        Assert.AreEqual(savedText, session.VisibleText, "waitmessage must leave the saved profile visible throughout delay 80.");
        Send(session, new("ack"));
        Assert.AreEqual(nextText, session.State.Dialogue!.TextSymbol);
    }
    private static CampaignSession RoundTrip(CampaignSession session)
    {
        Assert.IsNull(session.ValidatePlayerSave(session.State));
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, Content.ContentVersion);
        var save = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, Content.ContentVersion, validate: session.ValidatePlayerSave);
        return CampaignSession.RestoreCurrent(Content, save.State, save.Generation);
    }

    [TestMethod]
    public void NewGameDefaultsAndLegacyOmissionAreExplicitAndMalformedProfilesReject()
    {
        var fresh = CampaignSession.CreateCurrent(Content);
        Assert.AreSequenceEqual(new[] { "EC_WORD_I_AM", "EC_WORD_A", "EC_WORD_POKEMON", "EC_WORD_FRIEND" },
            fresh.State.ProfileWords!.Value.Select(w => Words.Words[w].Symbol).ToArray());
        var session = Visit(); var baseline = session.State with { ProfileWords = null,
            Script = session.State.Script with { Flags = session.State.Script.Flags.Remove(Flag) } };
        Set(session, baseline); Assert.AreSequenceEqual(Defaults, session.ProfileWords); Assert.IsNull(session.ValidatePlayerSave(baseline));
        Assert.AreSequenceEqual(Defaults, RoundTrip(session).ProfileWords);
        Assert.AreEqual("save_profile_words_missing", session.ValidatePlayerSave(baseline with {
            Script = baseline.Script with { Flags = baseline.Script.Flags.SetItem(Flag, true) } }));
        foreach (var profile in new ImmutableArray<ushort>[] { [], [1, 2, 3], default, [65535, 65535, 65535, 65535], [65534, 65534, 65534, 65534] })
            Assert.IsNotNull(session.ValidatePlayerSave(baseline with { ProfileWords = profile }));
        var edited = Defaults.SetItem(0, DifferentWord(session));
        Assert.AreEqual("save_profile_flag_missing", session.ValidatePlayerSave(baseline with { ProfileWords = edited }));
    }

    [TestMethod, DataRow(false, 1), DataRow(false, 127), DataRow(true, 1), DataRow(true, 127)]
    public void OriginalInfoLoopAndDeclinePreserveProfileAndVisitFlag(bool previouslyOpened, int cancel)
    {
        var session = Visit(); Set(session, session.State with { ProfileWords = Defaults,
            Script = session.State.Script with { Flags = session.State.Script.Flags.SetItem(Flag, previouslyOpened) } });
        var before = session.State; Menu(session);
        Send(session, new("ack", Choice: 2)); Assert.AreEqual("Text_ExplainProfile", session.State.Dialogue!.TextSymbol);
        AdvanceUntil(session, s => s.Script.Wait?.Kind == StoryWaitKind.MenuChoice);
        Send(session, new("ack", Choice: cancel)); Finish(session);
        Assert.AreEqual(previouslyOpened, session.State.Script.Flags.GetValueOrDefault(Flag));
        Assert.AreSequenceEqual(Defaults, session.ProfileWords); Assert.AreEqual(before.World.Rng, session.State.World.Rng);
        Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault("VAR_TEMP_1"));
    }

    [TestMethod]
    public void ChangedProfileCommitsOnlyOnConfirmationAndResumesSavedMessageThenPersists()
    {
        var session = Visit(); var before = session.State; Open(session); var original = session.ProfileWords;
        Assert.AreEqual("save_event_active", session.ValidatePlayerSave(session.State));
        Reject(session, new("ack"), "profile_composer_stale_input");
        Reject(session, new("move", Direction: Direction.South), "profile_composer_stale_input");
        ushort word = DifferentWord(session); Compose(session, "word", 0, word);
        Assert.AreSequenceEqual(original, session.ProfileWords); Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(Flag));
        Compose(session, "done"); Compose(session, "confirm", 1);
        Assert.AreEqual(SourceEasyChatEditPhase.Edit, session.EasyChatEditor!.Phase);
        Compose(session, "done"); Compose(session, "confirm", 0);
        Assert.IsNull(session.State.ProfileEditor); Assert.AreEqual(1, session.State.Script.Variables["VAR_RESULT"]);
        Assert.AreEqual(word, session.ProfileWords[0]); Assert.IsTrue(session.State.Script.Flags[Flag]);
        Assert.IsFalse(session.State.ScreenIsBlack); Assert.AreEqual(16, session.State.PresentationTicks);
        AssertSavedMessageThenDelay(session, "Text_ProfileSaysSomethingAboutYou");
        Finish(session); Assert.AreEqual(1, session.State.Script.Variables["VAR_TEMP_1"]);
        Assert.AreSame(before.Inventory, session.State.Inventory); Assert.AreEqual(before.Party, session.State.Party);
        Assert.AreEqual(before.World.Rng, session.State.World.Rng);
        var restored = RoundTrip(session); Assert.AreSequenceEqual(session.ProfileWords, restored.ProfileWords);
        Send(restored, new("interact")); Assert.AreEqual("Text_TellMoreNextTime", restored.State.Dialogue!.TextSymbol); Finish(restored);
        var revisit = Visit(restored.State); Send(revisit, new("interact"));
        Assert.AreEqual("Text_HiWeMeetAgain", revisit.State.Dialogue!.TextSymbol);
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void UnchangedDoneAndChangedCancellationReturnZeroButSetProfileFlag(bool cancelChanged)
    {
        var session = Visit(); Open(session); var original = session.ProfileWords;
        if (cancelChanged) Compose(session, "word", 0, DifferentWord(session));
        Compose(session, cancelChanged ? "cancel" : "done"); Compose(session, "confirm", 0);
        Assert.AreEqual(0, session.State.Script.Variables["VAR_RESULT"]);
        Assert.AreEqual(1, session.State.Script.Variables["VAR_0x8004"]);
        Assert.AreSequenceEqual(original, session.ProfileWords); Assert.IsTrue(session.State.Script.Flags[Flag]);
        AdvanceUntil(session, s => s.Dialogue?.TextSymbol == "Text_DecidedNotTo"); Finish(session);
        Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault("VAR_TEMP_1"));
        Assert.AreSequenceEqual(original, RoundTrip(session).ProfileWords);
    }

    [TestMethod]
    public void DeleteAllAndEmptyDoneUseSharedConfirmationsAndNeverCommitEmptyProfile()
    {
        var session = Visit(); Open(session); var original = session.ProfileWords;
        Compose(session, "delete_all"); Compose(session, "confirm", 1); Assert.AreSequenceEqual(original, session.EasyChatEditor!.Words);
        Compose(session, "delete_all"); Compose(session, "confirm", 0); Assert.IsTrue(session.EasyChatEditor!.Words.All(w => w == 65535));
        Compose(session, "done"); Assert.AreEqual(SourceEasyChatEditPhase.ConfirmCancel, session.EasyChatEditor!.Phase);
        Compose(session, "back"); Assert.AreEqual(SourceEasyChatEditPhase.Edit, session.EasyChatEditor!.Phase);
        Compose(session, "done"); Compose(session, "confirm", 0); Finish(session);
        Assert.AreSequenceEqual(original, session.ProfileWords); Assert.AreEqual(0, session.State.Script.Variables["VAR_RESULT"]);
        Assert.AreSequenceEqual(original, RoundTrip(session).ProfileWords);
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void SpecialPhraseUsesDraftEvenOnCancelButDisplaysSavedProfile(bool commit)
    {
        var session = Visit(); var flags = session.State.Script.Flags; var rng = session.State.World.Rng;
        Open(session); var previous = session.ProfileWords;
        for (int i = 0; i < 4; i++) Compose(session, "word", i, Words.ProfileDefinition!.SpecialPhrase[i]);
        Compose(session, commit ? "done" : "cancel"); Compose(session, "confirm", 0);
        Assert.AreEqual(commit ? 1 : 0, session.State.Script.Variables["VAR_RESULT"]);
        Assert.AreEqual(0, session.State.Script.Variables["VAR_0x8004"]);
        Assert.AreSequenceEqual(commit ? Words.ProfileDefinition!.SpecialPhrase : previous, session.ProfileWords);
        AssertSavedMessageThenDelay(session, "Text_HeardThatBefore"); Finish(session);
        Assert.AreEqual(1, session.State.Script.Variables["VAR_TEMP_1"]);
        Assert.AreEqual(rng, session.State.World.Rng);
        Assert.AreEqual(CanonicalJson.Hash(flags.SetItem(Flag, true)), CanonicalJson.Hash(session.State.Script.Flags));
        Assert.AreSequenceEqual(session.ProfileWords, RoundTrip(session).ProfileWords);
    }

    [TestMethod]
    public void MailKeepsItsActualCommandsPreviewAndItemTransactionThroughSharedEditor()
    {
        var baseline = CurrentCampaignFixture.Checkpoint.State;
        var holder = baseline.Party[0] with { HeldItem = "ITEM_NONE", Mail = SourceMailState.NoMail };
        var item = new ItemId("pret:ITEM_ORANGE_MAIL");
        var session = CampaignSession.RestoreCurrent(Content, baseline with { Party = baseline.Party.SetItem(0, holder),
            Inventory = baseline.Inventory! with { BagItems = [new(item, 1)] } }, 0);
        var original = session.ProfileWords; var rng = session.State.World.Rng; var flag = session.State.Script.Flags.GetValueOrDefault(Flag);
        Send(session, new("bag_open")); Send(session, new("mail_preview_open", Text: item.Value));
        Assert.AreEqual(CampaignMailComposePhase.Preview, session.State.MailComposer!.Phase); Assert.IsNull(session.EasyChatEditor);
        Send(session, new("mail_compose_back", Text: session.State.MailComposer!.WaitId));
        var give = new CampaignCommand("field_give", Text: item.Value, PartyIndex: 0) { TargetInstanceId = holder.InstanceId };
        Send(session, give); Assert.AreEqual(CampaignEasyChatContext.Mail, session.EasyChatEditor!.Context);
        Assert.AreEqual("mail", session.EasyChatEditor!.TemplateId); Assert.AreEqual(9, session.EasyChatEditor!.Words.Length);
        Send(session, new("mail_compose_done", Text: session.EasyChatEditor!.WaitId));
        Assert.AreEqual(SourceEasyChatEditPhase.ConfirmCancel, session.EasyChatEditor!.Phase);
        Send(session, new("mail_compose_confirm", Choice: 0, Text: session.EasyChatEditor!.WaitId));
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, item));
        Assert.AreEqual(holder, session.State.Party[0]);
        Send(session, give);
        Send(session, new("mail_compose_word", Choice: 0, Quantity: Defaults[0], Text: session.EasyChatEditor!.WaitId));
        Send(session, new("mail_compose_done", Text: session.EasyChatEditor!.WaitId));
        Send(session, new("mail_compose_confirm", Choice: 0, Text: session.EasyChatEditor!.WaitId));
        Assert.AreEqual(0, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, item));
        Assert.AreEqual("ITEM_ORANGE_MAIL", session.State.Party[0].HeldItem);
        Assert.AreEqual(Defaults[0], session.State.Mail!.Attached[session.State.Party[0].Mail]!.Words[0]);
        Assert.AreSequenceEqual(original, session.ProfileWords); Assert.AreEqual(flag, session.State.Script.Flags.GetValueOrDefault(Flag));
        Assert.AreEqual(rng, session.State.World.Rng);
    }

    [TestMethod]
    public void PreflightStopsOnlyAtAnAuthenticatedProfileTransaction()
    {
        var session = Visit(); Open(session); var original = session.State; long revision = session.Revision;
        var preflight = typeof(CampaignSession).GetMethod("PreflightStoryPath", BindingFlags.Instance | BindingFlags.NonPublic)!;
        preflight.Invoke(session, [new Func<CampaignState>(() => original), "MysteryEventClub_EventScript_Woman"]);
        Assert.AreSame(original, session.State); Assert.AreEqual(revision, session.Revision);
        foreach (var script in new[] {
            original.Script with { Revision = original.Script.Revision + 1 },
            original.Script with { Wait = original.Script.Wait! with { CorrelationId = "other" } } })
        {
            var forged = original with { Script = script };
            var error = Assert.Throws<TargetInvocationException>(() => preflight.Invoke(session,
                [new Func<CampaignState>(() => forged), "MysteryEventClub_EventScript_Woman"]));
            Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);
            Assert.AreEqual("profile_preflight_callback_context", error.InnerException!.Message);
            Assert.AreSame(original, session.State); Assert.AreEqual(revision, session.Revision);
        }
        Compose(session, "cancel"); Compose(session, "confirm", 0); Finish(session);
    }

    [TestMethod]
    public void EditorRejectsStaleTicketsWordsAndMismatchedSuspendedNativeInstruction()
    {
        var session = Visit(); Open(session); string stale = session.EasyChatEditor!.WaitId;
        Compose(session, "word", 0, DifferentWord(session));
        Reject(session, new("profile_compose_done", Text: stale), "profile_composer_stale_input");
        foreach (var pair in new[] { (-1, 1), (4, 1), (0, -1), (0, 65536) })
            Reject(session, new("profile_compose_word", Text: session.EasyChatEditor!.WaitId, Choice: pair.Item1, Quantity: pair.Item2), "profile_composer_word_context");
        var unavailable = Words.Words.Values.First(w => !SourceEasyChatRules.IsSelectable(Words, session.EasyChatSelectionFacts, w.Value));
        Reject(session, new("profile_compose_word", Text: session.EasyChatEditor!.WaitId, Choice: 0, Quantity: unavailable.Value), "profile_composer_word_unavailable");
        var valid = session.State; var script = valid.Script;
        foreach (var corrupted in new[] {
            script with { Revision = script.Revision + 1 },
            script with { Wait = script.Wait! with { CorrelationId = "other" } },
            script with { Wait = script.Wait! with { Kind = StoryWaitKind.Presentation } },
            script with { Stack = script.Stack.SetItem(script.Stack.Length - 1, script.Stack[^1] with { InstructionIndex = script.Stack[^1].InstructionIndex + 1 }) },
            script with { Variables = script.Variables.SetItem("VAR_0x8004", 1) } })
        {
            Set(session, valid with { Script = corrupted });
            Reject(session, new("profile_compose_done", Text: session.EasyChatEditor!.WaitId), "profile_composer_callback_context");
        }
        Set(session, valid); Compose(session, "cancel"); Compose(session, "confirm", 0); Finish(session);
        Reject(session, new("profile_compose_done", Text: stale), "profile_composer_context");
    }
}
