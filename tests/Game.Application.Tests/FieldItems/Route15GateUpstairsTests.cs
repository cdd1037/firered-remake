using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.FieldItems;

/// <summary>Controlled positions exercise the original gate scripts through public commands.</summary>
[TestClass]
public sealed class Route15GateUpstairsTests : WildBattleFixture
{
    private const string Downstairs = "map.route-15-west-entrance-1f";
    private const string Upstairs = "map.route-15-west-entrance-2f";
    private const string RewardFlag = "FLAG_GOT_EXP_SHARE_FROM_OAKS_AIDE";
    private const string Explanation = "Route15_WestEntrance_2F_Text_ExplainExpShare";
    private const int Articuno = 144;
    private static readonly ItemId ExpShare = new("pret:ITEM_EXP_SHARE");

    private void Set(CampaignSession session, CampaignState state) => typeof(CampaignSession)
        .GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(session, new CommandSession<CampaignState, CampaignCommand>(state,
            session.RulesVersion, content.ContentVersion, session.Revision));

    private void Place(CampaignSession session, CampaignState state, string mapName, int x, int y, Direction facing)
    {
        var map = new MapId(mapName);
        state = state with
        {
            World = state.World with { Player = new(map, x, y, content.Maps[map].GetTile(x, y)!.Elevation, facing) },
            Script = StoryState.Empty with
            {
                Flags = state.Script.Flags, Variables = state.Script.Variables,
                EffectSequence = state.Script.EffectSequence
            },
            Dialogue = null, ItemMenu = null, MonPicture = null, Motions = [], PresentationTicks = 0,
            PresentationEffects = [], EntryPrograms = [], ArrivalFacing = null,
            IsSurfing = false, IsCycling = false, BicycleSlope = false, MapEdits = null
        };
        var enter = typeof(CampaignSession).GetMethod("EnterMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pump = typeof(CampaignSession).GetMethod("Pump", BindingFlags.Instance | BindingFlags.NonPublic)!;
        state = (CampaignState)enter.Invoke(session, [state, null, Game.Domain.Roaming.RoamerTransition.Warp])!;
        Set(session, (CampaignState)pump.Invoke(session, [state])!);
        FinishConversation(session);
    }

    private HashSet<string> FinishConversation(CampaignSession session, int choice = 1)
    {
        var labels = new HashSet<string>(StringComparer.Ordinal);
        for (int n = 0; n < 300; n++)
        {
            if (session.State.Dialogue is { } dialogue) labels.Add(dialogue.TextSymbol);
            if (session.State.Dialogue is null && session.State.Script.Completed && session.State.EntryPrograms.IsEmpty)
                return labels;
            Send(session, session.State.Script.Wait?.Kind == StoryWaitKind.ButtonPress
                ? new("button_press", Text: "A")
                : new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? choice : null));
        }
        Assert.Fail("The source gate conversation did not finish.");
        return labels;
    }

    private CampaignSession VisitAide(int? caught = null, bool fullBag = false)
    {
        var session = CampaignSession.CreateCurrent(content);
        var state = FieldFixture();
        // Boundary cases deliberately vary only the dex inputs. Persistence cases keep
        // the complete recorded ownership and post-game prerequisites of the checkpoint.
        if (caught is { } count)
            state = state with
            {
                CaughtSpecies = Enumerable.Range(1, count).ToImmutableSortedSet(),
                SeenSpecies = state.SeenSpecies.Union(Enumerable.Range(1, count))
            };
        var bag = state.Inventory!.BagItems.RemoveAll(stack => stack.Item == ExpShare);
        if (fullBag)
            bag = content.Inventory.Items.Values.Where(item => item.Pocket == "POCKET_ITEMS" && item.Id != ExpShare)
                .OrderBy(item => item.Id.Value, StringComparer.Ordinal).Take(content.Inventory.Limits.BagItemSlots)
                .Select(item => new InventoryStack(item.Id, 1)).ToImmutableArray();
        state = state with
        {
            Inventory = state.Inventory with { BagItems = bag },
            Script = state.Script with
            {
                Flags = state.Script.Flags.SetItem(RewardFlag, false).SetItem("FLAG_SYS_ON_CYCLING_ROAD", false),
                Variables = state.Script.Variables.SetItem("VAR_MAP_SCENE_ROUTE16", 0)
            }
        };
        Place(session, state, Upstairs, 5, 6, Direction.North);
        return session;
    }

