using System.Reflection;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class NameRaterTransactionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static PretCampaignCatalog Content => CurrentCampaignFixture.Catalog;
    public enum InitialName { Default, SpeciesLiteral, CustomLiteral }

    [TestMethod]
    [DataRow(InitialName.Default, false)]
    [DataRow(InitialName.SpeciesLiteral, false)]
    [DataRow(InitialName.CustomLiteral, false)]
    [DataRow(InitialName.Default, true)]
    public void PublicRenamePreservesNamingIntentAndUsesSourceSameNameBranch(InitialName initial, bool typeSpecies)
    {
        var session = Visit(initial, out int index, out string species);
        var before = session.State; var mon = before.Party[index];
        Send(session, new("interact"));
        AckUntil(session, () => session.State.Script.Wait?.Kind == StoryWaitKind.PartySelection);
        Assert.AreEqual(1, session.State.Actors[session.State.LastTalked!].SourceLocalId);
        Send(session, new("name_rater_choose", PartyIndex: index) { TargetInstanceId = mon.InstanceId });
        AckUntil(session, () => session.State.Script.Wait?.Kind == StoryWaitKind.Nickname);
        Assert.AreEqual(mon.CustomNickname, session.State.NameRater!.Target!.OriginalCustomNickname);
        Assert.AreEqual(mon.CustomNickname, session.State.Nickname!.OriginalCustomNickname);
        Assert.AreEqual(Content.PokemonDisplayName(mon), session.State.TextBuffers["STR_VAR_3"]);
        Send(session, new("nickname", Text: typeSpecies ? species : "   ") { TargetInstanceId = mon.InstanceId });
        string? expected = typeSpecies ? species : mon.CustomNickname;
        Assert.AreEqual(expected, session.State.Party[index].CustomNickname);
        AckUntil(session, () => session.State.Dialogue?.TextSymbol ==
            "LavenderTown_House2_Text_FromNowOnShallBeKnownAsSameName");
        Assert.AreEqual(0, session.State.Script.Variables["VAR_RESULT"],
            "The source compares displayed names even when null becomes an explicit species-name literal.");
        AckUntil(session, () => session.State.Script.Completed && session.State.Dialogue is null && session.State.EntryPrograms.IsEmpty);
        Assert.IsFalse(session.State.Script.Locked); Assert.IsTrue(session.State.Script.Stack.IsEmpty);
        Assert.IsNull(session.State.NameRater); Assert.IsNull(session.State.Nickname);
        Assert.AreSequenceEqual(before.Party.SetItem(index, mon with { CustomNickname = expected }), session.State.Party);
        Assert.AreEqual(before.Storage, session.State.Storage); Assert.AreEqual(before.NextPokemonId, session.State.NextPokemonId);
        Assert.AreEqual(before.World.Rng, session.State.World.Rng);
        Assert.IsNull(session.ValidatePlayerSave(session.State));
    }

    private static CampaignSession Visit(InitialName initial, out int index, out string species)
    {
        var session = CampaignSession.CreateCurrent(Content); var checkpoint = CurrentCampaignFixture.Checkpoint;
        var state = checkpoint.State;
        index = Enumerable.Range(0, state.Party.Length).First(i =>
            NameRaterRules.Eligibility(state.Party[i], state.TrainerId, state.PlayerName) == NameRaterEligibility.Eligible);
        var mon = state.Party[index]; species = Content.PokemonDisplayName(mon with { CustomNickname = null });
        string? custom = initial switch { InitialName.Default => null, InitialName.SpeciesLiteral => species, _ => "BUDDY" };
        var map = Content.Maps[new("map.lavender-town-house-2")];
        // Only location and naming intent are controlled; use the checkpoint's existing same-OT creature.
        state = state with {
            Party = state.Party.SetItem(index, mon with { CustomNickname = custom }),
            World = state.World with { Player = new(map.Id, 4, 5, map.GetTile(4, 5)!.Elevation, Direction.North) },
            Script = StoryState.Empty with { Flags = state.Script.Flags, Variables = state.Script.Variables,
                EffectSequence = state.Script.EffectSequence },
            Dialogue = null, ItemMenu = null, Motions = [], EntryPrograms = [], ArrivalFacing = null,
            PresentationTicks = 0, PresentationEffects = [], IsSurfing = false, IsCycling = false, MapEdits = null
        };
        state = (CampaignState)typeof(CampaignSession).GetMethod("EnterMap", Private)!.Invoke(session, [state, null, RoamerTransition.Warp])!;
        state = (CampaignState)typeof(CampaignSession).GetMethod("Pump", Private)!.Invoke(session, [state])!;
        typeof(CampaignSession).GetField("_commands", Private)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, Content.ContentVersion, checkpoint.Generation));
        AckUntil(session, () => session.State.Script.Completed && session.State.EntryPrograms.IsEmpty);
        var npc = session.State.Actors.Values.Single(actor => actor.SourceLocalId == 1);
        Assert.IsTrue(npc.Active); Assert.AreEqual((4, 4), (npc.X, npc.Y));
        Assert.AreEqual("LavenderTown_House2_EventScript_NameRater", npc.ScriptId);
        Assert.IsNull(session.ValidatePlayerSave(session.State));
        return session;
    }

    private static void Send(CampaignSession session, CampaignCommand command)
    {
        long revision = session.Revision;
        var result = session.Execute("name-rater:" + revision, revision, command);
        Assert.AreEqual(CommandStatus.Accepted, result.Status, command + ": " + result.Error);
        Assert.AreEqual(revision + 1, session.Revision);
    }

    private static void AckUntil(CampaignSession session, Func<bool> done)
    {
        for (int n = 0; n < 100 && !done(); n++)
            Send(session, new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? 1 : null));
        Assert.IsTrue(done(), "Name Rater source script did not reach its expected callback or completion.");
    }
}
