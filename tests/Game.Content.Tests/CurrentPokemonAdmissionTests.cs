using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class CurrentPokemonAdmissionTests
{
    private static (PretCanonicalPokemonCatalog Canonical, SourceMechanicsCatalog Mechanics) Definitions()
    {
        var canonical = PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root);
        return (canonical, SourceMechanicsCatalogLoader.LoadAuthenticated(canonical, TestAssets.Root));
    }

    [TestMethod]
    public void CurrentRostersAreExplicitAndPreserveProjectedEvolutionsAndForms()
    {
        var (canonical, mechanics) = Definitions();
        var game = PretGamePokemonCatalog.LoadContent(File.ReadAllText(Path.Combine(TestAssets.Root,
            PretGamePokemonCatalog.RelativePath)), canonical, mechanics);
        Assert.AreEqual(189, game.Catalog.Species.Count);
        Assert.AreEqual(212, game.Opponents.Catalog.Species.Count);
        Assert.AreEqual(204, game.TrainerTowerOpponents.Catalog.Species.Count);
        Assert.IsTrue(canonical.Catalog.Species.ContainsKey(new("pret:SPECIES_TREECKO")));
        Assert.IsFalse(game.Catalog.Species.ContainsKey(new("pret:SPECIES_TREECKO")));
        Assert.AreEqual(42, game.Catalog.Species[new("pret:SPECIES_RHYHORN")].Evolutions.Single().Level);
        Assert.AreEqual(PokemonEvolutionMethod.Friendship, game.Catalog.Species[new("pret:SPECIES_CHANSEY")].Evolutions.Single().Method);
        foreach (var roster in new[] { game.Catalog, game.Opponents.Catalog, game.TrainerTowerOpponents.Catalog })
        {
            Assert.AreEqual(new SixStats(50, 180, 20, 150, 180, 20), roster.Species[new("pret:SPECIES_DEOXYS")].BaseStats);
            Assert.AreSame(mechanics.Catalog.MechanicsData, roster.MechanicsData);
            Assert.AreSame(canonical.Catalog.Moves, roster.Moves);
        }
        Assert.IsTrue(canonical.Catalog.Species[new("pret:SPECIES_CHANSEY")].Evolutions.IsEmpty);
    }

    [TestMethod]
    public void AdmissionPolicyRejectsUnknownFormsInvalidEdgesAndDuplicateSpecies()
    {
        var (canonical, mechanics) = Definitions();
        var json = File.ReadAllText(Path.Combine(TestAssets.Root, PretGamePokemonCatalog.RelativePath));
        var invalid = JsonNode.Parse(json)!;
        invalid["owned"]![0]!["formScope"] = "unknown-form";
        Assert.ThrowsExactly<InvalidDataException>(() => PretGamePokemonCatalog.LoadContent(invalid.ToJsonString(), canonical, mechanics));
        invalid = JsonNode.Parse(json)!;
        invalid["owned"]!.AsArray().Add(invalid["owned"]![0]!.DeepClone());
        Assert.ThrowsExactly<InvalidDataException>(() => PretGamePokemonCatalog.LoadContent(invalid.ToJsonString(), canonical, mechanics));
        invalid = JsonNode.Parse(json)!;
        invalid["owned"]![0]!["evolutions"]![0]!["level"] = 1;
        Assert.ThrowsExactly<System.Text.Json.JsonException>(() => PretGamePokemonCatalog.LoadContent(invalid.ToJsonString(), canonical, mechanics));
    }

    [TestMethod]
    public void CurrentCompositionCarriesBreedingTradeAndBattleRulesAsValues()
    {
        var (canonical, mechanics) = Definitions();
        var trades = PretNpcTrades.Load(TestAssets.Root, mechanics.Catalog).Trades;
        var game = PretGamePokemonCatalog.Load(TestAssets.Root, canonical, mechanics, trades);
        var breeding = game.Catalog.BreedingData!;
        Assert.AreEqual(189, breeding.Species.Count);
        Assert.IsTrue(breeding.Species.Keys.ToHashSet().SetEquals(game.Catalog.Species.Keys));
        Assert.AreEqual(58, breeding.AllMachineMoves.Length);
        Assert.AreEqual(new MoveId("pret:MOVE_DIVE"), breeding.AllMachineMoves[^1]);
        Assert.AreEqual(new SpeciesId("pret:SPECIES_PICHU"), breeding.Species[new("pret:SPECIES_PIKACHU")].BaseEggSpecies);
        Assert.AreEqual(9, game.Catalog.NpcTrades.Count);
        Assert.AreEqual(new Game.Domain.Ids.MapId("map.cerulean-city-house-3"), game.Catalog.NpcTrades[1].Map);
        Assert.AreSame(game.Catalog.BreedingData, (game.Catalog with { }).BreedingData);
        Assert.IsNotNull(game.Catalog.BattleItems);
    }
}
