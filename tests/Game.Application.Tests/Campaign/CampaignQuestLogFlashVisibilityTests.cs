using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class CampaignQuestLogFlashVisibilityTests
{
    private const string FirstFloor = "map.rock-tunnel-1f";
    private const string Basement = "map.rock-tunnel-b1f";
    private static CampaignQuestLogAction Arrival() => new(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.Arrived, 127)
        { TextKey = "ArrivedInLocation", Arguments = ["ROCK TUNNEL"] };
    private static CampaignState At(int level, string map = FirstFloor)
    {
        var basis = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog).State;
        return basis with
        {
            World = basis.World with { Player = new(new(map), 6, 8, 3, Direction.North) },
            Actors = ImmutableSortedDictionary<string, CampaignActor>.Empty, ObjectSlots = null,
            MapLayout = null, MapEdits = null, Motions = [], FlashLevel = level,
            Script = basis.Script with { Flags = basis.Script.Flags.SetItem(FieldFlashSourcePresentation.ActiveFlag, level == 0) }
        };
    }
    private static CampaignQuestLog Begin(CampaignState state) => CampaignQuestLogRecorder.Record(null, state, state,
        new("move"), CurrentCampaignFixture.Catalog, suppliedEvent: Arrival())!;
    private static CampaignQuestLog Cross(CampaignState first, CampaignState second) => CampaignQuestLogRecorder.Record(
        Begin(first), first, second, new("move"), CurrentCampaignFixture.Catalog, recordingFrame: 10)!;

    [TestMethod]
    [DataRow(0)]
    [DataRow(4)]
    public void CaptureUsesActualStableVisibilityAndSavedSceneRemainsIndependent(int level)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var state = At(level);
        var log = Begin(state);
        var scene = log.Scenes.Single();
        Assert.AreEqual(level, scene.FlashLevel);
        Assert.AreEqual(level, CampaignQuestLogRecorder.SavedScene(state, catalog.QuestLogSource).FlashLevel);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log, catalog.QuestLogSource, catalog));

        var saved = state with { FlashLevel = level == 0 ? 4 : 0 };
        var playback = new CampaignQuestLogPlayback(log, "RED", catalog.QuestLogSource,
            CampaignQuestLogRecorder.SavedScene(saved, catalog.QuestLogSource));
        playback.Tick(1d / 60);
        Assert.AreEqual(level, playback.FlashLevel);
        playback.Skip();
        Assert.IsTrue(playback.SavedGameVisible);
        Assert.AreEqual(saved.FlashLevel, playback.FlashLevel);
        Assert.AreEqual(level, scene.FlashLevel);
        Assert.AreEqual(level, state.FlashLevel);
        Assert.AreSame(scene, log.Scenes.Single());
        Assert.AreSame(state.World, saved.World);
        Assert.AreSame(state.Script, saved.Script);
    }

    [TestMethod]
    [DataRow(4, 0)]
    [DataRow(0, 4)]
    public void ProjectionVisibilitySwitchesOnTheRecordedMapSwitch(int originLevel, int destinationLevel)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var first = At(originLevel);
        var second = At(destinationLevel, Basement);
        var log = Cross(first, second);
        var scene = log.Scenes.Single();
        var marker = scene.Actions.Single(a => a.Transition is not null);
        Assert.AreEqual(originLevel, scene.FlashLevel);
        Assert.AreEqual(destinationLevel, marker.Transition!.FlashLevel);
        // Controlled timeline evidence exercises the switch; real ladder admission
        // and the gameplay that owns its observation have separate route coverage.
        var timed = marker with { Frames = 48, TransitionTiming = new(FieldTransitionCause.PlainWarp, 24, 48) };
        log = new([scene with { Actions = scene.Actions.Replace(marker, timed) }]);
        log = JsonSerializer.Deserialize<CampaignQuestLog>(JsonSerializer.Serialize(log))!;
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log, catalog.QuestLogSource, catalog));
        var playback = new CampaignQuestLogPlayback(log, "RED", catalog.QuestLogSource);
        playback.Tick(33d / 60);
        Assert.AreEqual(FirstFloor, playback.Map);
        Assert.AreEqual(originLevel, playback.FlashLevel);
        Assert.AreEqual(0, playback.ArtRevision);
        playback.Tick(1d / 60);
        Assert.AreEqual(Basement, playback.Map);
        Assert.AreEqual(destinationLevel, playback.FlashLevel);
        Assert.AreEqual(1, playback.ArtRevision);
        Assert.AreEqual(originLevel, log.Scenes[0].FlashLevel);
    }

    [TestMethod]
    public void LegacyMissingFactsStayUnknownAndExplicitZeroSurvivesSerialization()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var log = Cross(At(4), At(0, Basement));
        var scene = log.Scenes[0];
        var marker = scene.Actions.Single(a => a.Transition is not null);
        var legacyMarker = marker with { Transition = marker.Transition! with { FlashLevel = null } };
        var legacyScene = scene with { FlashLevel = null, Actions = scene.Actions.Replace(marker, legacyMarker) };
        string json = JsonSerializer.Serialize(new CampaignQuestLog([legacyScene]));
        Assert.IsFalse(json.Contains("FlashLevel", StringComparison.Ordinal));
        var legacy = JsonSerializer.Deserialize<CampaignQuestLog>(json)!;
        Assert.IsNull(CampaignQuestLogRecorder.Validate(legacy, catalog.QuestLogSource, catalog));
        var playback = new CampaignQuestLogPlayback(legacy, "RED", catalog.QuestLogSource);
        Assert.IsNull(playback.FlashLevel);
        playback.Tick(10d / 60);
        Assert.AreEqual(Basement, playback.Map);
        Assert.IsNull(playback.FlashLevel);

        // A missing destination fact never inherits a known source mask.
        playback = new(new([legacyScene with { FlashLevel = 4 }]), "RED", catalog.QuestLogSource);
        Assert.AreEqual(4, playback.FlashLevel);
        playback.Tick(10d / 60);
        Assert.IsNull(playback.FlashLevel);
        var explicitZero = JsonSerializer.Deserialize<CampaignQuestLog>(JsonSerializer.Serialize(log))!;
        Assert.AreEqual(0, explicitZero.Scenes[0].Actions.Single(a => a.Transition is not null).Transition!.FlashLevel);
        Assert.IsTrue(JsonSerializer.Serialize(log).Contains("\"FlashLevel\":0", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(5)]
    public void TransientAndOutOfRangeLevelsCannotBecomeRecordedFacts(int level)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var log = Cross(At(4), At(0, Basement));
        var scene = log.Scenes[0];
        var marker = scene.Actions.Single(a => a.Transition is not null);
        var invalidScene = new CampaignQuestLog([scene with { FlashLevel = level }]);
        var invalidMarker = marker with { Transition = marker.Transition! with { FlashLevel = level } };
        var invalidProjection = new CampaignQuestLog([scene with { Actions = scene.Actions.Replace(marker, invalidMarker) }]);
        foreach (var invalid in new[] { invalidScene, invalidProjection })
        {
            Assert.AreEqual("save_quest_log_flash_level_invalid", CampaignQuestLogRecorder.Validate(invalid));
            Assert.AreEqual("save_quest_log_flash_level_invalid", CampaignQuestLogRecorder.Validate(invalid, catalog.QuestLogSource, catalog));
        }
        Assert.IsNull(Begin(At(level)));
    }

    [TestMethod]
    public void CatalogRejectsDarknessOnNonFlashMapsWithoutInferringMissingFacts()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var log = Cross(At(4), At(0, "map.pallet-town"));
        var scene = log.Scenes[0];
        var marker = scene.Actions.Single(a => a.Transition is not null);
        foreach (int? level in new int?[] { null, 0, 4 })
        {
            var ordinaryScene = new CampaignQuestLog([scene with { Map = "map.pallet-town", FlashLevel = level }]);
            var changed = marker with { Transition = marker.Transition! with { FlashLevel = level } };
            var ordinaryProjection = new CampaignQuestLog([scene with { Actions = scene.Actions.Replace(marker, changed) }]);
            foreach (var candidate in new[] { ordinaryScene, ordinaryProjection })
                Assert.AreEqual(level == 4 ? "save_quest_log_flash_level_invalid" : null,
                    CampaignQuestLogRecorder.Validate(candidate, catalog.QuestLogSource, catalog));
        }
    }

    [TestMethod]
    public void EveryPresentProjectionFactCostsOneByteWithinTheExistingVisualLimit()
    {
        var player = new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId, "player", 1, 1, Direction.South, true);
        var actors = Enumerable.Range(0, 16).Select(i => player with { Id = i == 0 ? player.Id : "actor" + i }).ToImmutableArray();
        var tiles = Enumerable.Range(0, 210).Select(i => new FieldMetatileEdit(i % 15, i / 15, 1, 0, 3)).ToImmutableArray();
        var legacy = new CampaignQuestLogScene(FirstFloor, actors, [Arrival()]) { Layout = "layout", Tiles = tiles };
        Assert.AreEqual(CampaignQuestLogRecorder.VisualMemoryLimit, CampaignQuestLogRecorder.VisualMemoryBudget(legacy));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(new([legacy])));
        foreach (int level in new[] { 0, 4 })
        {
            var over = legacy with { FlashLevel = level };
            Assert.AreEqual(CampaignQuestLogRecorder.VisualMemoryLimit + 1, CampaignQuestLogRecorder.VisualMemoryBudget(over));
            Assert.AreEqual("save_quest_log_visual_budget_invalid", CampaignQuestLogRecorder.Validate(new([over])));
        }
        var marker = new CampaignQuestLogAction(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.None, 1)
        { StartFrame = 1, Transition = new(Basement, actors, "layout", tiles.Take(204).ToImmutableArray()) { FlashLevel = 4 } };
        var exact = new CampaignQuestLogScene(FirstFloor, [player], [Arrival(), marker]) { FlashLevel = 0 };
        Assert.AreEqual(CampaignQuestLogRecorder.VisualMemoryLimit, CampaignQuestLogRecorder.VisualMemoryBudget(exact));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(new([exact])));
        Assert.AreEqual(CampaignQuestLogRecorder.VisualMemoryLimit - 1,
            CampaignQuestLogRecorder.VisualMemoryBudget(exact with { FlashLevel = null }));
        var absent = marker with { Transition = marker.Transition! with { FlashLevel = null } };
        Assert.AreEqual(CampaignQuestLogRecorder.VisualMemoryLimit - 2,
            CampaignQuestLogRecorder.VisualMemoryBudget(exact with { FlashLevel = null, Actions = [Arrival(), absent] }));
    }

    [TestMethod]
    public void NewlyCapturedFactCannotOverflowAnOtherwiseFullInitialProjection()
    {
        var state = At(0, "map.pallet-town");
        var actors = Enumerable.Range(1, 15).ToImmutableSortedDictionary(i => "actor" + i,
            i => new CampaignActor("actor" + i, i, "OBJ_EVENT_GFX_MAN", 1, 1, 3, Direction.South,
                "MOVEMENT_TYPE_FACE_DOWN", "0", "0", true, false, 1, 1));
        var tiles = Enumerable.Range(0, 210).ToImmutableSortedDictionary(i => i.ToString(),
            i => new FieldMetatileEdit(i % 15, i / 15, 1, 0, 3));
        state = state with { Actors = actors, MapEdits = tiles, MapLayout = "LAYOUT_PALLET_TOWN" };
        var snapshot = CampaignQuestLogRecorder.SavedScene(state, CurrentCampaignFixture.Catalog.QuestLogSource);
        Assert.AreEqual(16, snapshot.Actors.Length);
        Assert.AreEqual(CampaignQuestLogRecorder.VisualMemoryLimit + 1, CampaignQuestLogRecorder.VisualMemoryBudget(snapshot));
        Assert.IsNull(Begin(state));
    }

    [TestMethod]
    public void FlashFieldMoveClosesTheOriginalSceneWithoutInventingAnActionTrack()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var before = At(4);
        var started = before with { FlashEffect = new(0, 1, 25, "PIKACHU", before.World.Player.MapId,
            FieldFlashSourcePresentation.ShowMon, "flash-record-test", 4) };
        var log = CampaignQuestLogRecorder.Record(Begin(before), before, started, new("field_flash"), catalog)!;
        Assert.IsTrue(log.Scenes[0].Closed);
        Assert.AreEqual(4, log.Scenes[0].FlashLevel);
        Assert.AreEqual(2, log.Scenes[0].Actions.Length);
        Assert.AreEqual("UsedFlash", log.Scenes[0].Actions[^1].TextKey);
        var completed = started with { FlashEffect = null, FlashLevel = 0 };
        Assert.AreSame(log, CampaignQuestLogRecorder.Record(log, started, completed, new("field_flash_complete"), catalog));
        Assert.AreEqual(0, CampaignQuestLogRecorder.SavedScene(completed, catalog.QuestLogSource).FlashLevel);

        var open = Begin(before);
        var cut = CampaignQuestLogRecorder.Record(open, before, before with { FlashLevel = 0 }, new("ack"), catalog)!;
        Assert.IsTrue(cut.Scenes[0].Closed);
        Assert.AreEqual(4, cut.Scenes[0].FlashLevel);
        Assert.AreEqual(open.Scenes[0].Actions, cut.Scenes[0].Actions);
    }

    [TestMethod]
    public void CampaignSaveRoundTripKeepsHistoricalFactsSeparateFromCurrentVisibility()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var saved = CurrentCampaignFixture.Checkpoint;
        var log = Cross(At(4), At(0, Basement));
        var state = saved.State with { QuestLog = log };
        var session = CampaignSession.RestoreCurrent(catalog, state, saved.Generation);
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, catalog.ContentVersion);
        var loaded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, catalog.ContentVersion,
            validate: session.ValidatePlayerSave);
        var historical = loaded.State.QuestLog!;
        Assert.AreEqual(4, historical.Scenes[0].FlashLevel);
        Assert.AreEqual(0, historical.Scenes[0].Actions.Single(a => a.Transition is not null).Transition!.FlashLevel);
        Assert.AreEqual(state.FlashLevel, loaded.State.FlashLevel);
        Assert.AreEqual(state.World.Rng, loaded.State.World.Rng);
        Assert.AreEqual(state.Money, loaded.State.Money);
        Assert.AreEqual(saved.Generation, loaded.Generation);
        var playback = new CampaignQuestLogPlayback(historical, state.PlayerName, catalog.QuestLogSource,
            CampaignQuestLogRecorder.SavedScene(loaded.State, catalog.QuestLogSource));
        Assert.AreEqual(4, playback.FlashLevel);
        playback.Skip();
        Assert.AreEqual(loaded.State.FlashLevel, playback.FlashLevel);
        Assert.AreEqual(loaded.State.World.Player.MapId.Value, playback.Map);
        Assert.AreSame(historical, loaded.State.QuestLog);
    }
}
