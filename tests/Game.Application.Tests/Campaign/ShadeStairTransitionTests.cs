using System.Reflection;
using System.Text.Json;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>
/// Controlled Mansion stair placement retains the checkpoint's earned party and
/// inventory. Public commands own both warps and their recording; this is not a
/// natural campaign-route or native-rendering acceptance claim.
/// </summary>
[TestClass, TestCategory("Game")]
public sealed class ShadeStairTransitionTests : WildBattleFixture
{
    private const string FirstFloor = "map.pokemon-mansion-1f";
    private const string SecondFloor = "map.pokemon-mansion-2f";
    private const int ShadeArrivalFrames = 19;

    private CampaignSession AtFirstFloorStair()
    {
        var session = CampaignSession.CreateCurrent(content);
        var basis = checkpoint.State;
        var map = new MapId(FirstFloor);
        var state = basis with
        {
            World = basis.World with { Player = new(map, 10, 13, 3, Direction.East) },
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
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));
        Assert.AreEqual(new TilePosition(map, 10, 13, 3, Direction.East), state.World.Player);
        Assert.AreEqual(JsonSerializer.Serialize(basis.Party), JsonSerializer.Serialize(state.Party));
        Assert.AreEqual(JsonSerializer.Serialize(basis.Inventory), JsonSerializer.Serialize(state.Inventory));
        Assert.IsTrue(state.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_POKEMON_MANSION_1F"));
        StrictRoundtrip(session);
        Assert.IsTrue(session.CanReorderFieldParty);
        // An accepted gameplay event creates the scene; no historical marker or
        // completed presentation observation is injected into the controlled state.
        Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
        Assert.AreEqual(FirstFloor, session.State.QuestLog!.Scenes[^1].Map);
        Assert.AreEqual(CampaignQuestLogEvent.PartyOrder, session.State.QuestLog.Scenes[^1].Actions.Single().Event);
        return session;
    }

    private void AssertSourceRoute()
    {
        var first = new MapId(FirstFloor);
        var second = new MapId(SecondFloor);
        Assert.AreEqual(new FieldWarp(3, 10, 13, 3, second, 2), content.Maps[first].FieldWarps[3]);
        Assert.AreEqual(new FieldWarp(2, 6, 14, 3, first, 3), content.Maps[second].FieldWarps[2]);
        Assert.AreEqual(0x6c, content.Maps[first].SourceBehaviorAt(10, 13));
        Assert.AreEqual(0x6f, content.Maps[second].SourceBehaviorAt(6, 14));
        foreach (var map in new[] { first, second })
        {
            var definition = content.MapDefinition(map);
            Assert.AreEqual("MAP_TYPE_INDOOR", definition.MapType);
            Assert.AreEqual("MAPSEC_POKEMON_MANSION", definition.RegionMapSection);
            Assert.AreEqual("WEATHER_SHADE", definition.Weather);
            Assert.AreEqual("MUS_POKE_MANSION", definition.Music);
            Assert.IsFalse(definition.RequiresFlash);
            Assert.IsNotNull(definition.MapPreview);
            Assert.AreEqual("Forest", definition.MapPreview.Type.ToString());
        }
        Assert.AreEqual("FLAG_WORLD_MAP_POKEMON_MANSION_1F", content.MapDefinition(first).MapPreview!.VisitFlag);
        Assert.IsNull(content.MapDefinition(second).MapPreview!.VisitFlag);
        Assert.AreEqual("PokemonMansion_1F_OnTransition", content.MapDefinition(first).Programs.OnTransition);
        Assert.IsNull(content.MapDefinition(second).Programs.OnTransition);
    }

