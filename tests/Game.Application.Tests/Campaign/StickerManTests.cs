using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Source-script transitions use isolated counter fixtures; strict saves use recorded history.</summary>
[TestClass, TestCategory("Game")]
public sealed class StickerManTests
{
    private const string Town = "map.four-island";
    private const string House = "map.four-island-house2";
    private const string Hof = "GAME_STAT_ENTERED_HOF";
    private const string Eggs = "GAME_STAT_HATCHED_EGGS";
    private const string HofAward = "VAR_HOF_BRAG_STATE";
    private const string EggAward = "VAR_EGG_BRAG_STATE";
    private const string Prefix = "FourIsland_House2_Text_";
    private static PretCampaignCatalog Content => CurrentCampaignFixture.Catalog;
    private int commandId;

    private void Set(CampaignSession session, CampaignState state) => typeof(CampaignSession)
        .GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(session, new CommandSession<CampaignState, CampaignCommand>(state,
            session.RulesVersion, Content.ContentVersion,
            Math.Max(session.Revision, CurrentCampaignFixture.Checkpoint.Generation)));

    private CampaignSession Visit(CampaignState? state = null, string mapName = House,
        int x = 4, int y = 5, Direction facing = Direction.North)
    {
        var session = CampaignSession.CreateCurrent(Content);
        Place(session, state ?? CurrentCampaignFixture.Checkpoint.State, mapName, x, y, facing);
        return session;
    }

    private void Place(CampaignSession session, CampaignState state, string mapName,
        int x, int y, Direction facing)
    {
        var map = new MapId(mapName);
        Assert.IsFalse(Content.Maps[map].GetTile(x, y)!.Blocked, "controlled position must be walkable");
        state = state with
        {
            World = state.World with { Player = new(map, x, y, Content.Maps[map].GetTile(x, y)!.Elevation, facing) },
            Script = StoryState.Empty with { Flags = state.Script.Flags, Variables = state.Script.Variables,
                EffectSequence = state.Script.EffectSequence },
            Dialogue = null, ItemMenu = null, MonPicture = null, Motions = [], PresentationTicks = 0,
            PresentationEffects = [], EntryPrograms = [], ArrivalFacing = null,
            IsSurfing = false, IsCycling = false, BicycleSlope = false, MapEdits = null
        };
        var enter = typeof(CampaignSession).GetMethod("EnterMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pump = typeof(CampaignSession).GetMethod("Pump", BindingFlags.Instance | BindingFlags.NonPublic)!;
        state = (CampaignState)enter.Invoke(session, [state, null, Game.Domain.Roaming.RoamerTransition.Warp])!;
        Set(session, (CampaignState)pump.Invoke(session, [state])!);
        Finish(session);
    }

    private void Send(CampaignSession session, CampaignCommand command)
    {
        string id = "sticker-man:" + commandId++;
        var revision = session.Revision;
        var result = session.Execute(id, revision, command);
        Assert.AreEqual(CommandStatus.Accepted, result.Status, command + ": " + result.Error);
        var stale = session.Execute(id, revision, command);
        Assert.AreEqual(CommandStatus.Rejected, stale.Status);
        Assert.AreEqual("stale_revision", stale.Error);
        Assert.AreSame(result.State, stale.State);
    }

    private HashSet<string> Finish(CampaignSession session, int? choice = null)
    {
        var labels = new HashSet<string>(StringComparer.Ordinal);
        for (int n = 0; n < 150; n++)
        {
            if (session.State.Dialogue is { } dialogue) labels.Add(dialogue.TextSymbol);
            if (session.State.Script.Wait?.Kind == StoryWaitKind.MenuChoice && choice is null) return labels;
            if (session.State.Dialogue is null && session.State.Script.Completed && session.State.EntryPrograms.IsEmpty)
                return labels;
            Send(session, session.State.Script.Wait?.Kind == StoryWaitKind.ButtonPress
                ? new("button_press", Text: "A") : new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.MenuChoice ? choice : null));
        }
        Assert.Fail("Sticker Man house source script did not finish.");
        return labels;
    }

    private CampaignSession Continue(CampaignSession session)
    {
        Assert.IsNull(session.ValidatePlayerSave(session.State), "strict player save");
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, Content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, Content.ContentVersion,
            validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(Content, decoded.State, decoded.Generation);
        Assert.AreEqual(session.Revision, restored.Revision);
        Assert.AreSequenceEqual(session.State.Script.Variables, restored.State.Script.Variables, "sticker state survives Continue");
        return restored;
    }

    private CampaignState Counters(int hof, int eggs) => CurrentCampaignFixture.Checkpoint.State with {
        GameStats = (CurrentCampaignFixture.Checkpoint.State.GameStats ?? ImmutableSortedDictionary<string,int>.Empty)
            .SetItem(Hof, hof).SetItem(Eggs, eggs),
        Script = CurrentCampaignFixture.Checkpoint.State.Script with {
            Variables = CurrentCampaignFixture.Checkpoint.State.Script.Variables.Remove(HofAward).Remove(EggAward),
            Flags = CurrentCampaignFixture.Checkpoint.State.Script.Flags.Remove("FLAG_MET_STICKER_MAN") }
    };

    private HashSet<string> Brag(CampaignSession session, int choice)
    {
        Send(session, new("interact"));
        var labels = Finish(session);
        Assert.AreEqual(StoryWaitKind.MenuChoice, session.State.Script.Wait?.Kind);
        labels.UnionWith(Finish(session, choice));
        Assert.IsTrue(session.State.Script.Completed);
        return labels;
    }

