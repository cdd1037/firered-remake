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
        var admissionOptions = ContentJson.CreateOptions();
        admissionOptions.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var admissions = ContentJson.ParseStrict<GamePokemonAdmissions>(File.ReadAllText(Path.Combine(TestAssets.Root,
            PretGamePokemonCatalog.RelativePath)), admissionOptions);
        Assert.IsTrue(game.Catalog.Species.Keys.ToHashSet().SetEquals(admissions.Owned.Species.Select(id => new SpeciesId(id))));
        Assert.IsTrue(game.Opponents.Catalog.Species.Keys.ToHashSet().SetEquals(admissions.Opponents.Species.Select(id => new SpeciesId(id))));
        Assert.IsTrue(game.TrainerTowerOpponents.Catalog.Species.Keys.ToHashSet().SetEquals(admissions.TrainerTowerOpponents.Species.Select(id => new SpeciesId(id))));
        Assert.IsTrue(game.Catalog.Species.Keys.All(game.Opponents.Catalog.Species.ContainsKey));
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
        Assert.AreSame(game.Catalog.Species[new("pret:SPECIES_BULBASAUR")],
            game.Opponents.Catalog.Species[new("pret:SPECIES_BULBASAUR")]);
        Assert.AreSame(game.Catalog.Species[new("pret:SPECIES_DEOXYS")],
            game.TrainerTowerOpponents.Catalog.Species[new("pret:SPECIES_DEOXYS")]);
    }

    [TestMethod]
    public void AdmissionPolicyRejectsUnknownFormsInvalidEdgesAndDuplicateSpecies()
    {
        var (canonical, mechanics) = Definitions();
        var json = File.ReadAllText(Path.Combine(TestAssets.Root, PretGamePokemonCatalog.RelativePath));
        var invalid = JsonNode.Parse(json)!;
        invalid["definitions"]![0]!["formScope"] = "unknown-form";
        Assert.ThrowsExactly<InvalidDataException>(() => PretGamePokemonCatalog.LoadContent(invalid.ToJsonString(), canonical, mechanics));
        invalid = JsonNode.Parse(json)!;
        invalid["owned"]!["species"]!.AsArray().Add(invalid["owned"]!["species"]![0]!.DeepClone());
        Assert.ThrowsExactly<InvalidDataException>(() => PretGamePokemonCatalog.LoadContent(invalid.ToJsonString(), canonical, mechanics));
        invalid = JsonNode.Parse(json)!;
        invalid["definitions"]![0]!["evolutions"]![0]!["level"] = 1;
        Assert.ThrowsExactly<System.Text.Json.JsonException>(() => PretGamePokemonCatalog.LoadContent(invalid.ToJsonString(), canonical, mechanics));
    }

    [TestMethod]
    public void EvolutionOverridesPreserveActualRosterDifferences()
    {
        var (canonical, mechanics) = Definitions();
        var game = PretGamePokemonCatalog.LoadContent(File.ReadAllText(Path.Combine(TestAssets.Root,
            PretGamePokemonCatalog.RelativePath)), canonical, mechanics);
        foreach (var symbol in new[] { "GOLBAT", "SLOWPOKE", "RHYHORN", "CHANSEY", "DRAGONAIR" })
        {
            var id = new SpeciesId("pret:SPECIES_" + symbol);
            Assert.IsFalse(game.Catalog.Species[id].Evolutions.IsEmpty, symbol);
            Assert.IsTrue(game.Opponents.Catalog.Species[id].Evolutions.IsEmpty, symbol);
            Assert.IsTrue(game.TrainerTowerOpponents.Catalog.Species[id].Evolutions.IsEmpty, symbol);
        }
        foreach (var symbol in new[] { "TOGEPI", "SMOOCHUM" })
        {
            var id = new SpeciesId("pret:SPECIES_" + symbol);
            Assert.IsFalse(game.Catalog.Species[id].Evolutions.IsEmpty, symbol);
            Assert.IsTrue(game.Catalog.Species[id].Evolutions.SequenceEqual(game.Opponents.Catalog.Species[id].Evolutions), symbol);
            Assert.IsTrue(game.TrainerTowerOpponents.Catalog.Species[id].Evolutions.IsEmpty, symbol);
        }
    }

    [TestMethod]
    [DataRow("old-schema")]
    [DataRow("unknown-schema")]
    [DataRow("duplicate-definition")]
    [DataRow("missing-definition")]
    [DataRow("unknown-reference")]
    [DataRow("unused-definition")]
    [DataRow("duplicate-override")]
    [DataRow("unadmitted-override")]
    [DataRow("redundant-override")]
    public void NormalizedPolicyRejectsInvalidReferencesAndOverrides(string mutation)
    {
        var (canonical, mechanics) = Definitions();
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, PretGamePokemonCatalog.RelativePath)))!;
        var definitions = node["definitions"]!.AsArray();
        var overrides = node["opponents"]!["evolutionOverrides"]!.AsArray();
        switch (mutation)
        {
            case "old-schema": node["schemaVersion"] = 1; break;
            case "unknown-schema": node["schemaVersion"] = 3; break;
            case "duplicate-definition": definitions.Add(definitions[0]!.DeepClone()); break;
            case "missing-definition": definitions.RemoveAt(0); break;
            case "unknown-reference": node["owned"]!["species"]![0] = "pret:SPECIES_MISSING"; break;
            case "unused-definition":
                definitions.Add(JsonNode.Parse("""{"id":{"value":"pret:SPECIES_TREECKO"},"evolutions":[]}""")); break;
            case "duplicate-override": overrides.Add(overrides[0]!.DeepClone()); break;
            case "unadmitted-override":
                node["owned"]!["evolutionOverrides"]!.AsArray().Add(JsonNode.Parse(
                    """{"id":{"value":"pret:SPECIES_PINSIR"},"evolutions":[]}""")); break;
            case "redundant-override": overrides.Add(definitions[0]!.DeepClone()); break;
            default: Assert.Fail(mutation); break;
        }
        Assert.ThrowsExactly<InvalidDataException>(() => PretGamePokemonCatalog.LoadContent(node.ToJsonString(), canonical, mechanics));
    }

    [TestMethod]
    [DataRow("unknown-field")]
    [DataRow("missing-roster")]
    [DataRow("missing-overrides")]
    [DataRow("override-form")]
    [DataRow("duplicate-property")]
    public void NormalizedPolicyRetainsStrictJsonGrammar(string mutation)
    {
        var (canonical, mechanics) = Definitions();
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, PretGamePokemonCatalog.RelativePath)))!;
        switch (mutation)
        {
            case "unknown-field": node["definitions"]![0]!["unexpected"] = true; break;
            case "missing-roster": node.AsObject().Remove("owned"); break;
            case "missing-overrides": node["owned"]!.AsObject().Remove("evolutionOverrides"); break;
            case "override-form": node["opponents"]!["evolutionOverrides"]![0]!["formScope"] = "unknown"; break;
            case "duplicate-property": break;
            default: Assert.Fail(mutation); break;
        }
        var json = mutation == "duplicate-property"
            ? node.ToJsonString().Replace("\"schemaVersion\":2", "\"schemaVersion\":2,\"schemaVersion\":2", StringComparison.Ordinal)
            : node.ToJsonString();
        Assert.ThrowsExactly<ContentValidationException>(() => PretGamePokemonCatalog.LoadContent(json, canonical, mechanics));
    }

    [TestMethod]
    public void OverrideCannotIntroduceNoncanonicalEvolution()
    {
        var (canonical, mechanics) = Definitions();
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, PretGamePokemonCatalog.RelativePath)))!;
        node["owned"]!["evolutionOverrides"]!.AsArray().Add(JsonNode.Parse(
            """{"id":{"value":"pret:SPECIES_BULBASAUR"},"evolutions":[{"level":1,"target":{"value":"pret:SPECIES_IVYSAUR"}}]}"""));
        Assert.ThrowsExactly<System.Text.Json.JsonException>(() => PretGamePokemonCatalog.LoadContent(node.ToJsonString(), canonical, mechanics));
    }

    [TestMethod]
    public void CurrentCompositionCarriesBreedingTradeAndBattleRulesAsValues()
    {
        var (canonical, mechanics) = Definitions();
        var trades = PretNpcTrades.Load(PretCurrentWorld.Load(TestAssets.Root), mechanics.Catalog).Trades;
        var game = PretGamePokemonCatalog.Load(TestAssets.Root, canonical, mechanics, trades);
        var breeding = game.Catalog.BreedingData!;
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
