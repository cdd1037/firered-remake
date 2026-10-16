using System.Collections.Immutable;
using System.Reflection;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>
/// Controlled current-checkpoint positions exercise public doors, conversations and strict Continue.
/// The two unavailable identity/story variants are explicitly isolated branch fixtures, not replayed saves.
/// </summary>
[TestClass, TestCategory("Game")]
public sealed class OrdinaryInteriorBatchFlowTests : WildBattleFixture
{
    private const string HomeHideFlag = "FLAG_HIDE_LOSTELLE_IN_HER_HOME";
    private static readonly (int Dx, int Dy, Direction Direction)[] Directions =
        [(0, -1, Direction.North), (1, 0, Direction.East), (-1, 0, Direction.West), (0, 1, Direction.South)];

    private CampaignState Place(CampaignState state, string mapName, int x, int y, Direction facing = Direction.North)
    {
        var map = content.Maps[new(mapName)];
        var definition = content.MapDefinition(map.Id);
        state = state with {
            World = state.World with { Player = new(map.Id, x, y, map.GetTile(x, y)!.Elevation, facing) },
            Actors = definition.Actors.ToImmutableSortedDictionary(a => a.LocalId,
                a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation,
                    Direction.South, a.MovementType, a.ScriptId, a.HideFlag,
                    false, false, a.X, a.Y), StringComparer.Ordinal),
            ObjectSlots = ImmutableSortedDictionary<int, string>.Empty.Add(0, "LOCALID_PLAYER"),
            SelectedObjectSlot = 0, LastTalked = null,
            MapEdits = null, MapLayout = null, FlashLevel = 0, IsSurfing = false, IsCycling = false,
            Music = definition.Music, EntryPrograms = [], ArrivalFacing = null,
            Dialogue = null, Motions = [], PresentationEffects = [], PresentationTicks = 0
        };
        // Production camera admission chooses the bounded local object pool. A town's full
        // template set can exceed sixteen actors, so do not activate every template in the fixture.
        return (CampaignState)typeof(CampaignSession).GetMethod("RefreshActors",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(validator, [state])!;
    }

