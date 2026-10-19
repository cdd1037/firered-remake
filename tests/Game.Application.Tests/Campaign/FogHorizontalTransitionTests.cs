using System.Reflection;
using System.Text.Json;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>
/// Controlled placement retains the strict checkpoint's earned party, inventory,
/// flags and RNG. Public commands own each actual warp and its recording.
/// These five bounded sequences are not a campaign replay or native-rendering gate.
/// </summary>
[TestClass, TestCategory("Game")]
public sealed class FogHorizontalTransitionTests : WildBattleFixture
{
    private const string Tower2 = "map.pokemon-tower-2f", Tower3 = "map.pokemon-tower-3f";
    private const string Tower4 = "map.pokemon-tower-4f";
    private const string Entrance = "map.five-island-lost-cave-entrance";
    private const string Room1 = "map.five-island-lost-cave-room1", Room2 = "map.five-island-lost-cave-room2";
    // Independent original-C vector: Task_ExitNonDoor and Task_ExitStairs both
    // release on callback19. Weather reaches idle on callback18, not callback17.
    private const int FogArrivalFrames = 19;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void AssertStructure(object? expected, object? actual, string message) =>
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected),
            JsonSerializer.SerializeToElement(actual)), message);

    private static void AssertStable(CampaignState state)
    {
        Assert.IsTrue(state.Script.Completed);
        Assert.IsFalse(state.Script.Locked);
        Assert.IsNull(state.Script.Wait);
        Assert.IsNull(state.Dialogue);
        Assert.IsNull(state.WildBattle);
        Assert.IsNull(state.FieldWeather, "This slice selects the static map header, not a scripted weather override.");
        Assert.IsTrue(state.EntryPrograms.IsEmpty);
        Assert.IsTrue(state.Motions.IsEmpty);
    }

    private CampaignSession Continue(CampaignSession session, CampaignState state)
    {
        Assert.IsNull(session.ValidatePlayerSave(state));
        var decoded = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(state, session.Revision,
            session.RulesVersion, content.ContentVersion), session.RulesVersion, content.ContentVersion,
            validate: session.ValidatePlayerSave);
        Assert.AreEqual(session.Revision, decoded.Generation);
        var restored = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        AssertStructure(state, restored.State, "Strict Continue preserves the complete saved gameplay and recording payload.");
        return restored;
    }

    private CampaignSession Place(string mapName, int x, int y, Direction facing)
    {
        var basis = checkpoint.State;
        var session = CampaignSession.CreateCurrent(content);
        var map = new MapId(mapName);
        var position = new TilePosition(map, x, y, content.Maps[map].GetTile(x, y)!.Elevation, facing);
        var state = basis with
        {
            World = basis.World with { Player = position },
            Script = StoryState.Empty with
            {
                Flags = basis.Script.Flags, Variables = basis.Script.Variables,
                EffectSequence = basis.Script.EffectSequence
            },
            Dialogue = null, ItemMenu = null, MonPicture = null, Motions = [],
            PresentationTicks = 0, PresentationEffects = [], EntryPrograms = [], ArrivalFacing = null,
            IsSurfing = false, IsCycling = false, BicycleSlope = false, MapEdits = null,
            MapLayout = null, FieldTransition = null, FieldWeather = null,
            PlayerVisible = true, ScreenIsBlack = false, ScreenIsWhite = false,
            Music = content.MapDefinition(map).Music, QuestLog = null
        };
        // Known strict placement helper: real map entry establishes actors, flags
        // and field state. No earned party, inventory or progression is invented.
        var enter = typeof(CampaignSession).GetMethod("EnterMap", Private)!;
        var pump = typeof(CampaignSession).GetMethod("Pump", Private)!;
        state = (CampaignState)enter.Invoke(session, [state, null, RoamerTransition.Warp])!;
        state = (CampaignState)pump.Invoke(session, [state])!;
        typeof(CampaignSession).GetField("_commands", Private)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));
        AssertStable(state);
        Assert.AreEqual(position, state.World.Player);
        Assert.AreEqual(basis.World.StepCount, state.World.StepCount);
        Assert.AreEqual(basis.World.Rng, state.World.Rng, "Controlled entry has no arrival direction and consumes no roamer draw.");
        AssertStructure(basis.Roamer, state.Roamer, "Controlled placement does not advance roamer history.");
        AssertStructure(basis.Party, state.Party, "Retain every owned individual's earned data.");
        AssertStructure(basis.Inventory, state.Inventory, "Retain the checkpoint inventory.");
        return Continue(session, state);
    }

    private void BeginRecording(CampaignSession session)
    {
        Assert.IsTrue(session.CanReorderFieldParty);
        Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
        var scene = session.State.QuestLog!.Scenes[^1];
        Assert.AreEqual(session.State.World.Player.MapId.Value, scene.Map);
        Assert.AreEqual(CampaignQuestLogEvent.PartyOrder, scene.Actions.Single().Event);
        // Finish the authentic public caption before the observed transition.
        for (ulong frame = 1; frame <= 256; frame++) session.ObserveQuestLogFieldFrame(frame);
    }

    private void AssertWarp(string from, int index, int x, int y, string to, int destinationIndex, int behavior)
    {
        var map = new MapId(from);
        Assert.AreEqual(new FieldWarp(index, x, y, 3, new(to), destinationIndex), content.Maps[map].FieldWarps[index]);
        Assert.AreEqual(behavior, content.Maps[map].SourceBehaviorAt(x, y));
        Assert.IsFalse(content.Maps[map].GetTile(x, y)!.Blocked);
    }

    private void AssertHeader(string name, bool fog, bool cave)
    {
        var definition = content.MapDefinition(new(name));
        Assert.AreEqual(cave ? "MAP_TYPE_UNDERGROUND" : "MAP_TYPE_INDOOR", definition.MapType);
        Assert.AreEqual(cave ? "MAPSEC_LOST_CAVE" : "MAPSEC_POKEMON_TOWER", definition.RegionMapSection);
        Assert.AreEqual(cave ? "MUS_SEVII_CAVE" : "MUS_POKE_TOWER", definition.Music);
        Assert.AreEqual(fog ? "WEATHER_FOG_HORIZONTAL" : "WEATHER_NONE", definition.Weather);
        Assert.IsFalse(definition.RequiresFlash);
        Assert.AreEqual("Cave", definition.MapPreview!.Type.ToString());
        Assert.AreEqual(cave ? "FLAG_WORLD_MAP_FIVE_ISLAND_LOST_CAVE_ENTRANCE" : "FLAG_WORLD_MAP_POKEMON_TOWER_1F",
            definition.MapPreview.VisitFlag);
    }

    [TestMethod]
    public void PublicTowerNormalFogStairsBothWaysRetainDestinationPaletteAndContinue()
    {
        AssertHeader(Tower2, false, false); AssertHeader(Tower3, true, false);
        AssertWarp(Tower2, 0, 4, 10, Tower3, 0, 0x6d);
        AssertWarp(Tower3, 0, 4, 10, Tower2, 0, 0x6f);
        var session = Place(Tower2, 4, 10, Direction.West);
        BeginRecording(session);
        session = ObservePublicWarp(session, Direction.West, new(new(Tower3), 4, 10, 3, Direction.East),
            FieldTransitionCause.StairWarp, FieldTransitionDeparture.Immediate, FieldArrivalPalette.FogHorizontal,
            0x6d, 0x6f, musicWait: 7);
        // The arrival faces east; one west move only turns on the stair.
        var atTop = session.State.World;
        Send(session, new("move", Direction.West));
        Assert.AreEqual(atTop.Player with { Facing = Direction.West }, session.State.World.Player);
        Assert.AreEqual(atTop.StepCount, session.State.World.StepCount);
        Assert.AreEqual(atTop.Rng, session.State.World.Rng);
        Assert.IsNull(session.State.FieldTransition);
        BeginRecording(session);
        session = ObservePublicWarp(session, Direction.West, new(new(Tower2), 4, 10, 3, Direction.East),
            FieldTransitionCause.StairWarp, FieldTransitionDeparture.Immediate, FieldArrivalPalette.Normal, 0x6f, 0x6d);
        AssertStructure(checkpoint.State.Party, session.State.Party, "Two public swaps restore the earned party order.");
        Assert.AreEqual(checkpoint.State.World.StepCount, session.State.World.StepCount);
    }

    [TestMethod]
    public void PublicTowerFogStairsBothWaysRestartArrivalAndRecordDelayedMusic()
    {
        AssertHeader(Tower3, true, false); AssertHeader(Tower4, true, false);
        AssertWarp(Tower3, 1, 18, 10, Tower4, 1, 0x6c);
        AssertWarp(Tower4, 1, 18, 10, Tower3, 1, 0x6e);
        var session = Place(Tower3, 18, 10, Direction.East);
        BeginRecording(session);
        session = ObservePublicWarp(session, Direction.East, new(new(Tower4), 18, 10, 3, Direction.West),
            FieldTransitionCause.StairWarp, FieldTransitionDeparture.Immediate, FieldArrivalPalette.FogHorizontal, 0x6c, 0x6e);
        Send(session, new("turn", Direction.East));
        BeginRecording(session);
        session = ObservePublicWarp(session, Direction.East, new(new(Tower3), 18, 10, 3, Direction.West),
            FieldTransitionCause.StairWarp, FieldTransitionDeparture.Immediate, FieldArrivalPalette.FogHorizontal,
            0x6e, 0x6c, musicWait: 9);
        AssertStructure(checkpoint.State.Party, session.State.Party, "Fog presentation cannot change the earned party.");
        Assert.AreEqual(checkpoint.State.World.StepCount, session.State.World.StepCount);
    }

    [TestMethod]
    public void PublicLostCaveLadderBothWaysCountsOnlyAcceptedStepsAndClearsFogOnNormalArrival()
    {
        AssertHeader(Entrance, false, true); AssertHeader(Room1, true, true);
        AssertWarp(Entrance, 1, 5, 5, Room1, 0, 0x61);
        AssertWarp(Room1, 0, 8, 2, Entrance, 1, 0x61);
        var session = Place(Entrance, 5, 6, Direction.North);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_FIVE_ISLAND_LOST_CAVE_ENTRANCE"));
        BeginRecording(session);
        session = ObservePublicWarp(session, Direction.North, new(new(Room1), 8, 2, 3, Direction.North),
            FieldTransitionCause.PlainWarp, FieldTransitionDeparture.StepOntoWarp, FieldArrivalPalette.FogHorizontal);
        Send(session, new("turn", Direction.South));
        var onLadder = session.State;
        Send(session, new("move", Direction.South));
        AssertStable(session.State);
        Assert.AreEqual(new TilePosition(new(Room1), 8, 3, 3, Direction.South), session.State.World.Player);
        Assert.AreEqual(onLadder.World.StepCount + 1, session.State.World.StepCount);
        // The genuine cave floor step performs the source cooldown draw. Do not
        // incorrectly demand unchanged RNG or replace it with fixture RNG.
        var cooldown = Gen3Rng.Next(onLadder.World.Rng, "encounter.cooldown-bypass", 100);
        Assert.IsTrue(cooldown.Trace.Result >= 5);
        Assert.AreEqual(cooldown.State, session.State.World.Rng);
        AssertStructure(onLadder.Roamer, session.State.Roamer, "An ordinary cave step does not warp the roamer.");
        Assert.AreEqual(1, session.State.Exploration!.EarlyEncounter!.StepsSinceLastEncounter);
        BeginRecording(session);
        session = ObservePublicWarp(session, Direction.North, new(new(Entrance), 5, 5, 3, Direction.North),
            FieldTransitionCause.PlainWarp, FieldTransitionDeparture.StepOntoWarp, FieldArrivalPalette.Normal, musicWait: 5);
        Assert.AreEqual(checkpoint.State.World.StepCount + 3, session.State.World.StepCount);
        AssertStructure(checkpoint.State.Party, session.State.Party, "The ladder loop restores the earned party order.");
    }

    [TestMethod]
    public void PublicLostCaveArrowRoomsBothWaysRecordStationaryFogArrival()
    {
        AssertHeader(Room1, true, true); AssertHeader(Room2, true, true);
        AssertWarp(Room1, 2, 8, 5, Room2, 3, 0x62);
        AssertWarp(Room2, 3, 2, 5, Room1, 2, 0x63);
        var session = Place(Room1, 8, 5, Direction.East);
        BeginRecording(session);
        session = ObservePublicWarp(session, Direction.East, new(new(Room2), 2, 5, 3, Direction.East),
            FieldTransitionCause.PlainWarp, FieldTransitionDeparture.Immediate, FieldArrivalPalette.FogHorizontal);
        Send(session, new("turn", Direction.West));
        BeginRecording(session);
        session = ObservePublicWarp(session, Direction.West, new(new(Room1), 8, 5, 3, Direction.West),
            FieldTransitionCause.PlainWarp, FieldTransitionDeparture.Immediate, FieldArrivalPalette.FogHorizontal, musicWait: 11);
        Assert.AreEqual(checkpoint.State.World.StepCount, session.State.World.StepCount);
        AssertStructure(checkpoint.State.Party, session.State.Party, "Both observed arrow warps preserve the earned party data.");
    }

    [TestMethod]
    public void PublicLostCaveWrongTurnSelfWarpResetsDestinationMapClockDespiteUnchangedMapId()
    {
        AssertHeader(Room1, true, true);
        AssertWarp(Room1, 4, 2, 5, Room1, 2, 0x63);
        AssertWarp(Room1, 2, 8, 5, Room2, 3, 0x62);
        var session = Place(Room1, 2, 5, Direction.West);
        BeginRecording(session);
        var origin = session.State;
        session = ObservePublicWarp(session, Direction.West, new(new(Room1), 8, 5, 3, Direction.West),
            FieldTransitionCause.PlainWarp, FieldTransitionDeparture.Immediate, FieldArrivalPalette.FogHorizontal, musicWait: 6);
        Assert.AreEqual(origin.World.Player.MapId, session.State.World.Player.MapId);
        Assert.AreNotEqual(origin.World.Player.X, session.State.World.Player.X);
        Assert.AreEqual(origin.World.StepCount, session.State.World.StepCount);
        var marker = session.State.QuestLog!.Scenes[^1].Actions.Single(action => action.Transition is not null);
        Assert.AreEqual(Room1, marker.Transition!.Map);
        Assert.IsNotNull(marker.TransitionTiming, "An authentic same-map load must retain its own observed timing.");
    }

    private CampaignSession ObservePublicWarp(CampaignSession session, Direction direction, TilePosition destination,
        FieldTransitionCause cause, FieldTransitionDeparture departureKind, FieldArrivalPalette palette,
        int? departureBehavior = null, int? arrivalBehavior = null, int musicWait = 0)
    {
        var origin = session.State;
        long commandRevision = session.Revision;
        var expectedRoamer = SourceRoamerRules.Transition(origin.Roamer!, RoamerTransition.Warp,
            content.CelioRoamerMaps![destination.MapId], origin.World.Rng);
        Send(session, new("move", direction));
        var state = session.State;
        var receipt = state.FieldTransition;
        Assert.IsNotNull(receipt, "Admission must retain the exact public movement receipt.");
        Assert.AreEqual(commandRevision + 1, session.Revision);
        Assert.AreEqual(cause, receipt.Cause);
        Assert.AreEqual(departureKind, receipt.Departure);
        Assert.AreEqual(arrivalBehavior is null ? FieldTransitionArrival.Plain : FieldTransitionArrival.Stair, receipt.Arrival);
        Assert.AreEqual(origin.World.Player, receipt.From);
        Assert.AreEqual(destination, receipt.To);
        Assert.AreEqual(destination, receipt.ArrivalAnchor);
        Assert.AreEqual(destination, state.World.Player);
        Assert.AreEqual(departureBehavior, receipt.DepartureStairBehavior);
        Assert.AreEqual(arrivalBehavior, receipt.ArrivalStairBehavior);
        Assert.AreEqual(departureKind == FieldTransitionDeparture.StepOntoWarp ?
            new TilePosition(receipt.From.MapId, receipt.From.X, receipt.From.Y - 1, 3, Direction.North) : null,
            receipt.DepartureStep);
        Assert.AreEqual(origin.World.StepCount + (departureKind == FieldTransitionDeparture.StepOntoWarp ? 1 : 0), state.World.StepCount);
        Assert.AreEqual(origin.World.Revision + 1, state.World.Revision);
        Assert.AreEqual(expectedRoamer.Rng, state.World.Rng, "The accepted map entry alone consumes roamer RNG.");
        AssertStructure(expectedRoamer.State, state.Roamer, "This includes real roamer history updates on same-map warps.");
        AssertStructure(origin.Party, state.Party, "Warp and presentation retain the full earned party payload.");
        AssertStructure(origin.Inventory, state.Inventory, "Warp and presentation retain the inventory.");
        AssertStable(state);
        Assert.IsTrue(session.CanPresentFieldTransition(origin, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(origin, receipt with { }));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with { }, receipt));
        if (cause == FieldTransitionCause.StairWarp || departureKind == FieldTransitionDeparture.StepOntoWarp)
        {
            Assert.IsFalse(session.CanPresentFieldTransition(receipt));
            Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsCycling = true }, receipt));
            Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsSurfing = true }, receipt));
        }
        Assert.AreEqual(FieldTransitionInterlude.None, session.FieldTransitionInterludeFor(receipt));
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(origin, receipt), "These actual same-section warps request no map preview.");

        var scene = state.QuestLog!.Scenes[^1];
        var marker = scene.Actions.Last(action => action.Transition is not null);
        Assert.IsNull(marker.TransitionTiming);
        var unknown = new CampaignQuestLogPlayback(new([scene]));
        unknown.Tick(marker.StartFrame / 60d);
        Assert.IsNull(unknown.ActiveTransitionTiming);
        Assert.AreEqual(destination.MapId.Value, unknown.Map, "An unobserved marker remains unknown, including in fog.");

        int change = FieldDoorPresentation.DepartureFrames(cause, departureKind) + musicWait;
        var samples = new List<FieldDoorPresentation.PhaseSample>();
        var stairSamples = new List<FieldStairPresentation.PhaseSample>();
        var plainDeparture = cause == FieldTransitionCause.PlainWarp && departureKind == FieldTransitionDeparture.Immediate
            ? new FieldWarpPresentation.Departure() : null;
        var stepDeparture = departureKind == FieldTransitionDeparture.StepOntoWarp ? new FieldDoorPresentation.StepDeparture() : null;
        var stairDeparture = departureBehavior is { } depart ? new FieldStairPresentation.Departure(depart) : null;
        for (int frame = 0; frame < change; frame++)
        {
            var sample = stairDeparture is not null ? FieldDoorPresentation.FromPlain(stairDeparture.Sample.Phase) :
                stepDeparture is not null ? stepDeparture.Sample : FieldDoorPresentation.FromPlain(plainDeparture!.Sample);
            samples.Add(sample);
            if (stairDeparture is not null) stairSamples.Add(stairDeparture.Sample);
            bool ready = frame + 1 >= change - 1;
            plainDeparture?.Advance(ready); stepDeparture?.Advance(ready); stairDeparture?.Advance(ready);
        }
        Assert.IsTrue(plainDeparture?.Complete ?? stepDeparture?.Complete ?? stairDeparture!.Complete);
        int expectedArrivalFrames = palette == FieldArrivalPalette.FogHorizontal ? FogArrivalFrames : 24;
        Assert.AreEqual(expectedArrivalFrames, FieldDoorPresentation.ArrivalFrames(receipt.Arrival, palette));
        var plainArrival = arrivalBehavior is null ? new FieldWarpPresentation.PlainArrival(palette) : null;
        var stairArrival = arrivalBehavior is { } arrive ? new FieldStairPresentation.Arrival(arrive, palette) : null;
        for (int frame = 0; frame < expectedArrivalFrames; frame++)
        {
            var sample = stairArrival is not null ? FieldDoorPresentation.FromPlain(stairArrival.Sample.Phase) :
                FieldDoorPresentation.FromPlain(plainArrival!.Sample);
            Assert.IsFalse(sample.Complete, "The last source task callback still belongs to the locked arrival.");
            if (frame == 0) Assert.AreEqual((16, 16), (sample.BackgroundBlend, sample.ObjectBlend));
            samples.Add(sample);
            if (stairArrival is not null) stairSamples.Add(stairArrival.Sample);
            plainArrival?.Advance(); stairArrival?.Advance();
        }
        Assert.IsTrue(plainArrival?.Complete ?? stairArrival!.Complete);
        Assert.AreEqual(expectedArrivalFrames, plainArrival?.Frames ?? stairArrival!.Frames);
        int end = change + expectedArrivalFrames;
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt with { }, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end - 1));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end + 1));
        long revision = session.Revision;
        string unchanged = JsonSerializer.Serialize(state);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change + 1, end + 1));

        var saved = session.PrepareFieldSave();
        var recordedScene = saved.QuestLog!.Scenes[^1];
        var recorded = recordedScene.Actions.Last(action => action.Transition is not null);
        var timing = recorded.TransitionTiming;
        Assert.IsNotNull(timing);
        Assert.AreEqual(cause, timing.Cause);
        Assert.AreEqual(departureKind, timing.EffectiveDeparture);
        Assert.AreEqual(receipt.Arrival, timing.Arrival);
        Assert.AreEqual(palette, timing.ArrivalPalette);
        Assert.AreEqual(FieldTransitionInterlude.None, timing.Interlude);
        Assert.AreEqual(0, timing.PreviewFrames);
        Assert.AreEqual(change, timing.SwitchFrame);
        Assert.AreEqual(end, timing.EndFrame);
        Assert.AreEqual(departureBehavior, timing.DepartureStairBehavior);
        Assert.AreEqual(arrivalBehavior, timing.ArrivalStairBehavior);
        Assert.AreEqual(receipt.DepartureStep is { } step ? new CampaignQuestLogDoorAnchor(step.X, step.Y) : null, timing.DepartureDoor);
        Assert.IsNull(timing.ArrivalDoor);
        Assert.AreEqual(marker with { Frames = end, TransitionTiming = timing }, recorded);
        AssertStructure(state with { QuestLog = saved.QuestLog }, saved, "Save projection changes only the completed recording.");
        AssertStructure(saved, session.PrepareFieldSave(), "Immediate repeated saves retain identical timing.");
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog, content.QuestLogSource, content));
        foreach (var malformed in new[] { timing with { EndFrame = end - 1 }, timing with { EndFrame = end + 1 },
            timing with { ArrivalPalette = (FieldArrivalPalette)99 } })
        {
            var invalidScene = recordedScene with { Actions = recordedScene.Actions.SetItem(recordedScene.Actions.IndexOf(recorded),
                recorded with { Frames = malformed.EndFrame, TransitionTiming = malformed }) };
            var invalidLog = saved.QuestLog with { Scenes = saved.QuestLog.Scenes.SetItem(saved.QuestLog.Scenes.Length - 1, invalidScene) };
            Assert.AreEqual("save_quest_log_transition_timing_invalid",
                CampaignQuestLogRecorder.Validate(invalidLog, content.QuestLogSource, content));
        }
        var restored = Continue(session, saved);
        Assert.IsFalse(restored.CompleteFieldTransitionPresentation(receipt, change, end));
        var playback = new CampaignQuestLogPlayback(new([restored.State.QuestLog!.Scenes[^1]]));
        playback.Tick(recorded.StartFrame / 60d);
        var sceneOrigin = recordedScene.Actors.Single(actor => actor.Id == CampaignQuestLogRecorder.PlayerId);
        for (int frame = 0; frame < end; frame++)
        {
            Assert.AreEqual(timing, playback.ActiveTransitionTiming);
            Assert.AreEqual(frame, playback.TransitionElapsedFrames, .00001);
            var expected = frame < change ? receipt.From : receipt.To;
            Assert.AreEqual(expected.MapId.Value, playback.Map);
            var player = playback.Actors.Single(actor => actor.Id == CampaignQuestLogRecorder.PlayerId);
            Assert.AreEqual((expected.X, expected.Y, expected.Facing), (player.X, player.Y, player.Facing));
            var mapOrigin = frame < change ? sceneOrigin : recorded.Transition!.Actors.Single(actor => actor.Id == CampaignQuestLogRecorder.PlayerId);
            Assert.AreEqual(mapOrigin, playback.MapOriginPlayer);
            Assert.AreEqual(frame < change ? recorded.StartFrame + frame : frame - change, playback.MapFrames, .00001,
                "Every authenticated map load resets the transient presentation clock, even when map IDs are equal.");
            Assert.AreEqual(samples[frame], FieldDoorPresentation.SampleRecorded(timing.Cause, timing.Arrival, change, frame,
                timing.DepartureStairBehavior, timing.ArrivalStairBehavior, timing.EffectiveDeparture, timing.Interlude, timing.ArrivalPalette));
            if (stairDeparture is not null)
                Assert.AreEqual(stairSamples[frame], FieldStairPresentation.SampleRecorded(departureBehavior!.Value,
                    arrivalBehavior!.Value, change, frame, timing.ArrivalPalette), "Recap retains every stair offset and pose as well as both palette banks.");
            if (frame == change)
            {
                Assert.AreEqual(0d, playback.MapFrames);
                Assert.AreEqual((16, 16), (samples[frame].BackgroundBlend, samples[frame].ObjectBlend));
                if (palette == FieldArrivalPalette.FogHorizontal)
                {
                    var fresh = FieldHorizontalFogPresentation.Sample(0);
                    var current = FieldHorizontalFogPresentation.Sample((long)Math.Max(0, playback.MapFrames - 1));
                    Assert.AreEqual(fresh, current, "Recap starts the destination's independently initialized fog phase.");
                    if (receipt.From.MapId == receipt.To.MapId)
                    {
                        var departed = FieldHorizontalFogPresentation.Sample(recorded.StartFrame + change - 2);
                        Assert.AreNotEqual(departed.ScrollOffset, current.ScrollOffset,
                            "A wrong turn cannot inherit the departing room's advanced fog scroll.");
                    }
                }
            }
            playback.Tick(1d / 60);
        }
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.AreEqual(expectedArrivalFrames, playback.MapFrames, .00001);
        Assert.AreEqual(destination.MapId.Value, playback.Map);
        Assert.AreSame(state, session.State);
        Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State), "Observation and recap preserve positions, steps, RNG and every committed gameplay field.");
        Assert.IsNull(marker.TransitionTiming, "Detached save projection cannot mutate the original marker.");
        return restored;
    }
}