    [TestMethod]
    [DataRow(0, 7)]
    [DataRow(9, 0)]
    public void PublicShadeStairsBothWaysRecordNineteenCallbackArrivalAndContinueWithoutGameplayChanges(
        int ascentMusicWait, int descentMusicWait)
    {
        AssertSourceRoute();
        var session = AtFirstFloorStair();
        var initial = session.State;
        session = ObservePublicStair(session, Direction.East, SecondFloor, 6, 14, 0x6c, 0x6f, ascentMusicWait);

        // Continue retains the first recording but ends its live ownership.
        // A second accepted party swap starts the return recording and restores
        // the checkpoint's original earned party order.
        Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
        Assert.AreEqual(SecondFloor, session.State.QuestLog!.Scenes[^1].Map);
        Assert.AreEqual(CampaignQuestLogEvent.PartyOrder, session.State.QuestLog.Scenes[^1].Actions.Single().Event);
        Assert.AreEqual(JsonSerializer.Serialize(checkpoint.State.Party), JsonSerializer.Serialize(session.State.Party));

        // The first west input only turns on the arrival stair; the second
        // executes the reciprocal warp from the actual saved arrival cell.
        var upstairs = session.State.World;
        Send(session, new("move", Direction.West));
        Assert.AreEqual(upstairs.Player with { Facing = Direction.West }, session.State.World.Player);
        Assert.IsNull(session.State.FieldTransition);
        Assert.AreEqual(upstairs.StepCount, session.State.World.StepCount);
        Assert.AreEqual(upstairs.Rng, session.State.World.Rng);
        session = ObservePublicStair(session, Direction.West, FirstFloor, 10, 13, 0x6f, 0x6c, descentMusicWait);

        Assert.AreEqual(initial.World.Player with { Facing = Direction.West }, session.State.World.Player);
        Assert.AreEqual(initial.World.StepCount, session.State.World.StepCount,
            "Directional stair warps and their presentation add no walking steps.");
        Assert.AreEqual(JsonSerializer.Serialize(checkpoint.State.Party), JsonSerializer.Serialize(session.State.Party));
        Assert.AreEqual(JsonSerializer.Serialize(initial.Inventory), JsonSerializer.Serialize(session.State.Inventory));
        var observations = session.State.QuestLog!.Scenes.SelectMany(scene => scene.Actions)
            .Where(action => action.TransitionTiming is not null).Select(action => action.TransitionTiming!).ToArray();
        Assert.AreEqual(2, observations.Length);
        Assert.IsTrue(observations.All(timing => timing.ArrivalPalette == FieldArrivalPalette.Shade &&
            timing.EndFrame - timing.SwitchFrame == ShadeArrivalFrames));
        StrictRoundtrip(session);
    }

