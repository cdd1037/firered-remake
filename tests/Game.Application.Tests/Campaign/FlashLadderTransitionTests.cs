using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Controlled map placement; existing ownership, PC menus, Flash and ladder steps remain public gameplay.</summary>
[TestClass, TestCategory("Game")]
public sealed class FlashLadderTransitionTests : WildBattleFixture
{
    private const string FirstFloor = "map.rock-tunnel-1f", Basement = "map.rock-tunnel-b1f";
    private const string Outside = "map.route-10", FlashMove = "pret:MOVE_FLASH";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private CampaignState Call(CampaignSession session, string method, params object?[] arguments) =>
        (CampaignState)typeof(CampaignSession).GetMethod(method, Private)!.Invoke(session, arguments)!;

    private void Install(CampaignSession session, CampaignState state) =>
        typeof(CampaignSession).GetField("_commands", Private)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));

    private static void AssertStructure(object? expected, object? actual, string message) =>
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected),
            JsonSerializer.SerializeToElement(actual)), message);

    private void AssertStrictContinue(CampaignSession session)
    {
        Assert.IsNull(session.ValidatePlayerSave(session.State));
        var decoded = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(session.State, session.Revision,
            session.RulesVersion, content.ContentVersion), session.RulesVersion, content.ContentVersion,
            validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        AssertStructure(session.State, restored.State, "Strict Continue preserves the entire saved payload.");
    }

    private void DrainUntil(CampaignSession session, Func<bool> complete)
    {
        for (int i = 0; i < 40 && !complete(); i++) Send(session, new("ack"));
        Assert.IsTrue(complete(), "The native map/PC callback must actually reach its stable endpoint.");
    }

    private CampaignSession Place(CampaignState basis, string mapName, int x, int y, Direction facing = Direction.North)
    {
        var session = CampaignSession.CreateCurrent(content);
        var map = new MapId(mapName);
        var state = basis with
        {
            World = basis.World with { Player = new(map, x, y, content.Maps[map].GetTile(x, y)!.Elevation, facing) },
            Script = StoryState.Empty with
            {
                Flags = basis.Script.Flags, Variables = basis.Script.Variables,
                EffectSequence = basis.Script.EffectSequence
            },
            Dialogue = null, ItemMenu = null, MonPicture = null, Motions = [],
            PresentationTicks = 0, PresentationEffects = [], EntryPrograms = [], ArrivalFacing = null,
            IsSurfing = false, IsCycling = false, BicycleSlope = false, MapEdits = null,
            MapLayout = null, FieldTransition = null, FieldWeather = null,
            PlayerVisible = true, Music = content.MapDefinition(map).Music, QuestLog = null
        };
        // Entry itself, rather than a fixture assignment, establishes the Flash level.
        Install(session, Call(session, "Pump", Call(session, "EnterMap", state, null, RoamerTransition.Warp)));
        DrainUntil(session, () => session.State.Script.Completed && session.State.EntryPrograms.IsEmpty);
        AssertStable(session.State);
        Assert.AreEqual(new TilePosition(map, x, y, content.Maps[map].GetTile(x, y)!.Elevation, facing), session.State.World.Player);
        AssertStrictContinue(session);
        return session;
    }

    private CampaignState WithdrawExistingFlashOwner()
    {
        var basis = checkpoint.State;
        Assert.IsTrue(basis.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"));
        Assert.IsFalse(basis.Script.Flags.GetValueOrDefault(FieldFlashSourcePresentation.ActiveFlag));
        Assert.IsNotNull(basis.Inventory!.Machines);
        Assert.AreEqual(1, basis.Inventory.Machines!.Value.Single(slot => slot.Item.Value == "pret:ITEM_HM05").Quantity);
        Assert.AreEqual(6, basis.Party.Length);
        Assert.IsFalse(basis.Party.Any(mon => mon.Pokemon.Moves.Any(move => move.Move.Value == FlashMove)));
        var hypno = basis.Storage!.Boxes[13].Slots[1];
        Assert.IsNotNull(hypno);
        Assert.AreEqual(6L, hypno.InstanceId);
        Assert.AreEqual("pret:SPECIES_HYPNO", hypno.Pokemon.Species.Value);
        Assert.AreEqual(80, hypno.Pokemon.Level);
        CollectionAssert.AreEqual(new[] { FlashMove, "pret:MOVE_HYPNOSIS", "pret:MOVE_DISABLE", "pret:MOVE_PSYCHIC" },
            hypno.Pokemon.Moves.Select(move => move.Move.Value).ToArray());
        PartyPokemon[] Owned(CampaignState state) => state.Party
            .Concat(state.Storage!.Boxes.SelectMany(box => box.Slots).OfType<PartyPokemon>())
            .OrderBy(mon => mon.InstanceId).ToArray();
        var owned = Owned(basis);
        var deposited = basis.Party[^1];
        var session = Place(basis, "map.viridian-pokemon-center-1f", 7, 5);
        Install(session, Call(session, "Pump", Call(session, "StartStory", session.State, "EventScript_PC")));
        DrainUntil(session, () => session.State.Pc?.View == CampaignPcView.Main);
        Send(session, new("pc_select", Choice: 0));
        DrainUntil(session, () => session.State.Pc?.View == CampaignPcView.StorageMenu);
        Send(session, new("storage_select", Choice: 1));
        Send(session, new("storage_box", Choice: 13));
        Send(session, new("storage_deposit", PartyIndex: 5));
        Assert.AreEqual(5, session.State.Party.Length);
        Assert.AreEqual(hypno, session.State.Storage!.Boxes[13].Slots[1]);
        Send(session, new("pc_back"));
        Send(session, new("storage_select", Choice: 0));
        Send(session, new("storage_withdraw", MoveSlot: 1));
        Assert.AreEqual(hypno, session.State.Party[^1]);
        Assert.IsNull(session.State.Storage!.Boxes[13].Slots[1]);
        var boxed = session.State.Storage.Boxes[13].Slots.Single(mon => mon?.InstanceId == deposited.InstanceId)!;
        Assert.AreEqual(deposited.InstanceId, boxed.InstanceId);
        Assert.AreEqual(deposited.IsEgg, boxed.IsEgg);
        Assert.AreEqual(deposited.Nickname, boxed.Nickname);
        Assert.AreEqual(deposited.Pokemon.Species, boxed.Pokemon.Species);
        Assert.AreEqual(deposited.Pokemon.Personality, boxed.Pokemon.Personality);
        Assert.AreEqual(deposited.Pokemon.OriginalTrainerId, boxed.Pokemon.OriginalTrainerId);
        Assert.AreEqual(deposited.Pokemon.Experience, boxed.Pokemon.Experience);
        Assert.AreEqual(deposited.Pokemon.Friendship, boxed.Pokemon.Friendship);
        AssertStructure(deposited.Pokemon.Ivs, boxed.Pokemon.Ivs, "Depositing preserves individual values.");
        AssertStructure(deposited.Pokemon.Evs, boxed.Pokemon.Evs, "Depositing preserves earned effort values.");
        CollectionAssert.AreEqual(deposited.Pokemon.Moves.Select(move => (move.Move, move.MaximumPp, move.PpUps)).ToArray(),
            boxed.Pokemon.Moves.Select(move => (move.Move, move.MaximumPp, move.PpUps)).ToArray());
        // Native boxing legitimately recalculates stats and restores HP/PP.
        Assert.AreEqual(0u, boxed.Status);
        Assert.AreEqual(boxed.Pokemon.Stats.MaxHp, boxed.Pokemon.CurrentHp);
        Assert.IsTrue(boxed.Pokemon.Moves.All(move => move.CurrentPp == move.MaximumPp));
        Send(session, new("pc_back"));
        Send(session, new("pc_back"));
        DrainUntil(session, () => session.State.Pc?.View == CampaignPcView.Main);
        Send(session, new("pc_back"));
        DrainUntil(session, () => session.State.Pc is null && session.State.Script.Completed);
        var currentOwners = Owned(session.State);
        CollectionAssert.AreEqual(owned.Select(mon => mon.InstanceId).ToArray(), currentOwners.Select(mon => mon.InstanceId).ToArray());
        Assert.AreEqual(currentOwners.Length, currentOwners.Select(mon => mon.InstanceId).Distinct().Count());
        foreach (var unchanged in owned.Where(mon => mon.InstanceId != deposited.InstanceId))
            AssertStructure(unchanged, currentOwners.Single(mon => mon.InstanceId == unchanged.InstanceId),
                "Public PC commands preserve every other owner, including the already learned Flash moves.");
        AssertStructure(basis.Inventory, session.State.Inventory, "PC movement does not consume or teach HM05.");
        AssertStrictContinue(session);
        return session.State;
    }

    private void UseFlash(CampaignSession session)
    {
        int index = Enumerable.Range(0, session.State.Party.Length).Single(i =>
            session.State.Party[i].InstanceId == 6 && session.State.Party[i].Pokemon.Moves.Any(move => move.Move.Value == FlashMove));
        var before = session.State;
        long revision = session.Revision;
        Assert.AreEqual(4, before.FlashLevel);
        Assert.IsTrue(session.CanUseFieldFlash(index));
        Send(session, new("field_flash", PartyIndex: index));
        Assert.AreEqual(FieldFlashSourcePresentation.ShowMon, session.State.FlashEffect!.Phase);
        string showMon = session.State.FlashEffect.WaitId;
        Send(session, new("field_flash_complete", Text: showMon));
        Assert.AreEqual(FieldFlashSourcePresentation.Illuminate, session.State.FlashEffect!.Phase);
        Assert.AreEqual(4, session.State.FlashLevel);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(FieldFlashSourcePresentation.ActiveFlag));
        string illuminate = session.State.FlashEffect.WaitId;
        Assert.AreNotEqual(showMon, illuminate);
        var waiting = session.State;
        var stale = session.Execute("stale-flash:" + commands++, session.Revision, new("field_flash_complete", Text: showMon));
        Assert.AreEqual(CommandStatus.Rejected, stale.Status);
        Assert.AreEqual("field_flash_callback_mismatch", stale.Error);
        Assert.AreSame(waiting, session.State);
        Send(session, new("field_flash_complete", Text: illuminate));
        Assert.AreEqual(revision + 3, session.Revision, "Exactly two matching completion callbacks commit after field_flash.");
        Assert.IsNull(session.State.FlashEffect);
        Assert.AreEqual(0, session.State.FlashLevel);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(FieldFlashSourcePresentation.ActiveFlag));
        Assert.AreEqual(before.World, session.State.World);
        AssertStructure(before.Party, session.State.Party, "Flash preserves existing party identities, moves and PP.");
        AssertStructure(before.Inventory, session.State.Inventory, "Flash does not consume inventory.");
        AssertStable(session.State);
        AssertStrictContinue(session);
    }

    private CampaignSession AtLadder(bool fromBasement, bool lit, Direction facing)
    {
        var session = Place(WithdrawExistingFlashOwner(), fromBasement ? Basement : FirstFloor,
            fromBasement ? 38 : 45, fromBasement ? 29 : 3, facing);
        Assert.AreEqual(4, session.State.FlashLevel);
        if (lit) UseFlash(session);
        Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
        Assert.AreEqual(CampaignQuestLogEvent.PartyOrder, session.State.QuestLog!.Scenes[^1].Actions.Single().Event);
        Assert.AreEqual(lit ? 0 : 4, session.State.QuestLog.Scenes[^1].FlashLevel);
        return session;
    }

    private void AssertSourceRoute()
    {
        var first = new MapId(FirstFloor); var basement = new MapId(Basement);
        Assert.AreEqual(new FieldWarp(1, 45, 2, 3, basement, 0), content.Maps[first].FieldWarps[1]);
        Assert.AreEqual(new FieldWarp(0, 38, 28, 3, first, 1), content.Maps[basement].FieldWarps[0]);
        Assert.AreEqual(new FieldMetatileEdit(45, 2, 0x2af, 0, 3), content.SourceMetatileAt(first, 45, 2));
        Assert.AreEqual(new FieldMetatileEdit(38, 28, 0x296, 0, 3), content.SourceMetatileAt(basement, 38, 28));
        foreach (var (map, x, y) in new[] { (first, 45, 2), (basement, 38, 28) })
        {
            var definition = content.MapDefinition(map);
            Assert.IsTrue(definition.RequiresFlash);
            Assert.AreEqual("MAP_TYPE_UNDERGROUND", definition.MapType);
            Assert.AreEqual("MAPSEC_ROCK_TUNNEL", definition.RegionMapSection);
            Assert.AreEqual("WEATHER_NONE", definition.Weather);
            Assert.AreEqual(0x61, content.Maps[map].SourceBehaviorAt(x, y));
            Assert.AreEqual(0x08, content.Maps[map].SourceBehaviorAt(x, y + 1));
            Assert.AreEqual(3, content.Maps[map].GetTile(x, y + 1)!.Elevation);
            Assert.IsFalse(content.Maps[map].GetTile(x, y + 1)!.Blocked);
        }
    }

    private static void AssertStable(CampaignState state)
    {
        Assert.IsTrue(state.Script.Completed);
        Assert.IsFalse(state.Script.Locked);
        Assert.IsNull(state.Script.Wait);
        Assert.IsNull(state.Dialogue);
        Assert.IsNull(state.WildBattle);
        Assert.IsTrue(state.EntryPrograms.IsEmpty);
        Assert.IsTrue(state.Motions.IsEmpty);
    }

    private static CampaignQuestLogAction Marker(CampaignState state) =>
        state.QuestLog!.Scenes[^1].Actions.Last(action => action.Transition is not null);

    [TestMethod]
    [DataRow(false, false, Direction.North, 0)]
    [DataRow(true, false, Direction.East, 7)]
    [DataRow(false, true, Direction.East, 9)]
    [DataRow(true, true, Direction.North, 0)]
    public void PublicDarkAndLitLaddersCaptureVisibilityAndStrictlyContinue(bool fromBasement, bool lit,
        Direction initialFacing, int musicWait)
    {
        AssertSourceRoute();
        var session = AtLadder(fromBasement, lit, initialFacing);
        var origin = session.State;
        var from = origin.World.Player;
        var destination = new MapId(fromBasement ? FirstFloor : Basement);
        var target = fromBasement ? (X: 45, Y: 2) : (X: 38, Y: 28);
        int level = lit ? 0 : 4;
        var expectedRoamer = SourceRoamerRules.Transition(origin.Roamer!, RoamerTransition.Warp,
            content.CelioRoamerMaps![destination], origin.World.Rng);
        Send(session, new("move", Direction.North));
        var state = session.State;
        var receipt = state.FieldTransition;
        Assert.IsNotNull(receipt);
        Assert.AreEqual(FieldTransitionCause.PlainWarp, receipt.Cause);
        Assert.AreEqual(FieldTransitionDeparture.StepOntoWarp, receipt.Departure);
        Assert.AreEqual(FieldTransitionArrival.Plain, receipt.Arrival);
        Assert.AreEqual(from, receipt.From);
        Assert.AreEqual(new TilePosition(from.MapId, from.X, from.Y - 1, 3, Direction.North), receipt.DepartureStep);
        Assert.AreEqual(new TilePosition(destination, target.X, target.Y, 3, Direction.North), receipt.To);
        Assert.AreEqual(receipt.To, receipt.ArrivalAnchor);
        Assert.AreEqual(receipt.To, state.World.Player);
        Assert.AreEqual(level, state.FlashLevel);
        Assert.AreEqual(lit, state.Script.Flags.GetValueOrDefault(FieldFlashSourcePresentation.ActiveFlag));
        Assert.AreEqual(origin.World.StepCount + 1, state.World.StepCount);
        Assert.AreEqual(origin.World.Revision + 1, state.World.Revision);
        Assert.AreEqual(expectedRoamer.Rng, state.World.Rng);
        AssertStructure(expectedRoamer.State, state.Roamer, "Only the real map-entry roamer transition runs.");
        AssertStructure(origin.Party, state.Party, "The ladder preserves the party.");
        AssertStructure(origin.Inventory, state.Inventory, "The ladder preserves inventory.");
        AssertStable(state);
        Assert.IsTrue(session.CanPresentFieldTransition(origin, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(origin, receipt with { }));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsCycling = true }, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsSurfing = true }, receipt));
        Assert.AreEqual(FieldTransitionInterlude.None, session.FieldTransitionInterludeFor(receipt));
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(origin, receipt));

        var marker = Marker(state);
        Assert.AreEqual(level, state.QuestLog!.Scenes[^1].FlashLevel);
        Assert.AreEqual(level, marker.Transition!.FlashLevel);
        Assert.IsNull(marker.TransitionTiming);
        var unknown = new CampaignQuestLogPlayback(new([state.QuestLog.Scenes[^1]]));
        unknown.Tick(marker.StartFrame / 60d);
        Assert.IsNull(unknown.ActiveTransitionTiming);
        Assert.AreEqual(destination.Value, unknown.Map);
        Assert.AreEqual(level, unknown.FlashLevel, "Captured visibility does not fabricate missing timing.");
        int change = FieldDoorPresentation.MinimumStepDepartureFrames + musicWait;
        int end = change + FieldWarpPresentation.PlainArrivalFrames;
        long revision = session.Revision;
        string unchanged = JsonSerializer.Serialize(state);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt with { }, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end - 1));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end + 1));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change + 1, end + 1));

        var saved = session.PrepareFieldSave();
        var recorded = Marker(saved);
        var timing = recorded.TransitionTiming;
        Assert.IsNotNull(timing);
        Assert.AreEqual(FieldTransitionCause.PlainWarp, timing.Cause);
        Assert.AreEqual(FieldTransitionDeparture.StepOntoWarp, timing.Departure);
        Assert.AreEqual(FieldTransitionArrival.Plain, timing.Arrival);
        Assert.AreEqual(new CampaignQuestLogDoorAnchor(from.X, from.Y - 1), timing.DepartureDoor);
        Assert.IsNull(timing.ArrivalDoor);
        Assert.AreEqual(change, timing.SwitchFrame);
        Assert.AreEqual(end, timing.EndFrame);
        Assert.AreEqual(level, saved.QuestLog!.Scenes[^1].FlashLevel);
        Assert.AreEqual(level, recorded.Transition!.FlashLevel);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog, content.QuestLogSource, content));
        Assert.IsNull(session.ValidatePlayerSave(saved));
        Assert.AreEqual(JsonSerializer.Serialize(state with { QuestLog = saved.QuestLog }), JsonSerializer.Serialize(saved));
        var decoded = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(saved, revision, session.RulesVersion, content.ContentVersion),
            session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        AssertStructure(saved, restored.State, "Strict Continue preserves all observed timing and darkness facts.");
        Assert.IsFalse(restored.CompleteFieldTransitionPresentation(receipt, change, end));
        var playback = new CampaignQuestLogPlayback(new([restored.State.QuestLog!.Scenes[^1]]));
        playback.Tick(recorded.StartFrame / 60d);
        var departure = new FieldDoorPresentation.StepDeparture();
        var arrival = new FieldWarpPresentation.PlainArrival();
        for (int frame = 0; frame < end; frame++)
        {
            Assert.AreEqual(timing, playback.ActiveTransitionTiming);
            var expected = frame < change ? receipt.From : receipt.To;
            Assert.AreEqual(expected.MapId.Value, playback.Map);
            Assert.AreEqual(level, playback.FlashLevel);
            var player = playback.Actors.Single(actor => actor.Id == CampaignQuestLogRecorder.PlayerId);
            Assert.AreEqual((expected.X, expected.Y, expected.Facing), (player.X, player.Y, player.Facing));
            var live = frame < change ? departure.Sample : FieldDoorPresentation.FromPlain(arrival.Sample);
            Assert.AreEqual(live, FieldDoorPresentation.SampleRecorded(timing.Cause, timing.Arrival,
                change, frame, null, null, timing.EffectiveDeparture));
            Assert.IsTrue(live.PlayerVisible);
            Assert.IsFalse(live.WipeActive);
            Assert.IsFalse(live.DoorAnimationActive);
            if (frame >= change) Assert.AreEqual(0, live.WalkPixels);
            if (frame < change) departure.Advance(frame + 1 >= change - 1);
            else arrival.Advance();
            playback.Tick(1d / 60);
        }
        Assert.IsTrue(departure.Complete);
        Assert.IsTrue(arrival.Complete);
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.AreEqual(level, playback.FlashLevel);
        Assert.AreSame(state, session.State);
        Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State), "Observation and recap do not change position, steps, RNG or any gameplay state.");
        Assert.IsNull(marker.TransitionTiming);
    }

    [TestMethod]
    public void HistoricalDarkLadderRemainsDarkAfterPublicFlashAndLegacyUnknownIsNeverInferred()
    {
        var session = AtLadder(false, false, Direction.North);
        Send(session, new("move", Direction.North));
        var receipt = session.State.FieldTransition!;
        int change = FieldDoorPresentation.MinimumStepDepartureFrames;
        int end = change + FieldWarpPresentation.PlainArrivalFrames;
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        UseFlash(session);
        var saved = session.PrepareFieldSave();
        var decoded = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(saved, session.Revision, session.RulesVersion, content.ContentVersion),
            session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        Assert.AreEqual(0, restored.State.FlashLevel);
        var history = restored.State.QuestLog!.Scenes.Single(scene => scene.Actions.Any(action => action.Transition is not null));
        var marker = history.Actions.Single(action => action.Transition is not null);
        Assert.AreEqual(4, history.FlashLevel);
        Assert.AreEqual(4, marker.Transition!.FlashLevel);
        var present = CampaignQuestLogRecorder.SavedScene(restored.State, content.QuestLogSource);
        Assert.AreEqual(0, present.FlashLevel);
        var playback = new CampaignQuestLogPlayback(new([history]), savedScene: present);
        Assert.AreEqual(4, playback.FlashLevel);
        playback.Tick((marker.StartFrame + change) / 60d);
        Assert.AreEqual(Basement, playback.Map);
        Assert.AreEqual(4, playback.FlashLevel, "Current Flash must not illuminate a recorded dark destination.");
        playback.NextScene();
        Assert.IsTrue(playback.SavedGameVisible);
        Assert.AreEqual(0, playback.FlashLevel);

        var legacy = history with
        {
            FlashLevel = null,
            Actions = history.Actions.Select(action => action.Transition is { } projection
                ? action with { Transition = projection with { FlashLevel = null } } : action).ToImmutableArray()
        };
        string json = JsonSerializer.Serialize(new CampaignQuestLog([legacy]));
        Assert.IsFalse(json.Contains("FlashLevel", StringComparison.Ordinal));
        var legacyLog = JsonSerializer.Deserialize<CampaignQuestLog>(json)!;
        Assert.IsNull(CampaignQuestLogRecorder.Validate(legacyLog, content.QuestLogSource, content));
        var unknown = new CampaignQuestLogPlayback(legacyLog, savedScene: present);
        Assert.IsNull(unknown.FlashLevel);
        unknown.Tick((marker.StartFrame + change) / 60d);
        Assert.AreEqual(Basement, unknown.Map);
        Assert.IsNull(unknown.FlashLevel, "Neither a Flash-map identity nor today's saved level may fill a missing historical fact.");
        unknown.NextScene();
        Assert.IsTrue(unknown.SavedGameVisible);
        Assert.AreEqual(0, unknown.FlashLevel);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FlashCaveEntranceAndNorthExitRemainOutsideTheLadderCapability(bool exiting)
    {
        var session = exiting ? Place(checkpoint.State, FirstFloor, 17, 3) : Place(checkpoint.State, Outside, 8, 20);
        Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
        var origin = session.State;
        var warp = content.Maps[origin.World.Player.MapId].FieldWarps[0];
        Assert.AreEqual(exiting ? (17, 2) : (8, 19), (warp.X, warp.Y));
        Assert.AreEqual(exiting ? 0x61 : 0x60, content.Maps[origin.World.Player.MapId].SourceBehaviorAt(warp.X, warp.Y));
        Send(session, new("move", Direction.North));
        var receipt = session.State.FieldTransition;
        Assert.IsNotNull(receipt);
        Assert.AreEqual(exiting ? Outside : FirstFloor, receipt.To.MapId.Value);
        Assert.AreEqual(exiting ? FieldTransitionCause.PlainWarp : FieldTransitionCause.NonAnimatedDoor, receipt.Cause);
        Assert.AreEqual(FieldTransitionDeparture.StepOntoWarp, receipt.Departure);
        Assert.AreEqual(exiting ? FieldTransitionArrival.NonAnimatedDoor : FieldTransitionArrival.Plain, receipt.Arrival);
        Assert.AreEqual(exiting ? 0 : 4, session.State.FlashLevel);
        Assert.IsFalse(session.CanPresentFieldTransition(origin, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(receipt));
        Assert.AreEqual(0, session.FieldTransitionPreviewDuration(origin, receipt));
        int change = FieldDoorPresentation.MinimumStepDepartureFrames;
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change,
            change + FieldDoorPresentation.ArrivalFrames(receipt.Arrival)));
        Assert.IsNull(Marker(session.State).TransitionTiming);
        AssertStrictContinue(session);
    }
}
