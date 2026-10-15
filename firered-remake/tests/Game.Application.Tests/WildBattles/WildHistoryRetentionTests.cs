using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Encounters;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class WildHistoryRetentionTests
{
    private static CampaignWildHistory Battle(int id, WildBattleOutcome outcome = WildBattleOutcome.Won) =>
        new("wild-battle:" + id, 19, 3, outcome, outcome == WildBattleOutcome.Won ? 10 : 0);

    private static CampaignState State(params CampaignWildHistory[] battles) =>
        new(null!, null!, null!, [], null, null, null, [], 0, "", "", "", "RED", "BLUE", 0, "", [], true)
        { Exploration = new(null, battles.Length + 1L, battles.ToImmutableArray()) };

    [TestMethod]
    public void OrdinarySettlementsCollapseWithoutChangingAllocatorOrLatestCallback()
    {
        var original = State(Enumerable.Range(1, 10000).Select(i => Battle(i)).ToArray());
        var compacted = CampaignWildHistoryRetention.Compact(original);
        Assert.AreEqual(1, compacted.Exploration!.Battles.Length);
        Assert.AreSame(original.Exploration!.Battles[^1], compacted.Exploration.Battles[0]);
        Assert.AreEqual(10001L, compacted.Exploration.NextWildBattleId);
        Assert.IsTrue(CampaignWildHistoryRetention.HasValidIdentity(compacted.Exploration));
        Assert.AreSame(compacted, CampaignWildHistoryRetention.Compact(compacted));
        Assert.AreEqual(10000, original.Exploration.Battles.Length);
    }

    [TestMethod]
    public void CaptureEvolutionAndSpecialReceiptsSurviveInOriginalOrder()
    {
        var original = State(Battle(1), Battle(2, WildBattleOutcome.Caught), Battle(3),
            Battle(4) with { ScriptedMarowak = true }, Battle(5), Battle(6)) with
        { EvolutionHistory = [new(1, "wild-battle:3", 1, 2, 15, 16)] };
        var compacted = CampaignWildHistoryRetention.Compact(original);
        CollectionAssert.AreEqual(new[] { "wild-battle:2", "wild-battle:3", "wild-battle:4", "wild-battle:6" },
            compacted.Exploration!.Battles.Select(b => b.Id).ToArray());
        Assert.IsTrue(original.EvolutionHistory == compacted.EvolutionHistory);
        Assert.IsTrue(CampaignWildHistoryRetention.HasValidIdentity(compacted.Exploration));
    }

    [TestMethod]
    public void PendingEvolutionPreventsPrematureRemovalOfItsWin()
    {
        var original = State(Battle(1), Battle(2)) with
        { Evolution = new("wild-battle:1", [], 0, CampaignEvolutionStage.Proposal, [], 0, []) };
        Assert.AreSame(original, CampaignWildHistoryRetention.Compact(original));
    }

    [TestMethod]
    public void RepeatedFishingAndRegionalTrainingDoNotAccumulateSettlements()
    {
        // Retention is independent of the source factory; complete source validation is
        // covered by the real checkpoint and existing encounter/capture flow tests.
        var templates = new[]
        {
            Battle(1) with { Fishing = new((FishingRod)0, new("map.test"), 0, new("species.test"), 3) },
            Battle(1) with { FourIsland = new(new("map.test"), (FourIslandEncounterKind)0, 0) },
            Battle(1) with { FiveIsland = new(new("map.test"), (FiveIslandEncounterKind)0, 0) },
            Battle(1) with { SixIsland = new(new("map.test"), (SixIslandEncounterKind)0, 0) },
            Battle(1) with { SeviiRuby = new(new("map.test"), (SeviiRubyEncounterKind)0, 0) },
            Battle(1) with { VictoryRoad = new(new("map.test"), (VictoryRoadEncounterKind)0, 0) },
            Battle(1) with { Safari = true },
            Battle(1) with { SourceTableReceipt = new(new(new("map.test"), (SourceTableEncounterKind)0, 0), new(1), new(2), null!) }
        };
        foreach (var template in templates)
        {
            var state = State();
            for (var i = 1; i <= 1000; i++)
            {
                state = state with { Exploration = state.Exploration! with
                { NextWildBattleId = i + 1L, Battles = state.Exploration.Battles.Add(template with { Id = "wild-battle:" + i }) } };
                state = CampaignWildHistoryRetention.Compact(state);
                Assert.AreEqual(1, state.Exploration!.Battles.Length);
                Assert.IsTrue(CampaignWildHistoryRetention.HasValidIdentity(state.Exploration));
            }
            Assert.AreEqual(1001L, state.Exploration!.NextWildBattleId);
        }
    }

    [TestMethod]
    public void SevenIslandRetainsFirstUnownAndFirstCaptureWithoutKeepingAllSightings()
    {
        var template = Battle(1) with { SourceSpeciesId = 201,
            SevenIsland = new(new("map.test"), SevenIslandEncounterKind.Land, 0) };
        var original = State(Enumerable.Range(1, 1000).Select(i => template with
        { Id = "wild-battle:" + i, Outcome = i == 50 ? WildBattleOutcome.Caught : WildBattleOutcome.Won }).ToArray());
        var compacted = CampaignWildHistoryRetention.Compact(original);
        CollectionAssert.AreEqual(new[] { "wild-battle:1", "wild-battle:50", "wild-battle:1000" },
            compacted.Exploration!.Battles.Select(b => b.Id).ToArray());
        Assert.AreSame(original.Exploration!.Battles[0], compacted.Exploration.Battles[0]);
        Assert.AreSame(original.Exploration.Battles[49], compacted.Exploration.Battles[1]);
    }

    [TestMethod]
    public void RetainedIdentityRejectsDuplicatesReorderingFutureAndNoncanonicalIds()
    {
        foreach (var ids in new[] {
            new[] { "wild-battle:2", "wild-battle:2" },
            new[] { "wild-battle:2", "wild-battle:1" },
            new[] { "wild-battle:1", "wild-battle:3" },
            new[] { "wild-battle:01", "wild-battle:2" },
            new[] { "wild-battle:0", "wild-battle:2" } })
        {
            var exploration = new CampaignExploration(null, 3,
                ids.Select(id => Battle(1) with { Id = id }).ToImmutableArray());
            Assert.IsFalse(CampaignWildHistoryRetention.HasValidIdentity(exploration), string.Join(",", ids));
        }
        Assert.IsTrue(CampaignWildHistoryRetention.HasValidIdentity(new(null, 101, [Battle(2), Battle(100)])));
    }

    [TestMethod]
    public void CurrentCheckpointCompactionPreservesGameplayAndPassesSaveValidation()
    {
        var original = CurrentCampaignFixture.Checkpoint.State;
        var compacted = CampaignWildHistoryRetention.Compact(original);
        Assert.IsTrue(compacted.Exploration!.Battles.Length < original.Exploration!.Battles.Length);
        Assert.AreSame(original.World, compacted.World);
        Assert.AreSame(original.Script, compacted.Script);
        Assert.IsTrue(original.Party == compacted.Party);
        Assert.AreEqual(original.Exploration.NextWildBattleId, compacted.Exploration.NextWildBattleId);
        Assert.IsTrue(original.Exploration.Captured == compacted.Exploration.Captured);
        Assert.AreSame(original.SeenSpecies, compacted.SeenSpecies);
        Assert.AreSame(original.CaughtSpecies, compacted.CaughtSpecies);
        Assert.IsTrue(original.EvolutionHistory == compacted.EvolutionHistory);
        var validator = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        Assert.IsNull(validator.ValidatePlayerSave(compacted));
        var bytes = SaveCodec.Encode(compacted, CurrentCampaignFixture.Checkpoint.Generation,
            validator.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion);
        var loaded = SaveCodec.Decode<CampaignState>(bytes, validator.RulesVersion,
            CurrentCampaignFixture.Catalog.ContentVersion, validate: validator.ValidatePlayerSave);
        Assert.AreEqual(CanonicalJson.Hash(compacted), CanonicalJson.Hash(loaded.State));
        Assert.AreEqual(CurrentCampaignFixture.Checkpoint.Generation, loaded.Generation);
        Console.WriteLine($"Wild history: {original.Exploration.Battles.Length} -> {compacted.Exploration.Battles.Length}");
        Console.WriteLine($"History bytes: {CanonicalJson.Encode(original.Exploration.Battles).Length} -> {CanonicalJson.Encode(compacted.Exploration.Battles).Length}");
    }
}
