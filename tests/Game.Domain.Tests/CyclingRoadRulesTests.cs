using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.World;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class CyclingRoadRulesTests
{
    // Independent bike.c Normal/Slope transition matrix. B is read by Normal only.
    [TestMethod]
    [DataRow(false, Direction.North, null, false, CyclingRoadAction.Move, Direction.South, true, 4)]
    [DataRow(false, Direction.West, null, true, CyclingRoadAction.Idle, Direction.West, false, 1)]
    [DataRow(false, Direction.North, Direction.South, false, CyclingRoadAction.Move, Direction.South, true, 4)]
    [DataRow(false, Direction.North, Direction.South, true, CyclingRoadAction.Move, Direction.South, true, 16)]
    [DataRow(false, Direction.South, Direction.North, false, CyclingRoadAction.Move, Direction.North, true, 16)]
    [DataRow(false, Direction.South, Direction.North, true, CyclingRoadAction.Move, Direction.North, true, 16)]
    [DataRow(false, Direction.North, Direction.East, false, CyclingRoadAction.Move, Direction.East, true, 16)]
    [DataRow(false, Direction.North, Direction.West, true, CyclingRoadAction.Move, Direction.West, true, 16)]
    [DataRow(true, Direction.South, Direction.South, false, CyclingRoadAction.Move, Direction.South, true, 4)]
    [DataRow(true, Direction.South, Direction.South, true, CyclingRoadAction.Move, Direction.South, true, 4)]
    [DataRow(true, Direction.North, Direction.North, false, CyclingRoadAction.Move, Direction.North, true, 16)]
    [DataRow(true, Direction.North, Direction.North, true, CyclingRoadAction.Move, Direction.North, true, 16)]
    [DataRow(true, Direction.East, Direction.East, true, CyclingRoadAction.Move, Direction.East, true, 16)]
    [DataRow(true, Direction.West, Direction.West, false, CyclingRoadAction.Move, Direction.West, true, 16)]
    [DataRow(true, Direction.North, Direction.South, false, CyclingRoadAction.Turn, Direction.South, false, 1)]
    [DataRow(true, Direction.South, Direction.North, true, CyclingRoadAction.Turn, Direction.North, false, 1)]
    [DataRow(true, Direction.East, Direction.West, false, CyclingRoadAction.Turn, Direction.West, false, 1)]
    [DataRow(true, Direction.North, null, false, CyclingRoadAction.Turn, Direction.South, false, 1)]
    [DataRow(true, Direction.South, null, true, CyclingRoadAction.Turn, Direction.South, false, 1)]
    public void NormalAndSlopeKeepTheirDistinctInputTransitions(bool slope, Direction facing, Direction? input,
        bool brake, CyclingRoadAction action, Direction direction, bool nextSlope, int ticks)
    {
        Assert.AreEqual(new CyclingRoadInputResult(action, direction, nextSlope, ticks),
            CyclingRoadRules.Resolve(slope, facing, input, brake));
    }

    [TestMethod]
    public void ReleasingInputTurnsBeforeBrakeHoldsOrGravityResumes()
    {
        var turn = CyclingRoadRules.Resolve(true, Direction.North, null, true);
        Assert.AreEqual(new CyclingRoadInputResult(CyclingRoadAction.Turn, Direction.South, false, 1), turn);
        Assert.AreEqual(new CyclingRoadInputResult(CyclingRoadAction.Idle, Direction.South, false, 1),
            CyclingRoadRules.Resolve(turn.SlopeState, turn.Direction, null, true));
        Assert.AreEqual(new CyclingRoadInputResult(CyclingRoadAction.Move, Direction.South, true, 4),
            CyclingRoadRules.Resolve(turn.SlopeState, turn.Direction, null, false));
        var changed = CyclingRoadRules.Resolve(true, Direction.South, Direction.North, false);
        Assert.AreEqual(CyclingRoadAction.Turn, changed.Action);
        Assert.AreEqual(1, changed.Ticks);
        Assert.AreEqual(new CyclingRoadInputResult(CyclingRoadAction.Move, Direction.North, true, 16),
            CyclingRoadRules.Resolve(changed.SlopeState, changed.Direction, Direction.North, false));
    }

    [TestMethod]
    public void OnlyTheTwoCyclingBehaviorsAreSlopesAndDirectionsAreValidated()
    {
        Assert.IsTrue(CyclingRoadRules.IsSlope(0xd0));
        Assert.IsTrue(CyclingRoadRules.IsSlope(0xd1));
        foreach (int? behavior in new int?[] { null, 0, 2, 0xcf, 0xd2 })
            Assert.IsFalse(CyclingRoadRules.IsSlope(behavior));
        Assert.Throws<ArgumentException>(() => CyclingRoadRules.Resolve(false, (Direction)99, null, false));
        Assert.Throws<ArgumentException>(() => CyclingRoadRules.Resolve(true, Direction.South, (Direction)99, false));
    }

    // rate 21: 21*16=336; bike truncates 336*80/100=268 before the buff.
    // buff 188 contributes floor(188*16/200)=15; 283*2/3=188, then Illuminate=376.
    [TestMethod]
    [DataRow(EncounterMovementMode.Walking, 0, "ITEM_NONE", "ABILITY_NONE", 336)]
    [DataRow(EncounterMovementMode.Bicycle, 0, "ITEM_NONE", "ABILITY_NONE", 268)]
    [DataRow(EncounterMovementMode.Bicycle, 188, "ITEM_NONE", "ABILITY_NONE", 283)]
    [DataRow(EncounterMovementMode.Bicycle, 188, "ITEM_CLEANSE_TAG", "ABILITY_NONE", 188)]
    [DataRow(EncounterMovementMode.Bicycle, 188, "ITEM_CLEANSE_TAG", "ABILITY_STENCH", 94)]
    [DataRow(EncounterMovementMode.Bicycle, 188, "ITEM_CLEANSE_TAG", "ABILITY_ILLUMINATE", 376)]
    [DataRow(EncounterMovementMode.Walking, 188, "ITEM_CLEANSE_TAG", "ABILITY_ILLUMINATE", 468)]
    public void BicycleRatePrecedesBuffCleanseTagAndAbility(EncounterMovementMode movement, int buff,
        string item, string ability, int expectedThreshold)
    {
        var table = Table("map.route-17");
        var state = State(0xd1) with { EncounterRateBuff = (ushort)buff };
        var profile = new EarlyLandEncounterProfile(RulesVersion: state.RulesVersion, Movement: movement,
            LeadAbility: ability, LeadHeldItem: item);
        var result = EarlyLandEncounterRules.Check(state, OrdinaryLandEncounterCatalogTests.Bind(table, 0xd1), Check(table.Map, 0xd1), profile, new(0, 19));
        Assert.AreEqual(expectedThreshold, result.RateThreshold);
        Assert.AreEqual(EncounterCheckOutcome.CandidateReady, result.Outcome);
        Assert.AreEqual(new RngState(3917380458u, 21), result.GlobalRng);
        CollectionAssert.AreEqual(new[] { "encounter.rate", "encounter.land-slot", "encounter.level" },
            result.RandomTrace.Select(t => t.Draw.Purpose).ToArray());
        Assert.AreEqual((uint)12345, result.State.RateRng.Seed);
        Assert.AreEqual((ulong)1, result.State.RateRng.Cursor);
        OrdinaryLandEncounterCatalogTests.Bind(table, 0xd1).ValidateState(result.State, table);
    }

    [TestMethod]
    public void BicycleReductionAlsoAppliesToOrdinaryGrassAndRateStillClamps()
    {
        foreach (var map in new[] { "map.route-1", "map.route-18" })
        {
            var table = Table(map);
            var state = State(2);
            var walking = new EarlyLandEncounterProfile(RulesVersion: state.RulesVersion);
            var bike = walking with { Movement = EncounterMovementMode.Bicycle };
            var walkResult = EarlyLandEncounterRules.Check(state, OrdinaryLandEncounterCatalogTests.Bind(table, 2), Check(table.Map, 2), walking, new(0));
            var bikeResult = EarlyLandEncounterRules.Check(state, OrdinaryLandEncounterCatalogTests.Bind(table, 2), Check(table.Map, 2), bike, new(0));
            Assert.AreEqual(336, walkResult.RateThreshold);
            Assert.AreEqual(268, bikeResult.RateThreshold);
            Assert.AreEqual(walkResult.Candidate, bikeResult.Candidate);
            Assert.AreEqual(walkResult.GlobalRng, bikeResult.GlobalRng);
            var maximum = EarlyLandEncounterRules.Check(state, OrdinaryLandEncounterCatalogTests.Bind(table with { RawEncounterRate = 255 }, 2),
                Check(table.Map, 2), bike with { LeadAbility = "ABILITY_ILLUMINATE" }, new(0));
            Assert.AreEqual(1600, maximum.RateThreshold);
        }
    }

    [TestMethod]
    public void CyclingGrassUsesLoadedMapAttributesAndKeepsItsPendingBehavior()
    {
        foreach (var map in new[] { "map.route-17", "map.another-slope" })
        {
            var table = Table(map);
            var definition = OrdinaryLandEncounterCatalogTests.Bind(table, 0xd1);
            var state = State(0xd1);
            var profile = new EarlyLandEncounterProfile(RulesVersion: state.RulesVersion, Movement: EncounterMovementMode.Bicycle);
            var result = EarlyLandEncounterRules.Check(state, definition, Check(table.Map, 0xd1), profile, new(0));
            Assert.AreEqual(EncounterCheckOutcome.CandidateReady, result.Outcome);
            Assert.AreEqual((byte)0xd1, result.State.Pending!.MetatileBehavior);
            Assert.AreEqual((ushort)0xd1, EarlyLandEncounterRules.CompleteCandidate(result.State, table, definition).PreviousMetatileBehavior);
            Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.Check(state, definition,
                Check(table.Map, 0xd0), profile, new(0)));
            Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.Check(state, definition,
                Check(table.Map, 0xd1) with { EncounterType = EncounterTileType.None }, profile, new(0)));
            var road = EarlyLandEncounterRules.Check(state, definition,
                Check(table.Map, 0xd0) with { EncounterType = EncounterTileType.None }, profile, new(0));
            Assert.AreEqual(EncounterCheckOutcome.NoEncounterTerrain, road.Outcome);
            Assert.IsTrue(road.RandomTrace.IsEmpty);
        }
    }

    [TestMethod]
    [DataRow(0xd0)]
    [DataRow(0xd1)]
    public void LoadedSlopeMovementUsesOrdinaryCollisionAndKeepsOtherTerrainUnchanged(int behavior)
    {
        var map = new ConnectedFieldMap(new("map.another-slope"), 1, 2, [new(behavior, 0, 3), new(behavior, 0, 3)], []);
        var state = new WorldSnapshot(ConnectedFieldRules.RulesVersion, 0, 0,
            new(map.Id, 0, 0, 3, Direction.South), new(42));
        var intent = new ConnectedFieldIntent(new("test/cycling-step"), 0, ConnectedFieldAction.Walk, Direction.South);
        var maps = new Dictionary<MapId, ConnectedFieldMap> { [map.Id] = map };
        var admitted = ConnectedFieldRules.Complete(state, intent, maps, biking: true);
        Assert.AreEqual(TransitionStatus.Accepted, admitted.Status);
        Assert.AreEqual(1, admitted.State.Player.Y);
        Assert.AreEqual(1L, admitted.State.StepCount);
        Assert.AreEqual(state.Rng, admitted.State.Rng);
        maps[map.Id] = map with { Cells = [new(behavior, 0, 3), new(behavior, 1, 3)] };
        var blocked = ConnectedFieldRules.Complete(state, intent, maps, biking: true);
        Assert.AreEqual(TransitionStatus.Accepted, blocked.Status);
        Assert.AreEqual(0, blocked.State.Player.Y);
        Assert.AreEqual("collision", blocked.Events.Single().Reason);
        maps[map.Id] = map with { Cells = [new(behavior, 0, 3), new(0xd2, 0, 3)] };
        Assert.AreEqual(TransitionStatus.Fault, ConnectedFieldRules.Complete(state, intent, maps).Status);
        maps[map.Id] = map with { Cells = [new(0, 0, 3), new(2, 0, 3)] };
        var ordinary = ConnectedFieldRules.Complete(state, intent, maps);
        var extended = ConnectedFieldRules.Complete(state, intent, maps, biking: true);
        Assert.AreEqual(ordinary.Status, extended.Status);
        Assert.AreEqual(ordinary.State, extended.State);
        CollectionAssert.AreEqual(ordinary.Events.ToArray(), extended.Events.ToArray());
    }

    private static EarlyLandEncounterState State(byte behavior) => new(new(0), behavior,
        StepsSinceLastEncounter: 21, RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion);
    private static EarlyLandEncounterCheck Check(MapId map, byte behavior) =>
        new(map, EncounterCheckTrigger.WalkingStepCompleted, behavior, EncounterTileType.Land);
    private static EarlyLandEncounterTable Table(string map) => new(new(map), 21,
        new[] { 20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1 }.Select((weight, index) =>
            new EarlyLandEncounterSlot(index, new("pret:SPECIES_RATTATA"), 20, 20, weight)).ToImmutableArray());
}
