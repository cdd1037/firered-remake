using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Items;
using Game.Domain.Randomness;
using Game.Domain.World;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class CurrentWalkingProcessTests
{
    [TestMethod]
    public void DefaultsAndRoundTripKeepTheExistingCurrentSerializedToken()
    {
        const string token = "firered-five-island-walking-candidates-v1";
        var state = new EarlyLandEncounterState(new(42), StepsSinceLastEncounter: 21);
        Assert.AreEqual(token, state.RulesVersion);
        Assert.AreEqual(token, new EarlyLandEncounterProfile().RulesVersion);
        Assert.AreEqual(token, EarlyLandEncounterProfile.OrdinaryWalking.RulesVersion);
        Assert.AreEqual(state, JsonSerializer.Deserialize<EarlyLandEncounterState>(JsonSerializer.Serialize(state)));
        var implicitSeed = EarlyLandEncounterRules.SeedFromGlobal(new RngState(42, 19));
        var explicitSeed = EarlyLandEncounterRules.SeedFromGlobal(new RngState(42, 19), rulesVersion: token);
        Assert.AreEqual(explicitSeed.State, implicitSeed.State);
        Assert.AreEqual(explicitSeed.GlobalRng, implicitSeed.GlobalRng);
        CollectionAssert.AreEqual(explicitSeed.RandomTrace.ToArray(), implicitSeed.RandomTrace.ToArray());
        EarlyLandEncounterRules.ValidateState(state);
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.ValidateState(state with { StepsSinceLastEncounter = 22 }));
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.ValidateState(state with { PreviousMetatileBehavior = 256 }));
        foreach (var retired in new[] { "firered-route22-walking-candidates-v1", "unknown", EarlyLandEncounterRules.SafariRulesVersion })
            Assert.Throws<NotSupportedException>(() => EarlyLandEncounterRules.ValidateState(state with { RulesVersion = retired }));
    }

    [TestMethod]
    public void LiveRepelKeepsOrderedLeadEqualityAndFinalStepPreemption()
    {
        ImmutableArray<EarlyRepelPartyMember> party = [new(0, 90), new(10, 80, true), new(10, 30), new(10, 60)];
        Assert.IsFalse(RepelRules.IsWildLevelAllowed(250, 29, party));
        Assert.IsTrue(RepelRules.IsWildLevelAllowed(250, 30, party));
        Assert.IsTrue(RepelRules.IsWildLevelAllowed(250, 31, party));
        Assert.IsTrue(RepelRules.IsWildLevelAllowed(0, 1, party));
        Assert.IsFalse(RepelRules.IsWildLevelAllowed(1, 100, [new(0, 1), new(10, 1, true)]));
        Assert.Throws<ArgumentException>(() => RepelRules.IsWildLevelAllowed(1, 30, default));
        Assert.Throws<ArgumentException>(() => RepelRules.IsWildLevelAllowed(1, 30, [new(-1, 30)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => RepelRules.IsWildLevelAllowed(1, 0, party));
        Assert.AreEqual(new EarlyRepelStepResult(0, true), RepelRules.AdvanceStep(1, EncounterCheckTrigger.WalkingStepCompleted));
        Assert.AreEqual(new EarlyRepelStepResult(1, false), RepelRules.AdvanceStep(1, EncounterCheckTrigger.TurnInPlaceCompleted));
        Assert.AreEqual(new EarlyRepelStepResult(1, false), RepelRules.AdvanceStep(1, EncounterCheckTrigger.WalkingStepCompleted, forcedMovement: true));
        Assert.AreEqual(new EarlyRepelStepResult(1, false), RepelRules.AdvanceStep(1, EncounterCheckTrigger.WalkingStepCompleted, forcedMovementTile: true));
        Assert.AreEqual(new EarlyRepelStepResult(0, false), RepelRules.AdvanceStep(0, EncounterCheckTrigger.WalkingStepCompleted));
    }

    [TestMethod]
    public void NonextendedEncounterLimitRemainsTwoHundredNotCurrentMaxRepel()
    {
        EarlyRepelRules.ValidateSteps(100);
        Assert.Throws<NotSupportedException>(() => EarlyRepelRules.ValidateSteps(101));
        EarlyRepelRules.ValidateSteps(200, pokemonTowerRules: true);
        Assert.Throws<NotSupportedException>(() => EarlyRepelRules.ValidateSteps(201, pokemonTowerRules: true));
        RepelRules.ValidateSteps(250);
        Assert.Throws<NotSupportedException>(() => RepelRules.ValidateSteps(251));
    }

    [TestMethod]
    public void StandaloneSafariMovementRetainsItsOwnTokenAndSourceTerrainChecks()
    {
        var mountain = new ConnectedFieldCell(SafariFieldMovementRules.MountainTopBehavior, 0, 4);
        var cells = Enumerable.Repeat(new ConnectedFieldCell(0, 0, 3), 54 * 35).ToImmutableArray().SetItem(0, mountain);
        var source = new ConnectedFieldMap(new("map.safari-zone-east"), 54, 35, cells, []);
        var movement = SafariFieldMovementRules.ForConnectedMovement(source, EarlyLandEncounterRules.SafariRulesVersion);
        Assert.AreEqual(mountain, source.Cells[0]);
        Assert.AreEqual(mountain with { SourceBehavior = 0 }, movement.Cells[0]);
        Assert.AreEqual(source.Cells[1], movement.Cells[1]);
        Assert.Throws<NotSupportedException>(() => SafariFieldMovementRules.ForConnectedMovement(source, EarlyLandEncounterRules.FiveIslandRulesVersion));
        Assert.Throws<ArgumentException>(() => SafariFieldMovementRules.ForConnectedMovement(source with { Width = 53 }, EarlyLandEncounterRules.SafariRulesVersion));
        foreach (var invalid in new[] { mountain with { Collision = 1 }, mountain with { Elevation = 3 } })
            Assert.Throws<ArgumentException>(() => SafariFieldMovementRules.ForConnectedMovement(source with { Cells = cells.SetItem(0, invalid) }, EarlyLandEncounterRules.SafariRulesVersion));
    }
}