    private CampaignSession Start(CampaignState state, bool isolatedBranch = false)
    {
        if (!isolatedBranch)
        {
            Assert.IsNull(validator.ValidatePlayerSave(state), "controlled room fixture must remain a valid current save");
            return CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
        }
        // There is no valid pre-rescue or female late-game checkpoint. Keep these two branch inputs
        // isolated from the strict-persistence cases rather than inventing rewritten ownership/history.
        var session = CampaignSession.CreateCurrent(content);
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, new CommandSession<CampaignState, CampaignCommand>(state,
                session.RulesVersion, content.ContentVersion, checkpoint.Generation));
        return session;
    }

    private CampaignSession Continue(CampaignSession session)
    {
        StrictRoundtrip(session);
        var decoded = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(session.State, session.Revision,
            session.RulesVersion, content.ContentVersion), session.RulesVersion, content.ContentVersion,
            validate: session.ValidatePlayerSave);
        return CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
    }

    private sealed record Transcript(HashSet<string> Texts, List<CampaignPresentation> Effects);

    private Transcript Finish(CampaignSession session)
    {
        var transcript = new Transcript(new(StringComparer.Ordinal), []);
        for (int n = 0; n < 150; n++)
        {
            if (session.State.Dialogue is { } dialogue) transcript.Texts.Add(dialogue.TextSymbol);
            transcript.Effects.AddRange(session.State.PresentationEffects);
            Assert.IsNull(session.State.WildBattle);
            Assert.IsNull(session.State.ForestTrainerBattle);
            if (session.State.Script.Completed && session.State.Script.Wait is null &&
                session.State.Dialogue is null && session.State.EntryPrograms.IsEmpty)
            {
                Assert.IsFalse(session.State.Script.Locked);
                return transcript;
            }
            Send(session, session.State.Script.Wait?.Kind == StoryWaitKind.ButtonPress
                ? new("button_press", Text: "A") : new("ack"));
        }
        Assert.Fail("ordinary interior source script did not finish");
        return transcript;
    }

    private void Step(CampaignSession session, Direction direction)
    {
        Send(session, new("move", Direction: direction));
        Finish(session);
    }

    private void WalkTo(CampaignSession session, int x, int y)
    {
        var map = content.ConnectedFieldMaps![session.State.World.Player.MapId];
        var start = (session.State.World.Player.X, session.State.World.Player.Y);
        var goal = (x, y);
        var warps = content.Maps[map.Id].FieldWarps.Select(w => (w.X, w.Y)).ToHashSet();
        var queue = new Queue<(int X, int Y)>(); queue.Enqueue(start);
        var seen = new HashSet<(int, int)> { start };
        var previous = new Dictionary<(int, int), ((int, int) Cell, Direction Direction)>();
        while (queue.TryDequeue(out var current) && current != goal)
            foreach (var direction in Directions)
            {
                var next = (current.X + direction.Dx, current.Y + direction.Dy);
                if (seen.Contains(next) || warps.Contains(next) || map.CellAt(next.Item1, next.Item2) is not { Collision: 0 } ||
                    session.State.Actors.Values.Any(a => a.Active && !a.Invisible && a.X == next.Item1 && a.Y == next.Item2)) continue;
                seen.Add(next); previous[next] = (current, direction.Direction); queue.Enqueue(next);
            }
        Assert.IsTrue(seen.Contains(goal), $"physical path in {map.Id.Value} from {start} to {goal}");
        var path = new Stack<Direction>();
        for (var current = goal; current != start; current = previous[current].Cell) path.Push(previous[current].Direction);
        foreach (var direction in path)
        {
            Step(session, direction);
            Assert.AreEqual(map.Id, session.State.World.Player.MapId);
        }
        Assert.AreEqual(goal, (session.State.World.Player.X, session.State.World.Player.Y));
    }

    private (int X, int Y, Direction Facing) Beside(string mapName, int x, int y)
    {
        var map = content.Maps[new(mapName)];
        var actors = content.MapDefinition(map.Id).Actors;
        var direction = Directions.First(d => map.GetTile(x - d.Dx, y - d.Dy) is { Blocked: false } &&
            !actors.Any(a => a.X == x - d.Dx && a.Y == y - d.Dy));
        return (x - direction.Dx, y - direction.Dy, direction.Direction);
    }

    private CampaignSession AtActor(string mapName, string script, CampaignState? basis = null, bool isolatedBranch = false)
    {
        var actor = content.MapDefinition(new(mapName)).Actors.Single(a => a.ScriptId == script);
        var beside = Beside(mapName, actor.X, actor.Y);
        return Start(Place(basis ?? FieldFixture(), mapName, beside.X, beside.Y, beside.Facing), isolatedBranch);
    }

    [TestMethod]
    [DataRow("map.viridian-school")]
    [DataRow("map.fuchsia-city-house-1")]
    [DataRow("map.three-island-house2")]
    [DataRow("map.viridian-house")]
    [DataRow("map.vermilion-city-house-3")]
    [DataRow("map.saffron-city-house")]
    [DataRow("map.one-island-house1")]
    [DataRow("map.one-island-house2")]
    [DataRow("map.three-island-house1")]
    [DataRow("map.three-island-house3")]
    [DataRow("map.three-island-house4")]
    [DataRow("map.three-island-house5")]
    [DataRow("map.five-island-house1")]
    [DataRow("map.five-island-house2")]
    [DataRow("map.six-island-house")]
    public void SourceExteriorDoorEntersAndPhysicallyReturnsAfterContinue(string mapName)
    {
        var room = content.Maps[new(mapName)];
        // Side records are source warp targets, but only the middle south-arrow metatile
        // is a physical walking exit. Keep the original three-record Kanto doors intact.
        var exit = room.FieldWarps.Single(w => room.SourceBehaviorAt(w.X, w.Y) == 0x65);
        var exterior = content.Maps[exit.DestinationMapId];
        var entrance = exterior.FieldWarps[exit.DestinationWarpId];
        var session = Start(Place(FieldFixture(), exterior.Id.Value, entrance.X, entrance.Y + 1));
        for (int n = 0; n < 3 && session.State.World.Player.MapId == exterior.Id; n++) Step(session, Direction.North);
        Assert.AreEqual(room.Id, session.State.World.Player.MapId);
        Assert.AreEqual(content.MapDefinition(room.Id).Music, session.State.Music);
        Assert.IsFalse(session.State.IsSurfing);
        Assert.IsFalse(session.State.IsCycling);
        session = Continue(session);
        // Walk to an actual source exit rather than relocating the player for the return leg.
        WalkTo(session, exit.X, exit.Y - 1);
        for (int n = 0; n < 3 && session.State.World.Player.MapId == room.Id; n++) Step(session, Direction.South);
        Assert.AreEqual(exterior.Id, session.State.World.Player.MapId);
        Assert.AreEqual((entrance.X, entrance.Y + 1), (session.State.World.Player.X, session.State.World.Player.Y));
        Assert.AreEqual(content.MapDefinition(exterior.Id).Music, session.State.Music);
        Continue(session);
    }

    [TestMethod]
    [DataRow("map.fuchsia-city-house-1", "FuchsiaCity_House1", "Woman", "WardenIsOldHasFalseTeeth")]
    [DataRow("map.three-island-house2", "ThreeIsland_House2", "Rocker", "CantMakeThisSpoonBend")]
    [DataRow("map.viridian-house", "ViridianCity_House", "BaldingMan", "NicknamingIsFun")]
    [DataRow("map.viridian-house", "ViridianCity_House", "LittleGirl", "MyDaddyLovesMonsToo")]
    [DataRow("map.vermilion-city-house-3", "VermilionCity_House3", "Boy", "PidgeyFlyLetterToSaffron")]
    [DataRow("map.vermilion-city-house-3", "VermilionCity_House3", "Lass", "SendMyPidgeyToUnionRoom")]
    [DataRow("map.saffron-city-house", "SaffronCity_House", "Lass", "DontLookAtMyLetter")]
    [DataRow("map.saffron-city-house", "SaffronCity_House", "Youngster", "GettingCopycatPokeDoll")]
    [DataRow("map.one-island-house1", "OneIsland_House1", "OldMan", "GazeUponVolcanoOnSunnyDays")]
    [DataRow("map.one-island-house1", "OneIsland_House1", "OldWoman", "LastTimeMtEmberErupted")]
    [DataRow("map.one-island-house2", "OneIsland_House2", "Lass", "IWantToStayHereForever")]
    [DataRow("map.three-island-house3", "ThreeIsland_House3", "Beauty", "WantedToLiveSomewhereQuiet")]
    [DataRow("map.three-island-house4", "ThreeIsland_House4", "BaldingMan", "GhostsInBerryForest")]
    [DataRow("map.three-island-house4", "ThreeIsland_House4", "LittleBoy", "PapaKeepsLyingToMe")]
    [DataRow("map.five-island-house1", "FiveIsland_House1", "Woman", "HusbandWentOffFishing")]
    [DataRow("map.five-island-house2", "FiveIsland_House2", "OldMan", "MeadowBelongedToFamily")]
    [DataRow("map.six-island-house", "SixIsland_House", "OldMan", "GoodPlaceForNatureAndHistory")]
    public void SourceNpcDialogueCompletesAndCanBeRepeatedAfterContinue(string mapName, string sourceName, string actor, string text)
    {
        var session = AtActor(mapName, sourceName + "_EventScript_" + actor);
        string expected = sourceName + "_Text_" + text;
        Send(session, new("interact"));
        Assert.IsTrue(Finish(session).Texts.Contains(expected), expected);
        Assert.IsNotNull(session.State.LastTalked);
        session = Continue(session);
        Send(session, new("interact"));
        Assert.IsTrue(Finish(session).Texts.Contains(expected));
        Continue(session);
    }

    [TestMethod]
    [DataRow("map.viridian-house", "ViridianCity_House", "NicknameSign", "SpearowNameSpeary")]
    [DataRow("map.vermilion-city-house-3", "VermilionCity_House3", "Letter", "DearPippiLetter")]
    [DataRow("map.saffron-city-house", "SaffronCity_House", "Letter", "ExplainPPUp")]
    [DataRow("map.three-island-house1", "ThreeIsland_House1", "DisplayCase", "RareRocksGemsDisplayed")]
    public void SourceBackgroundInteractionShowsItsOwnTextAndContinues(string mapName, string sourceName, string signName, string text)
    {
        var sign = content.MapDefinition(new(mapName)).BackgroundTriggers.Single(s => s.ScriptId == sourceName + "_EventScript_" + signName);
        var beside = Beside(mapName, sign.X, sign.Y);
        var session = Start(Place(FieldFixture(), mapName, beside.X, beside.Y, beside.Facing));
        Send(session, new("interact"));
        Assert.IsTrue(Finish(session).Texts.Contains(sourceName + "_Text_" + text));
        var decoration = content.MapDefinition(new(mapName)).Actors.FirstOrDefault(a =>
            a.X == sign.X && a.Y == sign.Y && a.ScriptId is "0" or "0x0");
        // Original object lookup selects the decorative object before its null script falls
        // through to the background event; signs without an object leave LastTalked alone.
        Assert.AreEqual(decoration?.LocalId, session.State.LastTalked);
        Continue(session);
    }

    [TestMethod]
    [DataRow("map.viridian-house", "ViridianCity_House", "Speary", "SPECIES_SPEAROW")]
    [DataRow("map.vermilion-city-house-3", "VermilionCity_House3", "Pidgey", "SPECIES_PIDGEY")]
    [DataRow("map.saffron-city-house", "SaffronCity_House", "Pidgey", "SPECIES_PIDGEY")]
    public void BirdActorsPreserveCryAndWaitEffectsAlongsideDialogue(string mapName, string sourceName, string actor, string species)
    {
        var session = AtActor(mapName, sourceName + "_EventScript_" + actor);
        Send(session, new("interact"));
        var transcript = Finish(session);
        Assert.IsTrue(transcript.Texts.Contains(sourceName + "_Text_" + actor));
        Assert.IsTrue(transcript.Effects.Any(e => e.Opcode == "playmoncry" &&
            e.Args.SequenceEqual(new[] { species, "CRY_MODE_NORMAL" })));
        Assert.IsTrue(transcript.Effects.Any(e => e.Opcode == "waitmoncry"));
        // This is presentation-command coverage; actual loudspeaker output is a separate gate.
        Continue(session);
    }

    [TestMethod]
    [DataRow(0, "Big guy")]
    [DataRow(1, "Big girl")]
    public void VisibleLostelleUsesGenderBufferAndSourceDialogue(int gender, string expected)
    {
        var state = FieldFixture();
        state = state with { PlayerGender = gender,
            Script = state.Script with { Flags = state.Script.Flags.SetItem(HomeHideFlag, false) } };
        bool isolated = gender != checkpoint.State.PlayerGender;
        var session = AtActor("map.three-island-house1", "ThreeIsland_House1_EventScript_Lostelle", state, isolated);
        Send(session, new("interact"));
        Assert.AreEqual(expected, session.State.TextBuffers.GetValueOrDefault("STR_VAR_1"));
        Assert.IsTrue(Finish(session).Texts.Contains("ThreeIsland_House1_Text_YoureAlwaysSoCool"));
        if (!isolated) Continue(session);
    }

    [TestMethod]
    public void HiddenLostelleCannotBeTalkedToAndStaysHiddenAfterContinue()
    {
        var state = FieldFixture();
        state = state with { Script = state.Script with { Flags = state.Script.Flags.SetItem(HomeHideFlag, true) } };
        var session = AtActor("map.three-island-house1", "ThreeIsland_House1_EventScript_Lostelle", state);
        Assert.IsFalse(session.State.Actors.Values.Single().Active);
        session = Continue(session);
        Assert.IsFalse(session.State.Actors.Values.Single().Active);
        string hash = CanonicalJson.Hash(session.State);
        var rejected = session.Execute("hidden-lostelle", session.Revision, new("interact"));
        Assert.AreEqual(CommandStatus.Rejected, rejected.Status);
        Assert.AreEqual("nothing_to_interact", rejected.Error);
        Assert.AreEqual(hash, CanonicalJson.Hash(session.State));
    }

    [TestMethod]
    [DataRow(false, "ImNotLostelle")]
    [DataRow(true, "IllGoPlayWithLostelle")]
    public void House5GirlSelectsTheSourceRescueBranch(bool rescued, string text)
    {
        var state = FieldFixture();
        state = state with { Script = state.Script with { Flags = state.Script.Flags.SetItem("FLAG_RESCUED_LOSTELLE", rescued) } };
        bool isolated = rescued != checkpoint.State.Script.Flags.GetValueOrDefault("FLAG_RESCUED_LOSTELLE");
        var session = AtActor("map.three-island-house5", "ThreeIsland_House5_EventScript_LittleGirl", state, isolated);
        Send(session, new("interact"));
        var transcript = Finish(session);
        CollectionAssert.AreEquivalent(new[] { "ThreeIsland_House5_Text_" + text }, transcript.Texts.ToArray());
        if (!isolated) Continue(session);
    }
}
