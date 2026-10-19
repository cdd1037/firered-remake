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
/// Controlled south-gateway placement, followed by public enter/exit/reentry and
/// party-order recording. Fresh first visit does not manufacture earned state.
/// The visited branch retains the maintained checkpoint's acquired flag and party.
/// These cases establish Application behavior, not a natural campaign playthrough.
/// </summary>
[TestClass, TestCategory("Game")]
public sealed class ForestEntryTransitionTests : WildBattleFixture
{
    private const string Forest = "map.viridian-forest", Gate = "map.route-2-forest-south-gate";
    private const string Visited = "FLAG_WORLD_MAP_VIRIDIAN_FOREST";
    private const int OrdinaryShadeArrivalFrames = 18;

    private void SetState(CampaignSession session, CampaignState state) =>
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));

    private CampaignSession AtSouthGate(bool firstVisit = false, bool record = true)
    {
        var session = CampaignSession.CreateCurrent(content);
        // No checkpoint flag is erased to manufacture first visit.
        var basis = firstVisit ? session.State : checkpoint.State;
        Assert.AreEqual(!firstVisit, basis.Script.Flags.GetValueOrDefault(Visited));
        var map = new MapId(Gate);
        var state = basis with
        {
            World = basis.World with { Player = new(map, 7, 2, 3, Direction.North) },
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
        Assert.AreEqual(new TilePosition(map, 7, 2, 3, Direction.North), state.World.Player);
        Assert.AreEqual(JsonSerializer.Serialize(basis.Party), JsonSerializer.Serialize(state.Party));
        Assert.AreEqual(JsonSerializer.Serialize(basis.Inventory), JsonSerializer.Serialize(state.Inventory));
        StrictRoundtrip(session);
        if (record)
        {
            Assert.IsFalse(firstVisit, "A fresh no-party placement cannot manufacture a party recording.");
            Assert.IsTrue(session.CanReorderFieldParty);
            Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
            Assert.AreEqual(CampaignQuestLogEvent.PartyOrder, session.State.QuestLog!.Scenes[^1].Actions.Single().Event);
            for (ulong frame = 1; frame <= 256; frame++) session.ObserveQuestLogFieldFrame(frame);
        }
        return session;
    }

    private static CampaignQuestLogAction Marker(CampaignState state) =>
        state.QuestLog!.Scenes[^1].Actions.Last(action => action.Transition is not null);

    private void AssertRouteMetadata()
    {
        var gate = new MapId(Gate);
        var forest = new MapId(Forest);
        Assert.AreEqual(new FieldWarp(3, 7, 1, 3, forest, 0), content.Maps[gate].FieldWarps[3]);
        Assert.AreEqual(new FieldWarp(0, 29, 62, 3, gate, 3), content.Maps[forest].FieldWarps[0]);
        Assert.AreEqual(0x60, content.Maps[gate].SourceBehaviorAt(7, 1));
        Assert.AreEqual(0x65, content.Maps[forest].SourceBehaviorAt(29, 62));
        Assert.AreEqual("MAP_TYPE_INDOOR", content.MapDefinition(gate).MapType);
        Assert.AreEqual("WEATHER_NONE", content.MapDefinition(gate).Weather);
        Assert.AreEqual("MAP_TYPE_ROUTE", content.MapDefinition(forest).MapType);
        Assert.AreEqual("WEATHER_SHADE", content.MapDefinition(forest).Weather);
        Assert.AreEqual("ViridianForest_OnTransition", content.MapDefinition(forest).Programs.OnTransition);
        Assert.IsNull(content.MapDefinition(gate).MapPreview);
        var preview = content.MapDefinition(forest).MapPreview;
        Assert.IsNotNull(preview);
        Assert.AreEqual("Forest", preview.Type.ToString());
        Assert.AreEqual(Visited, preview.VisitFlag);
    }

    private (CampaignState Origin, FieldTransitionReceipt Receipt) Enter(CampaignSession session)
    {
        AssertRouteMetadata();
        var origin = session.State;
        Send(session, new("move", Direction.North));
        var arrived = session.State;
        var receipt = arrived.FieldTransition;
        Assert.IsNotNull(receipt);
        Assert.AreEqual(FieldTransitionCause.NonAnimatedDoor, receipt.Cause);
        Assert.AreEqual(FieldTransitionDeparture.StepOntoWarp, receipt.Departure);
        Assert.AreEqual(FieldTransitionArrival.Plain, receipt.Arrival);
        Assert.AreEqual(origin.World.Player, receipt.From);
        Assert.AreEqual(new TilePosition(new(Gate), 7, 1, 3, Direction.North), receipt.DepartureStep);
        Assert.AreEqual(new TilePosition(new(Forest), 29, 62, 3, Direction.North), receipt.To);
        Assert.AreEqual(receipt.To, receipt.ArrivalAnchor);
        Assert.AreEqual(receipt.To, arrived.World.Player);
        Assert.IsTrue(session.CanPresentFieldTransition(origin, receipt));
        Assert.AreEqual(FieldTransitionInterlude.ForestPreview, session.FieldTransitionInterludeFor(receipt));
        Assert.IsTrue(arrived.Script.Flags.GetValueOrDefault(Visited));
        AssertWarpRules(origin, arrived, 1);
        return (origin, receipt);
    }

    private (CampaignState Origin, FieldTransitionReceipt Receipt) Exit(CampaignSession session)
    {
        Send(session, new("turn", Direction.South));
        var origin = session.State;
        Send(session, new("move", Direction.South));
        var arrived = session.State;
        var receipt = arrived.FieldTransition;
        Assert.IsNotNull(receipt);
        Assert.AreEqual(FieldTransitionCause.PlainWarp, receipt.Cause);
        Assert.AreEqual(FieldTransitionDeparture.Immediate, receipt.Departure);
        Assert.AreEqual(FieldTransitionArrival.NonAnimatedDoor, receipt.Arrival);
        Assert.IsNull(receipt.DepartureStep);
        Assert.AreEqual(new TilePosition(new(Forest), 29, 62, 3, Direction.South), receipt.From);
        Assert.AreEqual(new TilePosition(new(Gate), 7, 2, 3, Direction.South), receipt.To);
        Assert.AreEqual(new TilePosition(new(Gate), 7, 1, 3, Direction.South), receipt.ArrivalAnchor);
        Assert.IsTrue(session.CanPresentFieldTransition(origin, receipt));
        Assert.AreEqual(FieldTransitionInterlude.None, session.FieldTransitionInterludeFor(receipt));
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(origin, receipt));
        Assert.IsTrue(arrived.Script.Flags.GetValueOrDefault(Visited));
        AssertWarpRules(origin, arrived, 0);
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

    private static int ObserveForestArrival(bool visited, int loadingWait = 0)
    {
        var preview = new FieldForestPreviewPresentation.ForestArrival(visited ? 40 : 120, FieldArrivalPalette.Shade);
        for (int callback = 0; callback < 512 && !preview.Complete; callback++)
            preview.Advance(graphicsReady: callback >= loadingWait);
        Assert.IsTrue(preview.Complete, "The real source-derived arrival host must actually complete.");
        Assert.IsTrue(preview.ExtraWaitFrames > 0);
        Assert.AreEqual(preview.Frames - OrdinaryShadeArrivalFrames, preview.ExtraWaitFrames);
        return preview.Frames;
    }

    [TestMethod]
    public void FirstVisitReadsAuthenticPreEntryFlagAndPublicExitReentryUsesForty()
    {
        var session = AtSouthGate(firstVisit: true, record: false);
        var (origin, receipt) = Enter(session);
        Assert.IsFalse(origin.Script.Flags.GetValueOrDefault(Visited));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(Visited));
        Assert.AreEqual(120, session.FieldTransitionPreviewDuration(origin, receipt));
        var state = session.State;
        string unchanged = JsonSerializer.Serialize(state);
        long revision = session.Revision;
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(state, receipt));
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(origin with { }, receipt));
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(origin, receipt with { }));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsCycling = true }, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsSurfing = true }, receipt));
        int arrival = ObserveForestArrival(false);
        int change = FieldDoorPresentation.MinimumStepDepartureFrames;
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, change + arrival,
            arrival - OrdinaryShadeArrivalFrames), "An unrecorded transition cannot manufacture a Quest observation.");
        Assert.AreSame(state, session.State);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.AreEqual(revision, session.Revision);
        StrictRoundtrip(session);
        Exit(session);
        Send(session, new("turn", Direction.North));
        var (nextOrigin, nextReceipt) = Enter(session);
        Assert.IsTrue(nextOrigin.Script.Flags.GetValueOrDefault(Visited));
        Assert.AreEqual(40, session.FieldTransitionPreviewDuration(nextOrigin, nextReceipt));
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(origin, receipt));
        Assert.AreEqual(origin.World.StepCount + 2, session.State.World.StepCount);
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(9, 5)]
    public void PublicVisitedEntryRecordsArrivalDelayAndRecapKeepsTheActualSwitch(int musicWait, int loadingWait)
    {
        var session = AtSouthGate();
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
        Assert.AreEqual(Forest, unknown.Map, "Older and unobserved records remain immediate unknown transitions.");

        int arrival = ObserveForestArrival(true, loadingWait);
        int extra = arrival - OrdinaryShadeArrivalFrames;
        int change = FieldDoorPresentation.MinimumStepDepartureFrames + musicWait;
        int end = change + arrival;
        Assert.IsTrue(extra > 0 && end <= 255);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt with { }, change, end, extra));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end, -1));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end - 1, extra));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end, extra));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end, extra));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change + 1, end + 1, extra));
        var saved = session.PrepareFieldSave();
        var recorded = Marker(saved);
        var timing = recorded.TransitionTiming;
        Assert.IsNotNull(timing);
        Assert.AreEqual(FieldTransitionInterlude.ForestPreview, timing.Interlude);
        Assert.AreEqual("Shade", timing.ArrivalPalette.ToString());
        Assert.AreEqual(extra, timing.PreviewFrames);
        Assert.AreEqual(extra, timing.PreviewOmittedFrames);
        Assert.AreEqual(0, timing.InterludeFrames, "Forest art overlays the destination and has no pre-switch interlude.");
        Assert.AreEqual(change, timing.DepartureEndFrame);
        Assert.AreEqual(new CampaignQuestLogDoorAnchor(7, 1), timing.DepartureDoor);
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
            JsonSerializer.SerializeToNode(restored.State)), "Strict Continue preserves the complete recorded payload.");
        Assert.IsFalse(restored.CompleteFieldTransitionPresentation(receipt, change, end, extra));
        int replayEnd = change + OrdinaryShadeArrivalFrames;
        var normalized = timing with { Interlude = FieldTransitionInterlude.None, PreviewFrames = 0, EndFrame = replayEnd };
        var playback = new CampaignQuestLogPlayback(new([saved.QuestLog!.Scenes[^1]]), state.PlayerName, content.QuestLogSource);
        playback.Tick(marker.StartFrame / 60d);
        for (int frame = 0; frame < replayEnd; frame++)
        {
            Assert.AreEqual(normalized, playback.ActiveTransitionTiming);
            Assert.AreEqual(change, playback.ActiveTransitionTiming!.SwitchFrame);
            Assert.AreEqual(frame, playback.TransitionElapsedFrames, .00001);
            Assert.AreEqual(frame < change ? Gate : Forest, playback.Map);
            Assert.IsNull(playback.Caption);
            playback.Tick(1d / 60);
        }
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.IsTrue(playback.Finished, "Quest suppression removes the overlay's arrival tail only.");
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
    public void PublicExitRecordsOrdinaryArrivalAndAuthenticDepartureCaptionThenReenters()
    {
        var session = AtSouthGate();
        var (_, entry) = Enter(session);
        int arrival = ObserveForestArrival(true);
        int extra = arrival - OrdinaryShadeArrivalFrames;
        int enterSwitch = FieldDoorPresentation.MinimumStepDepartureFrames;
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(entry, enterSwitch, enterSwitch + arrival, extra));
        var (_, exit) = Exit(session);
        var state = session.State;
        var marker = Marker(state);
        var departureCaption = state.QuestLog!.Scenes[^1].Actions[^1];
        Assert.AreEqual(CampaignQuestLogEvent.Departed, departureCaption.Event);
        Assert.AreEqual(5, departureCaption.LocationId);
        Assert.AreEqual("ManagedToGetOutOfLocation", departureCaption.TextKey);
        Assert.IsFalse(state.Script.Flags.GetValueOrDefault(CampaignQuestLogDepartureCapture.PendingFlag));
        int change = FieldWarpPresentation.MinimumDepartureFrames;
        int end = change + FieldDoorPresentation.NonAnimatedArrivalFrames;
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(exit, change, end + extra, extra),
            "Source forest metadata does not admit another preview when returning to the gate.");
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(exit, change, end));
        var saved = session.PrepareFieldSave();
        var recorded = Marker(saved);
        var timing = recorded.TransitionTiming;
        Assert.IsNotNull(timing);
        Assert.AreEqual(FieldTransitionInterlude.None, timing.Interlude);
        Assert.AreEqual(FieldArrivalPalette.Normal, timing.ArrivalPalette);
        Assert.AreEqual(0, timing.PreviewFrames);
        Assert.AreEqual(change, timing.SwitchFrame);
        Assert.AreEqual(end, timing.EndFrame);
        Assert.IsNull(timing.DepartureDoor);
        Assert.AreEqual(new CampaignQuestLogDoorAnchor(7, 1), timing.ArrivalDoor);
        var caption = saved.QuestLog!.Scenes[^1].Actions[^1];
        Assert.AreEqual(departureCaption with { StartFrame = marker.StartFrame + end }, caption);
        Assert.IsNull(session.ValidatePlayerSave(saved));
        Assert.AreSame(state, session.State);
        var playback = new CampaignQuestLogPlayback(new([saved.QuestLog.Scenes[^1]]), state.PlayerName, content.QuestLogSource);
        playback.Tick((caption.StartFrame - extra) / 60d);
        Assert.AreEqual(Gate, playback.Map);
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.AreEqual(content.QuestLogSource.Expand(caption.TextKey!, state.PlayerName, caption.Arguments), playback.Caption);
        Send(session, new("turn", Direction.North));
        var (reentryOrigin, reentry) = Enter(session);
        Assert.AreEqual(40, session.FieldTransitionPreviewDuration(reentryOrigin, reentry));
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void RejectedCommandCannotPublishTimingAndLaterActionsUseTheShortenedArrivalClock()
    {
        var session = AtSouthGate();
        var (_, receipt) = Enter(session);
        var state = session.State;
        int arrival = ObserveForestArrival(true);
        int extra = arrival - OrdinaryShadeArrivalFrames;
        int change = FieldDoorPresentation.MinimumStepDepartureFrames;
        int end = change + arrival;
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end, extra));
        var rejected = session.Execute("invalid-forest-observation", session.Revision, new("not_a_command"));
        Assert.AreEqual(CommandStatus.Rejected, rejected.Status);
        Assert.AreSame(state, session.State);
        Assert.IsNull(Marker(state).TransitionTiming);
        Send(session, new("turn", Direction.South));
        var turn = session.State.QuestLog!.Scenes[^1].Actions.Last(a => a.Event == CampaignQuestLogEvent.None && a.Transition is null);
        Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
        var scene = session.State.QuestLog!.Scenes[^1];
        var caption = scene.Actions.Last(a => a.Event == CampaignQuestLogEvent.PartyOrder);
        Assert.IsNotNull(Marker(session.State).TransitionTiming);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end, extra));
        string logBefore = JsonSerializer.Serialize(session.State.QuestLog);
        var playback = new CampaignQuestLogPlayback(new([scene]), state.PlayerName, content.QuestLogSource);
        playback.Tick((turn.StartFrame - extra) / 60d);
        Assert.AreEqual(Forest, playback.Map);
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
    public void PlayTimeCommitDuringOverlayPreservesOwnershipAndPublishesTimingOnlyOnce()
    {
        var session = AtSouthGate();
        var (origin, receipt) = Enter(session);
        var arrived = session.State;
        Assert.AreEqual(40, session.FieldTransitionPreviewDuration(origin, receipt));
        Send(session, new("play_time_tick", Choice: 60));
        Assert.IsNull(session.State.FieldTransition);
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(origin, receipt));
        Assert.AreEqual(arrived.World, session.State.World);
        Assert.AreEqual(JsonSerializer.Serialize(arrived.QuestLog), JsonSerializer.Serialize(session.State.QuestLog));
        Assert.IsNull(Marker(session.State).TransitionTiming);
        int arrival = ObserveForestArrival(true);
        int change = FieldDoorPresentation.MinimumStepDepartureFrames;
        int end = change + arrival;
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end, arrival - OrdinaryShadeArrivalFrames));
        var saved = session.PrepareFieldSave();
        string expectedLog = JsonSerializer.Serialize(saved.QuestLog);
        Send(session, new("play_time_tick", Choice: 1));
        Assert.AreEqual(expectedLog, JsonSerializer.Serialize(session.State.QuestLog));
        Assert.AreEqual(arrived.World, session.State.World);
        Send(session, new("play_time_tick", Choice: 1));
        Assert.AreEqual(expectedLog, JsonSerializer.Serialize(session.State.QuestLog));
        Assert.AreEqual(end, Marker(session.State).Frames);
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ForeignStaleOrReplacedOwnerCannotAcquireForestTiming(bool replaceScene)
    {
        var session = AtSouthGate();
        var (_, receipt) = Enter(session);
        var other = AtSouthGate();
        var (_, otherReceipt) = Enter(other);
        int arrival = ObserveForestArrival(true);
        int extra = arrival - OrdinaryShadeArrivalFrames;
        int change = FieldDoorPresentation.MinimumStepDepartureFrames;
        int end = change + arrival;
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(otherReceipt, change, end, extra));
        Assert.IsFalse(other.CompleteFieldTransitionPresentation(receipt, change, end, extra));
        if (replaceScene)
        {
            var state = session.State;
            var log = state.QuestLog!;
            SetState(session, state with { QuestLog = new(log.Scenes.SetItem(log.Scenes.Length - 1, log.Scenes[^1] with { })) });
        }
        else Send(session, new("turn", Direction.South));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end, extra));
        Assert.IsNull(Marker(session.State).TransitionTiming);
        Assert.IsNull(Marker(other.State).TransitionTiming);
    }

    [TestMethod]
    public void ForestOverflowClosesUnknownMarkerWithoutTruncatingTheObservedArrival()
    {
        var session = AtSouthGate();
        var (_, receipt) = Enter(session);
        var state = session.State;
        int arrival = ObserveForestArrival(true);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, 250, 250 + arrival,
            arrival - OrdinaryShadeArrivalFrames));
        var saved = session.PrepareFieldSave();
        Assert.AreEqual(JsonSerializer.Serialize(CampaignQuestLogRecorder.Close(state.QuestLog!)), JsonSerializer.Serialize(saved.QuestLog));
        Assert.AreSame(Marker(state), Marker(saved));
        Assert.IsNull(Marker(saved).TransitionTiming);
        Assert.AreEqual(state.World, saved.World);
        Assert.AreSame(state, session.State);
        Assert.IsNull(session.ValidatePlayerSave(saved));
    }

    [TestMethod]
    public void PersistedForestTimingRejectsMissingForeignAndOverflowedDurations()
    {
        var session = AtSouthGate();
        var (_, receipt) = Enter(session);
        int arrival = ObserveForestArrival(true);
        int extra = arrival - OrdinaryShadeArrivalFrames;
        int change = FieldDoorPresentation.MinimumStepDepartureFrames;
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, change + arrival, extra));
        var saved = session.PrepareFieldSave();
        var scene = saved.QuestLog!.Scenes[^1];
        var marker = Marker(saved);
        var timing = marker.TransitionTiming!;
        int index = scene.Actions.IndexOf(marker);
        foreach (var malformed in new[]
        {
            timing with { PreviewFrames = 0 },
            timing with { PreviewFrames = -1 },
            timing with { PreviewFrames = int.MaxValue },
            timing with { PreviewFrames = 256 },
            timing with { Interlude = FieldTransitionInterlude.None },
            timing with { Interlude = FieldTransitionInterlude.CaveEnter },
            timing with { Interlude = FieldTransitionInterlude.CaveExit },
            timing with { Interlude = FieldTransitionInterlude.CavePreviewEnter },
            timing with { Interlude = FieldTransitionInterlude.RegionPreview },
            timing with { ArrivalPalette = default },
            timing with { DepartureDoor = null },
            timing with { ArrivalDoor = new(29, 62) },
            timing with { SwitchFrame = change - 1 },
            timing with { EndFrame = timing.EndFrame + 1 }
        })
        {
            var invalidScene = scene with { Actions = scene.Actions.SetItem(index, marker with { TransitionTiming = malformed }) };
            var invalid = saved.QuestLog with { Scenes = saved.QuestLog.Scenes.SetItem(saved.QuestLog.Scenes.Length - 1, invalidScene) };
            Assert.AreEqual("save_quest_log_transition_timing_invalid",
                CampaignQuestLogRecorder.Validate(invalid, content.QuestLogSource, content), JsonSerializer.Serialize(malformed));
        }
    }
}
