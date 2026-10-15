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
    [DataRow("pokemon/starter-catalog.json", false)]
    [DataRow("route1-pokemon/catalog.json", true)]
    public void EarlyCatalogsRejectObsoleteProofAndInvalidGameplayRows(string relative, bool route1)
    {
        var original = File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated", relative));
        PokemonCatalog Load(string json) => route1 ? PretRoute1PokemonCatalog.Load(json) : PretPokemonCatalog.Load(json);
        var catalog = Load(original);
        Assert.AreEqual(route1 ? 14 : 9, catalog.Species.Count);
        foreach (var mutate in InvalidRows())
        {
            var json = JsonNode.Parse(original)!;
            mutate(json["species"]![0]!["levelUpMoves"]!.AsArray());
            Assert.ThrowsExactly<ContentValidationException>(() => Load(json.ToJsonString()));
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
            foreach (var mutate in InvalidRows().Take(3))
            {
                var json = JsonNode.Parse(original)!;
                mutate(json["species"]![0]!["levelUpMoves"]!.AsArray());
                File.WriteAllText(pathToCatalog, json.ToJsonString());
                Assert.ThrowsExactly<JsonException>(() => PretCanonicalPokemonCatalog.Load(root, root));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void PowerPlantRuntimeCatalogDoesNotCarryUnusedInputInventories()
    {
        var document = PretPowerPlantRules.LoadDocument(TestAssets.Root);
        Assert.AreEqual(152, document.OwnedSpeciesCount);
        Assert.AreEqual(186, document.OpponentSpeciesCount);
        Assert.AreEqual(39, document.Machines.Length);
        Assert.AreEqual(2, document.NewMachines.Length);
        Assert.AreEqual(3, document.Items.Length);
        Assert.AreEqual(7, document.Pickups.Length);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(document, ContentJson.CreateOptions()));
        Assert.IsFalse(json.RootElement.TryGetProperty("inputs", out _));
        Assert.IsFalse(json.RootElement.TryGetProperty("dependencyInputs", out _));
        var presentation = PretPowerPlantRules.LoadPresentationDocument(TestAssets.Root);
        Assert.IsFalse(presentation.TryGetProperty("inputs", out _));
        Assert.IsFalse(presentation.TryGetProperty("inheritedInputs", out _));
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
