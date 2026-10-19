using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedLearnsetTests
{
    [TestMethod]
    public void LevelUpRowsContainOnlyGameplayFieldsAndUseStrictSchema()
    {
        var options = ContentJson.CreateOptions();
        var row = new PokemonLevelUpMove(7, new("pret:MOVE_LEECH_SEED"));
        const string valid = """{"level":7,"move":{"value":"pret:MOVE_LEECH_SEED"}}""";
        Assert.AreEqual(valid, JsonSerializer.Serialize(row, options));
        Assert.AreEqual(row, JsonSerializer.Deserialize<PokemonLevelUpMove>(valid, options));
        foreach (var invalid in new[] {
            """{"level":7,"move":{"value":"pret:MOVE_LEECH_SEED"},"source":{}}""",
            """{"level":7,"move":{"value":"pret:MOVE_LEECH_SEED"},"extra":1}""",
            """{"move":{"value":"pret:MOVE_LEECH_SEED"}}""",
            """{"level":7}""",
            """{"level":null,"move":{"value":"pret:MOVE_LEECH_SEED"}}""" })
            Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<PokemonLevelUpMove>(invalid, options));
    }

    [TestMethod]
    [DataRow("data/pokemon/starters.json", false)]
    [DataRow("data/pokemon/route1/catalog.json", true)]
    public void EarlyCatalogsResolveCanonicalReferencesAndRejectInlineDefinitions(string relative, bool route1)
    {
        var canonical = PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root);
        var original = File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated", relative));
        PokemonCatalog Load(string json) => route1 ? PretRoute1PokemonCatalog.Load(canonical, json) : PretPokemonCatalog.Load(canonical, json);
        var catalog = Load(original);
        Assert.AreEqual(route1 ? 14 : 9, catalog.Species.Count);
        foreach (var species in catalog.Species.Values)
        {
            Assert.AreSame(canonical.Catalog.Species[species.Id].BaseStats, species.BaseStats);
            Assert.IsTrue(canonical.Catalog.Species[species.Id].LevelUpMoves == species.LevelUpMoves);
        }
        foreach (var move in catalog.Moves.Values) Assert.AreSame(canonical.Catalog.Moves[move.Id], move);
        foreach (var mutation in new Action<JsonNode>[] {
            json => json["species"]![0]!["levelUpMoves"] = new JsonArray(),
            json => json["species"]![0]!["source"] = new JsonObject(),
            json => json["species"]![0]!["id"]!["value"] = "pret:SPECIES_UNKNOWN",
            json => json["species"]![0]!.AsObject().Remove("evolutions"),
            json => json["moves"]![0]!["pp"] = 99,
            json => json["moves"]![0]!["value"] = "pret:MOVE_UNKNOWN" })
        {
            var json = JsonNode.Parse(original)!;
            mutation(json);
            Assert.ThrowsExactly<ContentValidationException>(() => Load(json.ToJsonString()));
        }
    }

    [TestMethod]
    [DataRow("data/pokemon/route1/catalog.json", true)]
    [DataRow("data/services/old-man-tutorial.json", false)]
    public void EarlyPokemonArtifactsDoNotStoreDisplayNameCopies(string relative, bool route1)
    {
        var canonical = TestAssets.Cached(() => PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root));
        string routeJson = File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/data/pokemon/route1/catalog.json"));
        var original = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated", relative)))!;
        foreach (string field in new[] { "speciesNames", "moveNames" })
        {
            Assert.IsFalse(original.AsObject().ContainsKey(field));
            var legacy = original.DeepClone();
            legacy[field] = new JsonArray();
            var error = Assert.ThrowsExactly<ContentValidationException>(() =>
            {
                if (route1) PretRoute1PokemonCatalog.Load(canonical, legacy.ToJsonString());
                else PretOldManTutorial.LoadContent(canonical, routeJson, legacy.ToJsonString());
            });
            Assert.AreEqual("json_schema", error.Diagnostics.Single().Code);
        }
    }

    [TestMethod]
    public void OldManCompositionUsesCanonicalDefinitionsAndRejectsBrokenReferences()
    {
        var canonical = TestAssets.Cached(() => PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root));
        string routeJson = File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/data/pokemon/route1/catalog.json"));
        string tutorialJson = File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/data/services/old-man-tutorial.json"));
        var route = PretRoute1PokemonCatalog.Load(canonical, routeJson);
        var original = JsonNode.Parse(tutorialJson)!;
        var catalog = PretOldManTutorial.LoadContent(canonical, routeJson, tutorialJson).Catalog;
        var expectedSpecies = route.Species.Keys.Select(id => id.Value).Concat(
            original["species"]!.AsArray().Select(row => row!["id"]!["value"]!.GetValue<string>())).ToHashSet();
        var expectedMoves = route.Moves.Keys.Select(id => id.Value).Concat(
            original["moves"]!.AsArray().Select(row => row!["value"]!.GetValue<string>())).ToHashSet();
        Assert.IsTrue(expectedSpecies.SetEquals(catalog.Species.Keys.Select(id => id.Value)));
        Assert.IsTrue(expectedMoves.SetEquals(catalog.Moves.Keys.Select(id => id.Value)));
        foreach (var species in catalog.Species.Values)
        {
            var shared = canonical.Catalog.Species[species.Id];
            Assert.AreEqual(shared.SourceSpeciesId, species.SourceSpeciesId);
            Assert.AreSame(shared.BaseStats, species.BaseStats);
            Assert.IsTrue(shared.LevelUpMoves == species.LevelUpMoves);
        }
        foreach (var move in catalog.Moves.Values) Assert.AreSame(canonical.Catalog.Moves[move.Id], move);
        foreach (var mutation in new Action<JsonNode>[] {
            json => json["species"]![0]!["id"]!["value"] = "pret:SPECIES_UNKNOWN",
            json => json["moves"]![0]!["value"] = "pret:MOVE_UNKNOWN",
            json => json["species"]![0]!.AsObject().Remove("evolutions"),
            json => json["species"]![0]!["evolutions"]![0]!["target"]!["value"] = "pret:SPECIES_UNKNOWN",
            json => json["species"]![0]!["evolutions"]![0]!["level"] =
                json["species"]![0]!["evolutions"]![0]!["level"]!.GetValue<int>() + 1 })
        {
            var invalid = original.DeepClone();
            mutation(invalid);
            var error = Assert.ThrowsExactly<ContentValidationException>(() =>
                PretOldManTutorial.LoadContent(canonical, routeJson, invalid.ToJsonString()));
            Assert.AreEqual("json_schema", error.Diagnostics.Single().Code);
        }
    }

    [TestMethod]
    public void CanonicalCatalogRetainsOrderedGameplayValidationWithoutRowProofs()
    {
        var root = TestAssets.Output();
        try
        {
            foreach (var path in new[] { PretCanonicalPokemonCatalog.CatalogRelativePath, PretMetronomeMoveCatalog.CatalogRelativePath })
            {
                var target = Path.Combine(root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(TestAssets.Root, path), target);
            }
            var pathToCatalog = Path.Combine(root, PretCanonicalPokemonCatalog.CatalogRelativePath);
            var original = File.ReadAllText(pathToCatalog);
            var catalog = PretCanonicalPokemonCatalog.Load(root, root);
            Assert.AreEqual(4011, catalog.Catalog.Species.Values.Sum(s => s.LevelUpMoves.Length));
            Assert.AreEqual(184, catalog.Metadata.Values.Sum(s => s.SourceEvolutions.Length));
            foreach (var mutate in InvalidRows().Skip(3))
            {
                var json = JsonNode.Parse(original)!;
                mutate(json["species"]![0]!["levelUpMoves"]!.AsArray());
                File.WriteAllText(pathToCatalog, json.ToJsonString());
                Assert.ThrowsExactly<InvalidDataException>(() => PretCanonicalPokemonCatalog.Load(root, root));
            }
            foreach (var (mutate, index) in InvalidRows().Take(3).Select((mutate, index) => (mutate, index)))
            {
                var json = JsonNode.Parse(original)!;
                mutate(json["species"]![0]!["levelUpMoves"]!.AsArray());
                File.WriteAllText(pathToCatalog, json.ToJsonString());
                var error = Assert.ThrowsExactly<ContentValidationException>(() => PretCanonicalPokemonCatalog.Load(root, root));
                var diagnostic = error.Diagnostics.Single();
                Assert.AreEqual("json_schema", diagnostic.Code);
                Assert.AreEqual(index == 0 ? "$.species[0].levelUpMoves[0].source" : "$.species[0].levelUpMoves[0]", diagnostic.Path);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    private static Action<JsonArray>[] InvalidRows() => [
        rows => rows[0]!["source"] = new JsonObject { ["path"] = "retired-proof.h" },
        rows => rows[0]!.AsObject().Remove("level"),
        rows => rows[0]!.AsObject().Remove("move"),
        rows => rows[0]!["level"] = 0,
        rows => rows[0]!["level"] = 101,
        rows => rows[0]!["level"] = 50,
        rows => rows[0]!["move"]!["value"] = "pret:MOVE_UNKNOWN",
        rows => rows.Clear()
    ];
}
