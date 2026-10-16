using System.Collections.Immutable;
using System.Reflection;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Controlled valid checkpoint positions; every interaction uses public commands.</summary>
[TestClass, TestCategory("Game")]
public sealed class BadgeTopicListMenuTests : WildBattleFixture
{
    private const string Room = "map.cerulean-city-house-1";
    private const string Prefix = "CeruleanCity_House1_Text_";

    private CampaignSession At(string mapId, int x, int y, Direction facing)
    {
        var state = FieldFixture();
        var map = content.Maps[new(mapId)];
        var definition = content.MapDefinition(map.Id);
        state = state with {
            World = state.World with { Player = new(map.Id, x, y, map.GetTile(x, y)!.Elevation, facing) },
            Actors = definition.Actors.ToImmutableSortedDictionary(a => a.LocalId,
                a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation,
                    Direction.South, a.MovementType, a.ScriptId, a.HideFlag, false, false, a.X, a.Y), StringComparer.Ordinal),
            ObjectSlots = ImmutableSortedDictionary<int, string>.Empty.Add(0, "LOCALID_PLAYER"),
            SelectedObjectSlot = 0, LastTalked = null, MapEdits = null, MapLayout = null,
            FlashLevel = 0, IsSurfing = false, IsCycling = false, Music = definition.Music,
            EntryPrograms = [], ArrivalFacing = null, Dialogue = null, Motions = [],
            PresentationEffects = [], PresentationTicks = 0
        };
        state = (CampaignState)typeof(CampaignSession).GetMethod("RefreshActors", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(validator, [state])!;
        Assert.IsNull(validator.ValidatePlayerSave(state));
        return CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
    }

    private List<string> Until(CampaignSession session, Func<bool> done)
    {
        var texts = new List<string>();
        for (int step = 0; step < 60; step++)
        {
            if (session.State.Dialogue is { } dialogue) texts.Add(dialogue.TextSymbol);
            if (done()) return texts;
            Send(session, new("ack"));
        }
        Assert.Fail("Badge conversation did not reach bounded callback.");
        return texts;
    }

    private void Finish(CampaignSession session) => Until(session,
        () => session.State.Script.Completed && session.State.Script.Wait is null && session.State.Dialogue is null);

    private void Open(CampaignSession session)
    {
        Send(session, new("interact"));
        Until(session, () => session.SourceMenu is not null);
        Assert.AreEqual("LISTMENU_BADGES", session.SourceMenu!.Definition.Id);
    }

    [TestMethod]
    public void AllEightTopicsReturnToSameMenuThenExitReleasesFieldAndStrictSave()
    {
        var session = At(Room, 6, 6, Direction.North);
        Open(session);
        var before = session.State;
        string[] topics = ["AttackStatFlash", "ObeyLv30Cut", "SpeedStatFly", "ObeyLv50Strength",
            "DefenseStatSurf", "ObeyLv70RockSmash", "SpStatsWaterfall", "AllMonsWillObeyYou"];
        for (int topic = 0; topic < topics.Length; topic++)
        {
            Send(session, new("ack", Choice: topic));
            Assert.IsNull(session.SourceMenu, "topic dialogue must not accept another menu answer");
            Assert.IsNotNull(session.DisplaySourceMenu, "source keeps the suspended window visible");
            Assert.AreEqual("save_event_active", session.ValidatePlayerSave(session.State));
            var transcript = Until(session, () => session.SourceMenu is not null);
            CollectionAssert.Contains(transcript, Prefix + topics[topic]);
            Assert.AreEqual(topic, session.SourceMenu!.DefaultChoice);
            Assert.IsTrue(session.State.Script.Locked);
        }
        Send(session, new("ack", Choice: 8));
        Assert.IsNull(session.State.Script.ListMenu);
        var ending = Until(session, () => session.State.Script.Completed && session.State.Script.Wait is null && session.State.Dialogue is null);
        CollectionAssert.Contains(ending, Prefix + "ComeVisitAnytime");
        Assert.IsFalse(session.State.Script.Locked);
        Assert.AreEqual(before.Inventory, session.State.Inventory);
        Assert.IsTrue(before.Script.Flags.SequenceEqual(session.State.Script.Flags));
        StrictRoundtrip(session);
        Send(session, new("move", Direction: Direction.South)); Finish(session);
    }

    [TestMethod]
    public void BClosesImmediatelyAndNextConversationStartsAtFirstTopic()
    {
        var session = At(Room, 6, 6, Direction.North);
        Open(session);
        Send(session, new("ack", Choice: 6));
        Until(session, () => session.SourceMenu is not null);
        Assert.AreEqual(6, session.SourceMenu!.DefaultChoice);
        Send(session, new("ack", Choice: 127)); Finish(session);
        Assert.IsNull(session.State.Script.ListMenu);
        Assert.IsNull(session.DisplaySourceMenu);
        StrictRoundtrip(session);
        Open(session); Assert.AreEqual(0, session.SourceMenu!.DefaultChoice);
        Send(session, new("ack", Choice: 127)); Finish(session);
    }

    [TestMethod]
    [DataRow(-1)] [DataRow(9)] [DataRow(126)] [DataRow(128)]
    public void OutOfRangeAnswersDoNotChangeConversation(int choice)
    {
        var session = At(Room, 6, 6, Direction.North); Open(session);
        var before = session.State;
        var result = session.Execute("bad-badge-choice", session.Revision, new("ack", Choice: choice));
        Assert.AreEqual(CommandStatus.Rejected, result.Status);
        Assert.AreEqual("event_menu_choice_required", result.Error);
        Assert.AreSame(before, session.State);
    }

    [TestMethod]
    public void OriginalTownDoorAndRearDoorRemainTraversable()
    {
        var session = At("map.cerulean-city", 10, 12, Direction.North);
        Send(session, new("move", Direction: Direction.North)); Finish(session);
        Assert.AreEqual(Room, session.State.World.Player.MapId.Value);
        StrictRoundtrip(session);
        for (int n = 0; n < 3 && session.State.World.Player.MapId.Value == Room; n++)
        { Send(session, new("move", Direction: Direction.South)); Finish(session); }
        Assert.AreEqual("map.cerulean-city", session.State.World.Player.MapId.Value);
        session = At(Room, 3, 2, Direction.North);
        Send(session, new("move", Direction: Direction.North)); Finish(session);
        Assert.AreEqual("map.cerulean-city", session.State.World.Player.MapId.Value);
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void DetachedNativeListContextCannotBeSaved()
    {
        var session = At(Room, 6, 6, Direction.North);
        var invalid = session.State with { Script = session.State.Script with { ListMenu = new("LISTMENU_BADGES", 0, true) } };
        Assert.AreEqual("save_event_active", session.ValidatePlayerSave(invalid));
    }
}