    [TestMethod]
    [DataRow(1,1)][DataRow(39,1)][DataRow(40,2)][DataRow(99,2)]
    [DataRow(100,3)][DataRow(199,3)][DataRow(200,4)][DataRow(999,4)]
    public void HallOfFameSourceBoundariesAndRepeat(int count, int tier)
    {
        var session = Visit(Counters(count, 0));
        string inventory = CanonicalJson.Hash(session.State.Inventory);
        var labels = Brag(session, 0);
        Assert.AreEqual(tier, session.State.Script.Variables.GetValueOrDefault(HofAward));
        Assert.IsTrue(labels.Contains(Prefix + "HoFStickerApplied"));
        var repeat = Brag(session, 0);
        Assert.IsTrue(repeat.Contains(Prefix + (tier == 4 ? "LivingLegendHoF" : "ComeBackWithBetterStoryHoF")));
        Assert.IsFalse(repeat.Contains(Prefix + "HoFStickerApplied"));
        Assert.AreEqual(inventory, CanonicalJson.Hash(session.State.Inventory));
    }

    [TestMethod]
    [DataRow(1,1)][DataRow(99,1)][DataRow(100,2)][DataRow(199,2)]
    [DataRow(200,3)][DataRow(299,3)][DataRow(300,4)][DataRow(65536,4)]
    public void EggSourceBoundariesAndRepeat(int count, int tier)
    {
        var session = Visit(Counters(0, count));
        var labels = Brag(session, 0);
        Assert.AreEqual(tier, session.State.Script.Variables.GetValueOrDefault(EggAward));
        Assert.IsTrue(labels.Contains(Prefix + "EggStickerApplied"));
        var repeat = Brag(session, 0);
        Assert.IsTrue(repeat.Contains(Prefix + (tier == 4 ? "LivingLegendEggs" : "ComeBackWithBetterStoryEggs")));
        Assert.IsFalse(repeat.Contains(Prefix + "EggStickerApplied"));
    }

    [TestMethod]
    public void ZeroCountsMeetThenRepeatWithoutMenuOrAwards()
    {
        var session = Visit(Counters(0, 0));
        Send(session, new("interact"));
        Assert.IsTrue(Finish(session).Contains(Prefix + "GiveYouStickerIfYouBrag"));
        Assert.IsTrue(session.State.Script.Completed);
        Send(session, new("interact"));
        Assert.IsTrue(Finish(session).Contains(Prefix + "BragAboutSomethingForMe"));
        Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault(HofAward));
        Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault(EggAward));
    }

    [TestMethod]
    [DataRow(127)][DataRow(2)]
    public void BothCategoriesCancelAndUpgradeIndependently(int cancel)
    {
        var session = Visit(Counters(1, 1));
        Brag(session, cancel);
        Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault(HofAward));
        Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault(EggAward));
        Brag(session, 1);
        Assert.AreEqual(1, session.State.Script.Variables.GetValueOrDefault(EggAward));
        Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault(HofAward));
        Brag(session, 0);
        Assert.AreEqual(1, session.State.Script.Variables.GetValueOrDefault(HofAward));
        Set(session, session.State with { GameStats = session.State.GameStats!.SetItem(Hof, 100).SetItem(Eggs, 300) });
        Brag(session, 0); Brag(session, 1);
        Assert.AreEqual(3, session.State.Script.Variables.GetValueOrDefault(HofAward));
        Assert.AreEqual(4, session.State.Script.Variables.GetValueOrDefault(EggAward));
        Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault("VAR_LINK_WIN_BRAG_STATE"));
        Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault("VAR_0x8006"));
    }

    [TestMethod]
    public void PhysicalEntranceExitAndAwardContinueUseRecordedCounts()
    {
        var restored = CampaignSession.RestoreCurrent(Content, CurrentCampaignFixture.Checkpoint.State, CurrentCampaignFixture.Checkpoint.Generation);
        var session = Visit(restored.State, Town, 25, 27);
        for(int n=0;n<2 && session.State.World.Player.MapId.Value == Town;n++) { Send(session,new("move",Direction.North)); Finish(session); }
        Assert.AreEqual(House, session.State.World.Player.MapId.Value);
        Assert.AreEqual(1, session.State.Actors.Count);
        session = Continue(session);
        Place(session, session.State, House, 4, 5, Direction.North);
        Brag(session, 0);
        session = Continue(session);
        Assert.IsTrue(session.State.Script.Variables.GetValueOrDefault(HofAward) > 0);
        Place(session, session.State, House, 4, 6, Direction.South);
        for(int n=0;n<3 && session.State.World.Player.MapId.Value == House;n++) { Send(session,new("move",Direction.South)); Finish(session); }
        Assert.AreEqual((Town,25,27),(session.State.World.Player.MapId.Value,session.State.World.Player.X,session.State.World.Player.Y));
        Continue(session);
    }
    [TestMethod]
    public void SaveRejectsUnearnedAndUnavailableStickers()
    {
        var session = Visit();
        var state = session.State;
        Assert.AreEqual("save_field_milestone_award_invalid", session.ValidatePlayerSave(state with {
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_LINK_WIN_BRAG_STATE", 1) } }));
        Assert.AreEqual("save_field_milestone_award_invalid", session.ValidatePlayerSave(state with {
            Script = state.Script with { Variables = state.Script.Variables.SetItem(HofAward, 5) } }));
    }

}
