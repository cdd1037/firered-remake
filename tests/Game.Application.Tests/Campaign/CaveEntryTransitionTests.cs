using System.Reflection;
using System.Text.Json;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>
/// Controlled Route 4 placement; entrance, exit, reentry and recording use public
/// commands. Fresh first-visit placement is not an earned campaign playthrough.
/// The visited fixture retains its acquired flags, party and inventory.
/// </summary>
[TestClass, TestCategory("Game")]
public sealed class CaveEntryTransitionTests : WildBattleFixture
{
    private const string Cave = "map.mt-moon-1f", Outside = "map.route-4";
    private const string Visited = "FLAG_WORLD_MAP_MT_MOON_1F";

    private void SetState(CampaignSession session, CampaignState state) =>
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));

    private CampaignSession AtOutsideDoor(bool firstVisit = false, bool record = true)
    {
        var session = CampaignSession.CreateCurrent(content);
        // Do not erase the checkpoint's acquired visit flag to fake first arrival.
        var basis = firstVisit ? session.State : checkpoint.State;
        Assert.AreEqual(!firstVisit, basis.Script.Flags.GetValueOrDefault(Visited));
        var map = new MapId(Outside);
        var state = basis with
        {
            World = basis.World with { Player = new(map, 19, 6, 3, Direction.North) },
            Script = StoryState.Empty with
            {
                Flags = basis.Script.Flags, Variables = basis.Script.Variables,
                EffectSequence = basis.Script.EffectSequence
            },
            Dialogue = null, ItemMenu = null, MonPicture = null, Motions = [],
            PresentationTicks = 0, PresentationEffects = [], EntryPrograms = [], ArrivalFacing = null,
            IsSurfing = false, IsCycling = false, BicycleSlope = false, MapEdits = null,
            MapLayout = null, FieldTransition = null, FlashLevel = 0, FieldWeather = null,
            PlayerVisible = true, ScreenIsBlack = false, ScreenIsWhite = false,
            Music = content.MapDefinition(map).Music, QuestLog = null
        };
        var enter = typeof(CampaignSession).GetMethod("EnterMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pump = typeof(CampaignSession).GetMethod("Pump", BindingFlags.Instance | BindingFlags.NonPublic)!;
        state = (CampaignState)enter.Invoke(session, [state, null, RoamerTransition.Warp])!;
        state = (CampaignState)pump.Invoke(session, [state])!;
        SetState(session, state);
        Assert.AreEqual(!firstVisit, state.Script.Flags.GetValueOrDefault(Visited));
        Assert.AreEqual(new TilePosition(map, 19, 6, 3, Direction.North), state.World.Player);
        StrictRoundtrip(session);
        if (record)
        {
            Assert.IsFalse(firstVisit, "First-visit setup does not manufacture a party or recorded event.");
            Assert.IsTrue(session.CanReorderFieldParty);
            Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
            Assert.AreEqual(CampaignQuestLogEvent.PartyOrder, session.State.QuestLog!.Scenes[^1].Actions.Single().Event);
            // Let the actual public caption finish before inspecting later captions.
            for (ulong frame = 1; frame <= 256; frame++) session.ObserveQuestLogFieldFrame(frame);
        }
        return session;
    }

    private static CampaignQuestLogAction Marker(CampaignState state) =>
        state.QuestLog!.Scenes[^1].Actions.Last(action => action.Transition is not null);

    private (CampaignState Origin, FieldTransitionReceipt Receipt) Enter(CampaignSession session)
    {
        var origin = session.State;
        Send(session, new("move", Direction.North));
        var arrived = session.State;
        var receipt = arrived.FieldTransition;
        Assert.IsNotNull(receipt);
        Assert.AreEqual(FieldTransitionCause.NonAnimatedDoor, receipt.Cause);
        Assert.AreEqual(FieldTransitionDeparture.StepOntoWarp, receipt.Departure);
        Assert.AreEqual(FieldTransitionArrival.Plain, receipt.Arrival);
        Assert.AreEqual(origin.World.Player, receipt.From);
        Assert.AreEqual(new TilePosition(new(Outside), 19, 5, 3, Direction.North), receipt.DepartureStep);
        Assert.AreEqual(new TilePosition(new(Cave), 18, 37, 3, Direction.North), receipt.To);
        Assert.AreEqual(receipt.To, receipt.ArrivalAnchor);
        Assert.AreEqual(receipt.To, arrived.World.Player);
        Assert.IsTrue(session.CanPresentFieldTransition(origin, receipt));
        Assert.AreEqual(FieldTransitionInterlude.CavePreviewEnter, session.FieldTransitionInterludeFor(receipt));
        Assert.IsTrue(arrived.Script.Flags.GetValueOrDefault(Visited));
        Assert.IsTrue(arrived.Script.Flags.GetValueOrDefault(CampaignQuestLogDepartureCapture.PendingFlag));
        Assert.AreEqual(9, arrived.Script.Variables.GetValueOrDefault(CampaignQuestLogDepartureCapture.EntranceVariable));
        AssertWarpRules(origin, arrived, 1);
        return (origin, receipt);
    }

    private void AssertWarpRules(CampaignState before, CampaignState after, int steps)
    {
        var expected = SourceRoamerRules.Transition(before.Roamer ?? RoamerState.Empty, RoamerTransition.Warp,
            content.CelioRoamerMaps![after.World.Player.MapId], before.World.Rng);
        Assert.AreEqual(before.World.StepCount + steps, after.World.StepCount);
        Assert.AreEqual(before.World.Revision + 1, after.World.Revision);
        Assert.AreEqual(expected.Rng, after.World.Rng, "Only the accepted map-entry roamer transition consumes RNG.");
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.State), JsonSerializer.SerializeToElement(after.Roamer)));
        Assert.AreEqual(JsonSerializer.Serialize(before.Party), JsonSerializer.Serialize(after.Party));
        Assert.AreEqual(JsonSerializer.Serialize(before.Inventory), JsonSerializer.Serialize(after.Inventory));
        Assert.IsTrue(after.Script.Completed);
        Assert.IsFalse(after.Script.Locked);
        Assert.IsNull(after.Script.Wait);
        Assert.IsNull(after.WildBattle);
        Assert.IsTrue(after.EntryPrograms.IsEmpty);
        Assert.IsTrue(after.Motions.IsEmpty);
    }

    private static int ObservePreviewWrapper(bool visited, int heldAfter, int loadingWait = 0)
    {
        // Pixel/palette traces belong to the independent source-reference tests.
        // Here the real shared controller supplies the actual completion receipt.
        var preview = new FieldCaveTransitionPresentation.CavePreview(new ushort[16],
            Enumerable.Repeat((ushort)0x7fff, 16).ToArray(), new ushort[16], visited);
        for (int callback = 0; callback < 512 && !preview.Complete; callback++)
        {
            bool held = heldAfter == 0 || heldAfter > 0 && preview.Sample.HoldFrames >= heldAfter;
            preview.Advance(callback >= loadingWait, true, held);
        }
        Assert.IsTrue(preview.Complete, "The bounded callback controller must actually complete.");
        Assert.AreEqual(heldAfter < 0 ? (visited ? 41 : 121) : heldAfter + 1, preview.Sample.HoldFrames);
        return preview.Frames - (FieldCaveTransitionPresentation.EnterFrames - 1);
    }

    [TestMethod]
    public void FirstVisitReadsTheAuthenticatedPreEntryFlagAndPublicReentryUsesForty()
    {
        var session = AtOutsideDoor(firstVisit: true, record: false);
        var (origin, receipt) = Enter(session);
        Assert.IsFalse(origin.Script.Flags.GetValueOrDefault(Visited));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(Visited));
        var state = session.State;
        string unchanged = JsonSerializer.Serialize(state);
        long revision = session.Revision;
        Assert.AreEqual(120, session.FieldTransitionPreviewDuration(origin, receipt));
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(state, receipt));
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(origin with { }, receipt));
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(origin, receipt with { }));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsCycling = true }, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsSurfing = true }, receipt));
        int firstWrapper = ObservePreviewWrapper(false, -1);
        int firstSwitch = FieldDoorPresentation.MinimumStepDepartureFrames + firstWrapper + 27;
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, firstSwitch,
            firstSwitch + FieldWarpPresentation.PlainArrivalFrames, firstWrapper),
            "A live entry without a recording cannot manufacture Quest timing.");
        Assert.AreSame(state, session.State);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.AreEqual(revision, session.Revision);
        StrictRoundtrip(session);

        Send(session, new("turn", Direction.South));
        var beforeExit = session.State;
        Send(session, new("move", Direction.South));
        AssertWarpRules(beforeExit, session.State, 0);
        var (secondOrigin, secondReceipt) = Enter(session);
        Assert.IsTrue(secondOrigin.Script.Flags.GetValueOrDefault(Visited));
        Assert.AreEqual(40, session.FieldTransitionPreviewDuration(secondOrigin, secondReceipt));
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(origin, receipt), "The old owner is stale after reentry.");
        Assert.AreEqual(origin.World.StepCount + 2, session.State.World.StepCount);
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(-1, 0, 0)]
    [DataRow(0, 0, 0)]
    [DataRow(7, 9, 5)]
    public void PublicVisitedEntryKeepsLiveEvidenceButReplayOmitsPreviewWait(int heldAfter, int musicWait, int loadingWait)
    {
        var session = AtOutsideDoor();
        var (origin, receipt) = Enter(session);
        Assert.AreEqual(40, session.FieldTransitionPreviewDuration(origin, receipt));
        var state = session.State;
        string unchanged = JsonSerializer.Serialize(state);
        long revision = session.Revision;
        var marker = Marker(state);
        Assert.IsNull(marker.TransitionTiming);
        var unknown = new CampaignQuestLogPlayback(new([state.QuestLog!.Scenes[^1]]));
        unknown.Tick(marker.StartFrame / 60d);
        Assert.IsNull(unknown.ActiveTransitionTiming);
        Assert.AreEqual(Cave, unknown.Map, "Unobserved and older records remain immediate unknown transitions.");

        int wrapper = ObservePreviewWrapper(true, heldAfter, loadingWait);
        int departure = FieldDoorPresentation.MinimumStepDepartureFrames + musicWait;
        int change = departure + wrapper + FieldCaveTransitionPresentation.EnterFrames - 1;
        int end = change + FieldWarpPresentation.PlainArrivalFrames;
        Assert.IsTrue(end <= 255);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt with { }, change, end, wrapper));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end, -1));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end - 1, wrapper));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end, wrapper));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end, wrapper));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change + 1, end + 1, wrapper));
        var saved = session.PrepareFieldSave();
        var recorded = Marker(saved);
        var timing = recorded.TransitionTiming;
        Assert.IsNotNull(timing);
        Assert.AreEqual(FieldTransitionInterlude.CavePreviewEnter, timing.Interlude);
        Assert.AreEqual(wrapper, timing.PreviewFrames);
        Assert.AreEqual(wrapper - 1, timing.PreviewOmittedFrames);
        Assert.AreEqual(wrapper + 27, timing.InterludeFrames);
        Assert.AreEqual(departure, timing.DepartureEndFrame);
        Assert.AreEqual(new CampaignQuestLogDoorAnchor(19, 5), timing.DepartureDoor);
        Assert.IsNull(timing.ArrivalDoor);
        Assert.AreEqual(marker with { Frames = end, TransitionTiming = timing }, recorded);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog, content.QuestLogSource, content));
        Assert.IsNull(session.ValidatePlayerSave(saved));
        Assert.AreEqual(JsonSerializer.Serialize(state with { QuestLog = saved.QuestLog }), JsonSerializer.Serialize(saved));
        Assert.AreEqual(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(session.PrepareFieldSave()));
        Assert.AreEqual(1, CampaignQuestLogRecorder.Budget([recorded]) -
            CampaignQuestLogRecorder.Budget([recorded with { TransitionTiming = timing with { PreviewFrames = 0 } }]));
        string json = JsonSerializer.Serialize(timing);
        Assert.IsTrue(json.Contains("\"PreviewFrames\":", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("PreviewOmittedFrames", StringComparison.Ordinal));

        var bytes = SaveCodec.Encode(saved, revision, session.RulesVersion, content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        Assert.IsTrue(System.Text.Json.Nodes.JsonNode.DeepEquals(JsonSerializer.SerializeToNode(saved),
            JsonSerializer.SerializeToNode(restored.State)), "Strict Continue preserves the complete persisted payload.");
        Assert.IsFalse(restored.CompleteFieldTransitionPresentation(receipt, change, end, wrapper));
        int replaySwitch = departure + 28, replayEnd = replaySwitch + FieldWarpPresentation.PlainArrivalFrames;
        var normalized = timing with { PreviewFrames = 0, SwitchFrame = replaySwitch, EndFrame = replayEnd };
        var playback = new CampaignQuestLogPlayback(new([saved.QuestLog!.Scenes[^1]]), state.PlayerName, content.QuestLogSource);
        playback.Tick(marker.StartFrame / 60d);
        for (int frame = 0; frame < replayEnd; frame++)
        {
            Assert.AreEqual(normalized, playback.ActiveTransitionTiming);
            Assert.AreEqual(28, playback.ActiveTransitionTiming!.InterludeFrames);
            Assert.AreEqual(frame, playback.TransitionElapsedFrames, .00001);
            Assert.AreEqual(frame < replaySwitch ? Outside : Cave, playback.Map);
            Assert.IsNull(playback.Caption);
            playback.Tick(1d / 60);
        }
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.IsTrue(playback.Finished, "No preview-duration idle tail remains in the replay.");
        var skip = new CampaignQuestLogPlayback(saved.QuestLog!, state.PlayerName, content.QuestLogSource);
        skip.Tick(marker.StartFrame / 60d);
        skip.Skip();
        Assert.IsTrue(skip.Finished);
        Assert.AreSame(state, session.State);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(json, JsonSerializer.Serialize(recorded.TransitionTiming));
        Assert.IsNull(marker.TransitionTiming);
    }

    [TestMethod]
    public void RejectedCommandDoesNotPublishTimingAndLaterMovementAndCaptionShareTheShortenedClock()
    {
        var session = AtOutsideDoor();
        var (_, receipt) = Enter(session);
        var state = session.State;
        int wrapper = ObservePreviewWrapper(true, -1);
        int change = FieldDoorPresentation.MinimumStepDepartureFrames + wrapper + 27;
        int end = change + FieldWarpPresentation.PlainArrivalFrames;
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end, wrapper));
        var rejected = session.Execute("bad-preview-command", session.Revision, new("not_a_command"));
        Assert.AreEqual(CommandStatus.Rejected, rejected.Status);
        Assert.AreSame(state, session.State);
        Assert.IsNull(Marker(state).TransitionTiming);
        Send(session, new("turn", Direction.South));
        var turn = session.State.QuestLog!.Scenes[^1].Actions.Last(a => a.Event == CampaignQuestLogEvent.None && a.Transition is null);
        Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
        var scene = session.State.QuestLog!.Scenes[^1];
        var caption = scene.Actions.Last(a => a.Event == CampaignQuestLogEvent.PartyOrder);
        Assert.IsNotNull(Marker(session.State).TransitionTiming);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end, wrapper));
        string logBefore = JsonSerializer.Serialize(session.State.QuestLog);
        int omitted = wrapper - 1;
        var playback = new CampaignQuestLogPlayback(new([scene]), state.PlayerName, content.QuestLogSource);
        playback.Tick((turn.StartFrame - omitted) / 60d);
        Assert.AreEqual(Cave, playback.Map);
        Assert.AreEqual(Direction.South, playback.Actors.Single(a => a.Id == CampaignQuestLogRecorder.PlayerId).Facing);
        Assert.IsNull(playback.ActiveTransitionTiming);
        playback.Tick((caption.StartFrame - turn.StartFrame) / 60d);
        Assert.AreEqual(content.QuestLogSource.Expand(caption.TextKey!, state.PlayerName, caption.Arguments), playback.Caption);
        playback.Tick(10);
        Assert.IsTrue(playback.Finished);
        Assert.AreEqual(logBefore, JsonSerializer.Serialize(session.State.QuestLog));
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void StaleOrReplacedSceneCannotAcquirePreviewTiming(bool replaceScene)
    {
        var session = AtOutsideDoor();
        var (_, receipt) = Enter(session);
        if (replaceScene)
        {
            var state = session.State;
            var log = state.QuestLog!;
            SetState(session, state with { QuestLog = new(log.Scenes.SetItem(log.Scenes.Length - 1, log.Scenes[^1] with { })) });
        }
        else Send(session, new("turn", Direction.South));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, 127, 151, 60));
        Assert.IsNull(Marker(session.State).TransitionTiming);
    }

    [TestMethod]
    public void PreviewOverflowClosesTheUnchangedUnknownMarkerWithoutTruncation()
    {
        var session = AtOutsideDoor();
        var (_, receipt) = Enter(session);
        var state = session.State;
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, 250, 274, 60));
        var saved = session.PrepareFieldSave();
        Assert.AreEqual(JsonSerializer.Serialize(CampaignQuestLogRecorder.Close(state.QuestLog!)), JsonSerializer.Serialize(saved.QuestLog));
        Assert.AreSame(Marker(state), Marker(saved));
        Assert.IsNull(Marker(saved).TransitionTiming);
        Assert.AreEqual(state.World, saved.World);
        Assert.AreSame(state, session.State);
        Assert.IsNull(session.ValidatePlayerSave(saved));
    }

    [TestMethod]
    public void PersistedPreviewMetadataRejectsMissingForeignAndOverflowedDurations()
    {
        var session = AtOutsideDoor();
        var (_, receipt) = Enter(session);
        int wrapper = ObservePreviewWrapper(true, -1);
        int change = FieldDoorPresentation.MinimumStepDepartureFrames + wrapper + 27;
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change,
            change + FieldWarpPresentation.PlainArrivalFrames, wrapper));
        var saved = session.PrepareFieldSave();
        var scene = saved.QuestLog!.Scenes[^1];
        var marker = Marker(saved);
        var timing = marker.TransitionTiming!;
        int index = scene.Actions.IndexOf(marker);
        foreach (var malformed in new[]
        {
            timing with { PreviewFrames = 0 },
            timing with { PreviewFrames = 29 },
            timing with { PreviewFrames = -1 },
            timing with { PreviewFrames = int.MaxValue },
            timing with { PreviewFrames = 256 },
            timing with { Interlude = FieldTransitionInterlude.None },
            timing with { Interlude = FieldTransitionInterlude.CaveEnter },
            timing with { Interlude = FieldTransitionInterlude.CaveExit },
            timing with { DepartureDoor = null },
            timing with { ArrivalDoor = new(18, 37) },
            timing with { SwitchFrame = change - 1 },
            timing with { EndFrame = timing.EndFrame + 1 }
        })
        {
            var invalidScene = scene with
            {
                Actions = scene.Actions.SetItem(index, marker with { TransitionTiming = malformed })
            };
            var invalid = saved.QuestLog with
            {
                Scenes = saved.QuestLog.Scenes.SetItem(saved.QuestLog.Scenes.Length - 1, invalidScene)
            };
            Assert.AreEqual("save_quest_log_transition_timing_invalid",
                CampaignQuestLogRecorder.Validate(invalid, content.QuestLogSource, content), JsonSerializer.Serialize(malformed));
        }
    }

    [TestMethod]
    public void PublicLavenderTowerEntryUsesANonAnimatedDoorAndRecapHasNoGenericCaveTail()
    {
        const string towerFlag = "FLAG_WORLD_MAP_POKEMON_TOWER_1F";
        var lavender = new MapId("map.lavender-town");
        var tower = new MapId("map.pokemon-tower-1f");
        var session = AtOutsideDoor(record: false);
        var placed = session.State with
        {
            World = session.State.World with { Player = new(lavender, 18, 7, 3, Direction.North) },
            Music = content.MapDefinition(lavender).Music
        };
        var enterMap = typeof(CampaignSession).GetMethod("EnterMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pump = typeof(CampaignSession).GetMethod("Pump", BindingFlags.Instance | BindingFlags.NonPublic)!;
        placed = (CampaignState)enterMap.Invoke(session, [placed, null, RoamerTransition.Warp])!;
        placed = (CampaignState)pump.Invoke(session, [placed])!;
        SetState(session, placed);
        StrictRoundtrip(session);
        Assert.AreEqual(new FieldWarp(0, 18, 6, 0, tower, 1), content.Maps[lavender].FieldWarps[0]);
        Assert.AreEqual(new FieldWarp(1, 11, 18, 3, lavender, 0), content.Maps[tower].FieldWarps[1]);
        Assert.AreEqual(new FieldMetatileEdit(18, 6, 0x30f, 0, 3), content.SourceMetatileAt(lavender, 18, 6));
        Assert.AreEqual(0x60, content.Maps[lavender].SourceBehaviorAt(18, 6));
        Assert.AreEqual(0x65, content.Maps[tower].SourceBehaviorAt(11, 18));
        Assert.IsNull(GeneratedFieldDoorArt.Load(Path.Combine(TestAssets.Root, "content/generated"))
            .Find("LAYOUT_LAVENDER_TOWN", 0x30f), "The Tower entrance is a non-animated cave door.");
        Assert.AreEqual("MAP_TYPE_TOWN", content.MapDefinition(lavender).MapType);
        Assert.AreEqual("MAP_TYPE_INDOOR", content.MapDefinition(tower).MapType);
        Assert.AreEqual("WEATHER_SUNNY", content.MapDefinition(lavender).Weather);
        Assert.AreEqual("WEATHER_NONE", content.MapDefinition(tower).Weather);
        Assert.IsFalse(content.MapDefinition(tower).RequiresFlash);
        Assert.IsTrue(content.MapDefinition(tower).MapPreview?.Type==MapPreviewType.Cave);
        Assert.AreEqual(towerFlag, content.MapDefinition(tower).MapPreview?.VisitFlag);
        Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
        for (ulong frame = 1; frame <= 256; frame++) session.ObserveQuestLogFieldFrame(frame);
        var origin = session.State;
        bool visited = origin.Script.Flags.GetValueOrDefault(towerFlag);
        Send(session, new("move", Direction.North));
        var state = session.State;
        var receipt = state.FieldTransition;
        Assert.IsNotNull(receipt);
        Assert.AreEqual(FieldTransitionCause.NonAnimatedDoor, receipt.Cause);
        Assert.AreEqual(FieldTransitionDeparture.StepOntoWarp, receipt.Departure);
        Assert.AreEqual(FieldTransitionArrival.Plain, receipt.Arrival);
        Assert.AreEqual(new TilePosition(lavender, 18, 6, 3, Direction.North), receipt.DepartureStep);
        Assert.AreEqual(new TilePosition(tower, 11, 18, 3, Direction.North), receipt.To);
        Assert.AreEqual(receipt.To, receipt.ArrivalAnchor);
        AssertWarpRules(origin, state, 1);
        Assert.IsTrue(state.Script.Flags.GetValueOrDefault(towerFlag));
        Assert.AreEqual(visited, origin.Script.Flags.GetValueOrDefault(towerFlag));
        Assert.IsTrue(session.CanPresentFieldTransition(origin, receipt));
        Assert.AreEqual(visited ? 40 : 120, session.FieldTransitionPreviewDuration(origin, receipt));
        Assert.AreEqual(FieldTransitionInterlude.RegionPreview, session.FieldTransitionInterludeFor(receipt));
        string unchanged = JsonSerializer.Serialize(state);
        long revision = session.Revision;
        int wrapper = ObservePreviewWrapper(visited, -1);
        int departure = FieldDoorPresentation.MinimumStepDepartureFrames;
        int change = departure + wrapper + 27;
        int end = change + FieldWarpPresentation.PlainArrivalFrames;
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end, wrapper));
        var saved = session.PrepareFieldSave();
        var marker = Marker(saved);
        var timing = marker.TransitionTiming!;
        Assert.AreEqual(FieldTransitionInterlude.RegionPreview, timing.Interlude);
        Assert.AreEqual(wrapper + 27, timing.InterludeFrames);
        Assert.AreEqual(wrapper + 27, timing.PreviewOmittedFrames);
        Assert.AreEqual(departure, timing.DepartureEndFrame);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog, content.QuestLogSource, content));
        Assert.IsNull(session.ValidatePlayerSave(saved));

        var playback = new CampaignQuestLogPlayback(new([saved.QuestLog!.Scenes[^1]]), state.PlayerName, content.QuestLogSource);
        playback.Tick(marker.StartFrame / 60d);
        int replayEnd = departure + FieldWarpPresentation.PlainArrivalFrames;
        for (int frame = 0; frame < replayEnd; frame++)
        {
            var active = playback.ActiveTransitionTiming;
            Assert.IsNotNull(active);
            Assert.AreEqual(FieldTransitionInterlude.None, active.Interlude);
            Assert.AreEqual(0, active.PreviewFrames);
            Assert.AreEqual(0, active.InterludeFrames, "TOWN to INDOOR has no generic cave transition table row.");
            Assert.AreEqual(departure, active.SwitchFrame);
            Assert.AreEqual(replayEnd, active.EndFrame);
            Assert.AreEqual(frame < departure ? lavender.Value : tower.Value, playback.Map);
            playback.Tick(1d / 60);
        }
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.IsTrue(playback.Finished, "Quest suppression must not leave an extra 28-frame cave tail.");
        Assert.AreSame(state, session.State);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(wrapper, marker.TransitionTiming!.PreviewFrames, "Replay cannot rewrite persisted live evidence.");
        StrictRoundtrip(session);
    }
}
