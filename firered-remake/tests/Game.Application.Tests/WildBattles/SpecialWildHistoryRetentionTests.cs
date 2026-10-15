using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain.Encounters;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class SpecialWildHistoryRetentionTests
{
    [TestMethod]
    [DataRow("event-island")]
    [DataRow("mewtwo")]
    [DataRow("zapdos")]
    public void ThousandSpecialRetriesCompactWithFullSaveValidationAndCaptureLineage(string family)
    {
        var original = CurrentCampaignFixture.Checkpoint.State;
        bool Matches(CampaignWildHistory b) => family switch
        {
            "event-island" => b.EventIslandReceipt is not null,
            "mewtwo" => b.MewtwoReceipt is not null,
            "zapdos" => b.PowerPlantReceipt?.Origin.Kind == PowerPlantEncounterKind.Zapdos,
            _ => false
        };
        var terminal = original.Exploration!.Battles.Single(b => Matches(b) && b.Outcome == WildBattleOutcome.Caught);
        var expanded = new List<CampaignWildHistory>();
        foreach (var battle in original.Exploration.Battles)
        {
            if (battle == terminal)
                for (var i = 0; i < 1000; i++)
                    expanded.Add(terminal with { Id = "retry:" + i, Outcome = WildBattleOutcome.Lost, ExperienceGained = 0 });
            expanded.Add(battle);
        }
        // Construct an accepted historical model, not a claim of replayed gameplay.
        // IDs and their real capture/evolution references are renumbered together.
        var ids = expanded.Select((b, i) => (b.Id, Next: "wild-battle:" + (i + 1))).ToDictionary(x => x.Id, x => x.Next);
        var state = original with
        {
            Exploration = original.Exploration with
            {
                NextWildBattleId = expanded.Count + 1L,
                Battles = expanded.Select(b => b with { Id = ids[b.Id] }).ToImmutableArray(),
                Captured = original.Exploration.Captured!.Value.Select(c => c with { BattleId = ids[c.BattleId] }).ToImmutableArray()
            },
            EvolutionHistory = original.EvolutionHistory!.Value.Select(e => e.BattleId is { } id && ids.TryGetValue(id, out var next)
                ? e with { BattleId = next } : e).ToImmutableArray()
        };
        var validator = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        Assert.IsNull(validator.ValidatePlayerSave(state), "expanded historical model");
        var compacted = CampaignWildHistoryRetention.Compact(state);
        Assert.AreEqual(1, compacted.Exploration!.Battles.Count(Matches));
        Assert.IsNull(validator.ValidatePlayerSave(compacted), "compacted historical model");
        Assert.IsTrue(state.Exploration.Captured == compacted.Exploration.Captured);
        Assert.IsTrue(state.EvolutionHistory == compacted.EvolutionHistory);
        Assert.AreSame(state.World, compacted.World);
        Assert.AreSame(state.Script, compacted.Script);
        Assert.AreEqual(state.Exploration.NextWildBattleId, compacted.Exploration.NextWildBattleId);
        var bytes = SaveCodec.Encode(compacted, 1, validator.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion);
        var restored = SaveCodec.Decode<CampaignState>(bytes, validator.RulesVersion,
            CurrentCampaignFixture.Catalog.ContentVersion, validate: validator.ValidatePlayerSave);
        Assert.AreEqual(CanonicalJson.Hash(compacted), CanonicalJson.Hash(restored.State));
    }

    [TestMethod]
    public void ArticunoGenerationOrderAndLatestEscapeSurviveThousandAttempts()
    {
        var original = CurrentCampaignFixture.Checkpoint.State;
        var catalog = CurrentCampaignFixture.Catalog;
        var rng = original.World.Rng;
        var attempts = ImmutableArray.CreateBuilder<CampaignWildHistory>();
        for (var i = 1; i <= 1000; i++)
        {
            var made = SeafoamPokemonFactory.GenerateArticuno(catalog.SeafoamOwnership!, rng, original.TrainerId);
            attempts.Add(new("wild-battle:" + i, 144, 50, i == 1000 ? WildBattleOutcome.Escaped : WildBattleOutcome.Lost, 0)
            { SeafoamReceipt = new(SeafoamWildBattleContract.ArticunoOrigin, rng, made.GlobalRng, made.Pokemon) });
            rng = made.GlobalRng;
        }
        var state = original with
        {
            World = original.World with { Rng = rng },
            Script = original.Script with { Flags = original.Script.Flags.SetItem("FLAG_HIDE_ARTICUNO", true).SetItem("FLAG_FOUGHT_ARTICUNO", false) },
            Exploration = original.Exploration! with { NextWildBattleId = 1001, Battles = attempts.ToImmutable(), Captured = [] },
            EvolutionHistory = []
        };
        var validator = CampaignSession.CreateCurrent(catalog);
        var validate = typeof(CampaignSession).GetMethod("ValidateSeafoamEncounterProgress", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsNull(validate.Invoke(validator, [state]), "source-generated chronological attempt fixture");
        var compacted = CampaignWildHistoryRetention.Compact(state);
        Assert.AreEqual(1, compacted.Exploration!.Battles.Length);
        Assert.AreSame(state.Exploration.Battles[^1], compacted.Exploration.Battles[0]);
        Assert.IsNull(validate.Invoke(validator, [compacted]));
        Assert.AreSame(state.World, compacted.World);
    }

    [TestMethod]
    public void EventIslandHallRenewalAndReferencedEvolutionWinRemainAuthoritative()
    {
        var original = CurrentCampaignFixture.Checkpoint.State;
        var template = original.Exploration!.Battles.First(b => b.EventIslandReceipt?.Origin.Kind == EventIslandEncounterKind.Deoxys);
        var first = template with { Id = "wild-battle:1", Outcome = WildBattleOutcome.Won, ExperienceGained = 100,
            EventIslandReceipt = template.EventIslandReceipt! with { HallEntries = 1 } };
        var middle = template with { Id = "wild-battle:2", Outcome = WildBattleOutcome.Lost, ExperienceGained = 0,
            EventIslandReceipt = template.EventIslandReceipt! with { HallEntries = 2 } };
        var latest = first with { Id = "wild-battle:3", EventIslandReceipt = first.EventIslandReceipt! with { HallEntries = 2 } };
        var state = original with
        {
            Exploration = original.Exploration with { NextWildBattleId = 4, Battles = [first, middle, latest], Captured = [] },
            EvolutionHistory = [new(1, first.Id, 1, 2, 15, 16)],
            Script = original.Script with { Flags = original.Script.Flags.SetItem("FLAG_FOUGHT_DEOXYS", false)
                .SetItem("FLAG_DEOXYS_FLEW_AWAY", true) }
        };
        var validator = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var validate = typeof(CampaignSession).GetMethod("ValidateEventIslandBattleProgress", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsNull(validate.Invoke(validator, [state]), "Hall-of-Fame renewal permits the second defeat");
        var compacted = CampaignWildHistoryRetention.Compact(state);
        CollectionAssert.AreEqual(new[] { first.Id, latest.Id }, compacted.Exploration!.Battles.Select(b => b.Id).ToArray());
        Assert.IsNull(validate.Invoke(validator, [compacted]));
        var noRenewal = compacted with { Exploration = compacted.Exploration with
        { Battles = [first, latest with { EventIslandReceipt = latest.EventIslandReceipt! with { HallEntries = 1 } }] } };
        Assert.AreEqual("save_event_island_reencounter", validate.Invoke(validator, [noRenewal]));
        var missingFlew = compacted with { Script = compacted.Script with
        { Flags = compacted.Script.Flags.SetItem("FLAG_DEOXYS_FLEW_AWAY", false) } };
        Assert.AreEqual("save_event_island_completion", validate.Invoke(validator, [missingFlew]));
    }

    [TestMethod]
    public void AcceptedMewtwoHideStateRetainsItsEscapeReasonAndLatestAttempt()
    {
        var original = CurrentCampaignFixture.Checkpoint.State;
        var template = original.Exploration!.Battles.First(b => b.MewtwoReceipt is not null);
        var escaped = template with { Id = "wild-battle:1", Outcome = WildBattleOutcome.Escaped, ExperienceGained = 0 };
        var lost = template with { Id = "wild-battle:2", Outcome = WildBattleOutcome.Lost, ExperienceGained = 0 };
        var state = original with
        {
            Exploration = original.Exploration with { NextWildBattleId = 3, Battles = [escaped, lost], Captured = [] },
            EvolutionHistory = [],
            Script = original.Script with { Flags = original.Script.Flags.SetItem("FLAG_HIDE_MEWTWO", true).SetItem("FLAG_FOUGHT_MEWTWO", false) }
        };
        var validator = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var validate = typeof(CampaignSession).GetMethod("ValidateCeruleanCaveProgress", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsNull(validate.Invoke(validator, [state]), "accepted old hide-state contract, not a newly enabled gameplay path");
        var compacted = CampaignWildHistoryRetention.Compact(state);
        Assert.AreEqual(2, compacted.Exploration!.Battles.Length);
        Assert.IsNull(validate.Invoke(validator, [compacted]));
        var withoutReason = state with { Exploration = state.Exploration with { Battles = [lost] } };
        Assert.AreEqual("save_mewtwo_hide_receipt", validate.Invoke(validator, [withoutReason]));
    }
}