    private CampaignSession Continue(CampaignSession session)
    {
        StrictRoundtrip(session);
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion,
            validate: session.ValidatePlayerSave);
        return CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
    }

    private void ClimbStairs(CampaignSession session)
    {
        Place(session, session.State, Downstairs, 8, 10, Direction.East);
        Send(session, new("move", Direction.East)); FinishConversation(session);
        Send(session, new("move", Direction.East)); FinishConversation(session);
        Assert.AreEqual(Upstairs, session.State.World.Player.MapId.Value);
    }

    [TestMethod]
    public void OriginalStairsWorkBothWaysAndContinueOnEachFloor()
    {
        var session = VisitAide();
        ClimbStairs(session);
        Assert.IsFalse(session.State.IsCycling);
        Assert.AreEqual(1, session.State.Actors.Count);
        var aide = session.State.Actors.Values.Single();
        Assert.AreEqual(1, aide.SourceLocalId);
        Assert.AreEqual(5, aide.X);
        Assert.AreEqual(5, aide.Y);
        Assert.AreEqual(Direction.North, aide.Facing);
        Assert.AreEqual("Route15_WestEntrance_2F_EventScript_Aide", aide.ScriptId);
        session = Continue(session);
        Send(session, new("move", Direction.West)); FinishConversation(session);
        Send(session, new("move", Direction.West)); FinishConversation(session);
        Assert.AreEqual(Downstairs, session.State.World.Player.MapId.Value);
        session = Continue(session);
        ClimbStairs(session);
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void PublicStairsBothWaysPublishAuthenticPresentationReceiptsAndStrictRecordings()
    {
        var session = CampaignSession.CreateCurrent(content);
        Place(session, FieldFixture(), Downstairs, 8, 10, Direction.East);
        session = Continue(session);
        Assert.IsTrue(session.CanReorderFieldParty);
        // Start a real recording through an accepted gameplay event. No scene,
        // transition marker or presentation observation is injected into the fixture.
        Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
        Assert.AreEqual(Downstairs, session.State.QuestLog!.Scenes[^1].Map);
        Assert.AreEqual(CampaignQuestLogEvent.PartyOrder, session.State.QuestLog.Scenes[^1].Actions.Single().Event);

        var initial = session.State.World;
        var expectedUp = SourceRoamerRules.Transition(session.State.Roamer!, RoamerTransition.Warp,
            content.CelioRoamerMaps![new(Upstairs)], initial.Rng);
        var expectedDown = SourceRoamerRules.Transition(expectedUp.State, RoamerTransition.Warp,
            content.CelioRoamerMaps![new(Downstairs)], expectedUp.Rng);
        Send(session, new("move", Direction.East));
        Assert.AreEqual(new TilePosition(new(Downstairs), 9, 10,
            content.Maps[new MapId(Downstairs)].GetTile(9, 10)!.Elevation, Direction.East), session.State.World.Player);
        Assert.IsNull(session.State.FieldTransition);
        Assert.AreEqual(initial.StepCount + 1, session.State.World.StepCount);
        Assert.AreEqual(initial.Rng, session.State.World.Rng);

        ObservePublicStair(session, Direction.East, Upstairs, 10, 9, Direction.East, 0);
        // The source stair first turns the avatar without walking or warping.
        // The next input takes the reciprocal warp from its actual arrival cell.
        var upstairs = session.State.World;
        Send(session, new("move", Direction.West));
        Assert.AreEqual(upstairs.Player with { Facing = Direction.West }, session.State.World.Player);
        Assert.IsNull(session.State.FieldTransition);
        Assert.AreEqual(upstairs.StepCount, session.State.World.StepCount);
        Assert.AreEqual(upstairs.Rng, session.State.World.Rng);
        ObservePublicStair(session, Direction.West, Downstairs, 9, 10, Direction.West, 7);
        Assert.AreEqual(initial.StepCount + 1, session.State.World.StepCount,
            "Only approaching the lower stair is a gameplay step; neither stair presentation adds one.");
        Assert.AreEqual(expectedDown.Rng, session.State.World.Rng,
            "Only the two existing map-entry roamer transitions may consume RNG.");
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expectedDown.State),
            JsonSerializer.SerializeToElement(session.State.Roamer)));
    }

    private void ObservePublicStair(CampaignSession session, Direction direction, string destinationName,
        int destinationX, int destinationY, Direction arrivalFacing, int musicWait)
    {
        var origin = session.State;
        var from = origin.World.Player;
        var sourceMap = content.Maps[from.MapId];
        var sourceWarp = sourceMap.FieldWarps.Single(warp => warp.X == from.X && warp.Y == from.Y);
        var destination = new MapId(destinationName);
        var destinationMap = content.Maps[destination];
        var arrivalWarp = destinationMap.FieldWarps[sourceWarp.DestinationWarpId];
        Assert.AreEqual(destination, sourceWarp.DestinationMapId);
        Assert.AreEqual((destinationX, destinationY), (arrivalWarp.X, arrivalWarp.Y));
        Assert.AreEqual(from.MapId, arrivalWarp.DestinationMapId);
        Assert.AreEqual(sourceWarp.Index, arrivalWarp.DestinationWarpId);
        int? departureBehavior = sourceMap.SourceBehaviorAt(from.X, from.Y);
        int? arrivalBehavior = destinationMap.SourceBehaviorAt(destinationX, destinationY);
        Assert.IsTrue(departureBehavior is >= 0x6c and <= 0x6f);
        Assert.IsTrue(arrivalBehavior is >= 0x6c and <= 0x6f);
        var expectedRoamer = SourceRoamerRules.Transition(origin.Roamer!, RoamerTransition.Warp,
            content.CelioRoamerMaps![destination], origin.World.Rng);

        Send(session, new("move", direction));
        var state = session.State;
        var receipt = state.FieldTransition;
        Assert.IsNotNull(receipt, "The public stair command must retain its actual accepted receipt.");
        Assert.AreEqual(FieldTransitionCause.StairWarp, receipt.Cause);
        Assert.AreEqual(FieldTransitionDeparture.Immediate, receipt.Departure);
        Assert.AreEqual(FieldTransitionArrival.Stair, receipt.Arrival);
        Assert.IsNull(receipt.DepartureStep);
        Assert.AreEqual(from, receipt.From);
        Assert.AreEqual(new TilePosition(destination, destinationX, destinationY,
            destinationMap.GetTile(destinationX, destinationY)!.Elevation, arrivalFacing), receipt.To);
        Assert.AreEqual(new TilePosition(destination, destinationX, destinationY,
            arrivalWarp.Elevation, arrivalFacing), receipt.ArrivalAnchor);
        Assert.AreEqual(receipt.To, state.World.Player);
        Assert.AreEqual(departureBehavior, receipt.DepartureStairBehavior);
        Assert.AreEqual(arrivalBehavior, receipt.ArrivalStairBehavior);
        Assert.AreEqual(expectedRoamer.Rng, state.World.Rng,
            "The accepted warp performs one real roamer transition; presentation must add no draws.");
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expectedRoamer.State),
            JsonSerializer.SerializeToElement(state.Roamer)));
        Assert.AreEqual(origin.World.StepCount, state.World.StepCount);
        Assert.IsTrue(state.Script.Completed);
        Assert.IsFalse(state.Script.Locked);
        Assert.IsNull(state.Script.Wait);
        Assert.IsTrue(state.EntryPrograms.IsEmpty);
        Assert.IsNull(state.Dialogue);
        Assert.IsTrue(state.Motions.IsEmpty);
        Assert.IsTrue(session.CanPresentFieldTransition(origin, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(receipt), "Stairs require their stable walking origin.");
        Assert.IsFalse(session.CanPresentFieldTransition(origin, receipt with { }));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsCycling = true }, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsSurfing = true }, receipt));

        var marker = state.QuestLog!.Scenes[^1].Actions.Last(action => action.Transition is not null);
        Assert.IsNull(marker.TransitionTiming);
        var unobserved = new CampaignQuestLogPlayback(new([state.QuestLog.Scenes[^1]]));
        unobserved.Tick(marker.StartFrame / 60d);
        Assert.IsNull(unobserved.ActiveTransitionTiming);
        Assert.AreEqual(receipt.To.MapId.Value, unobserved.Map,
            "An authentic unobserved marker remains unknown and switches immediately.");
        int change = FieldStairPresentation.MinimumDepartureFrames + musicWait;
        int end = change + FieldStairPresentation.ArrivalFrames;
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt with { }, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt,
            FieldStairPresentation.MinimumDepartureFrames - 1,
            FieldStairPresentation.MinimumDepartureFrames - 1 + FieldStairPresentation.ArrivalFrames));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end - 1));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end + 1));
        long revision = session.Revision;
        var unchanged = JsonSerializer.SerializeToElement(state);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change + 1, end + 1));

        var saved = session.PrepareFieldSave();
        var recordedMarker = saved.QuestLog!.Scenes[^1].Actions.Last(action => action.Transition is not null);
        var timing = recordedMarker.TransitionTiming;
        Assert.IsNotNull(timing);
        Assert.AreEqual(receipt.Cause, timing.Cause);
        Assert.AreEqual(receipt.Arrival, timing.Arrival);
        Assert.AreEqual(departureBehavior, timing.DepartureStairBehavior);
        Assert.AreEqual(arrivalBehavior, timing.ArrivalStairBehavior);
        Assert.IsNull(timing.DepartureDoor);
        Assert.IsNull(timing.ArrivalDoor);
        Assert.AreEqual(change, timing.SwitchFrame);
        Assert.AreEqual(end, timing.EndFrame);
        Assert.AreEqual(end, recordedMarker.Frames);
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state with { QuestLog = saved.QuestLog }),
            JsonSerializer.SerializeToElement(saved)));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog, content.QuestLogSource, content));
        Assert.IsNull(session.ValidatePlayerSave(saved));
        var recordedScene = saved.QuestLog.Scenes[^1];
        int markerIndex = recordedScene.Actions.IndexOf(recordedMarker);
        foreach (var malformed in new[]
        {
            timing with { DepartureStairBehavior = null }, timing with { DepartureStairBehavior = 0x70 },
            timing with { ArrivalStairBehavior = null }, timing with { ArrivalStairBehavior = 0x70 },
            timing with { DepartureStairBehavior = departureBehavior!.Value ^ 1 },
            timing with { ArrivalStairBehavior = arrivalBehavior!.Value ^ 1 },
            timing with { Cause = FieldTransitionCause.PlainWarp },
            timing with { Arrival = FieldTransitionArrival.Plain }
        })
        {
            var invalidScene = recordedScene with
            {
                Actions = recordedScene.Actions.SetItem(markerIndex, recordedMarker with { TransitionTiming = malformed })
            };
            var invalidLog = saved.QuestLog with
            {
                Scenes = saved.QuestLog.Scenes.SetItem(saved.QuestLog.Scenes.Length - 1, invalidScene)
            };
            Assert.AreEqual("save_quest_log_transition_timing_invalid",
                CampaignQuestLogRecorder.Validate(invalidLog, content.QuestLogSource, content),
                "Missing, invalid or wrong-facing stair behavior must not authenticate recorded phases.");
        }
        var bytes = SaveCodec.Encode(saved, revision, session.RulesVersion, content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion,
            validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(saved),
            JsonSerializer.SerializeToElement(restored.State)));
        Assert.IsFalse(restored.CompleteFieldTransitionPresentation(receipt, change, end),
            "Continue preserves recorded timing without inheriting the old live observation owner.");

        var scene = restored.State.QuestLog!.Scenes[^1];
        var playback = new CampaignQuestLogPlayback(new([scene]));
        playback.Tick(recordedMarker.StartFrame / 60d);
        for (int frame = 0; frame < end; frame++)
        {
            Assert.AreEqual(timing, playback.ActiveTransitionTiming);
            var expected = frame < change ? receipt.From : receipt.To;
            Assert.AreEqual(expected.MapId.Value, playback.Map);
            var player = playback.Actors.Single(actor => actor.Id == CampaignQuestLogRecorder.PlayerId);
            Assert.AreEqual((expected.X, expected.Y, expected.Facing), (player.X, player.Y, player.Facing));
            playback.Tick(1d / 60);
        }
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.AreEqual(receipt.To.MapId.Value, playback.Map);
        Assert.AreSame(state, session.State);
        Assert.AreEqual(revision, session.Revision);
        Assert.IsTrue(JsonElement.DeepEquals(unchanged, JsonSerializer.SerializeToElement(session.State)));
        Assert.IsNull(marker.TransitionTiming, "Save projection must not mutate the committed provisional marker.");
    }

    [TestMethod]
    [DataRow(40, 1, false)]
    [DataRow(49, 1, false)]
    [DataRow(50, 1, true)]
    [DataRow(50, 0, false)]
    public void AideUsesFiftyCaughtThresholdAndPlayerChoice(int caught, int choice, bool rewarded)
    {
        var session = VisitAide(caught);
        var inventory = CanonicalJson.Hash(session.State.Inventory);
        Send(session, new("interact"));
        Assert.AreEqual("Route15_WestEntrance_2F_Text_GiveItemIfCaughtEnough", session.State.Dialogue?.TextSymbol);
        Assert.AreEqual("50", session.State.TextBuffers.GetValueOrDefault("STR_VAR_1"));
        Assert.AreEqual("EXP. SHARE", session.State.TextBuffers.GetValueOrDefault("STR_VAR_2"));
        var labels = FinishConversation(session, choice);
        Assert.AreEqual(rewarded, session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        Assert.AreEqual(rewarded ? 1 : 0, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, ExpShare));
        Assert.IsTrue(labels.Contains(choice == 0 ? "Aide_Text_GetEnoughMonsComeBackForItem" : caught < 50
            ? "Aide_Text_HaventCaughtEnoughMonsForItem" : Explanation));
        Assert.AreEqual(Direction.South, session.State.Actors.Values.Single().Facing);
        if (!rewarded) Assert.AreEqual(inventory, CanonicalJson.Hash(session.State.Inventory));
    }

    [TestMethod]
    public void AideCountsKantoCaughtRatherThanSeenOrNationalSpecies()
    {
        var session = VisitAide(49);
        Set(session, session.State with
        {
            CaughtSpecies = session.State.CaughtSpecies.Add(175),
            SeenSpecies = session.State.SeenSpecies.Union(Enumerable.Range(1, 151)).Add(175)
        });
        Assert.AreEqual(50, session.State.CaughtSpecies.Count);
        Send(session, new("interact"));
        var labels = FinishConversation(session);
        Assert.IsTrue(labels.Contains("Aide_Text_HaventCaughtEnoughMonsForItem"));
        Assert.AreEqual("49", session.State.TextBuffers.GetValueOrDefault("STR_VAR_3"));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        Assert.AreEqual(0, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, ExpShare));
    }

    [TestMethod]
    public void FullBagFailureRetryAndRepeatRemainAtomicAfterContinueAndReentry()
    {
        var session = VisitAide(fullBag: true);
        Assert.AreEqual(content.Inventory.Limits.BagItemSlots, session.State.Inventory!.BagItems.Length);
        var inventory = CanonicalJson.Hash(session.State.Inventory);
        Send(session, new("interact"));
        var labels = FinishConversation(session);
        Assert.IsTrue(labels.Contains("Aide_Text_DontHaveAnyRoomForItem"));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        Assert.AreEqual(0, OpeningInventoryRules.Quantity(session.State.Inventory.BagItems, ExpShare));
        Assert.AreEqual(inventory, CanonicalJson.Hash(session.State.Inventory));
        session = Continue(session);

        Set(session, session.State with { Inventory = session.State.Inventory! with
            { BagItems = session.State.Inventory.BagItems.RemoveAt(0) } });
        Send(session, new("interact"));
        labels = FinishConversation(session);
        Assert.IsTrue(labels.Contains("Route15_WestEntrance_2F_Text_ReceivedItemFromAide"));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, ExpShare));
        Assert.AreEqual(content.Inventory.Limits.BagItemSlots, session.State.Inventory.BagItems.Length);
        session = Continue(session);
        ClimbStairs(session);
        Place(session, session.State, Upstairs, 5, 6, Direction.North);
        inventory = CanonicalJson.Hash(session.State.Inventory);
        Send(session, new("interact"));
        labels = FinishConversation(session);
        CollectionAssert.AreEquivalent(new[] { Explanation }, labels.ToArray());
        Assert.AreEqual(inventory, CanonicalJson.Hash(session.State.Inventory));
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, ExpShare));
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void GivenExpSharePersistsAndCannotBeClaimedAgainBeforeTakingItBack()
    {
        var session = VisitAide();
        Send(session, new("interact")); FinishConversation(session);
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, ExpShare));
        var instanceId = session.State.Party[0].InstanceId;
        Send(session, new("bag_open"));
        Send(session, new("field_give", PartyIndex: 0, Text: ExpShare.Value, Quantity: 1, Choice: 1)
            { TargetInstanceId = instanceId });
        Send(session, new("menu_back"));
        Assert.AreEqual("ITEM_EXP_SHARE", session.State.Party[0].HeldItem);
        Assert.AreEqual(0, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, ExpShare));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        session = Continue(session);
        ClimbStairs(session);
        Place(session, session.State, Upstairs, 5, 6, Direction.North);
        Send(session, new("interact"));
        var labels = FinishConversation(session);
        CollectionAssert.AreEquivalent(new[] { Explanation }, labels.ToArray());
        Assert.AreEqual(0, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, ExpShare));
        Assert.AreEqual("ITEM_EXP_SHARE", session.State.Party[0].HeldItem);
        Send(session, new("party_item_open", PartyIndex: 0) { TargetInstanceId = instanceId });
        Send(session, new("field_take", PartyIndex: 0) { TargetInstanceId = instanceId });
        Send(session, new("menu_back"));
        Assert.AreEqual("ITEM_NONE", session.State.Party[0].HeldItem);
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, ExpShare));
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory.KeyItems ?? [], MachineRules.TmCaseId));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(RewardFlag));
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow("A")]
    [DataRow("B")]
    public void LeftBinocularsShowArticunoThenMarkOnlySeenWithoutAnEncounter(string button)
    {
        var session = VisitAide();
        Assert.IsFalse(session.State.SeenSpecies.Contains(Articuno), "The checkpoint must leave the binocular discovery observable.");
        Assert.IsFalse(session.State.CaughtSpecies.Contains(Articuno));
        Place(session, session.State, Upstairs, 4, 2, Direction.North);
        var seen = session.State.SeenSpecies;
        var caught = session.State.CaughtSpecies;
        var rng = session.State.World.Rng;
        var exploration = CanonicalJson.Hash(session.State.Exploration);
        var party = CanonicalJson.Hash(session.State.Party);
        Send(session, new("interact"));
        Assert.AreEqual("Route15_WestEntrance_2F_Text_LargeShiningBird", session.State.Dialogue?.TextSymbol);
        for (int n = 0; n < 20 && session.State.Script.Wait?.Kind != StoryWaitKind.ButtonPress; n++)
        {
            Assert.IsFalse(session.State.SeenSpecies.Contains(Articuno));
            Send(session, new("ack"));
        }
        Assert.AreEqual(StoryWaitKind.ButtonPress, session.State.Script.Wait?.Kind);
        Assert.AreEqual(new CampaignMonPicture(Articuno, 10, 3), session.State.MonPicture);
        Assert.IsFalse(session.State.SeenSpecies.Contains(Articuno));
        Assert.IsNull(session.State.WildBattle);
        var before = CanonicalJson.Hash(session.State);
        var rejected = session.Execute("binoculars-ack-is-not-button", session.Revision, new("ack"));
        Assert.AreEqual(CommandStatus.Rejected, rejected.Status);
        Assert.AreEqual("button_press_required", rejected.Error);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State));
        Send(session, new("button_press", Text: button)); FinishConversation(session);
        Assert.IsNull(session.State.MonPicture);
        CollectionAssert.AreEqual(seen.Add(Articuno).ToArray(), session.State.SeenSpecies.ToArray());
        CollectionAssert.AreEqual(caught.ToArray(), session.State.CaughtSpecies.ToArray());
        Assert.IsFalse(session.State.CaughtSpecies.Contains(Articuno));
        Assert.IsNull(session.State.WildBattle);
        Assert.IsNull(session.State.SafariBattle);
        Assert.IsNull(session.State.ForestTrainerBattle);
        Assert.AreEqual(rng, session.State.World.Rng);
        Assert.AreEqual(exploration, CanonicalJson.Hash(session.State.Exploration));
        Assert.AreEqual(party, CanonicalJson.Hash(session.State.Party));
        session = Continue(session);
        Send(session, new("interact")); FinishConversation(session);
        CollectionAssert.AreEqual(seen.Add(Articuno).ToArray(), session.State.SeenSpecies.ToArray());
        CollectionAssert.AreEqual(caught.ToArray(), session.State.CaughtSpecies.ToArray());
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void RightBinocularsUseOriginalIslandTextWithoutChangingDex()
    {
        var session = VisitAide();
        Place(session, session.State, Upstairs, 8, 2, Direction.North);
        var seen = session.State.SeenSpecies;
        var caught = session.State.CaughtSpecies;
        Send(session, new("interact"));
        Assert.AreEqual("Route15_WestEntrance_2F_Text_SmallIslandOnHorizon", session.State.Dialogue?.TextSymbol);
        var labels = FinishConversation(session);
        CollectionAssert.AreEquivalent(new[] { "Route15_WestEntrance_2F_Text_SmallIslandOnHorizon" }, labels.ToArray());
        Assert.IsNull(session.State.MonPicture);
        CollectionAssert.AreEqual(seen.ToArray(), session.State.SeenSpecies.ToArray());
        CollectionAssert.AreEqual(caught.ToArray(), session.State.CaughtSpecies.ToArray());
        Assert.IsNull(session.State.WildBattle);
        StrictRoundtrip(session);
    }
}
