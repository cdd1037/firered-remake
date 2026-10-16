using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class OrdinaryFishingContentTests
{
    private static string Json => File.ReadAllText(Path.Combine(TestAssets.Root,
        "content/generated/data/encounters/fishing.json"));

    private static FishingTaskProfile Profile(ImmutableArray<FishingRodTable> rows)
    {
        var canonical = PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root);
        var held = rows.SelectMany(t => t.Table.Slots).Select(s => s.Species).Distinct()
            .Select(id => new FishingHeldItem(id, canonical.Metadata[id].ItemCommon, canonical.Metadata[id].ItemRare)).ToImmutableArray();
        return new("test-ordinary-fishing", rows, [], held);
    }

    [TestMethod]
    public void GeneratedOrdinaryDefinitionsHaveCompleteOwnedSpeciesAndLevels()
    {
        var document = PretOrdinaryFishing.Parse(Json);
        foreach (var rod in Enum.GetValues<FishingRod>()) Assert.AreEqual(46, document.Count(row => row.Rod == rod));
        Assert.IsTrue(document.All(row => !row.Table.Map.Value.StartsWith("map.safari-zone-", StringComparison.Ordinal)));
        var pokemon = SharedMachineCatalogTests.Pokemon();
        var profile = Profile(document);
        foreach (var row in document)
        foreach (var slot in row.Table.Slots)
        foreach (byte level in Enumerable.Range(slot.MinimumLevel, slot.MaximumLevel - slot.MinimumLevel + 1).Select(n => (byte)n))
        {
            var origin = new FishingEncounterOrigin(row.Rod, row.Table.Map, slot.Index, slot.Species, level);
            var made = profile.GeneratePokemon(pokemon, origin, new(123), 42);
            Assert.IsTrue(FishingBattleContract.SourceInitialInstance(pokemon, origin, made.Pokemon, profile));
            Assert.IsFalse(string.IsNullOrEmpty(FishingBattleContract.HeldItem(origin, 99, profile)));
        }
    }

    [TestMethod]
    public void LegalGeneratedEditControlsGoodRodAndExistingSeafoamReceipts()
    {
        var json = JsonNode.Parse(Json)!.AsObject();
        var table = json["tables"]!.AsArray().Single(n => n!["rod"]!.GetValue<string>() == "good" && n["table"]!["map"]!["value"]!.GetValue<string>() == "map.seafoam-islands-b3f")!["table"]!;
        table["slots"]![0]!["minimumLevel"] = 16;
        table["slots"]![0]!["maximumLevel"] = 16;
        var changed = PretOrdinaryFishing.Parse(json.ToJsonString());
        var profile = Profile(changed);
        var origin = new SeafoamWildOrigin(new("map.seafoam-islands-b3f"), SeafoamEncounterKind.GoodRod, 2);
        Assert.AreEqual((byte)16, SeafoamWildBattleContract.SourceSlot(origin, profile)!.MinimumLevel);
        Assert.IsNull(SeafoamWildBattleContract.SourceSlot(origin), "Good Rod historical receipt needs current generated definitions");
        Assert.IsNull(SeafoamWildBattleContract.SourceSlot(origin with { Map = new("map.one-island") }, profile),
            "The shared definitions cannot authorize a non-Seafoam map as a Seafoam receipt");
        var owned = SharedMachineCatalogTests.Pokemon();
        var made = SeafoamPokemonFactory.GenerateSourceSlot(owned, origin, 16, new(77), 42, profile);
        Assert.IsTrue(SeafoamPokemonFactory.SourceInitialInstance(owned, origin, made.Pokemon, profile));
        var original = Profile(PretOrdinaryFishing.Parse(Json));
        Assert.IsFalse(SeafoamWildBattleContract.Matches(origin, made.Pokemon, original));
        table["slots"]![0]!["weight"] = 61;
        Assert.Throws<InvalidDataException>(() => PretOrdinaryFishing.Parse(json.ToJsonString()));
    }

    [TestMethod]
    public void EditedGoodRodSpeciesUsesCanonicalHeldPairRatherThanRodSpecificCases()
    {
        var json = JsonNode.Parse(Json)!.AsObject();
        var row = json["tables"]!.AsArray().Single(n => n!["rod"]!.GetValue<string>() == "good" && n["table"]!["map"]!["value"]!.GetValue<string>() == "map.seafoam-islands-b3f")!["table"]!;
        row["slots"]![0]!["species"]!["value"] = "pret:SPECIES_SHELLDER";
        var profile = Profile(PretOrdinaryFishing.Parse(json.ToJsonString()));
        var origin = new FishingEncounterOrigin(FishingRod.Good, new("map.seafoam-islands-b3f"), 2, new("pret:SPECIES_SHELLDER"), 5);
        Assert.AreEqual("ITEM_NONE", FishingBattleContract.HeldItem(origin, 44, profile));
        Assert.AreEqual("ITEM_PEARL", FishingBattleContract.HeldItem(origin, 45, profile));
        Assert.AreEqual("ITEM_PEARL", FishingBattleContract.HeldItem(origin, 94, profile));
        Assert.AreEqual("ITEM_BIG_PEARL", FishingBattleContract.HeldItem(origin, 95, profile));
        var seafoam = new SeafoamWildOrigin(origin.Map, SeafoamEncounterKind.GoodRod, origin.SlotIndex);
        var owned = SharedMachineCatalogTests.Pokemon();
        var mon = SeafoamPokemonFactory.GenerateSourceSlot(owned, seafoam, 5, new(42), 123, profile);
        Assert.AreEqual(origin.Species, mon.Pokemon.Species);
        var assigned = SeafoamPokemonFactory.AssignHeldItem(seafoam, new(42), profile);
        Assert.AreEqual(profile.HeldItem(origin, assigned.RandomTrace.Single().Result), assigned.HeldItem);
        Assert.AreEqual((ulong)1, assigned.GlobalRng.Cursor);
    }

    [TestMethod]
    public void StrictGeneratedLoaderRejectsEmptyDuplicateAndBadSourceRanges()
    {
        var json = JsonNode.Parse(Json)!.AsObject();
        json["tables"] = new JsonArray();
        Assert.Throws<InvalidDataException>(() => PretOrdinaryFishing.Parse(json.ToJsonString()));
        json = JsonNode.Parse(Json)!.AsObject();
        json["tables"]!.AsArray().Add(json["tables"]![0]!.DeepClone());
        Assert.Throws<InvalidDataException>(() => PretOrdinaryFishing.Parse(json.ToJsonString()));
        json = JsonNode.Parse(Json)!.AsObject();
        json["tables"]![0]!["table"]!["slots"]![0]!["maximumLevel"] = 101;
        Assert.Throws<InvalidDataException>(() => PretOrdinaryFishing.Parse(json.ToJsonString()));
        json = JsonNode.Parse(Json)!.AsObject();
        json["tables"]![0]!["table"]!["map"]!["value"] = "map.safari-zone-center";
        Assert.Throws<InvalidDataException>(() => PretOrdinaryFishing.Parse(json.ToJsonString()));
        json = JsonNode.Parse(Json)!.AsObject();
        json["tables"]!.AsArray().RemoveAt(0);
        Assert.Throws<InvalidDataException>(() => PretOrdinaryFishing.Parse(json.ToJsonString()),
            "A fishing header supplies every rod; one omitted rod must not become a no-bite map");
    }

    [TestMethod]
    public void CurrentCompositionIncludesEveryMountedSourceTableForEveryRod()
    {
        var catalog = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var source = PretOrdinaryFishing.Parse(Json);
        foreach (var rod in Enum.GetValues<FishingRod>())
        {
            var expected = source.Where(row => row.Rod == rod && catalog.Maps.ContainsKey(row.Table.Map)).ToArray();
            Assert.AreEqual(43, expected.Length);
            CollectionAssert.AreEquivalent(expected.Select(row => row.Table.Map).ToArray(),
                catalog.FishingTables.Tables.Keys.Where(k => k.Rod == rod).Select(k => k.Map).ToArray());
            foreach (var row in expected)
                Assert.IsFalse(catalog.FishingTables.EmptyMaps.Contains(new(rod, row.Table.Map)));
            Assert.IsTrue(catalog.FishingTables.HasTable(rod, new("map.fuchsia-city")), "A source header cannot become a no-bite map");
            Assert.IsFalse(catalog.FishingTables.IsAdmittedMap(rod, new("map.one-island-treasure-beach")));
        }
    }
}
