using System.Collections.Immutable;
using System.Reflection;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Controlled initial placement only; warps, field moves and Continue use public commands.</summary>
[TestClass, TestCategory("Game")]
public sealed class MtEmberRouteFlowTests : WildBattleFixture
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Summit = "map.mt-ember-summit";
    private const string SecondFloor = "map.mt-ember-summit-path-2f";

    private CampaignSession At(string name, int x, int y, Direction direction)
    {
        var session = CampaignSession.CreateCurrent(content);
        var state = FieldFixture();
        var map = content.Maps[new(name)];
        state = state with {
            World = state.World with { Player = new(map.Id, x, y, map.GetTile(x, y)!.Elevation, direction) },
            Script = StoryState.Empty with { Flags = state.Script.Flags,
                Variables = state.Script.Variables.SetItem("VAR_REPEL_STEP_COUNT", 250),
                EffectSequence = state.Script.EffectSequence },
            Dialogue = null, ItemMenu = null, EntryPrograms = [], Motions = [], MapEdits = null,
            PresentationEffects = [], PresentationTicks = 0, IsSurfing = false, IsCycling = false, FlashLevel = 0
        };
        // Materialize the original entry scripts/actors/patches for this controlled starting point.
        state = (CampaignState)typeof(CampaignSession).GetMethod("EnterMap", Private)!
            .Invoke(session, [state, null, RoamerTransition.Warp])!;
        state = (CampaignState)typeof(CampaignSession).GetMethod("Pump", Private)!.Invoke(session, [state])!;
        typeof(CampaignSession).GetField("_commands", Private)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, checkpoint.Generation));
        DrainField(session);
        Assert.IsNull(session.ValidatePlayerSave(session.State), "controlled map entry must be a valid save");
        return session;
    }

    private void DrainField(CampaignSession session)
    {
        for (int n = 0; n < 250; n++)
        {
            Assert.IsNull(session.State.WildBattle, "high-level lead and active Repel suppress route encounters");
            Assert.IsNull(session.State.ForestTrainerBattle);
            if (session.State.StrengthEffect is { Phase: FieldStrengthSourcePresentation.Push } push)
            { Send(session, new("field_strength_complete", Text: push.WaitId)); continue; }
            if (session.State.Script.Completed && session.State.Script.Wait is null &&
                session.State.Dialogue is null && session.State.EntryPrograms.IsEmpty &&
                session.State.StrengthEffect is null && session.State.RockSmashEffect is null) return;
            Send(session, new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? 1 : null));
        }
        Assert.Fail("field operation did not complete");
    }

    private void Cross(CampaignSession session, Direction direction, string destination)
    {
        string origin = session.State.World.Player.MapId.Value;
        for (int n = 0; n < 3 && session.State.World.Player.MapId.Value == origin; n++)
        { Send(session, new("move", direction)); DrainField(session); }
        Assert.AreEqual(destination, session.State.World.Player.MapId.Value);
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow("exterior", 14, 25, Direction.North, "summit-path-1f")]
    [DataRow("summit-path-1f", 2, 14, Direction.South, "exterior")]
    [DataRow("summit-path-1f", 11, 2, Direction.North, "summit-path-2f")]
    [DataRow("summit-path-2f", 8, 38, Direction.South, "summit-path-1f")]
    [DataRow("summit-path-2f", 39, 7, Direction.North, "summit-path-3f")]
    [DataRow("summit-path-3f", 2, 3, Direction.South, "summit-path-2f")]
    [DataRow("summit-path-3f", 11, 7, Direction.South, "exterior")]
    [DataRow("exterior", 39, 20, Direction.North, "summit-path-3f")]
    [DataRow("exterior", 29, 8, Direction.North, "summit")]
    [DataRow("summit", 9, 14, Direction.South, "exterior")]
    public void EverySummitApproachWarpCrossesUsingPublicMovement(string source, int x, int y, Direction direction, string destination)
        => Cross(At("map.mt-ember-" + source, x, y, direction), direction, "map.mt-ember-" + destination);

    private void WalkTo(CampaignSession session, int targetX, int targetY)
    {
        var start = (session.State.World.Player.X, session.State.World.Player.Y);
        var map = content.Maps[session.State.World.Player.MapId];
        var obstacles = content.MapDefinition(map.Id).Actors.Where(actor =>
            actor.HideFlag == "0" || !session.State.Script.Flags.GetValueOrDefault(actor.HideFlag))
            .Select(actor => (actor.X, actor.Y)).ToHashSet();
        var warps = map.FieldWarps.Select(warp => (warp.X, warp.Y)).ToHashSet();
        var queue = new Queue<(int X, int Y)>(); queue.Enqueue(start);
        var paths = new Dictionary<(int X, int Y), Direction[]> { [start] = [] };
        while (queue.TryDequeue(out var current) && !paths.ContainsKey((targetX, targetY)))
            foreach (var (dx, dy, direction) in new[] { (0, -1, Direction.North), (1, 0, Direction.East),
                (0, 1, Direction.South), (-1, 0, Direction.West) })
            {
                var next = (current.X + dx, current.Y + dy);
                if (paths.ContainsKey(next) || obstacles.Contains(next) || warps.Contains(next) ||
                    map.GetTile(next.Item1, next.Item2) is not { Blocked: false, Elevation: 3 }) continue;
                paths[next] = [..paths[current], direction]; queue.Enqueue(next);
            }
        Assert.IsTrue(paths.ContainsKey((targetX, targetY)), "No physical path through the original map interior");
        foreach (var direction in paths[(targetX, targetY)])
        { Send(session, new("move", direction)); DrainField(session); Assert.AreEqual(map.Id, session.State.World.Player.MapId); }
        Assert.AreEqual((targetX, targetY), (session.State.World.Player.X, session.State.World.Player.Y));
    }

    [TestMethod]
    public void WholeSummitApproachWalksThroughAllThreeCaveInteriorsWithoutRelocation()
    {
        var session = At("map.mt-ember-exterior", 14, 25, Direction.North);
        Cross(session, Direction.North, "map.mt-ember-summit-path-1f");
        WalkTo(session, 11, 2);
        Cross(session, Direction.North, SecondFloor);
        WalkTo(session, 39, 7);
        Cross(session, Direction.North, "map.mt-ember-summit-path-3f");
        WalkTo(session, 11, 7);
        Cross(session, Direction.South, "map.mt-ember-exterior");
        WalkTo(session, 29, 8);
        Cross(session, Direction.North, Summit);
        var entrance = content.Maps[new(Summit)].FieldWarps[0];
        Assert.AreEqual((entrance.X, entrance.Y), (session.State.World.Player.X, session.State.World.Player.Y));
        Send(session, new("move", Direction.North)); DrainField(session);
        Assert.AreEqual((9, 14), (session.State.World.Player.X, session.State.World.Player.Y));
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void SummitStrengthPushSurvivesContinueAndResetsOnRealMapEntry()
    {
        var session = At(Summit, 9, 13, Direction.North);
        int partyIndex = Enumerable.Range(0, session.State.Party.Length).First(index =>
            FieldHmSourceRules.Knows(session.State.Party[index], FieldHmSourceRules.StrengthMove));
        var source = session.State.Actors.Values.Single(actor => actor.SourceLocalId == 3);
        Send(session, new("field_strength", PartyIndex: partyIndex)); DrainField(session);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag));
        Send(session, new("move", Direction.North));
        Assert.AreEqual(FieldStrengthSourcePresentation.Push, session.State.StrengthEffect?.Phase);
        DrainField(session);
        Assert.AreEqual((9, 11), (session.State.Actors[source.LocalId].X, session.State.Actors[source.LocalId].Y));
        Assert.AreEqual((9, 13), (session.State.World.Player.X, session.State.World.Player.Y));
        StrictRoundtrip(session);
        var encoded = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, content.ContentVersion);
        var saved = SaveCodec.Decode<CampaignState>(encoded, session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave);
        session = CampaignSession.RestoreCurrent(content, saved.State, saved.Generation);
        Cross(session, Direction.South, "map.mt-ember-exterior");
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag));
        Cross(session, Direction.North, Summit);
        var reset = session.State.Actors.Values.Single(actor => actor.SourceLocalId == 3);
        Assert.AreEqual((9, 12), (reset.X, reset.Y));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag));
    }

    [TestMethod]
    [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)]
    [DataRow(6)] [DataRow(7)] [DataRow(8)] [DataRow(9)]
    public void EachSecondFloorRockUsesSharedRockSmashAndPersistsItsOwnFlag(int localId)
    {
        var map = content.Maps[new(SecondFloor)];
        var definition = content.MapDefinition(map.Id);
        var source = definition.Actors.Single(actor => actor.SourceLocalId == localId);
        var adjacent = new[] { (source.X, source.Y + 1, Direction.North), (source.X, source.Y - 1, Direction.South),
            (source.X + 1, source.Y, Direction.West), (source.X - 1, source.Y, Direction.East) }
            .First(cell => map.GetTile(cell.Item1, cell.Item2) is { Blocked: false, Elevation: 3 } &&
                !definition.Actors.Any(actor => actor.X == cell.Item1 && actor.Y == cell.Item2));
        var session = At(SecondFloor, adjacent.Item1, adjacent.Item2, adjacent.Item3);
        Smash(session, localId);
        StrictRoundtrip(session);
    }

    private void Smash(CampaignSession session, int localId)
    {
        int partyIndex = FieldRockSmashSourceRules.FirstPartyMember(session.State.Party);
        var source = session.State.Actors.Values.Single(actor => actor.SourceLocalId == localId);
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(source.HideFlag));
        Send(session, new("field_rock_smash", PartyIndex: partyIndex)); DrainField(session);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(source.HideFlag));
        Assert.IsFalse(session.State.Actors[source.LocalId].Active);
        Assert.IsNull(session.State.RockSmashEffect);
    }

    [TestMethod]
    public void RockSmashTemporaryFlagClearsOnlyAfterLeavingAndReentering()
    {
        var session = At(SecondFloor, 8, 38, Direction.West);
        Smash(session, 6);
        StrictRoundtrip(session);
        Cross(session, Direction.South, "map.mt-ember-summit-path-1f");
        Cross(session, Direction.North, SecondFloor);
        var rock = session.State.Actors.Values.Single(actor => actor.SourceLocalId == 6);
        Assert.IsTrue(rock.Active);
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault("FLAG_TEMP_17"));
        StrictRoundtrip(session);
    }
}
