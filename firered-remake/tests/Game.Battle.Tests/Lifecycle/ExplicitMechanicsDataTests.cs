using System.Text.Json;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Content;
using Game.Domain.Pokemon;
using Game.Domain.Battle;
using Game.Domain.Randomness;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Battle.Lifecycle;

[TestClass, TestCategory("Game")]
public sealed class ExplicitMechanicsDataTests
{
    [TestMethod]
    public void CopiedDefinitionsRetainMechanicsWithoutRegistration()
    {
        var original = TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
        var copy = original.Catalog with { };
        Assert.AreNotSame(original.Catalog, copy);
        Assert.AreSame(original.Data, copy.MechanicsData);
        Assert.AreSame(original.Catalog.BattleItems, copy.BattleItems);
        var composed = SourceMechanicsCatalog.FromDefinitions(copy);
        var shedinja = SourcePokemonFactory.Create(composed,
            new(new("pret:SPECIES_SHEDINJA"), 50, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new RngState(0)).Pokemon;
        Assert.AreEqual(1, shedinja.Stats.MaxHp);
        Assert.IsNull(SourcePokemonValidator.ValidatePermanent(composed, shedinja));
        Assert.AreEqual(original.FormStatOverrides, composed.FormStatOverrides);
        Assert.AreEqual(original.Abilities.Count, composed.Abilities.Count);
        var deoxys = SourcePokemonFactory.Create(composed,
            new(new("pret:SPECIES_DEOXYS"), 50, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new RngState(0)).Pokemon;
        Assert.AreEqual(200, deoxys.Stats.Attack);
        Assert.AreEqual(40, deoxys.Stats.Defense);
        Assert.AreEqual(200, deoxys.Stats.SpecialAttack);
        Assert.AreEqual(40, deoxys.Stats.SpecialDefense);
        Assert.AreEqual(170, deoxys.Stats.Speed);
        Assert.AreEqual("ABILITY_PRESSURE", deoxys.AbilityId);
        Assert.IsNull(SourcePokemonValidator.ValidatePermanent(composed, deoxys));
    }

    [TestMethod]
    public void CampaignProfileRequiresExplicitCampaignData()
    {
        var frame = new SourceBattleFrame {
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            SourceCampaignRulesVersion = GamePokemonCatalog.Profile
        };
        Assert.AreEqual("source_frame_campaign_profile", SourceBattleIdentityRules.Validate(frame));
    }
    [TestMethod]
    [DataRow(TrainerTowerChallengeKind.Single)]
    [DataRow(TrainerTowerChallengeKind.Double)]
    public void TowerRosterSurvivesJsonWithoutRegistry(TrainerTowerChallengeKind course)
    {
        var mechanics = TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
        var canonical = PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root);
        var game = PretGamePokemonCatalog.LoadContent(
            File.ReadAllText(Path.Combine(TestAssets.Root, PretGamePokemonCatalog.RelativePath)), canonical, mechanics);
        var tower = PretTrainerTowerRules.Load(TestAssets.Root);
        var created = tower.CreateBattle(game.TrainerTowerOpponents, course, 0, 0, 50, new RngState(42));
        var restored = JsonSerializer.Deserialize<TrainerPartyCreationResult>(JsonSerializer.Serialize(created))!;
        Assert.IsNotNull(restored.TowerRoster);
        Assert.AreEqual(created.Party.Length, restored.TowerRoster.Party.Length);
        Assert.IsTrue(restored.Trace.IsEmpty);
        WildBattleCalledPartyMember Player(uint pid) => new(SourcePokemonFactory.Create(mechanics,
            new(new("pret:SPECIES_CLEFAIRY"), 50, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new RngState(0)).Pokemon);
        var entry = SourceTrainerTowerEncounter.Begin(game.TrainerTowerOpponents, restored, [Player(0), Player(1)]);
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        var resumed = JsonSerializer.Deserialize<SourceBattleFrame>(JsonSerializer.Serialize(entry.State))! with { Campaign = game };
        Assert.IsNull(SourceTrainerTowerEncounter.Validate(mechanics.Catalog, resumed));
        Assert.IsNull(SourceFourActionTurns.ValidateCapability(mechanics.Catalog, resumed));
        Assert.AreEqual(created.Party[0].Pokemon.AbilitySlot, resumed.TrainerTowerEncounter!.Roster.Party[0].Pokemon.AbilitySlot);
    }

}
