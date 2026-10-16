using System.Reflection;
using Game.Application;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Roaming;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class RetiredSpecialEncounterTests
{
    // These are controlled settlement fixtures, not a replay of the story or capture commands.
    private static object? Invoke(string method, CampaignState state) =>
        typeof(CampaignSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog), [state]);

    private static CampaignState Compact(CampaignState state) =>
        (CampaignState)Invoke("CompactCurrentPokemonSources", state)!;

    private static void Roundtrip(CampaignState state, string validator)
    {
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var bytes = SaveCodec.Encode(state, 1, session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion);
        var restored = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion,
            CurrentCampaignFixture.Catalog.ContentVersion, validate: s => (string?)Invoke(validator, s));
        Assert.AreEqual(CanonicalJson.Hash(state), CanonicalJson.Hash(restored.State));
    }

    [TestMethod]
    public void LostelleCaughtSettlementSurvivesDepartureWithoutRequiringOwnedHypno()
    {
        var original = CurrentCampaignFixture.Checkpoint.State;
        var receipt = original.Exploration!.Battles.Single(b => b.LostelleHypno is not null);
        var caught = receipt with { Id = "wild-battle:1", Outcome = WildBattleOutcome.Caught, ExperienceGained = 0 };
        var mon = caught.LostelleHypno!.Generated;
        var state = original with
        {
            NextPokemonId = original.NextPokemonId + 1,
            Exploration = original.Exploration with
            {
                NextWildBattleId = 3,
                Battles = [caught, new("wild-battle:2", 16, 3, WildBattleOutcome.Escaped, 0)],
                Captured = [new(original.NextPokemonId, caught.Id, 97, mon.Personality, mon.Ivs, 30, "MAPSEC_BERRY_FOREST")]
            },
            EvolutionHistory = []
        };
        var compacted = Compact(state);
        Assert.AreEqual(0, compacted.Exploration!.Captured!.Value.Length);
        Assert.AreSame(caught, compacted.Exploration.Battles[0]);
        Assert.IsTrue(compacted.Script.Flags["FLAG_RESCUED_LOSTELLE"]);
        Assert.IsNull(Invoke("ValidateFirstSeviiBattleProgress", compacted));
        Roundtrip(compacted, "ValidateFirstSeviiBattleProgress");
        Assert.AreEqual("save_first_sevii_hypno_rescue", Invoke("ValidateFirstSeviiBattleProgress",
            compacted with { Script = compacted.Script with
            { Flags = compacted.Script.Flags.SetItem("FLAG_RESCUED_LOSTELLE", false) } }));
        Assert.AreEqual("save_first_sevii_hypno_receipt", Invoke("ValidateFirstSeviiBattleProgress",
            compacted with { Exploration = compacted.Exploration with
            { Battles = [caught, caught with { Id = "wild-battle:2" }] } }));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UnownRepresentativeSurvivesCompactionOfFirstSeenOrRetiredCapture(bool caught)
    {
        var original = CurrentCampaignFixture.Checkpoint.State;
        var origin = new SevenIslandWildOrigin(new("map.seven-island-tanoby-ruins-monean-chamber"), SevenIslandEncounterKind.Land, 0);
        var made = SevenIslandWildPokemonFactory.GenerateSourceSlot(CurrentCampaignFixture.Catalog.SevenIslandOwnership!,
            origin, 25, original.World.Rng, original.TrainerId);
        var history = new CampaignWildHistory("wild-battle:1", 201, 25,
            caught ? WildBattleOutcome.Caught : WildBattleOutcome.Escaped, 0)
        { SevenIsland = origin, SevenIslandReceipt = new(original.World.Rng, made.GlobalRng, made.Pokemon) };
        var state = original with
        {
            NextPokemonId = original.NextPokemonId + 1,
            World = original.World with { Rng = made.GlobalRng },
            Script = original.Script with { Flags = original.Script.Flags.SetItem("FLAG_SYS_UNLOCKED_TANOBY_RUINS", true) },
            SeenSpecies = original.SeenSpecies.Add(201),
            CaughtSpecies = caught ? original.CaughtSpecies.Add(201) : original.CaughtSpecies.Remove(201),
            UnownPokedexPersonality = made.Pokemon.Personality,
            EvolutionHistory = [],
            Exploration = original.Exploration! with
            {
                NextWildBattleId = 3,
                Battles = [history, new("wild-battle:2", 16, 3, WildBattleOutcome.Escaped, 0)],
                Captured = caught ? [new(original.NextPokemonId, history.Id, 201, made.Pokemon.Personality,
                    made.Pokemon.Ivs, 25, "MAPSEC_TANOBY_RUINS") { SevenIsland = origin }] : []
            }
        };
        Assert.IsNull(Invoke("ValidateUnownPokedex", state));
        var compacted = Compact(state);
        Assert.AreEqual(0, compacted.Exploration!.Captured!.Value.Length);
        Assert.AreEqual(1, compacted.Exploration.Battles.Length);
        Assert.IsFalse(compacted.Exploration.Battles.Any(b => b.SourceSpeciesId == 201));
        Assert.AreEqual(made.Pokemon.Personality, compacted.UnownPokedexPersonality);
        Assert.AreSame(state.SeenSpecies, compacted.SeenSpecies);
        Assert.AreSame(state.CaughtSpecies, compacted.CaughtSpecies);
        Assert.IsNull(Invoke("ValidateUnownPokedex", compacted));
        Roundtrip(compacted, "ValidateUnownPokedex");
        Assert.AreEqual("save_unown_pokedex_representative", Invoke("ValidateUnownPokedex",
            compacted with { UnownPokedexPersonality = null }));
    }

    [TestMethod]
    public void RetiredRoamerCaptureKeepsTerminalSettlementAndCannotReactivate()
    {
        var original = CurrentCampaignFixture.Checkpoint.State;
        var catalog = CurrentCampaignFixture.Catalog;
        var map = new MapId("map.route-1");
        var before = original.Roamer! with { Runtime = original.Roamer!.Runtime with { Location = catalog.CelioRoamerMaps![map] } };
        var individual = before.Individual!;
        var battle = new RoamerBattleResult(individual, individual.CurrentHp, individual.Status, RoamerBattleOutcome.Caught);
        var settled = SourceRoamerRules.Settle(before, battle, original.World.Rng);
        var encounter = new CampaignCelioRoamerEncounter(map, RoamerEncounterMethod.Land, individual, original.TrainerId);
        var history = new CampaignWildHistory("wild-battle:1", SourceRoamerRules.SourceSpeciesId(individual.Species),
            SourceRoamerRules.Level, WildBattleOutcome.Caught, 0)
        { CelioRoamer = new(encounter, battle, before, settled.State, original.World.Rng, settled.Rng, settled.Trace) };
        var ivs = SourceRoamerRules.UnpackIvs(individual.PackedIvs);
        var state = original with
        {
            NextPokemonId = original.NextPokemonId + 1,
            Roamer = settled.State,
            World = original.World with { Rng = settled.Rng },
            EvolutionHistory = [],
            Exploration = original.Exploration! with
            {
                NextWildBattleId = 3,
                Battles = [history, new("wild-battle:2", 16, 3, WildBattleOutcome.Escaped, 0)],
                Captured = [new(original.NextPokemonId, history.Id, history.SourceSpeciesId, individual.Personality,
                    new SixStats(ivs[0], ivs[1], ivs[2], ivs[3], ivs[4], ivs[5]), 50, "MAPSEC_ROUTE_1") { CelioRoamer = encounter }]
            }
        };
        Assert.IsNull(Invoke("ValidateCelioRoamerProgress", state));
        var compacted = Compact(state);
        Assert.AreEqual(0, compacted.Exploration!.Captured!.Value.Length);
        Assert.AreSame(history, compacted.Exploration.Battles[0]);
        Assert.IsFalse(compacted.Roamer!.Individual!.Active);
        Assert.AreSame(state.Script.Flags, compacted.Script.Flags);
        Assert.IsNull(Invoke("ValidateCelioRoamerProgress", compacted));
        Roundtrip(compacted, "ValidateCelioRoamerProgress");
        Assert.ThrowsExactly<InvalidOperationException>(() => SourceRoamerRules.RestoreEncounter(compacted.Roamer.Individual));
        Assert.AreEqual("save_celio_roamer_live_settlement", Invoke("ValidateCelioRoamerProgress",
            compacted with { Roamer = compacted.Roamer with { Individual = compacted.Roamer.Individual with { Active = true } } }));
    }
}
