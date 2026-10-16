using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class CelioHistoryRetentionTests
{
    internal static CampaignState WithPriorSettlements(CampaignState state, int count)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        for (var i = 0; i < count; i++)
        {
            var before = state.Roamer!;
            var maps = catalog.CelioRoamerMaps!.Where(p => p.Value == before.Runtime.Location &&
                (catalog.LandEncounters!.ContainsKey(p.Key) || catalog.WaterEncounters!.ContainsKey(p.Key))).ToArray();
            // Source roaming includes routes outside this campaign's mapped encounter
            // tables. Move using real source draws until an available route is reached.
            for (var move = 0; maps.Length == 0 && move < 100; move++)
            {
                var moved = SourceRoamerRules.Transition(before, RoamerTransition.Warp,
                    catalog.CelioRoamerMaps![state.World.Player.MapId], state.World.Rng);
                state = state with { Roamer = moved.State, World = state.World with { Rng = moved.Rng } };
                before = moved.State;
                maps = catalog.CelioRoamerMaps!.Where(p => p.Value == before.Runtime.Location &&
                    (catalog.LandEncounters!.ContainsKey(p.Key) || catalog.WaterEncounters!.ContainsKey(p.Key))).ToArray();
            }
            Assert.AreEqual(1, maps.Length, "source relocation must reach a currently mapped encounter route");
            var map = maps[0].Key;
            var method = catalog.LandEncounters!.ContainsKey(map) ? RoamerEncounterMethod.Land : RoamerEncounterMethod.Surfing;
            var origin = new CampaignCelioRoamerEncounter(map, method, before.Individual!, state.TrainerId);
            var outcome = i % 3 == 0 ? RoamerBattleOutcome.MonFled : i % 3 == 1 ? RoamerBattleOutcome.Ran : RoamerBattleOutcome.PlayerTeleported;
            var battle = new RoamerBattleResult(before.Individual!, Math.Max(1, before.Individual!.CurrentHp - 1), 0x40, outcome);
            var result = SourceRoamerRules.Settle(before, battle, state.World.Rng);
            var receipt = new CampaignCelioRoamerSettlement(origin, battle, before, result.State, state.World.Rng, result.Rng, result.Trace);
            var exploration = state.Exploration!;
            state = state with
            {
                Roamer = result.State,
                World = state.World with { Rng = result.Rng },
                SeenSpecies = state.SeenSpecies.Add(SourceRoamerRules.SourceSpeciesId(before.Individual.Species)),
                Exploration = exploration with
                {
                    NextWildBattleId = exploration.NextWildBattleId + 1,
                    Battles = exploration.Battles.Add(new("wild-battle:" + exploration.NextWildBattleId,
                        SourceRoamerRules.SourceSpeciesId(before.Individual.Species), SourceRoamerRules.Level, WildBattleOutcome.Escaped, 0)
                    { CelioRoamer = receipt })
                }
            };
        }
        return state;
    }

    [TestMethod]
    public void ThousandRealSettlementsRetainOneCurrentIndividualAndIdenticalNextEncounter()
    {
        var validator = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var state = WithPriorSettlements(CurrentCampaignFixture.Checkpoint.State, 1000);
        Assert.IsNull(validator.ValidatePlayerSave(state));
        var compacted = CampaignWildHistoryRetention.Compact(state);
        Assert.AreEqual(1, compacted.Exploration!.Battles.Count(b => b.CelioRoamer is not null));
        Console.WriteLine($"Celio settlements: {state.Exploration!.Battles.Count(b => b.CelioRoamer is not null)} -> {compacted.Exploration.Battles.Count(b => b.CelioRoamer is not null)}");
        Console.WriteLine($"Wild history: {state.Exploration.Battles.Length} -> {compacted.Exploration.Battles.Length}");
        Console.WriteLine($"History bytes: {CanonicalJson.Encode(state.Exploration.Battles).Length} -> {CanonicalJson.Encode(compacted.Exploration.Battles).Length}");
        Assert.IsNull(validator.ValidatePlayerSave(compacted));
        Assert.AreSame(state.Roamer, compacted.Roamer);
        Assert.AreSame(state.World, compacted.World);
        Assert.AreEqual(state.Exploration!.NextWildBattleId, compacted.Exploration.NextWildBattleId);
        var before = SourceRoamerRules.RestoreEncounter(state.Roamer!.Individual!);
        var after = SourceRoamerRules.RestoreEncounter(compacted.Roamer!.Individual!);
        Assert.AreEqual(before.Individual, after.Individual);
        CollectionAssert.AreEqual(before.Ivs.ToArray(), after.Ivs.ToArray());
        var nextBattle = new RoamerBattleResult(before.Individual, before.Individual.CurrentHp, before.Individual.Status, RoamerBattleOutcome.Ran);
        var nextBefore = SourceRoamerRules.Settle(state.Roamer, nextBattle, state.World.Rng);
        var nextAfter = SourceRoamerRules.Settle(compacted.Roamer, nextBattle, compacted.World.Rng);
        Assert.AreEqual(CanonicalJson.Hash(nextBefore), CanonicalJson.Hash(nextAfter));
        var bytes = SaveCodec.Encode(compacted, 1, validator.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion);
        var restored = SaveCodec.Decode<CampaignState>(bytes, validator.RulesVersion,
            CurrentCampaignFixture.Catalog.ContentVersion, validate: validator.ValidatePlayerSave);
        Assert.AreEqual(CanonicalJson.Hash(compacted), CanonicalJson.Hash(restored.State));
    }

    [TestMethod]
    public void CurrentIndividualMustMatchLatestSettlementOrInitialization()
    {
        var validator = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var original = CurrentCampaignFixture.Checkpoint.State;
        var neverSettled = original with { Roamer = original.Roamer! with
        { Individual = original.Roamer.Individual! with { CurrentHp = original.Roamer.Individual.CurrentHp - 1 } } };
        Assert.AreEqual("save_celio_roamer_live_settlement", validator.ValidatePlayerSave(neverSettled));
        var settled = CampaignWildHistoryRetention.Compact(WithPriorSettlements(original, 3));
        Assert.IsNull(validator.ValidatePlayerSave(settled));
        var history = settled.Exploration!.Battles.Single(b => b.CelioRoamer is not null);
        var receipt = history.CelioRoamer!;
        var after = receipt.After;
        var invalidStates = new[]
        {
            after with { Individual = after.Individual! with { Beauty = (byte)(after.Individual.Beauty ^ 1) } },
            after with { Runtime = after.Runtime with { Location = new RoamerMap(99, 99) } },
            after with { Runtime = after.Runtime with { PlayerHistory = after.Runtime.PlayerHistory.SetItem(0, new RoamerMap(99, 99)) } },
            after with { Runtime = after.Runtime with { PlayerHistory = default } }
        };
        foreach (var invalid in invalidStates)
        {
            var changed = history with { CelioRoamer = receipt with { After = invalid } };
            var tampered = settled with { Exploration = settled.Exploration with
                { Battles = settled.Exploration.Battles.Replace(history, changed) } };
            Assert.AreEqual("save_wild_history_invalid", validator.ValidatePlayerSave(tampered));
        }

        var wrongHp = settled with { Roamer = settled.Roamer! with
        { Individual = settled.Roamer.Individual! with { CurrentHp = settled.Roamer.Individual.CurrentHp - 1 } } };
        Assert.AreEqual("save_celio_roamer_live_settlement", validator.ValidatePlayerSave(wrongHp));
        var wrongStatus = settled with { Roamer = settled.Roamer! with
        { Individual = settled.Roamer.Individual! with { Status = 0 } } };
        Assert.AreEqual("save_celio_roamer_live_settlement", validator.ValidatePlayerSave(wrongStatus));
        var moved = SourceRoamerRules.Transition(settled.Roamer!, RoamerTransition.Warp,
            CurrentCampaignFixture.Catalog.CelioRoamerMaps![settled.World.Player.MapId], settled.World.Rng);
        var relocated = settled with { Roamer = moved.State, World = settled.World with { Rng = moved.Rng } };
        Assert.IsNull(validator.ValidatePlayerSave(relocated), "movement may change runtime location after the latest settlement");
    }
}