    private CampaignSession ObservePublicStair(CampaignSession session, Direction direction, string destinationName,
        int destinationX, int destinationY, int departureBehavior, int arrivalBehavior, int musicWait)
    {
        var origin = session.State;
        var from = origin.World.Player;
        var destination = new MapId(destinationName);
        var expectedRoamer = SourceRoamerRules.Transition(origin.Roamer!, RoamerTransition.Warp,
            content.CelioRoamerMaps![destination], origin.World.Rng);
        long commandRevision = session.Revision;
        Send(session, new("move", direction));
        var state = session.State;
        var receipt = state.FieldTransition;
        Assert.IsNotNull(receipt, "Admission must use the exact receipt emitted by the public stair command.");
        Assert.AreEqual(commandRevision + 1, session.Revision);
        Assert.AreEqual(FieldTransitionCause.StairWarp, receipt.Cause);
        Assert.AreEqual(FieldTransitionDeparture.Immediate, receipt.Departure);
        Assert.AreEqual(FieldTransitionArrival.Stair, receipt.Arrival);
        Assert.AreEqual(from, receipt.From);
        Assert.AreEqual(new TilePosition(destination, destinationX, destinationY, 3, direction), receipt.To);
        Assert.AreEqual(receipt.To, receipt.ArrivalAnchor);
        Assert.AreEqual(receipt.To, state.World.Player);
        Assert.IsNull(receipt.DepartureStep);
        Assert.AreEqual(departureBehavior, receipt.DepartureStairBehavior);
        Assert.AreEqual(arrivalBehavior, receipt.ArrivalStairBehavior);
        Assert.AreEqual(origin.World.StepCount, state.World.StepCount);
        Assert.AreEqual(origin.World.Revision + 1, state.World.Revision);
        Assert.AreEqual(expectedRoamer.Rng, state.World.Rng, "Only accepted map entry may consume roamer RNG.");
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expectedRoamer.State),
            JsonSerializer.SerializeToElement(state.Roamer)));
        Assert.AreEqual(JsonSerializer.Serialize(origin.Party), JsonSerializer.Serialize(state.Party));
        Assert.AreEqual(JsonSerializer.Serialize(origin.Inventory), JsonSerializer.Serialize(state.Inventory));
        Assert.IsTrue(state.Script.Completed);
        Assert.IsFalse(state.Script.Locked);
        Assert.IsNull(state.Script.Wait);
        Assert.IsNull(state.Dialogue);
        Assert.IsNull(state.WildBattle);
        Assert.IsTrue(state.EntryPrograms.IsEmpty);
        Assert.IsTrue(state.Motions.IsEmpty);
        Assert.IsTrue(session.CanPresentFieldTransition(origin, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(receipt), "Stairs require their stable walking origin.");
        Assert.IsFalse(session.CanPresentFieldTransition(origin, receipt with { }));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsCycling = true }, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsSurfing = true }, receipt));
        Assert.AreEqual(FieldTransitionInterlude.None, session.FieldTransitionInterludeFor(receipt));
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(origin, receipt),
            "Same-section stairs do not request the region's forest preview.");

        long revision = session.Revision;
        string unchanged = JsonSerializer.Serialize(state);
        var marker = state.QuestLog!.Scenes[^1].Actions.Last(action => action.Transition is not null);
        Assert.IsNull(marker.TransitionTiming);
        var unknown = new CampaignQuestLogPlayback(new([state.QuestLog.Scenes[^1]]));
        unknown.Tick(marker.StartFrame / 60d);
        Assert.IsNull(unknown.ActiveTransitionTiming);
        Assert.AreEqual(destinationName, unknown.Map);

        Assert.AreEqual(36, FieldDoorPresentation.DepartureFrames(receipt.Cause, receipt.Departure));
        Assert.AreEqual(24, FieldStairPresentation.ArrivalDuration(FieldArrivalPalette.Normal));
        Assert.AreEqual(ShadeArrivalFrames, FieldStairPresentation.ArrivalDuration(FieldArrivalPalette.Shade));
        Assert.AreEqual(ShadeArrivalFrames, FieldDoorPresentation.ArrivalFrames(FieldTransitionArrival.Stair, FieldArrivalPalette.Shade));
        int change = FieldStairPresentation.MinimumDepartureFrames + musicWait;
        var observedDeparture = new FieldStairPresentation.Departure(departureBehavior);
        for (int frame = 1; frame <= change; frame++)
        {
            observedDeparture.Advance(frame >= change - 1);
            Assert.AreEqual(frame == change, observedDeparture.Complete);
        }
        var observedArrival = new FieldStairPresentation.Arrival(arrivalBehavior, FieldArrivalPalette.Shade);
        var normalArrival = new FieldStairPresentation.Arrival(arrivalBehavior);
        for (int frame = 1; frame <= 24; frame++)
        {
            observedArrival.Advance();
            normalArrival.Advance();
            Assert.AreEqual(frame >= ShadeArrivalFrames, observedArrival.Complete);
            Assert.AreEqual(frame == 24, normalArrival.Complete);
        }
        Assert.AreEqual(ShadeArrivalFrames, observedArrival.Frames);
        Assert.AreEqual((0, 0), (observedArrival.Sample.OffsetX, observedArrival.Sample.OffsetY));
        int end = change + observedArrival.Frames;
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt with { }, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, 35, 35 + ShadeArrivalFrames));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, change + 18));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, change + 24));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change + 1, end + 1));

        var saved = session.PrepareFieldSave();
        var recordedScene = saved.QuestLog!.Scenes[^1];
        var recordedMarker = recordedScene.Actions.Last(action => action.Transition is not null);
        var timing = recordedMarker.TransitionTiming;
        Assert.IsNotNull(timing);
        Assert.AreEqual(receipt.Cause, timing.Cause);
        Assert.AreEqual(receipt.Arrival, timing.Arrival);
        Assert.AreEqual(FieldArrivalPalette.Shade, timing.ArrivalPalette);
        Assert.AreEqual(FieldTransitionInterlude.None, timing.Interlude);
        Assert.AreEqual(0, timing.PreviewFrames);
        Assert.AreEqual(departureBehavior, timing.DepartureStairBehavior);
        Assert.AreEqual(arrivalBehavior, timing.ArrivalStairBehavior);
        Assert.IsNull(timing.DepartureDoor);
        Assert.IsNull(timing.ArrivalDoor);
        Assert.AreEqual(change, timing.SwitchFrame);
        Assert.AreEqual(end, timing.EndFrame);
        Assert.AreEqual(marker with { Frames = end, TransitionTiming = timing }, recordedMarker);
        Assert.AreEqual(JsonSerializer.Serialize(state with { QuestLog = saved.QuestLog }), JsonSerializer.Serialize(saved));
        Assert.AreEqual(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(session.PrepareFieldSave()));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog, content.QuestLogSource, content));
        Assert.IsNull(session.ValidatePlayerSave(saved));

        int markerIndex = recordedScene.Actions.IndexOf(recordedMarker);
        foreach (var malformed in new[]
        {
            timing with { EndFrame = change + 18 },
            timing with { EndFrame = change + 24 },
            timing with { ArrivalPalette = FieldArrivalPalette.Normal },
            timing with { ArrivalPalette = (FieldArrivalPalette)99 },
            timing with { DepartureStairBehavior = null },
            timing with { ArrivalStairBehavior = null }
        })
        {
            var invalidScene = recordedScene with
            {
                Actions = recordedScene.Actions.SetItem(markerIndex, recordedMarker with
                    { Frames = malformed.EndFrame, TransitionTiming = malformed })
            };
            var invalidLog = saved.QuestLog with
                { Scenes = saved.QuestLog.Scenes.SetItem(saved.QuestLog.Scenes.Length - 1, invalidScene) };
            Assert.AreEqual("save_quest_log_transition_timing_invalid",
                CampaignQuestLogRecorder.Validate(invalidLog, content.QuestLogSource, content), JsonSerializer.Serialize(malformed));
        }

        var bytes = SaveCodec.Encode(saved, revision, session.RulesVersion, content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion,
            validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(saved), JsonSerializer.SerializeToElement(restored.State)));
        Assert.IsFalse(restored.CompleteFieldTransitionPresentation(receipt, change, end));
        var playback = new CampaignQuestLogPlayback(new([restored.State.QuestLog!.Scenes[^1]]));
        playback.Tick(recordedMarker.StartFrame / 60d);
        var departure = new FieldStairPresentation.Departure(departureBehavior);
        var arrival = new FieldStairPresentation.Arrival(arrivalBehavior, FieldArrivalPalette.Shade);
        for (int frame = 0; frame < end; frame++)
        {
            Assert.AreEqual(timing, playback.ActiveTransitionTiming);
            Assert.AreEqual(frame, playback.TransitionElapsedFrames, .00001);
            var expected = frame < change ? receipt.From : receipt.To;
            Assert.AreEqual(expected.MapId.Value, playback.Map);
            var player = playback.Actors.Single(actor => actor.Id == CampaignQuestLogRecorder.PlayerId);
            Assert.AreEqual((expected.X, expected.Y, expected.Facing), (player.X, player.Y, player.Facing));
            var live = frame < change ? departure.Sample : arrival.Sample;
            Assert.AreEqual(live, FieldStairPresentation.SampleRecorded(departureBehavior, arrivalBehavior,
                change, frame, playback.ActiveTransitionTiming!.ArrivalPalette), "Recap retains every live offset, pose and palette bank.");
            Assert.AreEqual(FieldDoorPresentation.FromPlain(live.Phase), FieldDoorPresentation.SampleRecorded(
                timing.Cause, timing.Arrival, change, frame, timing.DepartureStairBehavior,
                timing.ArrivalStairBehavior, timing.EffectiveDeparture, timing.Interlude, timing.ArrivalPalette));
            if (frame < change) departure.Advance(frame + 1 >= change - 1);
            else arrival.Advance();
            playback.Tick(1d / 60);
        }
        Assert.IsTrue(departure.Complete);
        Assert.IsTrue(arrival.Complete);
        Assert.AreEqual(ShadeArrivalFrames, arrival.Frames);
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.AreEqual(destinationName, playback.Map);
        Assert.AreSame(state, session.State);
        Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.IsNull(marker.TransitionTiming, "Presentation and save projection never mutate the committed provisional marker.");
        return restored;
    }
}
